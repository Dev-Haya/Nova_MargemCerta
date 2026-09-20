namespace EstoqueApp.Forms
{
    public sealed class FilterForm : Form
    {
        private readonly CheckedListBox _colunas = new();
        private readonly ComboBox _situacao = new();
        private readonly TextBox _texto = new();
        private readonly IReadOnlyDictionary<string, string> _nomesColunas;

        public string Texto => _texto.Text.Trim();
        public string Situacao => _situacao.SelectedItem?.ToString() ?? "Todos";
        public HashSet<string> ColunasVisiveis { get; } = new(StringComparer.OrdinalIgnoreCase);

        public FilterForm(IEnumerable<(string Nome, string Titulo)> colunas, IEnumerable<string> visiveis, string texto, string situacao)
        {
            _nomesColunas = colunas.ToDictionary(item => item.Nome, item => item.Titulo);
            ColunasVisiveis.UnionWith(visiveis);
            Text = "Filtros e colunas";
            Width = 430;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;

            var painel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 5 };
            painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            painel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            painel.Controls.Add(new Label { Text = "Buscar:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            _texto.Text = texto;
            _texto.Dock = DockStyle.Fill;
            painel.Controls.Add(_texto, 1, 0);
            painel.Controls.Add(new Label { Text = "Situação:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            _situacao.Items.AddRange(new object[] { "Todos", "Com alteração", "Em quarentena", "Aprovados" });
            _situacao.SelectedItem = situacao;
            if (_situacao.SelectedIndex < 0) _situacao.SelectedIndex = 0;
            _situacao.Dock = DockStyle.Fill;
            painel.Controls.Add(_situacao, 1, 1);
            painel.Controls.Add(new Label { Text = "Colunas visíveis:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            _colunas.Dock = DockStyle.Fill;
            _colunas.CheckOnClick = true;
            foreach (var coluna in _nomesColunas)
                _colunas.Items.Add(new ColunaItem(coluna.Key, coluna.Value), visiveis.Contains(coluna.Key));
            painel.Controls.Add(_colunas, 1, 2);
            painel.SetRowSpan(_colunas, 2);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft };
            var aplicar = new Button { Text = "Aplicar", DialogResult = DialogResult.OK, Width = 90 };
            var cancelar = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90 };
            botoes.Controls.Add(aplicar);
            botoes.Controls.Add(cancelar);
            Controls.Add(painel);
            Controls.Add(botoes);
            AcceptButton = aplicar;
            CancelButton = cancelar;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                foreach (ColunaItem coluna in _colunas.CheckedItems)
                    ColunasVisiveis.Add(coluna.Nome);
            }
            base.OnFormClosing(e);
        }

        private sealed record ColunaItem(string Nome, string Titulo)
        {
            public override string ToString() => Titulo;
        }
    }
}