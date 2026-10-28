using ImageNation.Clm.Domain;
using ImageNation.Clm.Models;
using ImageNation.Clm.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImageNation.Clm.Controllers;

public abstract class AppController(ICurrentUser current) : Controller
{
    protected ICurrentUser Current { get; } = current;

    protected async Task<AppUser?> PersonaAsync() => await Current.GetAsync();

    protected void Flash(OpResult result)
    {
        TempData[result.Ok ? "Success" : "Error"] = result.Message;
    }
}
