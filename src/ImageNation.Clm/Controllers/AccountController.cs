using System.Security.Claims;
using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Controllers;

[AllowAnonymous]
public class AccountController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var people = await db.Users.Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
        return View(people);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(Guid id)
    {
        var person = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (person == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, person.Id.ToString()),
            new(ClaimTypes.Name, person.Name),
            new(ClaimTypes.Role, person.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutPersona()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Denied() => View();
}
