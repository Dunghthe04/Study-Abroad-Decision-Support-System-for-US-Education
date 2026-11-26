"""Bộ phân loại SLM (Qwen3 4B qua Ollama): đọc tin nhắn, tách thành danh sách ý, mỗi ý có loại và thông tin tách được.

Đầu vào cùng dạng cột đầu vào trong slm/data/du_lieu_slm.jsonl ({"question", "profile", "pending_question", "context"}), đầu ra
theo SLM_TIN_SCHEMA ({"intents": [...]}). Nhờ vậy model chưa fine-tune, model đã fine-tune và dữ liệu huấn luyện dùng chung một
khuôn, đổi model chỉ cần đổi settings.slm_model. Hệ thống đọc đúng thứ model trả, không đổi tên khóa hay mã.
"""

import json
import logging
import math
import re
from pathlib import Path
from typing import Any

from app.schemas.chat import ChatState
from app.schemas.slm import PROFILE_KEYS, SLM_TIN_SCHEMA, SlmTinResult, SlmY
from app.services import usage_meter
from app.services.conversation_context import context_for_model
from app.services.ollama import OllamaError
from app.services.skill_call import SKILLS_DIR, load_skill

logger = logging.getLogger(__name__)

PROMPT_PATH = SKILLS_DIR / "slm_intents.md"


# Tên prompt (không có .md) thành đường dẫn; tên không có file thì báo ngay lúc khởi động
def prompt_path(name: str) -> Path:
    path = SKILLS_DIR / f"{name}.md"
    if not path.is_file():
        raise ValueError(f"không có prompt tách ý '{name}' trong {SKILLS_DIR}")
    return path


# Ollama lỗi (bận, hết giờ, trả nội dung không đọc được) thì gọi lại tối đa MAX_ATTEMPTS lần. Model trả sai hợp đồng thì
# KHÔNG gọi lại: nhiệt độ 0 và cùng prompt thì lần sau ra y hệt (đo lại 12 câu sai của usas-slm-v5: 12/12 giống nhau),
# gọi lại chỉ tốn thêm vài giây mỗi câu mà không cứu được câu nào
MAX_ATTEMPTS = 2
# Hết lượt mà vẫn sai (model trả sai luật, Ollama tắt): coi là tin mơ hồ, confidence 0 để chat hỏi lại học sinh
FALLBACK = SlmTinResult(
    intents=[SlmY(type="out_of_scope", span="tin không rõ", entities={"subtype": "ambiguous"})],
    confidence=0.0,
    fallback=True,
)
_TYPE_VALUE_RE = re.compile(r'"type"\s*:\s*"([^"]*)"')
_WORD_RE = re.compile(r"[^\W_]+(?:[-.'/][^\W_]+)*")
# Khóa mà giá trị phải là đoạn chữ nguyên văn trong tin nhắn (schools là danh sách, kiểm từng tên)
SPAN_KEYS = {*PROFILE_KEYS, "school", "city"}
# Điều kiện lọc trường có giá trị là một con số (tiền, điểm tiếng Anh, tỷ lệ, hạng)
NUMERIC_FILTER_KEYS = {
    "max_cost",
    "max_ielts",
    "max_toefl",
    "max_duolingo",
    "min_acceptance_rate",
    "max_acceptance_rate",
    "min_graduation_rate",
    "max_rank",
}


# Đầu vào của SLM, cùng khóa với cột đầu vào trong file dữ liệu
def slm_input(
    message: str, profile: dict[str, str] | None, pending: str, context: dict[str, Any] | None = None
) -> dict[str, Any]:
    data: dict[str, Any] = {"question": message, "profile": profile or None, "pending_question": pending}
    if context:  # chỉ thêm khi có, để câu không có ngữ cảnh giữ nguyên khuôn cũ
        data["context"] = context
    return data


# Hội thoại gửi cho SLM: system là prompt, user là JSON đầu vào viết liền.
# Dùng chung cho lúc chạy (OllamaSlm.read) và lúc xuất dữ liệu fine-tune, để model học đúng thứ nó sẽ nhận
def build_messages(
    prompt: str,
    message: str,
    profile: dict[str, str] | None,
    pending: str,
    context: dict[str, Any] | None = None,
) -> list[dict[str, str]]:
    user = slm_input(message, profile, pending, context)
    return [
        {"role": "system", "content": prompt},
        {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
    ]


# Làm sạch từng ý model trả về, thay vì bỏ cả câu trả lời:
#   entities: bỏ giá trị không có nguyên văn trong tin (model hay chép từ hồ sơ đầu vào, hoặc tự điền "null", "không có");
#     schools bỏ từng tên không có trong tin, hết tên thì bỏ cả khóa
#   filters: tin không có chữ số nào mà điều kiện lọc lại là một con số thì số đó do model bịa (vd "trường nào rẻ nhất" bị thêm
#     max_cost "1000000000"), bỏ điều kiện đó. Câu nối tiếp (follow_up) lấy điều kiện từ ngữ cảnh nên không kiểm
#   span: có chữ không nằm trong tin (hoặc rỗng) thì dùng nguyên tin thay cho đoạn đó
# Ý không phải object thì giữ nguyên để SlmTinResult.from_raw báo lỗi và gọi lại model
def keep_verbatim(raw: dict[str, Any], message: str, context_names: set[str] | None = None) -> dict[str, Any]:
    items = raw.get("intents")
    if not isinstance(items, list):
        return raw
    text, spoken = message.lower(), words(message)
    cleaned = []
    for item in items:
        if not isinstance(item, dict):
            cleaned.append(item)
            continue
        entities = item.get("entities") or {}
        # Câu tiếp nối: trường lấy từ ngữ cảnh không có trong tin nhưng hợp lệ nếu đúng tên trường đang nói tới
        carried = bool(entities.get("follow_up") is True and context_names)

        def spoken_school(value: Any, carried: bool = carried) -> bool:
            return isinstance(value, str) and (
                value.lower() in text or bool(carried and value.lower() in context_names)
            )

        kept: dict[str, Any] = {}
        for key, value in entities.items():
            if key == "schools":
                names = [name for name in value if spoken_school(name)] if isinstance(value, list) else []
                if names:
                    kept[key] = names
            elif key == "school":
                if spoken_school(value):
                    kept[key] = value
            elif key not in SPAN_KEYS or (isinstance(value, str) and value.lower() in text):
                kept[key] = value
        if (
            isinstance(kept.get("filters"), dict)
            and kept.get("follow_up") is not True
            and not has_digit(message)
        ):
            kept["filters"] = {
                key: value
                for key, value in kept["filters"].items()
                if not (key in NUMERIC_FILTER_KEYS and has_digit(str(value)))
            }
            if not kept["filters"]:
                del kept["filters"]
        relaxed = relax_compare(item.get("type"), kept)
        if relaxed is None:
            continue
        kept = relaxed
        span = item.get("span")
        if not isinstance(span, str) or not span.strip() or words(span) - spoken:
            span = message
        cleaned.append({**item, "span": span, "entities": kept})
    return {**raw, "intents": cleaned}


# Model gắn "so sánh" cho câu chưa nêu đủ hai trường ("so sánh giúp mình 3 trường ở bang Texas"). Luật compare cần từ hai tên trường nên
# cả tin bị loại và học sinh nhận "chưa hiểu". Hạ ý xuống việc gần nhất mà dữ liệu hỗ trợ, thay vì bỏ cả tin:
#   knowledge compare_facts: có điều kiện lọc hoặc cách xếp thì thành search; đúng một trường thì thành school_info; còn lại bỏ ý này (None)
#   profile_review compare: bỏ nhiệm vụ compare; hết nhiệm vụ thì bỏ luôn tên trường rác ("3 trường ở bang Texas"), chat hỏi lại bằng nút
def relax_compare(intent: Any, entities: dict[str, Any]) -> dict[str, Any] | None:
    tasks = entities.get("tasks") or []
    schools = entities.get("schools")
    if (isinstance(schools, list) and len(schools) >= 2) or entities.get("group") is not None:
        return entities
    if intent == "knowledge" and "compare_facts" in tasks:
        if entities.get("filters") or entities.get("sort") is not None:
            return {**{k: v for k, v in entities.items() if k != "schools"}, "tasks": ["search"]}
        if isinstance(schools, list) and len(schools) == 1:
            return {**entities, "tasks": ["school_info"]}
        return None
    if intent == "profile_review" and "compare" in tasks:
        rest = [t for t in tasks if t != "compare"]
        out = {k: v for k, v in entities.items() if k != "tasks"}
        if rest:
            return {**out, "tasks": rest}
        out.pop("schools", None)
        return out
    return entities


def has_digit(text: str) -> bool:
    return any(ch.isdigit() for ch in text)


# Các từ của một đoạn chữ, viết thường ("I-20", "TP.HCM", "8.5" giữ nguyên một từ)
def words(text: str) -> set[str]:
    return set(_WORD_RE.findall(text.lower()))


# Hồ sơ trong state thành dict chữ như cột ho_so của dữ liệu. Trường chưa có giá trị thì bỏ
# Vd: GPA = 3.6, SAT = 1350, ngân sách chưa có → {"gpa": "3.6", "sat": "1350"}
def profile_text(state: ChatState) -> dict[str, str]:
    profile: dict[str, str] = {}

    for key, field in state.profile.items():
        if field.value in (None, "", [], {}):
            continue

        if isinstance(field.value, str):
            value = field.value
        else:
            value = json.dumps(field.value, ensure_ascii=False)

        profile[key] = value

    return profile


# Độ tin cậy = xác suất model sinh ra đúng chữ của "type", lấy ý thấp nhất trong tin (một ý chắc chắn không che được
# ý đang đoán). Xác suất của một ý là tích xác suất các token tạo nên chữ đó.
# Ollama không trả logprobs thì không đo được, trả 1.0
def label_confidence(content: str, logprobs: list[dict[str, Any]]) -> float:
    spans = [match.span(1) for match in _TYPE_VALUE_RE.finditer(content)]  # vị trí chữ của từng "type"
    if not spans or not logprobs:
        return 1.0
    totals = [0.0] * len(spans)
    offset = 0
    for item in logprobs:
        token = item.get("token", "")
        for i, (start, end) in enumerate(spans):
            # token nằm (một phần) trong giá trị của type thứ i
            if offset < end and offset + len(token) > start:
                totals[i] += item.get("logprob", 0.0)
        offset += len(token)
    return math.exp(min(totals))


# Định dạng đầu ra gửi cho Ollama theo settings.slm_format.
# "json": model đã fine-tune tự viết đúng khuôn theo thứ tự đã học (type trước). Ép schema thì Ollama đổi thứ tự khóa
# theo bảng chữ cái (entities, span, type), lệch với lúc train nên sai nhiều (đo ở bản cũ: 70,8% so với 90,8%).
# "schema": ép theo SLM_TIN_SCHEMA, cho model chưa fine-tune vì nó không tự biết khuôn
def output_format(name: str) -> dict[str, Any] | str:
    if name == "json":
        return "json"
    if name == "schema":
        return SLM_TIN_SCHEMA
    raise ValueError(f"slm_format phải là 'json' hoặc 'schema', đang là '{name}'")


class OllamaSlm:
    """Làm đúng hợp đồng SlmClassifier trong slm_contract.py."""

    # output: định dạng đầu ra, lấy bằng output_format(settings.slm_format). Mặc định ép SLM_TIN_SCHEMA
    # prompt: tên file prompt trong app/skills (không có .md). Model fine-tune phải dùng đúng prompt lúc huấn luyện
    def __init__(
        self,
        slm: Any,
        output: dict[str, Any] | str = SLM_TIN_SCHEMA,
        use_context: bool = False,
        prompt: str = PROMPT_PATH.stem,
    ) -> None:
        self._use_context = use_context

        self._slm = slm
        self._output = output
        self.prompt_name = (
            prompt  # gắn nhãn các lần gọi SLM trong báo cáo token (cùng cách với SkillCall.name)
        )
        self.prompt, self.prompt_version = load_skill(prompt_path(prompt))

    async def classify(self, state: ChatState, message: str) -> SlmTinResult:
        context = context_for_model(state) if self._use_context else None
        return await self.read(message, profile_text(state), state.last_question, context)

    async def read(
        self,
        message: str,
        profile: dict[str, str] | None,
        pending: str,
        context: dict[str, Any] | None = None,
    ) -> SlmTinResult:
        messages = build_messages(self.prompt, message, profile, pending, context)
        names = {name.lower() for name in (context or {}).get("schools", [])}
        with usage_meter.purpose(self.prompt_name, self.prompt_version):
            for _ in range(MAX_ATTEMPTS):
                try:
                    raw, content, logprobs = await self._slm.chat_json_scored(messages, self._output)
                    raw = keep_verbatim(raw, message, names)  # bỏ giá trị và đoạn không có trong tin
                    # Kiểm từng ý, gộp ý cùng loại, đưa collect_profile lên đầu, cắt ý quá trần kỹ thuật, rồi kiểm cả tin.
                    # ValidationError của pydantic cũng là ValueError
                    return SlmTinResult.from_raw(raw, confidence=label_confidence(content, logprobs))
                except OllamaError as ex:
                    logger.warning("Ollama lỗi, thử lại nếu còn lượt: %s", ex)
                except ValueError as ex:  # ValidationError của pydantic cũng là ValueError
                    logger.warning("SLM trả kết quả sai hợp đồng, không gọi lại: %s", ex)
                    break
        # Mang theo tin gốc của học sinh làm span: nhánh ngoài phạm vi và luật xã giao đọc span, không đọc chữ giữ chỗ "tin không rõ"
        return FALLBACK.model_copy(
            deep=True, update={"intents": [FALLBACK.intents[0].model_copy(update={"span": message})]}
        )
