namespace ImageNation.Clm.Domain;

public class AppUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class InsEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AgreementType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class NonSupplierCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProjectTitle
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string InternalCode { get; set; } = "";
    public string ProductionEntity { get; set; } = "";
    public string Status { get; set; } = "Active";
    public string BusinessOwner { get; set; } = "";
}

public class DoaPolicy
{
    public int Id { get; set; } = 1;
    public decimal ThresholdUsd { get; set; } = 50_000m;
}

public class FxRate
{
    public CurrencyCode Currency { get; set; }
    public decimal UnitsPerUsd { get; set; }
}

public class ReminderLeadDay
{
    public int Id { get; set; }
    public int Days { get; set; }
}

public class ContractNumberSequence
{
    public int Year { get; set; }
    public int LastNumber { get; set; }
}

public class Contract
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = "";
    public string ContractName { get; set; } = "";
    public Guid AgreementTypeId { get; set; }
    public AgreementType AgreementType { get; set; } = null!;
    public RequestNature RequestNature { get; set; }
    public InitiationMethod InitiationMethod { get; set; }
    public string? TemplateName { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Guid InsEntityId { get; set; }
    public InsEntity InsEntity { get; set; } = null!;
    public string Department { get; set; } = "";
    public Guid ProjectTitleId { get; set; }
    public ProjectTitle ProjectTitle { get; set; } = null!;
    public string ProjectOwner { get; set; } = "";
    public string OtherPartyName { get; set; } = "";
    public string OtherPartyReference { get; set; } = "";
    public PartyKind OtherPartyKind { get; set; }
    public Guid NonSupplierCategoryId { get; set; }
    public NonSupplierCategory NonSupplierCategory { get; set; } = null!;
    public string Address { get; set; } = "";
    public ResidenceRegion ResidenceRegion { get; set; }
    public string SpecificPlace { get; set; } = "";
    public string? Country { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal ContractValueExVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalValue { get; set; }
    public string PaymentTerms { get; set; } = "";
    public string PayerInvoicingEntity { get; set; } = "";
    public string PayeeInvoicingEntity { get; set; } = "";
    public bool InternationalCoProduction { get; set; }
    public LifecycleStatus LifecycleStatus { get; set; }
    public bool DocumentLocked { get; set; }
    public string? ClarificationNote { get; set; }
    public string? RouteExplanation { get; set; }
    public string? ClosureOverrideReason { get; set; }
    public DateOnly? ExecutionDate { get; set; }
    public string? ExecutionSignatories { get; set; }
    public Guid? ParentContractId { get; set; }
    public Contract? ParentContract { get; set; }
    public Guid InitiatorId { get; set; }
    public AppUser Initiator { get; set; } = null!;
    public Guid? AssignedLawyerId { get; set; }
    public AppUser? AssignedLawyer { get; set; }
    public DateTimeOffset? BcLastSyncAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Contract> Children { get; set; } = new List<Contract>();
    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
    public ICollection<Signatory> Signatories { get; set; } = new List<Signatory>();
    public ICollection<Obligation> Obligations { get; set; } = new List<Obligation>();
    public ICollection<RightsWindow> Rights { get; set; } = new List<RightsWindow>();
    public ICollection<CriticalDate> CriticalDates { get; set; } = new List<CriticalDate>();
    public ICollection<RelatedDocument> RelatedDocuments { get; set; } = new List<RelatedDocument>();
    public ICollection<AssignmentEvent> Assignments { get; set; } = new List<AssignmentEvent>();
    public ICollection<AuditEvent> AuditEvents { get; set; } = new List<AuditEvent>();
}

public class DocumentVersion
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public VersionKind Kind { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsLocked { get; set; }
    public string? ChangeNote { get; set; }
    public Guid CreatedById { get; set; }
    public AppUser CreatedBy { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class ApprovalStep
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int Round { get; set; }
    public int Sequence { get; set; }
    public ApprovalRouteKind RouteKind { get; set; }
    public Guid ApproverId { get; set; }
    public AppUser Approver { get; set; } = null!;
    public ApprovalOutcome Outcome { get; set; }
    public bool IsActionable { get; set; }
    public string? Comment { get; set; }
    public string? AdHocReason { get; set; }
    public string RouteExplanation { get; set; } = "";
    public DateTimeOffset? DecidedAt { get; set; }
}

public class Signatory
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int Round { get; set; }
    public int SigningOrder { get; set; }
    public string Name { get; set; } = "";
    public string Organization { get; set; } = "";
    public SignatureState State { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? SignedAt { get; set; }
}

public class Obligation
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string Description { get; set; } = "";
    public MilestoneKind MilestoneKind { get; set; }
    public Guid OwnerId { get; set; }
    public AppUser Owner { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public ObligationStatus Status { get; set; }
    public bool IsMandatory { get; set; } = true;
    public string? EvidenceNote { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class RightsWindow
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string RightType { get; set; } = "";
    public string Territory { get; set; } = "";
    public DateOnly WindowStart { get; set; }
    public DateOnly WindowEnd { get; set; }
    public string Exclusivity { get; set; } = "";
    public string? Counterparty { get; set; }
}

public class CriticalDate
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public CriticalDateKind Kind { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public string? Outcome { get; set; }
}

public class RelatedDocument
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public RelatedDocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public Guid UploadedById { get; set; }
    public AppUser UploadedBy { get; set; } = null!;
    public DateTimeOffset UploadedAt { get; set; }
}

public class AssignmentEvent
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public Guid? FromLawyerId { get; set; }
    public AppUser? FromLawyer { get; set; }
    public Guid ToLawyerId { get; set; }
    public AppUser ToLawyer { get; set; } = null!;
    public Guid AssignedById { get; set; }
    public AppUser AssignedBy { get; set; } = null!;
    public string? Reason { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}

public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public Guid ActorId { get; set; }
    public AppUser Actor { get; set; } = null!;
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public class Notice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
