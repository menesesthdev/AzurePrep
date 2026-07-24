using System.Security.Claims;
using AzurePrep.Application.Autenticacao;
using AzurePrep.Application.Contracts;
using AzurePrep.Web.Autenticacao;
using AzurePrep.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzurePrep.Web.Controllers;

[Route("conta")]
public class ContaController : Controller
{
    private readonly IAutenticacaoService _autenticacao;
    private readonly IAuthenticationSchemeProvider _schemes;

    public ContaController(IAutenticacaoService autenticacao, IAuthenticationSchemeProvider schemes)
    {
        _autenticacao = autenticacao;
        _schemes = schemes;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel(await ProvedoresDisponiveisAsync(), returnUrl));
    }

    /// <summary>
    /// Dispara o fluxo OAuth. É POST com antiforgery de propósito: um GET permitiria que outro
    /// site iniciasse login por nós (login CSRF), prendendo a pessoa numa conta que não é dela.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("entrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(string provider, string? returnUrl = null)
    {
        // Só aceita esquema que realmente registramos — nada de challenge arbitrário.
        if (AutenticacaoSetup.ProvedorDoEsquema(provider) is null
            || await _schemes.GetSchemeAsync(provider) is null)
        {
            return BadRequest();
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback), new { returnUrl })
        };

        return Challenge(properties, provider);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await HttpContext.AuthenticateAsync(EsquemasDeAutenticacao.Externo);
        if (!result.Succeeded || result.Principal is null)
        {
            return RedirectToAction(nameof(Login), new { erro = true });
        }

        var scheme = result.Ticket?.AuthenticationScheme ?? string.Empty;
        var provider = AutenticacaoSetup.ProvedorDoEsquema(scheme);
        var providerKey = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (provider is null || string.IsNullOrWhiteSpace(providerKey))
        {
            return RedirectToAction(nameof(Login), new { erro = true });
        }

        var usuario = await _autenticacao.ObterOuCriarAsync(
            new LoginExternoRequest(
                provider.Value,
                providerKey,
                NomeDe(result.Principal),
                result.Principal.FindFirstValue(ClaimTypes.Email),
                AvatarDe(result.Principal)),
            cancellationToken);

        await HttpContext.SignInAsync(EsquemasDeAutenticacao.Aplicacao, ConstruirPrincipal(usuario));

        // O cookie externo já cumpriu seu papel — não deve sobreviver ao login.
        await HttpContext.SignOutAsync(EsquemasDeAutenticacao.Externo);

        return RedirectToLocal(returnUrl);
    }

    [HttpPost("sair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(EsquemasDeAutenticacao.Aplicacao);
        return RedirectToAction(nameof(Login));
    }

    // ---- Auxiliares ------------------------------------------------------

    private async Task<IReadOnlyList<string>> ProvedoresDisponiveisAsync()
    {
        var todos = await _schemes.GetAllSchemesAsync();
        return todos
            .Select(s => s.Name)
            .Where(name => AutenticacaoSetup.ProvedorDoEsquema(name) is not null)
            .ToList();
    }

    // O GitHub pode não expor nome real; nesse caso o login (handle) é o melhor rótulo.
    private static string NomeDe(ClaimsPrincipal principal)
        => principal.FindFirstValue(ClaimTypes.Name)
           ?? principal.FindFirstValue(ClaimTypes.GivenName)
           ?? principal.Identity?.Name
           ?? "Candidato";

    // Cada provedor nomeia a foto de um jeito; pegamos a primeira que existir.
    private static string? AvatarDe(ClaimsPrincipal principal)
        => principal.FindFirstValue("picture")
           ?? principal.FindFirstValue("urn:github:avatar")
           ?? principal.FindFirstValue("avatar_url");

    private static ClaimsPrincipal ConstruirPrincipal(UsuarioDto usuario)
    {
        // NameIdentifier passa a ser o Id LOCAL: daqui pra frente o app não usa mais a
        // identidade do provedor, e IUsuarioAtual lê exatamente esta claim.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Name),
            new("provider", usuario.Provider.ToString())
        };

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, usuario.Email));
        }

        if (!string.IsNullOrWhiteSpace(usuario.AvatarUrl))
        {
            claims.Add(new Claim("avatar", usuario.AvatarUrl));
        }

        var identity = new ClaimsIdentity(claims, EsquemasDeAutenticacao.Aplicacao);
        return new ClaimsPrincipal(identity);
    }

    // Só redireciona para dentro da aplicação — bloqueia open redirect via ?returnUrl=.
    private IActionResult RedirectToLocal(string? returnUrl)
        => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction("Index", "Home");
}
