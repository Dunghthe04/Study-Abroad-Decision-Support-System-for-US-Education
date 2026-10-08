"""Hybrid: CRM bên .NET đã lọc và chia nhóm trường, LLM chỉ xếp lại trong danh sách đó và viết lý do.

Số liệu phải đúng, lời văn được tự do:
- Phép so sánh (SAT, GPA, ngân sách) do code tính (fact_check.relations), LLM chỉ diễn đạt lại.
- Lý do nào nói ngược phép so sánh thì bị bỏ (fact_check.contradictions), .NET thay bằng câu soạn sẵn.
- .NET kiểm tra thêm lần nữa (AiOutputGuard): mã trường, con số, độ dài lý do.
"""

import json
import logging
from typing import Any, Protocol

# Kiểm tra JSON mà MLL trả về có đúng form k
from pydantic import ValidationError

from app.schemas.recommendation import AiPick, AiRankRequest, AiRankResponse, AiSchoolInput, AiStudentInput
from app.services.fact_check import (
    EXTRACURRICULAR_LEVEL_TEXT,
    contradictions,
    cost_ranks,
    extracurricular_level,
    relations,
)

logger = logging.getLogger(__name__)

SYSTEM_PROMPT = """Bạn là tư vấn viên du học Mỹ giàu kinh nghiệm, đang giải thích danh sách trường gợi ý cho một học sinh Việt Nam.
Hệ thống đã lọc sẵn và chia nhóm, gọi đúng tên nhóm khi viết: reach = "thử sức", match = "vừa sức", safety = "an toàn",
insufficient_data = "chưa đủ dữ liệu" (TRƯỜNG chưa công bố đủ SAT/GPA để xếp nhóm, không phải do học sinh).
Mỗi trường có các so sánh hệ thống đã tính sẵn (khóa "so_sanh"), luôn đúng: chỉ diễn đạt lại, tuyệt đối không nói ngược.

Nhiệm vụ:
1. Xếp lại thứ tự các trường TRONG TỪNG NHÓM: trường hợp với hồ sơ hơn đứng trước. Không đổi nhóm.
2. Mỗi trường viết 3 câu tiếng Việt tự nhiên, xưng "bạn", lần lượt theo 3 tiêu chí:
   - Học thuật: SAT/GPA của bạn so với sinh viên trúng tuyển, và vì sao trường thuộc nhóm này.
     Chỉ nhắc GPA khi trường có khóa "gpa"; chỉ nhắc SAT khi trường có khóa "sat".
   - Tài chính: chi phí so với ngân sách, trường rẻ nhất/đắt nhất nhóm nếu có, hoặc chi phí chưa có dữ liệu.
   - Ngoại khóa: dựa vào mức ngoại khóa trong hồ sơ (mạnh = lợi thế, còn mỏng = cần bổ sung), kèm một lời khuyên cụ thể;
     hồ sơ chưa có ngoại khóa thì khuyên bổ sung.
     Chỉ nhắc tiếng Anh khi khóa "tieng_anh" cho biết bạn CHƯA đạt yêu cầu.
   Không lặp lại cùng một mẫu câu giữa các trường, không chép nguyên câu của ví dụ.
   Mỗi lý do viết trên một dòng, dưới 340 ký tự.

Ví dụ cách viết (trường giả; KHÔNG dùng lại con số của ví dụ):
- "Điểm SAT 1250 của bạn còn dưới mốc 25% (1300) của sinh viên trúng tuyển, nên đây là lựa chọn để thử sức. Chi phí 45,000 USD/năm vẫn nằm gọn trong ngân sách. Điểm ngoại khóa 3.5/4 là lợi thế, hãy dùng nó làm chủ đề bài luận."
- "Với SAT 1300 nằm trong khoảng 25%-75% (1200-1400), đây là lựa chọn vừa sức với bạn. Đây cũng là trường có chi phí thấp nhất nhóm. Hồ sơ ngoại khóa còn mỏng, bạn nên bổ sung một dự án dài hạn."

Quy tắc bắt buộc:
- Trả về đủ mọi trường, mỗi trường đúng 1 lần, giữ nguyên mã trường (code).
- Chỉ dùng con số có trong dữ liệu được cho, viết đúng như dữ liệu (vd. 57,168 USD).
  Không tự tính ra số mới (chênh lệch, phần trăm), không nêu năm, thứ hạng hay số thứ tự.
- Không nói chắc chắn đậu hay trượt, không đoán tỉ lệ đậu.
- Không dùng thông tin ngoài dữ liệu được cho (danh tiếng, xếp hạng, thành phố...)."""
TEMPERATURE = 0.3  # hơi ngẫu nhiên để lời văn đa dạng; số liệu đã có fact_check và AiOutputGuard giữ đúng
# Ollama cắt cứng lý do ở độ dài này: chặn LLM viết lan man làm chậm (12 trường, khoảng 15 token/giây)
MAX_REASON_CHARS = 380

_SAT_TEXT = {"above": "cao hơn mốc 75%", "below": "thấp hơn mốc 25%", "within": "nằm trong khoảng 25%-75%"}
_GPA_TEXT = {"above": "cao hơn", "below": "thấp hơn", "equal": "bằng"}
_ENGLISH_TEXT = {
    "met": "bạn đạt yêu cầu tiếng Anh của trường",
    "below_min": "bạn chưa đạt yêu cầu tiếng Anh tối thiểu",
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
        raw = await self._llm.chat_json(messages, build_output_schema(codes), TEMPERATURE)

        try:
            items = AiRankResponse.model_validate(raw).items
        except ValidationError as ex:
            raise RecommenderError(f"LLM trả sai định dạng: {ex}") from ex

        ranks = cost_ranks(request.schools)
        truths = {
            s.code: {**relations(request.student, s), "cost_rank": ranks[s.code]} for s in request.schools
        }
        picks = []
        for pick in keep_valid_picks(items, codes):
            pick = pick.model_copy(update={"reason": clean_reason(pick.reason)})
            wrong = contradictions(pick.reason, truths[pick.code])
            if wrong:
                # Bỏ lý do sai: .NET (AiOutputGuard) tự thêm lại trường này với lý do soạn sẵn
                logger.warning("Bỏ lý do nói ngược số liệu %s %s: %s", pick.code, wrong, pick.reason)
                continue
            picks.append(pick)

        if not picks:
            raise RecommenderError("LLM không trả về lý do nào dùng được")
        if len(picks) < len(codes):
            logger.warning(
                "Thiếu %d/%d trường, .NET sẽ bổ sung lý do soạn sẵn", len(codes) - len(picks), len(codes)
            )
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


def clean_reason(text: str) -> str:
    """Giữ dòng đầu (LLM đôi khi viết thêm một bản nữa ở dòng sau) và bỏ câu cuối bị cắt dở vì giới hạn độ dài."""
    line = text.strip().split("\n")[0].strip()
    if line and line[-1] not in ".!?":
        end = line.rfind(". ")
        if end > 0:
            line = line[: end + 1]
    return line


def build_user_prompt(request: AiRankRequest) -> str:
    notes = group_notes(request)
    schools = [school_data(request.student, s, notes[s.code]) for s in request.schools]
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
        level = EXTRACURRICULAR_LEVEL_TEXT[extracurricular_level(st.extracurricular_score)]
        parts.append(f"ngoại khóa {_num(st.extracurricular_score)}/4 (mức {level})")
    if st.annual_budget_usd is not None:
        parts.append(f"ngân sách {_money(st.annual_budget_usd)}/năm")
    return ", ".join(parts)


def school_data(st: AiStudentInput, sc: AiSchoolInput, notes: list[str]) -> dict[str, Any]:
    """Dữ liệu một trường gửi cho LLM: số liệu kèm phép so sánh đã tính ("so_sanh" đặt đầu để LLM đọc trước)."""
    rel = relations(st, sc)
    data: dict[str, Any] = {"code": sc.code, "name": sc.name, "state": sc.state, "category": sc.category}
    if "sat" in rel:
        data["sat"] = {
            "so_sanh": _SAT_TEXT[rel["sat"]],
            "cua_ban": st.sat,
            "moc_25": sc.sat25,
            "moc_75": sc.sat75,
        }
    if "gpa" in rel:
        data["gpa"] = {
            "so_sanh": _GPA_TEXT[rel["gpa"]],
            "cua_ban": _num(st.gpa4),
            "trung_binh": _num(sc.avg_gpa4),
        }
    if sc.total_cost_usd is None:
        data["chi_phi"] = "chưa có dữ liệu, cần hỏi trường"
    else:
        data["chi_phi"] = {"moi_nam": _money(sc.total_cost_usd)}
        if "budget" in rel:
            data["chi_phi"]["so_sanh"] = "trong ngân sách" if rel["budget"] == "within" else "vượt ngân sách"
    data["tieng_anh"] = _ENGLISH_TEXT[sc.english]
    if notes:
        data["noi_bat"] = notes
    return data


def group_notes(request: AiRankRequest) -> dict[str, list[str]]:
    """Điểm nổi bật trong nhóm (chi phí thấp nhất / cao nhất): luôn đúng, không sinh số mới, giúp mỗi trường một ý riêng."""
    texts = {"cheapest": "chi phí thấp nhất trong nhóm", "priciest": "chi phí cao nhất trong nhóm"}
    ranks = cost_ranks(request.schools)
    return {
        s.code: [f"{texts[ranks[s.code]]} {s.category}"] if ranks[s.code] in texts else []
        for s in request.schools
    }


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
                        "reason": {"type": "string", "maxLength": MAX_REASON_CHARS},
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
