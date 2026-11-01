import hashlib
import json
from typing import Any

from app.schemas.chat import ChatState
from app.schemas.slm import SlmResult
from app.services.chat_flow import OPENING_QUESTIONS, SkillOutput, ask_next_missing, start_intake
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import skill_bac_hoc, skill_ho_so
from app.services.skills.skill_bac_hoc import BacHocHandler, BacHocReader
from app.services.skills.skill_ho_so import HoSoHandler, HoSoReader

# Giá trị hồ sơ nào do skill nào đọc
BAC_HOC_FIELDS = set(skill_bac_hoc.READ_FIELDS)
HO_SO_FIELDS = {name for names in skill_ho_so.QUESTION_FIELDS.values() for name in names}

PROFILE_DONE = "Hồ sơ của bạn đã đầy đủ rồi. Bạn cần mình tư vấn thêm gì không?"


class ChatProfileHandler:
    """
    Lưu trữ và xử lý thông tin hồ sơ của học sinh trong quá trình chat.
    """

    def __init__(self, llm: Any, model: str) -> None:
        self._bac_hoc = BacHocHandler(llm, model)
        self._ho_so = HoSoHandler(llm, model)
        self._bac_hoc_reader = BacHocReader(llm, model)
        self._ho_so_reader = HoSoReader(llm, model)

    # Có câu đang chờ: ghi phần thuộc nhóm còn lại trước, để skill không hỏi lại điều vừa được nói.
    # Không có câu chờ: ghi giá trị tự khai rồi hỏi tiếp mục còn thiếu.
    # Hồ sơ vẫn đủ nghĩa là không có gì mới, nên không mở lại bước xác nhận
    async def collect(self, state: ChatState, message: str, slm: SlmResult) -> SkillOutput:
        said = set(slm.profile_values())
        if self.pending_question(state) is None:
            await self._save_said(state, message, said)
            if self.ready(state, REQUIRED_B3):
                return SkillOutput(PROFILE_DONE)
            if state.intake_mode != "chat" and missing_fields(state.profile, REQUIRED_B3):
                return start_intake(state)
            return ask_next_missing(state)
        if state.question_id in skill_bac_hoc.QUESTION_FIELDS:
            await self._save_said(state, message, said - BAC_HOC_FIELDS)
            return await self._bac_hoc.run(state, message)
        await self._save_said(state, message, said - HO_SO_FIELDS)
        return await self._ho_so.run(state, message)

    # Đọc lại tin bằng skill có khuôn giá trị chuẩn. Ghi said_in_B0 để học sinh xác nhận lại ở X1,
    # được đè giá trị cũ vì học sinh có thể đang sửa hồ sơ
    async def _save_said(self, state: ChatState, message: str, said: set[str]) -> None:
        if said & BAC_HOC_FIELDS:
            read = await self._bac_hoc_reader.read("B1.1", message)
            skill_bac_hoc.save_read(state, read, status="said_in_B0")
        if said & HO_SO_FIELDS:
            known = {name: f.value for name, f in state.profile.items() if f.value is not None}
            read = await self._ho_so_reader.read(None, message, known, state.branch)
            skill_ho_so.save_read(state, read, status="said_in_B0")

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
