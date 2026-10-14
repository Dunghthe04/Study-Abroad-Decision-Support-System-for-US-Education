# Cung cấp các dependency/service mà API cần dùng,

import secrets

from fastapi import Depends, Header, HTTPException, Request, status

from app.core.config import Settings, get_settings
from app.services.activity_reader import ActivityReader
from app.services.recommender import Recommender


def require_api_key(
    x_api_key: str | None = Header(default=None),
    settings: Settings = Depends(get_settings),
) -> None:
    """Chỉ .NET backend (có khoá chung) mới gọi được OCR/LLM, tránh người ngoài chiếm GPU.

    Cấu hình api_key trống thì bỏ qua để chạy local không cần khoá.
    """
    expected = settings.api_key
    if not expected:
        return
    # compare_digest: so sánh mất thời gian như nhau, không đoán được khoá qua thời gian phản hồi
    if not x_api_key or not secrets.compare_digest(x_api_key.encode(), expected.encode()):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Sai hoặc thiếu API key.")


def get_recommender(request: Request) -> Recommender:
    """Lấy Recommender đã tạo lúc khởi động (giống lấy service đã đăng ký trong DI container)."""
    return request.app.state.recommender


def get_activity_reader(request: Request) -> ActivityReader:
    """Lấy ActivityReader đã tạo lúc khởi động."""
    return request.app.state.activity_reader


def get_transcript_reader(request: Request):
    """Lấy TranscriptReader đã tạo lúc khởi động."""
    return request.app.state.transcript_reader
