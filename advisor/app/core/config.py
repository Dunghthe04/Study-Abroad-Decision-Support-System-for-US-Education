"""Cấu hình đọc từ biến môi trường hoặc file .env (giống appsettings.json + IOptions bên .NET).
Thứ tự ưu tiên: biến môi trường (docker-compose) > file .env > giá trị mặc định dưới đây.
"""

# Dùng để lưu kết quả trả về của hàm nhằm tái sử dụng.
from functools import lru_cache
from pathlib import Path
from typing import Any

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
    # Thư mục ghi nhật ký skill SLM sinh ra (mỗi ngày một file skill_<ngày>.jsonl), để người làm đọc lại skill thật.
    # Để trống = không ghi. Đường dẫn tương đối tính từ thư mục advisor/
    skill_log_dir: str = ""
    ollama_base_url: str = "http://localhost:11434"
    slm_base_url: str = ""
    vision_base_url: str = ""
    embedding_base_url: str = ""
    embedding_api_key: str = ""
    # Chỉ dùng khi không có ANTHROPIC_API_KEY (bước B đọc bảng điểm chạy trên Ollama). Có khóa Claude thì để trống
    llm_model: str = ""
    slm_model: str = "usas-slm-v4"
    slm_format: str = "json"
    # Prompt tách ý (tên file trong app/skills, không có .md). slm_phan_loai_cac_y là bản đầy đủ (cho model chưa fine-tune);
    # slm_phan_loai_cac_y_ngan là bản ngắn, dùng cho model fine-tune với đúng bản này (xem scripts/export_slm_chat.py)
    slm_prompt: str = "slm_phan_loai_cac_y_ngan"
    # Đưa ngữ cảnh hội thoại (dang_noi_ve) vào đầu vào của SLM. Bật SAU khi model đã được huấn luyện với khóa này:
    # model chưa học thấy khóa lạ có thể đọc sai. Tắt thì chỉ chạy quy tắc dự phòng trong conversation_context
    slm_use_context: bool = False
    llm_timeout_seconds: float = 120
    # Kích thước context window dự kiến
    llm_num_ctx: int = 6144
    # Cấu hình số lớp model được đưa lên GPU
    llm_num_gpu: int = 99
    # cùng đầu vào cho cùng kết quả
    llm_seed: int = 42
    embedding_model: str = "bge-m3"
    database_url: str = ""
    # Địa chỉ backend .NET: B6 lấy trung tâm qua API của backend (luật chọn trung tâm nằm ở đó).
    backend_base_url: str = "http://localhost:5080"
    backend_timeout_seconds: float = 10
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
    anthropic_api_key: str = ""
    anthropic_base_url: str = "https://api.anthropic.com"
    anthropic_model: str = "claude-haiku-5-5"
    # Model dự phòng khi model chính quá tải (529, 5xx) hoặc treo. Để trống = không có dự phòng
    anthropic_fallback_model: str = ""
    # Thời gian chờ mỗi lần gọi và số lần thử mỗi model. 2 model x 2 lần x 60 giây vẫn dưới 300 giây .NET chờ
    anthropic_timeout_seconds: float = 60
    anthropic_max_retries: int = 2

    def model_post_init(self, __context: Any) -> None:
        if ADVISOR_ENV_FILE.is_file():
            from dotenv import dotenv_values
            env_vals = dotenv_values(ADVISOR_ENV_FILE)
            if not self.anthropic_api_key and env_vals.get("ANTHROPIC_API_KEY"):
                self.anthropic_api_key = env_vals["ANTHROPIC_API_KEY"]
            if not self.slm_base_url and env_vals.get("SLM_BASE_URL"):
                self.slm_base_url = env_vals["SLM_BASE_URL"]
            if not self.vision_base_url and env_vals.get("VISION_BASE_URL"):
                self.vision_base_url = env_vals["VISION_BASE_URL"]
            if not self.embedding_base_url and env_vals.get("EMBEDDING_BASE_URL"):
                self.embedding_base_url = env_vals["EMBEDDING_BASE_URL"]
            if not self.embedding_api_key and env_vals.get("EMBEDDING_API_KEY"):
                self.embedding_api_key = env_vals["EMBEDDING_API_KEY"]
            if not self.database_url and env_vals.get("DATABASE_URL"):
                self.database_url = env_vals["DATABASE_URL"]
            # Bỏ qua proxy cục bộ nếu biến trỏ về 127.0.0.1
            if "127.0.0.1" in self.anthropic_base_url or "localhost" in self.anthropic_base_url:
                self.anthropic_base_url = env_vals.get("ANTHROPIC_BASE_URL") or "https://api.anthropic.com"


# Lần gọi hàm sau nó lấy đối tượng đã lưu trong lru_cache
@lru_cache
def get_settings() -> Settings:
    """Chỉ đọc cấu hình 1 lần rồi dùng lại (giống đăng ký Singleton)."""
    return Settings()
