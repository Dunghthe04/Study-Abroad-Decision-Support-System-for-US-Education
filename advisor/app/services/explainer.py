"""AI tư vấn từng trường: đọc số liệu hệ thống đã chấm, viết lời giải thích và lời khuyên theo skill_explain.md.

Model nhỏ hay so sánh số sai (ví dụ nói SAT 1400 "cao hơn mốc 75% là 1410"), nên Python so sánh trước và gửi kèm
các nhận định đã đúng ("facts"); AI chỉ diễn đạt lại, không phải tự so sánh.
"""

from pathlib import Path
from typing import Any

from app.schemas.explain import ExplainRequest, ExplainResponse, ExplainResult, explain_schema
from app.services.skill_call import SKILLS_DIR, SkillCall

SKILL_PATH = SKILLS_DIR / "skill_explain.md"


class ExplainError(Exception):
    """AI trả sai định dạng 2 lần liền."""


def _usd(v: float) -> str:
    return f"{v:,.0f}".replace(",", ".") + " USD"


def build_facts(req: ExplainRequest) -> list[str]:
    """Các nhận định so sánh đã tính sẵn bằng code, đúng tuyệt đối."""
    st, sc = req.student, req.school
    facts: list[str] = []

    if st.gpa is not None and sc.gpa_position is not None:
        p = round(sc.gpa_position)
        level = "cao hơn phần lớn" if p > 75 else "ở khoảng giữa" if p >= 25 else "thấp hơn phần lớn"
        facts.append(f"GPA {st.gpa} {level} sinh viên trúng tuyển: cao hơn khoảng {p}% sinh viên của trường")
    if st.sat is not None and sc.sat25 is not None and sc.sat75 is not None:
        if st.sat < sc.sat25:
            facts.append(
                f"SAT {st.sat} thấp hơn mốc 25% của trường ({sc.sat25}), thiếu khoảng {sc.sat25 - st.sat} điểm"
            )
        elif st.sat > sc.sat75:
            facts.append(f"SAT {st.sat} cao hơn mốc 75% của trường ({sc.sat75})")
        else:
            facts.append(f"SAT {st.sat} nằm trong khoảng 25–75 của trường ({sc.sat25}–{sc.sat75})")
    elif st.sat is None:
        facts.append("Học sinh chưa có SAT; kết quả chỉ dựa vào GPA")
    if st.ec_tier is not None:
        tier = {1: "rất mỏng", 2: "dưới trung bình", 3: "trung bình", 4: "khá", 5: "nổi bật"}[st.ec_tier]
        adj = sc.ec_adjustment or 0
        effect = "là lợi thế" if adj > 0 else "làm hồ sơ yếu đi" if adj < 0 else "không ảnh hưởng"
        facts.append(f"Ngoại khóa ở mức {tier} (bậc {st.ec_tier}/5), ở trường này {effect}")
    if sc.cost is not None:
        if sc.need_aid and sc.over_budget:
            facts.append(
                f"Chi phí {_usd(sc.cost)}/năm, vượt ngân sách {_usd(sc.over_budget)}/năm: cần học bổng lớn"
            )
        else:
            facts.append(f"Chi phí {_usd(sc.cost)}/năm, nằm trong ngân sách")
    if sc.group == "Khó vào xa":
        facts.append("Trường nhận rất ít du học sinh so với tỉ lệ nhận chung")
    return facts


class Explainer:
    def __init__(self, llm: Any, model: str, skill_path: Path = SKILL_PATH) -> None:
        self._call = SkillCall(llm, model, skill_path)
        self.skill_version = self._call.skill_version

    async def explain(self, req: ExplainRequest) -> ExplainResponse:
        # Thiếu số thì bỏ key, AI không nhắc tới (skill: key không có = chưa có dữ liệu)
        user = req.model_dump(exclude_none=True)
        user["facts"] = build_facts(req)
        result = await self._call.run(user, explain_schema(), ExplainResult.model_validate)
        if result is None:
            raise ExplainError("AI chưa viết được lời tư vấn, hãy thử lại.")
        return ExplainResponse(
            **result.model_dump(), skill_version=self.skill_version, model=self._call.model
        )
