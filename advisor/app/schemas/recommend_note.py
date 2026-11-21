"""Khuôn dữ liệu của lượt viết lời cho danh sách gợi ý trường: Claude diễn đạt lại danh sách code đã xếp nhóm."""

from typing import Any

from pydantic import BaseModel, Field

from app.schemas.chat import AiUsageMixin
from app.schemas.common import CamelModel

MAX_SCHOOLS = 16  # 4 nhóm, mỗi nhóm hiện tối đa 4 trường
MAX_POINTS = 5
MAX_OTHERS = 400


class RecommendNoteRequest(CamelModel):
    schools: list[str] = Field(
        min_length=1, max_length=MAX_SCHOOLS
    )  # tên mọi trường đang hiện trong danh sách
    # Dữ kiện do code tính: tổng số trường, mỗi nhóm một dòng (tên nhóm, các trường hiện, số trường còn lại). Claude chỉ diễn đạt lại
    facts: list[str] = Field(min_length=1, max_length=12)
    others: list[str] = Field(
        default=[], max_length=MAX_OTHERS
    )  # tên các trường khác, để chặn nhắc tên ngoài danh sách


# Claude trả: một câu mở, mỗi nhóm một ý, một câu kết
class RecommendNoteResult(BaseModel):
    intro: str = Field(min_length=10, max_length=300)
    points: list[str] = Field(default=[], max_length=MAX_POINTS)
    closing: str = Field(default="", max_length=300)


# status OK thì answer là lời hoàn chỉnh đã qua kiểm tra, FAILED thì backend dùng câu liệt kê ghép bằng code
class RecommendNoteResponse(AiUsageMixin):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    answer: str = ""


def recommend_note_schema() -> dict[str, Any]:
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
