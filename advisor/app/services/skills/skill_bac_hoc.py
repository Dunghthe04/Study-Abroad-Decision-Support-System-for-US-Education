"""skill_bac_hoc (B1): Qwen đọc cấp học, bậc muốn học, ngành, kỳ nhập học (câu B1.1 đến B1.4), code ghi state.

Hỏi lần lượt B1.1 → B1.4, bỏ qua câu đã có câu trả lời, mỗi câu tối đa 2 lần; xong thì sang B2 (skill_ho_so).
Quy ước giá trị đọc ra: None = học sinh không nói; UNKNOWN = học sinh nói "chưa rõ" (ghi status="unknown").
"""

import re
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from app.schemas.chat import ChatState, ProfileField, QuickReply
from app.services.chat_flow import SkillOutput, go_to
from app.services.skill_call import SKILLS_DIR, SkillCall
from app.services.skills import buttons, major_catalog, skill_ho_so
from app.services.skills.major_catalog import Major
from app.services.skills.skill_ho_so import MAX_ASK

SKILL_PATH = SKILLS_DIR / "skill_bac_hoc.md"
UNKNOWN = "unknown"

# Cấp học hiện tại → các lớp form cho chọn, chép từ getGradeOptionsForLevel (profile-flow-options.ts).
# Mã cấp học giữ đồng bộ với currentEducationLevels trong DbSeeder.cs. Không có tiến sĩ.
# Để ở buttons.py vì ô chọn B1.1 trong chat cũng dùng
GRADE_OPTIONS = buttons.GRADE_OPTIONS
CURRENT_LEVELS = tuple(GRADE_OPTIONS)
ALL_GRADES = tuple(dict.fromkeys(g for grades in GRADE_OPTIONS.values() for g in grades))
# Độ dài tối đa của ngành (giống ValidateForCommit)
MAX_MAJOR_LEN = 128
# Qwen trả tên dễ đọc, code đổi sang mã StudyLevels của .NET
TARGET_LEVELS = {
    "college": "community_college",
    "bachelor": "undergraduate",
    "master": "master",
    "phd": "phd",
    UNKNOWN: UNKNOWN,
}
# Kỳ nhập học dạng "Fall 2027"
INTAKE_PATTERN = re.compile(r"(Fall|Spring|Summer) 20\d\d")
# Những cách Qwen có thể viết "chưa rõ ngành"
UNKNOWN_MAJORS = {UNKNOWN, "undecided", "chưa rõ", "chưa biết"}

# Câu hỏi B1 → thông tin câu đó cần, theo thứ tự hỏi
QUESTION_FIELDS = {
    "B1.1": ["current_level"],
    "B1.2": ["target_level"],
    "B1.3": ["major"],
    "B1.4": ["intake"],
}
READ_FIELDS = ("current_level", "current_grade", "target_level", "major", "intake")
# Ngành bắt buộc giống form: hỏi tới khi học sinh chọn ngành hoặc bấm "Em chưa rõ ngành" (nút "Chưa rõ" của form)
REQUIRED_QUESTIONS = ("B1.3",)
# Ô form không bắt buộc: có nút "Bỏ qua", bấm thì không hỏi lại
SKIPPABLE_QUESTIONS = ("B1.4",)

SCHEMA = {
    "type": "object",
    "required": ["current_level", "current_grade", "target_level", "major", "intake"],
    "properties": {
        "current_level": {"enum": [*CURRENT_LEVELS, None]},
        "current_grade": {"enum": [*ALL_GRADES, None]},
        "target_level": {"enum": [*TARGET_LEVELS, None]},
        "major": {"type": ["string", "null"]},
        "intake": {"type": ["string", "null"]},
    },
}


# Kết quả đọc, đã đổi sang mã chuẩn
@dataclass
class BacHocRead:
    current_level: str | None = None
    current_grade: str | None = None
    target_level: str | None = None
    major: str | None = None
    intake: str | None = None
    ai_used: bool = False


# Ngành: rỗng → None; "chưa rõ" → UNKNOWN; còn lại giữ tên đã bỏ khoảng trắng thừa
def clean_major(major: str | None) -> str | None:
    if major is None or not major.strip():
        return None
    major = major.strip()
    if len(major) > MAX_MAJOR_LEN:
        raise ValueError(f"major dài quá {MAX_MAJOR_LEN} ký tự")
    return UNKNOWN if major.lower() in UNKNOWN_MAJORS else major


# Lớp quyết định cấp học: "Lớp 12" chỉ thuộc high_school nên tự suy ra được.
# "Năm 1" có ở cả đại học lẫn thạc sĩ nên phải có current_level đi kèm
def check_grade(level: str | None, grade: str | None) -> tuple[str | None, str | None]:
    if grade is None:
        return level, None
    if level is None:
        levels = [lv for lv, grades in GRADE_OPTIONS.items() if grade in grades]
        if len(levels) != 1:
            raise ValueError(f"current_grade {grade} cần current_level đi kèm")
        level = levels[0]
    if grade not in GRADE_OPTIONS[level]:
        raise ValueError(f"current_grade {grade} không thuộc {level}")
    return level, grade


# Kiểm tra JSON Qwen trả, sai thì ném ValueError để SkillCall gọi lại
def check_read(raw: dict[str, Any]) -> BacHocRead:
    current_level = raw.get("current_level")
    if current_level is not None and current_level not in CURRENT_LEVELS:
        raise ValueError(f"current_level lạ: {current_level}")
    current_level, grade = check_grade(current_level, raw.get("current_grade"))

    target = raw.get("target_level")
    if target is not None and target not in TARGET_LEVELS:
        raise ValueError(f"target_level lạ: {target}")

    intake = raw.get("intake")
    if intake is not None and not INTAKE_PATTERN.fullmatch(intake):
        raise ValueError(f"intake sai dạng: {intake}")

    return BacHocRead(
        current_level=current_level,
        current_grade=grade,
        target_level=TARGET_LEVELS[target] if target is not None else None,
        major=clean_major(raw.get("major")),
        intake=intake,
        ai_used=True,
    )


# Gọi Qwen đọc câu trả lời ở B1. Qwen lỗi 2 lần thì trả kết quả rỗng: B2 sẽ hỏi lại câu đó
class BacHocReader:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def read(self, question_id: str, message: str) -> BacHocRead:
        user = {"question_id": question_id, "message": message}
        result = await self._call.run(user, SCHEMA, check_read)
        return result if result is not None else BacHocRead()


# Ngành học sinh gõ không có trong bảng majors: chữ đã gõ và các ngành gần giống (có thể rỗng)
@dataclass
class MajorMiss:
    text: str
    near: list[Major] = field(default_factory=list)


# Đổi ngành đọc được sang tên Anh trong bảng majors (giống form lưu). Không khớp đúng thì bỏ ngành
# và trả MajorMiss để hỏi lại. Không có danh sách ngành (không có DB) thì giữ nguyên chữ như trước
async def resolve_major(read: BacHocRead) -> MajorMiss | None:
    if read.major is None or read.major == UNKNOWN:
        return None
    found = await major_catalog.find(read.major)
    if found is None:
        return None
    if found.exact:
        read.major = found.exact.name
        return None
    miss = MajorMiss(read.major, found.near)
    read.major = None
    return miss


# Hỏi lại ngành: gần giống thì hiện các ngành đó thành nút để chọn, không giống ngành nào thì nhờ gõ lại
def ask_major_again(state: ChatState, miss: MajorMiss) -> SkillOutput:
    if len(miss.near) == 1:
        text = (
            f'Mình thấy câu trả lời của bạn gần với ngành "{miss.near[0].label}". '
            "Nếu đúng thì bạn bấm nút bên dưới, không thì gõ lại tên ngành nhé."
        )
    elif miss.near:
        names = ", ".join(f'"{m.label}"' for m in miss.near)
        text = (
            f"Mình thấy câu trả lời của bạn gần với các ngành {names}. "
            "Bạn chọn ngành đúng ở nút bên dưới, hoặc gõ lại tên ngành nhé."
        )
    else:
        text = (
            f'Mình chưa tìm thấy ngành "{miss.text}" trong danh sách ngành. '
            "Bạn gõ lại tên ngành khác hoặc tìm trong ô bên dưới nhé."
        )
    output = buttons.ask_with_buttons(state, "B1.3", text)
    # Nút ngành gần giống gửi tên Việt, lượt sau khớp đúng nên ghi thẳng
    output.quick_replies = [
        QuickReply(label=m.label, value=m.label) for m in miss.near
    ] + output.quick_replies
    return output


# Ghi kết quả đọc vào hồ sơ. UNKNOWN ghi status="unknown", không có giá trị.
# keep_existing=True (thông tin nói ở B0) thì không đè thông tin đã có
def save_read(
    state: ChatState, read: BacHocRead, status: str = "confirmed", keep_existing: bool = False
) -> None:
    for name in READ_FIELDS:
        value = getattr(read, name)
        if value is None or (keep_existing and name in state.profile):
            continue
        if value == UNKNOWN:
            state.profile[name] = ProfileField(value=None, status="unknown")
        else:
            state.profile[name] = ProfileField(value=value, status=status)

    lvl = getattr(state.profile.get("current_level"), "value", None)
    grd = getattr(state.profile.get("current_grade"), "value", None)
    grad_year = buttons.guess_graduation_year(lvl, grd)
    if grad_year is not None and (not keep_existing or "graduation_year" not in state.profile):
        state.profile["graduation_year"] = ProfileField(value=grad_year, status=status)


def answered(state: ChatState, question_id: str) -> bool:
    return any(name in state.profile for name in QUESTION_FIELDS[question_id])


# Câu này còn hỏi được không: câu bắt buộc thì luôn hỏi, câu khác tối đa 2 lần
def can_ask(state: ChatState, question_id: str) -> bool:
    return question_id in REQUIRED_QUESTIONS or state.asked.get(question_id, 0) < MAX_ASK


# Câu B1 tiếp theo chưa có câu trả lời và còn hỏi được. Hết thì None
def next_question(state: ChatState) -> str | None:
    for question_id in QUESTION_FIELDS:
        if not answered(state, question_id) and can_ask(state, question_id):
            return question_id
    return None


def ask_question(state: ChatState, question_id: str) -> SkillOutput:
    return buttons.ask_with_buttons(state, question_id, can_skip=question_id in SKIPPABLE_QUESTIONS)


# Hỏi câu B1 tiếp theo; hết câu thì sang B2 và hỏi câu đầu của skill_ho_so. skill_nhu_cau gọi hàm này
def ask_next(state: ChatState) -> SkillOutput:
    if skill_ho_so.reviewed(state):  # đang sửa hồ sơ: quay về bảng rà soát, không đổi bước
        return skill_ho_so.review(state)
    question_id = next_question(state)
    if question_id:
        return ask_question(state, question_id)
    go_to(state, "B2")
    return skill_ho_so.ask_next(state)


class BacHocHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._reader = BacHocReader(llm, model)

    # Ghi giá trị học sinh tự khai ngoài câu đang hỏi: said_in_B0 để xác nhận lại ở X1, được đè giá trị cũ
    # Ngành không khớp danh sách thì bỏ, tới B1.3 hỏi lại
    async def save_said(self, state: ChatState, message: str) -> None:
        read = await self._reader.read("B1.1", message)
        await resolve_major(read)
        save_read(state, read, status="said_in_B0")

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        question_id = state.question_id if state.question_id in QUESTION_FIELDS else "B1.1"
        # B5. Bấm nút (bậc học, "Em chưa rõ ngành") thì ghi thẳng, không gọi Qwen
        can_skip = question_id in SKIPPABLE_QUESTIONS
        button = buttons.pressed(question_id, message, can_skip=can_skip)
        if button is not None:
            buttons.press(state, button)
            if button == buttons.SKIP:
                state.asked[question_id] = MAX_ASK  # bỏ qua thì không hỏi lại
            return ask_next(state)
        # Chọn ngành ở ô tìm ngành / nút ngành gần giống, hoặc gõ đúng tên ngành: ghi thẳng, không gọi Qwen
        if question_id == "B1.3":
            found = await major_catalog.find(message)
            if found is not None and found.exact:
                state.profile["major"] = buttons.confirmed(found.exact.name)
                return ask_next(state)
        read = await self._reader.read(question_id, message)
        miss = await resolve_major(read)
        save_read(state, read)
        if miss is not None and question_id == "B1.3" and not answered(state, "B1.3"):
            return ask_major_again(state, miss)
        # Học sinh chưa trả lời câu đang hỏi thì hỏi lại (tối đa 2 lần, ngành thì hỏi tới khi có)
        if not answered(state, question_id) and can_ask(state, question_id):
            return ask_question(state, question_id)
        return ask_next(state)
