using Microsoft.Data.Sqlite;
using EstoqueApp.Models;
using System.Security.Cryptography;
using System.Text;

namespace EstoqueApp.Data
{
    /// <summary>
    /// Responsável por toda a comunicação com o banco SQLite local:
    /// criação da tabela de itens, carga inicial, leitura e o
    /// atualização em lote pós-aprovação e manutenção administrativa.
    /// </summary>
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;
        private readonly string _mutexName;

        public DatabaseService(string dbFileName = "estoque.db")
        {
            var path = Path.Combine(AppContext.BaseDirectory, dbFileName);
            _databasePath = path;
            _connectionString = $"Data Source={path}";
            var identificador = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path))));
            _mutexName = $"Local\\EstoqueApp_Db_{identificador}";
            InicializarBanco();
        }

        private void InicializarBanco()
        {
            using var bloqueio = AdquirirBloqueio();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Itens (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Codigo TEXT NOT NULL UNIQUE,
                    Descricao TEXT NOT NULL,
                    Marca TEXT NOT NULL DEFAULT 'Sem Marca',
                    Categoria TEXT NOT NULL DEFAULT 'Geral',
                    Fornecedor TEXT NOT NULL DEFAULT '',
                    PrecoCusto REAL NOT NULL DEFAULT 0,
                    PrecoVendaAtual REAL NOT NULL DEFAULT 0,
                    PrecoVendaSugerido REAL NOT NULL DEFAULT 0,
                    PrecoAntigo REAL NOT NULL DEFAULT 0,
                    PrecoNovo REAL NOT NULL DEFAULT 0,
                    Quantidade INTEGER NOT NULL DEFAULT 0
                );
                """;
            cmd.ExecuteNonQuery();

            var adicionouCusto = AdicionarColuna(conn, "PrecoCusto REAL NOT NULL DEFAULT 0");
            var adicionouVendaAtual = AdicionarColuna(conn, "PrecoVendaAtual REAL NOT NULL DEFAULT 0");
            var adicionouVendaSugerida = AdicionarColuna(conn, "PrecoVendaSugerido REAL NOT NULL DEFAULT 0");
            AdicionarColuna(conn, "Marca TEXT NOT NULL DEFAULT 'Sem Marca'");
            AdicionarColuna(conn, "Categoria TEXT NOT NULL DEFAULT 'Geral'");
            AdicionarColuna(conn, "Fornecedor TEXT NOT NULL DEFAULT ''");

            if (adicionouCusto || adicionouVendaAtual || adicionouVendaSugerida)
            {
                using var migracao = conn.CreateCommand();
                migracao.CommandText = """
                    UPDATE Itens SET
                        PrecoCusto = CASE WHEN $custo THEN PrecoNovo ELSE PrecoCusto END,
                        PrecoVendaAtual = CASE WHEN $atual THEN PrecoAntigo ELSE PrecoVendaAtual END,
                        PrecoVendaSugerido = CASE WHEN $sugerida THEN PrecoNovo ELSE PrecoVendaSugerido END;
                    """;
                migracao.Parameters.AddWithValue("$custo", adicionouCusto ? 1 : 0);
                migracao.Parameters.AddWithValue("$atual", adicionouVendaAtual ? 1 : 0);
                migracao.Parameters.AddWithValue("$sugerida", adicionouVendaSugerida ? 1 : 0);
                migracao.ExecuteNonQuery();
            }

            using var auditoria = conn.CreateCommand();
            auditoria.CommandText = """
                CREATE TABLE IF NOT EXISTS AuditoriaItens (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ItemId INTEGER NOT NULL,
                    Usuario TEXT NOT NULL,
                    Campo TEXT NOT NULL,
                    ValorAnterior TEXT,
                    ValorNovo TEXT,
                    AlteradoEm TEXT NOT NULL DEFAULT (datetime('now'))
                );
                """;
            auditoria.ExecuteNonQuery();
        }

        private static bool AdicionarColuna(SqliteConnection conn, string definicao)
        {
            try
            {
                using var comando = conn.CreateCommand();
                comando.CommandText = $"ALTER TABLE Itens ADD COLUMN {definicao};";
                comando.ExecuteNonQuery();
                return true;
            }
            catch (SqliteException)
            {
                return false;
            }
        }

        /// <summary>Retorna todos os itens cadastrados.</summary>
        public List<ItemEstoque> ObterTodos()
        {
            using var bloqueio = AdquirirBloqueio();
            var lista = new List<ItemEstoque>();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Codigo, Descricao, Marca, Categoria, Fornecedor, PrecoCusto, PrecoVendaAtual, PrecoVendaSugerido, Quantidade FROM Itens";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new ItemEstoque
                {
                    Id = reader.GetInt32(0),
                    Codigo = reader.GetString(1),
                    Descricao = reader.GetString(2),
                    Marca = reader.GetString(3),
                    Categoria = reader.GetString(4),
                    Fornecedor = reader.GetString(5),
                    PrecoCusto = (decimal)reader.GetDouble(6),
                    PrecoVendaAtual = (decimal)reader.GetDouble(7),
                    PrecoVendaSugerido = (decimal)reader.GetDouble(8),
                    Quantidade = reader.GetInt32(9)
                });
            }
            return lista;
        }

        /// <summary>
        /// Carga inicial: insere itens novos ou, se o código já existir,
        /// atualiza o custo importado sem alterar a venda atual aprovada.
        /// </summary>
        public void CarregarItensImportados(List<ItemEstoque> itensImportados)
        {
            using var bloqueio = AdquirirBloqueio();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var transacao = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            foreach (var item in itensImportados)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Itens (Codigo, Descricao, Marca, Categoria, Fornecedor, PrecoCusto, PrecoVendaAtual, PrecoVendaSugerido, PrecoAntigo, PrecoNovo, Quantidade)
                    VALUES ($codigo, $descricao, $marca, $categoria, $fornecedor, $precoCusto, 0, 0, 0, $precoCusto, $quantidade)
                    ON CONFLICT(Codigo) DO UPDATE SET
                        PrecoCusto = $precoCusto,
                        Descricao = $descricao,
                        Marca = $marca,
                        Categoria = $categoria,
                        Fornecedor = $fornecedor,
                        Quantidade = $quantidade;
                    """;
                cmd.Parameters.AddWithValue("$codigo", item.Codigo);
                cmd.Parameters.AddWithValue("$descricao", item.Descricao);
                cmd.Parameters.AddWithValue("$marca", item.Marca);
                cmd.Parameters.AddWithValue("$categoria", item.Categoria);
                cmd.Parameters.AddWithValue("$fornecedor", item.Fornecedor);
                cmd.Parameters.AddWithValue("$precoCusto", (double)item.PrecoCusto);
                cmd.Parameters.AddWithValue("$quantidade", item.Quantidade);
                cmd.ExecuteNonQuery();
            }

            transacao.Commit();
        }

        /// <summary>
        /// Salvamento Final: após aprovação na tela de comparação, grava
        /// a venda sugerida como a nova venda atual após aprovação.
        /// </summary>
        public void EfetivarAtualizacaoEmLote(List<ItemEstoque> itensAprovados)
        {
            using var bloqueio = AdquirirBloqueio();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var transacao = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            foreach (var item in itensAprovados)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    UPDATE Itens
                    SET PrecoVendaAtual = $precoVendaSugerido,
                        PrecoVendaSugerido = $precoVendaSugerido,
                        PrecoAntigo = $precoVendaSugerido,
                        PrecoNovo = $precoVendaSugerido,
                        Quantidade = $quantidade
                    WHERE Id = $id;
                    """;
                cmd.Parameters.AddWithValue("$precoVendaSugerido", (double)item.PrecoVendaSugerido);
                cmd.Parameters.AddWithValue("$quantidade", item.Quantidade);
                cmd.Parameters.AddWithValue("$id", item.Id);
                cmd.ExecuteNonQuery();
            }

            transacao.Commit();
        }

        public string CriarBackup()
        {
            using var bloqueio = AdquirirBloqueio();
            var pasta = Path.Combine(Path.GetDirectoryName(_databasePath)!, "backups");
            Directory.CreateDirectory(pasta);
            var destino = Path.Combine(pasta, $"estoque-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db");
            File.Copy(_databasePath, destino, overwrite: false);
            return destino;
        }

        public IReadOnlyList<string> ListarBackups()
        {
            using var bloqueio = AdquirirBloqueio();
            var pasta = Path.Combine(Path.GetDirectoryName(_databasePath)!, "backups");
            if (!Directory.Exists(pasta)) return Array.Empty<string>();
            return Directory.GetFiles(pasta, "estoque-*.db")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }

        public string RestaurarBackup(string caminhoBackup)
        {
            using var bloqueio = AdquirirBloqueio();
            if (!File.Exists(caminhoBackup))
                throw new FileNotFoundException("Backup não encontrado.", caminhoBackup);

            var backupAtual = CriarBackup();
            File.Copy(caminhoBackup, _databasePath, overwrite: true);
            InicializarBanco();
            return backupAtual;
        }

        public void SalvarManutencao(List<ItemEstoque> itens, IReadOnlyDictionary<int, ItemEstoque> originais, string usuario)
        {
            using var bloqueio = AdquirirBloqueio();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var transacao = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            foreach (var item in itens)
            {
                if (string.IsNullOrWhiteSpace(item.Codigo) || string.IsNullOrWhiteSpace(item.Descricao))
                    throw new InvalidOperationException("Código e descrição são obrigatórios.");
                if (item.PrecoCusto < 0 || item.PrecoVendaAtual < 0 || item.PrecoVendaSugerido < 0 || item.Quantidade < 0)
                    throw new InvalidOperationException($"Valores inválidos no produto {item.Codigo}.");
                if (!originais.TryGetValue(item.Id, out var original)) continue;

                using var atualizar = conn.CreateCommand();
                atualizar.CommandText = """
                    UPDATE Itens SET Codigo = $codigo, Descricao = $descricao, Marca = $marca,
                        Categoria = $categoria, Fornecedor = $fornecedor, PrecoCusto = $precoCusto,
                        PrecoVendaAtual = $precoVendaAtual,
                        PrecoVendaSugerido = $precoVendaSugerido,
                        PrecoAntigo = $precoVendaAtual, PrecoNovo = $precoVendaSugerido,
                        Quantidade = $quantidade
                    WHERE Id = $id;
                    """;
                atualizar.Parameters.AddWithValue("$codigo", item.Codigo.Trim());
                atualizar.Parameters.AddWithValue("$descricao", item.Descricao.Trim());
                atualizar.Parameters.AddWithValue("$marca", item.Marca.Trim());
                atualizar.Parameters.AddWithValue("$categoria", item.Categoria.Trim());
                atualizar.Parameters.AddWithValue("$fornecedor", item.Fornecedor.Trim());
                atualizar.Parameters.AddWithValue("$precoCusto", (double)item.PrecoCusto);
                atualizar.Parameters.AddWithValue("$precoVendaAtual", (double)item.PrecoVendaAtual);
                atualizar.Parameters.AddWithValue("$precoVendaSugerido", (double)item.PrecoVendaSugerido);
                atualizar.Parameters.AddWithValue("$quantidade", item.Quantidade);
                atualizar.Parameters.AddWithValue("$id", item.Id);
                atualizar.ExecuteNonQuery();

                RegistrarAuditoria(conn, item.Id, usuario, "Codigo", original.Codigo, item.Codigo);
                RegistrarAuditoria(conn, item.Id, usuario, "Descricao", original.Descricao, item.Descricao);
                RegistrarAuditoria(conn, item.Id, usuario, "Marca", original.Marca, item.Marca);
                RegistrarAuditoria(conn, item.Id, usuario, "Categoria", original.Categoria, item.Categoria);
                RegistrarAuditoria(conn, item.Id, usuario, "PrecoCusto", original.PrecoCusto.ToString("F2"), item.PrecoCusto.ToString("F2"));
                RegistrarAuditoria(conn, item.Id, usuario, "PrecoVendaAtual", original.PrecoVendaAtual.ToString("F2"), item.PrecoVendaAtual.ToString("F2"));
                RegistrarAuditoria(conn, item.Id, usuario, "PrecoVendaSugerido", original.PrecoVendaSugerido.ToString("F2"), item.PrecoVendaSugerido.ToString("F2"));
                RegistrarAuditoria(conn, item.Id, usuario, "Quantidade", original.Quantidade.ToString(), item.Quantidade.ToString());
            }

            transacao.Commit();
        }

        public void ExcluirItens(IReadOnlyCollection<ItemEstoque> itens, string usuario)
        {
            using var bloqueio = AdquirirBloqueio();
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var transacao = conn.BeginTransaction(System.Data.IsolationLevel.Serializable);

            foreach (var item in itens)
            {
                using var auditoria = conn.CreateCommand();
                auditoria.CommandText = "INSERT INTO AuditoriaItens (ItemId, Usuario, Campo, ValorAnterior, ValorNovo) VALUES ($itemId, $usuario, 'EXCLUSAO', $anterior, NULL);";
                auditoria.Parameters.AddWithValue("$itemId", item.Id);
                auditoria.Parameters.AddWithValue("$usuario", usuario);
                auditoria.Parameters.AddWithValue("$anterior", $"{item.Codigo} | {item.Descricao} | Marca: {item.Marca} | Venda: {item.PrecoVendaAtual:F2} | Estoque: {item.Quantidade}");
                auditoria.ExecuteNonQuery();

                using var excluir = conn.CreateCommand();
                excluir.CommandText = "DELETE FROM Itens WHERE Id = $id;";
                excluir.Parameters.AddWithValue("$id", item.Id);
                excluir.ExecuteNonQuery();
            }

            transacao.Commit();
        }

        private static void RegistrarAuditoria(SqliteConnection conn, int itemId, string usuario, string campo, string anterior, string novo)
        {
            if (anterior == novo) return;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO AuditoriaItens (ItemId, Usuario, Campo, ValorAnterior, ValorNovo) VALUES ($itemId, $usuario, $campo, $anterior, $novo);";
            cmd.Parameters.AddWithValue("$itemId", itemId);
            cmd.Parameters.AddWithValue("$usuario", usuario);
            cmd.Parameters.AddWithValue("$campo", campo);
            cmd.Parameters.AddWithValue("$anterior", anterior);
            cmd.Parameters.AddWithValue("$novo", novo);
            cmd.ExecuteNonQuery();
        }

        private IDisposable AdquirirBloqueio()
        {
            var mutex = new Mutex(false, _mutexName);
            if (!mutex.WaitOne(TimeSpan.FromSeconds(30)))
            {
                mutex.Dispose();
                throw new IOException("O banco está sendo usado por outra operação. Tente novamente em alguns segundos.");
            }
            return new MutexLiberacao(mutex);
        }

        private sealed class MutexLiberacao : IDisposable
        {
            private readonly Mutex _mutex;
            public MutexLiberacao(Mutex mutex) => _mutex = mutex;
            public void Dispose()
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
