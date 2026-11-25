"""Khuôn kết quả của lượt viết lời giải thích cách tính điểm phù hợp (UC-153 1a)."""

from typing import Any

from pydantic import BaseModel, Field

MAX_ANSWER = 900


class FitMethodResult(BaseModel):
    answer: str = Field(min_length=30, max_length=MAX_ANSWER)


def fit_method_schema() -> dict[str, Any]:
    return {
        "type": "object",
        "required": ["answer"],
        "additionalProperties": False,
        "properties": {"answer": {"type": "string", "minLength": 30}},
    }
