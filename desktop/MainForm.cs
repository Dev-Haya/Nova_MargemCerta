using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PrecoSyncApp.Data;
using PrecoSyncApp.Models;
using PrecoSyncApp.Services;

namespace PrecoSyncApp.Forms
{
    /// <summary>
    /// Tela principal: reúne o mapeador de colunas, a tabela comparativa
    /// (preço antigo x novo, com alerta vermelho para itens em quarentena)
    /// e os botões que disparam a comunicação com o serviço Python.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly DatabaseService _db = new DatabaseService();
        private readonly PythonProcessManager _pythonManager = new PythonProcessManager();
        private readonly PythonApiClient _apiClient = new PythonApiClient();
        // Troque pela URL real do seu projeto Firebase:
        private readonly LicenseService _licenseService = new LicenseService("https://SEU-PROJETO-default-rtdb.firebaseio.com");

        private DataGridView gridComparativo;
        private ComboBox cmbColunaCodigo, cmbColunaDescricao, cmbColunaPreco;
        private Button btnImportarPlanilha, btnEnviarPython, btnConfirmarAlteracoes;
        private Label lblStatus;

        private DataTable _planilhaCarregada;
        private List<Produto> _produtosAtuais = new List<Produto>();

        public MainForm()
        {
            InitializeComponent();
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
        }

        /// <summary>
        /// Monta a UI em código (drag-and-drop no Designer.cs é o normal em
        /// projeto real; aqui está explícito para ficar claro o que cada
        /// controle faz).
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "PrecoSync - Conferência de Preços";
            this.Width = 1000;
            this.Height = 650;
            this.StartPosition = FormStartPosition.CenterScreen;

            var painelMapeamento = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 90,
                Padding = new Padding(10)
            };

            btnImportarPlanilha = new Button { Text = "Importar Planilha", AutoSize = true };
            btnImportarPlanilha.Click += BtnImportarPlanilha_Click;

            cmbColunaCodigo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbColunaDescricao = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbColunaPreco = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

            painelMapeamento.Controls.Add(btnImportarPlanilha);
            painelMapeamento.Controls.Add(new Label { Text = "Coluna Código:", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
            painelMapeamento.Controls.Add(cmbColunaCodigo);
            painelMapeamento.Controls.Add(new Label { Text = "Coluna Descrição:", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
            painelMapeamento.Controls.Add(cmbColunaDescricao);
            painelMapeamento.Controls.Add(new Label { Text = "Coluna Preço:", AutoSize = true, Padding = new Padding(10, 8, 0, 0) });
            painelMapeamento.Controls.Add(cmbColunaPreco);

            gridComparativo = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            gridComparativo.Columns.Add(new DataGridViewTextBoxColumn { Name = "Codigo", HeaderText = "Código", DataPropertyName = "Codigo", Width = 100 });
            gridComparativo.Columns.Add(new DataGridViewTextBoxColumn { Name = "Descricao", HeaderText = "Descrição", DataPropertyName = "Descricao", Width = 300 });
            gridComparativo.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoAntigo", HeaderText = "Preço Antigo", DataPropertyName = "PrecoAntigo", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            gridComparativo.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoNovo", HeaderText = "Preço Novo", DataPropertyName = "PrecoNovo", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            gridComparativo.CellFormatting += GridComparativo_CellFormatting;

            var painelInferior = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                Padding = new Padding(10)
            };

            btnEnviarPython = new Button { Text = "Enviar para Análise (Python)", AutoSize = true, Enabled = false };
            btnEnviarPython.Click += BtnEnviarPython_Click;

            btnConfirmarAlteracoes = new Button { Text = "Confirmar Alterações", AutoSize = true, Enabled = false };
            btnConfirmarAlteracoes.Click += BtnConfirmarAlteracoes_Click;

            lblStatus = new Label { Text = "Pronto.", AutoSize = true, Padding = new Padding(20, 8, 0, 0) };

            painelInferior.Controls.Add(btnEnviarPython);
            painelInferior.Controls.Add(btnConfirmarAlteracoes);
            painelInferior.Controls.Add(lblStatus);

            this.Controls.Add(gridComparativo);
            this.Controls.Add(painelInferior);
            this.Controls.Add(painelMapeamento);
        }

        /// <summary>
        /// Ao abrir: sobe o processo Python em background e valida a licença
        /// (trava comercial) antes de liberar a tela.
        /// </summary>
        private async void MainForm_Load(object sender, EventArgs e)
        {
            this.Enabled = false;
            lblStatus.Text = "Validando assinatura...";

            // --- Trava comercial (Firebase) ---
            // Troque "00000000000000" pelo CNPJ real, obtido da config local do cliente.
            var status = await _licenseService.VerificarAssinaturaAsync("00000000000000");
            if (status == StatusLicenca.Vencida || status == StatusLicenca.NaoEncontrada)
            {
                MessageBox.Show("Assinatura vencida ou não encontrada. Entre em contato com o suporte.",
                    "Licença", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
                return;
            }
            // Se ErroDeComunicacao: opcionalmente deixar passar (modo offline
            // tolerante) ou bloquear — decisão de negócio.

            // --- Orquestração do processo Python ---
            lblStatus.Text = "Iniciando serviço de análise...";
            try
            {
                _pythonManager.Iniciar();
                bool disponivel = await _pythonManager.AguardarDisponivelAsync(_apiClient);
                if (!disponivel)
                {
                    MessageBox.Show("O serviço de análise não respondeu a tempo.", "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao iniciar serviço Python: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            CarregarProdutosDoBanco();
            lblStatus.Text = "Pronto.";
            this.Enabled = true;
        }

        /// <summary>
        /// Encerra o processo Python ao fechar a tela — sem deixar processo órfão.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _pythonManager.Parar();
        }

        private void CarregarProdutosDoBanco()
        {
            _produtosAtuais = _db.ListarTodos();
            gridComparativo.DataSource = _produtosAtuais;
        }

        /// <summary>
        /// Mapeador de cabeçalhos: abre um CSV e deixa o usuário escolher
        /// livremente qual coluna é qual, sem depender de planilha padronizada.
        /// </summary>
        private void BtnImportarPlanilha_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog { Filter = "Planilhas CSV (.csv)|.csv|Todos os arquivos (.)|." };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            _planilhaCarregada = LerCsvComoDataTable(dialog.FileName);

            var colunas = _planilhaCarregada.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            foreach (var combo in new[] { cmbColunaCodigo, cmbColunaDescricao, cmbColunaPreco })
            {
                combo.Items.Clear();
                combo.Items.AddRange(colunas);
                if (colunas.Length > 0) combo.SelectedIndex = 0;
            }

            var resultado = MessageBox.Show(
                "Planilha carregada. Selecione as colunas correspondentes e clique em 'Confirmar Mapeamento'.",
                "Mapeamento de Colunas", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Em uma versão completa, um botão dedicado dispararia isso;
            // aqui simplificamos confirmando na sequência:
            ConfirmarMapeamentoEImportar();
        }

        private void ConfirmarMapeamentoEImportar()
        {
            if (_planilhaCarregada == null) return;
            if (cmbColunaCodigo.SelectedItem == null || cmbColunaPreco.SelectedItem == null)
            {
                MessageBox.Show("Selecione ao menos a coluna de Código e a de Preço.");
                return;
            }

            var mapeamento = new MapeamentoColunas
            {
                ColunaCodigo = cmbColunaCodigo.SelectedItem.ToString(),
                ColunaDescricao = cmbColunaDescricao.SelectedItem?.ToString(),
                ColunaPreco = cmbColunaPreco.SelectedItem.ToString()
            };

            var itens = new List<(string Codigo, string Descricao, decimal Preco)>();
            foreach (DataRow row in _planilhaCarregada.Rows)
            {
                string codigo = row[mapeamento.ColunaCodigo]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(codigo)) continue;

                string descricao = mapeamento.ColunaDescricao != null
                    ? row[mapeamento.ColunaDescricao]?.ToString()
                    : "";

                decimal.TryParse(row[mapeamento.ColunaPreco]?.ToString(), out decimal preco);

                itens.Add((codigo, descricao, preco));
            }

            _db.ImportarCargaInicial(itens);
            CarregarProdutosDoBanco();
            btnEnviarPython.Enabled = true;
            lblStatus.Text = $"{itens.Count} itens importados.";
        }

        private DataTable LerCsvComoDataTable(string caminho)
        {
            var tabela = new DataTable();
            var linhas = File.ReadAllLines(caminho);
            if (linhas.Length == 0) return tabela;

            var cabecalhos = linhas[0].Split(';', ',');
            foreach (var cab in cabecalhos) tabela.Columns.Add(cab.Trim());

            for (int i = 1; i < linhas.Length; i++)
            {
                var valores = linhas[i].Split(';', ',');
                if (valores.Length != cabecalhos.Length) continue;
                tabela.Rows.Add(valores);
            }

            return tabela;
        }

        /// <summary>
        /// Envia os produtos atuais para o serviço Python e atualiza a grid
        /// com os preços sugeridos + flags de quarentena.
        /// </summary>
        private async void BtnEnviarPython_Click(object sender, EventArgs e)
        {
            btnEnviarPython.Enabled = false;
            lblStatus.Text = "Enviando dados para análise...";

            try
            {
                var resultado = await _apiClient.EnviarParaProcessamentoAsync(_produtosAtuais);
                _db.AtualizarPrecosSugeridos(resultado);
                CarregarProdutosDoBanco();
                btnConfirmarAlteracoes.Enabled = true;
                lblStatus.Text = "Análise concluída. Revise os preços em vermelho antes de confirmar.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao comunicar com o serviço de análise: {ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Falha na análise.";
            }
            finally
            {
                btnEnviarPython.Enabled = true;
            }
        }

        /// <summary>
        /// Só grava definitivamente no banco (PrecoAntigo = PrecoNovo)
        /// depois do usuário revisar e clicar aqui.
        /// </summary>
        private void BtnConfirmarAlteracoes_Click(object sender, EventArgs e)
        {
            var confirmacao = MessageBox.Show(
                "Confirmar todas as alterações de preço? Esta ação não pode ser desfeita.",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirmacao != DialogResult.Yes) return;

            var codigos = _produtosAtuais.Select(p => p.Codigo);
            _db.ConfirmarAlteracoes(codigos);
            CarregarProdutosDoBanco();
            btnConfirmarAlteracoes.Enabled = false;
            lblStatus.Text = "Alterações confirmadas com sucesso.";
        }

        /// <summary>
        /// Destaca em vermelho as linhas de itens em quarentena
        /// (tag enviada pelo Python indicando revisão manual obrigatória).
        /// </summary>
        private void GridComparativo_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _produtosAtuais.Count) return;

            var produto = _produtosAtuais[e.RowIndex];
            if (produto.EmQuarentena)
            {
                e.CellStyle.BackColor = Color.MistyRose;
                e.CellStyle.ForeColor = Color.DarkRed;
            }
        }
    }
}