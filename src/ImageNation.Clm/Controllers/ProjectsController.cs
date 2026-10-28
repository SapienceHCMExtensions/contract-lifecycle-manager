using ImageNation.Clm.Data;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class ProjectsController(ICurrentUser current, AppDbContext db) : AppController(current)
{
    public async Task<IActionResult> Index()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var projects = await db.Projects.OrderBy(p => p.Name).ToListAsync();
        var counts = await db.Contracts.VisibleTo(user)
            .GroupBy(c => c.ProjectTitleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        return View(new ProjectListPage
        {
            Projects = projects.Select(p => new ProjectCard
            {
                Project = p,
                VisibleAgreements = counts.FirstOrDefault(c => c.Key == p.Id)?.Count ?? 0
            }).ToList()
        });
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound();
        var rows = await db.Contracts.VisibleTo(user)
            .Where(c => c.ProjectTitleId == id)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ContractRow
            {
                Id = c.Id,
                PublicId = c.PublicId,
                ContractName = c.ContractName,
                Project = project.Name,
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
            })
            .ToListAsync();
        return View(new ProjectDetailsPage { Project = project, Agreements = rows });
    }
}
