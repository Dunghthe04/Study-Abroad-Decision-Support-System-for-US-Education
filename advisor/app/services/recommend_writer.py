"""Lời cho danh sách gợi ý trường: Claude diễn đạt lại danh sách code đã lọc và xếp nhóm, không đổi nhóm, không thêm hay bớt trường.

Claude trả JSON (câu mở, mỗi nhóm một ý, câu kết). Code kiểm tra rồi nối thành tin nhắn: phải nhắc đủ mọi trường đang hiện,
mọi số phải có trong dữ kiện, không nhắc tên trường ngoài danh sách, không hứa kết quả tuyển sinh, không phán trường nào "tốt nhất".
"""

from pathlib import Path
from typing import Any

from app.schemas.recommend_note import (
    RecommendNoteRequest,
    RecommendNoteResponse,
    RecommendNoteResult,
    recommend_note_schema,
)
from app.services.note_checks import (
    check_mentions_all,
    check_no_promise,
    check_no_strangers,
    check_no_verdict,
    check_numbers,
    numbers,
    render_note,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_goi_y_truong.md"

# Chọn trường là việc của học sinh: lời tóm tắt không được xếp hạng trường nào hơn trường nào
VERDICTS = ("tốt nhất", "đáng chọn nhất", "nên chọn", "hơn hẳn")


# Gom dữ liệu thành đúng một JSON để gửi Claude. others chỉ dùng để kiểm tra, không gửi
def build_user(req: RecommendNoteRequest) -> dict[str, Any]:
    return {"schools": req.schools, "facts": req.facts}


# Mọi số Claude được phép nhắc: số trong tên trường và các dòng dữ kiện
def allowed_numbers(req: RecommendNoteRequest) -> set[float]:
    return {n for text in [*req.schools, *req.facts] for n in numbers(text)}


def _texts(result: RecommendNoteResult) -> list[str]:
    return [result.intro, *result.points, result.closing]


# Lời hợp lệ: nhắc đủ mọi trường đang hiện, không số lạ, không tên trường lạ, không hứa hẹn, không xếp hạng.
# Sai thì ném ValueError để gọi lại một lần, rồi báo FAILED
def check_answer(raw: dict[str, Any], req: RecommendNoteRequest) -> RecommendNoteResult:
    result = RecommendNoteResult.model_validate(raw)
    text = " ".join(_texts(result))

    check_mentions_all(text, req.schools, "mọi trường đang hiện trong danh sách")
    check_numbers(text, allowed_numbers(req), "dữ kiện")
    check_no_promise(text)
    check_no_verdict(text, VERDICTS, "không được xếp hạng hay chọn thay học sinh")
    check_no_strangers(text, req.schools, req.others)
    return result


# Một lượt AI: gửi dữ kiện cho Claude, kiểm tra lời, đóng gói trả về
class RecommendWriter:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def write(self, req: RecommendNoteRequest) -> RecommendNoteResponse:
        result = await self._call.run(
            build_user(req), recommend_note_schema(), lambda raw: check_answer(raw, req)
        )
        return RecommendNoteResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if result is not None else "FAILED",
            answer=render_note(result) if result else "",
        )
