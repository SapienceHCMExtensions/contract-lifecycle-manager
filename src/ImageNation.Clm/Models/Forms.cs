using System.ComponentModel.DataAnnotations;
using ImageNation.Clm.Domain;

namespace ImageNation.Clm.Models;

public class StartSheetForm
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter the contract name.")]
    [StringLength(200)]
    public string ContractName { get; set; } = "";

    [Required(ErrorMessage = "Select the agreement type.")]
    public Guid? AgreementTypeId { get; set; }

    [Required(ErrorMessage = "Select whether this is new, a renewal, or a change.")]
    public RequestNature? RequestNature { get; set; }

    [Required(ErrorMessage = "Select how the draft will start.")]
    public InitiationMethod? InitiationMethod { get; set; }

    public string? TemplateName { get; set; }
    public IFormFile? DraftFile { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "Select the Image Nation contracting party.")]
    public Guid? InsEntityId { get; set; }

    public string? ProjectChoice { get; set; }

    [StringLength(200)]
    public string? NewProjectName { get; set; }

    [Required(ErrorMessage = "Enter the project owner.")]
    [StringLength(200)]
    public string ProjectOwner { get; set; } = "";

    [Required(ErrorMessage = "Enter the other contracting party.")]
    [StringLength(200)]
    public string OtherPartyName { get; set; } = "";

    [StringLength(40)]
    public string? OtherPartyReference { get; set; }

    [Required(ErrorMessage = "Select company or individual.")]
    public PartyKind? OtherPartyKind { get; set; }

    [Required(ErrorMessage = "Select the non-supplier category.")]
    public Guid? NonSupplierCategoryId { get; set; }

    [Required(ErrorMessage = "Enter the other party's address.")]
    [StringLength(500)]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Select where the other party is established.")]
    public ResidenceRegion? ResidenceRegion { get; set; }

    [Required(ErrorMessage = "Enter the specific place of residence or establishment.")]
    [StringLength(200)]
    public string SpecificPlace { get; set; } = "";

    [StringLength(80)]
    public string? Country { get; set; }

    public IFormFile? IdentityDocument { get; set; }

    [Required(ErrorMessage = "Select a currency.")]
    public CurrencyCode? Currency { get; set; }

    [Required(ErrorMessage = "Enter the contract value exclusive of VAT.")]
    [Range(0, 100000000000, ErrorMessage = "Enter a contract value of zero or more.")]
    public decimal? ContractValueExVat { get; set; }

    [Required(ErrorMessage = "Enter the payment terms.")]
    [StringLength(1000)]
    public string PaymentTerms { get; set; } = "";

    [Required(ErrorMessage = "Enter the payer invoicing entity.")]
    [StringLength(200)]
    public string PayerInvoicingEntity { get; set; } = "";

    [Required(ErrorMessage = "Enter the payee invoicing entity.")]
    [StringLength(200)]
    public string PayeeInvoicingEntity { get; set; } = "";

    public bool InternationalCoProduction { get; set; }
    public Guid? ParentContractId { get; set; }
}

public class StartSheetPage
{
    public StartSheetForm Form { get; set; } = new();
    public string Department { get; set; } = "";
    public string InitiatorName { get; set; } = "";
    public bool LockInitiation { get; set; }
    public bool RequireIdentity { get; set; } = true;
    public string? ExistingIdentityName { get; set; }
    public List<AgreementType> AgreementTypes { get; set; } = [];
    public List<InsEntity> InsEntities { get; set; } = [];
    public List<NonSupplierCategory> Categories { get; set; } = [];
    public List<ProjectTitle> Projects { get; set; } = [];
    public List<ContractOption> Parents { get; set; } = [];
}

public record ContractOption(Guid Id, string Label);

public class ContractFilter
{
    public string? Q { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TypeId { get; set; }
    public string? Department { get; set; }
    public Guid? LawyerId { get; set; }
    public LifecycleStatus? Status { get; set; }
    public string? Owner { get; set; }
    public DateOnly? EndFrom { get; set; }
    public DateOnly? EndTo { get; set; }
    public int Page { get; set; } = 1;
}

public class SignerInput
{
    public string? Name { get; set; }
    public string? Organization { get; set; }
}

public class OpResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public Guid? ContractId { get; init; }
    public Dictionary<string, string> Fields { get; init; } = [];

    public static OpResult Success(string message, Guid? id = null) => new() { Ok = true, Message = message, ContractId = id };

    public static OpResult Fail(string message) => new() { Ok = false, Message = message };

    public static OpResult Field(string field, string message) => new() { Ok = false, Fields = new Dictionary<string, string> { [field] = message } };
}

public class HorizonRow
{
    public Guid ContractId { get; set; }
    public string PublicId { get; set; } = "";
    public string ContractName { get; set; } = "";
    public string Item { get; set; } = "";
    public DateOnly Date { get; set; }
    public int DaysUntil { get; set; }
    public string Bucket { get; set; } = "";
}

public class WorkloadRow
{
    public Guid LawyerId { get; set; }
    public string Name { get; set; } = "";
    public int OpenCount { get; set; }
    public int OldestDays { get; set; }
    public int AwaitingCount { get; set; }
}

public sealed record BcPayload(
    string AgreementId,
    string Counterparty,
    string Department,
    string Project,
    decimal Value,
    string Currency,
    DateOnly? Effective,
    DateOnly? Expiry,
    string Status,
    DateTimeOffset? LastSyncAt);
