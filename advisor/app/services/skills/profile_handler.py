import hashlib
import json
from dataclasses import replace
from typing import Any

from app.schemas.chat import ChatState
from app.schemas.slm import PROFILE_KEYS, SlmTinResult, task_of
from app.services.chat_flow import OPENING_QUESTIONS, SkillOutput, ask_next_missing, start_intake
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import buttons, skill_bac_hoc, skill_ho_so

# Giá trị hồ sơ nào do skill nào đọc
BAC_HOC_FIELDS = set(skill_bac_hoc.READ_FIELDS)
HO_SO_FIELDS = {name for names in skill_ho_so.QUESTION_FIELDS.values() for name in names}

PROFILE_DONE = "Hồ sơ của bạn đã đầy đủ rồi. Bạn cần mình tư vấn thêm gì không?"
# Mục hồ sơ → câu hỏi của mục đó, đảo từ bảng câu hỏi của hai skill. Lớp đang học hỏi cùng câu với bậc đang học
EDIT_QUESTION = {
    name: question_id
    for question_id, names in {**skill_bac_hoc.QUESTION_FIELDS, **skill_ho_so.QUESTION_FIELDS}.items()
    for name in names
} | {"current_grade": "B1.1"}


class ChatProfileHandler:
    """
    Lưu trữ và xử lý thông tin hồ sơ của học sinh trong quá trình chat.
    """

    # skills: các skill do build_handlers tạo. Tra khi cần vì nhánh được tạo cả khi chưa đăng ký skill nào
    def __init__(self, skills: dict[str, Any]) -> None:
        self._skills = skills

    # Có câu đang chờ: ghi phần thuộc nhóm còn lại trước, để skill không hỏi lại điều vừa được nói.
    # Không có câu chờ: ghi giá trị tự khai rồi hỏi tiếp mục còn thiếu.
    # Hồ sơ vẫn đủ nghĩa là không có gì mới, nên không mở lại bước xác nhận
    async def collect(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        said = set(slm.profile_values())
        if wants_edit(slm) and (self.pending_question(state) is None or state.question_id == "X1"):
            return await self._edit(state, message, said)
        return await self._collect(state, message, said)

    async def _collect(self, state: ChatState, message: str, said: set[str]) -> SkillOutput:
        if self.pending_question(state) is None:
            await self._save_said(state, message, said)
            if self.ready(state, REQUIRED_B3):
                return SkillOutput(PROFILE_DONE)
            if state.intake_mode != "chat" and missing_fields(state.profile, REQUIRED_B3):
                return start_intake(state)
            return ask_next_missing(state)
        if state.question_id in skill_bac_hoc.QUESTION_FIELDS:
            await self._save_said(state, message, said - BAC_HOC_FIELDS)
            return await self._skills["skill_bac_hoc"].run(state, message)
        await self._save_said(state, message, said - HO_SO_FIELDS)
        return await self._skills["skill_ho_so"].run(state, message)

    # Học sinh muốn sửa hồ sơ (SLM trả edit) khi đang xem bảng rà soát hoặc không có câu đang chờ.
    # Chưa nói mục nào: hiện bảng và hỏi sửa mục nào. Nói mục mà chưa có giá trị mới: hỏi lại đúng câu của mục đó
    # (mục đầu tiên theo thứ tự hồ sơ). Nói cả giá trị: ghi như bình thường
    async def _edit(self, state: ChatState, message: str, said: set[str]) -> SkillOutput:
        if not said:
            return replace(skill_ho_so.review(state), answer=skill_ho_so.ASK_WHAT_TO_FIX)
        at_review = state.question_id == "X1"
        before = skill_ho_so.profile_snapshot(state)
        output = await self._collect(state, message, said)
        if skill_ho_so.profile_snapshot(state) != before:
            return output
        question_id = next((EDIT_QUESTION[name] for name in PROFILE_KEYS if name in said), None)
        if question_id is None:
            return output
        # Ở bảng rà soát mà giá trị sai thì skill_ho_so đã soạn câu hỏi lại (không kèm bảng): giữ câu đó
        text = (
            output.answer
            if at_review and output.widget is None and output.answer != skill_ho_so.ASK_WHAT_TO_FIX
            else None
        )
        return ask_edit_question(state, question_id, text)

    # Đọc lại tin bằng skill có khuôn giá trị chuẩn
    async def _save_said(self, state: ChatState, message: str, said: set[str]) -> None:
        if said & BAC_HOC_FIELDS:
            await self._skills["skill_bac_hoc"].save_said(state, message)
        if said & HO_SO_FIELDS:
            await self._skills["skill_ho_so"].save_said(state, message)

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


# Tin có ý collect_profile với nhiệm vụ edit
def wants_edit(slm: SlmTinResult) -> bool:
    return any(y.type == "collect_profile" and task_of(y.entities) == "edit" for y in slm.intents)


# Hỏi câu của mục cần sửa kèm nút, ô chọn như lúc điền hồ sơ trong chat
def ask_edit_question(state: ChatState, question_id: str, text: str | None = None) -> SkillOutput:
    if question_id in skill_bac_hoc.QUESTION_FIELDS:
        can_skip = question_id in skill_bac_hoc.SKIPPABLE_QUESTIONS
        return buttons.ask_with_buttons(state, question_id, text, can_skip=can_skip)
    return skill_ho_so.ask_question(state, question_id, text)
