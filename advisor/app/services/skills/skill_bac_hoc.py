"""skill_bac_hoc (B1), phần đọc: Qwen đọc cấp học, bậc muốn học, ngành, kỳ nhập học (câu B1.1 đến B1.4).

Phần run(state, message) (hỏi lần lượt, ghi state) làm ở B2. Ở đây chỉ có hàm đọc.
Quy ước giá trị trả về: None = học sinh không nói; UNKNOWN = học sinh nói "chưa rõ" (B2 ghi status="unknown").
"""

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_bac_hoc.md"
UNKNOWN = "unknown"

# Cấp học hiện tại → các lớp form cho chọn, chép từ getGradeOptionsForLevel (profile-flow-options.ts).
# Mã cấp học giữ đồng bộ với currentEducationLevels trong DbSeeder.cs. Không có tiến sĩ
GRADE_OPTIONS = {
    "middle_school": ("Lớp 6", "Lớp 7", "Lớp 8", "Lớp 9"),
    "high_school": ("Lớp 10", "Lớp 11", "Lớp 12"),
    "college_university": ("Năm 1", "Năm 2", "Năm 3", "Năm 4", "Năm cuối", "Đã tốt nghiệp"),
    "master": ("Năm 1", "Năm 2", "Đã tốt nghiệp"),
}
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
