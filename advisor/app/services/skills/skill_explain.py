"""skill_explain: học sinh đã có danh sách trường gợi ý và hỏi về một trường trong đó (vì sao nhóm khó vào, có nên nộp...).

Skill chỉ xác định trường nào, rồi báo .NET (action "explain" kèm mã trường) chạy phần tư vấn từng trường đã có sẵn
(Explainer đọc số đã lưu). AI ở đây không giải thích và không chấm lại, nên không thể bịa số hay đổi nhóm.
"""

import logging
import re
import unicodedata
from typing import Any

from app.schemas.chat import ChatState, QuickReply, ResultSchool
from app.services.chat_flow import SkillOutput
from app.services.explainer import GROUP_LABEL
from app.services.skill_call import SKILLS_DIR, SkillCall

logger = logging.getLogger("advisor.explain")

PICK_PATH = SKILLS_DIR / "skill_chon_truong.md"
PICK_SCHEMA = {
    "type": "object",
    "properties": {"code": {"type": ["string", "null"]}, "words": {"type": ["string", "null"]}},
    "required": ["code", "words"],
}

EXPLAIN_ACTION = "explain"
# .NET thay câu này bằng lời tư vấn thật; giữ lại để lượt chat luôn có câu trả lời nếu .NET không xử lý được
WORKING = "Mình xem trường {name} giúp bạn nhé."
ASK_SCHOOL = "Bạn muốn mình giải thích trường nào trong danh sách?"
NO_LIST = "Mình chưa có danh sách trường gợi ý nào để giải thích. Bạn cho mình gợi ý trường trước nhé."
MAX_BUTTONS = 6  # danh sách có thể 20 trường, nút chỉ hiện vài trường đầu


# Chữ chung của tên trường, không đủ để chứng minh học sinh nhắc đúng trường đó
COMMON_WORDS = {"university", "of", "the", "state", "college", "institute", "technology", "truong", "and"}
# Vị trí học sinh hay nói: số thứ tự và "cuối"
ORDINALS = {
    "nhat": 1,
    "hai": 2,
    "ba": 3,
    "tu": 4,
    "bon": 4,
    "nam": 5,
    "sau": 6,
    "bay": 7,
    "tam": 8,
    "chin": 9,
    "muoi": 10,
}
_POSITION_RE = re.compile(r"\b(?:thu|so)\s*(\d+|[a-z]+)\b")
_FIRST_RE = re.compile(r"\bdau tien\b|\bdau danh sach\b")
_LAST_RE = re.compile(r"\bcuoi cung\b|\bcuoi danh sach\b")


# Bỏ dấu, chữ thường: "Trường thứ ba" và "truong thu ba" như nhau
def plain(text: str) -> str:
    text = text.lower().replace("đ", "d")
    return "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn")


def tokens(text: str) -> set[str]:
    return set(re.findall(r"[a-z0-9]+", plain(text)))


# AI phải chỉ ra chữ học sinh viết để gọi trường đó (words). Chữ đó phải có trong tin nhắn và khớp tên hoặc mã của
# trường AI chọn: AI hay đoán một trường nổi tiếng khi học sinh hỏi trường không có trong danh sách (Harvard -> MIT)
# Mã AI chọn phải nằm trong danh sách; sai thì ném ValueError để SkillCall gọi lại. None = không rõ trường nào
def check_pick(raw: dict[str, Any], message: str, schools: list[ResultSchool]) -> str | None:
    code = raw.get("code") or None
    if code is None:
        return None
    by_lower = {s.code.lower(): s for s in schools}
    school = by_lower.get(str(code).lower())
    if school is None:
        raise ValueError(f"mã trường lạ: {code}")
    words = str(raw.get("words") or "").strip()
    if not words or plain(words) not in plain(message):
        raise ValueError("chữ AI nêu không có trong tin nhắn")
    proof = tokens(words) - COMMON_WORDS
    if not proof & (tokens(school.name) | {school.code.lower()}):
        raise ValueError(f"chữ AI nêu không khớp trường {school.code}")
    return school.code


# Gọi theo vị trí ("trường thứ ba", "trường đầu tiên", "trường cuối cùng"): code đếm, không để AI đếm (AI hay đếm sai)
def match_by_position(message: str, schools: list[ResultSchool]) -> ResultSchool | None:
    text = plain(message)
    if _LAST_RE.search(text):
        return schools[-1]
    if _FIRST_RE.search(text):
        return schools[0]
    found = _POSITION_RE.search(text)
    if found is None:
        return None
    word = found.group(1)
    number = int(word) if word.isdigit() else ORDINALS.get(word)
    return schools[number - 1] if number and number <= len(schools) else None


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

        chosen = match_by_name(message, schools) or match_by_position(message, schools)
        if chosen is None:
            payload = {
                "message": message,
                "schools": [
                    {"code": s.code, "name": s.name, "group": GROUP_LABEL.get(s.group or "", s.group)}
                    for s in schools
                ],
            }
            code = await self._pick.run(payload, PICK_SCHEMA, lambda raw: check_pick(raw, message, schools))
            chosen = next((s for s in schools if s.code == code), None)

        # Không rõ trường nào (hoặc AI sai hai lần): hỏi lại bằng nút, bấm là gửi lại một câu có đúng tên trường
        if chosen is None:
            return SkillOutput(ASK_SCHOOL, quick_replies=school_buttons(schools))
        return SkillOutput(
            WORKING.format(name=chosen.name), action=EXPLAIN_ACTION, action_data={"code": chosen.code}
        )
