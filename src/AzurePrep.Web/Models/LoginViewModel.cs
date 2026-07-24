namespace AzurePrep.Web.Models;

/// <summary>
/// Dados da tela de login. <paramref name="Provedores"/> traz só os esquemas efetivamente
/// registrados, para não oferecer um botão que quebraria por falta de credencial.
/// </summary>
public sealed record LoginViewModel(IReadOnlyList<string> Provedores, string? ReturnUrl);
