namespace ImageNation.Clm.Domain;

public enum UserRole
{
    Requestor,
    LegalTriage,
    Lawyer,
    ProjectApprover,
    CorporateApprover,
    Ceo,
    HeadOfLegal,
    Admin
}

public enum LifecycleStatus
{
    Requested,
    LegalReview,
    Drafting,
    Negotiation,
    AwaitingApproval,
    Returned,
    Approved,
    AwaitingSignature,
    PartiallySigned,
    ExecutedActive,
    RenewalReview,
    ExpiredTerminated,
    Closed,
    Superseded
}

public enum ReportingStatus
{
    InProcess,
    FullyExecutedAndComplete,
    ClosedNotProceeding
}

public enum RequestNature
{
    New,
    Renew,
    Amendment,
    Extension,
    Variation,
    AdditionalServices
}

public enum InitiationMethod
{
    Template,
    UploadedDraft,
    FromScratch
}

public enum PartyKind
{
    Company,
    Individual
}

public enum ResidenceRegion
{
    Uae,
    GccExceptUae,
    OutsideGcc
}

public enum CurrencyCode
{
    AED,
    USD,
    EUR,
    GBP
}

public enum VersionKind
{
    WorkingDraft,
    Historical,
    ApprovedPackage,
    ExecutedCopy
}

public enum ApprovalRouteKind
{
    Project,
    Corporate,
    Exceptional,
    AdHoc
}

public enum ApprovalOutcome
{
    Pending,
    Approved,
    Rejected,
    Withdrawn
}

public enum SignatureState
{
    Pending,
    Signed,
    Superseded
}

public enum ObligationStatus
{
    Open,
    Completed,
    Waived
}

public enum MilestoneKind
{
    General,
    LockedCut,
    FinalSoundMix
}

public enum CriticalDateKind
{
    Effective,
    Expiry,
    NoticeDeadline,
    OptionExercise,
    Renewal,
    AutoRenewal
}

public enum RelatedDocumentKind
{
    Appendix,
    Nda,
    Privacy,
    TradeLicence,
    Passport,
    Other
}
