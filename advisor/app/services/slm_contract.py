"""Hợp đồng giữa bộ phân loại SLM (Dũng) và handler hồ sơ (Việt). Chỉ khai báo kiểu, không chứa logic.

Mỗi bên làm đúng chữ ký dưới đây thì ghép được mà không phải đọc code của nhau.
Cần đổi chữ ký thì báo người kia trước, rồi hai bên cùng sửa trong cùng một ngày.
"""

from typing import TYPE_CHECKING, Protocol

from app.schemas.chat import ChatState
from app.schemas.slm import SlmResult

if TYPE_CHECKING:  # chỉ để gõ kiểu, tránh nhập vòng với chat_flow
    from app.services.chat_flow import SkillOutput


# Đọc tin nhắn, trả loại câu hỏi, độ tin cậy, thông tin tách được
class SlmClassifier(Protocol):
    async def classify(self, state: ChatState, message: str) -> SlmResult: ...


# Mọi thứ liên quan hồ sơ (chat và form)
class ProfileHandler(Protocol):
    # Nhận tin nhắn khi slm.touches_profile() hoặc học sinh bấm nút, điền form: ghi giá trị, hỏi câu kế tiếp
    async def collect(self, state: ChatState, message: str, slm: SlmResult) -> "SkillOutput": ...

    # Câu hồ sơ đang chờ học sinh trả lời (để thêm "Quay lại câu mình đang hỏi..."), None nếu không có
    def pending_question(self, state: ChatState) -> str | None: ...

    # Đủ các trường bắt buộc trong `required`, học sinh đã xác nhận hồ sơ, không còn câu chờ
    def ready(self, state: ChatState, required: list[str]) -> bool: ...

    # Mã đổi mỗi khi hồ sơ đổi. Dũng dùng làm khóa để dùng lại skill và kết quả đã lưu
    def version(self, state: ChatState) -> str: ...


# SLM giả: luôn trả một kết quả cố định. Việt dùng để test phần hồ sơ khi chưa có Ollama hay bộ phân loại thật
class FixedSlm:
    def __init__(self, result: SlmResult) -> None:
        self._result = result

    async def classify(self, state: ChatState, message: str) -> SlmResult:
        return self._result
