using AzurePrep.Application.Contracts;

namespace AzurePrep.Web.Models;

/// <summary>Dados iniciais para renderizar a tela de prova (shell + primeira questão).</summary>
public sealed record RealizarProvaViewModel(EstadoDaTentativaDto State, QuestaoDto FirstQuestion);
