namespace ImageNation.Clm.Domain;

public static class Labels
{
    public static ReportingStatus ReportingOf(LifecycleStatus status) => status switch
    {
        LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview => ReportingStatus.FullyExecutedAndComplete,
        LifecycleStatus.ExpiredTerminated or LifecycleStatus.Closed or LifecycleStatus.Superseded => ReportingStatus.ClosedNotProceeding,
        _ => ReportingStatus.InProcess
    };

    public static bool LocksDocument(LifecycleStatus status) => status is
        LifecycleStatus.AwaitingApproval
        or LifecycleStatus.Approved
        or LifecycleStatus.AwaitingSignature
        or LifecycleStatus.PartiallySigned
        or LifecycleStatus.ExecutedActive
        or LifecycleStatus.RenewalReview
        or LifecycleStatus.ExpiredTerminated
        or LifecycleStatus.Closed
        or LifecycleStatus.Superseded;

    public static bool IsEditable(LifecycleStatus status) => status is
        LifecycleStatus.Requested
        or LifecycleStatus.LegalReview
        or LifecycleStatus.Drafting
        or LifecycleStatus.Negotiation
        or LifecycleStatus.Returned;

    public static bool RequiresParent(RequestNature nature) => nature != RequestNature.New;

    public static bool IsPostExecution(LifecycleStatus status) => status is
        LifecycleStatus.ExecutedActive
        or LifecycleStatus.RenewalReview
        or LifecycleStatus.ExpiredTerminated
        or LifecycleStatus.Closed
        or LifecycleStatus.Superseded;

    public static string NextAction(LifecycleStatus status) => status switch
    {
        LifecycleStatus.Requested => "Triage the request",
        LifecycleStatus.LegalReview => "Assign a lawyer",
        LifecycleStatus.Drafting => "Prepare the next draft",
        LifecycleStatus.Negotiation => "Close the open terms",
        LifecycleStatus.AwaitingApproval => "Await the approval decision",
        LifecycleStatus.Returned => "Revise and resubmit",
        LifecycleStatus.Approved => "Send for signature",
        LifecycleStatus.AwaitingSignature => "Collect signatures",
        LifecycleStatus.PartiallySigned => "Chase the outstanding signer",
        LifecycleStatus.ExecutedActive => "Monitor obligations and dates",
        LifecycleStatus.RenewalReview => "Decide renewal, option, or closure",
        LifecycleStatus.ExpiredTerminated => "Confirm close-out",
        LifecycleStatus.Closed => "Retained for audit",
        LifecycleStatus.Superseded => "Open the replacement agreement",
        _ => "Review the record"
    };

    public static string StatusCss(LifecycleStatus status) => status switch
    {
        LifecycleStatus.ExecutedActive or LifecycleStatus.Approved => "status-ok",
        LifecycleStatus.AwaitingApproval or LifecycleStatus.AwaitingSignature or LifecycleStatus.PartiallySigned or LifecycleStatus.RenewalReview => "status-wait",
        LifecycleStatus.Returned or LifecycleStatus.ExpiredTerminated => "status-bad",
        LifecycleStatus.Closed or LifecycleStatus.Superseded => "status-mute",
        _ => "status-live"
    };

    public static string Text(LifecycleStatus status) => status switch
    {
        LifecycleStatus.LegalReview => "Legal review",
        LifecycleStatus.AwaitingApproval => "Awaiting approval",
        LifecycleStatus.AwaitingSignature => "Awaiting signature",
        LifecycleStatus.PartiallySigned => "Partially signed",
        LifecycleStatus.ExecutedActive => "Executed / active",
        LifecycleStatus.RenewalReview => "Renewal review",
        LifecycleStatus.ExpiredTerminated => "Expired / terminated",
        _ => status.ToString()
    };

    public static string Text(ReportingStatus status) => status switch
    {
        ReportingStatus.InProcess => "In Process",
        ReportingStatus.FullyExecutedAndComplete => "Fully Executed and Complete",
        ReportingStatus.ClosedNotProceeding => "Closed/Not Proceeding",
        _ => status.ToString()
    };

    public static string Text(RequestNature nature) => nature switch
    {
        RequestNature.AdditionalServices => "Additional Services",
        _ => nature.ToString()
    };

    public static string Text(InitiationMethod method) => method switch
    {
        InitiationMethod.UploadedDraft => "Uploaded third-party draft",
        InitiationMethod.FromScratch => "From scratch",
        InitiationMethod.Template => "Approved template",
        _ => method.ToString()
    };

    public static string Text(ResidenceRegion region) => region switch
    {
        ResidenceRegion.Uae => "UAE",
        ResidenceRegion.GccExceptUae => "GCC (except UAE)",
        ResidenceRegion.OutsideGcc => "Outside GCC",
        _ => region.ToString()
    };

    public static string Text(PartyKind kind) => kind == PartyKind.Company ? "Company" : "Individual";

    public static string Text(UserRole role) => role switch
    {
        UserRole.LegalTriage => "Legal triage",
        UserRole.ProjectApprover => "Project approver",
        UserRole.CorporateApprover => "Corporate approver",
        UserRole.Ceo => "CEO",
        UserRole.HeadOfLegal => "Head of Legal",
        UserRole.Requestor => "Business requestor",
        _ => role.ToString()
    };

    public static string Text(ApprovalRouteKind kind) => kind switch
    {
        ApprovalRouteKind.AdHoc => "Ad hoc",
        _ => kind.ToString()
    };

    public static string Text(MilestoneKind kind) => kind switch
    {
        MilestoneKind.LockedCut => "Locked Cut",
        MilestoneKind.FinalSoundMix => "Final Sound Mix",
        _ => "General"
    };

    public static string Text(CriticalDateKind kind) => kind switch
    {
        CriticalDateKind.NoticeDeadline => "Notice deadline",
        CriticalDateKind.OptionExercise => "Option exercise",
        CriticalDateKind.AutoRenewal => "Auto-renewal",
        _ => kind.ToString()
    };

    public static string Text(RelatedDocumentKind kind) => kind switch
    {
        RelatedDocumentKind.Nda => "NDA",
        RelatedDocumentKind.TradeLicence => "Trade licence",
        _ => kind.ToString()
    };

    public static string Text(VersionKind kind) => kind switch
    {
        VersionKind.WorkingDraft => "Working draft",
        VersionKind.ApprovedPackage => "Approved package",
        VersionKind.ExecutedCopy => "Executed copy",
        _ => "Historical"
    };

    public static string Text(ObligationStatus status) => status.ToString();

    public static string Text(SignatureState state) => state switch
    {
        SignatureState.Superseded => "Superseded",
        _ => state.ToString()
    };

    public static string Text(ApprovalOutcome outcome) => outcome.ToString();

    public static string Money(decimal amount, CurrencyCode currency) => $"{amount:N2} {currency}";
}

public static class ApprovedTemplates
{
    public static readonly string[] All =
    [
        "Talent engagement",
        "Crew deal memo",
        "Consultancy agreement",
        "Services agreement",
        "Distribution licence",
        "Co-production agreement",
        "Mutual NDA"
    ];
}

public static class Money
{
    public const decimal VatRate = 0.05m;

    public static decimal VatOn(decimal net) =>
        decimal.Round(net * VatRate, 2, MidpointRounding.AwayFromZero);

    public static decimal Total(decimal net) => net + VatOn(net);

    public static decimal ToUsd(decimal amount, decimal unitsPerUsd) =>
        decimal.Round(amount / unitsPerUsd, 2, MidpointRounding.AwayFromZero);
}
