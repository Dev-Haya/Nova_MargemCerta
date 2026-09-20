using System.Net.Http.Json;

namespace EstoqueApp.Services
{
    /// <summary>Estrutura esperada do documento de licença no Firebase.</summary>
    public class LicencaCliente
    {
        public bool Ativo { get; set; }
        public string DataVencimento { get; set; } = string.Empty;
    }

    /// <summary>
    /// Trava comercial: valida no Firebase (via Firestore REST API) se a
    /// mensalidade do cliente está em dia antes de liberar o uso do app.
    /// </summary>
    public class LicenseService
    {
        private readonly HttpClient _http = new();
        private readonly string _firebaseProjectId;
        private readonly string _colecao;

        public LicenseService(string firebaseProjectId, string colecao = "licencas")
        {
            _firebaseProjectId = firebaseProjectId;
            _colecao = colecao;
        }

        /// <summary>
        /// Consulta o documento da licença (identificado pelo ID do cliente)
        /// no Firestore e retorna true apenas se estiver ativo e não vencido.
        /// </summary>
        public async Task<bool> ValidarMensalidadeAsync(string clienteId)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_firebaseProjectId}" +
                      $"/databases/(default)/documents/{_colecao}/{clienteId}";

            try
            {
                var resposta = await _http.GetAsync(url);
                if (!resposta.IsSuccessStatusCode) return false;

                var documento = await resposta.Content.ReadFromJsonAsync<FirestoreDocumento>();
                var campos = documento?.Fields;
                if (campos is null) return false;

                bool ativo = campos.TryGetValue("ativo", out var ativoField) && ativoField.BooleanValue;
                bool venceu = campos.TryGetValue("dataVencimento", out var vencField)
                              && DateTime.TryParse(vencField.StringValue, out var vencimento)
                              && vencimento < DateTime.UtcNow;

                return ativo && !venceu;
            }
            catch
            {
                // Sem internet ou erro de rede: por segurança, bloqueia o uso.
                return false;
            }
        }

        // Classes auxiliares só para desserializar o formato específico do Firestore REST.
        private class FirestoreDocumento
        {
            public Dictionary<string, FirestoreField>? Fields { get; set; }
        }

        private class FirestoreField
        {
            public bool BooleanValue { get; set; }
            public string? StringValue { get; set; }
        }
    }
}
