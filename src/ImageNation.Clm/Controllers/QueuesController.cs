using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class QueuesController(ICurrentUser current, AppDbContext db) : AppController(current)
{
    public async Task<IActionResult> Index()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();

        IQueryable<Contract> query = db.Contracts.VisibleTo(user);
        string title;
        string intro;
        if (user.Role == UserRole.Lawyer)
        {
            query = query.Where(c => c.AssignedLawyerId == user.Id);
            title = "My matters";
            intro = "Agreements currently assigned to you, with age counted from the day the request was raised.";
        }
        else if (user.Role is UserRole.LegalTriage)
        {
            query = query.Where(c => c.LifecycleStatus == LifecycleStatus.Requested || c.LifecycleStatus == LifecycleStatus.LegalReview || c.AssignedLawyerId == null);
            title = "Intake queue";
            intro = "Requests waiting for triage or still without a lawyer.";
        }
        else if (Access.CanSeeWorkload(user))
        {
            query = query.Where(c => c.LifecycleStatus != LifecycleStatus.Closed && c.LifecycleStatus != LifecycleStatus.Superseded);
            title = "Open matters";
            intro = "Everything still moving, across lawyers and departments.";
        }
        else
        {
            return Forbid();
        }

        var rows = await ProjectRows(query.OrderBy(c => c.CreatedAt)).ToListAsync();
        return View(new QueuePage { Title = title, Intro = intro, Rows = rows });
    }

    internal static IQueryable<ContractRow> ProjectRows(IQueryable<Contract> query) =>
        query.Select(c => new ContractRow
        {
            Id = c.Id,
            PublicId = c.PublicId,
            ContractName = c.ContractName,
            Project = c.ProjectTitle.Name,
            Type = c.AgreementType.Name,
            Party = c.OtherPartyName,
            Department = c.Department,
            Lawyer = c.AssignedLawyer != null ? c.AssignedLawyer.Name : null,
            Owner = c.ProjectOwner,
            Status = c.LifecycleStatus,
            Value = c.TotalValue,
            Currency = c.Currency,
            EndDate = c.EndDate,
            CreatedAt = c.CreatedAt
        });
}
