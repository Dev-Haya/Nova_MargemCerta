namespace EstoqueApp.Models
{
    /// <summary>
    /// Representa um item do estoque, incluindo o preço antigo (antes da
    /// atualização) e o preço novo (vindo da nova carga/planilha), para que
    /// a tela de comparação possa destacar diferenças.
    /// </summary>
    public class ItemEstoque
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Marca { get; set; } = "Sem Marca";
        public string Categoria { get; set; } = "Geral";
        public string Fornecedor { get; set; } = string.Empty;
        public decimal PrecoCusto { get; set; }
        public decimal PrecoVendaAtual { get; set; }
        public decimal PrecoVendaSugerido { get; set; }
        public int Quantidade { get; set; }
        public decimal ValorAcumuladoCusto => PrecoCusto * Quantidade;
        public bool Quarentena { get; set; }
        public bool Aprovado { get; set; }
        public string? Alerta { get; set; }

        /// <summary>True quando o preço novo é diferente do antigo (linha de alerta).</summary>
        public bool PrecoMudou => PrecoVendaAtual != PrecoVendaSugerido;

        /// <summary>Diferença percentual entre o preço antigo e o novo.</summary>
        public decimal DiferencaPercentual =>
            PrecoVendaAtual == 0 ? 0 : Math.Round((PrecoVendaSugerido - PrecoVendaAtual) / PrecoVendaAtual * 100, 2);
    }
}
