import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    app_secret_token: str = os.getenv("APP_SECRET_TOKEN", "teste")
    app_log_level: str = os.getenv("APP_LOG_LEVEL", "INFO")
    app_log_console: bool = os.getenv("APP_LOG_CONSOLE", "false").strip().lower() in ("1", "true", "yes", "on")
    app_log_max_bytes: int = int(os.getenv("APP_LOG_MAX_BYTES", str(5 * 1024 * 1024)))
    app_log_backup_count: int = int(os.getenv("APP_LOG_BACKUP_COUNT", "3"))
    app_log_rotation_when: str = os.getenv("APP_LOG_ROTATION_WHEN", "midnight")
    app_log_rotation_interval: int = int(os.getenv("APP_LOG_ROTATION_INTERVAL", "1"))
    app_log_keep_days: int = int(os.getenv("APP_LOG_KEEP_DAYS", "7"))


settings = Settings()
