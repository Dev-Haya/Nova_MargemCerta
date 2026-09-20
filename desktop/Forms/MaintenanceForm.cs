using EstoqueApp.Data;
using EstoqueApp.Models;

namespace EstoqueApp.Forms
{
    public sealed class MaintenanceForm : Form
    {
        private readonly DatabaseService _db;
        private readonly DataGridView _grid = new();
        private readonly TextBox _busca = new();
        private readonly Label _status = new();
        private List<ItemEstoque> _itens = new();
        private Dictionary<int, ItemEstoque> _originais = new();

        public MaintenanceForm(DatabaseService db)
        {
            _db = db;
            Text = "Manutenção de cadastro";
            Width = 1080;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;

            var topo = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
            topo.Controls.Add(new Label { Text = "Buscar:", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
            _busca.Width = 260;
            _busca.TextChanged += (_, _) => AplicarFiltro();
            topo.Controls.Add(_busca);

            ConfigurarGrid();

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
            var backup = new Button { Text = "Criar backup" };
            backup.Click += (_, _) => CriarBackup();
            var salvar = new Button { Text = "Salvar alterações", Width = 130 };
            salvar.Click += (_, _) => Salvar();
            var excluir = new Button { Text = "Excluir selecionado(s)", Width = 145 };
            excluir.Click += (_, _) => ExcluirSelecionados();
            var fechar = new Button { Text = "Fechar", DialogResult = DialogResult.Cancel };
            botoes.Controls.Add(backup);
            botoes.Controls.Add(salvar);
            botoes.Controls.Add(excluir);
            botoes.Controls.Add(fechar);
            _status.AutoSize = true;
            _status.Padding = new Padding(16, 8, 0, 0);
            botoes.Controls.Add(_status);

            Controls.Add(_grid);
            Controls.Add(topo);
            Controls.Add(botoes);
            CancelButton = fechar;
            Load += (_, _) => Carregar();
        }

        private void ConfigurarGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Codigo", HeaderText = "Código", DataPropertyName = "Codigo", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Descricao", HeaderText = "Descrição", DataPropertyName = "Descricao", Width = 240 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Marca", HeaderText = "Marca", DataPropertyName = "Marca", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fornecedor", HeaderText = "Fornecedor", DataPropertyName = "Fornecedor", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Categoria", HeaderText = "Categoria", DataPropertyName = "Categoria", Width = 130 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoCusto", HeaderText = "Custo", DataPropertyName = "PrecoCusto", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoVendaAtual", HeaderText = "Venda atual", DataPropertyName = "PrecoVendaAtual", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoVendaSugerido", HeaderText = "Venda sugerida", DataPropertyName = "PrecoVendaSugerido", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantidade", HeaderText = "Estoque", DataPropertyName = "Quantidade", Width = 80 });
        }

        private void Carregar()
        {
            _itens = _db.ObterTodos();
            _originais = _itens.ToDictionary(item => item.Id, Clonar);
            _grid.DataSource = _itens;
            _status.Text = $"{_itens.Count} cadastro(s).";
        }

        private void AplicarFiltro()
        {
            var termo = _busca.Text.Trim();
            _grid.DataSource = string.IsNullOrEmpty(termo)
                ? _itens
                : _itens.Where(item => item.Codigo.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                                       item.Descricao.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                                       item.Marca.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                                       item.Fornecedor.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void CriarBackup()
        {
            try
            {
                var caminho = _db.CriarBackup();
                _status.Text = $"Backup criado: {Path.GetFileName(caminho)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível criar o backup: {ex.Message}", "Backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Salvar()
        {
            if (!Autorizar()) return;

            var confirmar = MessageBox.Show("Salvar as alterações cadastrais? Um backup será criado antes da gravação.",
                "Confirmar manutenção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmar != DialogResult.Yes) return;

            try
            {
                _db.CriarBackup();
                var usuario = Environment.GetEnvironmentVariable("APP_USER") ?? Environment.UserName;
                _db.SalvarManutencao(_itens, _originais, usuario);
                _status.Text = "Alterações salvas e registradas na auditoria.";
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível salvar: {ex.Message}", "Validação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExcluirSelecionados()
        {
            if (!Autorizar()) return;

            var selecionados = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as ItemEstoque)
                .Where(item => item is not null)
                .Cast<ItemEstoque>()
                .DistinctBy(item => item.Id)
                .ToList();

            if (selecionados.Count == 0)
            {
                MessageBox.Show("Selecione ao menos um registro para excluir.", "Exclusão", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var resumo = string.Join(Environment.NewLine, selecionados.Take(8).Select(item => $"- {item.Codigo}: {item.Descricao}"));
            if (selecionados.Count > 8) resumo += Environment.NewLine + $"... e mais {selecionados.Count - 8} registro(s).";
            var confirmar = MessageBox.Show(
                $"A exclusão é permanente no cadastro. Um backup será criado antes da operação.\n\n{resumo}\n\nContinuar?",
                "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmar != DialogResult.Yes) return;

            try
            {
                _db.CriarBackup();
                var usuario = Environment.GetEnvironmentVariable("APP_USER") ?? Environment.UserName;
                _db.ExcluirItens(selecionados, usuario);
                Carregar();
                _status.Text = $"{selecionados.Count} registro(s) excluído(s) e auditado(s).";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível excluir: {ex.Message}", "Exclusão", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool Autorizar()
        {
            var senhaConfigurada = Environment.GetEnvironmentVariable("APP_ADMIN_PASSWORD");
            if (string.IsNullOrEmpty(senhaConfigurada))
            {
                return (Environment.GetEnvironmentVariable("APP_ENVIRONMENT") ?? "Development")
                    .Equals("Development", StringComparison.OrdinalIgnoreCase);
            }

            using var prompt = new Form { Text = "Autorização", Width = 340, Height = 145, StartPosition = FormStartPosition.CenterParent };
            var senha = new TextBox { Left = 20, Top = 18, Width = 280, UseSystemPasswordChar = true };
            var ok = new Button { Text = "Confirmar", Left = 205, Top = 55, Width = 95, DialogResult = DialogResult.OK };
            prompt.Controls.Add(new Label { Text = "Senha administrativa:", Left = 20, Top = 0, AutoSize = true });
            prompt.Controls.Add(senha);
            prompt.Controls.Add(ok);
            prompt.AcceptButton = ok;
            return prompt.ShowDialog() == DialogResult.OK && senha.Text == senhaConfigurada;
        }

        private static ItemEstoque Clonar(ItemEstoque item)
        {
            return new ItemEstoque
            {
                Id = item.Id, Codigo = item.Codigo, Descricao = item.Descricao,
                Marca = item.Marca, Categoria = item.Categoria,
                Fornecedor = item.Fornecedor,
                PrecoCusto = item.PrecoCusto, PrecoVendaAtual = item.PrecoVendaAtual,
                PrecoVendaSugerido = item.PrecoVendaSugerido,
                Quantidade = item.Quantidade
            };
        }
    }
}