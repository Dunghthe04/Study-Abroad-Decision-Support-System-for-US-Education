"""Khuôn dữ liệu của lượt viết lời cho goal-seeking ("cần gì để vào trường X"): Claude diễn đạt lại kết quả code đã tính."""

from typing import Any, Literal

from pydantic import BaseModel, Field

from app.schemas.chat import AiUsageMixin
from app.schemas.common import CamelModel

Outcome = Literal["already_reached", "reachable", "blocked", "no_data"]
MAX_POINTS = 5
MAX_OTHERS = 400
MAX_ANSWER_CHARS = 900


class GoalNoteRequest(CamelModel):
    school: str = Field(
        min_length=1, max_length=256
    )  # tên trường hệ thống đã hiểu, học sinh sẽ thấy để sửa nếu nhầm
    target: str = Field(min_length=1, max_length=64)  # nhóm muốn đạt (tên tiếng Việt)
    outcome: Outcome
    # Dữ kiện do code tính, mỗi câu một ý (tài chính, học thuật, mức cần đạt, lý do bị chặn...). Claude chỉ diễn đạt lại
    facts: list[str] = Field(min_length=1, max_length=16)
    alternatives: list[str] = Field(default=[], max_length=5)  # trường tương tự đang ở nhóm đích với học sinh
    others: list[str] = Field(
        default=[], max_length=MAX_OTHERS
    )  # tên các trường khác, để chặn nhắc tên ngoài danh sách


# Claude trả: lời mở (nói lại trường và nhóm), vài ý chính, lời kết
class GoalNoteResult(BaseModel):
    intro: str = Field(min_length=10, max_length=300)
    points: list[str] = Field(default=[], max_length=MAX_POINTS)
    closing: str = Field(default="", max_length=300)


# status OK thì answer là tin nhắn hoàn chỉnh đã qua kiểm tra, FAILED thì backend dùng câu ghép bằng code
class GoalNoteResponse(AiUsageMixin):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    answer: str = ""


def goal_note_schema() -> dict[str, Any]:
    return {
        "type": "object",
        "required": ["intro", "points", "closing"],
        "additionalProperties": False,
        "properties": {
            "intro": {"type": "string", "minLength": 10},
            "points": {"type": "array", "items": {"type": "string"}, "maxItems": MAX_POINTS},
            "closing": {"type": "string"},
        },
    }
