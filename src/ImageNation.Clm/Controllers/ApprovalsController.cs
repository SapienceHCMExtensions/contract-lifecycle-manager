using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class ApprovalsController(ICurrentUser current, AppDbContext db) : AppController(current)
{
    public async Task<IActionResult> Index()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var rows = await db.ApprovalSteps
            .Where(s => s.ApproverId == user.Id && s.IsActionable && s.Outcome == ApprovalOutcome.Pending
                && s.Contract.LifecycleStatus == LifecycleStatus.AwaitingApproval)
            .OrderBy(s => s.Contract.UpdatedAt)
            .Select(s => new ApprovalQueueRow
            {
                ContractId = s.ContractId,
                PublicId = s.Contract.PublicId,
                ContractName = s.Contract.ContractName,
                Route = s.RouteKind.ToString(),
                Value = s.Contract.TotalValue,
                Currency = s.Contract.Currency,
                Owner = s.Contract.ProjectOwner,
                Since = s.Contract.UpdatedAt
            })
            .ToListAsync();
        return View(new ApprovalQueuePage { Rows = rows });
    }
}
