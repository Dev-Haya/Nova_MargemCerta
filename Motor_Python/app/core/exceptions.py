from fastapi import HTTPException, status


class AppError(Exception):
    def __init__(self, message: str, status_code: int = status.HTTP_400_BAD_REQUEST) -> None:
        self.message = message
        self.status_code = status_code
        super().__init__(message)


class InvalidFileError(AppError):
    def __init__(self, message: str) -> None:
        super().__init__(message, status_code=status.HTTP_400_BAD_REQUEST)


class UnauthorizedError(AppError):
    def __init__(self, message: str = "Acesso não autorizado.") -> None:
        super().__init__(message, status_code=status.HTTP_401_UNAUTHORIZED)


class ProcessingError(AppError):
    def __init__(self, message: str) -> None:
        super().__init__(message, status_code=status.HTTP_500_INTERNAL_SERVER_ERROR)
