from datetime import date, timedelta

from engine.app.schemas.models import RequestProcessamento
from engine.app.services.processing import processar_dados_planilha


def processar(regras: dict, custo: float = 10.0, venda_atual: float = 10.0):
    payload = RequestProcessamento(
        planilha_fornecedor=[
            {
                "codigo": "A",
                "descricao": "Produto A",
                "preco_custo": custo,
                "marca": "Marca A",
                "categoria": "Categoria A",
                "fornecedor": "Fornecedor A",
            }
        ],
        dados_atuais=[{"codigo": "A", "preco_venda_atual": venda_atual, "estoque": 2}],
        regras=regras,
    )
    return processar_dados_planilha(payload)


def test_aplica_margem_por_fornecedor_antes_da_margem_padrao():
    resultado = processar({"margem_padrao": 0.30, "margem_por_fornecedor": {"Fornecedor A": 0.50}})

    assert resultado.itens[0].preco_venda_sugerido == 20.0


def test_aplica_margem_da_faixa_de_custo():
    resultado = processar({"margem_padrao": 0.30, "faixas_custo": [{"minimo": 5, "maximo": 15, "valor": 0.40}]})

    assert resultado.itens[0].preco_venda_sugerido == 16.666666666666668


def test_aplica_markup_em_vez_de_margem():
    resultado = processar({"margem_padrao": 0.50, "tipo_calculo": "markup"})

    assert resultado.itens[0].preco_venda_sugerido == 15.0


def test_aplica_arredondamento_final_99():
    resultado = processar({"margem_padrao": 0.333, "arredondamento": "final_99"})

    assert resultado.itens[0].preco_venda_sugerido == 14.99


def test_respeita_limites_minimo_e_maximo_por_produto():
    resultado = processar(
        {
            "margem_padrao": 0.30,
            "preco_minimo_por_produto": {"A": 20},
            "preco_maximo_por_produto": {"A": 21},
        }
    )

    assert resultado.itens[0].preco_venda_sugerido == 20.0


def test_aplica_promocao_apenas_dentro_da_validade():
    ontem = (date.today() - timedelta(days=1)).isoformat()
    amanha = (date.today() + timedelta(days=1)).isoformat()
    resultado = processar(
        {
            "margem_padrao": 0.30,
            "promocoes": [{"nome": "Semana", "inicio": ontem, "fim": amanha, "desconto": 0.10}],
        }
    )

    assert round(resultado.itens[0].preco_venda_sugerido, 2) == 12.86
    assert "Promoção vigente" in (resultado.itens[0].alerta or "")


def test_ignora_promocao_fora_da_validade():
    inicio = (date.today() - timedelta(days=10)).isoformat()
    fim = (date.today() - timedelta(days=1)).isoformat()
    resultado = processar(
        {
            "margem_padrao": 0.30,
            "promocoes": [{"nome": "Encerrada", "inicio": inicio, "fim": fim, "desconto": 0.10}],
        }
    )

    assert round(resultado.itens[0].preco_venda_sugerido, 2) == round(10 / 0.7, 2)
    assert "Promoção vigente" not in (resultado.itens[0].alerta or "")


def test_calcula_impacto_de_faturamento_e_margem():
    resultado = processar({"margem_padrao": 0.50})

    assert resultado.resumo.faturamento_atual == 20.0
    assert resultado.resumo.faturamento_sugerido == 40.0
    assert resultado.resumo.margem_sugerida == 0.5