using EstoqueApp.Forms;
using EstoqueApp.Models;
using EstoqueApp.Utils;

namespace EstoqueApp.Tests;

public sealed class UiFlowTests
{
    [Fact]
    public void ApprovalReviewAprovaTodosOsItensDoLote()
    {
        using var formulario = new ApprovalReviewForm(new[]
        {
            new ItemEstoque { Codigo = "A", Descricao = "A", PrecoVendaAtual = 10, PrecoVendaSugerido = 12 },
            new ItemEstoque { Codigo = "B", Descricao = "B", PrecoVendaAtual = 20, PrecoVendaSugerido = 18 }
        });

        var metodo = typeof(ApprovalReviewForm).GetMethod("Confirmar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        metodo.Invoke(formulario, new object[] { true });

        Assert.Equal(2, formulario.ItensAprovados.Count);
    }

    [Fact]
    public void FilterFormMantemSituacaoEColunasVisiveisSelecionadas()
    {
        using var formulario = new FilterForm(
            new[] { ("Codigo", "Código"), ("PrecoVendaAtual", "Venda atual") },
            new[] { "Codigo" },
            "A",
            "Com alteração");

        Assert.Equal("A", formulario.Texto);
        Assert.Equal("Com alteração", formulario.Situacao);
        Assert.Contains("Codigo", formulario.ColunasVisiveis);
        Assert.DoesNotContain("PrecoVendaAtual", formulario.ColunasVisiveis);
    }

    [Fact]
    public void ColumnLayoutServiceReutilizaMapeamentoPorCabecalhos()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"layouts-{Guid.NewGuid():N}.json");
        try
        {
            var cabecalhos = new[] { "codigo", "descricao", "preco", "fornecedor" };
            var mapa = new MapeamentoColunas
            {
                ColunaCodigo = "codigo",
                ColunaDescricao = "descricao",
                ColunaPreco = "preco",
                ColunaQuantidade = "codigo",
                ColunaFornecedor = "fornecedor"
            };
            new ColumnLayoutService(arquivo).Salvar(cabecalhos, mapa);

            var carregado = new ColumnLayoutService(arquivo);

            Assert.True(carregado.TentarObter(cabecalhos, out var resultado));
            Assert.Equal("fornecedor", resultado!.ColunaFornecedor);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Theory]
    [InlineData("R$15,70", 15.70)]
    [InlineData("R$15.70", 15.70)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    public void ImportadorAceitaFormatosDeMoedaESeparadores(string preco, double esperado)
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"planilha-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(arquivo, $"codigo;descricao;preco;quantidade\nA;Produto;\"{preco}\";2");
            var mapa = new MapeamentoColunas
            {
                ColunaCodigo = "codigo",
                ColunaDescricao = "descricao",
                ColunaPreco = "preco",
                ColunaQuantidade = "quantidade"
            };

            var item = Assert.Single(PlanilhaImporter.Importar(arquivo, mapa));

            Assert.Equal((decimal)esperado, item.PrecoCusto);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void ImportadorIgnoraLinhasDeRodapeForaDoContexto()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), $"planilha-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(arquivo,
                "codigo;descricao;preco;quantidade\n" +
                "A;Produto A;15,70;2\n" +
                "TOTAL;Resumo;R$ 31,40;\n" +
                "observacao;Gerado pelo sistema;sem valor;sem estoque");
            var mapa = new MapeamentoColunas
            {
                ColunaCodigo = "codigo",
                ColunaDescricao = "descricao",
                ColunaPreco = "preco",
                ColunaQuantidade = "quantidade"
            };

            var itens = PlanilhaImporter.Importar(arquivo, mapa);

            var item = Assert.Single(itens);
            Assert.Equal("A", item.Codigo);
            Assert.Equal(15.70m, item.PrecoCusto);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }
}