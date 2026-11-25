"""Tư vấn từng trường: Qwen viết lời từ số đã lưu. Không chấm lại, không đổi nhóm."""

from pathlib import Path
from typing import Any

from app.schemas.explain import (
    ExplainFit,
    ExplainRequest,
    ExplainResponse,
    ExplainResult,
    ExplainSchool,
    ExplainStudent,
    explain_schema,
)
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = Path(SKILLS_DIR) / "skill_explain.md"

GROUP_LABEL = {
    "R": "Khó vào",
    "RF": "Khó vào xa",
    "M": "Vừa sức",
    "S": "An toàn hơn",
    "NA": "Chưa đủ dữ liệu",
    "O": "Nhận gần như tất cả",
}


# Chuyển đổi tiền USD sang định dạng có dấu chấm phân cách hàng nghìn
def money(value: float | None) -> str:
    return f"{value:,.0f}".replace(",", ".") if value is not None else "?"


# Tên thành phần điểm phù hợp bằng lời học sinh hiểu (UC-24)
FIT_PART_LABEL = {
    "position": "vừa tầm học thuật",
    "budget": "ngân sách",
    "quality": "chất lượng trường",
    "aid": "học bổng",
    "english": "tiếng Anh",
}
SCORE_SCALE = 100  # điểm hiện theo thang 100 như thẻ giải thích


def _points(value: float) -> int:
    return round(value * SCORE_SCALE)


# Điểm phù hợp để gửi Qwen: tổng, từng thành phần (điểm và trọng số) và thành phần thấp nhất do CODE tìm sẵn,
# để AI không tự so sánh. Thành phần thiếu dữ liệu bị bỏ khỏi tổng nên không tính là thấp nhất.
def fit_payload(fit: ExplainFit) -> dict[str, Any]:
    parts = [
        {
            "name": FIT_PART_LABEL[p.key],
            "score": _points(p.score) if p.score is not None else None,
            "weight": round(p.weight, 2),
        }
        for p in fit.parts
    ]
    scored = [p for p in parts if p["score"] is not None]
    payload: dict[str, Any] = {
        "total": _points(fit.total) if fit.total is not None else None,
        "parts": parts,
        "missing": [p["name"] for p in parts if p["score"] is None],
        "priorityAdjusted": fit.priority_adjusted,
    }
    if scored:
        low = min(p["score"] for p in scored)
        payload["lowest"] = [p["name"] for p in scored if p["score"] == low]
    return payload


# Qwen dựa vào câu mẫu này để viết lời giải thích cho từng trường, dựa vào số đã lưu trong cơ sở dữ liệu.
def fact_of(code: str, st: ExplainStudent, sc: ExplainSchool) -> str:
    """Đổi một mã lý do thành câu đúng. Qwen chỉ diễn đạt lại, không so sánh."""
    gpa = f"GPA {st.gpa}" if st.gpa is not None else "GPA"
    sat = f"SAT {st.sat}" if st.sat is not None else "SAT"
    pos = f": cao hơn khoảng {sc.pg:.0f}% sinh viên của trường" if sc.pg is not None else ""
    if code == "GPA_HIGH":
        return f"{gpa} cao hơn mặt bằng sinh viên trúng tuyển{pos}"
    if code == "GPA_MID":
        return f"{gpa} nằm trong mặt bằng sinh viên trúng tuyển{pos}"
    if code == "GPA_LOW":
        return f"{gpa} thấp hơn phần lớn sinh viên trúng tuyển{pos}"
    if code == "SAT_ABOVE":
        mark = f" ({sc.sat75})" if sc.sat75 is not None else ""
        return f"{sat} cao hơn mốc 75% của trường{mark}"
    if code == "SAT_IN_RANGE":
        if sc.sat25 is not None and sc.sat75 is not None:
            return f"{sat} nằm trong khoảng {sc.sat25}–{sc.sat75} của trường"
        return f"{sat} nằm trong khoảng SAT của trường"
    if code == "SAT_BELOW":
        if sc.sat25 is not None and st.sat is not None and sc.sat25 > st.sat:
            return f"{sat} thấp hơn mốc 25% của trường ({sc.sat25}), thiếu khoảng {sc.sat25 - st.sat} điểm"
        return f"{sat} thấp hơn mốc 25% của trường"
    if code == "BUDGET_OK":
        return f"Chi phí {money(sc.cost)} USD/năm, nằm trong ngân sách"
    if code == "NEED_AID":
        return f"Chi phí {money(sc.cost)} USD/năm, vượt ngân sách {money(sc.gap)} USD/năm: cần học bổng lớn"
    if code == "INTL_LOW":
        return "Trường nhận rất ít du học sinh so với tỉ lệ nhận chung"
    if code == "EC_PLUS":
        return "Ngoại khóa của bạn là lợi thế ở trường này"
    if code == "EC_MINUS":
        return "Ngoại khóa của bạn cần bổ sung cho trường này"
    if code == "LOW_RATE":
        return "Trường nhận dưới 15% hồ sơ nên luôn xếp Khó vào, điểm cao đến đâu cũng không đổi nhóm"
    if code == "RATE_CAP":
        return "Trường nhận dưới 50% hồ sơ nên chỉ tối đa là Vừa sức, chưa tính là An toàn"
    if code == "WEAK_CAP":
        return "Có một chỉ số của bạn thấp hơn mốc 25% của trường nên chưa tính là An toàn"
    return ""


# Xây dựng danh sách lý do cho từng trường
def build_facts(req: ExplainRequest) -> list[str]:
    facts = []

    for code in req.school.why:
        fact = fact_of(code, req.student, req.school)

        if fact:
            facts.append(fact)

    return facts


# Gom dữ liệu thành đúng 1 JSON để gửi QWEN
def build_user(req: ExplainRequest) -> dict[str, Any]:
    st, sc = req.student, req.school
    school: dict[str, Any] = {
        "name": sc.name,
        "state": sc.state,
        "control": sc.control,
        "group": GROUP_LABEL[sc.g2],  # mã g2 → nhãn tiếng Việt
        "needAid": sc.blk == "AID",  # True nếu trường nằm ở khối học bổng
        "acceptance": round(sc.acc * 100, 1) if sc.acc is not None else None,  # 0–1 → %
        "sat25": sc.sat25,
        "sat75": sc.sat75,
        "cost": sc.cost,
        "gap": sc.gap,
    }
    user: dict[str, Any] = {
        "student": st.model_dump(exclude_none=True),
        "school": {k: v for k, v in school.items() if v is not None},
        "facts": build_facts(req),
    }
    if sc.fit is not None:
        user["fit"] = fit_payload(sc.fit)
    if req.basis:
        user["basis"] = req.basis
    return user


# Một lượt AI: gửi JSON cho Qwen, kiểm tra kết quả, đóng gói trả về
class Explainer:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def explain(self, req: ExplainRequest) -> ExplainResponse:
        result = await self._call.run(
            build_user(req), explain_schema(), lambda raw: ExplainResult.model_validate(raw)
        )
        return ExplainResponse(
            skill_version=self.skill_version,
            model=self._call.model,
            status="OK" if result is not None else "FAILED",
            summary=result.summary if result else "",
            strengths=result.strengths if result else [],
            improvements=result.improvements if result else [],
        )
