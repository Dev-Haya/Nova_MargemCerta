using EstoqueApp.Data;

namespace EstoqueApp.Forms
{
    public sealed class BackupForm : Form
    {
        private readonly DatabaseService _db;
        private readonly ListBox _lista = new();
        private readonly Label _status = new();
        private IReadOnlyList<string> _backups = Array.Empty<string>();

        public bool Restaurado { get; private set; }

        public BackupForm(DatabaseService db)
        {
            _db = db;
            Text = "Histórico e restauração";
            Width = 620;
            Height = 420;
            StartPosition = FormStartPosition.CenterParent;

            _lista.Dock = DockStyle.Fill;
            _lista.DisplayMember = "Nome";
            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8) };
            var criar = new Button { Text = "Criar backup" };
            criar.Click += (_, _) => CriarBackup();
            var restaurar = new Button { Text = "Restaurar selecionado", Width = 145 };
            restaurar.Click += (_, _) => Restaurar();
            var fechar = new Button { Text = "Fechar", DialogResult = DialogResult.Cancel };
            botoes.Controls.Add(criar);
            botoes.Controls.Add(restaurar);
            botoes.Controls.Add(fechar);
            _status.AutoSize = true;
            _status.Padding = new Padding(12, 8, 0, 0);
            botoes.Controls.Add(_status);
            Controls.Add(_lista);
            Controls.Add(botoes);
            CancelButton = fechar;
            Load += (_, _) => Carregar();
        }

        private void Carregar()
        {
            _backups = _db.ListarBackups();
            _lista.Items.Clear();
            foreach (var backup in _backups)
                _lista.Items.Add(Path.GetFileName(backup));
            _status.Text = $"{_backups.Count} backup(s).";
        }

        private void CriarBackup()
        {
            try
            {
                _db.CriarBackup();
                Carregar();
                _status.Text = "Backup criado.";
            }
            catch (Exception ex) { MostrarErro(ex); }
        }

        private void Restaurar()
        {
            if (_lista.SelectedIndex < 0)
            {
                MessageBox.Show("Selecione um backup.", "Restauração", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmar = MessageBox.Show(
                "O banco atual será substituído. Um backup do estado atual será criado antes. Continuar?",
                "Confirmar restauração", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmar != DialogResult.Yes) return;

            try
            {
                _db.RestaurarBackup(_backups[_lista.SelectedIndex]);
                Restaurado = true;
                MessageBox.Show("Backup restaurado. A tela principal será recarregada.", "Restauração", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex) { MostrarErro(ex); }
        }

        private static void MostrarErro(Exception ex) => MessageBox.Show(ex.Message, "Restauração", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}