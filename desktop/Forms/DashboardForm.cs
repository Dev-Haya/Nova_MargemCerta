using System.Globalization;
using System.Text;
using EstoqueApp.Models;

namespace EstoqueApp.Forms
{
    public sealed class DashboardForm : Form
    {
        public DashboardForm(IReadOnlyCollection<ItemEstoque> itens)
        {
            Text = "Dashboard de preços";
            Width = 760;
            Height = 430;
            StartPosition = FormStartPosition.CenterParent;

            var alterados = itens.Where(item => item.PrecoMudou).ToList();
            var aumentos = alterados.Where(item => item.PrecoVendaSugerido > item.PrecoVendaAtual).ToList();
            var reducoes = alterados.Where(item => item.PrecoVendaSugerido < item.PrecoVendaAtual).ToList();
            var quarentena = itens.Count(item => item.Quarentena);
            var margemMedia = itens.Where(item => item.PrecoCusto > 0 && item.PrecoVendaSugerido > 0)
                .Select(item => (item.PrecoVendaSugerido - item.PrecoCusto) / item.PrecoVendaSugerido)
                .DefaultIfEmpty()
                .Average();
            var faturamentoAtual = itens.Sum(item => item.PrecoVendaAtual * item.Quantidade);
            var faturamentoSugerido = itens.Sum(item => item.PrecoVendaSugerido * item.Quantidade);

            var tabela = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 9 };
            tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            tabela.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            Adicionar(tabela, 0, "Produtos cadastrados", itens.Count.ToString("N0"));
            Adicionar(tabela, 1, "Produtos com alteração", alterados.Count.ToString("N0"));
            Adicionar(tabela, 2, "Aumentos", aumentos.Count.ToString("N0"));
            Adicionar(tabela, 3, "Reduções", reducoes.Count.ToString("N0"));
            Adicionar(tabela, 4, "Itens em quarentena", quarentena.ToString("N0"));
            Adicionar(tabela, 5, "Margem média sugerida", margemMedia.ToString("P2", CultureInfo.CurrentCulture));
            Adicionar(tabela, 6, "Faturamento atual estimado", faturamentoAtual.ToString("C2", CultureInfo.CurrentCulture));
            Adicionar(tabela, 7, "Faturamento sugerido estimado", faturamentoSugerido.ToString("C2", CultureInfo.CurrentCulture));
            Adicionar(tabela, 8, "Impacto estimado", (faturamentoSugerido - faturamentoAtual).ToString("C2", CultureInfo.CurrentCulture));
            Controls.Add(tabela);
        }

        private static void Adicionar(TableLayoutPanel tabela, int linha, string nome, string valor)
        {
            tabela.Controls.Add(new Label { Text = nome, Dock = DockStyle.Fill, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, linha);
            tabela.Controls.Add(new Label { Text = valor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 1, linha);
        }
    }
}