import json
import logging
import os
import time
from collections.abc import Iterator
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import TextIO

from app.core.config import settings


class JsonFormatter(logging.Formatter):
    """Formata registros de log como JSON."""

    def format(self, record: logging.LogRecord) -> str:
        payload = {
            "timestamp": datetime.fromtimestamp(record.created, tz=timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC"),
            "level": record.levelname,
            "message": record.getMessage(),
            "logger": record.name,
        }

        for key in ("request_path", "request_method", "arquivo", "request_id", "user_id", "client_ip", "status_code"):
            if hasattr(record, key):
                payload[key] = getattr(record, key)

        if record.exc_info:
            payload["exception"] = self.formatException(record.exc_info)

        return json.dumps(payload, ensure_ascii=False, default=str)


class MultiRotatingFileHandler(logging.Handler):
    """Manipulador de log com rotação por tamanho ou por tempo."""

    def __init__(
        self,
        filename: str | Path,
        max_bytes: int = 5 * 1024 * 1024,
        backup_count: int = 3,
        when: str = "midnight",
        interval: int = 1,
        encoding: str = "utf-8",
    ) -> None:
        super().__init__()
        self.base_filename = os.path.abspath(str(filename))
        self.max_bytes = max_bytes
        self.backup_count = backup_count
        self.when = when
        self.interval = interval
        self.encoding = encoding
        self.stream = None
        self.rollover_at = self._compute_rollover_at()

    def _compute_rollover_at(self) -> float:
        if self.when == "midnight":
            now = datetime.now(tz=timezone.utc)
            next_midnight = datetime(now.year, now.month, now.day, tzinfo=timezone.utc) + timedelta(days=1)
            return next_midnight.timestamp()
        return time.time() + (self.interval * 60)

    @contextmanager
    def _open_stream(self) -> Iterator[TextIO]:
        with open(self.base_filename, "a", encoding=self.encoding) as stream:
            yield stream

    def emit(self, record: logging.LogRecord) -> None:
        try:
            formatter = self.formatter
            if formatter is None:
                formatter = logging.Formatter("%(message)s")

            formatted_message = formatter.format(record)

            if self.should_rollover(formatted_message):
                self.do_rollover()

            with self._open_stream() as stream:
                stream.write(formatted_message + "\n")
                stream.flush()
        except OSError:
            self.handleError(record)

    def should_rollover(self, message: str) -> bool:
        if time.time() >= self.rollover_at:
            return True

        if self.max_bytes > 0:
            current_size = os.path.getsize(self.base_filename) if os.path.exists(self.base_filename) else 0
            message_size = len(message.encode(self.encoding, errors="replace")) + 1
            if current_size + message_size >= self.max_bytes:
                return True

        return False

    def do_rollover(self) -> None:
        if self.max_bytes > 0 and os.path.exists(self.base_filename):
            self._rotate_by_size()
        else:
            self._rotate_by_time()

        self.rollover_at = self._compute_rollover_at()

    def _rotate_by_size(self) -> None:
        for index in range(self.backup_count - 1, 0, -1):
            source = f"{self.base_filename}.{index}"
            destination = f"{self.base_filename}.{index + 1}"
            if os.path.exists(source):
                if os.path.exists(destination):
                    os.remove(destination)
                os.replace(source, destination)

        if self.backup_count > 0:
            first_backup = f"{self.base_filename}.1"
            if os.path.exists(first_backup):
                os.remove(first_backup)

        if os.path.exists(self.base_filename):
            os.replace(self.base_filename, f"{self.base_filename}.1")

        with open(self.base_filename, "a", encoding=self.encoding):
            pass

    def _rotate_by_time(self) -> None:
        suffix = datetime.now(tz=timezone.utc).strftime("%Y%m%d")
        rotated_name = f"{self.base_filename}.{suffix}"
        if os.path.exists(self.base_filename):
            if os.path.exists(rotated_name):
                os.remove(rotated_name)
            os.replace(self.base_filename, rotated_name)

        with open(self.base_filename, "a", encoding=self.encoding):
            pass

    def close(self) -> None:
        if self.stream is not None:
            self.stream.close()
            self.stream = None
        super().close()


def _limpar_logs_antigos(log_dir: Path, keep_days: int = 7, include_level_logs: bool = True) -> None:
    if not log_dir.exists():
        return

    now = datetime.now(tz=timezone.utc)
    patterns = ["app.log*"]
    if include_level_logs:
        patterns.extend(["app.*.log*"])

    for file_path in [path for pattern in patterns for path in log_dir.glob(pattern)]:
        try:
            if file_path.is_file():
                modified_at = datetime.fromtimestamp(file_path.stat().st_mtime, tz=timezone.utc)
                if modified_at < now - timedelta(days=keep_days):
                    file_path.unlink()
        except OSError:
            continue


def configure_logging(
    log_file: str | Path | None = None,
    max_bytes: int | None = None,
    backup_count: int | None = None,
    when: str | None = None,
    interval: int | None = None,
    keep_days: int | None = None,
) -> logging.Logger:
    max_bytes = max_bytes if max_bytes is not None else settings.app_log_max_bytes
    backup_count = backup_count if backup_count is not None else settings.app_log_backup_count
    when = when if when is not None else settings.app_log_rotation_when
    interval = interval if interval is not None else settings.app_log_rotation_interval
    keep_days = keep_days if keep_days is not None else settings.app_log_keep_days
    console_enabled = settings.app_log_console

    log_path = Path(log_file) if log_file else Path(__file__).resolve().parent / "app.log"
    log_path.parent.mkdir(parents=True, exist_ok=True)

    _limpar_logs_antigos(log_path.parent, keep_days=keep_days, include_level_logs=False)

    logger = logging.getLogger("motor_precificacao")

    for handler in list(logger.handlers):
        logger.removeHandler(handler)
        handler.close()

    logger.setLevel(getattr(logging, settings.app_log_level.upper(), logging.INFO))
    logger.propagate = False

    formatter = JsonFormatter()

    file_handler = MultiRotatingFileHandler(
        log_path,
        max_bytes=max_bytes,
        backup_count=backup_count,
        when=when,
        interval=interval,
        encoding="utf-8",
    )
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)

    for level_name in ("debug", "info", "warning", "error"):
        level_path = log_path.with_name(log_path.stem + f".{level_name}.log")
        level_path.parent.mkdir(parents=True, exist_ok=True)
        level_handler = MultiRotatingFileHandler(
            level_path,
            max_bytes=max_bytes,
            backup_count=backup_count,
            when=when,
            interval=interval,
            encoding="utf-8",
        )
        level_handler.setFormatter(formatter)
        level_handler.setLevel(getattr(logging, level_name.upper()))
        logger.addHandler(level_handler)

    if console_enabled:
        console_handler = logging.StreamHandler()
        console_handler.setFormatter(formatter)
        console_handler.setLevel(getattr(logging, settings.app_log_level.upper(), logging.INFO))
        logger.addHandler(console_handler)

    _limpar_logs_antigos(log_path.parent, keep_days=3, include_level_logs=True)

    return logger


logger = configure_logging()

__all__ = [
    "JsonFormatter",
    "MultiRotatingFileHandler",
    "_limpar_logs_antigos",
    "configure_logging",
    "logger",
]
