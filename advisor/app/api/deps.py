# Cung cấp các dependency/service mà API cần dùng,

from fastapi import Request

from app.services.recommender import Recommender


def get_recommender(request: Request) -> Recommender:
    """Lấy Recommender đã tạo lúc khởi động (giống lấy service đã đăng ký trong DI container)."""
    return request.app.state.recommender
