"""Khuôn dữ liệu của lượt viết lời cho kịch bản what-if: Gemini viết lời từ kết quả simulate đã được code tính."""

from typing import Any, Literal

from pydantic import BaseModel, Field

from app.schemas.chat import AiUsageMixin
from app.schemas.common import CamelModel

GroupKey = Literal["reach", "match", "safety", "aid"]
Direction = Literal["better", "worse", "mixed", "none"]
MAX_SCHOOLS_PER_GROUP = 4  # như trang gợi ý: mỗi nhóm tối đa 4 trường
MAX_OTHERS = 400


# Một trường được nêu trong nhóm. moved_from có nghĩa là trường vừa đổi nhóm so với hồ sơ thật (tên nhóm cũ, tiếng Việt)
class ScenarioSchool(CamelModel):
    name: str = Field(min_length=1, max_length=256)
    moved_from: str | None = None
    reasons: list[str] = Field(default=[], max_length=2)  # mã lý do, chỉ gắn cho trường đã đổi nhóm
    need_aid: bool = False  # vượt ngân sách, chỉ đi được nếu có học bổng lớn


# Một nhóm trong kết quả mô phỏng (đã chọn tối đa 4 trường), more = số trường còn lại của nhóm
class ScenarioGroup(CamelModel):
    key: GroupKey
    title: str = Field(min_length=1, max_length=64)
    schools: list[ScenarioSchool] = Field(min_length=1, max_length=MAX_SCHOOLS_PER_GROUP)
    more: int = Field(default=0, ge=0)


class ScenarioStudent(CamelModel):
    budget_usd: int | None = None  # ngân sách đang dùng trong kịch bản (USD mỗi năm)
    scholarship: str | None = None  # không cần | nên có | bắt buộc


class ScenarioNoteRequest(CamelModel):
    label: str = Field(
        min_length=1, max_length=300
    )  # điều đang thử kèm giá trị hiện tại, vd "SAT 1500 (hiện tại 1290)"
    factors: list[str] = Field(
        default=[], max_length=8
    )  # sat, gpa, act, ielts, toefl, duolingo, budget, scholarship
    unsupported: list[str] = Field(default=[], max_length=10)  # yếu tố học sinh nói nhưng chưa thử được
    direction: Direction  # better: có trường dễ vào hơn, worse: khó vào hơn, mixed: cả hai, none: không trường nào đổi nhóm
    student: ScenarioStudent = ScenarioStudent()
    groups: list[ScenarioGroup] = Field(default=[], max_length=4)  # rỗng khi direction = none
    others: list[str] = Field(
        default=[], max_length=MAX_OTHERS
    )  # tên các trường khác trong kết quả, để chặn nhắc tên ngoài danh sách


# Phần Gemini nối vào mỗi nhóm: tên trường (phải đúng danh sách của nhóm) và một ghi chú ngắn
class ScenarioNoteGroup(BaseModel):
    key: GroupKey
    schools: list[str] = Field(min_length=1, max_length=MAX_SCHOOLS_PER_GROUP)
    note: str = Field(default="", max_length=300)


class ScenarioNoteResult(BaseModel):
    intro: str = Field(min_length=10, max_length=300)
    groups: list[ScenarioNoteGroup] = Field(default=[], max_length=4)
    closing: str = Field(default="", max_length=300)


# Kết quả trả về: status OK thì answer là tin nhắn hoàn chỉnh đã qua kiểm tra, FAILED thì backend dùng câu mẫu bằng code
class ScenarioNoteResponse(AiUsageMixin):
    skill_version: str
    model: str
    status: str  # OK | FAILED
    answer: str = ""


def scenario_note_schema() -> dict[str, Any]:
    group = {
        "type": "object",
        "required": ["key", "schools", "note"],
        "additionalProperties": False,
        "properties": {
            "key": {"enum": ["reach", "match", "safety", "aid"]},
            "schools": {"type": "array", "items": {"type": "string"}, "minItems": 1},
            "note": {"type": "string"},
        },
    }
    return {
        "type": "object",
        "required": ["intro", "groups", "closing"],
        "additionalProperties": False,
        "properties": {
            "intro": {"type": "string", "minLength": 10},
            "groups": {"type": "array", "items": group},
            "closing": {"type": "string"},
        },
    }
