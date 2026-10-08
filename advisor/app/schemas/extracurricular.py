"""Hợp đồng dữ liệu .NET <-> advisor cho bước tính điểm ngoại khóa (thang 0–4)."""

from typing import Literal

from pydantic import Field

from app.schemas.recommendation import CamelModel
from app.services.extracurricular import Role

# activity = hoạt động, kinh nghiệm (tính bằng công thức q); award = giải thưởng (điểm thưởng theo cấp giải)
Kind = Literal["activity", "award"]


class ActivityInput(CamelModel):
    """Một hoạt động ngoại khóa học sinh khai trên form (không có tên, email của học sinh)."""

    id: str = Field(min_length=1, max_length=64)
    name: str = Field(min_length=1, max_length=200)
    role: str | None = Field(default=None, max_length=100)  # chữ học sinh nhập, vd "Chủ nhiệm CLB"
    organization: str | None = Field(default=None, max_length=200)
    description: str | None = Field(default=None, max_length=1000)
    # học sinh tự khai: 1 trường … 5 quốc tế; None = không khai, LLM đọc từ mô tả
    impact_level: int | None = Field(default=None, ge=1, le=5)
    months: int | None = Field(default=None, ge=0)
    kind: Kind = "activity"


class ExtracurricularRequest(CamelModel):
    activities: list[ActivityInput] = Field(default_factory=list, max_length=20)


class ScoredActivity(CamelModel):
    id: str
    role: Role
    reputable_org: bool
    impact_level: int  # sau khi LLM đối chiếu với mô tả (không cao hơn mức học sinh khai)
    quality: float
    points: float
    counted: bool
    kind: Kind = "activity"


class ExtracurricularResponse(CamelModel):
    score: float  # 0–4
    ai_used: bool  # False = LLM lỗi, thuộc tính đọc bằng từ khóa
    activities: list[ScoredActivity]
