using System.Data;
using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Services;

public class ContractEditor(AppDbContext db, FileStorage files)
{
    public async Task<StartSheetPage> PageAsync(AppUser user, StartSheetForm? form, Contract? existing)
    {
        form ??= new StartSheetForm();
        var types = await db.AgreementTypes.OrderBy(t => t.SortOrder).ToListAsync();
        if (existing != null)
        {
            types = types.Where(t => t.IsActive || t.Id == existing.AgreementTypeId).ToList();
        }
        else
        {
            types = types.Where(t => t.IsActive).ToList();
        }

        var categories = await db.NonSupplierCategories.OrderBy(c => c.SortOrder).ToListAsync();
        categories = existing == null
            ? categories.Where(c => c.IsActive).ToList()
            : categories.Where(c => c.IsActive || c.Id == existing.NonSupplierCategoryId).ToList();

        var parents = await db.Contracts.VisibleTo(user)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContractOption(c.Id, c.PublicId + " — " + c.ContractName))
            .ToListAsync();
        if (existing != null)
        {
            parents = parents.Where(p => p.Id != existing.Id).ToList();
        }

        string? identityName = null;
        if (existing != null)
        {
            identityName = existing.RelatedDocuments
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefault(d => d.Kind is RelatedDocumentKind.TradeLicence or RelatedDocumentKind.Passport)
                ?.FileName;
        }

        return new StartSheetPage
        {
            Form = form,
            Department = existing?.Department ?? user.Department,
            InitiatorName = existing?.Initiator.Name ?? user.Name,
            LockInitiation = existing != null,
            RequireIdentity = existing == null,
            ExistingIdentityName = identityName,
            AgreementTypes = types,
            InsEntities = await db.InsEntities.Where(e => e.IsActive).OrderBy(e => e.SortOrder).ToListAsync(),
            Categories = categories,
            Projects = await db.Projects.OrderBy(p => p.Name).ToListAsync(),
            Parents = parents
        };
    }

    public async Task<OpResult> CreateAsync(StartSheetForm form, AppUser user)
    {
        var check = await ValidateAsync(form, user, creating: true, existing: null);
        if (!check.Ok)
        {
            return check;
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var project = await ResolveProjectAsync(form, user);
        if (project == null)
        {
            return OpResult.Field(nameof(form.ProjectChoice), "Select a title or enter a new one.");
        }

        var now = DateTimeOffset.UtcNow;
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            PublicId = await NextPublicIdAsync(now.Year),
            InitiatorId = user.Id,
            Department = user.Department,
            LifecycleStatus = LifecycleStatus.Requested,
            DocumentLocked = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(contract, form, project);
        db.Contracts.Add(contract);

        var version = await FirstVersionAsync(contract, form, user, now);
        if (version.Error != null)
        {
            return OpResult.Field(version.Field ?? nameof(form.DraftFile), version.Error);
        }

        contract.Versions.Add(version.Version!);
        var identity = await IdentityAsync(contract, form, user, now, required: true);
        if (identity.Error != null)
        {
            return OpResult.Field(nameof(form.IdentityDocument), identity.Error);
        }

        if (identity.Document != null)
        {
            contract.RelatedDocuments.Add(identity.Document);
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            ActorId = user.Id,
            Action = "Request created",
            Detail = $"{contract.PublicId} raised by {user.Name} ({user.Department}) as {Labels.Text(contract.InitiationMethod)}.",
            CreatedAt = now
        });
        NotifyLegal(contract, $"{user.Name} submitted {contract.PublicId} {contract.ContractName}.");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return OpResult.Success($"{contract.PublicId} is in the legal queue.", contract.Id);
    }

    public async Task<OpResult> UpdateAsync(StartSheetForm form, AppUser user)
    {
        var contract = await db.Contracts
            .Include(c => c.Versions)
            .Include(c => c.RelatedDocuments)
            .Include(c => c.Initiator)
            .FirstOrDefaultAsync(c => c.Id == form.Id);
        if (contract == null || !await db.Contracts.VisibleTo(user).AnyAsync(c => c.Id == contract.Id))
        {
            return OpResult.Fail("That agreement is not available to you.");
        }

        if (!Access.CanEdit(user, contract))
        {
            return OpResult.Fail("This start sheet is locked.");
        }

        var check = await ValidateAsync(form, user, creating: false, existing: contract);
        if (!check.Ok)
        {
            return check;
        }

        var project = await ResolveProjectAsync(form, user);
        if (project == null)
        {
            return OpResult.Field(nameof(form.ProjectChoice), "Select a title or enter a new one.");
        }

        Apply(contract, form, project);
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        var identity = await IdentityAsync(contract, form, user, contract.UpdatedAt, required: false);
        if (identity.Error != null)
        {
            return OpResult.Field(nameof(form.IdentityDocument), identity.Error);
        }

        if (identity.Document != null)
        {
            contract.RelatedDocuments.Add(identity.Document);
        }

        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            ActorId = user.Id,
            Action = "Start sheet updated",
            Detail = $"{user.Name} updated the start sheet.",
            CreatedAt = contract.UpdatedAt
        });
        await db.SaveChangesAsync();
        return OpResult.Success("Start sheet saved.", contract.Id);
    }

    private async Task<OpResult> ValidateAsync(StartSheetForm form, AppUser user, bool creating, Contract? existing)
    {
        if (form.RequestNature is RequestNature nature && Labels.RequiresParent(nature))
        {
            if (form.ParentContractId == null)
            {
                return OpResult.Field(nameof(form.ParentContractId), "Name the agreement this one renews or changes.");
            }

            var parentOk = await db.Contracts.VisibleTo(user).AnyAsync(c => c.Id == form.ParentContractId && c.Id != form.Id);
            if (!parentOk)
            {
                return OpResult.Field(nameof(form.ParentContractId), "Choose a parent agreement you can see.");
            }
        }

        if (form.StartDate is DateOnly start && form.EndDate is DateOnly end && end < start)
        {
            return OpResult.Field(nameof(form.EndDate), "The end date cannot be before the start date.");
        }

        if (form.ProjectChoice == "new" && string.IsNullOrWhiteSpace(form.NewProjectName))
        {
            return OpResult.Field(nameof(form.NewProjectName), "Enter the new title.");
        }

        if (form.ProjectChoice != "new" && !Guid.TryParse(form.ProjectChoice, out _))
        {
            return OpResult.Field(nameof(form.ProjectChoice), "Select a title or create one.");
        }

        if (creating && form.InitiationMethod == InitiationMethod.Template && string.IsNullOrWhiteSpace(form.TemplateName))
        {
            return OpResult.Field(nameof(form.TemplateName), "Select an approved template.");
        }

        if (creating && form.InitiationMethod == InitiationMethod.UploadedDraft && (form.DraftFile == null || form.DraftFile.Length == 0))
        {
            return OpResult.Field(nameof(form.DraftFile), "Upload the third-party draft.");
        }

        if (creating && (form.IdentityDocument == null || form.IdentityDocument.Length == 0))
        {
            var kind = form.OtherPartyKind == PartyKind.Individual ? "passport" : "trade licence";
            return OpResult.Field(nameof(form.IdentityDocument), $"Upload a valid {kind}.");
        }

        if (existing != null && form.InitiationMethod != existing.InitiationMethod)
        {
            return OpResult.Field(nameof(form.InitiationMethod), "The way the draft started is part of the audit record.");
        }

        return OpResult.Success("");
    }

    private static void Apply(Contract contract, StartSheetForm form, ProjectTitle project)
    {
        contract.ContractName = form.ContractName.Trim();
        contract.AgreementTypeId = form.AgreementTypeId!.Value;
        contract.RequestNature = form.RequestNature!.Value;
        contract.InitiationMethod = form.InitiationMethod!.Value;
        contract.TemplateName = form.InitiationMethod == InitiationMethod.Template ? form.TemplateName?.Trim() : null;
        contract.Description = form.Description?.Trim();
        contract.StartDate = form.StartDate;
        contract.EndDate = form.EndDate;
        contract.InsEntityId = form.InsEntityId!.Value;
        contract.ProjectTitleId = project.Id;
        contract.ProjectOwner = form.ProjectOwner.Trim();
        contract.OtherPartyName = form.OtherPartyName.Trim();
        contract.OtherPartyReference = string.IsNullOrWhiteSpace(form.OtherPartyReference)
            ? contract.OtherPartyReference
            : form.OtherPartyReference.Trim();
        if (string.IsNullOrWhiteSpace(contract.OtherPartyReference))
        {
            contract.OtherPartyReference = "CP-" + Random.Shared.Next(100000, 999999);
        }

        contract.OtherPartyKind = form.OtherPartyKind!.Value;
        contract.NonSupplierCategoryId = form.NonSupplierCategoryId!.Value;
        contract.Address = form.Address.Trim();
        contract.ResidenceRegion = form.ResidenceRegion!.Value;
        contract.SpecificPlace = form.SpecificPlace.Trim();
        contract.Country = form.Country?.Trim();
        contract.Currency = form.Currency!.Value;
        contract.ContractValueExVat = form.ContractValueExVat!.Value;
        contract.VatAmount = Money.VatOn(contract.ContractValueExVat);
        contract.TotalValue = Money.Total(contract.ContractValueExVat);
        contract.PaymentTerms = form.PaymentTerms.Trim();
        contract.PayerInvoicingEntity = form.PayerInvoicingEntity.Trim();
        contract.PayeeInvoicingEntity = form.PayeeInvoicingEntity.Trim();
        contract.InternationalCoProduction = form.InternationalCoProduction;
        contract.ParentContractId = Labels.RequiresParent(contract.RequestNature) ? form.ParentContractId : null;
    }

    private async Task<ProjectTitle?> ResolveProjectAsync(StartSheetForm form, AppUser user)
    {
        if (form.ProjectChoice == "new")
        {
            var name = form.NewProjectName!.Trim();
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
            if (existing != null)
            {
                return existing;
            }

            var project = new ProjectTitle
            {
                Id = Guid.NewGuid(),
                Name = name,
                InternalCode = "NEW-" + DateTime.UtcNow.ToString("yyMMddHHmmss"),
                ProductionEntity = "To be confirmed",
                Status = "Active",
                BusinessOwner = string.IsNullOrWhiteSpace(form.ProjectOwner) ? user.Name : form.ProjectOwner.Trim()
            };
            db.Projects.Add(project);
            return project;
        }

        if (!Guid.TryParse(form.ProjectChoice, out var id))
        {
            return null;
        }

        return await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
    }

    private async Task<(DocumentVersion? Version, string? Error, string? Field)> FirstVersionAsync(Contract contract, StartSheetForm form, AppUser user, DateTimeOffset now)
    {
        try
        {
            StoredFile stored;
            string note;
            if (form.InitiationMethod == InitiationMethod.UploadedDraft)
            {
                stored = await files.SaveAsync(form.DraftFile!, contract.Id);
                note = "Third-party draft received at intake.";
            }
            else if (form.InitiationMethod == InitiationMethod.Template)
            {
                var body = Shell(contract, form.TemplateName!);
                stored = await files.SaveTextAsync(contract.Id, "template-shell.txt", body);
                note = $"Opened from the approved template “{form.TemplateName}”.";
            }
            else
            {
                var body = Shell(contract, "From scratch");
                stored = await files.SaveTextAsync(contract.Id, "from-scratch.txt", body);
                note = "Blank controlled shell. Clause language is authored by Legal.";
            }

            return (new DocumentVersion
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                VersionNumber = 1,
                FileName = stored.FileName,
                StoredPath = stored.RelativePath,
                Kind = VersionKind.WorkingDraft,
                IsCurrent = true,
                IsLocked = false,
                ChangeNote = note,
                CreatedById = user.Id,
                CreatedAt = now
            }, null, null);
        }
        catch (InvalidOperationException ex)
        {
            return (null, ex.Message, nameof(form.DraftFile));
        }
    }

    private async Task<(RelatedDocument? Document, string? Error)> IdentityAsync(Contract contract, StartSheetForm form, AppUser user, DateTimeOffset now, bool required)
    {
        if (form.IdentityDocument == null || form.IdentityDocument.Length == 0)
        {
            return required ? (null, "Upload the identity document.") : (null, null);
        }

        try
        {
            var stored = await files.SaveAsync(form.IdentityDocument, contract.Id);
            return (new RelatedDocument
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                Kind = form.OtherPartyKind == PartyKind.Individual ? RelatedDocumentKind.Passport : RelatedDocumentKind.TradeLicence,
                FileName = stored.FileName,
                StoredPath = stored.RelativePath,
                UploadedById = user.Id,
                UploadedAt = now
            }, null);
        }
        catch (InvalidOperationException ex)
        {
            return (null, ex.Message);
        }
    }

    private static string Shell(Contract contract, string source) =>
        $"""
        IMAGE NATION ABU DHABI
        Controlled draft shell — not precedent clause language
        Source: {source}
        Contract: {contract.ContractName}
        Other party: {contract.OtherPartyName}
        Value exclusive of VAT: {contract.ContractValueExVat:N2} {contract.Currency}
        Legal authors the operative wording into the next version.
        """;

    private async Task<string> NextPublicIdAsync(int year)
    {
        var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Year == year);
        if (seq == null)
        {
            seq = new ContractNumberSequence { Year = year, LastNumber = 0 };
            db.NumberSequences.Add(seq);
        }

        seq.LastNumber++;
        return $"IN-{year}-{seq.LastNumber:0000}";
    }

    private void NotifyLegal(Contract contract, string message)
    {
        var recipients = db.Users.Local.Where(u => u.IsActive && u.Role is UserRole.LegalTriage or UserRole.HeadOfLegal).ToList();
        if (recipients.Count == 0)
        {
            recipients = db.Users.Where(u => u.IsActive && (u.Role == UserRole.LegalTriage || u.Role == UserRole.HeadOfLegal)).ToList();
        }

        foreach (var person in recipients)
        {
            db.Notices.Add(new Notice
            {
                Id = Guid.NewGuid(),
                UserId = person.Id,
                ContractId = contract.Id,
                Message = message,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
