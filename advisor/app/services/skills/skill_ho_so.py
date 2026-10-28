"""skill_ho_so (B2): biết đang chờ câu nào, Qwen đọc giá trị, code kiểm tra bằng validators, ghi state.profile.

Giá trị sai (GPA 85, IELTS 6.3...) không được ghi; học sinh nhận câu hỏi lại.
Thứ tự hỏi: D2 → E1 → F1 (đủ REQUIRED_B3), rồi các câu tùy chọn, rồi X1 (rà soát). Học sinh bấm "Đúng" ở X1
thì mọi thông tin said_in_B0 thành confirmed và chuyển sang B3.
B4: hoạt động / giải thưởng thiếu ý (vai trò, phạm vi, tháng...) thì giữ phần đã nói và hỏi tiếp từng ý.
B5: câu hỏi có nút gợi ý (buttons.py); bấm nút thì ghi thẳng, không gọi Qwen.
"""

from collections.abc import Callable
from copy import deepcopy
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Literal, NamedTuple

from pydantic import BaseModel, Field

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import NEED_PROFILE, SkillOutput, ask, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import buttons
from app.services.skills.validators import (
    MAX_ACTIVITIES,
    MAX_AWARDS,
    SCHOOL_TYPES,
    AskAgain,
    check_act,
    check_activities,
    check_activity,
    check_award,
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


# B4. Danh sách hoạt động / giải thưởng: câu hỏi, khóa giữ các mục đang thiếu ý, hàm kiểm tra một mục,
# hàm kiểm tra cả danh sách (số mục), số mục tối đa
class ListField(NamedTuple):
    question: str
    pending_key: str
    check_item: Callable[..., dict]
    check_list: Callable[[list[dict]], list[dict]]
    limit: int


# Khóa *_pending không có trong nháp form (profile_sync.DRAFT_KEYS) nên chỉ nằm trong trạng thái chat
LIST_FIELDS = {
    "activities": ListField("D4", "activity_pending", check_activity, check_activities, MAX_ACTIVITIES),
    "awards": ListField("D5", "award_pending", check_award, check_awards, MAX_AWARDS),
}
PENDING_KEYS = tuple(spec.pending_key for spec in LIST_FIELDS.values())
# Một câu D4/D5 được hỏi tiếp tối đa ngần này lần (mỗi lần thường một ý: vai trò, phạm vi, tháng...)
MAX_FOLLOW_UPS = 4

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


# Kết quả đọc: values đã qua validators, ask_again là câu hỏi lại nếu có giá trị sai.
# pending: hoạt động / giải thưởng còn thiếu ý, giữ nguyên dạng Qwen đọc để hỏi tiếp (B4)
@dataclass
class HoSoRead:
    values: dict[str, Any] = field(default_factory=dict)
    none_fields: list[str] = field(default_factory=list)
    ask_again: str | None = None
    ai_used: bool = False
    pending: dict[str, list[dict]] = field(default_factory=dict)


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
    if name == "preferred_states":
        return check_states(value)
    if isinstance(value, str):
        return value.strip()
    return value


# B4. Kiểm tra từng mục: mục đủ ý thì chuẩn hóa theo dạng form, mục thiếu ý giữ nguyên để hỏi tiếp.
# question là câu hỏi ý còn thiếu của mục thiếu đầu tiên
def split_items(name: str, items: list[dict]) -> tuple[list[dict], list[dict], str | None]:
    done, waiting, question = [], [], None
    for item in items:
        try:
            done.append(LIST_FIELDS[name].check_item(**item))
        except AskAgain as err:
            waiting.append(item)
            question = question or err.question
    return done, waiting, question


# Hoạt động / giải thưởng: quá số mục thì hỏi lại; mục đủ ý vào values, mục thiếu ý vào pending
def check_items(result: HoSoRead, name: str, items: list[dict]) -> None:
    spec = LIST_FIELDS[name]
    if len(items) > spec.limit:
        spec.check_list(items)  # ném AskAgain "tối đa ... mục"
    done, waiting, question = split_items(name, items)
    if done:
        result.values[name] = done
    if waiting:
        result.pending[name] = waiting
        raise AskAgain(question)


# Chạy validators cho mọi thông tin đọc được. Thông tin của câu đang hỏi kiểm tra trước,
# nên nếu có nhiều giá trị sai thì câu hỏi lại là của câu đang hỏi
def check_values(read: HoSoRaw, question_id: str | None) -> HoSoRead:
    found = read.values.model_dump(exclude_none=True)
    asked = QUESTION_FIELDS.get(question_id or "", [])
    order = [n for n in asked if n in found] + [n for n in found if n not in asked]

    result = HoSoRead(ai_used=True)
    for name in order:
        try:
            if name in LIST_FIELDS:
                check_items(result, name, [item.model_dump() for item in getattr(read.values, name)])
            else:
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
# còn ở B2 học sinh sửa thì ghi đè. Hoạt động / giải thưởng thì thêm vào danh sách đang có (save_items)
def save_read(
    state: ChatState, read: HoSoRead, status: str = "confirmed", keep_existing: bool = False
) -> None:
    fields = [(name, value, status) for name, value in read.values.items() if name not in LIST_FIELDS]
    fields += [(name, None, "none") for name in read.none_fields]
    for name, value, field_status in fields:
        if keep_existing and name in state.profile:
            continue
        state.profile[name] = ProfileField(value=value, status=field_status)
    for name in LIST_FIELDS:
        if not (keep_existing and name in state.profile):
            save_items(state, name, read.values.get(name, []), read.pending.get(name, []), status)


def item_name(item: dict) -> str:
    return (item.get("name") or "").strip().lower()


# B4. Ghép ý học sinh vừa nói vào mục đang thiếu ý: mục cùng tên; mục chưa có tên; hoặc (học sinh
# không nhắc tên) mục thiếu đầu tiên. Mục có tên mới thì thêm vào cuối
def fill(pending: list[dict], items: list[dict]) -> list[dict]:
    merged = [dict(item) for item in pending]
    for item in items:
        name = item_name(item)
        if name:
            target = next((p for p in merged if item_name(p) == name), None)
            target = target or next((p for p in merged if not item_name(p)), None)
        else:
            target = merged[0] if merged else None
        if target is None:
            merged.append(dict(item))
        else:
            target.update(
                {key: value for key, value in item.items() if value is not None and value is not False}
            )
    return merged


# B4. Thêm mục đủ ý vào danh sách; mục chưa đủ ý (cả mục dở từ lượt trước) giữ ở khóa *_pending.
# Trùng tên thì mục mới thay mục cũ; quá số mục tối đa thì bỏ mục cũ nhất
def save_items(state: ChatState, name: str, done: list[dict], new_pending: list[dict], status: str) -> None:
    if not done and not new_pending:
        return
    spec = LIST_FIELDS[name]
    old_pending = state.profile.pop(spec.pending_key, None)
    more_done, waiting, _ = split_items(name, fill(old_pending.value if old_pending else [], new_pending))
    done = done + more_done
    done_names = {item_name(item) for item in done}
    waiting = [item for item in waiting if item_name(item) not in done_names]
    if done:
        old = state.profile.get(name)
        old_items = old.value if old is not None and isinstance(old.value, list) else []
        kept = [item for item in old_items if item_name(item) not in done_names]
        state.profile[name] = ProfileField(value=(kept + done)[-spec.limit :], status=status)
    if waiting:
        state.profile[spec.pending_key] = ProfileField(value=waiting, status="unknown")


# B4. Câu hỏi ý còn thiếu của mục dở đầu tiên của câu D4 hoặc D5. Không có mục dở thì None
def pending_question(state: ChatState, question_id: str) -> str | None:
    for name, spec in LIST_FIELDS.items():
        pending = state.profile.get(spec.pending_key)
        if spec.question == question_id and pending is not None:
            return split_items(name, pending.value[:1])[2]
    return None


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


# Bảng rà soát X1: widget review hiện toàn bộ hồ sơ (trừ mục dở *_pending), kèm hai nút Đúng / Cần sửa
def review(state: ChatState) -> SkillOutput:
    profile = {name: f for name, f in state.profile.items() if name not in PENDING_KEYS}
    data = {
        "profile": {name: {"value": f.value, "status": f.status} for name, f in profile.items()},
        "missing": missing_fields(state.profile, REQUIRED_B3),
    }
    return SkillOutput(
        ask(state, "X1"), quick_replies=[CONFIRM_REPLY, FIX_REPLY], widget=Widget(type="review", data=data)
    )


# Hỏi một câu B2 kèm nút; câu tùy chọn có thêm nút "Bỏ qua". text: câu khác câu chuẩn (câu hỏi lại)
def ask_question(state: ChatState, question_id: str, text: str | None = None) -> SkillOutput:
    return buttons.ask_with_buttons(state, question_id, text, can_skip=question_id in OPTIONAL_QUESTIONS)


# Hỏi câu tiếp theo của B2, hết câu thì sang rà soát. skill_bac_hoc gọi hàm này khi chuyển sang B2.
# D4/D5 mà đang có mục dở (nói ở B0) thì hỏi luôn ý còn thiếu thay cho câu chung
def ask_next(state: ChatState) -> SkillOutput:
    question_id = next_question(state)
    if question_id is None:
        return review(state)
    return ask_question(state, question_id, pending_question(state, question_id))


# B4. Lượt này có mục dở mới hoặc vừa được bổ sung thì hỏi tiếp ý còn thiếu.
# Học sinh trả lời câu hỏi tiếp mà không nói thêm gì cho mục đó, hoặc đã hỏi đủ lần, thì bỏ mục đó
def follow_up(state: ChatState, before: dict[str, Any]) -> SkillOutput | None:
    for spec in LIST_FIELDS.values():
        pending = state.profile.get(spec.pending_key)
        if pending is None:
            continue
        stuck = pending.value == before.get(spec.pending_key)
        if stuck and state.question_id != spec.question:
            continue  # mục dở từ trước (vd nói ở B0), chờ tới lúc hỏi câu của nó
        if stuck or state.asked.get(spec.question, 0) > MAX_FOLLOW_UPS:
            del state.profile[spec.pending_key]
            continue
        return ask_question(state, spec.question, pending_question(state, spec.question))
    return None


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
        output = buttons.ask_with_buttons(state, "B1.2")
    else:
        output = ask_question(state, FIELD_QUESTION[missing[0]])
    output.answer = f"{NEED_PROFILE} {output.answer}"
    return output


# B5. Học sinh bấm nút của câu đang hỏi: ghi thẳng giá trị của nút, bỏ mục dở của câu đó, hỏi câu tiếp
def press_button(state: ChatState, button: buttons.Button) -> SkillOutput:
    buttons.press(state, button)
    for spec in LIST_FIELDS.values():
        if spec.question == state.question_id:
            state.profile.pop(spec.pending_key, None)
    return ask_next(state)


class HoSoHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._reader = HoSoReader(llm, model)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        question_id = state.question_id
        if question_id == "X1" and is_confirm(message):
            return confirm(state)
        button = buttons.pressed(question_id, message, can_skip=question_id in OPTIONAL_QUESTIONS)
        if button is not None:
            return press_button(state, button)

        known = {name: f.value for name, f in state.profile.items() if f.value is not None}
        asked_id = question_id if question_id in QUESTION_FIELDS else None
        before = {key: deepcopy(state.profile[key].value) for key in PENDING_KEYS if key in state.profile}
        read = await self._reader.read(asked_id, message, known, state.branch)
        save_read(state, read)

        follow = follow_up(state, before)
        if follow is not None:
            return follow
        if question_id == "X1":
            # Học sinh sửa thông tin ở bảng rà soát: sai thì hỏi lại, đúng thì hiện lại bảng
            return SkillOutput(read.ask_again) if read.ask_again else review(state)
        # Câu bắt buộc chưa có câu trả lời (giá trị sai, hoặc học sinh không nói) thì hỏi lại, tối đa 2 lần.
        # Câu tùy chọn chỉ hỏi lại khi giá trị sai
        limit = MAX_ASK if asked_id in REQUIRED_QUESTIONS or read.ask_again else 1
        if asked_id and not answered(state, asked_id) and state.asked.get(asked_id, 0) < limit:
            return ask_question(state, asked_id, read.ask_again)
        return ask_next(state)
