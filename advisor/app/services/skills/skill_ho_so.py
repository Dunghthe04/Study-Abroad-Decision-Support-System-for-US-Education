"""skill_ho_so (B2), phần đọc: biết đang chờ câu nào, Qwen đọc giá trị, code kiểm tra bằng validators.

Phần run(state, message) (chọn câu tiếp theo, ghi state.profile) làm ở B3. Ở đây chỉ có hàm đọc.
Giá trị sai (GPA 85, IELTS 6.3...) không được ghi; kết quả mang theo câu hỏi lại để B3 gửi cho học sinh.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

from pydantic import BaseModel, Field

from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skills.validators import (
    AskAgain,
    check_act,
    check_activities,
    check_awards,
    check_budget,
    check_english,
    check_gpa,
    check_sat,
    check_scholarship,
)

SKILL_PATH = SKILLS_DIR / "skill_ho_so.md"

# Câu hỏi → các thông tin câu đó cần (bảng 6.1, 6.2)
QUESTION_FIELDS = {
    "D1": ["education_system"],
    "D2": ["gpa"],
    "D3": ["sat", "act"],
    "E1": ["english"],
    "E2": ["english_plan"],
    "F1": ["budget"],
    "F2": ["scholarship"],
    "F3": ["funding_source"],
    "D4": ["activities"],
    "D5": ["awards"],
    "N1": ["relatives_state"],
    "N2": ["preferred_states"],
    "N3": ["school_type"],
    "N4": ["visa_refused"],
}
# Thông tin học sinh được nói "không có" (ghi status="none")
CAN_BE_NONE = ("sat", "act", "english", "activities", "awards", "relatives_state")

EDUCATION_SYSTEMS = ("vn_thpt", "ib", "a_level", "ap", "other")
FUNDING_SOURCES = ("parents", "relatives", "self", "loan")
SCHOOL_TYPES = ("public", "private", "any")


# Khuôn từng thông tin Qwen đọc ra. Chỉ kiểm tra dạng; khoảng điểm do validators kiểm tra
class GpaRead(BaseModel):
    value: float
    scale: int | None = None


class EnglishRead(BaseModel):
    type: str
    score: float
    ielts_kind: str | None = None
    date: str | None = Field(default=None, pattern=r"^\d{4}-\d{2}$")


class BudgetRead(BaseModel):
    amount: float
    currency: str


class ActivityRead(BaseModel):
    name: str
    role: str
    scope: int
    months: int


class AwardRead(BaseModel):
    title: str
    level: int


class HoSoValues(BaseModel):
    education_system: Literal[EDUCATION_SYSTEMS] | None = None
    gpa: GpaRead | None = None
    sat: float | None = None
    act: float | None = None
    english: EnglishRead | None = None
    english_plan: str | None = None
    budget: BudgetRead | None = None
    scholarship: str | None = None
    funding_source: Literal[FUNDING_SOURCES] | None = None
    activities: list[ActivityRead] | None = None
    awards: list[AwardRead] | None = None
    relatives_state: str | None = None
    preferred_states: list[str] | None = None
    school_type: Literal[SCHOOL_TYPES] | None = None
    visa_refused: bool | None = None


class HoSoRaw(BaseModel):
    values: HoSoValues
    none_fields: list[Literal[CAN_BE_NONE]] = []


# Khuôn JSON Ollama phải trả, sinh từ HoSoRaw ($defs nằm ở gốc nên các $ref đều tìm được)
SCHEMA = HoSoRaw.model_json_schema()


# Kết quả đọc: values đã qua validators, ask_again là câu hỏi lại nếu có giá trị sai
@dataclass
class HoSoRead:
    values: dict[str, Any] = field(default_factory=dict)
    none_fields: list[str] = field(default_factory=list)
    ask_again: str | None = None
    ai_used: bool = False


# Kiểm tra một thông tin bằng validators, trả về giá trị chuẩn để ghi vào hồ sơ. Sai thì ném AskAgain
def check_value(name: str, value: Any) -> Any:
    if name == "gpa":
        gpa, scale = check_gpa(value.value, value.scale)
        return {"value": gpa, "scale": scale}
    if name == "sat":
        return check_sat(value)
    if name == "act":
        return check_act(value)
    if name == "english":
        cert, score, kind = check_english(value.type, value.score, value.ielts_kind)
        return {"type": cert, "value": score, "kind": kind, "date": value.date}
    if name == "budget":
        amount, currency, tier = check_budget(value.amount, value.currency)
        return {"amount": amount, "currency": currency, "tier": tier}
    if name == "scholarship":
        return check_scholarship(value)
    if name == "activities":
        return check_activities([a.model_dump() for a in value])
    if name == "awards":
        return check_awards([a.model_dump() for a in value])
    if isinstance(value, str):
        return value.strip()
    return value


# Chạy validators cho mọi thông tin đọc được. Thông tin của câu đang hỏi kiểm tra trước,
# nên nếu có nhiều giá trị sai thì câu hỏi lại là của câu đang hỏi
def check_values(read: HoSoRaw, question_id: str | None) -> HoSoRead:
    found = read.values.model_dump(exclude_none=True)
    asked = QUESTION_FIELDS.get(question_id or "", [])
    order = [n for n in asked if n in found] + [n for n in found if n not in asked]

    result = HoSoRead(ai_used=True)
    for name in order:
        try:
            result.values[name] = check_value(name, getattr(read.values, name))
        except AskAgain as err:
            result.ask_again = result.ask_again or err.question
    # Một thông tin vừa có giá trị vừa "không có" thì tin giá trị
    result.none_fields = [n for n in dict.fromkeys(read.none_fields) if n not in found]
    return result


# Kiểm tra dạng JSON Qwen trả, sai thì ném ValueError để SkillCall gọi lại
def check_read(raw: dict[str, Any]) -> HoSoRaw:
    return HoSoRaw.model_validate(raw)


# Gọi Qwen đọc câu trả lời ở B2. Qwen lỗi 2 lần thì trả kết quả rỗng: B3 sẽ hỏi lại câu đó
class HoSoReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(
        self, question_id: str | None, message: str, known: dict[str, Any], branch: str | None = "DH"
    ) -> HoSoRead:
        user = {
            "branch": branch,
            "question_id": question_id,
            "fields": QUESTION_FIELDS.get(question_id or "", []),
            "message": message,
            "known": known,
        }
        raw = await self._call.run(user, SCHEMA, check_read)
        return check_values(raw, question_id) if raw is not None else HoSoRead()
