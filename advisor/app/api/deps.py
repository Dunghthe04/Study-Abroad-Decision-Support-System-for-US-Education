# Cung cấp các dependency/service mà API cần dùng,
# 1. Khởi đông -> tạo 1 OllamaClient
# 2. Lưu đối tượng vào app.state.llm

from fastapi import Request

from app.services.ollama import OllamaClient
from app.services.scorer import Scorer


def get_llm(request: Request) -> OllamaClient:
    """Lấy OllamaClient đã tạo lúc khởi động (giống lấy service đã đăng ký trong DI container)."""
    return request.app.state.llm

def get_scorer(request: Request) -> Scorer:
    """Lấy Scorer (lượt ② chấm trường) đã tạo lúc khởi động."""
    return request.app.state.scorer