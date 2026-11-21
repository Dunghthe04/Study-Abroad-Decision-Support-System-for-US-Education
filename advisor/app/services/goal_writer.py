"""Lời cho goal-seeking: Claude diễn đạt lại kết quả code đã tính, không tính lại, không đổi nhóm, không thêm trường.

Claude trả JSON (lời mở, vài ý chính, lời kết). Code kiểm tra rồi nối thành tin nhắn: lời mở phải nhắc đúng tên trường đã hiểu,
mọi số phải có trong dữ kiện, không nhắc tên trường ngoài trường đang hỏi và các trường tương tự, không hứa hẹn kết quả.
"""

from pathlib import Path
from typing import Any

from app.schemas.goal_note import GoalNoteRequest, GoalNoteResponse, GoalNoteResult, goal_note_schema
from app.services.note_checks import (
    check_no_promise,
    check_no_strangers,
    check_numbers,
    numbers,
    render_note,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_goal_seek.md"


# Gom dữ liệu thành đúng một JSON để gửi Claude. others chỉ dùng để kiểm tra, không gửi
def build_user(req: GoalNoteRequest) -> dict[str, Any]:
    return {
        "school": req.school,
        "target": req.target,
        "outcome": req.outcome,
        "facts": req.facts,
        "alternatives": req.alternatives,
    }


# Mọi số Claude được phép nhắc: số trong tên trường, nhóm đích và các dữ kiện
def allowed_numbers(req: GoalNoteRequest) -> set[float]:
    texts = [req.school, req.target, *req.facts, *req.alternatives]
    return {n for text in texts for n in numbers(text)}


def _texts(result: GoalNoteResult) -> list[str]:
    return [result.intro, *result.points, result.closing]


# Lời hợp lệ: nhắc đúng tên trường ở lời mở, không số lạ, không tên trường lạ, không hứa hẹn.
# Sai thì ném ValueError để gọi lại một lần, rồi báo FAILED
def check_answer(raw: dict[str, Any], req: GoalNoteRequest) -> GoalNoteResult:
    result = GoalNoteResult.model_validate(raw)
    text = " ".join(_texts(result))

    if req.school.lower() not in result.intro.lower():
        raise ValueError(f"lời mở phải nhắc đúng tên trường đã hiểu: {req.school!r}")
    check_numbers(text, allowed_numbers(req), "dữ kiện")
    check_no_promise(text)
    check_no_strangers(text, {req.school, *req.alternatives}, req.others)
    return result


# Một lượt AI: gửi dữ kiện cho Claude, kiểm tra lời, đóng gói trả về
class GoalWriter:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def write(self, req: GoalNoteRequest) -> GoalNoteResponse:
        result = await self._call.run(build_user(req), goal_note_schema(), lambda raw: check_answer(raw, req))
        return GoalNoteResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if result is not None else "FAILED",
            answer=render_note(result) if result else "",
        )
