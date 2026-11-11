"""Khuôn kết quả của lượt viết lời cho câu "nếu đổi X" khi X không thử được (mục lọc trường, mục không ảnh hưởng)."""

from typing import Any

from pydantic import BaseModel, Field

MAX_ANSWER = 500


class WhatIfNoteResult(BaseModel):
    answer: str = Field(min_length=10, max_length=MAX_ANSWER)


def what_if_note_schema() -> dict[str, Any]:
    return {
        "type": "object",
        "required": ["answer"],
        "additionalProperties": False,
        "properties": {"answer": {"type": "string", "minLength": 10}},
    }
