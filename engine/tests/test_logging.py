import importlib
import json
import re
from pathlib import Path

from fastapi.testclient import TestClient

from engine import logging_config
import engine.main as main
from engine.logging_config import configure_logging


def test_configure_logging_escreve_no_arquivo(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"

    logger = configure_logging(log_path)
    logger.info("teste de logging")

    assert log_path.exists()
    content = log_path.read_text(encoding="utf-8")
    assert "teste de logging" in content


def test_configure_logging_roteia_arquivo_quando_ultrapassa_limite(tmp_path: Path) -> None:
    
    
    log_path = tmp_path / "app.log"

    logger = configure_logging(log_path, max_bytes=80, backup_count=1)
    logger.info("linha 1" * 20)
    logger.info("linha 2" * 20)

    assert log_path.exists()
    assert (tmp_path / "app.log.1").exists()


def test_main_chama_configuracao_de_logs_ao_iniciar_app(monkeypatch) -> None:
    called = False

    original_configure_logging = logging_config.configure_logging

    def wrapped_configure_logging(*args, **kwargs):
        nonlocal called
        called = True
        return original_configure_logging(*args, **kwargs)

    monkeypatch.setattr(logging_config, "configure_logging", wrapped_configure_logging)

    importlib.reload(main)
    main.app.router.on_startup[0]()

    assert called


def _strip_ansi(text: str) -> str:
    return re.sub(r"\x1b\[[0-9;]*m", "", text)


def test_configure_logging_escreve_json_com_contexto(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    logger = configure_logging(log_path)

    logger.info(
        "teste estruturado",
        extra={"request_path": "/upload-planilha", "request_method": "POST", "arquivo": "planilha.csv"},
    )

    content = log_path.read_text(encoding="utf-8").strip()
    payload = json.loads(_strip_ansi(content.splitlines()[-1]))

    assert payload["message"] == "teste estruturado"
    assert payload["request_path"] == "/upload-planilha"
    assert payload["request_method"] == "POST"
    assert payload["arquivo"] == "planilha.csv"


def test_configure_logging_cria_arquivos_por_nivel_e_contexto(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    logger = configure_logging(log_path)

    logger.info("info contextual", extra={"request_id": "req-1", "user_id": "ana"})
    logger.warning("warning contextual", extra={"request_id": "req-1", "user_id": "ana"})
    logger.error("error contextual", extra={"request_id": "req-1", "user_id": "ana"})

    assert (tmp_path / "app.info.log").exists()
    assert (tmp_path / "app.warning.log").exists()
    assert (tmp_path / "app.error.log").exists()

    payload = json.loads(_strip_ansi((tmp_path / "app.info.log").read_text(encoding="utf-8").strip().splitlines()[-1]))
    assert payload["request_id"] == "req-1"
    assert payload["user_id"] == "ana"


def test_configure_logging_nao_emite_no_console(tmp_path: Path, capsys) -> None:
    logger = configure_logging(tmp_path / "app.log")
    logger.info("log no console")

    captured = capsys.readouterr()
    assert captured.out == ""
    assert captured.err == ""


def test_ping_nao_gera_log_info_no_arquivo(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    main.logger = configure_logging(log_path)
    client = TestClient(main.app)

    response = client.get("/ping")

    assert response.status_code == 200
    if log_path.exists():
        assert "Health check executado em /ping" not in log_path.read_text(encoding="utf-8")


def test_configure_logging_usa_retencao_padrao_de_sete_dias(monkeypatch, tmp_path: Path) -> None:
    captured: list[int] = []

    def fake_limpar_logs_antigos(log_dir: Path, keep_days: int = 7, **kwargs) -> None:
        captured.append(keep_days)

    monkeypatch.setattr(logging_config, "_limpar_logs_antigos", fake_limpar_logs_antigos)
    configure_logging(tmp_path / "app.log")

    assert 7 in captured
    assert 3 in captured


def test_configure_logging_escreve_estrutura_simples_no_arquivo(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    logger = configure_logging(log_path)

    logger.warning("log colorido")

    content = log_path.read_text(encoding="utf-8")
    assert "\x1b[" not in content
    assert '"level": "WARNING"' in content
    assert '"message": "log colorido"' in content


def test_token_invalido_registra_warning(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    main.logger = configure_logging(log_path)
    client = TestClient(main.app)

    response = client.post(
        "/process-prices",
        headers={"X-App-Token": "senha123"},
        json={
            "planilha_fornecedor": [],
            "dados_atuais": [],
            "regras": {
                "margem_padrao": 0.3,
                "aumento_maximo_percentual": 0.2,
                "reducao_maxima_percentual": 0.1,
            },
        },
    )

    assert response.status_code == 401
    content = log_path.read_text(encoding="utf-8")
    assert "nao autorizado" in content.lower() or "não autorizado" in content.lower()
    assert "warning" in content.lower()
    assert "unauthenticated" in content


def test_upload_planilha_registra_erro_e_traceback_para_formato_invalido(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    main.logger = configure_logging(log_path)
    client = TestClient(main.app)

    response = client.post(
        "/upload-planilha",
        headers={"X-App-Token": "teste"},
        files={"file": ("arquivo.pdf", b"conteudo", "application/pdf")},
    )

    assert response.status_code == 400
    content = log_path.read_text(encoding="utf-8")
    assert "formato de arquivo inválido" in content.lower()
    assert "error" in content.lower()
    assert "traceback" in content.lower()


def test_upload_planilha_registra_traceback_para_csv_corrompido(tmp_path: Path) -> None:
    log_path = tmp_path / "app.log"
    main.logger = configure_logging(log_path)
    client = TestClient(main.app)

    response = client.post(
        "/upload-planilha",
        headers={"X-App-Token": "teste"},
        files={"file": ("arquivo.csv", b'codigo;descricao\n1;"teste\n2;ok', "text/csv")},
    )

    assert response.status_code == 400
    content = log_path.read_text(encoding="utf-8")
    assert "erro" in content.lower() or "falha" in content.lower()
    assert "traceback" in content.lower()
