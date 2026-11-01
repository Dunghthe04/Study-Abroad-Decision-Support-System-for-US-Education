"""Bộ phân loại SLM (Qwen3 4B qua Ollama): đọc tin nhắn, trả loại câu hỏi, nhãn phụ và thông tin tách được.

Đầu vào cùng dạng cột đầu vào trong data/slm/du_lieu_slm.xlsx, đầu ra theo SLM_SCHEMA. Nhờ vậy model chưa fine-tune,
model đã fine-tune và dữ liệu huấn luyện dùng chung một khuôn, đổi model chỉ cần đổi settings.slm_model.
"""

import json
import logging
import math
import re
from typing import Any

from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import PRIORITY, PROFILE_KEYS, SLM_SCHEMA, SlmRead, SlmResult
from app.services.ollama import OllamaError
from app.services.skill_call import SKILLS_DIR, load_skill

logger = logging.getLogger(__name__)

PROMPT_PATH = SKILLS_DIR / "slm_phan_loai.md"
MAX_ATTEMPTS = 2  # sai thì gọi lại đúng 1 lần
# Hai lần đều sai (model trả sai luật, Ollama tắt): coi là tin mơ hồ, confidence 0 để chat hỏi lại học sinh
FALLBACK = SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "mo_ho"}, confidence=0.0)
_TYPE_VALUE_RE = re.compile(r'"type"\s*:\s*"([^"]*)"')
# Khóa mà giá trị phải là đoạn chữ nguyên văn trong tin nhắn
SPAN_KEYS = {*PROFILE_KEYS, "truong", "thanh_pho"}


# Đầu vào của SLM, cùng khóa với cột đầu vào trong file dữ liệu
def slm_input(message: str, profile: dict[str, str] | None, pending: str) -> dict[str, Any]:
    return {"cau_hoi": message, "ho_so": profile or None, "cau_he_thong_dang_hoi": pending}


# Hội thoại gửi cho SLM: system là prompt, user là JSON đầu vào viết liền.
# Dùng chung cho lúc chạy (OllamaSlm.read) và lúc xuất dữ liệu fine-tune, để model học đúng thứ nó sẽ nhận
def build_messages(
    prompt: str, message: str, profile: dict[str, str] | None, pending: str
) -> list[dict[str, str]]:
    user = slm_input(message, profile, pending)
    return [
        {"role": "system", "content": prompt},
        {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
    ]


# Sửa cặp nhãn model chọn đúng nhưng đặt sai chỗ, thay vì bỏ cả câu trả lời. Luật thứ tự cố định nên sửa chắc chắn được:
#   nhãn phụ xếp trước nhãn chính → đảo (trung_tam + phụ kien_thuc → kien_thuc + phụ trung_tam)
#   mac_dinh có nhãn phụ → câu hỏi là nhãn chính, giá trị hồ sơ vẫn nằm trong thuc_the (mac_dinh + kien_thuc → kien_thuc)
#   nhãn phụ trùng nhãn chính → bỏ nhãn phụ
# Đo trên val: lùi về mơ hồ 4 → 0, nhãn chính 90,8% → 94,2%
def fix_labels(raw: dict[str, Any]) -> dict[str, Any]:
    intent, sub = raw.get("type"), raw.get("nhan_phu")
    if sub is None or sub == intent:
        return {**raw, "nhan_phu": None}
    if intent == "mac_dinh":
        return {**raw, "type": sub, "nhan_phu": None}
    if intent in PRIORITY and sub in PRIORITY and PRIORITY.index(sub) < PRIORITY.index(intent):
        return {**raw, "type": sub, "nhan_phu": intent}
    return raw


# Bỏ giá trị không có nguyên văn trong tin: model hay chép từ hồ sơ đầu vào, hoặc tự điền "null", "không có"
def keep_verbatim(thuc_the: dict[str, Any], message: str) -> dict[str, Any]:
    text = message.lower()
    return {
        key: value
        for key, value in thuc_the.items()
        if key not in SPAN_KEYS or (isinstance(value, str) and value.lower() in text)
    }


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


# Độ tin cậy của nhãn chính = xác suất model sinh ra đúng chữ của "type" (tích xác suất các token tạo nên nó).
# Ollama không trả logprobs thì không đo được, trả 1.0
def label_confidence(content: str, logprobs: list[dict[str, Any]]) -> float:
    # Tìm vị trí nhãn type
    match = _TYPE_VALUE_RE.search(content)
    # Nếu không thấy
    if not match or not logprobs:
        return 1.0
    # Xác định vị trí của nhãn
    start, end = match.span(1)
    # Total : tổng, offset : vtri hiện tại
    total, offset = 0.0, 0
    for item in logprobs:
        token = item.get("token", "")
        if offset < end and offset + len(token) > start:  # token nằm (một phần) trong giá trị của type
            total += item.get("logprob", 0.0)
        offset += len(token)
    return math.exp(total)


# Định dạng đầu ra gửi cho Ollama theo settings.slm_format.
# "json": model đã fine-tune tự viết đúng khuôn theo thứ tự đã học (type trước). Ép schema thì Ollama đổi thứ tự khóa
# theo bảng chữ cái (nhan_phu, thuc_the, type), lệch với lúc train nên sai nhiều (đo val: 70,8% so với 90,8%).
# "schema": ép theo SLM_SCHEMA, cho model chưa fine-tune vì nó không tự biết khuôn
def output_format(name: str) -> dict[str, Any] | str:
    if name == "json":
        return "json"
    if name == "schema":
        return SLM_SCHEMA
    raise ValueError(f"slm_format phải là 'json' hoặc 'schema', đang là '{name}'")


class OllamaSlm:
    """Làm đúng hợp đồng SlmClassifier trong slm_contract.py."""

    # slm: OllamaClient tạo với model SLM (settings.slm_model). Để Any để test truyền được client giả.
    # output: định dạng đầu ra, lấy bằng output_format(settings.slm_format). Mặc định ép SLM_SCHEMA
    def __init__(self, slm: Any, output: dict[str, Any] | str = SLM_SCHEMA) -> None:
        self._slm = slm
        self._output = output
        self.prompt, self.prompt_version = load_skill(PROMPT_PATH)

    # Lấy hồ sơ và câu hệ thống đang hỏi từ state (last_question rỗng khi không có câu nào chờ)
    async def classify(self, state: ChatState, message: str) -> SlmResult:
        return await self.read(message, profile_text(state), state.last_question)

    # Phần chính, tách riêng để script đo chạy thẳng trên dữ liệu mà không cần dựng ChatState
    async def read(self, message: str, profile: dict[str, str] | None, pending: str) -> SlmResult:
        messages = build_messages(self.prompt, message, profile, pending)
        for _ in range(MAX_ATTEMPTS):
            try:
                raw, content, logprobs = await self._slm.chat_json_scored(messages, self._output)
                raw = fix_labels(raw)
                raw["thuc_the"] = keep_verbatim(
                    raw.get("thuc_the") or {}, message
                )  # bỏ giá trị không có trong tin
                read = SlmRead.model_validate(raw)  # kiểm luật nhãn và thực thể
                return SlmResult(**read.model_dump(), confidence=label_confidence(content, logprobs))
            except (OllamaError, ValidationError) as ex:
                logger.warning("SLM trả kết quả không dùng được: %s", ex)
        return FALLBACK.model_copy(deep=True)
