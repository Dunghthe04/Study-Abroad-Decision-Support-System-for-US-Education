"""Hợp đồng đầu ra của SLM (Qwen3 4B) ở lần gọi 1: phân loại câu hỏi và tách thông tin.

Đây là chỗ nối giữa hai phần: bộ phân loại (Dũng) tạo ra SlmResult; handler hồ sơ (Việt) và các nhánh còn lại đọc nó.
Sửa file này thì báo người kia trước.
"""

from typing import Literal

from pydantic import BaseModel, Field

# Năm loại có cách xử lý riêng, cộng "ho_so": tin chỉ trả lời câu hồ sơ đang chờ (sơ đồ gọi là "loại rỗng")
INTENTS = ("tu_van", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi", "ho_so")
Intent = Literal["tu_van", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi", "ho_so"]


# Thông tin ngoài hồ sơ tách từ tin nhắn. Không nhắc tới thì để None
class SlmEntities(BaseModel):
    school: str | None = None  # tên trường học sinh hỏi tới (câu hỏi về một trường)
    service: str | None = None  # dịch vụ cần ở trung tâm (mã trong centers.SERVICES)
    city: str | None = None  # thành phố học sinh nói tới (tìm trung tâm)


# Định nghĩa cấu trúc dữ liệu mà SLM cần trả về, ép theo SCHEMA
class SlmRead(BaseModel):
    type: Intent
    # Xác định tin nhắn có chứa thông tin hồ sơ hay không.
    has_profile: bool = False
    # Chứa những thông tin cụ thể được trích xuất từ tin nhắn.
    entities: SlmEntities = Field(default_factory=SlmEntities)


# SlmRead cộng độ tin cậy (confidence). Độ tin cậy do code tính (từ xác suất của model), không để SLM tự khai
class SlmResult(SlmRead):
    confidence: float = Field(default=1.0, ge=0, le=1)

    # Hàm này kiểm tra xem tin nhắn có cần chuyển đến Handler hồ sơ hay không.
    def touches_profile(self) -> bool:
        return self.type == "ho_so" or self.has_profile


# Khuôn JSON bắt SLM trả đúng (truyền cho Ollama qua tham số format). Viết tay như các khuôn khác trong dự án
SLM_SCHEMA = {
    "type": "object",
    "required": ["type", "has_profile", "entities"],
    "properties": {
        "type": {"enum": list(INTENTS)},
        "has_profile": {"type": "boolean"},
        "entities": {
            "type": "object",
            "required": ["school", "service", "city"],
            "properties": {
                "school": {"type": ["string", "null"]},
                "service": {"type": ["string", "null"]},
                "city": {"type": ["string", "null"]},
            },
        },
    },
}
