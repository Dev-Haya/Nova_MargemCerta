using EstoqueApp.Utils;

namespace EstoqueApp.Forms
{
    /// <summary>
    /// Tela que permite ao usuário escolher dinamicamente qual cabeçalho
    /// da planilha corresponde a cada campo do item.
    /// </summary>
    public class ColumnMapperForm : Form
    {
        private readonly ComboBox _cboCodigo = new();
        private readonly ComboBox _cboDescricao = new();
        private readonly ComboBox _cboPrecoUnitario = new();
        private readonly ComboBox _cboQuantidade = new();
        private readonly ComboBox _cboMarca = new();
        private readonly ComboBox _cboFornecedor = new();
        private readonly ComboBox _cboCategoria = new();

        public MapeamentoColunas? Resultado { get; private set; }

        public ColumnMapperForm(List<string> cabecalhosDisponiveis)
        {
            Text = "Mapear Colunas da Planilha";
            Width = 420;
            Height = 410;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var combos = new[] { _cboCodigo, _cboDescricao, _cboPrecoUnitario, _cboQuantidade, _cboMarca, _cboFornecedor, _cboCategoria };
            var rotulos = new[] { "Código:", "Descrição:", "Preço unitário (custo):", "Quantidade:", "Marca (opcional):", "Fornecedor (opcional):", "Categoria (opcional):" };

            for (int i = 0; i < combos.Length; i++)
            {
                var lbl = new Label { Text = rotulos[i], Left = 20, Top = 20 + i * 40, Width = 100 };
                combos[i].Left = 130;
                combos[i].Top = 18 + i * 40;
                combos[i].Width = 240;
                combos[i].DropDownStyle = ComboBoxStyle.DropDownList;
                combos[i].Items.AddRange(cabecalhosDisponiveis.ToArray());
                Controls.Add(lbl);
                Controls.Add(combos[i]);
            }

            _cboMarca.Items.Insert(0, "(não informar)");
            _cboMarca.SelectedIndex = 0;
            _cboFornecedor.Items.Insert(0, "(não informar)");
            _cboFornecedor.SelectedIndex = 0;
            _cboCategoria.Items.Insert(0, "(não informar)");
            _cboCategoria.SelectedIndex = 0;

            var btnConfirmar = new Button { Text = "Confirmar", Left = 130, Top = 280, Width = 100 };
            btnConfirmar.Click += BtnConfirmar_Click;

            var btnCancelar = new Button { Text = "Cancelar", Left = 240, Top = 280, Width = 100 };
            btnCancelar.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(btnConfirmar);
            Controls.Add(btnCancelar);
        }

        private void BtnConfirmar_Click(object? sender, EventArgs e)
        {
            if (_cboCodigo.SelectedItem is null || _cboDescricao.SelectedItem is null ||
                _cboPrecoUnitario.SelectedItem is null || _cboQuantidade.SelectedItem is null)
            {
                MessageBox.Show("Selecione uma coluna para cada campo.", "Atenção",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Resultado = new MapeamentoColunas
            {
                ColunaCodigo = _cboCodigo.SelectedItem!.ToString()!,
                ColunaDescricao = _cboDescricao.SelectedItem!.ToString()!,
                ColunaPrecoUnitario = _cboPrecoUnitario.SelectedItem!.ToString()!,
                ColunaQuantidade = _cboQuantidade.SelectedItem!.ToString()!,
                ColunaMarca = _cboMarca.SelectedItem?.ToString() == "(não informar)" ? null : _cboMarca.SelectedItem?.ToString(),
                ColunaFornecedor = _cboFornecedor.SelectedItem?.ToString() == "(não informar)" ? null : _cboFornecedor.SelectedItem?.ToString(),
                ColunaCategoria = _cboCategoria.SelectedItem?.ToString() == "(não informar)" ? null : _cboCategoria.SelectedItem?.ToString()
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
