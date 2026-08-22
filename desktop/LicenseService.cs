using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PrecoSyncApp.Services
{
    public enum StatusLicenca
    {
        Ativa,
        Vencida,
        NaoEncontrada,
        ErroDeComunicacao
    }

    /// <summary>
    /// Trava comercial: consulta leve ao Firebase Realtime Database (via REST)
    /// para checar se a assinatura do CNPJ do cliente está em dia.
    /// </summary>
    public class LicenseService
    {
        private readonly HttpClient _http;
        private readonly string _firebaseBaseUrl;

        public LicenseService(string firebaseBaseUrl)
        {
            _firebaseBaseUrl = firebaseBaseUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        /// <summary>
        /// Consulta o nó /clientes/{cnpj}.json e verifica se o cliente está ativo e dentro da validade.
        /// </summary>
        public async Task<StatusLicenca> VerificarAssinaturaAsync(string cnpj)
        {
            try
            {
                // Remove caracteres especiais para manter apenas números (ex: "12345678000199")
                string cnpjLimpo = Regex.Replace(cnpj ?? "", @"[^\d]", "");

                if (string.IsNullOrEmpty(cnpjLimpo))
                    return StatusLicenca.NaoEncontrada;

                string url = $"{_firebaseBaseUrl}/clientes/{cnpjLimpo}.json";
                var registro = await _http.GetFromJsonAsync<RegistroCliente>(url);

                if (registro == null)
                    return StatusLicenca.NaoEncontrada;

                // FIX: Checa se o cliente foi desativado manualmente no Firebase
                if (!registro.Ativo)
                    return StatusLicenca.Vencida;

                // Checa se a data de validade é igual ou superior a hoje (UTC)
                if (registro.ValidoAte.Date >= DateTime.UtcNow.Date)
                    return StatusLicenca.Ativa;

                return StatusLicenca.Vencida;
            }
            catch
            {
                return StatusLicenca.ErroDeComunicacao;
            }
        }

        private class RegistroCliente
        {
            [JsonPropertyName("valido_ate")]
            public DateTime ValidoAte { get; set; }

            [JsonPropertyName("ativo")]
            public bool Ativo { get; set; }
        }
    }
}
