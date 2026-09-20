from engine.app.services.processing import ler_arquivo_planilha


def test_importa_app_a_partir_do_pacote_app():
    import engine.main as main
    from engine.app.api.routes import app as app_package

    assert main.app is app_package


def test_ler_arquivo_planilha_normaliza_coluna_fornecedores_e_codigo_vazios():
    csv_content = "codigo;descricao;preco_custo;fornecedores\n ;Produto;10.5;\n"

    dados = ler_arquivo_planilha(csv_content.encode("utf-8"), "arquivo.csv")

    assert dados[0]["codigo"] == ""
    assert dados[0]["fornecedor"] == ""


def test_ler_arquivo_planilha_ignora_linhas_em_branco_e_preserva_preco():
    csv_content = "codigo;descricao;preco_custo;fornecedores\n;;10.5;\n1;Produto A;12.75;Fornecedor X\n"

    dados = ler_arquivo_planilha(csv_content.encode("utf-8"), "arquivo.csv")

    assert len(dados) == 1
    assert dados[0]["descricao"] == "Produto A"
    assert dados[0]["preco_custo"] == 12.75


def test_ler_arquivo_planilha_reconhece_coluna_preco_com_variacao_de_header():
    csv_content = "Codigo,descricao,preço_,marca,categoria,fornecedor\n248714,guardanapo,5,teste,descartavel,Teste\n"

    dados = ler_arquivo_planilha(csv_content.encode("utf-8"), "arquivo.csv")

    assert len(dados) == 1
    assert dados[0]["preco_custo"] == 5.0
