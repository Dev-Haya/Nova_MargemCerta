using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PrecoSyncApp.Services
{
    /// <summary>
    /// Inicia o processo Python e encerra a execução junto com o app.
    /// Em DEBUG, usa o Python embutido do repositório com uvicorn;
    /// em RELEASE, usa o executável compilado.
    /// </summary>
    public class PythonProcessManager : IDisposable
    {
        private Process _processo;
        private readonly string _caminhoExecutavel;
        private readonly string _raizProjeto;
        private readonly string _caminhoPythonDesenvolvimento;

        public PythonProcessManager(string nomeExecutavel = "servico_precos.exe")
        {
            _caminhoExecutavel = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "python", nomeExecutavel);
            _raizProjeto = LocalizarRaizProjeto();
            _caminhoPythonDesenvolvimento = Path.Combine(_raizProjeto, "engine", "python_embed", "python.exe");
        }

        public bool EstaRodando => _processo != null && !_processo.HasExited;

        public void Iniciar()
        {
            if (EstaRodando)
            {
                return;
            }

            var startInfo = CriarStartInfo();

            _processo = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _processo.Start();
        }

        public async Task<bool> AguardarDisponivelAsync(PythonApiClient cliente = null, int timeoutSegundos = 30)
        {
            var limite = DateTime.UtcNow.AddSeconds(timeoutSegundos);

            while (DateTime.UtcNow < limite)
            {
                if (cliente != null)
                {
                    if (await cliente.TestarConexaoAsync().ConfigureAwait(false))
                    {
                        return true;
                    }
                }
                else
                {
                    try
                    {
                        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                        using var response = await http.GetAsync("http://127.0.0.1:8000/ping").ConfigureAwait(false);
                        if (response.IsSuccessStatusCode)
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // tenta novamente
                    }
                }

                await Task.Delay(500).ConfigureAwait(false);
            }

            return false;
        }

        public void Parar()
        {
            if (_processo == null)
            {
                return;
            }

            try
            {
                if (!_processo.HasExited)
                {
                    _processo.Kill();
                    _processo.WaitForExit(5000);
                }
            }
            catch
            {
                // Ignora falhas ao encerrar.
            }
            finally
            {
                _processo.Dispose();
                _processo = null;
            }
        }

        public void Dispose()
        {
            Parar();
        }

        private ProcessStartInfo CriarStartInfo()
        {
#if DEBUG
            if (File.Exists(_caminhoPythonDesenvolvimento))
            {
                return new ProcessStartInfo
                {
                    FileName = _caminhoPythonDesenvolvimento,
                    Arguments = "-m uvicorn engine.main:app --host 127.0.0.1 --port 8000 --reload",
                    WorkingDirectory = _raizProjeto,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = false,
                    UseShellExecute = false
                };
            }

            return new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-m uvicorn engine.main:app --host 127.0.0.1 --port 8000 --reload",
                WorkingDirectory = _raizProjeto,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                UseShellExecute = false
            };
#else
            if (!File.Exists(_caminhoExecutavel))
            {
                throw new FileNotFoundException("Executável do serviço Python não encontrado.", _caminhoExecutavel);
            }

            return new ProcessStartInfo
            {
                FileName = _caminhoExecutavel,
                WorkingDirectory = Path.GetDirectoryName(_caminhoExecutavel),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            };
#endif
        }

        private static string LocalizarRaizProjeto()
        {
            var atual = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (atual != null)
            {
                var candidato = Path.Combine(atual.FullName, "engine", "main.py");
                if (File.Exists(candidato))
                {
                    return atual.FullName;
                }

                atual = atual.Parent;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    public sealed class PythonApiClient
    {
        private readonly HttpClient _httpClient;

        public PythonApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:8000")
            };
        }

        public async Task<bool> TestarConexaoAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync("/ping").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
