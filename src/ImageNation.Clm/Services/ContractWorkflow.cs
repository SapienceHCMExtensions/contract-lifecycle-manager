using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Services;

public class ContractWorkflow(AppDbContext db, FileStorage files)
{
    public async Task<OpResult> AcceptAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanTriage(user)) return OpResult.Fail("Only Legal can accept a request.");
        if (contract.LifecycleStatus != LifecycleStatus.Requested) return OpResult.Fail("Only a request waiting on triage can be accepted.");
        ApplyStatus(contract, LifecycleStatus.LegalReview);
        Audit(contract, user, "Accepted", "Moved into legal review on the same record.");
        await db.SaveChangesAsync();
        return OpResult.Success("Accepted into legal review.");
    }

    public async Task<OpResult> ReturnAsync(Guid id, AppUser user, string? note)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanTriage(user) && !Access.CanDraft(user, contract)) return OpResult.Fail("You cannot return this request.");
        if (contract.LifecycleStatus is not (LifecycleStatus.LegalReview or LifecycleStatus.Drafting or LifecycleStatus.Negotiation))
            return OpResult.Fail("This agreement is not with Legal.");
        if (string.IsNullOrWhiteSpace(note)) return OpResult.Fail("Say what needs clarifying.");
        contract.ClarificationNote = note.Trim();
        ApplyStatus(contract, LifecycleStatus.Requested);
        Audit(contract, user, "Returned for clarification", note.Trim());
        Notify(contract.InitiatorId, contract, $"{contract.PublicId} was returned: {note.Trim()}");
        await db.SaveChangesAsync();
        return OpResult.Success("Returned to the requestor. The record was not duplicated.");
    }

    public async Task<OpResult> ResubmitAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (user.Id != contract.InitiatorId && !Access.CanTriage(user)) return OpResult.Fail("Only the requestor can resubmit this.");
        if (contract.LifecycleStatus != LifecycleStatus.Requested || string.IsNullOrWhiteSpace(contract.ClarificationNote))
            return OpResult.Fail("Nothing is waiting to be resubmitted.");
        ApplyStatus(contract, LifecycleStatus.LegalReview);
        Audit(contract, user, "Resubmitted", "Clarification sent back to Legal on the same record.");
        NotifyRole(UserRole.LegalTriage, contract, $"{contract.PublicId} was resubmitted.");
        await db.SaveChangesAsync();
        return OpResult.Success("Resubmitted to Legal.");
    }

    public async Task<OpResult> AssignAsync(Guid id, AppUser user, Guid lawyerId, string? reason)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanTriage(user)) return OpResult.Fail("Only Legal can assign a lawyer.");
        if (contract.DocumentLocked) return OpResult.Fail("Assignments wait until the document is unlocked.");
        var lawyer = await db.Users.FirstOrDefaultAsync(u => u.Id == lawyerId && u.IsActive && u.Role == UserRole.Lawyer);
        if (lawyer == null) return OpResult.Fail("Choose a lawyer.");
        if (contract.AssignedLawyerId == lawyer.Id) return OpResult.Fail("That lawyer already holds this matter.");
        if (contract.AssignedLawyerId != null && string.IsNullOrWhiteSpace(reason)) return OpResult.Fail("Enter a reason for the reassignment.");
        var previous = contract.AssignedLawyerId;
        contract.AssignedLawyerId = lawyer.Id;
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        db.Assignments.Add(new AssignmentEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            FromLawyerId = previous,
            ToLawyerId = lawyer.Id,
            AssignedById = user.Id,
            Reason = reason?.Trim(),
            AssignedAt = DateTimeOffset.UtcNow
        });
        Audit(contract, user, previous == null ? "Lawyer assigned" : "Lawyer reassigned", $"{lawyer.Name}. {reason}".Trim());
        Notify(lawyer.Id, contract, $"{contract.PublicId} is now in your queue.");
        if (previous != null) Notify(previous.Value, contract, $"{contract.PublicId} was reassigned to {lawyer.Name}.");
        await db.SaveChangesAsync();
        return OpResult.Success($"Assigned to {lawyer.Name}.");
    }

    public async Task<OpResult> BeginDraftingAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanTriage(user) && !Access.CanDraft(user, contract)) return OpResult.Fail("You cannot open drafting.");
        if (contract.LifecycleStatus != LifecycleStatus.LegalReview) return OpResult.Fail("Accept the request into legal review first.");
        if (contract.AssignedLawyerId == null) return OpResult.Fail("Assign a lawyer before substantive review.");
        ApplyStatus(contract, LifecycleStatus.Drafting);
        Audit(contract, user, "Drafting opened", "Substantive legal review started.");
        await db.SaveChangesAsync();
        return OpResult.Success("Drafting is open.");
    }

    public async Task<OpResult> MarkNegotiationAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("Only the assigned lawyer or Legal can mark negotiation.");
        if (contract.LifecycleStatus != LifecycleStatus.Drafting) return OpResult.Fail("Negotiation starts from drafting.");
        ApplyStatus(contract, LifecycleStatus.Negotiation);
        Audit(contract, user, "Negotiation", "Terms are still open.");
        await db.SaveChangesAsync();
        return OpResult.Success("Marked as in negotiation.");
    }

    public async Task<OpResult> ResumeDraftingAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("You cannot resume drafting.");
        if (contract.LifecycleStatus != LifecycleStatus.Returned) return OpResult.Fail("Only a returned agreement can resume drafting.");
        ApplyStatus(contract, LifecycleStatus.Drafting);
        Audit(contract, user, "Drafting resumed", "Returned agreement is editable again.");
        await db.SaveChangesAsync();
        return OpResult.Success("Drafting resumed.");
    }

    public async Task<OpResult> UploadVersionAsync(Guid id, AppUser user, IFormFile? file, string? changeNote)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanEdit(user, contract) && !Access.CanDraft(user, contract)) return OpResult.Fail("You cannot add a draft.");
        if (contract.DocumentLocked || !Labels.IsEditable(contract.LifecycleStatus)) return OpResult.Fail("The document is locked.");
        if (file == null || file.Length == 0) return OpResult.Fail("Choose a file.");
        if (string.IsNullOrWhiteSpace(changeNote)) return OpResult.Fail("Describe what changed in this version.");
        StoredFile stored;
        try
        {
            stored = await files.SaveAsync(file, contract.Id);
        }
        catch (InvalidOperationException ex)
        {
            return OpResult.Fail(ex.Message);
        }

        var next = contract.Versions.Select(v => v.VersionNumber).DefaultIfEmpty(0).Max() + 1;
        foreach (var version in contract.Versions.Where(v => v.IsCurrent))
        {
            version.IsCurrent = false;
            version.IsLocked = true;
            version.Kind = VersionKind.Historical;
        }

        contract.Versions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            VersionNumber = next,
            FileName = stored.FileName,
            StoredPath = stored.RelativePath,
            Kind = VersionKind.WorkingDraft,
            IsCurrent = true,
            IsLocked = false,
            ChangeNote = changeNote.Trim(),
            CreatedById = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        Audit(contract, user, "Draft uploaded", $"Version {next}. {changeNote.Trim()}");
        await db.SaveChangesAsync();
        return OpResult.Success($"Version {next} is now the working draft. Earlier versions stay read-only.");
    }

    public async Task<OpResult> SubmitForApprovalAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("Only the assigned lawyer or Legal can submit for approval.");
        if (contract.LifecycleStatus is not (LifecycleStatus.Drafting or LifecycleStatus.Negotiation or LifecycleStatus.Returned))
            return OpResult.Fail("Submit from drafting, negotiation, or a return.");
        if (contract.AssignedLawyerId == null) return OpResult.Fail("Assign a lawyer before approval.");
        if (!contract.Versions.Any(v => v.IsCurrent)) return OpResult.Fail("There is no working draft to approve.");

        var policy = await db.DoaPolicies.SingleAsync();
        var rates = await db.FxRates.ToListAsync();
        var people = await db.Users.Where(u => u.IsActive).ToListAsync();
        RoutePlan plan;
        try
        {
            plan = DoaRouter.Plan(contract, policy, rates, people);
        }
        catch (InvalidOperationException ex)
        {
            return OpResult.Fail(ex.Message);
        }

        var round = contract.ApprovalSteps.Select(s => s.Round).DefaultIfEmpty(0).Max() + 1;
        var sequence = 1;
        foreach (var step in plan.Steps)
        {
            contract.ApprovalSteps.Add(new ApprovalStep
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                Round = round,
                Sequence = sequence,
                RouteKind = step.Kind,
                ApproverId = step.Approver.Id,
                Outcome = ApprovalOutcome.Pending,
                IsActionable = sequence == 1,
                RouteExplanation = step.Why
            });
            if (sequence == 1)
            {
                Notify(step.Approver.Id, contract, $"{contract.PublicId} is waiting for your approval.");
            }

            sequence++;
        }

        contract.RouteExplanation = plan.Explanation;
        ApplyStatus(contract, LifecycleStatus.AwaitingApproval);
        Audit(contract, user, "Submitted for approval", plan.Explanation);
        await db.SaveChangesAsync();
        return OpResult.Success("Submitted. The working draft is locked.");
    }

    public async Task<OpResult> DecideAsync(Guid id, AppUser user, string? decision, string? comment)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (contract.LifecycleStatus != LifecycleStatus.AwaitingApproval) return OpResult.Fail("This agreement is not awaiting approval.");
        var step = contract.ApprovalSteps.SingleOrDefault(s => s.IsActionable && s.Outcome == ApprovalOutcome.Pending);
        if (step == null || step.ApproverId != user.Id) return OpResult.Fail("This decision is not waiting on you.");

        var approve = string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase);
        if (!approve && string.IsNullOrWhiteSpace(comment)) return OpResult.Fail("A rejection needs a comment.");

        step.Comment = comment?.Trim();
        step.DecidedAt = DateTimeOffset.UtcNow;
        step.IsActionable = false;

        if (!approve)
        {
            step.Outcome = ApprovalOutcome.Rejected;
            foreach (var later in contract.ApprovalSteps.Where(s => s.Round == step.Round && s.Outcome == ApprovalOutcome.Pending))
            {
                later.Outcome = ApprovalOutcome.Withdrawn;
                later.IsActionable = false;
            }

            ApplyStatus(contract, LifecycleStatus.Returned);
            Audit(contract, user, "Rejected", comment!.Trim());
            if (contract.AssignedLawyerId != null)
            {
                Notify(contract.AssignedLawyerId.Value, contract, $"{contract.PublicId} was rejected and unlocked.");
            }

            Notify(contract.InitiatorId, contract, $"{contract.PublicId} was not approved.");
            await db.SaveChangesAsync();
            return OpResult.Success("Rejected. The draft is editable again and the approval package is kept.");
        }

        step.Outcome = ApprovalOutcome.Approved;
        var next = contract.ApprovalSteps
            .Where(s => s.Round == step.Round && s.Outcome == ApprovalOutcome.Pending)
            .OrderBy(s => s.Sequence)
            .FirstOrDefault();
        if (next != null)
        {
            next.IsActionable = true;
            Notify(next.ApproverId, contract, $"{contract.PublicId} is waiting for your approval.");
            Audit(contract, user, "Approved", comment?.Trim() ?? "Approved.");
            await db.SaveChangesAsync();
            return OpResult.Success("Approved. The next approver has been asked.");
        }

        var current = contract.Versions.FirstOrDefault(v => v.IsCurrent);
        if (current != null)
        {
            current.Kind = VersionKind.ApprovedPackage;
            current.IsLocked = true;
        }

        ApplyStatus(contract, LifecycleStatus.Approved);
        Audit(contract, user, "Fully approved", comment?.Trim() ?? "Final approval recorded.");
        if (contract.AssignedLawyerId != null)
        {
            Notify(contract.AssignedLawyerId.Value, contract, $"{contract.PublicId} is approved and can go to signature.");
        }

        await db.SaveChangesAsync();
        return OpResult.Success("All required approvals are complete.");
    }

    public async Task<OpResult> AddAdHocAsync(Guid id, AppUser user, Guid approverId, string? reason)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user)) return OpResult.Fail("You cannot add an approver.");
        if (contract.LifecycleStatus != LifecycleStatus.AwaitingApproval) return OpResult.Fail("Ad hoc approvers are added while approval is open.");
        if (string.IsNullOrWhiteSpace(reason)) return OpResult.Fail("Enter why this approver is needed.");
        var approver = await db.Users.FirstOrDefaultAsync(u => u.Id == approverId && u.IsActive && u.Role != UserRole.Admin);
        if (approver == null) return OpResult.Fail("Choose an approver.");
        var current = contract.ApprovalSteps.SingleOrDefault(s => s.IsActionable && s.Outcome == ApprovalOutcome.Pending);
        if (current == null) return OpResult.Fail("There is no open approval step.");
        foreach (var step in contract.ApprovalSteps.Where(s => s.Round == current.Round && s.Sequence >= current.Sequence))
        {
            step.Sequence++;
        }

        current.IsActionable = false;
        contract.ApprovalSteps.Add(new ApprovalStep
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Round = current.Round,
            Sequence = current.Sequence - 1,
            RouteKind = ApprovalRouteKind.AdHoc,
            ApproverId = approver.Id,
            Outcome = ApprovalOutcome.Pending,
            IsActionable = true,
            AdHocReason = reason.Trim(),
            RouteExplanation = "Added for this deal"
        });
        Audit(contract, user, "Ad hoc approver", $"{approver.Name}. {reason.Trim()}");
        Notify(approver.Id, contract, $"You were added as an approver on {contract.PublicId}.");
        await db.SaveChangesAsync();
        return OpResult.Success($"{approver.Name} was inserted into the route.");
    }

    public async Task<OpResult> WithdrawAsync(Guid id, AppUser user, string? reason)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("You cannot withdraw this agreement.");
        if (contract.LifecycleStatus is not (LifecycleStatus.AwaitingApproval or LifecycleStatus.Approved or LifecycleStatus.AwaitingSignature or LifecycleStatus.PartiallySigned))
            return OpResult.Fail("Withdrawal is only available from approval or signature.");
        if (string.IsNullOrWhiteSpace(reason)) return OpResult.Fail("Enter why this is being withdrawn.");
        foreach (var step in contract.ApprovalSteps.Where(s => s.Outcome == ApprovalOutcome.Pending))
        {
            step.Outcome = ApprovalOutcome.Withdrawn;
            step.IsActionable = false;
        }

        foreach (var signer in contract.Signatories.Where(s => s.State == SignatureState.Pending))
        {
            signer.State = SignatureState.Superseded;
        }

        ApplyStatus(contract, LifecycleStatus.Drafting);
        Audit(contract, user, "Withdrawn", reason.Trim());
        await db.SaveChangesAsync();
        return OpResult.Success("Withdrawn to drafting. The document can be edited again.");
    }

    public async Task<OpResult> SendForSignatureAsync(Guid id, AppUser user, IReadOnlyList<SignerInput> signers)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("Only Legal can send for signature.");
        if (contract.LifecycleStatus != LifecycleStatus.Approved) return OpResult.Fail("Signature starts from an approved version.");
        var clean = signers.Where(s => !string.IsNullOrWhiteSpace(s.Name)).ToList();
        if (clean.Count == 0) return OpResult.Fail("Add at least one signatory.");
        var round = contract.Signatories.Select(s => s.Round).DefaultIfEmpty(0).Max() + 1;
        var order = 1;
        var now = DateTimeOffset.UtcNow;
        foreach (var signer in clean)
        {
            contract.Signatories.Add(new Signatory
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                Round = round,
                SigningOrder = order++,
                Name = signer.Name!.Trim(),
                Organization = signer.Organization?.Trim() ?? "",
                State = SignatureState.Pending,
                SentAt = now
            });
        }

        ApplyStatus(contract, LifecycleStatus.AwaitingSignature);
        Audit(contract, user, "Sent for signature", $"Simulated e-sign dispatch to {clean.Count} signatory(ies). DocuSign and Adobe Sign are not connected.");
        await db.SaveChangesAsync();
        return OpResult.Success("Sent. Signature progress will be recorded here.");
    }

    public async Task<OpResult> SignNextAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("Only Legal can record a simulated signature.");
        if (contract.LifecycleStatus is not (LifecycleStatus.AwaitingSignature or LifecycleStatus.PartiallySigned))
            return OpResult.Fail("Nothing is out for signature.");
        var round = contract.Signatories.Select(s => s.Round).DefaultIfEmpty(0).Max();
        var next = contract.Signatories.Where(s => s.Round == round && s.State == SignatureState.Pending).OrderBy(s => s.SigningOrder).FirstOrDefault();
        if (next == null) return OpResult.Fail("Every signatory in this round has already signed.");
        next.State = SignatureState.Signed;
        next.SignedAt = DateTimeOffset.UtcNow;
        var remaining = contract.Signatories.Any(s => s.Round == round && s.State == SignatureState.Pending);
        if (remaining)
        {
            ApplyStatus(contract, LifecycleStatus.PartiallySigned);
            Audit(contract, user, "Partial signature", $"{next.Name} signed. Others are still outstanding.");
            await db.SaveChangesAsync();
            return OpResult.Success($"{next.Name} is recorded as signed.");
        }

        var copy = await CaptureExecutedCopyAsync(contract, user, "Executed copy returned from simulated e-sign.");
        if (copy != null) return copy;
        contract.ExecutionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        contract.ExecutionSignatories = string.Join("; ", contract.Signatories.Where(s => s.Round == round).OrderBy(s => s.SigningOrder).Select(s => s.Name));
        ApplyStatus(contract, LifecycleStatus.ExecutedActive);
        Audit(contract, user, "Executed", "All signatures are in. The executed copy is locked.");
        Notify(contract.InitiatorId, contract, $"{contract.PublicId} is executed.");
        await db.SaveChangesAsync();
        return OpResult.Success("Fully executed. Post-signature monitoring is open.");
    }

    public async Task<OpResult> ManualExecuteAsync(Guid id, AppUser user, DateOnly? executionDate, string? signatories, IFormFile? file)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract)) return OpResult.Fail("Only Legal can record manual execution.");
        if (contract.LifecycleStatus is not (LifecycleStatus.Approved or LifecycleStatus.AwaitingSignature or LifecycleStatus.PartiallySigned))
            return OpResult.Fail("Manual execution is available after approval.");
        if (executionDate == null) return OpResult.Fail("Enter the execution date.");
        if (string.IsNullOrWhiteSpace(signatories)) return OpResult.Fail("Enter the signatories.");
        if (file == null || file.Length == 0) return OpResult.Fail("Upload the signed copy.");
        StoredFile stored;
        try
        {
            stored = await files.SaveAsync(file, contract.Id);
        }
        catch (InvalidOperationException ex)
        {
            return OpResult.Fail(ex.Message);
        }

        ArchiveCurrent(contract, VersionKind.ApprovedPackage);
        var next = contract.Versions.Select(v => v.VersionNumber).DefaultIfEmpty(0).Max() + 1;
        contract.Versions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            VersionNumber = next,
            FileName = stored.FileName,
            StoredPath = stored.RelativePath,
            Kind = VersionKind.ExecutedCopy,
            IsCurrent = true,
            IsLocked = true,
            ChangeNote = "Manually executed copy.",
            CreatedById = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        foreach (var signer in contract.Signatories.Where(s => s.State == SignatureState.Pending))
        {
            signer.State = SignatureState.Superseded;
        }

        contract.ExecutionDate = executionDate;
        contract.ExecutionSignatories = signatories.Trim();
        ApplyStatus(contract, LifecycleStatus.ExecutedActive);
        Audit(contract, user, "Manual execution", $"Uploaded by {user.Name} on {executionDate:yyyy-MM-dd}. Signatories: {signatories.Trim()}.");
        await db.SaveChangesAsync();
        return OpResult.Success("The uploaded copy is the authoritative executed version.");
    }

    public async Task<OpResult> AddObligationAsync(Guid id, AppUser user, string? description, Guid ownerId, DateOnly? due, MilestoneKind kind, bool mandatory)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanManageObligations(user, contract)) return OpResult.Fail("Obligations are added after execution.");
        if (string.IsNullOrWhiteSpace(description) || due == null) return OpResult.Fail("Enter a description and a due date.");
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == ownerId && u.IsActive);
        if (owner == null) return OpResult.Fail("Choose an owner.");
        contract.Obligations.Add(new Obligation
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Description = description.Trim(),
            OwnerId = owner.Id,
            DueDate = due.Value,
            MilestoneKind = kind,
            IsMandatory = mandatory,
            Status = ObligationStatus.Open
        });
        Audit(contract, user, "Obligation added", description.Trim());
        Notify(owner.Id, contract, $"You own a milestone on {contract.PublicId}: {description.Trim()}.");
        await db.SaveChangesAsync();
        return OpResult.Success("Obligation added.");
    }

    public async Task<OpResult> CompleteObligationAsync(Guid id, AppUser user, Guid obligationId, string? evidence)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        var obligation = contract.Obligations.FirstOrDefault(o => o.Id == obligationId);
        if (obligation == null) return OpResult.Fail("That obligation is not on this agreement.");
        if (obligation.Status != ObligationStatus.Open) return OpResult.Fail("That obligation is already closed.");
        if (user.Id != obligation.OwnerId && !Access.CanManageObligations(user, contract) && !Access.CanDraft(user, contract))
            return OpResult.Fail("Only the owner or Legal can complete this.");
        obligation.Status = ObligationStatus.Completed;
        obligation.EvidenceNote = evidence?.Trim();
        obligation.CompletedAt = DateTimeOffset.UtcNow;
        Audit(contract, user, "Obligation completed", obligation.Description);
        await db.SaveChangesAsync();
        return OpResult.Success("Obligation marked complete.");
    }

    public async Task<OpResult> AddRightsAsync(Guid id, AppUser user, string? rightType, string? territory, DateOnly? start, DateOnly? end, string? exclusivity, string? counterparty)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanManageObligations(user, contract) && !Access.CanDraft(user, contract)) return OpResult.Fail("Rights are recorded after execution.");
        if (contract.LifecycleStatus is not (LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview))
            return OpResult.Fail("Rights are recorded on an active agreement.");
        if (string.IsNullOrWhiteSpace(rightType) || string.IsNullOrWhiteSpace(territory) || start == null || end == null || string.IsNullOrWhiteSpace(exclusivity))
            return OpResult.Fail("Enter the right, territory, window, and exclusivity.");
        if (end < start) return OpResult.Fail("The window cannot end before it starts.");
        db.Rights.Add(new RightsWindow
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            RightType = rightType.Trim(),
            Territory = territory.Trim(),
            WindowStart = start.Value,
            WindowEnd = end.Value,
            Exclusivity = exclusivity.Trim(),
            Counterparty = counterparty?.Trim()
        });
        Audit(contract, user, "Rights recorded", $"{rightType.Trim()} / {territory.Trim()}");
        await db.SaveChangesAsync();
        return OpResult.Success("Rights window saved.");
    }

    public async Task<OpResult> AddCriticalDateAsync(Guid id, AppUser user, CriticalDateKind kind, DateOnly? date, string? note)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (contract.LifecycleStatus is not (LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview))
            return OpResult.Fail("Critical dates are tracked after execution.");
        if (!Access.CanManageObligations(user, contract) && !Access.CanDraft(user, contract)) return OpResult.Fail("You cannot add a date.");
        if (date == null) return OpResult.Fail("Enter the date.");
        db.CriticalDates.Add(new CriticalDate
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            Kind = kind,
            Date = date.Value,
            Note = note?.Trim()
        });
        Audit(contract, user, "Critical date", $"{Labels.Text(kind)} {date:yyyy-MM-dd}");
        await db.SaveChangesAsync();
        return OpResult.Success("Date saved. It will appear on the horizon.");
    }

    public async Task<OpResult> AddRelatedAsync(Guid id, AppUser user, RelatedDocumentKind kind, IFormFile? file)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user) && user.Id != contract.InitiatorId)
            return OpResult.Fail("You cannot attach a document.");
        if (kind is RelatedDocumentKind.TradeLicence or RelatedDocumentKind.Passport)
            return OpResult.Fail("Identity documents are uploaded on the start sheet.");
        if (file == null || file.Length == 0) return OpResult.Fail("Choose a file.");
        try
        {
            var stored = await files.SaveAsync(file, contract.Id);
            db.RelatedDocuments.Add(new RelatedDocument
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                Kind = kind,
                FileName = stored.FileName,
                StoredPath = stored.RelativePath,
                UploadedById = user.Id,
                UploadedAt = DateTimeOffset.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return OpResult.Fail(ex.Message);
        }

        Audit(contract, user, "Related document", Labels.Text(kind));
        await db.SaveChangesAsync();
        return OpResult.Success("Document linked to this agreement.");
    }

    public async Task<OpResult> StartRenewalAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && user.Id != contract.InitiatorId) return OpResult.Fail("You cannot open renewal review.");
        if (contract.LifecycleStatus != LifecycleStatus.ExecutedActive) return OpResult.Fail("Renewal review starts from an active agreement.");
        ApplyStatus(contract, LifecycleStatus.RenewalReview);
        Audit(contract, user, "Renewal review", "Option, renewal, or closure is under review.");
        await db.SaveChangesAsync();
        return OpResult.Success("Renewal review is open.");
    }

    public async Task<OpResult> SupersedeAsync(Guid id, AppUser user, Guid childId)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user)) return OpResult.Fail("Only Legal can supersede an agreement.");
        if (contract.LifecycleStatus is not (LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview))
            return OpResult.Fail("Only an active agreement can be superseded.");
        var child = await db.Contracts.FirstOrDefaultAsync(c => c.Id == childId && c.ParentContractId == contract.Id);
        if (child == null) return OpResult.Fail("Choose a renewal or amendment that points back at this agreement.");
        ApplyStatus(contract, LifecycleStatus.Superseded);
        Audit(contract, user, "Superseded", $"Replaced by {child.PublicId}.");
        await db.SaveChangesAsync();
        return OpResult.Success($"Superseded by {child.PublicId}. This record stays in the history.");
    }

    public async Task<OpResult> ExpireAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user)) return OpResult.Fail("Only Legal can expire an agreement.");
        if (contract.LifecycleStatus is not (LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview))
            return OpResult.Fail("Only an active agreement can expire.");
        ApplyStatus(contract, LifecycleStatus.ExpiredTerminated);
        Audit(contract, user, "Expired", "The original term is no longer active.");
        await db.SaveChangesAsync();
        return OpResult.Success("Marked expired / terminated.");
    }

    public async Task<OpResult> CloseAsync(Guid id, AppUser user, string? reason)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user)) return OpResult.Fail("Only Legal can close an agreement.");
        if (contract.LifecycleStatus is LifecycleStatus.Closed or LifecycleStatus.Superseded)
            return OpResult.Fail("This agreement is already closed.");
        var openMandatory = contract.Obligations.Any(o => o.IsMandatory && o.Status == ObligationStatus.Open);
        var post = contract.LifecycleStatus is LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview or LifecycleStatus.ExpiredTerminated;
        if (post && openMandatory && string.IsNullOrWhiteSpace(reason))
            return OpResult.Fail("Mandatory obligations are still open. Enter an override reason to close.");
        contract.ClosureOverrideReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ApplyStatus(contract, LifecycleStatus.Closed);
        Audit(contract, user, "Closed", contract.ClosureOverrideReason ?? "Close-out complete.");
        await db.SaveChangesAsync();
        return OpResult.Success("Closed. The record stays available for audit.");
    }

    public async Task<OpResult> SyncBcAsync(Guid id, AppUser user)
    {
        var (contract, error) = await LoadAsync(id, user);
        if (contract == null) return error!;
        if (!Labels.IsPostExecution(contract.LifecycleStatus)) return OpResult.Fail("Business Central is offered a record after execution.");
        if (!Access.CanDraft(user, contract) && !Access.CanTriage(user)) return OpResult.Fail("Only Legal can record a sync marker.");
        contract.BcLastSyncAt = DateTimeOffset.UtcNow;
        Audit(contract, user, "Business Central marker", "Local sync marker only. No payload was sent to Dynamics 365.");
        await db.SaveChangesAsync();
        return OpResult.Success("Sync marker recorded locally. Dynamics 365 was not called.");
    }

    private async Task<OpResult?> CaptureExecutedCopyAsync(Contract contract, AppUser user, string note)
    {
        var current = contract.Versions.FirstOrDefault(v => v.IsCurrent);
        if (current == null) return OpResult.Fail("There is no approved file to capture.");
        StoredFile stored;
        try
        {
            stored = await files.CopyAsync(contract.Id, current.StoredPath, "executed-" + current.FileName);
        }
        catch (Exception)
        {
            return OpResult.Fail("The approved file could not be copied into the executed version.");
        }

        ArchiveCurrent(contract, VersionKind.ApprovedPackage);
        var next = contract.Versions.Select(v => v.VersionNumber).DefaultIfEmpty(0).Max() + 1;
        contract.Versions.Add(new DocumentVersion
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            VersionNumber = next,
            FileName = stored.FileName,
            StoredPath = stored.RelativePath,
            Kind = VersionKind.ExecutedCopy,
            IsCurrent = true,
            IsLocked = true,
            ChangeNote = note,
            CreatedById = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        return null;
    }

    private static void ArchiveCurrent(Contract contract, VersionKind kind)
    {
        foreach (var version in contract.Versions.Where(v => v.IsCurrent))
        {
            version.IsCurrent = false;
            version.IsLocked = true;
            if (version.Kind == VersionKind.WorkingDraft)
            {
                version.Kind = kind;
            }
        }
    }

    private static void ApplyStatus(Contract contract, LifecycleStatus status)
    {
        contract.LifecycleStatus = status;
        contract.DocumentLocked = Labels.LocksDocument(status);
        contract.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var version in contract.Versions)
        {
            version.IsLocked = !version.IsCurrent || contract.DocumentLocked;
        }
    }

    private async Task<(Contract? Contract, OpResult? Error)> LoadAsync(Guid id, AppUser user)
    {
        var visible = await db.Contracts.VisibleTo(user).AnyAsync(c => c.Id == id);
        if (!visible)
        {
            return (null, OpResult.Fail("That agreement is not available to you."));
        }

        var contract = await db.Contracts
            .Include(c => c.Versions)
            .Include(c => c.ApprovalSteps)
            .Include(c => c.Signatories)
            .Include(c => c.Obligations)
            .Include(c => c.Initiator)
            .FirstAsync(c => c.Id == id);
        return (contract, null);
    }

    private void Audit(Contract contract, AppUser user, string action, string detail)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            ContractId = contract.Id,
            ActorId = user.Id,
            Action = action,
            Detail = detail,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private void Notify(Guid userId, Contract contract, string message)
    {
        db.Notices.Add(new Notice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContractId = contract.Id,
            Message = message,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private void NotifyRole(UserRole role, Contract contract, string message)
    {
        var ids = db.Users.Where(u => u.IsActive && u.Role == role).Select(u => u.Id).ToList();
        foreach (var id in ids)
        {
            Notify(id, contract, message);
        }
    }
}
