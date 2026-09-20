using EstoqueApp.Data;
using EstoqueApp.Models;
using EstoqueApp.Services;
using EstoqueApp.Utils;
using System.Text.Json;
using System.Globalization;
using System.Text;

namespace EstoqueApp.Forms
{
    public class MainForm : Form
    {
        private readonly DatabaseService _db = new();
        private readonly PythonIntegrationService _pythonApi = new();
        private readonly ProcessoPythonController _pythonProcesso = new();
        private readonly ColumnLayoutService _layoutsColunas = new();
        private readonly DataGridView _grid = new();
        private readonly Label _lblStatus = new();
        private readonly StatusStrip _statusStrip = new();
        private readonly ToolStripStatusLabel _lblTema = new();
        private readonly ToolStripProgressBar _progresso = new() { Width = 140, Visible = false, Style = ProgressBarStyle.Marquee };
        private readonly ContextMenuStrip _menuCabecalho = new();
        private readonly ToolTip _atalhosToolTip = new() { AutoPopDelay = 5000, InitialDelay = 400, ReshowDelay = 200, ShowAlways = true };

        private List<ItemEstoque> _itensAtuais = new();
        private RegrasNegocio _regras = CarregarRegras();
        private string _textoFiltro = string.Empty;
        private string _situacaoFiltro = "Todos";
        private HashSet<string> _colunasVisiveis = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _filtrosCabecalho = new(StringComparer.OrdinalIgnoreCase);
        private bool _temaEscuro;
        private FileSystemWatcher? _watcherPlanilhas;
        private string? _pastaMonitorada;
        private readonly HashSet<string> _arquivosEmProcessamento = new(StringComparer.OrdinalIgnoreCase);

        public MainForm()
        {
            Text = "Controle de Estoque";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 440);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            _temaEscuro = string.Equals(Environment.GetEnvironmentVariable("APP_THEME"), "dark", StringComparison.OrdinalIgnoreCase);

            ConfigurarDragAndDrop();
            ConfigurarGrid();
            ConfigurarBotoes();
            ConfigurarStatusBar();
            AplicarTema();

            Load += async (_, _) =>
            {
                await IniciarMotorPythonAsync();
                await CarregarTabelaAsync();
            };
            FormClosed += (_, _) =>
            {
                PararMonitoramento();
                _pythonProcesso.Dispose();
            };
        }

        // ---------- Drag and drop de planilhas ----------
        private void ConfigurarDragAndDrop()
        {
            AllowDrop = true;
            DragEnter += (_, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                    e.Effect = DragDropEffects.Copy;
            };
            DragDrop += async (_, e) =>
            {
                var arquivos = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
                if (arquivos is { Length: > 0 })
                    await ImportarPlanilhaAsync(arquivos[0]);
            };
        }

        // ---------- Tabela comparativa (preço antigo x novo, com alertas) ----------
        private void ConfigurarGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.AllowUserToOrderColumns = true;
            _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            _grid.EnableHeadersVisualStyles = false;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _grid.RowTemplate.Height = 30;
            _grid.RowHeadersVisible = false;
            _grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;

            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Aprovado", HeaderText = "Aprovar?", DataPropertyName = "Aprovado", Width = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Codigo", HeaderText = "Código", DataPropertyName = "Codigo", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Descricao", HeaderText = "Descrição", DataPropertyName = "Descricao", Width = 260, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 180, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Marca", HeaderText = "Marca", DataPropertyName = "Marca", Width = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fornecedor", HeaderText = "Fornecedor", DataPropertyName = "Fornecedor", Width = 140 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoCusto", HeaderText = "Custo", DataPropertyName = "PrecoCusto", Width = 100, DefaultCellStyle = EstiloMonetario() });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ValorAcumuladoCusto", HeaderText = "Valor acumulado (custo)", DataPropertyName = "ValorAcumuladoCusto", Width = 150, ReadOnly = true, DefaultCellStyle = EstiloMonetario() });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoVendaAtual", HeaderText = "Venda atual", DataPropertyName = "PrecoVendaAtual", Width = 110, DefaultCellStyle = EstiloMonetario() });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoVendaSugerido", HeaderText = "Venda sugerida", DataPropertyName = "PrecoVendaSugerido", Width = 120, DefaultCellStyle = EstiloMonetario() });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantidade", HeaderText = "Qtd.", DataPropertyName = "Quantidade", Width = 80, DefaultCellStyle = EstiloNumerico() });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Alerta", HeaderText = "Alerta", DataPropertyName = "Alerta", Width = 190, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150, DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True } });

            _grid.RowPrePaint += Grid_RowPrePaint;

            foreach (DataGridViewColumn coluna in _grid.Columns)
                coluna.SortMode = coluna is DataGridViewCheckBoxColumn ? DataGridViewColumnSortMode.NotSortable : DataGridViewColumnSortMode.Automatic;

            foreach (DataGridViewColumn coluna in _grid.Columns)
                coluna.ReadOnly = coluna.Name != "Aprovado";

            _grid.Columns["Aprovado"].Frozen = true;
            _grid.Columns["Codigo"].Frozen = true;
            _grid.Columns["Descricao"].Frozen = true;

            _colunasVisiveis = _grid.Columns.Cast<DataGridViewColumn>()
                .Select(coluna => coluna.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Controls.Add(_grid);
        }

        private static DataGridViewCellStyle EstiloMonetario() => new()
        {
            Format = "C2",
            Alignment = DataGridViewContentAlignment.MiddleRight
        };

        private static DataGridViewCellStyle EstiloNumerico() => new()
        {
            Alignment = DataGridViewContentAlignment.MiddleRight
        };

        private void Grid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            if (_grid.Rows[e.RowIndex].DataBoundItem is not ItemEstoque item) return;
            if (_grid.Columns[e.ColumnIndex].Name == "Descricao") e.ToolTipText = item.Descricao;
            if (_grid.Columns[e.ColumnIndex].Name == "Alerta") e.ToolTipText = item.Alerta ?? string.Empty;
        }

        private void ConfigurarBotoes()
        {
            var painel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 78,
                Padding = new Padding(8),
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight
            };

            var btnImportar = new Button { Text = "Importar Planilha" };
            _atalhosToolTip.SetToolTip(btnImportar, "Importar planilhas (Ctrl+I)");
            btnImportar.Click += async (_, _) =>
            {
                using var dialog = new OpenFileDialog { Filter = "Planilhas|*.xlsx;*.csv", Multiselect = true };
                if (dialog.ShowDialog() == DialogResult.OK)
                    await ImportarPlanilhasAsync(dialog.FileNames);
            };

            var btnEnviarPython = new Button { Text = "Processar dados" };
            _atalhosToolTip.SetToolTip(btnEnviarPython, "Processar preços (Ctrl+P)");
            btnEnviarPython.Click += async (_, _) => await EnviarParaPythonAsync();

            var btnRegras = new Button { Text = "Configurar regras" };
            btnRegras.Click += (_, _) => ConfigurarRegras();

            var btnFiltros = new Button { Text = "Filtros e colunas" };
            _atalhosToolTip.SetToolTip(btnFiltros, "Abrir filtros e colunas (Ctrl+F)");
            btnFiltros.Click += (_, _) => AbrirFiltros();

            var btnTema = new Button { Text = "Alternar tema" };
            _atalhosToolTip.SetToolTip(btnTema, "Alternar tema claro/escuro (Ctrl+D)");
            btnTema.Click += (_, _) => AlternarTema();

            var btnDashboard = new Button { Text = "Dashboard" };
            btnDashboard.Click += (_, _) => AbrirDashboard();

            var btnExportar = new Button { Text = "Exportar CSV" };
            btnExportar.Click += (_, _) => ExportarCsv();

            var btnHistorico = new Button { Text = "Histórico / backup" };
            btnHistorico.Click += (_, _) => AbrirHistorico();

            var btnMonitorar = new Button { Text = "Monitorar pasta" };
            btnMonitorar.Click += (_, _) => AlternarMonitoramento();

            var btnCadastro = new Button { Text = "Manutenção de cadastro" };
            btnCadastro.Click += (_, _) => AbrirManutencao();

            var btnAprovar = new Button { Text = "Aprovar" };
            _atalhosToolTip.SetToolTip(btnAprovar, "Revisar e aprovar alterações (Ctrl+A)");
            btnAprovar.Click += async (_, _) => await EfetivarAtualizacaoAsync();

            _lblStatus.AutoSize = true;
            _lblStatus.Padding = new Padding(15, 10, 0, 0);

            painel.Controls.Add(btnImportar);
            painel.Controls.Add(btnEnviarPython);
            painel.Controls.Add(btnRegras);
            painel.Controls.Add(btnFiltros);
            painel.Controls.Add(btnTema);
            painel.Controls.Add(btnDashboard);
            painel.Controls.Add(btnExportar);
            painel.Controls.Add(btnHistorico);
            painel.Controls.Add(btnMonitorar);
            painel.Controls.Add(btnCadastro);
            painel.Controls.Add(btnAprovar);
            painel.Controls.Add(_lblStatus);
            Controls.Add(painel);
        }

        private void Grid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var linha = _grid.Rows[e.RowIndex];
            var corBase = e.RowIndex % 2 == 0
                ? (_temaEscuro ? Color.FromArgb(45, 47, 50) : Color.White)
                : (_temaEscuro ? Color.FromArgb(51, 54, 58) : Color.FromArgb(245, 247, 249));
            if (linha.DataBoundItem is not ItemEstoque item)
            {
                linha.DefaultCellStyle.BackColor = corBase;
                linha.DefaultCellStyle.ForeColor = _temaEscuro ? Color.WhiteSmoke : SystemColors.ControlText;
                return;
            }

            linha.DefaultCellStyle.BackColor = item.Quarentena
                ? (_temaEscuro ? Color.FromArgb(92, 50, 55) : Color.MistyRose)
                : item.PrecoVendaSugerido > item.PrecoVendaAtual
                    ? (_temaEscuro ? Color.FromArgb(82, 76, 32) : Color.LightYellow)
                    : item.PrecoVendaSugerido < item.PrecoVendaAtual
                        ? (_temaEscuro ? Color.FromArgb(36, 76, 52) : Color.Honeydew)
                        : corBase;
            linha.DefaultCellStyle.ForeColor = item.Quarentena
                ? (_temaEscuro ? Color.FromArgb(255, 180, 180) : Color.DarkRed)
                : (_temaEscuro ? Color.WhiteSmoke : Color.DarkSlateGray);
        }

        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.I) { e.SuppressKeyPress = true; AbrirDialogoImportacao(); }
            else if (e.Control && e.KeyCode == Keys.P) { e.SuppressKeyPress = true; _ = EnviarParaPythonAsync(); }
            else if (e.Control && e.KeyCode == Keys.A) { e.SuppressKeyPress = true; _ = EfetivarAtualizacaoAsync(); }
            else if (e.Control && e.KeyCode == Keys.F) { e.SuppressKeyPress = true; AbrirFiltros(); }
            else if (e.Control && e.KeyCode == Keys.D) { e.SuppressKeyPress = true; AlternarTema(); }
        }

        private void AbrirDialogoImportacao()
        {
            using var dialog = new OpenFileDialog { Filter = "Planilhas|*.xlsx;*.csv", Multiselect = true };
            if (dialog.ShowDialog() == DialogResult.OK)
                _ = ImportarPlanilhasAsync(dialog.FileNames);
        }

        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.ColumnIndex < 0) return;
            var coluna = _grid.Columns[e.ColumnIndex];
            var entrada = new ToolStripTextBox { Text = _filtrosCabecalho.GetValueOrDefault(coluna.Name, "") };
            _menuCabecalho.Items.Clear();
            _menuCabecalho.Items.Add($"Filtrar: {coluna.HeaderText}");
            _menuCabecalho.Items.Add(entrada);
            var aplicar = _menuCabecalho.Items.Add("Aplicar filtro");
            aplicar.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(entrada.Text)) _filtrosCabecalho.Remove(coluna.Name);
                else _filtrosCabecalho[coluna.Name] = entrada.Text.Trim();
                AplicarFiltros();
            };
            _menuCabecalho.Items.Add("Limpar filtro").Click += (_, _) => { _filtrosCabecalho.Remove(coluna.Name); AplicarFiltros(); };
            _menuCabecalho.Show(_grid, _grid.PointToClient(Cursor.Position));
        }

        private void ConfigurarStatusBar()
        {
            _statusStrip.Items.Add(_lblTema);
            _statusStrip.Items.Add(_progresso);
            _statusStrip.Dock = DockStyle.Bottom;
            Controls.Add(_statusStrip);
        }

        // ---------- Fluxo principal ----------
        private async Task CarregarTabelaAsync()
        {
            _itensAtuais = await Task.Run(() => _db.ObterTodos());
            _grid.DataSource = _itensAtuais;
            AplicarFiltros();
            _lblStatus.Text = $"{_itensAtuais.Count} itens carregados.";
        }

        private async Task ImportarPlanilhaAsync(string caminhoArquivo)
        {
            DefinirProgresso(true);
            try
            {
                var cabecalhos = PlanilhaImporter.LerCabecalhos(caminhoArquivo);

                var mapeamento = ObterMapeamento(cabecalhos);
                if (mapeamento is null) return;

                var itensImportados = PlanilhaImporter.Importar(caminhoArquivo, mapeamento);
                var erros = PlanilhaImporter.ValidarItens(itensImportados);
                if (erros.Count > 0)
                {
                    MessageBox.Show(string.Join(Environment.NewLine, erros.Take(15)), "Dados inválidos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _db.CarregarItensImportados(itensImportados);
                await CarregarTabelaAsync();

                _lblStatus.Text = $"Importação concluída: {itensImportados.Count} itens processados.";
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("A planilha não foi encontrada. Ela pode ter sido movida durante a importação.", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show($"A planilha está inválida ou corrompida: {ex.Message}", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Não foi possível ler o arquivo. Verifique se ele está aberto em outro programa. Detalhe: {ex.Message}", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro inesperado ao importar a planilha: {ex.Message}", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                DefinirProgresso(false);
            }
        }

        private async Task ImportarPlanilhasAsync(IEnumerable<string> caminhos)
        {
            var arquivos = caminhos.ToList();
            if (arquivos.Count == 1)
            {
                await ImportarPlanilhaAsync(arquivos[0]);
                return;
            }

            var total = 0;
            DefinirProgresso(true);
            try
            {
                foreach (var caminho in arquivos)
                {
                    try
                    {
                        var cabecalhos = PlanilhaImporter.LerCabecalhos(caminho);
                        var mapeamento = ObterMapeamento(cabecalhos);
                        if (mapeamento is null) break;
                        var itens = PlanilhaImporter.Importar(caminho, mapeamento);
                        var erros = PlanilhaImporter.ValidarItens(itens);
                        if (erros.Count > 0)
                        {
                            MessageBox.Show($"{Path.GetFileName(caminho)}:\n{string.Join(Environment.NewLine, erros.Take(10))}", "Arquivo ignorado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            continue;
                        }
                        _db.CarregarItensImportados(itens);
                        total += itens.Count;
                    }
                    catch (IOException ex)
                    {
                        MessageBox.Show($"Não foi possível ler {Path.GetFileName(caminho)}. Verifique se ele está aberto. Detalhe: {ex.Message}", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                await CarregarTabelaAsync();
                _lblStatus.Text = $"Importação em lote concluída: {total} item(ns).";
            }
            finally
            {
                DefinirProgresso(false);
            }
        }

        private MapeamentoColunas? ObterMapeamento(IReadOnlyList<string> cabecalhos)
        {
            if (_layoutsColunas.TentarObter(cabecalhos, out var salvo) && salvo is not null)
                return salvo;

            using var mapper = new ColumnMapperForm(cabecalhos.ToList());
            if (mapper.ShowDialog(this) != DialogResult.OK || mapper.Resultado is null)
                return null;

            _layoutsColunas.Salvar(cabecalhos, mapper.Resultado);
            return mapper.Resultado;
        }

        private void DefinirProgresso(bool ativo)
        {
            _progresso.Visible = ativo;
            _progresso.Style = ProgressBarStyle.Marquee;
        }

        private async Task EnviarParaPythonAsync()
        {
            if (_itensAtuais.Count == 0)
            {
                _lblStatus.Text = "Importe uma planilha antes de processar.";
                return;
            }

            if (!await _pythonApi.VerificarConexaoAsync())
            {
                _lblStatus.Text = "Motor Python indisponível em http://127.0.0.1:8000.";
                return;
            }

            var requisicao = new RequisicaoProcessamento
            {
                PlanilhaFornecedor = _itensAtuais.Select(item => new ItemPlanilha
                {
                    Codigo = item.Codigo,
                    Descricao = item.Descricao,
                    PrecoCusto = item.PrecoCusto,
                    Marca = item.Marca,
                    Categoria = item.Categoria,
                    Fornecedor = item.Fornecedor
                }).ToList(),
                Regras = _regras,
                DadosAtuais = _itensAtuais.Select(item => new ItemDadoAtual
                {
                    Codigo = item.Codigo,
                    PrecoVendaAtual = item.PrecoVendaAtual,
                    Estoque = item.Quantidade
                }).ToList()
            };

            var resultado = await _pythonApi.ProcessarPrecosAsync(requisicao);
            if (resultado is null || !resultado.Sucesso)
            {
                _lblStatus.Text = resultado?.Mensagem ?? "Falha ao processar os preços.";
                return;
            }

            var processados = resultado.Itens.ToDictionary(item => item.Codigo, StringComparer.OrdinalIgnoreCase);
            foreach (var item in _itensAtuais)
            {
                if (!processados.TryGetValue(item.Codigo, out var processado)) continue;

                item.PrecoVendaSugerido = processado.PrecoVendaSugerido;
                item.Quarentena = processado.Quarentena;
                item.Aprovado = !processado.Quarentena;
                item.Alerta = processado.Alerta;
            }

            _grid.Refresh();
            _lblStatus.Text = $"{resultado.Resumo.TotalItens} itens processados; " +
                              $"{resultado.Resumo.ItensEmQuarentena} em quarentena.";
        }

        private async Task IniciarMotorPythonAsync()
        {
            try
            {
                _lblStatus.Text = "Iniciando motor Python...";
                await _pythonProcesso.IniciarAsync(_pythonApi);
                _lblStatus.Text = "Motor Python pronto.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Motor Python indisponível: {ex.Message}";
            }
        }

        private void AlternarMonitoramento()
        {
            if (_watcherPlanilhas is not null)
            {
                PararMonitoramento();
                _lblStatus.Text = "Monitoramento de pasta parado.";
                return;
            }

            using var dialogo = new FolderBrowserDialog { Description = "Escolha a pasta que receberá as planilhas" };
            if (dialogo.ShowDialog(this) != DialogResult.OK) return;

            _pastaMonitorada = dialogo.SelectedPath;
            _watcherPlanilhas = new FileSystemWatcher(_pastaMonitorada)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            _watcherPlanilhas.Created += PlanilhaDetectada;
            _watcherPlanilhas.Renamed += PlanilhaDetectada;
            _lblStatus.Text = $"Monitorando {_pastaMonitorada}.";
        }

        private void PlanilhaDetectada(object sender, FileSystemEventArgs evento)
        {
            if (!evento.FullPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
                !evento.FullPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) return;
            _ = BeginInvoke(new Action(async () => await ImportarArquivoMonitoradoAsync(evento.FullPath)));
        }

        private async Task ImportarArquivoMonitoradoAsync(string caminho)
        {
            if (!_arquivosEmProcessamento.Add(caminho)) return;
            try
            {
                for (var tentativa = 0; tentativa < 20; tentativa++)
                {
                    try
                    {
                        using var arquivo = File.Open(caminho, FileMode.Open, FileAccess.Read, FileShare.Read);
                        break;
                    }
                    catch (IOException) { await Task.Delay(500); }
                }
                await ImportarPlanilhaAsync(caminho);
                _lblStatus.Text = $"Planilha detectada e importada: {Path.GetFileName(caminho)}";
            }
            finally
            {
                _arquivosEmProcessamento.Remove(caminho);
            }
        }

        private void PararMonitoramento()
        {
            if (_watcherPlanilhas is null) return;
            _watcherPlanilhas.EnableRaisingEvents = false;
            _watcherPlanilhas.Dispose();
            _watcherPlanilhas = null;
            _pastaMonitorada = null;
        }

        private async Task EfetivarAtualizacaoAsync()
        {
            var itensAlterados = _itensAtuais.Where(i => i.PrecoMudou).ToList();
            if (itensAlterados.Count == 0)
            {
                _lblStatus.Text = "Nenhuma alteração de preço para aprovar.";
                return;
            }

            using var revisao = new ApprovalReviewForm(itensAlterados);
            if (revisao.ShowDialog(this) != DialogResult.OK)
                return;

            var aprovados = revisao.ItensAprovados;
            if (aprovados.Count == 0)
            {
                _lblStatus.Text = "Nenhuma alteração foi aprovada.";
                return;
            }

            await Task.Run(() => _db.EfetivarAtualizacaoEmLote(aprovados));
            await CarregarTabelaAsync();
            _lblStatus.Text = $"{aprovados.Count} alteração(ões) aprovada(s) e salva(s).";
        }

        private void AbrirFiltros()
        {
            var colunas = _grid.Columns.Cast<DataGridViewColumn>()
                .Select(coluna => (coluna.Name, coluna.HeaderText));
            using var dialogo = new FilterForm(colunas, _colunasVisiveis, _textoFiltro, _situacaoFiltro);
            if (dialogo.ShowDialog(this) != DialogResult.OK) return;

            _textoFiltro = dialogo.Texto;
            _situacaoFiltro = dialogo.Situacao;
            _colunasVisiveis = dialogo.ColunasVisiveis;
            AplicarFiltros();
        }

        private void AbrirDashboard()
        {
            using var dashboard = new DashboardForm(_itensAtuais);
            dashboard.ShowDialog(this);
        }

        private void AbrirHistorico()
        {
            using var historico = new BackupForm(_db);
            if (historico.ShowDialog(this) == DialogResult.OK && historico.Restaurado)
                _ = CarregarTabelaAsync();
        }

        private void ExportarCsv()
        {
            using var dialogo = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"precos-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
            if (dialogo.ShowDialog(this) != DialogResult.OK) return;

            var itensVisiveis = _grid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as ItemEstoque)
                .Where(item => item is not null)
                .Cast<ItemEstoque>()
                .ToList();
            var linhas = new List<string> { "codigo;descricao;marca;fornecedor;categoria;preco_custo;valor_acumulado_custo;venda_atual;venda_sugerida;quantidade;quarentena;alerta" };
            linhas.AddRange(itensVisiveis.Select(item => string.Join(";", new[]
            {
                EscaparCsv(item.Codigo), EscaparCsv(item.Descricao), EscaparCsv(item.Marca), EscaparCsv(item.Fornecedor), EscaparCsv(item.Categoria),
                item.PrecoCusto.ToString("F2", CultureInfo.InvariantCulture),
                item.ValorAcumuladoCusto.ToString("F2", CultureInfo.InvariantCulture),
                item.PrecoVendaAtual.ToString("F2", CultureInfo.InvariantCulture),
                item.PrecoVendaSugerido.ToString("F2", CultureInfo.InvariantCulture),
                item.Quantidade.ToString(CultureInfo.InvariantCulture), item.Quarentena ? "sim" : "nao", EscaparCsv(item.Alerta ?? "")
            })));
            File.WriteAllLines(dialogo.FileName, linhas, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            _lblStatus.Text = $"Exportados {itensVisiveis.Count} item(ns).";
        }

        private static string EscaparCsv(string valor)
        {
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        }

        private void AplicarFiltros()
        {
            IEnumerable<ItemEstoque> itens = _itensAtuais;
            if (!string.IsNullOrWhiteSpace(_textoFiltro))
            {
                itens = itens.Where(item => item.Codigo.Contains(_textoFiltro, StringComparison.OrdinalIgnoreCase) ||
                                            item.Descricao.Contains(_textoFiltro, StringComparison.OrdinalIgnoreCase) ||
                                            item.Marca.Contains(_textoFiltro, StringComparison.OrdinalIgnoreCase) ||
                                            item.Fornecedor.Contains(_textoFiltro, StringComparison.OrdinalIgnoreCase) ||
                                            item.Categoria.Contains(_textoFiltro, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var filtro in _filtrosCabecalho)
                itens = itens.Where(item => ObterTextoColuna(item, filtro.Key).Contains(filtro.Value, StringComparison.OrdinalIgnoreCase));

            itens = _situacaoFiltro switch
            {
                "Com alteração" => itens.Where(item => item.PrecoMudou),
                "Em quarentena" => itens.Where(item => item.Quarentena),
                "Aprovados" => itens.Where(item => item.Aprovado),
                _ => itens
            };

            _grid.DataSource = itens.ToList();
            foreach (DataGridViewColumn coluna in _grid.Columns)
                coluna.Visible = _colunasVisiveis.Contains(coluna.Name);

            _lblStatus.Text = $"Exibindo {_grid.Rows.Count} de {_itensAtuais.Count} item(ns).";
        }

        private static string ObterTextoColuna(ItemEstoque item, string coluna) => coluna switch
        {
            "Codigo" => item.Codigo,
            "Descricao" => item.Descricao,
            "Marca" => item.Marca,
            "Fornecedor" => item.Fornecedor,
            "PrecoCusto" => item.PrecoCusto.ToString("C2"),
            "ValorAcumuladoCusto" => item.ValorAcumuladoCusto.ToString("C2"),
            "PrecoVendaAtual" => item.PrecoVendaAtual.ToString("C2"),
            "PrecoVendaSugerido" => item.PrecoVendaSugerido.ToString("C2"),
            "Quantidade" => item.Quantidade.ToString(),
            "Alerta" => item.Alerta ?? string.Empty,
            _ => string.Empty
        };

        private void AlternarTema()
        {
            _temaEscuro = !_temaEscuro;
            Environment.SetEnvironmentVariable("APP_THEME", _temaEscuro ? "dark" : "light");
            AplicarTema();
        }

        private void AplicarTema()
        {
            var fundo = _temaEscuro ? Color.FromArgb(32, 34, 37) : SystemColors.Control;
            var texto = _temaEscuro ? Color.WhiteSmoke : SystemColors.ControlText;
            BackColor = fundo;
            ForeColor = texto;
            _grid.BackgroundColor = _temaEscuro ? Color.FromArgb(45, 47, 50) : Color.White;
            _grid.ForeColor = texto;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = _temaEscuro ? Color.FromArgb(60, 63, 68) : SystemColors.Control;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = texto;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            _grid.ColumnHeadersHeight = 34;
            _grid.DefaultCellStyle.SelectionBackColor = _temaEscuro ? Color.FromArgb(70, 95, 125) : SystemColors.Highlight;
            _grid.DefaultCellStyle.SelectionForeColor = _temaEscuro ? Color.White : SystemColors.HighlightText;
            _statusStrip.BackColor = fundo;
            _statusStrip.ForeColor = texto;
            _lblTema.Text = _temaEscuro ? "Tema escuro" : "Tema claro";
            _grid.Invalidate();
        }

        private void ConfigurarRegras()
        {
            using var dialogo = new BusinessRulesForm(_regras);
            if (dialogo.ShowDialog(this) == DialogResult.OK && dialogo.Resultado is not null)
            {
                _regras = dialogo.Resultado;
                SalvarRegras(_regras);
                _lblStatus.Text = $"Regras atualizadas: {_regras.MargemPorMarca.Count} marcas e {_regras.MargemPorCategoria.Count} categorias.";
            }
        }

        private void AbrirManutencao()
        {
            using var dialogo = new MaintenanceForm(_db);
            if (dialogo.ShowDialog(this) == DialogResult.OK)
                _ = CarregarTabelaAsync();
        }

        private static string CaminhoRegras => Path.Combine(AppContext.BaseDirectory, "regras-negocio.json");

        private static RegrasNegocio CarregarRegras()
        {
            try
            {
                if (File.Exists(CaminhoRegras))
                    return JsonSerializer.Deserialize<RegrasNegocio>(File.ReadAllText(CaminhoRegras)) ?? new RegrasNegocio();
            }
            catch (JsonException) { }
            return new RegrasNegocio();
        }

        private static void SalvarRegras(RegrasNegocio regras)
        {
            var json = JsonSerializer.Serialize(regras, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CaminhoRegras, json);
        }
    }
}
