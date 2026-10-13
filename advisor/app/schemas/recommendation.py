# Định dạng dữ liệu giữa 2 bên
"""Hợp đồng dữ liệu .NET <-> advisor cho luồng gợi ý trường.

Phải khớp với backend/src/StudyAbroad.Application/Recommendations/IRecommendationAi.cs.
.NET gửi JSON camelCase (studyLevel, avgGpa4), Python dùng snake_case (study_level, avg_gpa4).
"""

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

from app.core.study_levels import StudyLevel

Category = Literal["reach", "match", "safety", "insufficient_data"]
EnglishStatus = Literal["met", "below_min", "no_score", "unknown"]
# Tiêu chí quyết định nhóm (CRM lấy mức thấp hơn của GPA và SAT): gpa, sat, hoặc cả hai cùng mức
CategoryBasis = Literal["gpa", "sat", "gpa_sat"]


class CamelModel(BaseModel):
    """Lớp cha: JSON dùng camelCase, code Python dùng snake_case."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class AiStudentInput(CamelModel):
    """Hồ sơ học sinh: chỉ có số liệu, không có tên/email/số điện thoại."""

    study_level: StudyLevel
    major: str | None = None
    gpa4: float | None = None
    sat: int | None = None
    annual_budget_usd: float | None = None
    ielts: float | None = None
    toefl: float | None = None
    duolingo: float | None = None
    extracurricular_score: float | None = None  # thang 0–4


class AiSchoolInput(CamelModel):
    """Một trường trong tập ứng viên CRM. Category do CRM quyết định, LLM không được đổi."""

    code: str = Field(min_length=1, max_length=32)
    name: str = Field(min_length=1, max_length=256)
    state: str | None = None
    category: Category
    avg_gpa4: float | None = None
    sat25: int | None = None
    sat75: int | None = None
    tuition_usd: float | None = None
    total_cost_usd: float | None = None
    english: EnglishStatus
    category_basis: CategoryBasis | None = None  # None = chưa đủ dữ liệu


class AiRankRequest(CamelModel):
    student: AiStudentInput
    schools: list[AiSchoolInput] = Field(min_length=1, max_length=50)


class AiPick(CamelModel):
    code: str
    reason: str


class AiRankResponse(CamelModel):
    items: list[AiPick]
