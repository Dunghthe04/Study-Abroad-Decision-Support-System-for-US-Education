"""Khuôn dữ liệu vào/ra của lượt tư vấn từng trường: Qwen viết lời từ số đã lưu."""

from typing import Any, Literal

from pydantic import BaseModel, Field

from app.schemas.pick import WhyCode

MAX_POINTS = 3  # tối đa 3 ý


# Dữ liệu đầu vào sinh viên
class ExplainStudent(BaseModel):
    gpa: float | None = Field(default=None, ge=0, le=4)
    sat: int | None = Field(default=None, ge=400, le=1600)
    budget: float | None = None
    tier: int | None = Field(default=None, ge=1, le=5)  # bậc ngoại khóa
    major: str | None = Field(default=None, max_length=200)


# Dữ liệu 1 trường
class ExplainSchool(BaseModel):
    name: str = Field(min_length=1, max_length=256)
    state: str | None = None
    control: str | None = None  # public | private
    g2: Literal["R", "RF", "M", "S", "NA", "O"]
    blk: Literal["MAIN", "AID"] = "MAIN"
    why: list[WhyCode] = Field(default=[], max_length=11)  # mã lý do cloud đã chọn
    acc: float | None = None  # tỷ lệ nhận chung, 0–1
    sat25: int | None = None
    sat75: int | None = None
    pg: float | None = None  # GPA của học sinh cao hơn khoảng bao nhiêu % sinh viên trường
    ps: float | None = None
    cost: float | None = None
    gap: float | None = None  # USD còn thiếu khi vượt ngân sách


# Dữ liệu đầu vào để tư vấn
class ExplainRequest(BaseModel):
    student: ExplainStudent
    school: ExplainSchool


# Kết quả trả về từ Qwen: tóm tắt, điểm mạnh, điểm cần cải thiện
class ExplainResult(BaseModel):
    summary: str = Field(min_length=10)
    strengths: list[str] = Field(default=[], max_length=MAX_POINTS)
    improvements: list[str] = Field(default=[], max_length=MAX_POINTS)


# Kết quả trả về từ API: phiên bản, trạng thái, kết quả
class ExplainResponse(BaseModel):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    summary: str = ""
    strengths: list[str] = []
    improvements: list[str] = []


def explain_schema() -> dict[str, Any]:
    """Khuôn JSON ép Qwen trả: đoạn tóm tắt + 2 danh sách ngắn."""
    points = {"type": "array", "items": {"type": "string"}, "maxItems": MAX_POINTS}
    return {
        "type": "object",
        "required": ["summary", "strengths", "improvements"],
        "properties": {
            "summary": {"type": "string"},
            "strengths": points,
            "improvements": points,
        },
    }
