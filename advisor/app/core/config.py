"""Cấu hình đọc từ biến môi trường hoặc file .env (giống appsettings.json + IOptions bên .NET).

Tên biến không phân biệt hoa thường: OLLAMA_BASE_URL -> ollama_base_url.
Thứ tự ưu tiên: biến môi trường (docker-compose) > file .env > giá trị mặc định dưới đây.
"""

# tạo lần đầu , những lần sau lấy lại object cũ k cần tạo lại
from functools import lru_cache
from pathlib import Path

# BaseSettings: đọc config từ env, SettingsConfigDict: cấu hình cách BaseSettings đọc cấu hình
from pydantic_settings import BaseSettings, SettingsConfigDict

ADVISOR_ENV_FILE = Path(__file__).resolve().parents[2] / ".env"  # app/core/config.py -> advisor/.env


# Class chứa toàn bộ config của app
class Settings(BaseSettings):
    # đọc thêm cấu hình ở .env, nếu .env có những biết mà Settings ko có thì bỏ qua
    # Luôn là advisor/.env dù chạy uvicorn từ thư mục nào (chạy từ gốc repo thì ".env" sẽ là file của docker-compose)
    model_config = SettingsConfigDict(env_file=ADVISOR_ENV_FILE, extra="ignore")
    ollama_base_url: str = "http://localhost:11434"
    llm_model: str = "qwen3:8b"
    llm_timeout_seconds: float = 120
    # Đọc bảng điểm: model ảnh chép ảnh thành chữ (bước A), LLM chữ bóc điểm thành JSON (bước B)
    vision_model: str = "glm-ocr"
    vision_timeout_seconds: float = 240
    vision_keep_alive: int = 0  # nhả GPU ngay sau bước A để model bước B có chỗ (GPU 4GB)
    vision_cpu_fallback: bool = True  # Ollama lỗi 5xx (thường do thiếu VRAM) thì thử lại 1 lần trên CPU
    image_max_side: int = 2000
    # Để trống = dùng chung llm_model. Máy GPU yếu / server chỉ có CPU nên đặt qwen3:4b cho nhanh
    transcript_parse_model: str = ""
    transcript_parse_timeout_seconds: float = 180


@lru_cache
def get_settings() -> Settings:
    """Chỉ đọc cấu hình 1 lần rồi dùng lại (giống đăng ký Singleton)."""
    return Settings()
