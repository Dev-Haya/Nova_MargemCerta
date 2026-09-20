using ClosedXML.Excel;
using EstoqueApp.Models;
using System.Globalization;
using System.Text;

namespace EstoqueApp.Utils
{
    /// <summary>Mapa que diz em qual coluna (cabeçalho) está cada campo do item.</summary>
    public class MapeamentoColunas
    {
        public string ColunaCodigo { get; set; } = string.Empty;
        public string ColunaDescricao { get; set; } = string.Empty;
        public string ColunaPrecoUnitario { get; set; } = string.Empty;
        // Mantido para ler layouts salvos antes da nomeação explícita do preço unitário.
        public string ColunaPreco { get; set; } = string.Empty;
        public string ColunaQuantidade { get; set; } = string.Empty;
        public string? ColunaMarca { get; set; }
        public string? ColunaCategoria { get; set; }
        public string? ColunaFornecedor { get; set; }
    }

    /// <summary>
    /// Lê arquivos .xlsx ou .csv e permite escolher dinamicamente quais
    /// cabeçalhos correspondem a Código/Descrição/Preço unitário/Quantidade
    /// (implementa o "Mapeador de Colunas" do diagrama).
    /// </summary>
    public static class PlanilhaImporter
    {
        public static IReadOnlyList<string> ValidarItens(IEnumerable<ItemEstoque> itens)
        {
            var erros = new List<string>();
            var lista = itens.ToList();
            var duplicados = lista.Where(item => !string.IsNullOrWhiteSpace(item.Codigo))
                .GroupBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
                .Where(grupo => grupo.Count() > 1)
                .Select(grupo => grupo.Key);

            foreach (var duplicado in duplicados)
                erros.Add($"Código duplicado: {duplicado}");
            erros.AddRange(lista.Where(item => string.IsNullOrWhiteSpace(item.Codigo)).Select((_, indice) => $"Linha sem código: {indice + 2}"));
            erros.AddRange(lista.Where(item => string.IsNullOrWhiteSpace(item.Descricao)).Select(item => $"Produto {item.Codigo} sem descrição"));
            erros.AddRange(lista.Where(item => item.PrecoCusto < 0).Select(item => $"Produto {item.Codigo} com custo negativo"));
            erros.AddRange(lista.Where(item => item.Quantidade < 0).Select(item => $"Produto {item.Codigo} com estoque negativo"));
            return erros;
        }

        /// <summary>Lê apenas a primeira linha (cabeçalhos) para popular o mapeador na tela.</summary>
        public static List<string> LerCabecalhos(string caminhoArquivo)
        {
            if (Path.GetExtension(caminhoArquivo).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var primeiraLinha = File.ReadLines(caminhoArquivo).FirstOrDefault() ?? "";
                return LerLinhaCsv(primeiraLinha).Select(h => h.Trim()).ToList();
            }

            using var workbook = new XLWorkbook(caminhoArquivo);
            var planilha = workbook.Worksheet(1);
            var primeiraLinhaExcel = planilha.Row(1);
            return primeiraLinhaExcel.CellsUsed().Select(c => c.GetString().Trim()).ToList();
        }

        /// <summary>Lê todas as linhas de dados aplicando o mapeamento escolhido pelo usuário.</summary>
        public static List<ItemEstoque> Importar(string caminhoArquivo, MapeamentoColunas mapa)
        {
            return Path.GetExtension(caminhoArquivo).Equals(".csv", StringComparison.OrdinalIgnoreCase)
                ? ImportarCsv(caminhoArquivo, mapa)
                : ImportarExcel(caminhoArquivo, mapa);
        }

        private static List<ItemEstoque> ImportarCsv(string caminho, MapeamentoColunas mapa)
        {
            var linhas = File.ReadAllLines(caminho);
            if (linhas.Length == 0) return new();

            var cabecalhos = LerLinhaCsv(linhas[0]).Select(h => h.Trim()).ToList();

            int idxCodigo = cabecalhos.IndexOf(mapa.ColunaCodigo);
            int idxDescricao = cabecalhos.IndexOf(mapa.ColunaDescricao);
            int idxPreco = cabecalhos.IndexOf(ObterColunaPreco(mapa));
            int idxQtd = cabecalhos.IndexOf(mapa.ColunaQuantidade);
            int idxMarca = mapa.ColunaMarca is null ? -1 : cabecalhos.IndexOf(mapa.ColunaMarca);
            int idxCategoria = mapa.ColunaCategoria is null ? -1 : cabecalhos.IndexOf(mapa.ColunaCategoria);
            int idxFornecedor = mapa.ColunaFornecedor is null ? -1 : cabecalhos.IndexOf(mapa.ColunaFornecedor);
            ValidarIndicesObrigatorios(idxCodigo, idxDescricao, idxPreco, idxQtd);

            var itens = new List<ItemEstoque>();
            for (int i = 1; i < linhas.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(linhas[i])) continue;
                var campos = LerLinhaCsv(linhas[i]);
                if (Math.Max(idxCodigo, Math.Max(idxDescricao, Math.Max(idxPreco, idxQtd))) >= campos.Count)
                    continue;

                var codigo = campos[idxCodigo].Trim();
                var descricao = campos[idxDescricao].Trim();
                if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(descricao) ||
                    !TentarLerDecimal(campos[idxPreco], out var preco) ||
                    !TentarLerInteiro(campos[idxQtd], out var quantidade))
                    continue;

                itens.Add(new ItemEstoque
                {
                    Codigo = codigo,
                    Descricao = descricao,
                    PrecoCusto = preco,
                    Quantidade = quantidade,
                    Marca = idxMarca >= 0 && idxMarca < campos.Count ? campos[idxMarca].Trim() : "Sem Marca"
                    ,Categoria = idxCategoria >= 0 && idxCategoria < campos.Count ? campos[idxCategoria].Trim() : "Geral"
                    ,Fornecedor = idxFornecedor >= 0 && idxFornecedor < campos.Count ? campos[idxFornecedor].Trim() : string.Empty
                });
            }
            return itens;
        }

        private static List<ItemEstoque> ImportarExcel(string caminho, MapeamentoColunas mapa)
        {
            using var workbook = new XLWorkbook(caminho);
            var planilha = workbook.Worksheet(1);
            var cabecalhos = planilha.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();

            int colCodigo = cabecalhos.IndexOf(mapa.ColunaCodigo) + 1;
            int colDescricao = cabecalhos.IndexOf(mapa.ColunaDescricao) + 1;
            int colPreco = cabecalhos.IndexOf(ObterColunaPreco(mapa)) + 1;
            int colQtd = cabecalhos.IndexOf(mapa.ColunaQuantidade) + 1;
            int colMarca = mapa.ColunaMarca is null ? 0 : cabecalhos.IndexOf(mapa.ColunaMarca) + 1;
            int colCategoria = mapa.ColunaCategoria is null ? 0 : cabecalhos.IndexOf(mapa.ColunaCategoria) + 1;
            int colFornecedor = mapa.ColunaFornecedor is null ? 0 : cabecalhos.IndexOf(mapa.ColunaFornecedor) + 1;
            ValidarIndicesObrigatorios(colCodigo, colDescricao, colPreco, colQtd);

            var itens = new List<ItemEstoque>();
            var ultimaLinha = planilha.LastRowUsed()!.RowNumber();

            for (int linha = 2; linha <= ultimaLinha; linha++)
            {
                var codigo = planilha.Cell(linha, colCodigo).GetString().Trim();
                if (string.IsNullOrWhiteSpace(codigo)) continue;
                var descricao = planilha.Cell(linha, colDescricao).GetString().Trim();
                if (string.IsNullOrWhiteSpace(descricao) ||
                    !TentarLerDecimalExcel(planilha.Cell(linha, colPreco), out var preco) ||
                    !TentarLerInteiroExcel(planilha.Cell(linha, colQtd), out var quantidade))
                    continue;

                itens.Add(new ItemEstoque
                {
                    Codigo = codigo,
                    Descricao = descricao,
                    PrecoCusto = preco,
                    Quantidade = quantidade,
                    Marca = colMarca > 0 ? planilha.Cell(linha, colMarca).GetString().Trim() : "Sem Marca"
                    ,Categoria = colCategoria > 0 ? planilha.Cell(linha, colCategoria).GetString().Trim() : "Geral"
                    ,Fornecedor = colFornecedor > 0 ? planilha.Cell(linha, colFornecedor).GetString().Trim() : string.Empty
                });
            }
            return itens;
        }

        private static string ObterColunaPreco(MapeamentoColunas mapa) =>
            string.IsNullOrWhiteSpace(mapa.ColunaPrecoUnitario) ? mapa.ColunaPreco : mapa.ColunaPrecoUnitario;

        private static void ValidarIndicesObrigatorios(int codigo, int descricao, int preco, int quantidade)
        {
            if (codigo < 0 || descricao < 0 || preco < 0 || quantidade < 0)
                throw new InvalidDataException("O mapeamento precisa conter Código, Descrição, Preço unitário e Quantidade.");
        }

        private static List<string> LerLinhaCsv(string linha)
        {
            var separador = DetectarSeparador(linha);
            var campos = new List<string>();
            var campo = new StringBuilder();
            var entreAspas = false;

            for (var indice = 0; indice < linha.Length; indice++)
            {
                var caractere = linha[indice];
                if (caractere == '"')
                {
                    if (entreAspas && indice + 1 < linha.Length && linha[indice + 1] == '"')
                    {
                        campo.Append('"');
                        indice++;
                    }
                    else
                    {
                        entreAspas = !entreAspas;
                    }
                }
                else if (caractere == separador && !entreAspas)
                {
                    campos.Add(campo.ToString());
                    campo.Clear();
                }
                else
                {
                    campo.Append(caractere);
                }
            }

            campos.Add(campo.ToString());
            return campos;
        }

        private static char DetectarSeparador(string linha)
        {
            var pontoEVirgula = linha.Count(caractere => caractere == ';');
            var virgulas = linha.Count(caractere => caractere == ',');
            return pontoEVirgula > virgulas ? ';' : ',';
        }

        private static bool TentarLerDecimal(string texto, out decimal valor)
        {
            var normalizado = texto.Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("$", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty);
            var ultimaVirgula = normalizado.LastIndexOf(',');
            var ultimoPonto = normalizado.LastIndexOf('.');

            if (ultimaVirgula >= 0 && ultimoPonto >= 0)
            {
                var separadorDecimal = ultimaVirgula > ultimoPonto ? ',' : '.';
                var separadorMilhar = separadorDecimal == ',' ? '.' : ',';
                normalizado = normalizado.Replace(separadorMilhar.ToString(), string.Empty)
                    .Replace(separadorDecimal.ToString(), ".");
            }
            else if (ultimaVirgula >= 0)
            {
                normalizado = normalizado.Replace(',', '.');
            }

            return decimal.TryParse(normalizado, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out valor);
        }

        private static bool TentarLerInteiro(string texto, out int valor)
        {
            if (int.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out valor))
                return true;
            if (TentarLerDecimal(texto, out var decimalValor) && decimal.Truncate(decimalValor) == decimalValor &&
                decimalValor <= int.MaxValue && decimalValor >= int.MinValue)
            {
                valor = (int)decimalValor;
                return true;
            }
            valor = 0;
            return false;
        }

        private static decimal LerDecimalExcel(IXLCell celula)
        {
            try
            {
                return celula.GetValue<decimal>();
            }
            catch (FormatException)
            {
                return TentarLerDecimal(celula.GetString(), out var valor) ? valor : 0;
            }
        }

        private static bool TentarLerDecimalExcel(IXLCell celula, out decimal valor)
        {
            try
            {
                valor = celula.GetValue<decimal>();
                return true;
            }
            catch (FormatException)
            {
                return TentarLerDecimal(celula.GetString(), out valor);
            }
            catch (InvalidCastException)
            {
                valor = 0;
                return false;
            }
            catch (InvalidOperationException)
            {
                valor = 0;
                return false;
            }
        }

        private static bool TentarLerInteiroExcel(IXLCell celula, out int valor)
        {
            try
            {
                valor = (int)celula.GetValue<double>();
                return true;
            }
            catch (FormatException)
            {
                return TentarLerInteiro(celula.GetString(), out valor);
            }
            catch (InvalidCastException)
            {
                valor = 0;
                return false;
            }
            catch (InvalidOperationException)
            {
                valor = 0;
                return false;
            }
        }

        private static int LerInteiroExcel(IXLCell celula)
        {
            try
            {
                return (int)celula.GetValue<double>();
            }
            catch (FormatException)
            {
                return TentarLerInteiro(celula.GetString(), out var valor) ? valor : 0;
            }
        }
    }
}
