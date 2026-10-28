using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

public class AdminController(ICurrentUser current, AppDbContext db) : AppController(current)
{
    public async Task<IActionResult> Index()
    {
        var user = await PersonaAsync();
        if (user == null) return Challenge();
        if (!Access.CanConfigure(user)) return Forbid();
        return View(await PageAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddType(string name)
    {
        if (!await Allowed()) return Forbid();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Enter an agreement type.";
            return RedirectToAction(nameof(Index));
        }

        var order = await db.AgreementTypes.MaxAsync(t => (int?)t.SortOrder) ?? 0;
        db.AgreementTypes.Add(new AgreementType { Id = Guid.NewGuid(), Name = name.Trim(), SortOrder = order + 1, IsActive = true });
        await db.SaveChangesAsync();
        TempData["Success"] = "Agreement type added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleType(Guid id)
    {
        if (!await Allowed()) return Forbid();
        var type = await db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type != null)
        {
            type.IsActive = !type.IsActive;
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name)
    {
        if (!await Allowed()) return Forbid();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Enter a non-supplier category.";
            return RedirectToAction(nameof(Index));
        }

        var order = await db.NonSupplierCategories.MaxAsync(t => (int?)t.SortOrder) ?? 0;
        db.NonSupplierCategories.Add(new NonSupplierCategory { Id = Guid.NewGuid(), Name = name.Trim(), SortOrder = order + 1, IsActive = true });
        await db.SaveChangesAsync();
        TempData["Success"] = "Non-supplier category added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(Guid id)
    {
        if (!await Allowed()) return Forbid();
        var category = await db.NonSupplierCategories.FirstOrDefaultAsync(t => t.Id == id);
        if (category != null)
        {
            category.IsActive = !category.IsActive;
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePolicy(decimal thresholdUsd, decimal aed, decimal usd, decimal eur, decimal gbp, int lead1, int lead2, int lead3)
    {
        if (!await Allowed()) return Forbid();
        if (thresholdUsd <= 0 || aed <= 0 || usd <= 0 || eur <= 0 || gbp <= 0)
        {
            TempData["Error"] = "The threshold and each exchange rate must be greater than zero.";
            return RedirectToAction(nameof(Index));
        }

        var leads = new[] { lead1, lead2, lead3 }.Where(d => d > 0).Distinct().OrderByDescending(d => d).ToList();
        if (leads.Count == 0)
        {
            TempData["Error"] = "Enter at least one reminder lead time in days.";
            return RedirectToAction(nameof(Index));
        }

        var policy = await db.DoaPolicies.SingleAsync();
        policy.ThresholdUsd = thresholdUsd;
        await SetRate(CurrencyCode.AED, aed);
        await SetRate(CurrencyCode.USD, usd);
        await SetRate(CurrencyCode.EUR, eur);
        await SetRate(CurrencyCode.GBP, gbp);
        db.ReminderLeadDays.RemoveRange(db.ReminderLeadDays);
        foreach (var lead in leads.OrderBy(d => d))
        {
            db.ReminderLeadDays.Add(new ReminderLeadDay { Days = lead });
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Delegation of authority and reminders saved.";
        return RedirectToAction(nameof(Index));
    }

    private async Task SetRate(CurrencyCode currency, decimal unitsPerUsd)
    {
        var rate = await db.FxRates.FirstAsync(r => r.Currency == currency);
        rate.UnitsPerUsd = unitsPerUsd;
    }

    private async Task<bool> Allowed()
    {
        var user = await PersonaAsync();
        return user != null && Access.CanConfigure(user);
    }

    private async Task<AdminPage> PageAsync() => new()
    {
        Types = await db.AgreementTypes.OrderBy(t => t.SortOrder).ToListAsync(),
        Categories = await db.NonSupplierCategories.OrderBy(c => c.SortOrder).ToListAsync(),
        Policy = await db.DoaPolicies.SingleAsync(),
        Rates = await db.FxRates.ToListAsync(),
        LeadDays = await db.ReminderLeadDays.OrderBy(d => d.Days).ToListAsync()
    };
}
