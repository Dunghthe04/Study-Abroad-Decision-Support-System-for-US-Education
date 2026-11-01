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
from app.schemas.slm import SLM_SCHEMA, SlmRead, SlmResult
from app.services.ollama import OllamaError
from app.services.skill_call import SKILLS_DIR, load_skill

logger = logging.getLogger(__name__)

PROMPT_PATH = SKILLS_DIR / "slm_phan_loai.md"
MAX_ATTEMPTS = 2  # sai thì gọi lại đúng 1 lần
# Hai lần đều sai (model trả sai luật, Ollama tắt): coi là tin mơ hồ, confidence 0 để chat hỏi lại học sinh
FALLBACK = SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "mo_ho"}, confidence=0.0)
_TYPE_VALUE_RE = re.compile(r'"type"\s*:\s*"([^"]*)"')


# Đầu vào của SLM, cùng khóa với cột đầu vào trong file dữ liệu
def slm_input(message: str, profile: dict[str, str] | None, pending: str) -> dict[str, Any]:
    return {"cau_hoi": message, "ho_so": profile or None, "cau_he_thong_dang_hoi": pending}


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


class OllamaSlm:
    """Làm đúng hợp đồng SlmClassifier trong slm_contract.py."""

    # slm: OllamaClient tạo với model SLM (settings.slm_model, qwen3:4b). Để Any để test truyền được client giả
    def __init__(self, slm: Any) -> None:
        self._slm = slm
        self.prompt, self.prompt_version = load_skill(PROMPT_PATH)

    # Lấy hồ sơ và câu hệ thống đang hỏi từ state (last_question rỗng khi không có câu nào chờ)
    async def classify(self, state: ChatState, message: str) -> SlmResult:
        return await self.read(message, profile_text(state), state.last_question)

    # Phần chính, tách riêng để script đo chạy thẳng trên dữ liệu mà không cần dựng ChatState
    async def read(self, message: str, profile: dict[str, str] | None, pending: str) -> SlmResult:
        user = slm_input(message, profile, pending)
        messages = [
            {"role": "system", "content": self.prompt},
            {"role": "user", "content": json.dumps(user, ensure_ascii=False, separators=(",", ":"))},
        ]
        for _ in range(MAX_ATTEMPTS):
            try:
                raw, content, logprobs = await self._slm.chat_json_scored(messages, SLM_SCHEMA)
                read = SlmRead.model_validate(raw)  # kiểm luật nhãn và thực thể
                return SlmResult(**read.model_dump(), confidence=label_confidence(content, logprobs))
            except (OllamaError, ValidationError) as ex:
                logger.warning("SLM trả kết quả không dùng được: %s", ex)
        return FALLBACK.model_copy(deep=True)
