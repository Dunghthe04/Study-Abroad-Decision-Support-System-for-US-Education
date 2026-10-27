import logging
from contextlib import asynccontextmanager

import httpx  # gửi request đến ollama
from fastapi import Depends, FastAPI

from app.api.deps import require_api_key
from app.api.routes import chat, health, profile, recommend
from app.core.config import get_settings
from app.services.chat_flow import ChatFlow
from app.services.ec_reader import EcReader
from app.services.explainer import Explainer
from app.services.gemini import GeminiClient
from app.services.ollama import OllamaClient
from app.services.picker import Picker
from app.services.scorer import Scorer
from app.services.skill_picker_loader import load_skill_picker
from app.services.transcript_reader import TranscriptReader
from logging.handlers import RotatingFileHandler
from pathlib import Path


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
        app.state.chat_flow = ChatFlow(picker=load_skill_picker(http))
        yield


ADVISOR_DIR = Path(__file__).resolve().parents[1]  # app/main.py -> advisor/


# Chỉ cho các dòng chat_turn đi qua, để file log không lẫn log khác
class OnlyChatTurns(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        return record.getMessage().startswith("chat_turn ")


# Ghi các dòng chat_turn ra file (nếu cấu hình CHAT_LOG_FILE). Lỗi ghi file không được làm sập dịch vụ
def add_chat_file_handler(log: logging.Logger) -> None:
    path = get_settings().chat_log_file
    if not path:
        return
    file = Path(path)
    if not file.is_absolute():
        file = ADVISOR_DIR / file
    try:
        file.parent.mkdir(parents=True, exist_ok=True)
        # utf-8 bắt buộc: Windows mặc định không phải utf-8 nên tiếng Việt sẽ hỏng hoặc báo lỗi
        handler = RotatingFileHandler(file, maxBytes=5_000_000, backupCount=5, encoding="utf-8")
    except OSError:
        log.exception("Không mở được file log chat: %s", file)
        return
    handler.addFilter(OnlyChatTurns())
    handler.setFormatter(logging.Formatter("%(asctime)s %(message)s"))
    log.addHandler(handler)


# Log của advisor (tên bắt đầu bằng "advisor") mặc định bị uvicorn bỏ qua nên phải tự bật mức INFO
def setup_logging() -> None:
    log = logging.getLogger("advisor")
    log.setLevel(logging.INFO)
    if not log.handlers:  # chỉ chạy lần đầu
        handler = logging.StreamHandler()
        handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(name)s %(message)s"))
        log.addHandler(handler)
        add_chat_file_handler(log)  # thêm dòng này


def create_app() -> FastAPI:
    """Tạo ứng dụng. Viết thành hàm để test có thể tạo app mới, độc lập cho mỗi lần chạy."""
    setup_logging()
    app = FastAPI(title="Study Abroad Advisor Service", version="0.1.0", lifespan=lifespan)
    app.include_router(health.router)
    # Các route gọi Ollama đều cần khoá; health để mở cho docker healthcheck
    app.include_router(profile.router, dependencies=[Depends(require_api_key)])
    app.include_router(recommend.router, dependencies=[Depends(require_api_key)])
    app.include_router(chat.router, dependencies=[Depends(require_api_key)])  # thêm
    return app


app = create_app()
