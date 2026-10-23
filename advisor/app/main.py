from contextlib import asynccontextmanager

import httpx  # gửi request đến ollama
from fastapi import Depends, FastAPI

from app.api.deps import require_api_key
from app.api.routes import health, profile, recommend
from app.core.config import get_settings
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer
from app.services.gemini import GeminiClient
from app.services.ollama import OllamaClient
from app.services.picker import Picker
from app.services.scorer import Scorer
from app.services.transcript_reader import TranscriptReader


# Khai báo vòng đơi fastapi
@asynccontextmanager
# Đọc Settings → tạo HTTP client → tạo OllamaClient
# Quản lý việc khởi tạo và dọn dẹp tài nguyên theo vòng đời của ứng dụng.
async def lifespan(app: FastAPI):
    settings = get_settings()
    # Tạo kết nối Http đến ollama
    async with (
        httpx.AsyncClient(base_url=settings.ollama_base_url, timeout=settings.llm_timeout_seconds) as http,
        httpx.AsyncClient(base_url=settings.gemini_base_url, timeout=180) as gemini_http,
    ):
        app.state.llm = OllamaClient(
            http,
            settings.llm_model,
            num_ctx=settings.llm_num_ctx,
            num_gpu=settings.llm_num_gpu,
            seed=settings.llm_seed,
            keep_alive=settings.llm_keep_alive,
        )
        app.state.transcript_reader = TranscriptReader(http, settings)
        app.state.ec_reader = EcReader(app.state.llm, settings.llm_model)
        gemini = GeminiClient(
            gemini_http,
            settings.gemini_model,
            settings.gemini_api_key,
            thinking_level=settings.gemini_thinking_level,
        )
        app.state.scorer = Scorer(gemini, settings.gemini_model)
        app.state.picker = Picker(gemini, settings.gemini_model)
        app.state.explainer = Explainer(app.state.llm, settings.llm_model)

        yield


def create_app() -> FastAPI:
    """Tạo ứng dụng. Viết thành hàm để test có thể tạo app mới, độc lập cho mỗi lần chạy."""
    app = FastAPI(title="Study Abroad Advisor Service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    # Các route gọi Ollama đều cần khoá; health để mở cho docker healthcheck
    app.include_router(profile.router, dependencies=[Depends(require_api_key)])
    app.include_router(recommend.router, dependencies=[Depends(require_api_key)])
    return app


app = create_app()
