from typing import Annotated

import uvicorn
from fastapi import (
    Body,
    Depends,
    FastAPI,
    File,
    HTTPException,
    Request,
    UploadFile,
    status,
)
from fastapi.responses import JSONResponse
from fastapi.security import APIKeyHeader

from logging_config import configure_logging, logger
from schemas import RequestProcessamento, ResponseProcessamento
from services import ler_arquivo_planilha, processar_dados_planilha

APP_SECRET_TOKEN = "teste"


def _resolver_user_id(request: Request) -> str:
    """Resolve o identificador de usuário com base nos cabeçalhos da requisição.

    A prioridade é: cabeçalho X-User-ID, depois X-App-Token, e por fim anonymous.

    Args:
        request (Request): Objeto de requisição HTTP do FastAPI.

    Returns:
        str: Identificador do usuário para contextos de log e auditoria.
    """
    user_id = request.headers.get("X-User-ID")
    if user_id:
        return user_id

    token = request.headers.get("X-App-Token")
    if token:
        return "unauthenticated"

    return "anonymous"


# Configura o campo de Token que vai aparecer na página /docs do navegador
api_key_header = APIKeyHeader(name="X-App-Token", auto_error=False)

app = FastAPI(
    title="Motor de Precificação Local",
    description="API para processamento de planilhas e cálculo de margens.",
    version="1.0.0",
    dependencies=[Depends(api_key_header)]  # <--- Habilita o botão de cadeado no Swagger
)

@app.on_event("startup")
def startup_event() -> None:
    """Inicializa a configuração de logging quando a aplicação é iniciada.

    Essa função garante que o logger esteja pronto antes de processar qualquer
    requisição HTTP e registra o evento de inicialização.
    """
    configure_logging()
    logger.info("API iniciada com sucesso")

# Middleware de Segurança (Mantenha o seu middleware como está)
@app.middleware("http")
async def validar_token_de_acesso(request: Request, call_next):
    """Verifica o token de acesso e adiciona contexto de requisição ao estado.

    Este middleware registra acessos públicos, valida o cabeçalho X-App-Token,
    adiciona X-Request-ID e X-Client-IP à resposta e bloqueia requisições não
    autorizadas.

    Args:
        request (Request): Requisição HTTP recebida pelo FastAPI.
        call_next (Callable): Função para executar a próxima etapa da pilha de middleware.

    Returns:
        Response: Resposta HTTP para o cliente.
    """
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

    if token_recebido != APP_SECRET_TOKEN:
        logger.warning(
            "Tentativa de acesso não autorizado para %s com token %s",
            request.url.path,
            token_recebido or "<ausente>",
            extra={
                "request_path": request.url.path,
                "request_method": request.method,
                "request_id": request_id,
                "user_id": user_id,
            },
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
            extra={
                "request_path": request.url.path,
                "request_method": request.method,
                "request_id": request_id,
                "user_id": user_id,
                "client_ip": client_ip,
                "status_code": response.status_code,
            },
        )
        return response

    response = await call_next(request)
    response.headers["X-Request-ID"] = request_id
    response.headers["X-Client-IP"] = client_ip
    logger.debug(
        "Resposta enviada para %s %s",
        request.method,
        request.url.path,
        extra={
            "request_path": request.url.path,
            "request_method": request.method,
            "request_id": request_id,
            "user_id": user_id,
            "client_ip": client_ip,
            "status_code": response.status_code,
        },
    )
    return response


@app.get("/ping")
def ping():
    """Retorna um status simples para health check da API.

    Esta rota é usada para verificar se a aplicação está respondendo corretamente.

    Returns:
        dict: Objeto JSON simples indicando que o serviço está online.
    """
    logger.debug("Health check executado em /ping")
    return {"status": "online", "servidor": "Motor Python pronto!"}


@app.post("/process-prices", response_model=ResponseProcessamento)
def processar_precos(payload: Annotated[RequestProcessamento | None, Body()] = None):
    """Processa a requisição de cálculo de preços usando os dados enviados.

    Valida a presença do payload, chama a camada de serviço para gerar o
    resultado e trata exceções inesperadas retornando HTTP 500 quando necessário.

    Args:
        payload (RequestProcessamento | None): Dados de processamento enviados no corpo.

    Returns:
        ResponseProcessamento: Resultado do processamento de preços.

    Raises:
        HTTPException: Se o payload estiver ausente ou ocorrer erro interno.
    """
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
        raise HTTPException(status_code=500, detail="Erro interno no processamento") from exc

@app.post("/upload-planilha")
async def upload_planilha(
    request: Request,
    file: Annotated[UploadFile, File(...)],
):
    """
    Rota para receber o arquivo (.xlsx/.csv) direto do C#, 
    extrair os dados brutos e retornar a lista padronizada.
    """
    try:
        request_id = getattr(request.state, "request_id", "-")
        user_id = getattr(request.state, "user_id", "anonymous")
        logger.info(
            "Recebido upload de planilha: %s",
            file.filename,
            extra={
                "request_path": "/upload-planilha",
                "request_method": "POST",
                "arquivo": file.filename,
                "request_id": request_id,
                "user_id": user_id,
            },
        )

        conteudo = await file.read()
        dados_extraidos = ler_arquivo_planilha(conteudo, file.filename)

        logger.debug(
            "Upload concluído com sucesso para %s (%s linhas)",
            file.filename,
            len(dados_extraidos),
            extra={
                "request_path": "/upload-planilha",
                "request_method": "POST",
                "arquivo": file.filename,
                "request_id": request_id,
                "user_id": user_id,
            },
        )
        return {
            "sucesso": True,
            "nome_arquivo": file.filename,
            "total_linhas": len(dados_extraidos),
            "dados": dados_extraidos
        }

    except ValueError as exc:
        logger.error("Erro ao processar upload de planilha %s", file.filename, exc_info=True)
        logger.exception("Traceback para erro de upload de planilha %s: %s", file.filename, exc)
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except (AttributeError, OSError, RuntimeError, TypeError) as exc:
        logger.exception("Erro inesperado ao processar planilha %s: %s", file.filename, exc)
        raise HTTPException(status_code=500, detail=f"Erro ao processar planilha: {exc!s}") from exc
    
if __name__ == "__main__":
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=True)