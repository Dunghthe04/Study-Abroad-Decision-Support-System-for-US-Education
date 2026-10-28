"""skill_ho_so (B2): biết đang chờ câu nào, Qwen đọc giá trị, code kiểm tra bằng validators, ghi state.profile.

Giá trị sai (GPA 85, IELTS 6.3...) không được ghi; học sinh nhận câu hỏi lại.
Thứ tự hỏi: D2 → E1 → F1 (đủ REQUIRED_B3), rồi các câu tùy chọn, rồi X1 (rà soát). Học sinh bấm "Đúng" ở X1
thì mọi thông tin said_in_B0 thành confirmed và chuyển sang B3.
"""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal

from pydantic import BaseModel, Field

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import NEED_PROFILE, QUESTIONS, SkillOutput, ask, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills.validators import (
    SCHOOL_TYPES,
    AskAgain,
    check_act,
    check_activities,
    check_awards,
    check_budget,
    check_budget_tier,
    check_english,
    check_english_plan,
    check_gpa,
    check_sat,
    check_scholarship,
    check_states,
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

# Mỗi câu hỏi tối đa 2 lần (framework); câu tùy chọn chỉ hỏi 1 lần
MAX_ASK = 2
# Câu bắt buộc cho B3, theo thứ tự hỏi
REQUIRED_QUESTIONS = ("D2", "E1", "F1")
# Câu tùy chọn: F2 (học bổng) hỏi sớm vì B4 cần; E2 chỉ hỏi khi học sinh chưa có chứng chỉ tiếng Anh
OPTIONAL_QUESTIONS = ("F2", "E2", "D1", "D3", "D4", "D5", "F3", "N1", "N2", "N3", "N4")
# Thông tin bắt buộc → câu hỏi để hỏi lại khi học sinh xác nhận mà vẫn còn thiếu
FIELD_QUESTION = {"gpa": "D2", "english": "E1", "budget": "F1"}
# Những câu học sinh gõ (hoặc nút bấm gửi) để xác nhận hồ sơ ở X1
CONFIRM_WORDS = {
    "đúng",
    "đúng rồi",
    "dạ đúng",
    "dạ đúng rồi",
    "vâng",
    "ok",
    "oke",
    "xác nhận",
    "chuẩn",
    "yes",
}
CONFIRM_REPLY = QuickReply(label="Đúng rồi", value="Đúng rồi")
FIX_REPLY = QuickReply(label="Cần sửa", value="Em cần sửa thông tin")
SAVED = "Mình đã ghi nhận hồ sơ của bạn."

EDUCATION_SYSTEMS = ("vn_thpt", "ib", "a_level", "ap", "other")
FUNDING_SOURCES = ("parents", "relatives", "self", "loan")


# Khuôn từng thông tin Qwen đọc ra. Chỉ kiểm tra dạng; khoảng điểm do validators kiểm tra
class GpaRead(BaseModel):
    value: float
    scale: int | None = None


class EnglishRead(BaseModel):
    type: str
    score: float
    date: str | None = Field(default=None, pattern=r"^\d{4}-\d{2}$")


# Kế hoạch thi tiếng Anh: planned False là "chưa có kế hoạch"; có kế hoạch thì tháng có thể chưa biết
class EnglishPlanRead(BaseModel):
    planned: bool
    date: str | None = Field(default=None, pattern=r"^\d{4}-\d{2}$")


# Học sinh nói số tiền (amount, currency) hoặc bấm nút mức (tier). Chỉ mức tier được lưu
class BudgetRead(BaseModel):
    amount: float | None = None
    currency: str | None = None
    tier: str | None = None


# Một hoạt động như Bước 5 của form. Thiếu ý nào thì để null, validators sẽ hỏi lại đúng ý đó
class ActivityRead(BaseModel):
    name: str | None = None
    role: str | None = None
    impact_level: str | None = None
    start_date: str | None = None
    end_date: str | None = None
    is_ongoing: bool = False


class AwardRead(BaseModel):
    name: str | None = None
    impact_level: str | None = None
    year: int | None = None


class HoSoValues(BaseModel):
    education_system: Literal[EDUCATION_SYSTEMS] | None = None
    gpa: GpaRead | None = None
    sat: float | None = None
    act: float | None = None
    english: EnglishRead | None = None
    english_plan: EnglishPlanRead | None = None
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
        cert, score, test_date = check_english(value.type, value.score, value.date)
        return {"type": cert, "value": score, "date": test_date}
    if name == "english_plan":
        return check_english_plan(value.planned, value.date)
    if name == "budget":
        if value.tier is not None:
            return check_budget_tier(value.tier)
        if value.amount is None:
            raise AskAgain("Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm? Bạn cho mình một con số nhé.")
        return check_budget(value.amount, value.currency or "USD")
    if name == "scholarship":
        return check_scholarship(value)
    if name == "activities":
        return check_activities([a.model_dump() for a in value])
    if name == "awards":
        return check_awards([a.model_dump() for a in value])
    if name == "preferred_states":
        return check_states(value)
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


# Ghi kết quả đọc vào hồ sơ. keep_existing=True (thông tin nói ở B0) thì không đè thông tin đã có;
# còn ở B2 học sinh sửa thì ghi đè
def save_read(
    state: ChatState, read: HoSoRead, status: str = "confirmed", keep_existing: bool = False
) -> None:
    fields = [(name, value, status) for name, value in read.values.items()]
    fields += [(name, None, "none") for name in read.none_fields]
    for name, value, field_status in fields:
        if keep_existing and name in state.profile:
            continue
        state.profile[name] = ProfileField(value=value, status=field_status)


# Câu đã có câu trả lời chưa: thông tin của câu có trong hồ sơ, trạng thái nào cũng được (kể cả said_in_B0)
def answered(state: ChatState, question_id: str) -> bool:
    return any(name in state.profile for name in QUESTION_FIELDS[question_id])


# Chọn câu tiếp theo: câu bắt buộc còn thiếu (hỏi tối đa 2 lần), rồi câu tùy chọn chưa hỏi. Hết thì None
def next_question(state: ChatState) -> str | None:
    for question_id in REQUIRED_QUESTIONS:
        if not answered(state, question_id) and state.asked.get(question_id, 0) < MAX_ASK:
            return question_id
    english = state.profile.get("english")
    for question_id in OPTIONAL_QUESTIONS:
        if question_id == "E2" and (english is None or english.status != "none"):
            continue
        if not answered(state, question_id) and state.asked.get(question_id, 0) == 0:
            return question_id
    return None


# Bảng rà soát X1: widget review hiện toàn bộ hồ sơ, kèm hai nút Đúng / Cần sửa
def review(state: ChatState) -> SkillOutput:
    data = {
        "profile": {name: {"value": f.value, "status": f.status} for name, f in state.profile.items()},
        "missing": missing_fields(state.profile, REQUIRED_B3),
    }
    return SkillOutput(
        ask(state, "X1"), quick_replies=[CONFIRM_REPLY, FIX_REPLY], widget=Widget(type="review", data=data)
    )


# Hỏi câu tiếp theo của B2, hết câu thì sang rà soát. skill_bac_hoc gọi hàm này khi chuyển sang B2
def ask_next(state: ChatState) -> SkillOutput:
    question_id = next_question(state)
    return SkillOutput(ask(state, question_id)) if question_id else review(state)


def is_confirm(message: str) -> bool:
    return message.strip().strip(".!").lower() in CONFIRM_WORDS


# Học sinh xác nhận ở X1: said_in_B0 thành confirmed. Còn thiếu thông tin bắt buộc thì hỏi lại, chưa sang B3
def confirm(state: ChatState) -> SkillOutput:
    for profile_field in state.profile.values():
        if profile_field.status == "said_in_B0":
            profile_field.status = "confirmed"
    missing = missing_fields(state.profile, REQUIRED_B3)
    if not missing:
        go_to(state, "B3")
        return SkillOutput(f"{SAVED} {ask(state, 'X3')}")
    if missing[0] == "target_level":
        go_to(state, "B1")
        return SkillOutput(f"{NEED_PROFILE} {ask(state, 'B1.2')}")
    return SkillOutput(f"{NEED_PROFILE} {ask(state, FIELD_QUESTION[missing[0]])}")


class HoSoHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._reader = HoSoReader(llm, model)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        question_id = state.question_id
        if question_id == "X1" and is_confirm(message):
            return confirm(state)

        known = {name: f.value for name, f in state.profile.items() if f.value is not None}
        asked_id = question_id if question_id in QUESTION_FIELDS else None
        read = await self._reader.read(asked_id, message, known, state.branch)
        save_read(state, read)

        if question_id == "X1":
            # Học sinh sửa thông tin ở bảng rà soát: sai thì hỏi lại, đúng thì hiện lại bảng
            return SkillOutput(read.ask_again) if read.ask_again else review(state)
        # Câu bắt buộc chưa có câu trả lời (giá trị sai, hoặc học sinh không nói) thì hỏi lại, tối đa 2 lần.
        # Câu tùy chọn chỉ hỏi lại khi giá trị sai
        limit = MAX_ASK if asked_id in REQUIRED_QUESTIONS or read.ask_again else 1
        if asked_id and not answered(state, asked_id) and state.asked.get(asked_id, 0) < limit:
            ask(state, asked_id)
            return SkillOutput(read.ask_again or QUESTIONS[asked_id])
        return ask_next(state)
