"""skill_explain: học sinh đã có danh sách trường gợi ý và hỏi về một trường trong đó (vì sao nhóm khó vào, có nên nộp...).

Skill chỉ xác định trường nào, rồi báo .NET (action "explain" kèm mã trường) chạy phần tư vấn từng trường đã có sẵn
(Explainer đọc số đã lưu). AI ở đây không giải thích và không chấm lại, nên không thể bịa số hay đổi nhóm.
"""

import logging
from typing import Any

from app.schemas.chat import ChatState, QuickReply, ResultSchool
from app.services.chat_flow import SkillOutput
from app.services.explainer import GROUP_LABEL
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.explain")

PICK_PATH = SKILLS_DIR / "skill_chon_truong.md"
PICK_SCHEMA = {
    "type": "object",
    "properties": {"code": {"type": ["string", "null"]}},
    "required": ["code"],
}

EXPLAIN_ACTION = "explain"
# .NET thay câu này bằng lời tư vấn thật; giữ lại để lượt chat luôn có câu trả lời nếu .NET không xử lý được
WORKING = "Mình xem trường {name} giúp bạn nhé."
ASK_SCHOOL = "Bạn muốn mình giải thích trường nào trong danh sách?"
NO_LIST = "Mình chưa có danh sách trường gợi ý nào để giải thích. Bạn cho mình gợi ý trường trước nhé."
MAX_BUTTONS = 6  # danh sách có thể 20 trường, nút chỉ hiện vài trường đầu


# Mã AI chọn phải nằm trong danh sách (AI đoán bừa mã lạ thì ném ValueError để SkillCall gọi lại); None = không rõ trường nào
def check_pick(raw: dict[str, Any], schools: list[ResultSchool]) -> str | None:
    code = raw.get("code") or None
    if code is None:
        return None
    by_lower = {s.code.lower(): s.code for s in schools}
    if str(code).lower() not in by_lower:
        raise ValueError(f"mã trường lạ: {code}")
    return by_lower[str(code).lower()]


# Học sinh gõ đúng tên một trường (kể cả khi bấm nút) thì khỏi hỏi AI. Trùng nhiều tên thì để AI quyết
def match_by_name(message: str, schools: list[ResultSchool]) -> ResultSchool | None:
    lowered = message.lower()
    hits = [s for s in schools if s.name.lower() in lowered]
    return hits[0] if len(hits) == 1 else None


def school_buttons(schools: list[ResultSchool]) -> list[QuickReply]:
    return [
        QuickReply(label=s.name, value=f"Giải thích giúp mình trường {s.name}") for s in schools[:MAX_BUTTONS]
    ]


class ExplainHandler:
    def __init__(self, llm: Any, model: str) -> None:
        self._pick = SkillCall(llm, model, PICK_PATH)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        schools = state.schools
        if not schools:
            return SkillOutput(NO_LIST)

        chosen = match_by_name(message, schools)
        if chosen is None:
            payload = {
                "message": message,
                "schools": [
                    {"code": s.code, "name": s.name, "group": GROUP_LABEL.get(s.group or "", s.group)}
                    for s in schools
                ],
            }
            code = await self._pick.run(payload, PICK_SCHEMA, lambda raw: check_pick(raw, schools))
            chosen = next((s for s in schools if s.code == code), None)

        # Không rõ trường nào (hoặc AI sai hai lần): hỏi lại bằng nút, bấm là gửi lại một câu có đúng tên trường
        if chosen is None:
            return SkillOutput(ASK_SCHOOL, quick_replies=school_buttons(schools))
        return SkillOutput(
            WORKING.format(name=chosen.name), action=EXPLAIN_ACTION, action_data={"code": chosen.code}
        )
