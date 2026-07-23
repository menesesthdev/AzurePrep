using System.Diagnostics;
using AzurePrep.Application.Exames;
using AzurePrep.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrep.Web.Controllers;

public class HomeController : Controller
{
    private readonly ICatalogoDeExamesService _catalog;

    public HomeController(ICatalogoDeExamesService catalog)
    {
        _catalog = catalog;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var exams = await _catalog.ObterExamesDisponiveisAsync(cancellationToken);
        return View(exams);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
