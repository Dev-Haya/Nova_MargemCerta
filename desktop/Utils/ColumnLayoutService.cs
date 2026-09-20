using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EstoqueApp.Utils
{
    public sealed class ColumnLayoutService
    {
        private readonly string _arquivo;
        private readonly Dictionary<string, MapeamentoColunas> _layouts;

        public ColumnLayoutService(string? arquivo = null)
        {
            _arquivo = arquivo ?? Path.Combine(AppContext.BaseDirectory, "layouts-colunas.json");
            _layouts = Carregar();
        }

        public bool TentarObter(IReadOnlyList<string> cabecalhos, out MapeamentoColunas? mapa) =>
            _layouts.TryGetValue(ObterChave(cabecalhos), out mapa);

        public void Salvar(IReadOnlyList<string> cabecalhos, MapeamentoColunas mapa)
        {
            _layouts[ObterChave(cabecalhos)] = mapa;
            File.WriteAllText(_arquivo, JsonSerializer.Serialize(_layouts, new JsonSerializerOptions { WriteIndented = true }));
        }

        private Dictionary<string, MapeamentoColunas> Carregar()
        {
            try
            {
                if (File.Exists(_arquivo))
                    return JsonSerializer.Deserialize<Dictionary<string, MapeamentoColunas>>(File.ReadAllText(_arquivo)) ?? new();
            }
            catch (JsonException) { }
            return new();
        }

        private static string ObterChave(IEnumerable<string> cabecalhos)
        {
            var normalizado = "v3|" + string.Join("|", cabecalhos.Select(c => c.Trim().ToUpperInvariant()));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado)));
        }
    }
}