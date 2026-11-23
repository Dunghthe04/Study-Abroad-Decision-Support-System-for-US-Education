"""Nhận xét so sánh trường: Claude diễn đạt lại số liệu code đã tra, không tính lại, không chọn thay học sinh, không thêm trường.

Claude trả JSON (câu mở, vài điểm, câu kết). Code kiểm tra rồi nối thành tin nhắn: phải nhắc đủ tên các trường đang so sánh,
mọi số phải có trong số liệu, không nhắc tên trường ngoài danh sách, không hứa kết quả tuyển sinh, không phán trường nào "tốt hơn".
"""

from pathlib import Path
from typing import Any

from app.schemas.compare_note import (
    CompareNoteRequest,
    CompareNoteResponse,
    CompareNoteResult,
    compare_note_schema,
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

SKILL_PATH = Path(SKILLS_DIR) / "skill_so_sanh.md"

# Phán xét chung về trường: chọn trường là việc của học sinh (và phần tư vấn theo hồ sơ), không phải của lời so sánh số liệu
VERDICTS = ("nên chọn", "tốt hơn", "tốt nhất", "kém hơn", "tệ hơn", "đáng chọn", "hơn hẳn")


# Gom dữ liệu thành đúng một JSON để gửi Claude. others chỉ dùng để kiểm tra, không gửi
def build_user(req: CompareNoteRequest) -> dict[str, Any]:
    return {"schools": req.schools, "facts": req.facts}


# Mọi số Claude được phép nhắc: số trong tên trường và các dòng số liệu
def allowed_numbers(req: CompareNoteRequest) -> set[float]:
    return {n for text in [*req.schools, *req.facts] for n in numbers(text)}


def _texts(result: CompareNoteResult) -> list[str]:
    return [result.intro, *result.points, result.closing]


# Lời hợp lệ: nhắc đủ tên các trường, không số lạ, không tên trường lạ, không hứa hẹn, không phán trường nào tốt hơn.
# Sai thì ném ValueError để gọi lại một lần, rồi báo FAILED
def check_answer(raw: dict[str, Any], req: CompareNoteRequest) -> CompareNoteResult:
    result = CompareNoteResult.model_validate(raw)
    text = " ".join(_texts(result))

    check_mentions_all(text, req.schools, "tên các trường đang so sánh")
    check_numbers(text, allowed_numbers(req), "số liệu")
    check_no_promise(text)
    check_no_verdict(text, VERDICTS, "không được phán trường nào tốt hơn hay nên chọn")
    check_no_strangers(text, req.schools, req.others)
    return result


# Một lượt AI: gửi số liệu cho Claude, kiểm tra lời, đóng gói trả về
class CompareWriter:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def write(self, req: CompareNoteRequest) -> CompareNoteResponse:
        result = await self._call.run(
            build_user(req), compare_note_schema(), lambda raw: check_answer(raw, req)
        )
        return CompareNoteResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if result is not None else "FAILED",
            answer=render_note(result) if result else "",
        )
