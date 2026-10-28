using ImageNation.Clm.Domain;

namespace ImageNation.Clm.Services;

public static class Access
{
    public static bool SeesPortfolio(AppUser user) => user.Role is UserRole.Admin or UserRole.HeadOfLegal or UserRole.LegalTriage;

    public static bool CanTriage(AppUser user) => user.Role is UserRole.LegalTriage or UserRole.HeadOfLegal;

    public static bool CanDraft(AppUser user, Contract contract) =>
        user.Role is UserRole.HeadOfLegal or UserRole.LegalTriage
        || (user.Role == UserRole.Lawyer && contract.AssignedLawyerId == user.Id);

    public static bool CanEdit(AppUser user, Contract contract) =>
        Labels.IsEditable(contract.LifecycleStatus)
        && (user.Id == contract.InitiatorId || CanDraft(user, contract) || CanTriage(user));

    public static bool CanConfigure(AppUser user) => user.Role == UserRole.Admin;

    public static bool CanSeeWorkload(AppUser user) => user.Role is UserRole.Admin or UserRole.HeadOfLegal or UserRole.LegalTriage;

    public static bool CanExport(AppUser user) => CanSeeWorkload(user);

    public static bool CanManageObligations(AppUser user, Contract contract) =>
        contract.LifecycleStatus is LifecycleStatus.ExecutedActive or LifecycleStatus.RenewalReview
        && (user.Id == contract.InitiatorId || CanDraft(user, contract) || user.Role == UserRole.HeadOfLegal);

    public static IQueryable<Contract> VisibleTo(this IQueryable<Contract> query, AppUser user)
    {
        return user.Role switch
        {
            UserRole.Admin or UserRole.HeadOfLegal or UserRole.LegalTriage => query,
            UserRole.Requestor => query.Where(c =>
                c.Department == user.Department
                || c.InitiatorId == user.Id
                || c.ApprovalSteps.Any(s => s.ApproverId == user.Id)),
            UserRole.Lawyer => query.Where(c =>
                c.AssignedLawyerId == user.Id
                || c.InitiatorId == user.Id
                || c.Assignments.Any(a => a.ToLawyerId == user.Id || a.FromLawyerId == user.Id)
                || c.ApprovalSteps.Any(s => s.ApproverId == user.Id)),
            UserRole.ProjectApprover or UserRole.CorporateApprover or UserRole.Ceo =>
                query.Where(c => c.ApprovalSteps.Any(s => s.ApproverId == user.Id)),
            _ => query.Where(c => false)
        };
    }
}
