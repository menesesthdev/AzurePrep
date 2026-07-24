using AzurePrep.Domain.Correcao;

namespace AzurePrep.Domain.Tests;

public class EscalaDeNotaTests
{
    // A âncora que dá sentido à escala: o percentual de corte do exame vale exatamente 700,
    // qualquer que seja esse percentual. É o que impede a nota de contradizer o veredito.
    [Theory]
    [InlineData(70)]
    [InlineData(75)]
    [InlineData(50)]
    public void Converter_NoPercentualDeCorte_Retorna700(int passing)
    {
        Assert.Equal(EscalaDeNota.NotaDeCorte, EscalaDeNota.Converter(passing, passing));
    }

    [Fact]
    public void Converter_ProvaPerfeita_Retorna1000()
    {
        Assert.Equal(1000, EscalaDeNota.Converter(100m, 70));
    }

    [Fact]
    public void Converter_ZeroAcertos_RetornaNotaMinima()
    {
        Assert.Equal(EscalaDeNota.NotaMinima, EscalaDeNota.Converter(0m, 70));
    }

    [Fact]
    public void Converter_AbaixoDoCorte_FicaAbaixoDe700()
    {
        var nota = EscalaDeNota.Converter(69.9m, 70);

        Assert.InRange(nota, EscalaDeNota.NotaMinima, EscalaDeNota.NotaDeCorte - 1);
    }

    [Fact]
    public void Converter_AcimaDoCorte_FicaEntre700E1000()
    {
        var nota = EscalaDeNota.Converter(85m, 70);

        Assert.InRange(nota, EscalaDeNota.NotaDeCorte + 1, EscalaDeNota.NotaMaxima);
    }

    [Fact]
    public void Converter_EhMonotonica()
    {
        var anterior = 0;

        for (var percent = 0m; percent <= 100m; percent += 2.5m)
        {
            var nota = EscalaDeNota.Converter(percent, 70);
            Assert.True(nota >= anterior, $"Nota caiu em {percent}%: {nota} < {anterior}");
            anterior = nota;
        }
    }

    [Fact]
    public void Converter_ForaDaFaixa_EhClampeada()
    {
        Assert.Equal(EscalaDeNota.NotaMinima, EscalaDeNota.Converter(-10m, 70));
        Assert.Equal(EscalaDeNota.NotaMaxima, EscalaDeNota.Converter(150m, 70));
    }

    // Bordas degeneradas: sem corte tudo aprova; corte em 100% só a prova perfeita aprova.
    [Fact]
    public void Converter_SemCorte_SempreAtingeANotaDeCorte()
    {
        Assert.True(EscalaDeNota.Converter(0m, 0) >= EscalaDeNota.NotaDeCorte);
    }

    [Fact]
    public void Converter_CorteEm100_SoAprovaProvaPerfeita()
    {
        Assert.Equal(EscalaDeNota.NotaMaxima, EscalaDeNota.Converter(100m, 100));
        Assert.True(EscalaDeNota.Converter(99m, 100) < EscalaDeNota.NotaDeCorte);
    }
}
