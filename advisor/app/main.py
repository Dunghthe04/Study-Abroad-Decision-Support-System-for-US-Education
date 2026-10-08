from contextlib import asynccontextmanager

import httpx
from fastapi import FastAPI

from app.api.routes import health, profile, recommendation
from app.core.config import get_settings
from app.services.activity_reader import ActivityReader
from app.services.ollama import OllamaClient
from app.services.recommender import Recommender


# Khai báo vòng đơi fastapi
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Trước yield: chạy 1 lần lúc khởi động. Sau yield: chạy lúc tắt (đóng kết nối tới Ollama)."""
    settings = get_settings()
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        llm = OllamaClient(http, settings.llm_model)
        app.state.recommender = Recommender(llm)
        app.state.activity_reader = ActivityReader(llm)
        yield


def create_app() -> FastAPI:
    """Tạo ứng dụng. Viết thành hàm để test có thể tạo app mới, độc lập cho mỗi lần chạy."""
    app = FastAPI(title="Study Abroad Advisor Service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    app.include_router(recommendation.router)
    app.include_router(profile.router)
    return app


app = create_app()
