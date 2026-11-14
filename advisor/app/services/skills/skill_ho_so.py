"""skill_ho_so (B2): biết đang chờ câu nào, Qwen đọc giá trị, code kiểm tra bằng validators, ghi state.profile.

Giá trị sai (GPA 85, IELTS 6.3...) không được ghi; học sinh nhận câu hỏi lại.
Thứ tự hỏi: D2 → E1 → F1 (đủ REQUIRED_B3), rồi các câu tùy chọn, rồi X1 (rà soát). Học sinh bấm "Đúng" ở X1
thì mọi thông tin said_in_B0 thành confirmed và chuyển sang B3.
B4: hoạt động / giải thưởng thiếu ý (vai trò, phạm vi, tháng...) thì giữ phần đã nói và hỏi tiếp từng ý.
B5: câu hỏi có nút gợi ý (buttons.py); bấm nút thì ghi thẳng, không gọi Qwen.
"""

import re
import uuid
from collections.abc import Callable
from copy import deepcopy
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path
from typing import Any, Literal, NamedTuple

from pydantic import BaseModel, Field

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import NEED_PROFILE, SkillOutput, ask, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import buttons, exchange_rate, state_catalog
from app.services.skills.validators import (
    CERT_NAMES,
    CERT_TYPES,
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
    check_certification,
    check_english,
    check_exam_plan,
    check_gpa,
    check_sat,
    check_scholarship,
    check_standardized_plan,
    check_states,
    parse_month_year_text,
    read_money,
)

SKILL_PATH = SKILLS_DIR / "skill_ho_so.md"

# Câu hỏi → các thông tin câu đó cần (bảng 6.1, 6.2)
QUESTION_FIELDS = {
    "D1": ["education_system"],
    "D2": ["gpa"],
    "D3": ["sat", "act"],
    "E1": ["english"],
    "E2": ["english_plan"],
    "E3": ["standardized_plan"],
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
# Câu bắt buộc cho B3, theo thứ tự hỏi (REQUIRED_B3 bên skill_rules). Câu bắt buộc không có nút "Bỏ qua":
# chưa có SAT/ACT thì bấm "Em chưa thi SAT/ACT", không cần học bổng thì chọn "Không cần"
REQUIRED_QUESTIONS = ("D2", "E1", "D3", "F1", "F2")
# Câu tùy chọn (có nút "Bỏ qua"): E2 chỉ hỏi khi học sinh chưa có chứng chỉ tiếng Anh;
# E3 chỉ hỏi khi học sinh chưa có bài thi chuẩn hóa (bấm "Chưa thi SAT/ACT" ở D3);
# D1 chỉ hỏi học sinh cấp 3; N1 chỉ hỏi khi chưa có bang muốn học (gợi ý bang theo người thân).
# N4 (visa) không hỏi ở B2: chỉ hỏi khi học sinh hỏi về visa / lộ trình. N1, N4 không lưu vào form
OPTIONAL_QUESTIONS = ("E2", "E3", "D1", "D4", "D5", "F3", "N1", "N2", "N3")
# Thứ tự hỏi theo trang form: hỏi xong các câu của một trang mới sang trang sau, không quay lại trang trước.
# Trang 1 mục tiêu (D1, N2, N3), 2 học bạ (D2), 3 tiếng Anh, SAT (E1, E2, D3, E3), 4 tài chính (F1-F3),
# 5 hoạt động, giải thưởng (D4, D5). Hết form mới hỏi thông tin thêm không có trong form (N1 người thân)
QUESTION_ORDER = ("D1", "N2", "N3", "D2", "E1", "E2", "D3", "E3", "F1", "F2", "F3", "D4", "D5", "N1")
# Thông tin bắt buộc → câu hỏi để hỏi lại khi học sinh xác nhận mà vẫn còn thiếu
FIELD_QUESTION = {"gpa": "D2", "english": "E1", "sat": "D3", "budget": "F1", "scholarship": "F2"}
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
# Bấm "Cần sửa" hoặc nói muốn sửa mà chưa nói sửa gì: hỏi lại mục cần sửa thay vì hiện lại bảng

# Ô chọn "sửa mục nào" khi bấm Cần sửa: tên nhóm → các câu hỏi của nhóm. Câu B1 viết thẳng mã vì skill_bac_hoc
# đã import file này. Tên nhóm không có dấu phẩy vì ô chọn nối các nhóm đã tích bằng ", "
EDIT_SECTIONS = {
    "Mục tiêu": ("B1.1", "B1.2", "B1.3", "B1.4"),
    "Học lực (GPA)": ("D1", "D2"),
    "Tiếng Anh": ("E1", "E2"),
    "SAT/ACT": ("D3", "E3"),
    "Tài chính": ("F1", "F2", "F3"),
    "Hoạt động và giải thưởng": ("D4", "D5"),
    "Nguyện vọng": ("N2", "N3", "N1"),
}
# Thứ tự hỏi khi sửa: câu bậc học rồi câu hồ sơ theo thứ tự form. Không có N4 (visa) vì chat không hỏi câu đó
EDIT_ORDER = ("B1.1", "B1.2", "B1.3", "B1.4", *QUESTION_ORDER)
EDIT_MENU = (
    "Bạn muốn sửa những mục nào? Bạn chọn một hoặc nhiều mục rồi bấm Gửi, mình sẽ hỏi lại lần lượt từng mục. "
    'Bạn cũng có thể nhắn thẳng mục và giá trị mới, ví dụ "sửa SAT thành 1450".'
)
EDIT_MENU_TEXT = re.compile(
    r"em muốn sửa\s+(.+)", re.IGNORECASE
)  # câu ô chọn ghép: "Em muốn sửa Tiếng Anh, Tài chính"
FORM_REPLY = QuickReply(label="Sửa trên form", value="Sửa trên form")
RESTART_REPLY = QuickReply(label="Điền lại từ đầu", value="Điền lại từ đầu")
KEEP_REPLY = QuickReply(label="Giữ như cũ", value="Giữ như cũ")
EDIT_ON_FORM = "Bạn bấm nút bên dưới để mở form hồ sơ và sửa trực tiếp nhé."
# Câu ô chọn E2 / E3 ghép: "Em dự kiến thi tháng 5/2027", "Em dự kiến thi IELTS, mục tiêu 7.5, tháng 10/2027"...
# Khớp thì ghi thẳng, không gọi Qwen. E2: tiếng Anh; E3: chuẩn hóa.
PLAN_TEXT = re.compile(r"em dự kiến thi,?\s*tháng (\d{1,2})/(\d{4})", re.IGNORECASE)


def read_plan_text(question_id: str | None, message: str) -> dict | None:
    if question_id not in ("E2", "E3"):
        return None
    text = message.strip().rstrip(".")
    m_prefix = re.match(r"^(?:em\s+|mình\s+)?dự kiến thi\b", text, re.IGNORECASE)
    if not m_prefix:
        return None
    rest = text[m_prefix.end() :].strip()

    date_str = None
    m_date = re.search(r"(?:,\s*)?tháng\s+(\d{1,2})/(\d{4})", rest, re.IGNORECASE)
    if m_date:
        month = int(m_date.group(1))
        year = int(m_date.group(2))
        date_str = f"{year}-{month:02d}"
        rest = rest[: m_date.start()] + rest[m_date.end() :]

    target_score = None
    m_score = re.search(r"(?:,\s*)?mục tiêu\s+(\d+(?:[.,]\d+)?)", rest, re.IGNORECASE)
    if m_score:
        target_score = float(m_score.group(1).replace(",", "."))
        rest = rest[: m_score.start()] + rest[m_score.end() :]

    categories = ("english",) if question_id == "E2" else ("standardized", "graduate")
    valid_types = {code: spec for code, spec in CERT_TYPES.items() if spec.category in categories}
    test_type = None
    clean_rest = re.sub(r"^[\s,]+|[\s,]+$", "", rest).strip().lower()
    if clean_rest:
        for code, spec in valid_types.items():
            if clean_rest == spec.name.lower() or clean_rest == code.lower():
                test_type = code
                break
        if test_type is None:
            for code, spec in sorted(valid_types.items(), key=lambda x: -len(x[1].name)):
                if spec.name.lower() in clean_rest or code.lower() in clean_rest:
                    test_type = code
                    break

    if date_str is None and test_type is None and target_score is None:
        return None

    return {
        "planned": True,
        "date": date_str,
        "test_type": test_type,
        "target_score": target_score,
    }


# english_plan / standardized_plan khi học sinh chưa thi và chưa nói tháng dự kiến (tích ô form nhưng chưa điền gì)
PLANNED_NO_DATE = {"planned": True, "date": None}
# Câu nút xác nhận thang GPA gửi (và ô chọn D2 ghép): "GPA 8.5 thang 10". Khớp thì ghi thẳng, không gọi Qwen
GPA_TEXT = re.compile(r"gpa\s+(\d+(?:[.,]\d+)?)\s+thang\s+(4|10)", re.IGNORECASE)
# Câu ô nhập D4 / D5 ghép (buttons.select_fields): "Hoạt động CLB Tiếng Anh, vai trò Thành viên, phạm vi Trường,
# từ 9/2024 đến 5/2025" (hoặc ", đang tham gia"), "Giải thưởng HSG Toán, phạm vi Tỉnh / Thành phố, năm 2025".
# Khớp thì ghi thẳng, không gọi Qwen
ACTIVITY_TEXT = re.compile(
    r"hoạt động (?P<name>.+?), vai trò (?P<role>.+?), phạm vi (?P<impact>.+?), từ (?P<start>\d{1,2}/\d{4})"
    r"(?:, (?P<ongoing>đang tham gia)| đến (?P<end>\d{1,2}/\d{4}))",
    re.IGNORECASE,
)
AWARD_TEXT = re.compile(
    r"giải thưởng (?P<name>.+?), phạm vi (?P<impact>.+?), năm (?P<year>\d{4})", re.IGNORECASE
)


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
# plan_overdue: thẻ dự định thi quá tháng dự kiến đang được hỏi "đã thi chưa", giá trị {"id": ...}
PENDING_KEYS = tuple(spec.pending_key for spec in LIST_FIELDS.values()) + ("cert_pending", "plan_overdue")
# Một câu D4/D5 được hỏi tiếp tối đa ngần này lần (mỗi lần thường một ý: vai trò, phạm vi, tháng...)
MAX_FOLLOW_UPS = 4
# Ghi xong một hoạt động / giải thưởng thì hỏi còn mục nào nữa không, kèm nút để sang câu sau
MORE_TEXT = {
    "D4": "Mình đã ghi {name}. Bạn còn hoạt động nào nữa không? Có thì nhập tiếp nhé.",
    "D5": "Mình đã ghi {name}. Bạn còn giải thưởng nào nữa không? Có thì nhập tiếp nhé.",
}
NO_MORE = {"D4": "Không còn hoạt động", "D5": "Không còn giải"}

# Câu ô nhập E1 / D3 ghép (buttons.select_fields): "Chứng chỉ IELTS Academic, điểm 6.5, thi tháng 5/2025";
# AP, A-Level có thêm ", môn Calculus BC" sau tên. Khớp thì ghi thẳng một thẻ chứng chỉ như form, không gọi Qwen
CERT_TEXT = re.compile(
    r"chứng chỉ (?P<name>.+?)(?:, môn (?P<subject>.+?))?, điểm (?P<score>[^,\s]+),"
    r" thi tháng (?P<month>\d{1,2})/(?P<year>\d{4})",
    re.IGNORECASE,
)
# Câu có ô nhập chứng chỉ → nhóm chứng chỉ của câu, giống hai mục của bước 3 form
CERT_QUESTIONS = {"E1": ("english",), "D3": ("standardized", "graduate")}
CERT_MORE = "Mình đã ghi {name}. Bạn còn chứng chỉ nào nữa không? Có thì nhập tiếp nhé."
NO_MORE_CERT = "Không còn chứng chỉ"
MAX_CERTS = 10
# Thông tin rút ra từ danh sách chứng chỉ (skill_rules, đánh giá dùng). Qwen / nút đổi chúng thì sửa danh sách
CERT_FIELDS = ("english", "sat", "act")
# Dự định thi: mỗi dự định là một thẻ isTarget trong certifications như form. english_plan / standardized_plan
# chỉ là bản tóm tắt dự định đầu tiên của mục (giống ChatProfileMapper.PlanSummary); planned False là
# "chưa thi và chưa có dự định thi" (ô tích của form)
PLAN_QUESTIONS = {
    "E2": ("english_plan", ("english",)),
    "E3": ("standardized_plan", ("standardized", "graduate")),
}
PLAN_MORE = "Mình đã ghi dự định thi {name}. Bạn còn dự định thi nào nữa không? Có thì chọn tiếp nhé."
NO_MORE_PLAN = "Không còn dự định"
# Dự định đã quá tháng dự kiến: hỏi đã thi chưa, kèm 3 nút
OVERDUE_TAKEN = "Đã thi rồi"
OVERDUE_MOVE = "Chưa, đổi tháng"
OVERDUE_DROP = "Bỏ dự định"
OVERDUE_REPLIES = (OVERDUE_TAKEN, OVERDUE_MOVE, OVERDUE_DROP)

# Mã giống form và DB (EducationSystems, ProfileConstants.FundingSources) để lưu được vào hồ sơ
EDUCATION_SYSTEMS = ("standard", "specialized", "dual_degree", "international", "private", "other")
FUNDING_SOURCES = ("family_support", "personal_savings", "bank_loan", "scholarship", "other")


# Khuôn từng thông tin Qwen đọc ra. Chỉ kiểm tra dạng; khoảng điểm do validators kiểm tra
class GpaRead(BaseModel):
    value: float
    scale: int | None = None


class EnglishRead(BaseModel):
    type: str
    score: float
    date: str | None = Field(default=None, pattern=r"^\d{4}-\d{2}$")


# Kế hoạch thi tiếng Anh: planned False là "chưa có kế hoạch"; có kế hoạch thì tháng / bài thi / điểm mục tiêu có thể chưa biết
class EnglishPlanRead(BaseModel):
    planned: bool
    date: str | None = Field(default=None, pattern=r"^\d{4}-\d{2}$")
    test_type: str | None = None
    target_score: float | None = None


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
    standardized_plan: EnglishPlanRead | None = None  # dùng chung khuôn, kiểm tra nhóm khác
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
    pending_cert: dict | None = None
    # Học sinh không nói thang GPA: thang trong values là đoán. Ở B2 thì hỏi lại thang thay vì ghi
    gpa_guessed: bool = False


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
        return check_exam_plan(
            value.planned,
            value.date,
            value.test_type,
            value.target_score,
            allowed_categories=("english",),
        )
    if name == "standardized_plan":
        return check_standardized_plan(value.planned, value.date, value.test_type, value.target_score)
    if name == "budget":
        # Có số tiền thì tự quy ra mức, không tin mức Qwen đoán
        if value.amount is not None:
            return check_budget(value.amount, value.currency)
        if value.tier is not None:
            return check_budget_tier(value.tier)
        raise AskAgain("Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm? Bạn cho mình một con số nhé.")
    if name == "scholarship":
        return check_scholarship(value)
    if name == "preferred_states":
        # Đổi tên bang sang mã giống form (California → CA)
        return state_catalog.to_codes(check_states(value), state_catalog.loaded())
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
def check_values(read: HoSoRaw, question_id: str | None, message: str = "") -> HoSoRead:
    found = read.values.model_dump(exclude_none=True)
    asked = QUESTION_FIELDS.get(question_id or "", [])
    order = [n for n in asked if n in found] + [n for n in found if n not in asked]

    result = HoSoRead(ai_used=True)

    # Yêu cầu ngày tháng thi cho chứng chỉ tiếng Anh (E1 hoặc khi học sinh chat điểm)
    if read.values.english:
        eng = read.values.english
        eng_date = eng.date or parse_month_year_text(message)
        if eng_date:
            eng.date = eng_date
        elif question_id is not None:
            c_name = CERT_NAMES.get(eng.type, eng.type.upper())
            result.pending_cert = {
                "type": eng.type,
                "score": eng.score,
                "category": "english",
                "cert_name": c_name,
            }
            result.ask_again = f"Bạn thi chứng chỉ {c_name} ({eng.score}) vào tháng mấy, năm nào (ví dụ: tháng 5/2025 hoặc 10/2024)?"
            read.values.english = None
            if "english" in found:
                found.pop("english")
            if "english" in order:
                order.remove("english")

    # Yêu cầu ngày tháng thi cho bài thi chuẩn hóa SAT / ACT
    if read.values.sat is not None:
        sat_date = parse_month_year_text(message)
        if sat_date is None and question_id is not None:
            result.pending_cert = {
                "type": "sat",
                "score": read.values.sat,
                "category": "standardized",
                "cert_name": "SAT",
            }
            result.ask_again = f"Bạn thi SAT ({read.values.sat:g}) vào tháng mấy, năm nào (ví dụ: tháng 12/2024 hoặc 03/2025)?"
            read.values.sat = None
            if "sat" in found:
                found.pop("sat")
            if "sat" in order:
                order.remove("sat")

    if read.values.act is not None:
        act_date = parse_month_year_text(message)
        if act_date is None and question_id is not None:
            result.pending_cert = {
                "type": "act",
                "score": read.values.act,
                "category": "standardized",
                "cert_name": "ACT",
            }
            result.ask_again = f"Bạn thi ACT ({read.values.act:g}) vào tháng mấy, năm nào (ví dụ: tháng 10/2024 hoặc 04/2025)?"
            read.values.act = None
            if "act" in found:
                found.pop("act")
            if "act" in order:
                order.remove("act")

    for name in order:
        try:
            if name in LIST_FIELDS:
                check_items(result, name, [item.model_dump() for item in getattr(read.values, name)])
            else:
                result.values[name] = check_value(name, getattr(read.values, name))
                result.gpa_guessed = result.gpa_guessed or (name == "gpa" and read.values.gpa.scale is None)
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
        # Đọc sẵn danh sách bang để check_value đổi tên bang sang mã
        await state_catalog.states()
        return check_values(raw, question_id, message) if raw is not None else HoSoRead()


# Ghi kết quả đọc vào hồ sơ. keep_existing=True (thông tin nói ở B0) thì không đè thông tin đã có;
# còn ở B2 học sinh sửa thì ghi đè. Hoạt động / giải thưởng thì thêm vào danh sách đang có (save_items)
def save_read(
    state: ChatState,
    read: HoSoRead,
    status: str = "confirmed",
    keep_existing: bool = False,
    message: str = "",
) -> None:
    if read.pending_cert:
        state.profile["cert_pending"] = ProfileField(value=read.pending_cert, status="unknown")
    fields = [(name, value, status) for name, value in read.values.items() if name not in LIST_FIELDS]
    fields += [(name, None, "none") for name in read.none_fields]
    written = []
    for name, value, field_status in fields:
        if keep_existing and name in state.profile:
            continue
        state.profile[name] = ProfileField(value=value, status=field_status)
        written.append(name)
    sat_act_date = parse_month_year_text(message)
    sat_act_dates = {"sat": sat_act_date, "act": sat_act_date} if sat_act_date else None
    certs_from_fields(state, written, status, sat_act_dates)
    certs_from_plans(state, written, status)
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


# Ý còn thiếu lấy mức thấp nhất (TH-22): thành viên, cấp trường, 0 tháng (kết thúc ngay tháng bắt đầu),
# năm đạt giải là năm nay
def lowest(name: str, item: dict, today: date | None = None) -> dict:
    now = (today or date.today()).strftime("%m/%Y")
    if name == "awards":
        defaults = {"impact_level": "school", "year": int(now[-4:])}
    else:
        start = item.get("start_date") or now
        defaults = {"role": "member", "impact_level": "school", "start_date": start, "end_date": start}
        if item.get("is_ongoing"):
            defaults.pop("end_date")
    return {**item, **{key: value for key, value in defaults.items() if not item.get(key)}}


# Mục dở không hỏi tiếp được nữa: điền ý còn thiếu bằng mức thấp nhất rồi ghi; mục chưa có tên thì bỏ
def save_lowest(state: ChatState, name: str) -> None:
    spec = LIST_FIELDS[name]
    pending = state.profile.pop(spec.pending_key)
    done, _, _ = split_items(name, [lowest(name, item) for item in pending.value])
    save_items(state, name, done, [], "confirmed")


# B4. Câu hỏi ý còn thiếu của mục dở đầu tiên của câu D4 hoặc D5. Không có mục dở thì None
def pending_question(state: ChatState, question_id: str) -> str | None:
    for name, spec in LIST_FIELDS.items():
        pending = state.profile.get(spec.pending_key)
        if spec.question == question_id and pending is not None:
            return split_items(name, pending.value[:1])[2]
    return None


# Câu đã có câu trả lời chưa: thông tin của câu có trong hồ sơ, trạng thái nào cũng được (kể cả said_in_B0)
def answered(state: ChatState, question_id: str) -> bool:
    if question_id == "D3" and any(c.get("category") != "english" for c in saved_certs(state)):
        return True  # đã có AP, IB, GRE... dù chưa có SAT / ACT
    if question_id == "E2":
        # Ô "sẽ thi sau" đã tích nhưng chưa có bài thi / tháng dự kiến thì vẫn hỏi để bổ sung
        plan = state.profile.get("english_plan")
        return plan_cards(state, "E2") != [] or (plan is not None and plan.value != PLANNED_NO_DATE)
    if question_id == "E3":
        # Tương tự: ô thi chuẩn hóa đã tích nhưng chưa có bài thi / tháng dự kiến
        plan = state.profile.get("standardized_plan")
        return plan_cards(state, "E3") != [] or (plan is not None and plan.value != PLANNED_NO_DATE)
    return any(name in state.profile for name in QUESTION_FIELDS[question_id])


# Câu tùy chọn này có cần hỏi học sinh này không (E2, D1, N1 chỉ hỏi khi cần)
def needed(state: ChatState, question_id: str) -> bool:
    if question_id == "E2":
        english = state.profile.get("english")
        return english is not None and english.status == "none"
    if question_id == "E3":
        # Hỏi khi học sinh đã tích ô "Chưa thi SAT/ACT" (sat và act đều none)
        sat = state.profile.get("sat")
        act = state.profile.get("act")
        return sat is not None and sat.status == "none" and act is not None and act.status == "none"
    if question_id == "D1":
        level = state.profile.get("current_level")
        return level is not None and level.value == "high_school"
    if question_id == "N1":
        return "preferred_states" not in state.profile
    return True


# Hồ sơ đã có từ trước: các mục bắt buộc còn thiếu của việc đang làm. Hồ sơ trống lúc bắt đầu thì None (hỏi đủ)
def required_missing(state: ChatState) -> set[str] | None:
    if state.required_only is None:
        return None
    return set(missing_fields(state.profile, state.required_only))


# Chọn câu tiếp theo theo thứ tự trang form: câu bắt buộc còn thiếu (hỏi tối đa 2 lần),
# câu tùy chọn cần hỏi mà chưa hỏi (1 lần). Hồ sơ đã có từ trước thì chỉ hỏi câu của mục bắt buộc còn thiếu.
# Hết thì None
def next_question(state: ChatState) -> str | None:
    missing = required_missing(state)
    for question_id in QUESTION_ORDER:
        if answered(state, question_id):
            continue
        if missing is not None:
            if missing & set(QUESTION_FIELDS[question_id]) and state.asked.get(question_id, 0) < MAX_ASK:
                return question_id
            continue
        if question_id in REQUIRED_QUESTIONS:
            if state.asked.get(question_id, 0) < MAX_ASK:
                return question_id
        elif needed(state, question_id) and state.asked.get(question_id, 0) == 0:
            return question_id
    return None


# Đã hiện bảng rà soát X1, hoặc học sinh đang sửa mục hồ sơ (edit_queue khác None, kể cả hồ sơ điền bằng form chưa
# từng thấy bảng trong chat): trả lời xong câu của mục cần sửa thì quay về bảng, không hỏi tiếp các câu khác
def reviewed(state: ChatState) -> bool:
    return state.asked.get("X1", 0) > 0 or state.edit_queue is not None


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


# Các nhóm học sinh đã tích trong ô chọn sửa mục. Có tên không phải nhóm (học sinh tự gõ) thì None để SLM đọc
def read_edit_menu(message: str) -> list[str] | None:
    match = EDIT_MENU_TEXT.fullmatch(message.strip())
    if match is None:
        return None
    sections = []
    for part in match.group(1).split(","):
        name = next((name for name in EDIT_SECTIONS if buttons.same_text(name, part)), None)
        if name is None:
            return None
        sections.append(name)
    return sections


# Câu hỏi của các nhóm đã chọn, theo thứ tự hỏi khi sửa
def edit_questions(sections: list[str]) -> list[str]:
    wanted = {question_id for name in sections for question_id in EDIT_SECTIONS[name]}
    return [question_id for question_id in EDIT_ORDER if question_id in wanted]


# Ô chọn nhóm cần sửa (chọn được nhiều nhóm) kèm 3 nút: sửa trên form, điền lại từ đầu, hồ sơ đã đúng.
# Vẫn ở câu X1 để bấm "Đúng rồi" là xác nhận hồ sơ như ở bảng rà soát
def edit_menu(state: ChatState) -> SkillOutput:
    ask(state, "X1")
    field = {
        "name": "sections",
        "multiple": True,
        "label": "Mục cần sửa",
        "placeholder": "Chọn mục",
        "options": buttons.same_options(list(EDIT_SECTIONS)),
    }
    widget = Widget(type="select", data={"fields": [field], "format": "Em muốn sửa {sections}"})
    return SkillOutput(EDIT_MENU, quick_replies=[FORM_REPLY, RESTART_REPLY, CONFIRM_REPLY], widget=widget)


# Hỏi một câu B2 kèm nút; câu tùy chọn có thêm nút "Bỏ qua". text: câu khác câu chuẩn (câu hỏi lại)
def ask_question(state: ChatState, question_id: str, text: str | None = None) -> SkillOutput:
    return buttons.ask_with_buttons(state, question_id, text, can_skip=question_id in OPTIONAL_QUESTIONS)


# Học sinh nói chưa thi tiếng Anh thì tích ô "sẽ thi sau" giống form (chưa có tháng dự kiến)
def plan_english(state: ChatState) -> None:
    english = state.profile.get("english")
    if english is not None and english.status == "none" and "english_plan" not in state.profile:
        state.profile["english_plan"] = ProfileField(value=PLANNED_NO_DATE, status="confirmed")


# Học sinh bấm "Chưa thi SAT/ACT" thì tích ô "sẽ thi sau" chuẩn hóa giống form
def plan_standardized(state: ChatState) -> None:
    sat = state.profile.get("sat")
    act = state.profile.get("act")
    if (
        sat is not None
        and sat.status == "none"
        and act is not None
        and act.status == "none"
        and "standardized_plan" not in state.profile
    ):
        state.profile["standardized_plan"] = ProfileField(value=PLANNED_NO_DATE, status="confirmed")


# Hỏi câu tiếp theo của B2, hết câu thì sang rà soát. skill_bac_hoc gọi hàm này khi chuyển sang B2.
# D4/D5 mà đang có mục dở (nói ở B0) thì hỏi luôn ý còn thiếu thay cho câu chung
def ask_next(state: ChatState) -> SkillOutput:
    plan_english(state)
    plan_standardized(state)
    overdue = overdue_card(state)
    if overdue is not None:
        return ask_overdue(state, overdue)
    if reviewed(state):
        return review(state)
    question_id = next_question(state)
    if question_id is None:
        return review(state)
    return ask_question(state, question_id, pending_question(state, question_id))


# B4. Lượt này có mục dở mới hoặc vừa được bổ sung thì hỏi tiếp ý còn thiếu.
# Học sinh trả lời câu hỏi tiếp mà không nói thêm gì cho mục đó, hoặc đã hỏi đủ lần, thì ý còn thiếu
# lấy mức thấp nhất (TH-22)
def follow_up(state: ChatState, before: dict[str, Any]) -> SkillOutput | None:
    for name, spec in LIST_FIELDS.items():
        pending = state.profile.get(spec.pending_key)
        if pending is None:
            continue
        stuck = pending.value == before.get(spec.pending_key)
        if stuck and state.question_id != spec.question:
            continue  # mục dở từ trước (vd nói ở B0), chờ tới lúc hỏi câu của nó
        if stuck or state.asked.get(spec.question, 0) > MAX_FOLLOW_UPS:
            save_lowest(state, name)
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
        # Sang B3, chat_flow chạy skill_danh_gia ngay trong lượt này
        go_to(state, "B3")
        return SkillOutput(SAVED)
    if missing[0] == "target_level":
        go_to(state, "B1")
        output = buttons.ask_with_buttons(state, "B1.2")
    else:
        output = ask_question(state, FIELD_QUESTION[missing[0]])
    output.answer = f"{NEED_PROFILE} {output.answer}"
    return output


# B5. Học sinh bấm nút của câu đang hỏi: ghi thẳng giá trị của nút, bỏ mục dở của câu đó, hỏi câu tiếp
# "GPA 8.5 thang 10" → (8.5, 10); câu khác thì None
def read_gpa_text(message: str) -> tuple[float, int] | None:
    match = GPA_TEXT.fullmatch(message.strip().rstrip("."))
    return (float(match[1].replace(",", ".")), int(match[2])) if match else None


# Học sinh nói GPA mà không nói thang: đoán thang rồi hỏi lại kèm nút. GPA <= 4 thì có cả thang 4 và thang 10
def ask_gpa_scale(state: ChatState, gpa: float) -> SkillOutput:
    ask(state, "D2")
    number = f"{gpa:g}"
    if gpa > 4:
        replies = [QuickReply(label="Đúng, thang 10", value=f"GPA {number} thang 10")]
        return SkillOutput(f"GPA {number} của bạn là trên thang 10 đúng không?", quick_replies=replies)
    replies = [
        QuickReply(label="Đúng, thang 4", value=f"GPA {number} thang 4"),
        QuickReply(label="Không, thang 10", value=f"GPA {number} thang 10"),
    ]
    return SkillOutput(f"GPA {number} của bạn là trên thang 4 đúng không?", quick_replies=replies)


# Học sinh nói chứng chỉ mà thiếu ngày thi: hỏi lại tháng năm kèm widget tháng/năm thi và nút không nhớ
def ask_cert_date(state: ChatState, pending: dict, question_text: str | None = None) -> SkillOutput:
    c_name = pending.get("cert_name") or CERT_NAMES.get(pending["type"], pending["type"].upper())
    score_val = pending.get("score")
    score_str = (
        f" ({score_val:g})"
        if isinstance(score_val, (int, float))
        else (f" ({score_val})" if score_val else "")
    )
    text = (
        question_text
        or f"Bạn thi chứng chỉ {c_name}{score_str} vào tháng mấy, năm nào (ví dụ: tháng 5/2025 hoặc 10/2024)?"
    )
    return SkillOutput(
        text,
        quick_replies=[QuickReply(label="Không nhớ ngày thi", value="Không nhớ")],
        widget=Widget(type="select", data=buttons.cert_date_widget()),
    )


# Ghi GPA học sinh đã nói rõ thang (bấm nút xác nhận hoặc chọn ở ô chọn), không gọi Qwen
def save_gpa(state: ChatState, gpa: float, scale: int) -> SkillOutput:
    try:
        value, scale = check_gpa(gpa, scale)
    except AskAgain as err:
        return ask_question(state, "D2", err.question)

    # Kế hoạch đồng bộ 3.1: Nếu GPA đang tính từ học bạ chi tiết (source = transcript)
    # thì không tự ý ghi đè GPA tổng, mà nhắc học sinh sửa ở bảng điểm Bước 2
    old_gpa = state.profile.get("gpa")
    if old_gpa and isinstance(old_gpa.value, dict) and old_gpa.value.get("source") == "transcript":
        out = review(state) if state.question_id == "X1" else ask_next(state)
        out.answer = (
            f"Điểm GPA của em hiện đang được tính từ học bạ chi tiết ở Bước 2. "
            f"Nếu muốn thay đổi, em hãy mở Bước 2 trên form để cập nhật điểm các môn nhé!\n\n{out.answer}"
        )
        return out

    state.profile["gpa"] = ProfileField(value={"value": value, "scale": scale}, status="confirmed")
    out = review(state) if state.question_id == "X1" else ask_next(state)
    if buttons.is_middle_school_profile(state.profile):
        out.answer = (
            f"Mình đã ghi nhận mục tiêu GPA {value:g} (thang {scale}) của bạn. "
            f"Nếu muốn lập lộ trình điểm chi tiết cho từng năm từ Lớp 9 đến Lớp 12 để theo dõi xu hướng tiến bộ (Grade Trend), "
            f"bạn có thể mở Bước 2 trên form để chia điểm nhé!\n\n{out.answer}"
        )
    return out


# Ghi mức ngân sách từ câu số tiền ở F1 ("800 triệu", "36.000 USD"), không gọi Qwen
def save_budget(state: ChatState, amount: float, currency: str | None) -> SkillOutput:
    try:
        tier = check_budget(amount, currency)
    except AskAgain as err:
        return ask_question(state, "F1", err.question)
    state.profile["budget"] = ProfileField(value=tier, status="confirmed")
    return ask_next(state)


# Ghi một dự định thi học sinh chọn ở ô chọn E2 / E3 (bài thi, điểm mục tiêu, tháng), không gọi Qwen.
# Dự định thành thẻ mục tiêu như form: cùng bài thì sửa thẻ dự định cũ, không thì thêm thẻ. Ghi xong hỏi còn
# dự định nào nữa không, nút "Không còn dự định" để sang câu sau
def save_plan(state: ChatState, question_id: str, plan_data: dict) -> SkillOutput:
    categories = PLAN_QUESTIONS[question_id][1]
    try:
        if not plan_data.get("test_type"):
            raise AskAgain("Bạn định thi bài nào? Bạn chọn bài thi trong ô giúp mình nhé.")
        plan = check_exam_plan(
            True,
            plan_data.get("date"),
            plan_data.get("test_type"),
            plan_data.get("target_score"),
            allowed_categories=categories,
        )
        check_target_above_taken(state, plan)
    except AskAgain as err:
        return ask_question(state, question_id, err.question)
    moved = state.profile.pop("plan_overdue", None) is not None
    put_plan(state, plan, "confirmed")
    name = CERT_NAMES.get(plan["test_type"], plan["test_type"].upper())
    if moved:
        out = ask_next(state)
        out.answer = f"Mình đã đổi tháng dự kiến thi {name}.\n\n{out.answer}"
        return out
    if state.question_id == "X1":
        return review(state)
    output = ask_question(state, question_id, PLAN_MORE.format(name=name))
    output.quick_replies = [QuickReply(label=NO_MORE_PLAN, value=NO_MORE_PLAN)]
    return output


def save_english_plan(state: ChatState, month: int, year: int, test_type: str = "ielts") -> SkillOutput:
    return save_plan(state, "E2", {"date": f"{year}-{month:02d}", "test_type": test_type})


def save_standardized_plan(state: ChatState, month: int, year: int, test_type: str = "sat") -> SkillOutput:
    return save_plan(state, "E3", {"date": f"{year}-{month:02d}", "test_type": test_type})


# Câu dự định của một thẻ: E2 (tiếng Anh) hoặc E3 (chuẩn hóa)
def plan_question(cert: dict) -> str:
    return "E2" if cert.get("category") == "english" else "E3"


# Thẻ dự định thi của mục E2 hoặc E3
def plan_cards(state: ChatState, question_id: str) -> list[dict]:
    return [c for c in saved_certs(state) if c.get("isTarget") and plan_question(c) == question_id]


# Điểm mục tiêu phải cao hơn điểm thật cao nhất của cùng bài (giống validatePlanConflicts của form)
def check_target_above_taken(state: ChatState, plan: dict) -> None:
    target = plan.get("target_score")
    if target is None:
        return
    taken = [
        c["overall"]
        for c in saved_certs(state)
        if not c.get("isTarget") and c.get("type") == plan["test_type"] and c.get("overall") is not None
    ]
    if taken and target <= max(taken):
        name = CERT_NAMES.get(plan["test_type"], plan["test_type"].upper())
        raise AskAgain(
            f"Điểm mục tiêu phải cao hơn điểm {name} bạn đã có ({max(taken):g}). Bạn nhập lại điểm mục tiêu nhé?"
        )


# Tóm tắt dự định đầu tiên của mục vào english_plan / standardized_plan (giống ChatProfileMapper.PlanSummary).
# Mục không còn thẻ dự định thì bỏ khóa, trừ khi học sinh đã nói "chưa có dự định thi" (planned False)
def sync_plan_key(state: ChatState, question_id: str, status: str = "confirmed") -> None:
    key = PLAN_QUESTIONS[question_id][0]
    card = next((c for c in plan_cards(state, question_id) if c.get("type")), None)
    if card is None:
        old = state.profile.get(key)
        if old is not None and not (isinstance(old.value, dict) and old.value.get("planned") is False):
            state.profile.pop(key)
        return
    summary = {"planned": True, "date": card.get("testDate"), "test_type": card["type"]}
    if card.get("overall") is not None:
        summary["target_score"] = card["overall"]
    state.profile[key] = ProfileField(value=summary, status=status)


# Thêm hoặc sửa thẻ dự định của cùng bài (giữ id). Đổi tháng mà không nói điểm mục tiêu thì giữ điểm cũ
def put_plan(state: ChatState, plan: dict, status: str) -> None:
    test_type = plan["test_type"]
    certs = saved_certs(state)
    old = next((c for c in certs if c.get("isTarget") and c.get("type") == test_type), None)
    target = plan.get("target_score")
    if target is None and old is not None:
        target = old.get("overall")
    cert = {
        "category": CERT_TYPES[test_type].category,
        "type": test_type,
        "subject": None,
        "overall": target,
        "scaleText": None,
        "testDate": plan.get("date"),
        "isTarget": True,
    }
    write_certs(state, put_cert(certs, cert), status)
    sync_plan_key(state, plan_question(cert), status)


# Qwen / nút vừa ghi english_plan, standardized_plan: planned False thì bỏ thẻ dự định của mục;
# có bài thi thì thêm / sửa thẻ dự định (không có bài thi thì chỉ là dấu chưa thi, không thành thẻ)
def certs_from_plans(state: ChatState, names: list[str], status: str) -> None:
    for question_id, (key, _) in PLAN_QUESTIONS.items():
        if key not in names or key not in state.profile:
            continue
        value = state.profile[key].value
        if not isinstance(value, dict):
            continue
        if value.get("planned") is False:
            certs = [
                c for c in saved_certs(state) if not (c.get("isTarget") and plan_question(c) == question_id)
            ]
            write_certs(state, certs, status)
        elif value.get("test_type") in CERT_TYPES:
            try:
                check_target_above_taken(state, value)
            except AskAgain:
                # điểm mục tiêu không cao hơn điểm đã có thì vẫn ghi dự định, bỏ điểm mục tiêu
                value = {k: v for k, v in value.items() if k != "target_score"}
            put_plan(state, value, status)


# Thẻ dự định thi đã quá tháng dự kiến (tháng dự kiến trước tháng này). Không có thì None
def overdue_card(state: ChatState, today: date | None = None) -> dict | None:
    this_month = (today or date.today()).strftime("%Y-%m")
    return next(
        (
            c
            for c in saved_certs(state)
            if c.get("isTarget") and c.get("testDate") and c["testDate"] < this_month
        ),
        None,
    )


def plan_label(cert: dict) -> str:
    year, month = cert["testDate"].split("-")
    return f"{CERT_NAMES.get(cert['type'], cert['type'].upper())} (dự kiến {month}/{year})"


# Hỏi dự định đã quá hạn: đã thi chưa, kèm 3 nút Đã thi rồi / Chưa, đổi tháng / Bỏ dự định
def ask_overdue(state: ChatState, cert: dict) -> SkillOutput:
    state.profile["plan_overdue"] = ProfileField(value={"id": cert["id"]}, status="unknown")
    ask(state, plan_question(cert))
    text = f"Bạn đã thi {plan_label(cert)} chưa?"
    state.last_question = text
    return SkillOutput(
        text, quick_replies=[QuickReply(label=label, value=label) for label in OVERDUE_REPLIES]
    )


# Thẻ dự định quá hạn đang hỏi. Thẻ không còn (đã sửa ở form) thì bỏ khóa, trả None
def overdue_pending(state: ChatState) -> dict | None:
    pending = state.profile.get("plan_overdue")
    if pending is None or not isinstance(pending.value, dict):
        return None
    card = next((c for c in saved_certs(state) if c.get("id") == pending.value.get("id")), None)
    if card is None or not card.get("isTarget"):
        state.profile.pop("plan_overdue", None)
        return None
    return card


# Học sinh trả lời câu dự định quá hạn bằng 1 trong 3 nút. Câu khác thì None (đi luồng thường)
def answer_overdue(state: ChatState, message: str) -> SkillOutput | None:
    card = overdue_pending(state)
    if card is None:
        return None
    name = CERT_NAMES.get(card["type"], card["type"].upper())
    if buttons.same_text(message, OVERDUE_TAKEN):
        # Ô nhập chứng chỉ của E1 / D3; ghi cùng bài thì thẻ thật thay thẻ dự định (save_cert_text)
        question_id = "E1" if plan_question(card) == "E2" else "D3"
        return ask_question(state, question_id, f"Bạn nhập điểm {name} đã thi và tháng thi nhé.")
    if buttons.same_text(message, OVERDUE_MOVE):
        # Ô chọn dự định của E2 / E3; chọn cùng bài thì sửa thẻ dự định này (save_plan)
        return ask_question(state, plan_question(card), f"Bạn định thi {name} vào tháng nào?")
    if buttons.same_text(message, OVERDUE_DROP):
        state.profile.pop("plan_overdue", None)
        write_certs(state, [c for c in saved_certs(state) if c.get("id") != card["id"]], "confirmed")
        sync_plan_key(state, plan_question(card))
        out = ask_next(state)
        out.answer = f"Mình đã bỏ dự định thi {plan_label(card)}.\n\n{out.answer}"
        return out
    return None


# D4 / D5: ghi xong một mục mà chưa tới số mục tối đa thì hỏi còn mục nữa không (ô nhập vẫn hiện),
# nút "Không còn ..." để sang câu sau. Không có mục mới thì None
def ask_more(state: ChatState, question_id: str, count_before: int) -> SkillOutput | None:
    name, spec = next((n, s) for n, s in LIST_FIELDS.items() if s.question == question_id)
    saved = state.profile.get(name)
    items = saved.value if saved is not None and isinstance(saved.value, list) else []
    if len(items) <= count_before or len(items) >= spec.limit:
        return None
    output = ask_question(state, question_id, MORE_TEXT[question_id].format(name=items[-1]["name"]))
    output.quick_replies = [QuickReply(label=NO_MORE[question_id], value=NO_MORE[question_id])]
    return output


def list_count(state: ChatState, question_id: str | None) -> int:
    name = next((n for n, s in LIST_FIELDS.items() if s.question == question_id), None)
    saved = state.profile.get(name) if name else None
    return len(saved.value) if saved is not None and isinstance(saved.value, list) else 0


# Đọc câu ô nhập D4 / D5 ghép; chữ vai trò, phạm vi đổi về mã như form. Câu khác thì None
def read_item_text(question_id: str | None, message: str) -> tuple[str, dict] | None:
    text = message.strip().rstrip(".")
    roles = {label.lower(): code for code, label in buttons.ROLE_LABELS.items()}
    impacts = {label.lower(): code for code, label in buttons.IMPACT_LABELS.items()}
    activity = ACTIVITY_TEXT.fullmatch(text) if question_id == "D4" else None
    if activity is not None:
        month_year = lambda value: f"{int(value.split('/')[0]):02d}/{value.split('/')[1]}"  # noqa: E731
        return "activities", {
            "name": activity["name"],
            "role": roles.get(activity["role"].strip().lower(), activity["role"]),
            "impact_level": impacts.get(activity["impact"].strip().lower(), activity["impact"]),
            "start_date": month_year(activity["start"]),
            "end_date": month_year(activity["end"]) if activity["end"] else None,
            "is_ongoing": activity["ongoing"] is not None,
        }
    award = AWARD_TEXT.fullmatch(text) if question_id == "D5" else None
    if award is not None:
        return "awards", {
            "name": award["name"],
            "impact_level": impacts.get(award["impact"].strip().lower(), award["impact"]),
            "year": int(award["year"]),
        }
    return None


# Ghi một hoạt động / giải thưởng học sinh nhập ở ô nhập, không gọi Qwen. Sai ý nào thì hỏi lại ý đó
def save_item_text(state: ChatState, name: str, item: dict) -> SkillOutput:
    spec = LIST_FIELDS[name]
    count_before = list_count(state, spec.question)
    try:
        done = spec.check_item(**item)
    except AskAgain as err:
        return ask_question(state, spec.question, err.question)
    save_items(state, name, [done], [], "confirmed")
    return ask_more(state, spec.question, count_before) or ask_next(state)


# Danh sách thẻ chứng chỉ như form (khóa certifications), bản sao để sửa
def saved_certs(state: ChatState) -> list[dict]:
    saved = state.profile.get("certifications")
    return [dict(c) for c in saved.value] if saved is not None and isinstance(saved.value, list) else []


def same_cert(a: dict, b: dict) -> bool:
    return (
        a.get("type") == b.get("type")
        and (a.get("subject") or "").lower() == (b.get("subject") or "").lower()
        and bool(a.get("isTarget")) == bool(b.get("isTarget"))
    )


# Thẻ cùng loại và cùng môn thì thay (giữ id), không thì thêm thẻ mới. Đủ khóa như FlowCertificationItemDto
# để nháp gửi lại giống hệt (merge_profile không thấy khác)
def put_cert(certs: list[dict], cert: dict) -> list[dict]:
    old = next((c for c in certs if same_cert(c, cert)), None)
    item = {"id": old["id"] if old else str(uuid.uuid4()), "customName": None, "sections": None, **cert}
    return [item if c is old else c for c in certs] if old else [*certs, item]


def write_certs(state: ChatState, certs: list[dict], status: str) -> None:
    if certs:
        state.profile["certifications"] = ProfileField(value=certs, status=status)
    else:
        state.profile.pop("certifications", None)


# Qwen / nút vừa ghi english, sat, act: sửa thẻ chứng chỉ cùng loại; "không có" thì bỏ thẻ loại đó
def certs_from_fields(
    state: ChatState,
    names: list[str],
    status: str,
    sat_act_dates: dict[str, str | None] | None = None,
) -> None:
    names = [name for name in names if name in CERT_FIELDS and name in state.profile]
    if not names:
        return
    certs = saved_certs(state)
    for name in names:
        value = state.profile[name].value
        if value is None:
            group = "english" if name == "english" else None
            # Chỉ bỏ thẻ đã thi; thẻ dự định (isTarget) giữ nguyên
            certs = [
                c
                for c in certs
                if c.get("isTarget") or (c.get("category") != group if group else c.get("type") != name)
            ]
            continue
        english = name == "english"
        test_date = value.get("date") if english else (sat_act_dates or {}).get(name)
        cert = {
            "category": "english" if english else "standardized",
            "type": value["type"] if english else name,
            "subject": None,
            "overall": value["value"] if english else value,
            "scaleText": None,
            "testDate": test_date,
            "isTarget": False,
        }
        certs = put_cert(certs, cert)
    write_certs(state, certs, status)


# Thêm thẻ ở ô nhập thì tính lại english, sat, act như ChatProfileMapper: tiếng Anh chính là thẻ thi mới
# nhất rồi điểm cao nhất; SAT / ACT lấy điểm cao nhất
def fields_from_certs(state: ChatState, status: str) -> None:
    # Điểm mục tiêu của thẻ dự định không phải điểm thật
    certs = [c for c in saved_certs(state) if not c.get("isTarget")]
    english = [c for c in certs if c.get("category") == "english" and c.get("overall") is not None]
    if english:
        main = max(english, key=lambda c: (c.get("testDate") or "", c["overall"]))
        value = {"type": main["type"], "value": main["overall"], "date": main.get("testDate")}
        state.profile["english"] = ProfileField(value=value, status=status)
    for name in ("sat", "act"):
        scores = [c["overall"] for c in certs if c.get("type") == name and c.get("overall") is not None]
        if scores:
            state.profile[name] = ProfileField(value=int(max(scores)), status=status)


# Tên trên ô chọn ("IELTS Academic"), mã ("ielts") hoặc tên ngắn ("IELTS") → mã chứng chỉ
def cert_type_of(name: str) -> str | None:
    key = name.strip().lower()
    return next(
        (
            code
            for code, spec in CERT_TYPES.items()
            if key in (code, spec.name.lower(), CERT_NAMES[code].lower())
        ),
        None,
    )


# Đọc câu ô nhập E1 / D3 ghép thành tham số của check_certification. Câu khác thì None
def read_cert_text(question_id: str | None, message: str) -> dict | None:
    match = CERT_TEXT.fullmatch(message.strip().rstrip(".")) if question_id in CERT_QUESTIONS else None
    if match is None:
        return None
    return {
        "cert": cert_type_of(match["name"]) or match["name"],
        "score": match["score"],
        "test_date": f"{match['year']}-{int(match['month']):02d}",
        "subject": match["subject"],
    }


# Ghi một thẻ chứng chỉ học sinh nhập ở ô nhập, không gọi Qwen; sai thì hỏi lại. Ghi xong hỏi còn chứng chỉ
# nào nữa không (ô nhập vẫn hiện), nút "Không còn chứng chỉ" để sang câu sau
def save_cert_text(state: ChatState, item: dict) -> SkillOutput:
    question_id = state.question_id or "E1"
    try:
        cert = check_certification(**item)
    except AskAgain as err:
        return ask_question(state, question_id, err.question)
    certs = saved_certs(state)
    overdue = overdue_pending(state)
    if overdue is not None and overdue["type"] == cert["type"]:
        # Dự định quá hạn học sinh đã thi: thẻ thật thay thẻ dự định, giữ id (như nút "Đã thi rồi" của form)
        state.profile.pop("plan_overdue", None)
        certs = [c for c in certs if c["id"] != overdue["id"]]
        certs = [*certs, {"id": overdue["id"], "customName": None, "sections": None, **cert}]
    else:
        overdue = None
        certs = put_cert(certs, cert)
    write_certs(state, certs, "confirmed")
    fields_from_certs(state, "confirmed")
    if overdue is not None:
        sync_plan_key(state, plan_question(overdue))
    if len(certs) >= MAX_CERTS:
        return ask_next(state)
    name = " ".join(filter(None, [CERT_NAMES[cert["type"]], cert["subject"]]))
    output = ask_question(state, question_id, CERT_MORE.format(name=name))
    output.quick_replies = [QuickReply(label=NO_MORE_CERT, value=NO_MORE_CERT)]
    return output


# Câu ghép từ ô nhập hoặc nút "Không còn ..." của câu đang hỏi là câu trả lời rõ: chat_flow không nhờ model
# đoán skill (model có thể đoán nhầm "Không còn ..." là muốn dừng)
# Ở bảng rà soát: nút Đúng rồi hoặc câu gõ xác nhận (CONFIRM_WORDS), nút Cần sửa, câu ô chọn nhóm, nút Sửa trên form,
# Điền lại từ đầu. Câu nào cũng có thể có nút Giữ như cũ
def is_form_text(question_id: str | None, message: str) -> bool:
    edit_replies = (FIX_REPLY, FORM_REPLY, RESTART_REPLY)
    return (
        (question_id == "X1" and any(buttons.same_text(message, reply.value) for reply in edit_replies))
        or (question_id == "X1" and read_edit_menu(message) is not None)
        or (question_id == "X1" and is_confirm(message))
        or buttons.same_text(message, KEEP_REPLY.value)
        or (question_id in NO_MORE and buttons.same_text(message, NO_MORE[question_id]))
        or (question_id in CERT_QUESTIONS and buttons.same_text(message, NO_MORE_CERT))
        or (question_id in PLAN_QUESTIONS and buttons.same_text(message, NO_MORE_PLAN))
        or (question_id in PLAN_QUESTIONS and any(buttons.same_text(message, r) for r in OVERDUE_REPLIES))
        or read_plan_text(question_id, message) is not None
        or read_item_text(question_id, message) is not None
        or read_cert_text(question_id, message) is not None
    )


def press_button(state: ChatState, button: buttons.Button) -> SkillOutput:
    buttons.press(state, button)
    certs_from_fields(state, list(button.values), "confirmed")
    certs_from_plans(state, list(button.values), "confirmed")
    state.profile.pop("cert_pending", None)
    for spec in LIST_FIELDS.values():
        if spec.question == state.question_id:
            state.profile.pop(spec.pending_key, None)
    return ask_next(state)


# Giá trị và trạng thái từng trường, để biết lượt này có sửa gì không
def profile_snapshot(state: ChatState) -> dict[str, tuple[Any, str]]:
    return {name: (deepcopy(f.value), f.status) for name, f in state.profile.items()}


class HoSoHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._reader = HoSoReader(llm, model)

    # Ghi giá trị học sinh tự khai ngoài câu đang hỏi: said_in_B0 để xác nhận lại ở X1, được đè giá trị cũ
    async def save_said(self, state: ChatState, message: str) -> None:
        known = {name: f.value for name, f in state.profile.items() if f.value is not None}
        save_read(
            state,
            await self._reader.read(None, message, known, state.branch),
            status="said_in_B0",
            message=message,
        )

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        # Tỷ giá cũ hơn 1 ngày thì lấy lại trước khi đổi tiền hoặc vẽ ô ngân sách
        await exchange_rate.refresh()
        question_id = state.question_id
        if question_id == "X1" and is_confirm(message):
            return confirm(state)
        if question_id in NO_MORE and buttons.same_text(message, NO_MORE[question_id]):
            return ask_next(state)
        if question_id in CERT_QUESTIONS and buttons.same_text(message, NO_MORE_CERT):
            return ask_next(state)
        if question_id in PLAN_QUESTIONS and buttons.same_text(message, NO_MORE_PLAN):
            return ask_next(state)
        overdue = answer_overdue(state, message)
        if overdue is not None:
            return overdue
        cert_text = read_cert_text(question_id, message)
        if cert_text is not None:
            return save_cert_text(state, cert_text)
        item_text = read_item_text(question_id, message)
        if item_text is not None:
            return save_item_text(state, *item_text)
        button = buttons.pressed(question_id, message, can_skip=question_id in OPTIONAL_QUESTIONS)
        if button is not None:
            return press_button(state, button)
        gpa_text = read_gpa_text(message)
        if gpa_text is not None:
            return save_gpa(state, *gpa_text)
        money = read_money(message) if question_id == "F1" else None
        if money is not None:
            return save_budget(state, *money)
        plan_data = read_plan_text(question_id, message)
        if plan_data is not None:
            return save_plan(state, question_id, plan_data)

        # Xử lý pending_cert nếu học sinh đang được hỏi ngày tháng thi chứng chỉ
        pending_field = state.profile.get("cert_pending")
        if pending_field is not None and isinstance(pending_field.value, dict):
            # Nếu người dùng bấm nút của câu hỏi (hoặc nút bỏ qua):
            button = buttons.pressed(question_id, message, can_skip=question_id in OPTIONAL_QUESTIONS)
            if button is not None:
                state.profile.pop("cert_pending", None)
                return press_button(state, button)

            date_str = parse_month_year_text(message)
            pending = pending_field.value
            msg_lower = message.strip().lower()
            is_skip = any(w in msg_lower for w in ("không nhớ", "quên", "chưa rõ", "bỏ qua", "skip"))

            if date_str or is_skip:
                state.profile.pop("cert_pending", None)
                cert = {
                    "category": pending.get("category", "english"),
                    "type": pending["type"],
                    "subject": pending.get("subject"),
                    "overall": pending["score"],
                    "scaleText": None,
                    "testDate": date_str if date_str else None,
                }
                certs = put_cert(saved_certs(state), cert)
                write_certs(state, certs, "confirmed")
                fields_from_certs(state, "confirmed")
                c_name = pending.get("cert_name") or CERT_NAMES.get(pending["type"], pending["type"].upper())
                out = ask_next(state)
                if date_str:
                    out.answer = f"Mình đã ghi nhận chứng chỉ {c_name} (điểm {pending['score']}, thi {date_str}).\n\n{out.answer}"
                else:
                    out.answer = f"Mình đã ghi nhận điểm {c_name} {pending['score']}.\n\n{out.answer}"
                return out

            return ask_cert_date(state, pending)

        known = {name: f.value for name, f in state.profile.items() if f.value is not None}
        asked_id = question_id if question_id in QUESTION_FIELDS else None
        before = {key: deepcopy(state.profile[key].value) for key in PENDING_KEYS if key in state.profile}
        count_before = list_count(state, question_id)
        snapshot = profile_snapshot(state)
        read = await self._reader.read(asked_id, message, known, state.branch)
        # GPA không nói thang thì chưa ghi, hỏi lại thang sau khi ghi các thông tin khác
        guessed_gpa = read.values.pop("gpa", None) if read.gpa_guessed else None
        save_read(state, read, message=message)
        if guessed_gpa is not None:
            return ask_gpa_scale(state, guessed_gpa["value"])
        if read.pending_cert is not None:
            return ask_cert_date(state, read.pending_cert, read.ask_again)

        follow = follow_up(state, before)
        if follow is not None:
            return follow
        if question_id in NO_MORE:
            more = ask_more(state, question_id, count_before)
            if more is not None:
                return more
        if question_id == "X1":
            # Học sinh sửa thông tin ở bảng rà soát: sai thì hỏi lại, đúng thì hiện lại bảng,
            # chưa nói sửa gì thì hiện ô chọn mục cần sửa
            if read.ask_again:
                return SkillOutput(read.ask_again)
            return review(state) if profile_snapshot(state) != snapshot else edit_menu(state)
        # Câu bắt buộc chưa có câu trả lời (giá trị sai, hoặc học sinh không nói) thì hỏi lại, tối đa 2 lần.
        # Câu tùy chọn chỉ hỏi lại khi giá trị sai
        limit = MAX_ASK if asked_id in REQUIRED_QUESTIONS or read.ask_again else 1
        if asked_id and not answered(state, asked_id) and state.asked.get(asked_id, 0) < limit:
            return ask_question(state, asked_id, read.ask_again)
        return ask_next(state)
