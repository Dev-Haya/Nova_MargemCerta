using EstoqueApp.Forms;
using EstoqueApp.Services;

namespace EstoqueApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            using var instancia = new Mutex(true, "Local\\EstoqueApp_Unica", out var primeiraInstancia);
            if (!primeiraInstancia)
            {
                MessageBox.Show("O EstoqueApp já está em execução.", "Aplicativo em execução", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var ambiente = Environment.GetEnvironmentVariable("APP_ENVIRONMENT") ?? "Development";
            var firebaseProjectId = Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID");
            var clienteId = Environment.GetEnvironmentVariable("FIREBASE_CLIENT_ID");
            var configuracaoFirebaseCompleta = !string.IsNullOrWhiteSpace(firebaseProjectId) &&
                                                !string.IsNullOrWhiteSpace(clienteId);

            var licencaValida = ambiente.Equals("Development", StringComparison.OrdinalIgnoreCase) &&
                                !configuracaoFirebaseCompleta ||
                                configuracaoFirebaseCompleta &&
                                new LicenseService(firebaseProjectId!)
                                    .ValidarMensalidadeAsync(clienteId!)
                                    .GetAwaiter()
                                    .GetResult();

            if (!licencaValida)
            {
                MessageBox.Show(
                    configuracaoFirebaseCompleta
                        ? "Licença expirada ou inválida. Entre em contato com o suporte."
                        : "Configure FIREBASE_PROJECT_ID e FIREBASE_CLIENT_ID antes de executar em produção.",
                    "Acesso Bloqueado", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
