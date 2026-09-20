using EstoqueApp.Models;

namespace EstoqueApp.Forms
{
    public sealed class BusinessRulesForm : Form
    {
        private readonly NumericUpDown _margemPadrao = CriarPercentual(30);
        private readonly NumericUpDown _aumentoMaximo = CriarPercentual(15);
        private readonly NumericUpDown _reducaoMaxima = CriarPercentual(5);
        private readonly DataGridView _marcas = new();
        private readonly DataGridView _categorias = new();
        private readonly DataGridView _fornecedores = new();
        private readonly DataGridView _faixas = new();
        private readonly DataGridView _limites = new();
        private readonly DataGridView _promocoes = new();
        private readonly ComboBox _tipoCalculo = new();
        private readonly ComboBox _arredondamento = new();

        public RegrasNegocio? Resultado { get; private set; }

        public BusinessRulesForm(RegrasNegocio atuais)
        {
            Text = "Regras de negócio de precificação";
            Width = 760;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(680, 480);

            _margemPadrao.Value = atuais.MargemPadrao * 100;
            _aumentoMaximo.Value = atuais.AumentoMaximoPercentual * 100;
            _reducaoMaxima.Value = atuais.ReducaoMaximaPercentual * 100;
            _tipoCalculo.Items.AddRange(new object[] { "Margem", "Markup" });
            _tipoCalculo.SelectedIndex = atuais.TipoCalculo.Equals("markup", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _arredondamento.Items.AddRange(new object[] { "Nenhum", "Final .99", "Arredondar para .05" });
            _arredondamento.SelectedIndex = atuais.Arredondamento switch { "final_99" => 1, "centavos_05" => 2, _ => 0 };

            var tabelaGeral = new TableLayoutPanel { Dock = DockStyle.Top, Height = 145, ColumnCount = 2, RowCount = 5, Padding = new Padding(10) };
            tabelaGeral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            tabelaGeral.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            AdicionarLinha(tabelaGeral, 0, "Margem padrão (%):", _margemPadrao);
            AdicionarLinha(tabelaGeral, 1, "Aumento máximo (%):", _aumentoMaximo);
            AdicionarLinha(tabelaGeral, 2, "Redução máxima (%):", _reducaoMaxima);
            AdicionarLinha(tabelaGeral, 3, "Cálculo:", _tipoCalculo);
            AdicionarLinha(tabelaGeral, 4, "Arredondamento:", _arredondamento);

            ConfigurarTabela(_marcas, "Marca", atuais.MargemPorMarca);
            ConfigurarTabela(_categorias, "Categoria", atuais.MargemPorCategoria);
            ConfigurarTabela(_fornecedores, "Fornecedor", atuais.MargemPorFornecedor);
            ConfigurarFaixas(_faixas, atuais.FaixasCusto);
            ConfigurarLimites(_limites, atuais.PrecoMinimoPorProduto, atuais.PrecoMaximoPorProduto);
            ConfigurarPromocoes(_promocoes, atuais.Promocoes);

            var abas = new TabControl { Dock = DockStyle.Fill };
            var abaMargens = new TabPage("Margens");
            var margens = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(8) };
            margens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            margens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            margens.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            margens.Controls.Add(CriarGrupo("Por marca", _marcas), 0, 0);
            margens.Controls.Add(CriarGrupo("Por categoria", _categorias), 1, 0);
            margens.Controls.Add(CriarGrupo("Por fornecedor", _fornecedores), 2, 0);
            abaMargens.Controls.Add(margens);
            var abaFaixas = new TabPage("Faixas e limites");
            var faixasLimites = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
            faixasLimites.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            faixasLimites.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            faixasLimites.Controls.Add(CriarGrupo("Margem por faixa de custo", _faixas), 0, 0);
            faixasLimites.Controls.Add(CriarGrupo("Preço mínimo / máximo por produto", _limites), 1, 0);
            abaFaixas.Controls.Add(faixasLimites);
            var abaPromocoes = new TabPage("Promoções");
            abaPromocoes.Controls.Add(CriarGrupo("Promoções vigentes", _promocoes));
            abas.TabPages.AddRange(new[] { abaMargens, abaFaixas, abaPromocoes });

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var salvar = new Button { Text = "Salvar regras", Width = 120 };
            salvar.Click += (_, _) => Salvar();
            var cancelar = new Button { Text = "Cancelar", Width = 90, DialogResult = DialogResult.Cancel };
            botoes.Controls.Add(salvar);
            botoes.Controls.Add(cancelar);

            Controls.Add(abas);
            Controls.Add(tabelaGeral);
            Controls.Add(botoes);
            AcceptButton = salvar;
            CancelButton = cancelar;
        }

        private static NumericUpDown CriarPercentual(decimal valor)
        {
            return new NumericUpDown { Minimum = 0, Maximum = 99, DecimalPlaces = 2, Increment = 1, Width = 110, Value = valor };
        }

        private static void AdicionarLinha(TableLayoutPanel tabela, int linha, string texto, Control controle)
        {
            tabela.Controls.Add(new Label { Text = texto, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, linha);
            tabela.Controls.Add(controle, 1, linha);
        }

        private static GroupBox CriarGrupo(string titulo, DataGridView tabela)
        {
            var grupo = new GroupBox { Text = titulo, Dock = DockStyle.Fill, Padding = new Padding(8) };
            grupo.Controls.Add(tabela);
            return grupo;
        }

        private static void ConfigurarTabela(DataGridView tabela, string nomeCampo, Dictionary<string, decimal> valores)
        {
            tabela.Dock = DockStyle.Fill;
            tabela.AllowUserToAddRows = true;
            tabela.AllowUserToDeleteRows = true;
            tabela.AutoGenerateColumns = false;
            tabela.RowHeadersVisible = false;
            tabela.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = nomeCampo, Name = "Nome", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            tabela.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Margem (%)", Name = "Margem", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });

            foreach (var par in valores)
                tabela.Rows.Add(par.Key, par.Value * 100);
        }

        private void Salvar()
        {
            if (!LerTabela(_marcas, "marca", out var marcas) || !LerTabela(_categorias, "categoria", out var categorias))
                return;
            if (!LerTabela(_fornecedores, "fornecedor", out var fornecedores) || !LerFaixas(out var faixas) || !LerLimites(out var minimos, out var maximos) || !LerPromocoes(out var promocoes))
                return;

            Resultado = new RegrasNegocio
            {
                MargemPadrao = _margemPadrao.Value / 100,
                AumentoMaximoPercentual = _aumentoMaximo.Value / 100,
                ReducaoMaximaPercentual = _reducaoMaxima.Value / 100,
                MargemPorMarca = marcas,
                MargemPorCategoria = categorias,
                MargemPorFornecedor = fornecedores,
                FaixasCusto = faixas,
                PrecoMinimoPorProduto = minimos,
                PrecoMaximoPorProduto = maximos,
                TipoCalculo = _tipoCalculo.SelectedIndex == 1 ? "markup" : "margem",
                Arredondamento = _arredondamento.SelectedIndex switch { 1 => "final_99", 2 => "centavos_05", _ => "nenhum" },
                Promocoes = promocoes
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private static bool LerTabela(DataGridView tabela, string tipo, out Dictionary<string, decimal> resultado)
        {
            resultado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow linha in tabela.Rows)
            {
                if (linha.IsNewRow) continue;
                var nome = Convert.ToString(linha.Cells["Nome"].Value)?.Trim();
                var textoMargem = Convert.ToString(linha.Cells["Margem"].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(textoMargem)) continue;
                if (string.IsNullOrWhiteSpace(nome) || !decimal.TryParse(textoMargem, out var margem) || margem < 0 || margem >= 100)
                {
                    MessageBox.Show($"Informe {tipo} e uma margem entre 0 e 99,99%.", "Regra inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                resultado[nome] = margem / 100;
            }
            return true;
        }

        private static void ConfigurarFaixas(DataGridView tabela, IEnumerable<FaixaCusto> valores)
        {
            ConfigurarColunas(tabela, ("Mínimo", 90), ("Máximo", 90), ("Margem (%)", 100));
            foreach (var faixa in valores) tabela.Rows.Add(faixa.Minimo, faixa.Maximo, faixa.Valor * 100);
        }

        private bool LerFaixas(out List<FaixaCusto> resultado)
        {
            resultado = new();
            foreach (DataGridViewRow linha in _faixas.Rows)
            {
                if (linha.IsNewRow) continue;
                if (!decimal.TryParse(Convert.ToString(linha.Cells[0].Value), out var minimo) ||
                    !decimal.TryParse(Convert.ToString(linha.Cells[1].Value), out var maximo) ||
                    !decimal.TryParse(Convert.ToString(linha.Cells[2].Value), out var valor) ||
                    minimo < 0 || maximo < minimo || valor < 0 || valor >= 100)
                {
                    if (linha.Cells[0].Value is null && linha.Cells[1].Value is null) continue;
                    MessageBox.Show("Faixas devem ter mínimo, máximo e margem entre 0 e 99,99%.", "Regra inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                resultado.Add(new FaixaCusto { Minimo = minimo, Maximo = maximo, Valor = valor / 100 });
            }
            return true;
        }

        private static void ConfigurarLimites(DataGridView tabela, Dictionary<string, decimal> minimos, Dictionary<string, decimal> maximos)
        {
            ConfigurarColunas(tabela, ("Código", 100), ("Mínimo", 90), ("Máximo", 90));
            foreach (var codigo in minimos.Keys.Union(maximos.Keys)) tabela.Rows.Add(codigo, minimos.GetValueOrDefault(codigo), maximos.GetValueOrDefault(codigo));
        }

        private bool LerLimites(out Dictionary<string, decimal> minimos, out Dictionary<string, decimal> maximos)
        {
            minimos = new(StringComparer.OrdinalIgnoreCase); maximos = new(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow linha in _limites.Rows)
            {
                if (linha.IsNewRow) continue;
                var codigo = Convert.ToString(linha.Cells[0].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(codigo)) continue;
                if (!decimal.TryParse(Convert.ToString(linha.Cells[1].Value), out var minimo) || !decimal.TryParse(Convert.ToString(linha.Cells[2].Value), out var maximo) || minimo < 0 || maximo < minimo)
                { MessageBox.Show("Informe código e limites válidos.", "Regra inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
                minimos[codigo] = minimo; maximos[codigo] = maximo;
            }
            return true;
        }

        private static void ConfigurarPromocoes(DataGridView tabela, IEnumerable<Promocao> valores)
        {
            ConfigurarColunas(tabela, ("Nome", 130), ("Início", 90), ("Fim", 90), ("Desconto (%)", 90), ("Código", 90), ("Marca", 110), ("Categoria", 110));
            foreach (var p in valores) tabela.Rows.Add(p.Nome, p.Inicio, p.Fim, p.Desconto * 100, p.Codigo, p.Marca, p.Categoria);
        }

        private bool LerPromocoes(out List<Promocao> resultado)
        {
            resultado = new();
            foreach (DataGridViewRow linha in _promocoes.Rows)
            {
                if (linha.IsNewRow) continue;
                var nome = Convert.ToString(linha.Cells[0].Value)?.Trim();
                if (string.IsNullOrWhiteSpace(nome)) continue;
                if (!DateTime.TryParse(Convert.ToString(linha.Cells[1].Value), out var inicio) || !DateTime.TryParse(Convert.ToString(linha.Cells[2].Value), out var fim) || !decimal.TryParse(Convert.ToString(linha.Cells[3].Value), out var desconto) || fim < inicio || desconto < 0 || desconto >= 100)
                { MessageBox.Show("Promoção exige datas válidas e desconto entre 0 e 99,99%.", "Regra inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
                resultado.Add(new Promocao { Nome = nome, Inicio = inicio.ToString("yyyy-MM-dd"), Fim = fim.ToString("yyyy-MM-dd"), Desconto = desconto / 100, Codigo = Convert.ToString(linha.Cells[4].Value)?.Trim() ?? "", Marca = Convert.ToString(linha.Cells[5].Value)?.Trim() ?? "", Categoria = Convert.ToString(linha.Cells[6].Value)?.Trim() ?? "" });
            }
            return true;
        }

        private static void ConfigurarColunas(DataGridView tabela, params (string Nome, int Largura)[] colunas)
        {
            tabela.Dock = DockStyle.Fill; tabela.AllowUserToAddRows = true; tabela.AllowUserToDeleteRows = true; tabela.AutoGenerateColumns = false; tabela.RowHeadersVisible = false;
            foreach (var coluna in colunas) tabela.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = coluna.Nome, Width = coluna.Largura });
        }
    }
}