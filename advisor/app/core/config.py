"""Cấu hình đọc từ biến môi trường hoặc file .env (giống appsettings.json + IOptions bên .NET).
Thứ tự ưu tiên: biến môi trường (docker-compose) > file .env > giá trị mặc định dưới đây.
"""

# Dùng để lưu kết quả trả về của hàm nhằm tái sử dụng.
from functools import lru_cache

# BaseSettings: class nền giúp đọc và kiểm tra cấu hình, SettingsConfigDict: khai báo các tùy chọn quy định cách đọc cấu hình.
from pydantic_settings import BaseSettings, SettingsConfigDict


# Class chứa toàn bộ config của app
class Settings(BaseSettings):
    # đọc thêm cấu hình ở .env, nếu .env có những biết mà Settings ko có thì bỏ qua
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")
    ollama_base_url: str = "http://localhost:11434"
    llm_model: str = "qwen3:8b"
    llm_timeout_seconds: float = 120
    # Kích thước context window dự kiến
    llm_num_ctx: int = 6144
    # Cấu hình số lớp model được đưa lên GPU
    llm_num_gpu: int = 99
    # cùng đầu vào cho cùng kết quả
    llm_seed: int = 42  
    llm_keep_alive: str = "30m"  # giữ model trong VRAM giữa các lượt, không nạp lại

# Lần gọi hàm sau nó lấy đối tượng đã lưu trong lru_cache
@lru_cache
def get_settings() -> Settings:
    """Chỉ đọc cấu hình 1 lần rồi dùng lại (giống đăng ký Singleton)."""
    return Settings()
