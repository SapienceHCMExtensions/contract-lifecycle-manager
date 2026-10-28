using System.Text;
using ImageNation.Clm.Domain;

namespace ImageNation.Clm.Services;

public sealed record PlannedStep(ApprovalRouteKind Kind, AppUser Approver, string Why);

public sealed record RoutePlan(decimal ValueUsd, decimal UnitsPerUsd, string Explanation, IReadOnlyList<PlannedStep> Steps);

public static class DoaRouter
{
    public static RoutePlan Plan(Contract contract, DoaPolicy policy, IReadOnlyList<FxRate> rates, IReadOnlyList<AppUser> users)
    {
        var rate = rates.SingleOrDefault(r => r.Currency == contract.Currency)
            ?? throw new InvalidOperationException($"No USD rate is configured for {contract.Currency}.");

        var usd = Money.ToUsd(contract.ContractValueExVat, rate.UnitsPerUsd);
        var steps = new List<PlannedStep>();
        var below = usd < policy.ThresholdUsd;

        if (below)
        {
            var approver = users.Single(u => u.IsActive && u.Role == UserRole.ProjectApprover);
            steps.Add(new PlannedStep(ApprovalRouteKind.Project, approver, $"Below {policy.ThresholdUsd:N0} USD"));
        }
        else
        {
            var cfo = users.Single(u => u.IsActive && u.Role == UserRole.CorporateApprover);
            var head = users.Single(u => u.IsActive && u.Role == UserRole.HeadOfLegal);
            steps.Add(new PlannedStep(ApprovalRouteKind.Corporate, cfo, $"At or above {policy.ThresholdUsd:N0} USD"));
            steps.Add(new PlannedStep(ApprovalRouteKind.Corporate, head, "Second corporate approver"));
        }

        if (contract.InternationalCoProduction)
        {
            var ceo = users.Single(u => u.IsActive && u.Role == UserRole.Ceo);
            steps.Add(new PlannedStep(ApprovalRouteKind.Exceptional, ceo, "International co-production"));
        }

        var text = new StringBuilder();
        text.Append($"{contract.ContractValueExVat:N2} {contract.Currency} converts to {usd:N2} USD ");
        text.Append($"at {rate.UnitsPerUsd:N4} {contract.Currency} per USD, ");
        text.Append(below ? "below " : "at or above ");
        text.Append($"the {policy.ThresholdUsd:N0} USD threshold. ");
        text.Append(below
            ? $"Project route: {steps[0].Approver.Name}, {steps[0].Approver.Title}."
            : $"Corporate route: {steps[0].Approver.Name}, {steps[0].Approver.Title}, then {steps[1].Approver.Name}, {steps[1].Approver.Title}.");
        if (contract.InternationalCoProduction)
        {
            var ceo = steps[^1].Approver;
            text.Append($" International co-production adds the exceptional route: {ceo.Name}, {ceo.Title}.");
        }

        return new RoutePlan(usd, rate.UnitsPerUsd, text.ToString(), steps);
    }
}
