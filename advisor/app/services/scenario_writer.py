"""Lời cho kịch bản what-if: Gemini viết lời từ kết quả simulate do code tính, không tính lại, không đổi nhóm.

Gemini trả JSON có cấu trúc (lời mở, từng nhóm kèm tên trường và ghi chú, lời kết). Code kiểm tra tên trường,
nhóm và số rồi nối thành tin nhắn: mọi chữ trong tin vẫn do Gemini viết, nhưng không thể nêu sai nhóm hay bịa tên trường.
"""

import re
from pathlib import Path
from typing import Any

from app.schemas.scenario_note import (
    ScenarioNoteRequest,
    ScenarioNoteResponse,
    ScenarioNoteResult,
    scenario_note_schema,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_kich_ban.md"

NUMBER = re.compile(r"\d+(?:[.,]\d+)*")
THOUSANDS = re.compile(r"\d{1,3}(?:[.,]\d{3})+")

# Mã lý do của một trường (backend suy từ số đã tính) → câu đúng. Gemini chỉ diễn đạt lại, không tự so sánh
REASON_TEXT = {
    "GPA_HIGH": "GPA cao hơn mặt bằng sinh viên trúng tuyển",
    "GPA_MID": "GPA nằm trong mặt bằng sinh viên trúng tuyển",
    "GPA_LOW": "GPA thấp hơn phần lớn sinh viên trúng tuyển",
    "SAT_ABOVE": "SAT cao hơn mốc 75% của trường",
    "SAT_IN_RANGE": "SAT nằm trong khoảng SAT của trường",
    "SAT_BELOW": "SAT thấp hơn mốc 25% của trường",
    "BUDGET_OK": "chi phí nằm trong ngân sách",
    "NEED_AID": "chi phí vượt ngân sách nên cần học bổng lớn",
    "INTL_LOW": "trường nhận rất ít du học sinh so với tỉ lệ nhận chung",
    "EC_PLUS": "ngoại khóa là lợi thế ở trường này",
    "EC_MINUS": "ngoại khóa cần bổ sung cho trường này",
    "LOW_RATE": "trường chỉ nhận dưới 15% hồ sơ nên luôn là Khó vào",
    "RATE_CAP": "trường nhận dưới 50% hồ sơ nên chỉ tối đa là Vừa sức",
    "WEAK_CAP": "một chỉ số còn yếu nên chưa lên An toàn",
}

# Yếu tố học sinh đổi → nó ảnh hưởng gì đến kết quả (để giải thích khi nhóm không đổi)
FACTOR_EFFECT = {
    "gpa": "GPA làm đổi nhóm của trường",
    "sat": "SAT làm đổi nhóm của trường",
    "act": "ACT làm đổi nhóm của trường (chỉ khi thiếu SAT)",
    "ielts": "IELTS không làm đổi nhóm, chỉ làm trường vào hoặc ra khỏi danh sách (đủ mức tiếng Anh tối thiểu) và đổi thứ tự trong nhóm",
    "toefl": "TOEFL không làm đổi nhóm, chỉ làm trường vào hoặc ra khỏi danh sách (đủ mức tiếng Anh tối thiểu) và đổi thứ tự trong nhóm",
    "duolingo": "Duolingo không làm đổi nhóm, chỉ làm trường vào hoặc ra khỏi danh sách (đủ mức tiếng Anh tối thiểu) và đổi thứ tự trong nhóm",
    "budget": "ngân sách làm trường vào hoặc ra khỏi danh sách, không đổi nhóm của từng trường",
    "scholarship": "mức cần học bổng làm trường vào hoặc ra khỏi danh sách, không đổi nhóm của từng trường",
}


# Các số trong một đoạn chữ. "40.000" và "40,000" là bốn mươi nghìn, "7,5" là bảy phẩy năm
def numbers(text: str) -> list[float]:
    return [
        float(re.sub(r"[.,]", "", token)) if THOUSANDS.fullmatch(token) else float(token.replace(",", "."))
        for token in NUMBER.findall(text)
    ]


# Gom dữ liệu thành đúng một JSON để gửi Gemini (khóa camelCase, khớp mô tả trong skill)
def build_user(req: ScenarioNoteRequest) -> dict[str, Any]:
    def school(s) -> dict[str, Any]:
        out: dict[str, Any] = {"name": s.name}
        if s.moved_from:
            out["movedFrom"] = s.moved_from
        if reasons := [REASON_TEXT[r] for r in s.reasons if r in REASON_TEXT]:
            out["why"] = reasons
        if s.need_aid:
            out["needAid"] = True
        return out

    return {
        "label": req.label,
        "direction": req.direction,
        "factorEffects": [FACTOR_EFFECT[f] for f in req.factors if f in FACTOR_EFFECT],
        "unsupported": req.unsupported,
        "student": req.student.model_dump(by_alias=True, exclude_none=True),
        "groups": [
            {"key": g.key, "title": g.title, "schools": [school(s) for s in g.schools], "more": g.more}
            for g in req.groups
        ],
    }


# Mọi số Gemini được phép nhắc: số trong dữ liệu gửi đi (nhãn, ngân sách, tên, các câu lý do và tác động)
def allowed_numbers(req: ScenarioNoteRequest) -> set[float]:
    texts = [req.label, *req.unsupported, *(s.name for g in req.groups for s in g.schools)]
    texts += [REASON_TEXT[r] for g in req.groups for s in g.schools for r in s.reasons if r in REASON_TEXT]
    texts += [FACTOR_EFFECT[f] for f in req.factors if f in FACTOR_EFFECT]
    allowed = {n for text in texts for n in numbers(text)}
    allowed.update(float(g.more) for g in req.groups)
    if req.student.budget_usd is not None:
        allowed.add(float(req.student.budget_usd))
    return allowed


def _texts(result: ScenarioNoteResult) -> list[str]:
    return [result.intro, *(g.note for g in result.groups), result.closing]


# Lời hợp lệ: đúng nhóm, đúng danh sách trường mỗi nhóm, không nhắc số lạ, không nhắc tên trường ngoài danh sách.
# Sai thì ném ValueError để gọi lại một lần, rồi báo FAILED
def check_answer(raw: dict[str, Any], req: ScenarioNoteRequest) -> ScenarioNoteResult:
    result = ScenarioNoteResult.model_validate(raw)

    wanted = {g.key: {s.name for s in g.schools} for g in req.groups}
    got = [g.key for g in result.groups]
    if len(got) != len(set(got)) or set(got) != set(wanted):
        raise ValueError(f"các nhóm phải đúng danh sách {sorted(wanted)}, nhận được {got}")
    for group in result.groups:
        if len(group.schools) != len(set(group.schools)) or set(group.schools) != wanted[group.key]:
            raise ValueError(f"danh sách trường của nhóm {group.key} không khớp dữ liệu")

    text = " ".join(_texts(result))
    extra = sorted(set(numbers(text)) - allowed_numbers(req))
    if extra:
        raise ValueError(f"lời nhắc số không có trong kết quả: {extra}")

    # Bỏ các tên trường hợp lệ rồi tìm tên trường khác của kết quả: nhắc tới là sai (có thể là nhầm nhóm hoặc bịa)
    rest = text.lower()
    for name in sorted({s.name for g in req.groups for s in g.schools}, key=len, reverse=True):
        rest = rest.replace(name.lower(), " ")
    strangers = [name for name in req.others if name.lower() in rest]
    if strangers:
        raise ValueError(f"lời nhắc tên trường ngoài danh sách: {strangers[:3]}")
    return result


# Nối các phần Gemini viết thành tin nhắn: lời mở, mỗi nhóm một dòng, lời kết
def render(result: ScenarioNoteResult, req: ScenarioNoteRequest) -> str:
    titles = {g.key: g.title for g in req.groups}
    lines = [result.intro.strip()]
    if result.groups:
        lines.append("")
        for group in result.groups:
            note = f" {group.note.strip()}" if group.note.strip() else ""
            lines.append(f"- {titles[group.key]}: {', '.join(group.schools)}.{note}")
    if result.closing.strip():
        lines += ["", result.closing.strip()]
    return "\n".join(lines)


# Một lượt AI: gửi kết quả cho Gemini, kiểm tra lời, đóng gói trả về
class ScenarioWriter:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def write(self, req: ScenarioNoteRequest) -> ScenarioNoteResponse:
        result = await self._call.run(
            build_user(req), scenario_note_schema(), lambda raw: check_answer(raw, req)
        )
        return ScenarioNoteResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if result is not None else "FAILED",
            answer=render(result, req) if result else "",
        )
