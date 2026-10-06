"""Hybrid: CRM bên .NET đã lọc và chia nhóm trường, LLM chỉ xếp lại trong danh sách đó và viết lý do.

Phép so sánh số (SAT, GPA, chi phí) do code tính sẵn thành "facts", LLM chỉ diễn đạt lại:
model 8B tự so sánh số hay sai (đã gặp: "SAT 1380 vượt mốc 75% (1450)").
Kết quả còn được .NET kiểm tra lần nữa (AiOutputGuard): mã trường, con số, độ dài lý do.
"""

import json
import logging
from typing import Any, Protocol

# Kiểm tra JSON mà MLL trả về có đúng form k
from pydantic import ValidationError

from app.schemas.recommendation import AiPick, AiRankRequest, AiRankResponse, AiSchoolInput, AiStudentInput

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """Bạn là tư vấn viên du học Mỹ, nói chuyện trực tiếp với học sinh.
Hệ thống đã lọc sẵn danh sách trường và chia nhóm: reach (khó vào), match (vừa sức), safety (an toàn),
insufficient_data (chưa đủ dữ liệu). Mỗi trường có "facts": các so sánh hệ thống đã tính sẵn, luôn đúng.

Nhiệm vụ:
1. Xếp lại thứ tự các trường TRONG TỪNG NHÓM: trường hợp với hồ sơ hơn đứng trước. Không đổi nhóm.
2. Mỗi trường viết đúng 1 câu tiếng Việt tự nhiên (khoảng 25 từ), xưng "bạn", nêu 1-2 ý quan trọng nhất
   trong facts của trường đó. Nếu có điểm bất lợi (vượt ngân sách, chưa đạt tiếng Anh) thì phải nhắc.

Quy tắc bắt buộc:
- Trả về đủ mọi trường, mỗi trường đúng 1 lần, giữ nguyên mã trường (code).
- Chỉ dùng con số có trong facts và hồ sơ, viết đúng như dữ liệu (vd. 57,168 USD).
  Không tự tính ra số mới, không nêu năm, thứ hạng hay số thứ tự. Không nói ngược ý facts.
- Không nói chắc chắn đậu hay trượt, không đoán tỉ lệ đậu.
- Không dùng thông tin ngoài dữ liệu được cho (danh tiếng, xếp hạng, thành phố...)."""

_ENGLISH_TEXT = {
    "met": "đạt yêu cầu tiếng Anh",
    "below_min": "chưa đạt yêu cầu tiếng Anh tối thiểu",
    "no_score": "bạn chưa có điểm tiếng Anh",
    "unknown": "trường không công bố yêu cầu tiếng Anh",
}


# Protocol mô tả hợp đồng
class JsonLLM(Protocol):
    # class nào mà muốn được coi là JsonLLM thì phải chứa chat_json (giống interface)
    async def chat_json(
        self, messages: list[dict[str, str]], schema: dict[str, Any], temperature: float = 0.0
    ) -> dict[str, Any]: ...


class RecommenderError(Exception):
    """LLM trả kết quả không dùng được."""


class Recommender:
    def __init__(seft, llm: JsonLLM) -> None:
        seft._llm = llm

    async def rank(self, request: AiRankRequest) -> AiRankResponse:
        codes = [s.code for s in request.schools]
        messages = [
            # Vai trò và luật cho QWen
            {"role": "system", "content": SYSTEM_PROMPT},
            # Đây là yêu cầu dữ liệu
            {"role": "user", "content": build_user_prompt(request)},
        ]
        # OllamaError (Ollama tắt, quá thời gian) không bắt ở đây: để lỗi đi thẳng lên route
        raw = await self._llm.chat_json(messages, build_output_schema(codes))
        try:
            # Kiểm tra xem raw có đúng cấu trúc k
            items = AiRankResponse.model_validate(raw).items
        except ValidationError as ex:
            raise RecommenderError(f"LLM trả sai định dạng: {ex}") from ex

        picks = keep_valid_picks(items, codes)

        # Nếu rỗng
        if not picks:
            raise RecommenderError("LLM không trả về trường nào hợp lệ")
        if len(picks) < len(codes):
            # Không bỏ cả kết quả: .NET (AiOutputGuard) tự thêm trường bị thiếu với lý do soạn sẵn
            logger.warning("LLM bỏ sót %d/%d trường", len(codes) - len(picks), len(codes))
        return AiRankResponse(items=picks)


# Bỏ mã k có trong đầu vào và lăp
def keep_valid_picks(items: list[AiPick], codes: list[str]) -> list[AiPick]:
    allowed = set(codes)
    seen: set[str] = set()
    picks = []
    for item in items:
        if item.code in allowed and item.code not in seen:
            seen.add(item.code)
            picks.append(item)
    return picks


# Tạo prompt rõ ràng để gửi QWen
def build_user_prompt(request: AiRankRequest) -> str:
    schools = []

    for s in request.schools:
        school = {
            "code": s.code,
            "name": s.name,
            "state": s.state,
            "category": s.category,
            # Sự thật khi so hs với trường
            "facts": school_facts(request.student, s),
        }

        schools.append(school)

    return (
        f"Hồ sơ học sinh: {describe_student(request.student)}\n\n"
        f"Danh sách trường (JSON):\n{json.dumps(schools, ensure_ascii=False)}"
    )


def describe_student(st: AiStudentInput) -> str:
    parts = [f"bậc {st.study_level}"]
    if st.major:
        parts.append(f"ngành {st.major}")
    if st.gpa4 is not None:
        parts.append(f"GPA {_num(st.gpa4)}/4")
    if st.sat is not None:
        parts.append(f"SAT {st.sat}")
    for label, value in (("IELTS", st.ielts), ("TOEFL", st.toefl), ("Duolingo", st.duolingo)):
        if value is not None:
            parts.append(f"{label} {_num(value)}")
    if st.extracurricular_score is not None:
        parts.append(f"ngoại khóa {_num(st.extracurricular_score)}/10")
    if st.annual_budget_usd is not None:
        parts.append(f"ngân sách {_money(st.annual_budget_usd)}/năm")
    return ", ".join(parts)


def school_facts(st: AiStudentInput, sc: AiSchoolInput) -> list[str]:
    """So sánh hồ sơ với một trường bằng code (luôn đúng). Thiếu dữ liệu thì bỏ qua ý đó."""
    facts = []
    if st.sat is not None and sc.sat25 is not None and sc.sat75 is not None:
        if st.sat > sc.sat75:
            where = f"cao hơn mốc 75% ({sc.sat75})"
        elif st.sat < sc.sat25:
            where = f"thấp hơn mốc 25% ({sc.sat25})"
        else:
            where = f"trong khoảng mốc 25%-75% ({sc.sat25}-{sc.sat75})"
        facts.append(f"SAT {st.sat} {where}")

    if st.gpa4 is not None and sc.avg_gpa4 is not None:
        compare = "cao hơn" if st.gpa4 > sc.avg_gpa4 else "thấp hơn" if st.gpa4 < sc.avg_gpa4 else "bằng"
        facts.append(f"GPA {_num(st.gpa4)} {compare} GPA trung bình {_num(sc.avg_gpa4)}")

    if sc.total_cost_usd is not None:
        cost = f"chi phí {_money(sc.total_cost_usd)}/năm"
        if st.annual_budget_usd is not None:
            within = sc.total_cost_usd <= st.annual_budget_usd
            cost += f", {'trong' if within else 'vượt'} ngân sách {_money(st.annual_budget_usd)}"
        facts.append(cost)

    facts.append(_ENGLISH_TEXT[sc.english])
    return facts


def build_output_schema(codes: list[str]) -> dict[str, Any]:
    """Khuôn JSON gửi cho Ollama: code chỉ được là mã có trong đầu vào, số phần tử đúng bằng số trường."""
    return {
        "type": "object",
        "properties": {
            "items": {
                "type": "array",
                "minItems": len(codes),
                "maxItems": len(codes),
                "items": {
                    "type": "object",
                    "properties": {
                        "code": {"type": "string", "enum": codes},
                        "reason": {"type": "string"},
                    },
                    "required": ["code", "reason"],
                },
            }
        },
        "required": ["items"],
    }


def _num(value: float) -> str:
    """3.6 -> "3.6", 7.0 -> "7" (AiOutputGuard bên .NET đọc được cả hai)."""
    return str(int(value)) if float(value).is_integer() else str(value)


def _money(value: float) -> str:
    """57168.0 -> "57,168 USD": đúng định dạng AiOutputGuard đọc được."""
    return f"{int(value):,} USD" if float(value).is_integer() else f"{value} USD"
