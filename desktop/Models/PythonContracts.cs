using System.Text.Json.Serialization;

namespace EstoqueApp.Models
{
    public sealed class ItemPlanilha
    {
        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("descricao")]
        public string Descricao { get; set; } = string.Empty;

        [JsonPropertyName("preco_custo")]
        public decimal PrecoCusto { get; set; }

        [JsonPropertyName("marca")]
        public string Marca { get; set; } = "Sem Marca";

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; } = "Geral";

        [JsonPropertyName("fornecedor")]
        public string Fornecedor { get; set; } = string.Empty;
    }

    public sealed class ItemDadoAtual
    {
        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("preco_venda_atual")]
        public decimal PrecoVendaAtual { get; set; }

        [JsonPropertyName("preco_custo_atual")]
        public decimal PrecoCustoAtual { get; set; }

        [JsonPropertyName("estoque")]
        public int Estoque { get; set; }
    }

    public sealed class RegrasNegocio
    {
        [JsonPropertyName("margem_padrao")]
        public decimal MargemPadrao { get; set; } = 0.30m;

        [JsonPropertyName("margem_por_marca")]
        public Dictionary<string, decimal> MargemPorMarca { get; set; } = new();

        [JsonPropertyName("margem_por_categoria")]
        public Dictionary<string, decimal> MargemPorCategoria { get; set; } = new();

        [JsonPropertyName("margem_por_fornecedor")]
        public Dictionary<string, decimal> MargemPorFornecedor { get; set; } = new();

        [JsonPropertyName("faixas_custo")]
        public List<FaixaCusto> FaixasCusto { get; set; } = new();

        [JsonPropertyName("preco_minimo_por_produto")]
        public Dictionary<string, decimal> PrecoMinimoPorProduto { get; set; } = new();

        [JsonPropertyName("preco_maximo_por_produto")]
        public Dictionary<string, decimal> PrecoMaximoPorProduto { get; set; } = new();

        [JsonPropertyName("tipo_calculo")]
        public string TipoCalculo { get; set; } = "margem";

        [JsonPropertyName("arredondamento")]
        public string Arredondamento { get; set; } = "nenhum";

        [JsonPropertyName("promocoes")]
        public List<Promocao> Promocoes { get; set; } = new();

        [JsonPropertyName("aumento_maximo_percentual")]
        public decimal AumentoMaximoPercentual { get; set; } = 0.15m;

        [JsonPropertyName("reducao_maxima_percentual")]
        public decimal ReducaoMaximaPercentual { get; set; } = 0.05m;
    }

    public sealed class FaixaCusto
    {
        [JsonPropertyName("minimo")] public decimal Minimo { get; set; }
        [JsonPropertyName("maximo")] public decimal Maximo { get; set; }
        [JsonPropertyName("valor")] public decimal Valor { get; set; }
    }

    public sealed class Promocao
    {
        [JsonPropertyName("nome")] public string Nome { get; set; } = "Promoção";
        [JsonPropertyName("inicio")] public string Inicio { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        [JsonPropertyName("fim")] public string Fim { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        [JsonPropertyName("desconto")] public decimal Desconto { get; set; }
        [JsonPropertyName("codigo")] public string Codigo { get; set; } = string.Empty;
        [JsonPropertyName("marca")] public string Marca { get; set; } = string.Empty;
        [JsonPropertyName("categoria")] public string Categoria { get; set; } = string.Empty;
    }

    public sealed class RequisicaoProcessamento
    {
        [JsonPropertyName("planilha_fornecedor")]
        public List<ItemPlanilha> PlanilhaFornecedor { get; set; } = new();

        [JsonPropertyName("dados_atuais")]
        public List<ItemDadoAtual> DadosAtuais { get; set; } = new();

        [JsonPropertyName("regras")]
        public RegrasNegocio Regras { get; set; } = new();
    }

    public sealed class ItemProcessado
    {
        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("preco_venda_sugerido")]
        public decimal PrecoVendaSugerido { get; set; }

        [JsonPropertyName("alerta")]
        public string? Alerta { get; set; }

        [JsonPropertyName("quarentena")]
        public bool Quarentena { get; set; }
    }

    public sealed class ResumoProcessamento
    {
        [JsonPropertyName("total_itens")]
        public int TotalItens { get; set; }

        [JsonPropertyName("itens_em_quarentena")]
        public int ItensEmQuarentena { get; set; }

        [JsonPropertyName("faturamento_atual")] public decimal FaturamentoAtual { get; set; }
        [JsonPropertyName("faturamento_sugerido")] public decimal FaturamentoSugerido { get; set; }
        [JsonPropertyName("margem_atual")] public decimal MargemAtual { get; set; }
        [JsonPropertyName("margem_sugerida")] public decimal MargemSugerida { get; set; }
    }

    public sealed class RespostaProcessamento
    {
        [JsonPropertyName("sucesso")]
        public bool Sucesso { get; set; }

        [JsonPropertyName("mensagem")]
        public string Mensagem { get; set; } = string.Empty;

        [JsonPropertyName("resumo")]
        public ResumoProcessamento Resumo { get; set; } = new();

        [JsonPropertyName("itens")]
        public List<ItemProcessado> Itens { get; set; } = new();
    }
}