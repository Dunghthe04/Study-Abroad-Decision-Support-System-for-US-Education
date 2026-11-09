"""Khuôn dữ liệu của lượt viết nhận xét so sánh trường: Claude diễn đạt lại số liệu code đã tra, không tính lại, không chọn thay học sinh."""

from typing import Any

from pydantic import BaseModel, Field

from app.schemas.chat import AiUsageMixin
from app.schemas.common import CamelModel

MAX_SCHOOLS = 4
MAX_POINTS = 5
MAX_OTHERS = 400


class CompareNoteRequest(CamelModel):
    schools: list[str] = Field(min_length=2, max_length=MAX_SCHOOLS)  # tên trường hệ thống đã hiểu
    # Số liệu do code tra, mỗi dòng một mục (giá trị từng trường, câu chênh lệch chi phí). Claude chỉ diễn đạt lại
    facts: list[str] = Field(min_length=1, max_length=24)
    others: list[str] = Field(
        default=[], max_length=MAX_OTHERS
    )  # tên các trường khác, để chặn nhắc tên ngoài danh sách


# Claude trả: một câu mở, vài điểm khác nhau, một câu giúp chọn theo ưu tiên
class CompareNoteResult(BaseModel):
    intro: str = Field(min_length=10, max_length=300)
    points: list[str] = Field(default=[], max_length=MAX_POINTS)
    closing: str = Field(default="", max_length=300)


# status OK thì answer là nhận xét hoàn chỉnh đã qua kiểm tra, FAILED thì backend chỉ hiện bảng số liệu
class CompareNoteResponse(AiUsageMixin):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    answer: str = ""


def compare_note_schema() -> dict[str, Any]:
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
