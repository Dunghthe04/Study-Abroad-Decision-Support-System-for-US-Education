import hashlib
import json
from typing import Any

from app.schemas.chat import ChatState
from app.schemas.slm import SlmResult
from app.services.chat_flow import OPENING_QUESTIONS, SkillOutput
from app.services.skill_rules import missing_fields
from app.services.skills import skill_bac_hoc
from app.services.skills.skill_bac_hoc import BacHocHandler
from app.services.skills.skill_ho_so import HoSoHandler


class ChatProfileHandler:
    """
    Lưu trữ và xử lý thông tin hồ sơ của học sinh trong quá trình chat.
    """

    def __init__(self, llm: Any, model: str) -> None:
        self._bac_hoc = BacHocHandler(llm, model)
        self._ho_so = HoSoHandler(llm, model)

    # Giao tin cho skill sở hữu câu đang chờ. Dựa vào mã câu, không dựa vào current_step
    async def collect(self, state: ChatState, message: str, slm: SlmResult) -> SkillOutput:
        if state.question_id in skill_bac_hoc.QUESTION_FIELDS:
            return await self._bac_hoc.run(state, message)
        return await self._ho_so.run(state, message)

    def pending_question(self, state: ChatState) -> str | None:
        """
        Trả về câu hỏi đang chờ học sinh trả lời, nếu có.
        """
        if state.question_id is None or state.question_id in OPENING_QUESTIONS:
            return None
        return state.last_question or None

    def version(self, state: ChatState) -> str:
        """
        Trả về hash của thông tin hồ sơ hiện tại, dùng để so sánh xem hồ sơ có thay đổi không.
        """
        profile = {key: [field.value, field.status] for key, field in state.profile.items()}
        text = json.dumps(profile, ensure_ascii=False, sort_keys=True, default=str)
        return hashlib.sha256(text.encode()).hexdigest()[:16]

    # Đủ để tư vấn: không thiếu mục bắt buộc, không còn câu đang chờ, không còn thông tin chưa xác nhận
    def ready(self, state: ChatState, required: list[str]) -> bool:
        if missing_fields(state.profile, required) or self.pending_question(state) is not None:
            return False
        return all(field.status != "said_in_B0" for field in state.profile.values())
