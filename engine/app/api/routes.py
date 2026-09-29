from contextlib import asynccontextmanager
from typing import Annotated

import uvicorn
from fastapi import Body, Depends, FastAPI, File, HTTPException, Request, UploadFile, status
from fastapi.responses import JSONResponse
from fastapi.security import APIKeyHeader

from engine import logging_config
from engine.app.api.errors import register_exception_handlers
from engine.app.core.config import settings
from engine.app.core.exceptions import InvalidFileError, ProcessingError, UnauthorizedError
from engine.app.schemas import RequestProcessamento, ResponseProcessamento
from engine.app.services import ler_arquivo_planilha, processar_dados_planilha

logger = logging_config.logger


def _resolver_user_id(request: Request) -> str:
    user_id = request.headers.get("X-User-ID")
    if user_id:
        return user_id

    token = request.headers.get("X-App-Token")
    if token:
        return "unauthenticated"

    return "anonymous"


api_key_header = APIKeyHeader(name="X-App-Token", auto_error=False)


@asynccontextmanager
async def lifespan(_: FastAPI):
    logging_config.configure_logging()
    logger.info("API iniciada com sucesso")
    yield
    logger.info("API encerrada com sucesso")


def startup_event() -> None:
    logging_config.configure_logging()
    logger.info("API iniciada com sucesso")


app = FastAPI(
    title="Motor de Precificação Local",
    description="API para processamento de planilhas e cálculo de margens.",
    version="1.0.0",
    dependencies=[Depends(api_key_header)],
    lifespan=lifespan,
)

app.router.add_event_handler("startup", startup_event)
register_exception_handlers(app)


@app.middleware("http")
async def validar_token_de_acesso(request: Request, call_next):
    request_id = request.headers.get("X-Request-ID") or "req-" + str(id(request))
    user_id = _resolver_user_id(request)
    client_ip = request.headers.get("X-Forwarded-For", request.client.host if request.client else "unknown")

    request.state.request_id = request_id
    request.state.user_id = user_id
    request.state.client_ip = client_ip

    if request.url.path in ["/ping", "/docs", "/openapi.json"]:
        logger.debug(
            "Acesso público à rota %s",
            request.url.path,
            extra={"request_path": request.url.path, "request_method": request.method, "request_id": request_id, "user_id": user_id, "client_ip": client_ip},
        )
        return await call_next(request)

    token_recebido = request.headers.get("X-App-Token")

    if token_recebido != settings.app_secret_token:
        logger.warning(
            "Tentativa de acesso não autorizado para %s com token %s",
            request.url.path,
            token_recebido or "<ausente>",
            extra={"request_path": request.url.path, "request_method": request.method, "request_id": request_id, "user_id": user_id},
        )
        response = JSONResponse(
            status_code=status.HTTP_401_UNAUTHORIZED,
            content={"sucesso": False, "mensagem": "Acesso não autorizado. Token inválido ou ausente."},
        )
        response.headers["X-Request-ID"] = request_id
        response.headers["X-Client-IP"] = client_ip
        logger.warning(
            "Resposta de autenticação para %s %s",
            request.method,
            request.url.path,
            extra={"request_path": request.url.path, "request_method": request.method, "request_id": request_id, "user_id": user_id, "client_ip": client_ip, "status_code": response.status_code},
        )
        return response

    response = await call_next(request)
    response.headers["X-Request-ID"] = request_id
    response.headers["X-Client-IP"] = client_ip
    logger.debug(
        "Resposta enviada para %s %s",
        request.method,
        request.url.path,
        extra={"request_path": request.url.path, "request_method": request.method, "request_id": request_id, "user_id": user_id, "client_ip": client_ip, "status_code": response.status_code},
    )
    return response


@app.get("/ping")
def ping():
    logger.debug("Health check executado em /ping")
    return {"status": "online", "servidor": "Motor Python pronto!"}


@app.post("/process-prices", response_model=ResponseProcessamento)
def processar_precos(payload: Annotated[RequestProcessamento | None, Body()] = None):
    if payload is None:
        logger.warning("Requisição recebida sem payload válido em /process-prices")
        raise HTTPException(
            status_code=400,
            detail="O corpo da requisição deve conter um JSON válido com os dados de processamento.",
        )

    logger.info("Iniciando processamento de planilha para %s itens do fornecedor", len(payload.planilha_fornecedor))
    try:
        resultado = processar_dados_planilha(payload)
        logger.debug("Processamento concluído com sucesso: %s", resultado.mensagem)
        return resultado
    except Exception as exc:
        logger.exception("Erro inesperado ao processar dados: %s", exc)
        raise ProcessingError("Erro interno no processamento") from exc


@app.post("/upload-planilha")
async def upload_planilha(request: Request, file: Annotated[UploadFile, File(...)]):
    try:
        request_id = getattr(request.state, "request_id", "-")
        user_id = getattr(request.state, "user_id", "anonymous")
        logger.info(
            "Recebido upload de planilha: %s",
            file.filename,
            extra={"request_path": "/upload-planilha", "request_method": "POST", "arquivo": file.filename, "request_id": request_id, "user_id": user_id},
        )

        conteudo = await file.read()
        dados_extraidos = ler_arquivo_planilha(conteudo, file.filename)

        logger.debug(
            "Upload concluído com sucesso para %s (%s linhas)",
            file.filename,
            len(dados_extraidos),
            extra={"request_path": "/upload-planilha", "request_method": "POST", "arquivo": file.filename, "request_id": request_id, "user_id": user_id},
        )
        return {
            "sucesso": True,
            "nome_arquivo": file.filename,
            "total_linhas": len(dados_extraidos),
            "dados": dados_extraidos,
        }

    except ValueError as exc:
        logger.error("Erro ao processar upload de planilha %s", file.filename, exc_info=True)
        logger.exception("Traceback para erro de upload de planilha %s: %s", file.filename, exc)
        raise InvalidFileError(str(exc)) from exc
    except (AttributeError, OSError, RuntimeError, TypeError) as exc:
        logger.exception("Erro inesperado ao processar planilha %s: %s", file.filename, exc)
        raise ProcessingError(f"Erro ao processar planilha: {exc!s}") from exc
