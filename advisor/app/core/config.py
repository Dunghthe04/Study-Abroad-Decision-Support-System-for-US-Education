"""Cấu hình đọc từ biến môi trường hoặc file .env (giống appsettings.json + IOptions bên .NET).

Tên biến không phân biệt hoa thường: OLLAMA_BASE_URL -> ollama_base_url.
Thứ tự ưu tiên: biến môi trường (docker-compose) > file .env > giá trị mặc định dưới đây.
"""

# tạo lần đầu , những lần sau lấy lại object cũ k cần tạo lại
from functools import lru_cache

# BaseSettings: đọc config từ env, SettingsConfigDict: cấu hình cách BaseSettings đọc cấu hình
from pydantic_settings import BaseSettings, SettingsConfigDict


# Class chứa toàn bộ config của app
class Settings(BaseSettings):
    # đọc thêm cấu hình ở .env, nếu .env có những biết mà Settings ko có thì bỏ qua
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")
    ollama_base_url: str = "http://localhost:11434"
    llm_model: str = "qwen3:8b"
    llm_timeout_seconds: float = 120


@lru_cache
def get_settings() -> Settings:
    """Chỉ đọc cấu hình 1 lần rồi dùng lại (giống đăng ký Singleton)."""
    return Settings()
