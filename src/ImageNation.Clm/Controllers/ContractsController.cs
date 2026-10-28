using System.Globalization;
using System.Text;
using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class ContractsController(ICurrentUser current, AppDbContext db, ContractEditor editor, ContractWorkflow workflow, FileStorage files) : AppController(current)
{
    private const int PageSize = 25;

    public async Task<IActionResult> Index(ContractFilter filter)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        if (filter.Page < 1) filter.Page = 1;
        var query = Apply(db.Contracts.VisibleTo(user), filter);
        var total = await query.CountAsync();
        var rows = await QueuesController.ProjectRows(query.OrderByDescending(c => c.UpdatedAt))
            .Skip((filter.Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
        return View(new ContractIndexPage
        {
            Filter = filter,
            Rows = rows,
            Page = filter.Page,
            Total = total,
            PageSize = PageSize,
            Projects = await db.Projects.OrderBy(p => p.Name).ToListAsync(),
            Types = await db.AgreementTypes.Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToListAsync(),
            Lawyers = await db.Users.Where(u => u.Role == UserRole.Lawyer && u.IsActive).OrderBy(u => u.Name).ToListAsync(),
            Departments = await db.Users.Select(u => u.Department).Distinct().OrderBy(d => d).ToListAsync(),
            CanExport = Access.CanExport(user)
        });
    }

    public async Task<IActionResult> Export(ContractFilter filter)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        if (!Access.CanExport(user)) return Forbid();
        var rows = await QueuesController.ProjectRows(Apply(db.Contracts.VisibleTo(user), filter).OrderBy(c => c.PublicId)).ToListAsync();
        var csv = new StringBuilder();
        csv.AppendLine("Contract ID,Name,Project,Type,Party,Department,Lawyer,Owner,Status,Reporting status,Total value,Currency,End date");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",",
                Csv(row.PublicId), Csv(row.ContractName), Csv(row.Project), Csv(row.Type), Csv(row.Party),
                Csv(row.Department), Csv(row.Lawyer), Csv(row.Owner), Csv(Labels.Text(row.Status)),
                Csv(Labels.Text(Labels.ReportingOf(row.Status))),
                row.Value.ToString("0.00", CultureInfo.InvariantCulture), row.Currency,
                row.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "image-nation-contracts.csv");
    }

    public async Task<IActionResult> Create()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        return View("Form", await editor.PageAsync(user, new StartSheetForm(), null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StartSheetForm form)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        if (!ModelState.IsValid)
        {
            return View("Form", await editor.PageAsync(user, form, null));
        }

        var result = await editor.CreateAsync(form, user);
        if (!result.Ok)
        {
            Apply(result);
            return View("Form", await editor.PageAsync(user, form, null));
        }

        Flash(result);
        return RedirectToAction(nameof(Details), new { id = result.ContractId });
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var contract = await LoadSheetAsync(id, user);
        if (contract == null) return NotFound();
        if (!Access.CanEdit(user, contract))
        {
            TempData["Error"] = "This start sheet is locked.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("Form", await editor.PageAsync(user, ToForm(contract), contract));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StartSheetForm form)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var existing = form.Id == null ? null : await LoadSheetAsync(form.Id.Value, user);
        if (existing == null) return NotFound();
        if (!ModelState.IsValid)
        {
            return View("Form", await editor.PageAsync(user, form, existing));
        }

        var result = await editor.UpdateAsync(form, user);
        if (!result.Ok)
        {
            Apply(result);
            return View("Form", await editor.PageAsync(user, form, existing));
        }

        Flash(result);
        return RedirectToAction(nameof(Details), new { id = result.ContractId });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var contract = await db.Contracts.VisibleTo(user)
            .AsSplitQuery()
            .Include(c => c.AgreementType)
            .Include(c => c.InsEntity)
            .Include(c => c.ProjectTitle)
            .Include(c => c.NonSupplierCategory)
            .Include(c => c.Initiator)
            .Include(c => c.AssignedLawyer)
            .Include(c => c.ParentContract)
            .Include(c => c.Versions).ThenInclude(v => v.CreatedBy)
            .Include(c => c.ApprovalSteps).ThenInclude(s => s.Approver)
            .Include(c => c.Signatories)
            .Include(c => c.Obligations).ThenInclude(o => o.Owner)
            .Include(c => c.Rights)
            .Include(c => c.CriticalDates)
            .Include(c => c.RelatedDocuments).ThenInclude(d => d.UploadedBy)
            .Include(c => c.Assignments).ThenInclude(a => a.ToLawyer)
            .Include(c => c.Assignments).ThenInclude(a => a.FromLawyer)
            .Include(c => c.Assignments).ThenInclude(a => a.AssignedBy)
            .Include(c => c.AuditEvents).ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return NotFound();

        await db.Notices.Where(n => n.UserId == user.Id && n.ContractId == id && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        RoutePlan? live = null;
        decimal? usd = null;
        try
        {
            var policy = await db.DoaPolicies.SingleAsync();
            var rates = await db.FxRates.ToListAsync();
            var people = await db.Users.Where(u => u.IsActive).ToListAsync();
            live = DoaRouter.Plan(contract, policy, rates, people);
            usd = live.ValueUsd;
        }
        catch (InvalidOperationException)
        {
            live = null;
        }

        BcPayload? bc = null;
        if (Labels.IsPostExecution(contract.LifecycleStatus))
        {
            bc = new BcPayload(
                contract.PublicId,
                contract.OtherPartyName,
                contract.Department,
                contract.ProjectTitle.Name,
                contract.TotalValue,
                contract.Currency.ToString(),
                contract.StartDate,
                contract.EndDate,
                Labels.Text(contract.LifecycleStatus),
                contract.BcLastSyncAt);
        }

        return View(new ContractDetailsPage
        {
            Contract = contract,
            Viewer = user,
            LiveRoute = live,
            ValueUsd = usd,
            BusinessCentral = bc,
            Lawyers = await db.Users.Where(u => u.Role == UserRole.Lawyer && u.IsActive).OrderBy(u => u.Name).ToListAsync(),
            People = await db.Users.Where(u => u.IsActive && u.Role != UserRole.Admin).OrderBy(u => u.Name).ToListAsync(),
            Children = await db.Contracts.Where(c => c.ParentContractId == contract.Id).OrderBy(c => c.PublicId).ToListAsync(),
            ActionableStep = contract.ApprovalSteps.SingleOrDefault(s => s.IsActionable && s.Outcome == ApprovalOutcome.Pending && s.ApproverId == user.Id)
        });
    }

    public async Task<IActionResult> DownloadVersion(Guid id)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var version = await db.Versions.FirstOrDefaultAsync(v => v.Id == id);
        if (version == null || !await db.Contracts.VisibleTo(user).AnyAsync(c => c.Id == version.ContractId)) return NotFound();
        var path = files.Resolve(version.StoredPath);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", version.FileName);
    }

    public async Task<IActionResult> DownloadRelated(Guid id)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var document = await db.RelatedDocuments.FirstOrDefaultAsync(d => d.Id == id);
        if (document == null || !await db.Contracts.VisibleTo(user).AnyAsync(c => c.Id == document.ContractId)) return NotFound();
        var path = files.Resolve(document.StoredPath);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/octet-stream", document.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Accept(Guid id) => Act(id, user => workflow.AcceptAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Return(Guid id, string? note) => Act(id, user => workflow.ReturnAsync(id, user, note));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Resubmit(Guid id) => Act(id, user => workflow.ResubmitAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Assign(Guid id, Guid lawyerId, string? reason) => Act(id, user => workflow.AssignAsync(id, user, lawyerId, reason));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> BeginDrafting(Guid id) => Act(id, user => workflow.BeginDraftingAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> MarkNegotiation(Guid id) => Act(id, user => workflow.MarkNegotiationAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ResumeDrafting(Guid id) => Act(id, user => workflow.ResumeDraftingAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> UploadVersion(Guid id, IFormFile? file, string? changeNote) => Act(id, user => workflow.UploadVersionAsync(id, user, file, changeNote));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SubmitApproval(Guid id) => Act(id, user => workflow.SubmitForApprovalAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Decide(Guid id, string? decision, string? comment) => Act(id, user => workflow.DecideAsync(id, user, decision, comment));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> AdHoc(Guid id, Guid approverId, string? reason) => Act(id, user => workflow.AddAdHocAsync(id, user, approverId, reason));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Withdraw(Guid id, string? reason) => Act(id, user => workflow.WithdrawAsync(id, user, reason));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SendSignature(Guid id, List<SignerInput>? signers) => Act(id, user => workflow.SendForSignatureAsync(id, user, signers ?? []));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SignNext(Guid id) => Act(id, user => workflow.SignNextAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> ManualExecute(Guid id, DateOnly? executionDate, string? signatories, IFormFile? file) => Act(id, user => workflow.ManualExecuteAsync(id, user, executionDate, signatories, file));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> AddObligation(Guid id, string? description, Guid ownerId, DateOnly? due, MilestoneKind kind, bool mandatory) => Act(id, user => workflow.AddObligationAsync(id, user, description, ownerId, due, kind, mandatory));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> CompleteObligation(Guid id, Guid obligationId, string? evidence) => Act(id, user => workflow.CompleteObligationAsync(id, user, obligationId, evidence));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> AddRights(Guid id, string? rightType, string? territory, DateOnly? start, DateOnly? end, string? exclusivity, string? counterparty) => Act(id, user => workflow.AddRightsAsync(id, user, rightType, territory, start, end, exclusivity, counterparty));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> AddCriticalDate(Guid id, CriticalDateKind kind, DateOnly? date, string? note) => Act(id, user => workflow.AddCriticalDateAsync(id, user, kind, date, note));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> AddRelated(Guid id, RelatedDocumentKind kind, IFormFile? file) => Act(id, user => workflow.AddRelatedAsync(id, user, kind, file));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> StartRenewal(Guid id) => Act(id, user => workflow.StartRenewalAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Supersede(Guid id, Guid childId) => Act(id, user => workflow.SupersedeAsync(id, user, childId));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Expire(Guid id) => Act(id, user => workflow.ExpireAsync(id, user));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Close(Guid id, string? reason) => Act(id, user => workflow.CloseAsync(id, user, reason));
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> SyncBc(Guid id) => Act(id, user => workflow.SyncBcAsync(id, user));

    private async Task<IActionResult> Act(Guid id, Func<AppUser, Task<OpResult>> action)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        Flash(await action(user));
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<Contract?> LoadSheetAsync(Guid id, AppUser user) =>
        await db.Contracts.VisibleTo(user)
            .Include(c => c.RelatedDocuments)
            .Include(c => c.Initiator)
            .FirstOrDefaultAsync(c => c.Id == id);

    private static StartSheetForm ToForm(Contract contract) => new()
    {
        Id = contract.Id,
        ContractName = contract.ContractName,
        AgreementTypeId = contract.AgreementTypeId,
        RequestNature = contract.RequestNature,
        InitiationMethod = contract.InitiationMethod,
        TemplateName = contract.TemplateName,
        Description = contract.Description,
        StartDate = contract.StartDate,
        EndDate = contract.EndDate,
        InsEntityId = contract.InsEntityId,
        ProjectChoice = contract.ProjectTitleId.ToString(),
        ProjectOwner = contract.ProjectOwner,
        OtherPartyName = contract.OtherPartyName,
        OtherPartyReference = contract.OtherPartyReference,
        OtherPartyKind = contract.OtherPartyKind,
        NonSupplierCategoryId = contract.NonSupplierCategoryId,
        Address = contract.Address,
        ResidenceRegion = contract.ResidenceRegion,
        SpecificPlace = contract.SpecificPlace,
        Country = contract.Country,
        Currency = contract.Currency,
        ContractValueExVat = contract.ContractValueExVat,
        PaymentTerms = contract.PaymentTerms,
        PayerInvoicingEntity = contract.PayerInvoicingEntity,
        PayeeInvoicingEntity = contract.PayeeInvoicingEntity,
        InternationalCoProduction = contract.InternationalCoProduction,
        ParentContractId = contract.ParentContractId
    };

    private void Apply(OpResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message) && result.Fields.Count == 0)
        {
            ModelState.AddModelError(string.Empty, result.Message);
        }

        foreach (var field in result.Fields)
        {
            var key = field.Key;
            if (!string.IsNullOrEmpty(key) && !key.Contains('.'))
            {
                key = "form." + key;
            }

            ModelState.AddModelError(key, field.Value);
        }
    }

    private static IQueryable<Contract> Apply(IQueryable<Contract> query, ContractFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var q = filter.Q.Trim().ToLower();
            query = query.Where(c =>
                c.PublicId.ToLower().Contains(q)
                || c.ContractName.ToLower().Contains(q)
                || c.OtherPartyName.ToLower().Contains(q));
        }

        if (filter.ProjectId != null) query = query.Where(c => c.ProjectTitleId == filter.ProjectId);
        if (filter.TypeId != null) query = query.Where(c => c.AgreementTypeId == filter.TypeId);
        if (!string.IsNullOrWhiteSpace(filter.Department)) query = query.Where(c => c.Department == filter.Department);
        if (filter.LawyerId != null) query = query.Where(c => c.AssignedLawyerId == filter.LawyerId);
        if (filter.Status != null) query = query.Where(c => c.LifecycleStatus == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Owner))
        {
            var owner = filter.Owner.Trim().ToLower();
            query = query.Where(c => c.ProjectOwner.ToLower().Contains(owner));
        }

        if (filter.EndFrom != null) query = query.Where(c => c.EndDate >= filter.EndFrom);
        if (filter.EndTo != null) query = query.Where(c => c.EndDate <= filter.EndTo);
        return query;
    }

    private static string Csv(string? value)
    {
        var text = value ?? "";
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
