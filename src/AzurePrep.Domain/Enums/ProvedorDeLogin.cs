namespace AzurePrep.Domain.Enums;

/// <summary>
/// Provedor de identidade externo usado no login. Não há senha local: a identidade sempre
/// vem de um provedor social, e o par (provedor, chave) é o que identifica a pessoa.
/// </summary>
public enum ProvedorDeLogin
{
    Google = 1,
    LinkedIn = 2,
    GitHub = 3
}
