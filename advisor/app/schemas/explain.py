"""Dữ liệu vào/ra của phần AI tư vấn từng trường (giải thích kết quả đã chấm bằng lời)."""

from typing import Any

from pydantic import BaseModel, Field

MAX_POINTS = 3  # tối đa 3 ý mỗi danh sách


class ExplainStudent(BaseModel):
    gpa: float | None = Field(default=None, ge=0, le=4)
    sat: int | None = Field(default=None, ge=400, le=1600)
    ielts: float | None = None
    toefl: float | None = None
    duolingo: float | None = None
    budget: float | None = None
    ec_tier: int | None = Field(default=None, ge=1, le=5)
    major: str | None = Field(default=None, max_length=200)


class ExplainSchool(BaseModel):
    name: str = Field(min_length=1, max_length=256)
    state: str | None = None
    control: str | None = None  # public | private
    group: str  # Khó vào | Khó vào xa | Vừa sức | An toàn hơn | Chưa đủ dữ liệu
    need_aid: bool = False
    acceptance: float | None = None  # %
    avg_gpa: float | None = None
    sat25: int | None = None
    sat75: int | None = None
    gpa_position: float | None = None
    sat_position: float | None = None
    cost: float | None = None
    over_budget: float | None = None
    ec_adjustment: float | None = None
    reasons: list[str] = Field(default=[], max_length=6)


class ExplainRequest(BaseModel):
    student: ExplainStudent
    school: ExplainSchool


class ExplainResult(BaseModel):
    summary: str = Field(min_length=10)
    strengths: list[str] = Field(default=[], max_length=MAX_POINTS)
    improvements: list[str] = Field(default=[], max_length=MAX_POINTS)


class ExplainResponse(ExplainResult):
    skill_version: str
    model: str


def explain_schema() -> dict[str, Any]:
    """JSON Schema cho format của Ollama: đoạn tóm tắt + 2 danh sách ngắn."""
    point = {"type": "string"}
    return {
        "type": "object",
        "required": ["summary", "strengths", "improvements"],
        "properties": {
            "summary": {"type": "string"},
            "strengths": {"type": "array", "items": point, "maxItems": MAX_POINTS},
            "improvements": {"type": "array", "items": point, "maxItems": MAX_POINTS},
        },
    }
