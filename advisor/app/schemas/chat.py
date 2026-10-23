"""Khuôn dữ liệu vào/ra của luồng chat (framework v5, Phần 4). .NET gửi trạng thái, advisor trả trạng thái mới."""

from typing import Any, Literal

from pydantic import Field

from app.core.study_levels import StudyLevel
from app.schemas.common import CamelModel

Step = Literal["B0", "B1", "B2", "B3", "B4", "B5", "B6", "END"]
FieldStatus = Literal["confirmed", "said_in_B0", "none", "unknown", "declined"]


# Một tin nhắn trong lịch sử chat
class ChatMessage(CamelModel):
    role: Literal["user", "assistant", "system"]
    content: str = Field(min_length=1, max_length=4000)


# Một thông tin hồ sơ + kèm status
class ProfileField(CamelModel):
    value: Any = None
    status: FieldStatus


# Trạng thái code giữ giữa các lượt (fw 4.3)
# Hệ thống lưu lại thông tin về tiến trình hội thoại để khi người dùng nhắn tin tiếp theo, AI biết mình đang làm đến đâu.
class ChatState(CamelModel):
    current_step: Step = "B0"
    need: str | None = None
    branch: str | None = None
    profile: dict[str, ProfileField] = {}
    last_question: str = ""  # câu hệ thống vừa hỏi, là đầu vào của bước chọn skill
    asked: dict[str, int] = {}  # mã câu hỏi → số lần đã hỏi (tối đa 2) ví dụ câu hỏi B0 hỏi tối đa 2
    return_to: Step | None = None  # bước dở cần quay lại sau B5, B6


# Nguồn trích dẫn, khớp record Citation bên .NET
class Citation(CamelModel):
    title: str
    url: str | None = None
    snippet: str | None = None


class ChatRequest(CamelModel):
    messages: list[ChatMessage] = Field(min_length=1, max_length=100)
    session_id: str | None = None
    study_level: StudyLevel | None = None
    state: ChatState | None = None  # None = cuộc trò chuyện mới


class ChatResponse(CamelModel):
    answer: str
    citations: list[Citation] = []
    disclaimer: str | None = None  # Lời lưu ý
    state: ChatState
    skills: list[str] = []  # skill đã chạy ở lượt này, để ghi log
