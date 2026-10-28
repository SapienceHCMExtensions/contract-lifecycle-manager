using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class DashboardController(ICurrentUser current, AppDbContext db) : AppController(current)
{
    public async Task<IActionResult> Index()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();

        var visible = db.Contracts.VisibleTo(user);
        var counts = await visible.GroupBy(c => c.LifecycleStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        var ids = visible.Select(c => c.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var leads = await db.ReminderLeadDays.OrderBy(d => d.Days).Select(d => d.Days).ToListAsync();
        var horizonLimit = leads.DefaultIfEmpty(90).Max();

        var critical = await db.CriticalDates
            .Where(d => ids.Contains(d.ContractId))
            .Select(d => new { d.ContractId, d.Contract.PublicId, d.Contract.ContractName, d.Kind, d.Date })
            .ToListAsync();
        var endings = await visible
            .Where(c => c.EndDate != null && c.LifecycleStatus != LifecycleStatus.Closed && c.LifecycleStatus != LifecycleStatus.Superseded)
            .Select(c => new { c.Id, c.PublicId, c.ContractName, c.EndDate })
            .ToListAsync();
        var obligations = await db.Obligations
            .Where(o => ids.Contains(o.ContractId) && o.Status == ObligationStatus.Open)
            .Select(o => new { o.ContractId, o.Contract.PublicId, o.Contract.ContractName, o.Description, o.DueDate, o.MilestoneKind })
            .ToListAsync();

        var horizon = new List<HorizonRow>();
        foreach (var date in critical)
        {
            var days = date.Date.DayNumber - today.DayNumber;
            if (days >= 0 && days <= horizonLimit)
            {
                horizon.Add(new HorizonRow
                {
                    ContractId = date.ContractId,
                    PublicId = date.PublicId,
                    ContractName = date.ContractName,
                    Item = Labels.Text(date.Kind),
                    Date = date.Date,
                    DaysUntil = days,
                    Bucket = Bucket(days, leads)
                });
            }
        }

        foreach (var ending in endings)
        {
            var days = ending.EndDate!.Value.DayNumber - today.DayNumber;
            if (days >= 0 && days <= horizonLimit)
            {
                horizon.Add(new HorizonRow
                {
                    ContractId = ending.Id,
                    PublicId = ending.PublicId,
                    ContractName = ending.ContractName,
                    Item = "Contract end",
                    Date = ending.EndDate.Value,
                    DaysUntil = days,
                    Bucket = Bucket(days, leads)
                });
            }
        }

        var overdue = obligations
            .Where(o => o.DueDate < today)
            .Select(o => new HorizonRow
            {
                ContractId = o.ContractId,
                PublicId = o.PublicId,
                ContractName = o.ContractName,
                Item = o.MilestoneKind == MilestoneKind.General ? o.Description : Labels.Text(o.MilestoneKind),
                Date = o.DueDate,
                DaysUntil = o.DueDate.DayNumber - today.DayNumber,
                Bucket = "Overdue"
            })
            .OrderBy(o => o.Date)
            .ToList();

        foreach (var obligation in obligations.Where(o => o.DueDate >= today && o.DueDate.DayNumber - today.DayNumber <= horizonLimit))
        {
            var days = obligation.DueDate.DayNumber - today.DayNumber;
            horizon.Add(new HorizonRow
            {
                ContractId = obligation.ContractId,
                PublicId = obligation.PublicId,
                ContractName = obligation.ContractName,
                Item = obligation.MilestoneKind == MilestoneKind.General ? obligation.Description : Labels.Text(obligation.MilestoneKind),
                Date = obligation.DueDate,
                DaysUntil = days,
                Bucket = Bucket(days, leads)
            });
        }

        var workload = new List<WorkloadRow>();
        if (Access.CanSeeWorkload(user))
        {
            var lawyers = await db.Users.Where(u => u.Role == UserRole.Lawyer && u.IsActive).OrderBy(u => u.Name).ToListAsync();
            var open = await db.Contracts
                .Where(c => c.AssignedLawyerId != null
                    && c.LifecycleStatus != LifecycleStatus.Closed
                    && c.LifecycleStatus != LifecycleStatus.Superseded
                    && c.LifecycleStatus != LifecycleStatus.ExpiredTerminated)
                .Select(c => new { c.AssignedLawyerId, c.CreatedAt, c.LifecycleStatus })
                .ToListAsync();
            foreach (var lawyer in lawyers)
            {
                var mine = open.Where(c => c.AssignedLawyerId == lawyer.Id).ToList();
                workload.Add(new WorkloadRow
                {
                    LawyerId = lawyer.Id,
                    Name = lawyer.Name,
                    OpenCount = mine.Count,
                    OldestDays = mine.Count == 0 ? 0 : mine.Max(c => (DateTime.UtcNow - c.CreatedAt.UtcDateTime).Days),
                    AwaitingCount = mine.Count(c => c.LifecycleStatus is LifecycleStatus.AwaitingApproval or LifecycleStatus.AwaitingSignature or LifecycleStatus.PartiallySigned)
                });
            }
        }

        var page = new DashboardPage
        {
            Viewer = user,
            StatusCounts = counts.ToDictionary(c => c.Key, c => c.Count),
            ApprovalWaiting = await db.ApprovalSteps.CountAsync(s =>
                s.ApproverId == user.Id && s.IsActionable && s.Outcome == ApprovalOutcome.Pending
                && s.Contract.LifecycleStatus == LifecycleStatus.AwaitingApproval),
            OverdueCount = overdue.Count,
            Horizon = horizon.OrderBy(h => h.Date).ToList(),
            Overdue = overdue,
            Workload = workload,
            Notices = await db.Notices.Include(n => n.Contract).Where(n => n.UserId == user.Id).OrderByDescending(n => n.CreatedAt).Take(8).ToListAsync(),
            LeadDays = leads,
            ShowWorkload = Access.CanSeeWorkload(user)
        };
        return View(page);
    }

    private static string Bucket(int days, List<int> leads)
    {
        foreach (var lead in leads.OrderBy(d => d))
        {
            if (days <= lead)
            {
                return $"{lead} days";
            }
        }

        return "Later";
    }
}
