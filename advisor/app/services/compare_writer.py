"""Nhận xét so sánh trường: Gemini diễn đạt lại số liệu code đã tra, không tính lại, không chọn thay học sinh, không thêm trường.

Gemini trả JSON (câu mở, vài điểm, câu kết). Code kiểm tra rồi nối thành tin nhắn: phải nhắc đủ tên các trường đang so sánh,
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
from app.services.scenario_writer import numbers
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_so_sanh.md"

# Lời hứa kết quả tuyển sinh: hệ thống chỉ so số liệu, không nói được khả năng đỗ
PROMISES = (
    "chắc chắn đậu",
    "chắc chắn đỗ",
    "đảm bảo đậu",
    "đảm bảo đỗ",
    "chắc chắn được nhận",
    "sẽ đậu",
    "sẽ đỗ",
    "100%",
)

# Phán xét chung về trường: chọn trường là việc của học sinh (và phần tư vấn theo hồ sơ), không phải của lời so sánh số liệu
VERDICTS = ("nên chọn", "tốt hơn", "tốt nhất", "kém hơn", "tệ hơn", "đáng chọn", "hơn hẳn")


# Gom dữ liệu thành đúng một JSON để gửi Gemini. others chỉ dùng để kiểm tra, không gửi
def build_user(req: CompareNoteRequest) -> dict[str, Any]:
    return {"schools": req.schools, "facts": req.facts}


# Mọi số Gemini được phép nhắc: số trong tên trường và các dòng số liệu
def allowed_numbers(req: CompareNoteRequest) -> set[float]:
    return {n for text in [*req.schools, *req.facts] for n in numbers(text)}


def _texts(result: CompareNoteResult) -> list[str]:
    return [result.intro, *result.points, result.closing]


# Lời hợp lệ: nhắc đủ tên các trường, không số lạ, không tên trường lạ, không hứa hẹn, không phán trường nào tốt hơn.
# Sai thì ném ValueError để gọi lại một lần, rồi báo FAILED
def check_answer(raw: dict[str, Any], req: CompareNoteRequest) -> CompareNoteResult:
    result = CompareNoteResult.model_validate(raw)
    text = " ".join(_texts(result))
    lowered = text.lower()

    missing = [name for name in req.schools if name.lower() not in lowered]
    if missing:
        raise ValueError(f"lời phải nhắc đủ tên các trường đang so sánh, thiếu: {missing}")

    extra = sorted(set(numbers(text)) - allowed_numbers(req))
    if extra:
        raise ValueError(f"lời nhắc số không có trong số liệu: {extra}")

    promised = [p for p in PROMISES if p in lowered]
    if promised:
        raise ValueError(f"lời hứa kết quả tuyển sinh: {promised}")

    judged = [v for v in VERDICTS if v in lowered]
    if judged:
        raise ValueError(f"không được phán trường nào tốt hơn hay nên chọn: {judged}")

    # Bỏ tên trường hợp lệ rồi tìm tên trường khác: nhắc tới là sai (có thể nhầm hoặc bịa)
    rest = lowered
    for name in sorted(set(req.schools), key=len, reverse=True):
        rest = rest.replace(name.lower(), " ")
    strangers = [name for name in req.others if name.lower() in rest]
    if strangers:
        raise ValueError(f"lời nhắc tên trường ngoài danh sách: {strangers[:3]}")
    return result


# Nối các phần Gemini viết thành tin nhắn: câu mở, các điểm, câu kết
def render(result: CompareNoteResult) -> str:
    lines = [result.intro.strip()]
    if result.points:
        lines.append("")
        lines += [f"- {point.strip()}" for point in result.points if point.strip()]
    if result.closing.strip():
        lines += ["", result.closing.strip()]
    return "\n".join(lines)


# Một lượt AI: gửi số liệu cho Gemini, kiểm tra lời, đóng gói trả về
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
            answer=render(result) if result else "",
        )
