import io

import numpy as np
import pandas as pd

from app.schemas import (
    ItemProcessado,
    RequestProcessamento,
    ResponseProcessamento,
    ResumoProcessamento,
)
import logging_config

logger = logging_config.logger


def _decodificar_csv(conteudo_bytes: bytes, nome_arquivo: str) -> str:
    encodings = ["utf-8-sig", "utf-8", "latin-1", "cp1252"]
    for encoding in encodings:
        try:
            return conteudo_bytes.decode(encoding, errors="strict")
        except UnicodeDecodeError as exc:
            logger.warning(
                "Falha ao decodificar CSV %s como %s: %s",
                nome_arquivo,
                encoding,
                exc,
            )
    raise ValueError("Não foi possível decodificar o CSV. Envie um arquivo em UTF-8 ou Latin-1.")


def ler_arquivo_planilha(conteudo_bytes: bytes, nome_arquivo: str) -> list[dict]:
    nome_arquivo = nome_arquivo.lower()
    logger.debug("Iniciando leitura da planilha %s", nome_arquivo)

    if nome_arquivo.endswith(".csv"):
        content = _decodificar_csv(conteudo_bytes, nome_arquivo)
        cleaned_content = content.replace("\r", "")

        df = None
        for sep in [";", ",", "\t"]:
            try:
                parsed_df = pd.read_csv(
                    io.StringIO(cleaned_content),
                    sep=sep,
                    engine="python",
                    skip_blank_lines=True,
                    dtype=str,
                    keep_default_na=False,
                )
                if parsed_df.shape[1] > 1:
                    df = parsed_df
                    break
            except Exception as exc:  # noqa: BLE001
                logger.warning("Erro ao tentar inferir separador CSV para %s com %s: %s", nome_arquivo, sep, exc)
                continue

        if df is None:
            logger.error("Falha na leitura do CSV %s", nome_arquivo)
            raise ValueError("Não foi possível ler o conteúdo do CSV enviado.")
    elif nome_arquivo.endswith(".xlsx"):
        try:
            df = pd.read_excel(io.BytesIO(conteudo_bytes), engine="openpyxl", dtype=str, keep_default_na=False)
        except Exception as exc:
            logger.exception("Erro ao ler planilha Excel %s: %s", nome_arquivo, exc)
            raise ValueError("Não foi possível ler o conteúdo da planilha enviada.") from exc
    elif nome_arquivo.endswith(".xls"):
        try:
            df = pd.read_excel(io.BytesIO(conteudo_bytes), dtype=str, keep_default_na=False)
        except Exception as exc:
            logger.exception("Erro ao ler planilha Excel %s: %s", nome_arquivo, exc)
            raise ValueError("Não foi possível ler o conteúdo da planilha enviada.") from exc
    else:
        logger.error("Formato de arquivo inválido: %s", nome_arquivo)
        raise ValueError("Formato de arquivo inválido. Envie .xlsx, .xls ou .csv.")

    df = df.replace(r"^\s*$", np.nan, regex=True)
    df = df.loc[:, ~df.isna().all(axis=0)].copy()
    df = df.loc[:, ~df.columns.isna()].copy()
    df.columns = [str(col).strip().lower() for col in df.columns]

    aliases = {
        "preço_custo": "preco_custo",
        "preco custo": "preco_custo",
        "preco": "preco_custo",
        "valor": "preco_custo",
        "descricao": "descricao",
        "codigo": "codigo",
        "marca": "marca",
        "categoria": "categoria",
    }
    normalized_columns = []
    for col in df.columns:
        normalized_columns.append(aliases.get(col, col))
    df.columns = normalized_columns

    colunas_padrao = {
        "codigo": "",
        "descricao": "Sem descrição",
        "preco_custo": 0.0,
        "marca": "Sem Marca",
        "categoria": "Geral",
    }
    for col, val_default in colunas_padrao.items():
        if col not in df.columns:
            df[col] = val_default

    df["codigo"] = df["codigo"].astype(str).str.replace(r"\.0$", "", regex=True).str.strip()
    df["codigo"] = df["codigo"].replace(["nan", "None", "<NA>", "nan.0", ""], "")

    if "preco_custo" in df.columns:
        df["preco_custo"] = (
            df["preco_custo"]
            .astype(str)
            .str.replace("R$", "", regex=False)
            .str.replace(" ", "", regex=False)
            .str.replace(".", "", regex=False)
            .str.replace(",", ".", regex=False)
        )
        df["preco_custo"] = pd.to_numeric(df["preco_custo"], errors="coerce").fillna(0.0)

    df = df.fillna(colunas_padrao)
    df = df.replace({np.nan: None})

    for col in df.select_dtypes(include=["object"]).columns:
        df[col] = df[col].astype(str).str.lstrip("=+@-")

    dados = df.to_dict(orient="records")
    logger.debug("Leitura da planilha %s concluída com %s linhas", nome_arquivo, len(dados))
    return dados


def processar_dados_planilha(payload: RequestProcessamento) -> ResponseProcessamento:
    try:
        df_fornecedor = pd.DataFrame([item.model_dump() for item in payload.planilha_fornecedor])
        df_atuais = pd.DataFrame([item.model_dump() for item in payload.dados_atuais])

        if df_fornecedor.empty:
            return ResponseProcessamento(
                sucesso=False,
                mensagem="A planilha enviada está vazia.",
                resumo=ResumoProcessamento(total_itens=0, itens_em_quarentena=0, alteracoes_de_preco=0),
                itens=[],
            )

        for col in ["codigo", "descricao", "preco_custo", "marca", "categoria"]:
            if col not in df_fornecedor.columns:
                df_fornecedor[col] = "" if col != "preco_custo" else 0.0

        if not df_atuais.empty and "codigo" in df_atuais.columns:
            df_merged = pd.merge(df_fornecedor, df_atuais, on="codigo", how="left", suffixes=("_novo", "_atual"))
        else:
            df_merged = df_fornecedor.copy()
            df_merged["preco_venda_atual"] = 0.0

        itens_processados = []
        total_quarentena = 0
        total_alteracoes = 0

        margem_padrao = payload.regras.margem_padrao
        if margem_padrao >= 1.0:
            margem_padrao = 0.99

        limite_aumento = payload.regras.aumento_maximo_percentual
        limite_reducao = payload.regras.reducao_maxima_percentual

        for _, row in df_merged.iterrows():
            codigo = str(row.get("codigo") or "")
            descricao = str(row.get("descricao") or "Sem descrição").strip()
            marca = str(row.get("marca") or "Sem Marca")
            categoria = str(row.get("categoria") or "Geral")

            try:
                preco_custo_novo = float(row.get("preco_custo") or 0.0)
            except (ValueError, TypeError):
                preco_custo_novo = 0.0

            try:
                preco_venda_atual = float(row.get("preco_venda_atual") or 0.0)
            except (ValueError, TypeError):
                preco_venda_atual = 0.0

            margem_aplicada = margem_padrao
            preco_venda_sugerido = preco_custo_novo / (1 - margem_aplicada)
            diferenca_valor = preco_venda_sugerido - preco_venda_atual
            porcentagem_diferenca = 0.0 if preco_venda_atual == 0 else diferenca_valor / preco_venda_atual

            quarentena = False
            alerta = None

            if porcentagem_diferenca > limite_aumento:
                quarentena = True
                alerta = "Aumento acima do limite"
            elif porcentagem_diferenca < -limite_reducao:
                quarentena = True
                alerta = "Redução acima do limite"

            total_quarentena += int(quarentena)
            total_alteracoes += int(abs(porcentagem_diferenca) > 0.0001)

            itens_processados.append(
                ItemProcessado(
                    codigo=codigo,
                    descricao=descricao,
                    marca=marca,
                    categoria=categoria,
                    preco_custo_novo=preco_custo_novo,
                    preco_venda_atual=preco_venda_atual,
                    preco_venda_sugerido=preco_venda_sugerido,
                    margem_aplicada=margem_aplicada,
                    diferenca_valor=diferenca_valor,
                    quarentena=quarentena,
                    alerta=alerta,
                )
            )

        return ResponseProcessamento(
            sucesso=True,
            mensagem="Processamento concluído com sucesso.",
            resumo=ResumoProcessamento(
                total_itens=len(itens_processados),
                itens_em_quarentena=total_quarentena,
                alteracoes_de_preco=total_alteracoes,
            ),
            itens=itens_processados,
        )
    except Exception as exc:  # noqa: BLE001
        logger.exception("Erro inesperado ao processar dados: %s", exc)
        raise
