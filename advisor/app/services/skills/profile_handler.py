import hashlib
import json
from typing import Any

from app.schemas.chat import ChatState, Widget
from app.schemas.slm import PROFILE_KEYS, SlmTinResult, task_of
from app.services.chat_flow import (
    OPENING_QUESTIONS,
    SkillOutput,
    ask_next_missing,
    limit_to_required,
    start_intake,
)
from app.services.intake import EDIT_ONLY_GOAL, form_goals
from app.services.replies import PROFILE_DONE
from app.services.skill_rules import REQUIRED_B3, missing_fields
from app.services.skills import buttons, edit_prefill, skill_bac_hoc, skill_ho_so

# Giá trị hồ sơ nào do skill nào đọc
BAC_HOC_FIELDS = set(skill_bac_hoc.READ_FIELDS)
HO_SO_FIELDS = {name for names in skill_ho_so.QUESTION_FIELDS.values() for name in names}

# Câu hỏi → các mục hồ sơ câu đó ghi, gộp bảng của hai skill
ALL_QUESTION_FIELDS = {**skill_bac_hoc.QUESTION_FIELDS, **skill_ho_so.QUESTION_FIELDS}
# Mục hồ sơ → câu hỏi của mục đó (đảo bảng trên). Lớp đang học hỏi cùng câu với bậc đang học
EDIT_QUESTION = {
    name: question_id for question_id, names in ALL_QUESTION_FIELDS.items() for name in names
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
        # Tin còn ý khác (câu hỏi): giá trị hồ sơ chỉ là bối cảnh của câu hỏi, không phải phản hồi cho bảng rà soát
        aside = any(y.type != "collect_profile" for y in slm.intents)
        # Muốn sửa khi không có câu chờ, hoặc khi đã hiện bảng rà soát (đang ở bảng, hay đang hỏi dở mục sửa trước)
        if wants_edit(slm) and (self.pending_question(state) is None or skill_ho_so.reviewed(state)):
            return await self._edit(state, message, said)
        if not aside:
            return await self._collect(state, message, said)
        before = skill_ho_so.profile_snapshot(state)
        if self.pending_question(state) is None:
            # Chưa hỏi gì: chỉ ghi nháp giá trị nói kèm câu hỏi (said_in_B0, xác nhận sau). Không tự mời điền hồ sơ:
            # phần nào cần hồ sơ (tư vấn, gợi ý trường) thì nhánh đó tự hỏi mục còn thiếu, hỏi thêm ở đây là hỏi hai lần.
            # Riêng hồ sơ đã đủ mà vừa có giá trị mới thì hiện bảng rà soát để học sinh xác nhận
            limit_to_required(state, REQUIRED_B3)
            await self._save_said(state, message, said)
            complete = not missing_fields(state.profile, REQUIRED_B3)
            return (
                skill_ho_so.review(state)
                if complete and has_unconfirmed_news(state, before)
                else SkillOutput("")
            )
        output = await self._collect(state, message, said)
        if output.answer not in (skill_ho_so.EDIT_MENU, PROFILE_DONE):
            return output
        # Không có giá trị nào mới cần xác nhận thì im lặng, câu trả lời của ý kia đứng một mình (bảng rà soát đang chờ vẫn
        # chờ như cũ). Có giá trị mới chưa xác nhận thì vẫn hiện bảng rà soát để học sinh xác nhận
        return skill_ho_so.review(state) if has_unconfirmed_news(state, before) else SkillOutput("")

    # Ở bảng rà soát: nút Cần sửa, ô chọn nhóm, Sửa trên form, Điền lại từ đầu thì xử lý luôn, không gọi skill.
    # Không có câu đang hỏi (vd nút sửa của câu trả lời kịch bản) thì chỉ nhận câu ô chọn nhóm.
    # Đang sửa mà bấm Giữ như cũ thì sang câu kế tiếp, hồ sơ không đổi.
    # Đang sửa: trả lời xong một mục thì skill quay về bảng rà soát X1. Còn mục chờ sửa thì hỏi mục kế tiếp,
    # hết thì để bảng hiện và thôi đánh dấu đang sửa
    async def _collect(self, state: ChatState, message: str, said: set[str]) -> SkillOutput:
        if state.question_id == "X1" or (
            state.question_id is None and skill_ho_so.read_edit_menu(message) is not None
        ):
            choice = edit_choice(state, message)
            if choice is not None:
                return choice
        if state.edit_queue is not None and buttons.same_text(message, skill_ho_so.KEEP_REPLY.value):
            return next_edit_question(state)
        output = await self._answer(state, message, said)
        if state.question_id == "X1" and state.edit_queue is not None:
            return next_edit_question(state, output)
        return output

    async def _answer(self, state: ChatState, message: str, said: set[str]) -> SkillOutput:
        if self.pending_question(state) is None:
            # Hồ sơ đã có từ trước (xét trước khi ghi giá trị tự khai): chỉ hỏi mục bắt buộc, rồi hiện bảng rà soát
            limit_to_required(state, REQUIRED_B3)
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

    # Học sinh muốn sửa hồ sơ (SLM trả edit). Chưa nói mục nào: hiện bảng và hỏi sửa mục nào.
    # Mục kèm giá trị mới thì ghi luôn; mục chưa có giá trị thì hỏi lần lượt câu của từng mục theo thứ tự hồ sơ,
    # hỏi câu đầu, các câu sau cất vào state.edit_queue. Đang hỏi dở mục sửa trước mà muốn sửa thêm: mục mới hỏi trước,
    # mục đang hỏi (nếu tin này không trả lời nó) và các mục chưa hỏi vẫn giữ
    async def _edit(self, state: ChatState, message: str, said: set[str]) -> SkillOutput:
        if not said:
            state.edit_queue = None
            return skill_ho_so.edit_menu(state)
        at_review = state.question_id == "X1"
        waiting = state.question_id if state.question_id in ALL_QUESTION_FIELDS else None
        old_queue, state.edit_queue = state.edit_queue or [], None
        before = skill_ho_so.profile_snapshot(state)
        if waiting is None:  # đang ở bảng rà soát hoặc không có câu chờ
            output = await self._collect(state, message, said)
        else:  # tin này là yêu cầu sửa, không phải câu trả lời cho câu đang chờ: chỉ ghi giá trị đã nói
            await self._save_said(state, message, said)
            output = skill_ho_so.review(state)
        after = skill_ho_so.profile_snapshot(state)

        def unchanged(names: list[str] | tuple[str, ...]) -> bool:
            return all(after.get(name) == before.get(name) for name in names)

        new = [EDIT_QUESTION[name] for name in PROFILE_KEYS if name in said and unchanged([name])]
        if waiting is not None and unchanged(ALL_QUESTION_FIELDS[waiting]):
            new.append(waiting)
        queue = list(dict.fromkeys(new + old_queue))  # bỏ câu trùng (vd SAT và ACT cùng câu D3), giữ thứ tự
        if not queue:
            return output
        # Ở bảng rà soát mà giá trị sai thì skill_ho_so đã soạn câu hỏi lại (không kèm bảng): giữ câu đó
        text = output.answer if at_review and output.widget is None else None
        state.edit_queue = queue[1:]
        return ask_edit_question(state, queue[0], text)

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


# So với trước lượt này có giá trị nào mới mà học sinh chưa xác nhận không
def has_unconfirmed_news(state: ChatState, before: dict) -> bool:
    after = skill_ho_so.profile_snapshot(state)
    return any(value != before.get(name) and value[1] != "confirmed" for name, value in after.items())


# Tin có ý collect_profile với nhiệm vụ edit
def wants_edit(slm: SlmTinResult) -> bool:
    return any(y.type == "collect_profile" and task_of(y.entities) == "edit" for y in slm.intents)


# Hỏi câu của mục cần sửa kèm nút, ô chọn như lúc điền hồ sơ trong chat.
# Đang sửa mà mục đã có giá trị thì thêm nút "Giữ như cũ"
def ask_edit_question(state: ChatState, question_id: str, text: str | None = None) -> SkillOutput:
    # Ngân sách phụ huynh đã nhập thì không sửa được: báo rồi sang mục sửa kế tiếp (hết thì về bảng rà soát)
    if question_id == "F1" and buttons.budget_locked(state):
        output = next_edit_question(state)
        output.answer = f"{buttons.locked_budget_text(state)}\n\n{output.answer}"
        return output
    if question_id in skill_bac_hoc.QUESTION_FIELDS:
        can_skip = question_id in skill_bac_hoc.SKIPPABLE_QUESTIONS
        output = buttons.ask_with_buttons(state, question_id, text, can_skip=can_skip)
    else:
        output = skill_ho_so.ask_question(state, question_id, text)
    if state.edit_queue is not None and any(
        name in state.profile for name in ALL_QUESTION_FIELDS[question_id]
    ):
        output.quick_replies = [*output.quick_replies, skill_ho_so.KEEP_REPLY]
        # Kèm giá trị cũ: khung "Hiện tại" + điền sẵn ô chọn, câu chỉ có nút thì ghi trong câu hỏi
        output = edit_prefill.add_current(state, question_id, ALL_QUESTION_FIELDS[question_id], output)
    return output


# Hỏi câu kế tiếp trong hàng chờ sửa, bỏ câu không cần hỏi với hồ sơ lúc này (vd D1 khi không học cấp 3).
# Hết câu thì thôi đánh dấu đang sửa và hiện bảng rà soát (output: bảng skill vừa trả, nếu có)
def next_edit_question(state: ChatState, output: SkillOutput | None = None) -> SkillOutput:
    while state.edit_queue:
        question_id = state.edit_queue.pop(0)
        if skill_ho_so.needed(state, question_id):
            return ask_edit_question(state, question_id)
    state.edit_queue = None
    return output or skill_ho_so.review(state)


# Bấm nút hoặc gửi ô chọn ở bảng rà soát. Không phải các nút này thì None
def edit_choice(state: ChatState, message: str) -> SkillOutput | None:
    if buttons.same_text(message, skill_ho_so.FIX_REPLY.value):
        state.edit_queue = None
        return skill_ho_so.edit_menu(state)
    if buttons.same_text(message, skill_ho_so.FORM_REPLY.value):
        state.edit_queue = None
        # Gửi kèm việc học sinh đã hỏi (vd gợi ý trường) để form tích sẵn ô nhiệm vụ, như khi chọn điền bằng form.
        # Không có việc nào đang chờ (chỉ muốn sửa hồ sơ) thì form tích "Chỉ sửa hồ sơ"
        goals = form_goals(state.pending_advice) if state.pending_advice is not None else [EDIT_ONLY_GOAL]
        return SkillOutput(skill_ho_so.EDIT_ON_FORM, widget=Widget(type="form", data={"goals": goals}))
    if buttons.same_text(message, skill_ho_so.RESTART_REPLY.value):
        state.edit_queue = list(skill_ho_so.EDIT_ORDER)
        return next_edit_question(state)
    sections = skill_ho_so.read_edit_menu(message)
    if sections is None:
        return None
    state.edit_queue = skill_ho_so.edit_questions(sections)
    return next_edit_question(state)
