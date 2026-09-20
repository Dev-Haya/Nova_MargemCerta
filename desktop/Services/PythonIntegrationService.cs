using System.Net.Http.Headers;
using System.Net.Http.Json;
using EstoqueApp.Models;

namespace EstoqueApp.Services
{
    /// <summary>
    /// Camada de comunicação HTTP com o programa Python. O Python roda
    /// como processo separado (em background) expondo a API local do engine
    /// e trocando dados em JSON com o C#.
    /// </summary>
    public class PythonIntegrationService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _token;

        public PythonIntegrationService(string? baseUrl = null, string? token = null)
        {
            _baseUrl = (baseUrl ?? Environment.GetEnvironmentVariable("MOTOR_PYTHON_URL") ?? "http://127.0.0.1:8000").TrimEnd('/');
            _token = token ?? Environment.GetEnvironmentVariable("APP_SECRET_TOKEN") ?? "teste";
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        private HttpRequestMessage CriarRequisicao(HttpMethod metodo, string endpoint)
        {
            var requisicao = new HttpRequestMessage(metodo, $"{_baseUrl}/{endpoint.TrimStart('/')}");
            requisicao.Headers.Add("X-App-Token", _token);
            return requisicao;
        }

        /// <summary>Envia os itens atuais e os dados do fornecedor para o motor de precificação.</summary>
        public async Task<RespostaProcessamento?> ProcessarPrecosAsync(RequisicaoProcessamento payload)
        {
            using var requisicao = CriarRequisicao(HttpMethod.Post, "process-prices");
            requisicao.Content = JsonContent.Create(payload);

            using var resposta = await _http.SendAsync(requisicao);
            if (!resposta.IsSuccessStatusCode)
                return null;

            return await resposta.Content.ReadFromJsonAsync<RespostaProcessamento>();
        }

        /// <summary>Faz o health check público da API Python.</summary>
        public async Task<bool> VerificarConexaoAsync()
        {
            try
            {
                using var requisicao = CriarRequisicao(HttpMethod.Get, "ping");
                using var resposta = await _http.SendAsync(requisicao);
                return resposta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Controla o ciclo de vida do processo Python (.exe) rodando em
    /// background, conforme o bloco "Controle de API" do diagrama.
    /// </summary>
    public sealed class ProcessoPythonController : IDisposable
    {
        private System.Diagnostics.Process? _processo;

        public async Task<bool> IniciarAsync(PythonIntegrationService api, string? caminhoExe = null, string? argumentos = null)
        {
            if (await api.VerificarConexaoAsync())
                return true;

            if (_processo is { HasExited: false })
                return await AguardarApiAsync(api);

            caminhoExe ??= Environment.GetEnvironmentVariable("MOTOR_PYTHON_EXE") ?? "python";
            argumentos ??= Environment.GetEnvironmentVariable("MOTOR_PYTHON_ARGS") ?? "-m uvicorn engine.main:app --host 127.0.0.1 --port 8000";

            _processo = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = caminhoExe,
                    Arguments = argumentos,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Environment.GetEnvironmentVariable("MOTOR_PYTHON_WORKDIR") ?? Directory.GetCurrentDirectory(),
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
            if (!_processo.Start())
                throw new InvalidOperationException("Não foi possível iniciar o motor Python.");

            if (!await AguardarApiAsync(api))
            {
                Parar();
                throw new TimeoutException("O motor Python não respondeu ao health check.");
            }
            return true;
        }

        private static async Task<bool> AguardarApiAsync(PythonIntegrationService api)
        {
            for (var tentativa = 0; tentativa < 30; tentativa++)
            {
                if (await api.VerificarConexaoAsync()) return true;
                await Task.Delay(250);
            }
            return false;
        }

        public void Parar()
        {
            if (_processo is { HasExited: false })
            {
                _processo.Kill(entireProcessTree: true);
                _processo.WaitForExit(3000);
                _processo.Dispose();
                _processo = null;
            }
        }

        public void Dispose() => Parar();
    }
}
