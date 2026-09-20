using Microsoft.Data.Sqlite;
using EstoqueApp.Data;
using EstoqueApp.Models;

namespace EstoqueApp.Tests;

public sealed class DatabaseServiceTests
{
    [Fact]
    public void ExcluirItensRegistraAuditoriaERemoveRegistro()
    {
        using var ambiente = BancoTemporario.Criar();
        var banco = ambiente.Banco;
        banco.CarregarItensImportados(new List<ItemEstoque>
        {
            new() { Codigo = "A", Descricao = "Produto A", PrecoCusto = 10, Quantidade = 3 }
        });
        var item = banco.ObterTodos().Single();

        banco.ExcluirItens(new[] { item }, "teste");

        Assert.Empty(banco.ObterTodos());
        using var conexao = new SqliteConnection(ambiente.ConnectionString);
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT Campo, Usuario FROM AuditoriaItens WHERE ItemId = $id";
        comando.Parameters.AddWithValue("$id", item.Id);
        using var leitor = comando.ExecuteReader();
        Assert.True(leitor.Read());
        Assert.Equal("EXCLUSAO", leitor.GetString(0));
        Assert.Equal("teste", leitor.GetString(1));
    }

    [Fact]
    public void SalvarManutencaoRegistraSomenteCamposAlterados()
    {
        using var ambiente = BancoTemporario.Criar();
        ambiente.Banco.CarregarItensImportados(new List<ItemEstoque>
        {
            new() { Codigo = "A", Descricao = "Produto A", PrecoCusto = 10, Quantidade = 3 }
        });
        var original = ambiente.Banco.ObterTodos().Single();
        var editado = Clone(original);
        editado.Descricao = "Produto alterado";

        ambiente.Banco.SalvarManutencao(new List<ItemEstoque> { editado }, new Dictionary<int, ItemEstoque> { [original.Id] = original }, "teste");

        using var conexao = new SqliteConnection(ambiente.ConnectionString);
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT Campo, ValorAnterior, ValorNovo FROM AuditoriaItens WHERE ItemId = $id";
        comando.Parameters.AddWithValue("$id", original.Id);
        using var leitor = comando.ExecuteReader();
        Assert.True(leitor.Read());
        Assert.Equal("Descricao", leitor.GetString(0));
        Assert.Equal("Produto A", leitor.GetString(1));
        Assert.Equal("Produto alterado", leitor.GetString(2));
        Assert.False(leitor.Read());
    }

    [Fact]
    public void RestaurarBackupPreservaEstadoAnteriorEmNovoBackup()
    {
        using var ambiente = BancoTemporario.Criar();
        ambiente.Banco.CarregarItensImportados(new List<ItemEstoque>
        {
            new() { Codigo = "A", Descricao = "Produto A", PrecoCusto = 10, Quantidade = 3 }
        });
        var backup = ambiente.Banco.CriarBackup();
        ambiente.Banco.CarregarItensImportados(new List<ItemEstoque>
        {
            new() { Codigo = "B", Descricao = "Produto B", PrecoCusto = 20, Quantidade = 1 }
        });

        var backupDoEstadoAtual = ambiente.Banco.RestaurarBackup(backup);

        Assert.Equal("A", Assert.Single(ambiente.Banco.ObterTodos()).Codigo);
        Assert.True(File.Exists(backupDoEstadoAtual));
        Assert.Contains(ambiente.Banco.ListarBackups(), caminho => Path.GetFullPath(caminho) == Path.GetFullPath(backupDoEstadoAtual));
    }

    private static ItemEstoque Clone(ItemEstoque item) => new()
    {
        Id = item.Id, Codigo = item.Codigo, Descricao = item.Descricao, Marca = item.Marca,
        Categoria = item.Categoria, Fornecedor = item.Fornecedor, PrecoCusto = item.PrecoCusto,
        PrecoVendaAtual = item.PrecoVendaAtual, PrecoVendaSugerido = item.PrecoVendaSugerido,
        Quantidade = item.Quantidade
    };

    private sealed class BancoTemporario : IDisposable
    {
        private readonly string _pasta;
        public DatabaseService Banco { get; }
        public string ConnectionString { get; }

        private BancoTemporario(string pasta)
        {
            _pasta = pasta;
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, "teste.db");
            ConnectionString = $"Data Source={arquivo}";
            Banco = new DatabaseService(arquivo);
        }

        public static BancoTemporario Criar() => new(Path.Combine(Path.GetTempPath(), "estoque-tests-" + Guid.NewGuid().ToString("N")));

        public void Dispose()
        {
            try { Directory.Delete(_pasta, recursive: true); } catch { }
        }
    }
}