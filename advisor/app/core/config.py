"""Cấu hình đọc từ biến môi trường hoặc file .env (giống appsettings.json + IOptions bên .NET).
Thứ tự ưu tiên: biến môi trường (docker-compose) > file .env > giá trị mặc định dưới đây.
"""

# Dùng để lưu kết quả trả về của hàm nhằm tái sử dụng.
from functools import lru_cache
from pathlib import Path

# BaseSettings: class nền giúp đọc và kiểm tra cấu hình, SettingsConfigDict: khai báo các tùy chọn quy định cách đọc cấu hình.
from pydantic_settings import BaseSettings, SettingsConfigDict

ADVISOR_ENV_FILE = Path(__file__).resolve().parents[2] / ".env"  # app/core/config.py -> advisor/.env


# Class chứa toàn bộ config của app
class Settings(BaseSettings):
    # đọc thêm cấu hình ở .env, nếu .env có những biết mà Settings ko có thì bỏ qua
    # Luôn là advisor/.env dù chạy uvicorn từ thư mục nào (chạy từ gốc repo thì ".env" sẽ là file của docker-compose)
    model_config = SettingsConfigDict(env_file=ADVISOR_ENV_FILE, extra="ignore")
    # Khoá chung với .NET (header X-Api-Key). Để trống = không kiểm tra (chạy local); prod bắt buộc đặt
    api_key: str = ""
    # Đường dẫn file lưu log từng lượt chat (dữ liệu để gắn nhãn). Để trống = không ghi file.
    # Đường dẫn tương đối tính từ thư mục advisor/
    chat_log_file: str = ""
    # Số ngày giữ file log chat, quá hạn thì tự xóa
    chat_log_keep_days: int = 90
    ollama_base_url: str = "http://localhost:11434"
    llm_model: str = "qwen3:8b"
    llm_timeout_seconds: float = 120
    # Kích thước context window dự kiến
    llm_num_ctx: int = 6144
    # Cấu hình số lớp model được đưa lên GPU
    llm_num_gpu: int = 99
    # cùng đầu vào cho cùng kết quả
    llm_seed: int = 42
    embedding_model: str = "bge-m3"
    database_url: str = ""
    # khi thử có thể đặt "verified,draft"
    knowledge_statuses: str = "verified"
    # Độ tương đồng tối thiểu (cosine, 0 đến 1) giữa câu hỏi và câu hỏi mẫu trong kho; thấp hơn thì từ chối
    knowledge_min_score: float = 0.72
    # Số đoạn kiến thức đưa cho AI mỗi lần trả lời
    knowledge_top_k: int = 4
    llm_keep_alive: str = "30m"  # giữ model trong VRAM giữa các lượt, không nạp lại
    # Đọc bảng điểm: model ảnh chép ảnh thành chữ (bước A), LLM chữ bóc điểm thành JSON (bước B)
    vision_model: str = "glm-ocr"
    vision_timeout_seconds: float = 240
    vision_keep_alive: int = 0  # nhả GPU ngay sau bước A để model bước B có chỗ (GPU 4GB)
    vision_cpu_fallback: bool = True  # Ollama lỗi 5xx (thường do thiếu VRAM) thì thử lại 1 lần trên CPU
    image_max_side: int = 2000
    # Để trống = dùng chung llm_model. Máy GPU yếu / server chỉ có CPU nên đặt qwen3:4b cho nhanh
    transcript_parse_model: str = ""
    transcript_parse_timeout_seconds: float = 180
    gemini_api_key: str = ""
    gemini_base_url: str = "https://generativelanguage.googleapis.com"
    gemini_model: str = "gemini-3.8-flash"
    gemini_thinking_level: str = "low"


# Lần gọi hàm sau nó lấy đối tượng đã lưu trong lru_cache
@lru_cache
def get_settings() -> Settings:
    """Chỉ đọc cấu hình 1 lần rồi dùng lại (giống đăng ký Singleton)."""
    return Settings()
