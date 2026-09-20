using EstoqueApp.Models;

namespace EstoqueApp.Forms
{
    public sealed class ApprovalReviewForm : Form
    {
        private readonly DataGridView _grid = new();
        private readonly List<ItemEstoque> _itens;

        public List<ItemEstoque> ItensAprovados => _itens.Where(item => item.Aprovado).ToList();

        public ApprovalReviewForm(IEnumerable<ItemEstoque> itens)
        {
            _itens = itens.ToList();
            Text = "Revisar alterações de preço";
            Width = 900;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;

            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Aprovado", HeaderText = "Aprovar", DataPropertyName = "Aprovado", Width = 65 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Código", DataPropertyName = "Codigo", Width = 90, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Descrição", DataPropertyName = "Descricao", Width = 240, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Venda atual", DataPropertyName = "PrecoVendaAtual", Width = 110, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Venda sugerida", DataPropertyName = "PrecoVendaSugerido", Width = 120, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Diferença", DataPropertyName = "DiferencaPercentual", Width = 90, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2'%'" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Alerta", DataPropertyName = "Alerta", Width = 170, ReadOnly = true });
            _grid.DataSource = _itens;

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft };
            var confirmarTodos = new Button { Text = "Aprovar todos e salvar", Width = 160 };
            confirmarTodos.Click += (_, _) => Confirmar(true);
            var salvarSelecionados = new Button { Text = "Salvar selecionados", Width = 135 };
            salvarSelecionados.Click += (_, _) => Confirmar(false);
            var cancelar = new Button { Text = "Cancelar", Width = 90, DialogResult = DialogResult.Cancel };
            botoes.Controls.Add(confirmarTodos);
            botoes.Controls.Add(salvarSelecionados);
            botoes.Controls.Add(cancelar);
            Controls.Add(_grid);
            Controls.Add(botoes);
            CancelButton = cancelar;
        }

        private void Confirmar(bool todos)
        {
            if (todos)
                foreach (var item in _itens) item.Aprovado = true;
            _grid.EndEdit();
            if (ItensAprovados.Count == 0)
            {
                MessageBox.Show("Selecione ao menos uma alteração.", "Aprovação", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}