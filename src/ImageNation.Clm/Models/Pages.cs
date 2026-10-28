using ImageNation.Clm.Domain;
using ImageNation.Clm.Services;

namespace ImageNation.Clm.Models;

public class ContractRow
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = "";
    public string ContractName { get; set; } = "";
    public string Project { get; set; } = "";
    public string Type { get; set; } = "";
    public string Party { get; set; } = "";
    public string Department { get; set; } = "";
    public string? Lawyer { get; set; }
    public string Owner { get; set; } = "";
    public LifecycleStatus Status { get; set; }
    public decimal Value { get; set; }
    public CurrencyCode Currency { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ContractIndexPage
{
    public ContractFilter Filter { get; set; } = new();
    public List<ContractRow> Rows { get; set; } = [];
    public int Page { get; set; } = 1;
    public int Total { get; set; }
    public int PageSize { get; set; } = 25;
    public List<ProjectTitle> Projects { get; set; } = [];
    public List<AgreementType> Types { get; set; } = [];
    public List<AppUser> Lawyers { get; set; } = [];
    public List<string> Departments { get; set; } = [];
    public bool CanExport { get; set; }
}

public class ContractDetailsPage
{
    public Contract Contract { get; set; } = null!;
    public AppUser Viewer { get; set; } = null!;
    public RoutePlan? LiveRoute { get; set; }
    public decimal? ValueUsd { get; set; }
    public BcPayload? BusinessCentral { get; set; }
    public List<AppUser> Lawyers { get; set; } = [];
    public List<AppUser> People { get; set; } = [];
    public List<Contract> Children { get; set; } = [];
    public ApprovalStep? ActionableStep { get; set; }
}

public class DashboardPage
{
    public AppUser Viewer { get; set; } = null!;
    public Dictionary<LifecycleStatus, int> StatusCounts { get; set; } = [];
    public int ApprovalWaiting { get; set; }
    public int OverdueCount { get; set; }
    public List<HorizonRow> Horizon { get; set; } = [];
    public List<HorizonRow> Overdue { get; set; } = [];
    public List<WorkloadRow> Workload { get; set; } = [];
    public List<Notice> Notices { get; set; } = [];
    public List<int> LeadDays { get; set; } = [];
    public bool ShowWorkload { get; set; }
}

public class QueuePage
{
    public string Title { get; set; } = "";
    public string Intro { get; set; } = "";
    public List<ContractRow> Rows { get; set; } = [];
}

public class ApprovalQueuePage
{
    public List<ApprovalQueueRow> Rows { get; set; } = [];
}

public class ApprovalQueueRow
{
    public Guid ContractId { get; set; }
    public string PublicId { get; set; } = "";
    public string ContractName { get; set; } = "";
    public string Route { get; set; } = "";
    public decimal Value { get; set; }
    public CurrencyCode Currency { get; set; }
    public string Owner { get; set; } = "";
    public DateTimeOffset Since { get; set; }
}

public class ProjectListPage
{
    public List<ProjectCard> Projects { get; set; } = [];
}

public class ProjectCard
{
    public ProjectTitle Project { get; set; } = null!;
    public int VisibleAgreements { get; set; }
}

public class ProjectDetailsPage
{
    public ProjectTitle Project { get; set; } = null!;
    public List<ContractRow> Agreements { get; set; } = [];
}

public class AdminPage
{
    public List<AgreementType> Types { get; set; } = [];
    public List<NonSupplierCategory> Categories { get; set; } = [];
    public DoaPolicy Policy { get; set; } = null!;
    public List<FxRate> Rates { get; set; } = [];
    public List<ReminderLeadDay> LeadDays { get; set; } = [];
}
