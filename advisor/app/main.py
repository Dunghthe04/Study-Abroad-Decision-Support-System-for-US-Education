from contextlib import asynccontextmanager

import httpx  # gửi request đến ollama
from fastapi import Depends, FastAPI

from app.api.deps import require_api_key
from app.api.routes import health, profile
from app.core.config import get_settings
from app.services.ollama import OllamaClient
from app.services.transcript_reader import TranscriptReader


# Khai báo vòng đơi fastapi
@asynccontextmanager
# Đọc Settings → tạo HTTP client → tạo OllamaClient
# Quản lý việc khởi tạo và dọn dẹp tài nguyên theo vòng đời của ứng dụng.
async def lifespan(app: FastAPI):
    settings = get_settings()
    # Tạo kết nối Http đến ollama
    async with httpx.AsyncClient(
        base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds
    ) as http:
        # tạo OllamaClient
        app.state.llm = OllamaClient(
            http,
            settings.llm_model,
            num_ctx=settings.llm_num_ctx,
            num_gpu=settings.llm_num_gpu,
            seed=settings.llm_seed,
            keep_alive=settings.llm_keep_alive,
        )
        # Đọc bảng điểm (OCR) của USAS-640
        app.state.transcript_reader = TranscriptReader(http, settings)
        yield


def create_app() -> FastAPI:
    """Tạo ứng dụng. Viết thành hàm để test có thể tạo app mới, độc lập cho mỗi lần chạy."""
    app = FastAPI(title="Study Abroad Advisor Service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    # Các route gọi Ollama đều cần khoá; health để mở cho docker healthcheck
    app.include_router(profile.router, dependencies=[Depends(require_api_key)])
    return app


app = create_app()
