# Cung cấp các dependency/service mà API cần dùng,
# 1. Khởi đông -> tạo 1 OllamaClient
# 2. Lưu đối tượng vào app.state.llm

from fastapi import Request

from app.services.ollama import OllamaClient


def get_llm(request: Request) -> OllamaClient:
    """Lấy OllamaClient đã tạo lúc khởi động (giống lấy service đã đăng ký trong DI container)."""
    return request.app.state.llm
