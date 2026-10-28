using System.Security.Claims;
using ImageNation.Clm.Data;
using ImageNation.Clm.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ImageNation.Clm.Services;

public interface ICurrentUser
{
    Task<AppUser?> GetAsync();
}

public class CurrentUser(IHttpContextAccessor http, AppDbContext db) : ICurrentUser
{
    private AppUser? _cached;
    private bool _loaded;

    public async Task<AppUser?> GetAsync()
    {
        if (_loaded)
        {
            return _cached;
        }

        _loaded = true;
        var idValue = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var id))
        {
            return null;
        }

        _cached = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
        return _cached;
    }
}

public class PersonaFilter(ICurrentUser current, AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is Controller controller)
        {
            var user = await current.GetAsync();
            controller.ViewData["Persona"] = user;
            if (user != null)
            {
                controller.ViewData["Unread"] = await db.Notices.CountAsync(n => n.UserId == user.Id && !n.IsRead);
            }
        }

        await next();
    }
}
