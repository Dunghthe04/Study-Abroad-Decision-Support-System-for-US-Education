# Cung cấp các dependency/service mà API cần dùng,

from fastapi import Request

from app.services.activity_reader import ActivityReader
from app.services.recommender import Recommender


def get_recommender(request: Request) -> Recommender:
    """Lấy Recommender đã tạo lúc khởi động (giống lấy service đã đăng ký trong DI container)."""
    return request.app.state.recommender


def get_activity_reader(request: Request) -> ActivityReader:
    """Lấy ActivityReader đã tạo lúc khởi động."""
    return request.app.state.activity_reader
