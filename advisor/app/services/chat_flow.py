"""
Điều phối luồng chat: đọc trạng thái, chọn skill bằng model, chạy skill, trả câu trả lời và trạng thái
Vòng xử lý tin nhắn
1. Model chọn skill(SkillPicker) -> 2. Code kiểm luật (plan_skill) -> 3. chạy từng skill-> 4. ghi log trả kết quả
"""

import json
import logging
import time
from dataclasses import dataclass, field
from typing import Any, Protocol

from app.schemas.chat import ChatRequest, ChatResponse, ChatState, Citation, QuickReply, Step, Widget
from app.services.intake import (
    CHAT_BUTTON,
    CONTINUE_CHAT,
    FORM_BUTTON,
    INTAKE_FORM,
    INTAKE_ID,
    INTAKE_MORE,
    intake_ask,
    intake_choice,
)
from app.services.pii_mask import mask_personal_info
from app.services.profile_sync import changed_keys, merge_profile
from app.services.skill_rules import (
    REQUIRED_B3,
    REQUIRED_BY_SKILL,
    STEP_SKILL,
    Plan,
    missing_fields,
    plan_skills,
)

# Tạo 1 logger
logger = logging.getLogger("advisor.chat")

# Câu hỏi lấy nguyên văn từ framework, AI không tự đặt câu hỏi
# Câu hỏi lấy nguyên văn từ framework, AI không tự đặt câu hỏi.
# Chữ phải giống hệt cột last_question trong ml/skill_picker/data/skill_picker.csv (có test kiểm tra),
# vì model chọn skill nhìn câu này làm đầu vào.
# X1 đến X3: câu chuyển bước, framework chưa đặt mã nên đặt tạm.
QUESTIONS = {
    "B0.1": "Chào bạn, mình là trợ lý tư vấn du học Mỹ. Bạn đang cần mình giúp điều gì?",
    "B0.2": "Bạn muốn mình tìm trường theo hồ sơ của bạn, hay bạn đang có câu hỏi cụ thể?",
    "B1.1": "Hiện bạn đang học lớp mấy, hoặc đang làm gì?",
    "B1.2": "Bạn muốn sang Mỹ học bậc nào?",
    "B1.3": "Bạn định học ngành gì? Chưa chắc cũng không sao, bạn cứ kể vài môn bạn thích.",
    "B1.4": "Bạn dự định nhập học khoảng khi nào?",
    "D1": "Bạn học chương trình nào: THPT Việt Nam, IB, A-level hay AP?",
    "D2": "Điểm trung bình từng học kỳ của bạn là bao nhiêu? Bạn có thể tải ảnh học bạ cho nhanh.",
    "D3": "Bạn đã thi SAT hoặc ACT chưa? Nếu rồi thì được bao nhiêu điểm?",
    "E1": "Bạn đã có chứng chỉ tiếng Anh nào chưa (IELTS, TOEFL, Duolingo)? Điểm bao nhiêu, thi khi nào?",
    "E2": "Bạn dự định thi khi nào, mục tiêu khoảng bao nhiêu điểm?",
    "F1": "Gia đình bạn có thể chi khoảng bao nhiêu mỗi năm cho cả học phí và sinh hoạt?",
    "F2": "Bạn cần học bổng ở mức nào: không cần, nên có, hay bắt buộc phải có mới đi được?",
    "F3": "Chi phí du học chủ yếu do ai chi trả?",
    "D4": "Bạn kể giúp mình các hoạt động ngoại khóa: tên, vai trò, phạm vi (trường, quận, thành phố, quốc gia, quốc tế), làm trong bao lâu.",
    "D5": "Bạn có giải thưởng nào không? Tên giải và cấp giải?",
    "N1": "Bạn có người thân ở Mỹ không?",
    "N2": "Bạn đã tìm hiểu bang nào ở Mỹ chưa?",
    "N3": "Bạn thích trường công hay trường tư?",
    "N4": "Bạn từng bị từ chối visa chưa?",
    "X1": "Bạn kiểm tra giúp mình hồ sơ ở trên, đúng không?",
    "X2": "Bạn có muốn mình đề xuất lộ trình và danh sách trường phù hợp không?",
    "X3": "Bạn có muốn mình xem hồ sơ và gợi ý trường không?",
}


NOT_READY = "Phần này mình đang hoàn thiện, bạn quay lại sau nhé."
NEED_PROFILE = (
    "Mình cần biết thêm về hồ sơ của bạn trước khi làm bước này. Bạn cho mình xin thêm thông tin nhé."
)
NEED_RESULTS = "Bạn cần xem danh sách trường gợi ý trước, rồi mình mới phân tích tiếp được nhé."
NEED_MAJOR = "Để gợi ý trường mình cần biết ngành bạn muốn học. Bạn chọn ngành giúp mình nhé."
# Skill trả lời xen ngang (không đổi bước, không hỏi sang câu khác): trả lời xong thì nhắc lại câu đang chờ
INTERRUPT_SKILLS = {"skill_tra_loi", "skill_chuyen_trung_tam"}
BACK_TO_QUESTION = "Quay lại câu mình đang hỏi nhé: "
OPENING_QUESTIONS = {"B0.1", "B0.2"}
# Skill đánh giá hồ sơ (B3): chạy ngay khi rà soát hồ sơ xong
ASSESS_SKILL = "skill_danh_gia"
CHAIN_STEPS = {"B5", "B6"}
END_SKILL = "ket_thuc"
# Bước hỏi hồ sơ (bậc học, hồ sơ): câu hỏi có nút gợi ý
INTAKE_STEPS = ("B1", "B2")


# Ghi lại câu vừa hỏi vào Chatsate, lưu last_question, question_id và tăng số lần hỏi
def ask(state: ChatState, question_id: str) -> str:
    # Lưu câu vừa hỏi, cùng mã câu để lượt sau biết học sinh đang trả lời câu nào
    state.last_question = QUESTIONS[question_id]
    state.question_id = question_id
    # Tăng số lần hỏi
    state.asked[question_id] = state.asked.get(question_id, 0) + 1
    return state.last_question


# Chuyển sang bước khác: mọi skill đổi bước đều gọi hàm này, không gán current_step trực tiếp
# Xóa câu đang chờ của bước cũ; nếu bước mới có câu hỏi thì gọi ask() SAU go_to
def go_to(state: ChatState, step: Step) -> None:
    state.current_step = step
    state.last_question = ""
    state.question_id = None


# Model chọn skill: chỉ cần có hàm predict trả về .skills (đã xếp xác suất cao trước) và .probs
# Class này dùng để mô tả kiểu của model chọn skill, không cần khởi tạo
# Giống interface
class SkillChooser(Protocol):
    async def predict(self, step: str, last_question: str, message: str, missing: list[str]) -> Any: ...


# Kết quả của 1 skill: câu trả lời gửi học sinh, kèm nút gợi ý và công cụ nhập nếu có
# (trạng thái sửa trực tiếp trên state được truyền vào)
@dataclass
class SkillOutput:
    answer: str
    quick_replies: list[QuickReply] = field(default_factory=list)
    widget: Widget | None = None
    citations: list[Citation] = field(default_factory=list)  # nguồn của thông tin trong câu trả lời
    disclaimer: str | None = None  # lời lưu ý kèm theo
    action: str | None = None  # việc .NET cần làm sau lượt này, xem ChatResponse.action
    action_data: dict[str, Any] = field(default_factory=dict)  # dữ liệu kèm action


# Hợp đồng chung mọi skill: nhân trạng thái và tin nhắn, trả câu trả lời
class SkillHandler(Protocol):
    async def run(self, state: ChatState, message: str) -> SkillOutput: ...


# Skill tạm: học sinh nói lạc đề thì trả lời ngắn và nhắc lại câu đang hỏi
class OffTopicHandler:
    async def run(self, state: ChatState, message: str) -> SkillOutput:
        if state.last_question:
            return SkillOutput(f"Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé. {state.last_question}")
        return SkillOutput("Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé. Bạn cần mình giúp gì về du học?")


# Skill tạm: học sinh muốn dừng thì chào và đánh dấu kết thúc
class EndHandler:
    async def run(self, state: ChatState, message: str) -> SkillOutput:
        go_to(state, "END")
        return SkillOutput("Chúc bạn thành công. Khi cần bạn cứ quay lại nhé.")


class ChatFlow:
    def __init__(
        self, picker: SkillChooser | None = None, handlers: dict[str, SkillHandler] | None = None
    ) -> None:
        self._picker = picker  # None = chưa có model: luôn dùng skill của bước hiện tại
        # Nạp các kills và đối tượng skill mặc định
        self._handlers: dict[str, SkillHandler] = {"ngoai_le": OffTopicHandler(), "ket_thuc": EndHandler()}
        # Thêm các skill khác nếu có
        self._handlers.update(handlers or {})

    async def reply(self, req: ChatRequest) -> ChatResponse:
        # Cuộc trò chuyện mới: giao diện đã tự hiện lời chào, nên tin nhắn đầu tiên là câu trả lời cho B0.1.
        # Ghi nhận đã chào rồi xử lý tin nhắn như mọi lượt khác, không bỏ qua câu học sinh vừa gõ.
        first_turn = req.state is None
        # Nếu là lượt đầu
        if first_turn:
            state = ChatState(study_level=req.study_level)
            # Ghi lại là đã hỏi
            ask(state, "B0.1")
        else:
            state = req.state.model_copy(deep=True)
            if req.study_level:  # .NET gửi bậc học mỗi lượt; không gửi thì giữ bậc đã nhớ
                state.study_level = req.study_level
        if req.study_level:  # .NET gửi bậc học mỗi lượt; không gửi thì giữ bậc đã nhớ
            state.study_level = req.study_level
        # Hồ sơ trong nháp (do .NET gửi) là nguồn thật; không gửi thì giữ hồ sơ trong state như cũ
        if req.profile is not None:
            state.profile = merge_profile(state.profile, req.profile)
        # Danh sách trường gợi ý do .NET gửi mỗi lượt; không gửi thì giữ như cũ
        if req.schools is not None:
            state.schools = req.schools
        # Cuộc trò chuyện đã kết thúc mà học sinh nhắn tiếp: mở lại từ đầu (giữ hồ sơ), như vừa được chào ở B0.1.
        # Model chọn skill chưa từng học bước END nên nếu để nguyên sẽ đoán sai.
        if state.current_step == "END":
            go_to(state, "B0")
            ask(state, "B0.1")
        # Chụp hồ sơ đầu lượt để cuối lượt biết khóa nào đổi (sao chép sâu vì skill sửa trực tiếp trên state)
        profile_before = {key: field.model_copy(deep=True) for key, field in state.profile.items()}
        message = last_user_message(req)
        # chương trình lấy thời điểm kết thúc trừ thời điểm bắt đầu để tính thời gian xử lý.
        started = time.perf_counter()
        # Đang chờ học sinh chọn cách điền hồ sơ: nhận lựa chọn bằng chữ trên nút, không nhờ model.
        # Nút "Tiếp tục bằng chat" trên form thì hỏi tiếp trong chat dù chat không đang chờ chọn
        continue_chat = message.strip() == CONTINUE_CHAT
        if state.question_id == INTAKE_ID or continue_chat:
            if state.question_id == INTAKE_ID:
                state.question_id = None
            choice = "chat" if continue_chat else intake_choice(message)
            if choice:
                output = begin_intake(state, choice)
                log_turn(
                    state,
                    message,
                    [],
                    {},
                    Plan(skills=[], source="fallback_step"),
                    (time.perf_counter() - started) * 1000,
                    req.session_id,
                )
                return ChatResponse(
                    answer=output.answer,
                    state=state,
                    quick_replies=output.quick_replies,
                    widget=output.widget,
                    profile=state.profile,
                    profile_changed=changed_keys(profile_before, state.profile),
                )

        # 1. Model chọn skill(lỗi thì coi như không chắc, dùng skill của bước hiện tại).
        # Bấm nút của câu đang hỏi ở B1, B2 là câu trả lời rõ: dùng skill của bước, không nhờ model
        # (model từng đoán nút "Bỏ qua" là muốn dừng nên kết thúc cuộc trò chuyện giữa chừng)
        if state.current_step in INTAKE_STEPS and is_button(state.question_id, message):
            predicted, probs = [], {}
        else:
            predicted, probs = await self._choose(state, message)
        # Kết thúc chỉ khi học sinh chỉ muốn dừng; đi kèm skill khác thì học sinh đang trả lời, không dừng
        if len(predicted) > 1:
            predicted = [skill for skill in predicted if skill != END_SKILL]

        # 2. Code kiểm luật: tối đa 2 skill, thứ tự chạy, điều kiện chạy
        plan = plan_skills(state, predicted, has_results=state.has_results)

        # 3. Chạy từng skill, ghép các câu trả lời
        # Câu đang chờ học sinh trả lời, chụp trước khi chạy skill để biết skill có hỏi sang câu khác không
        pending = (state.question_id, state.last_question)
        not_ready = False
        answers: list[str] = []
        quick_replies: list[QuickReply] = []
        widget: Widget | None = None
        citations: list[Citation] = []
        disclaimer: str | None = None
        action: str | None = None
        action_data: dict[str, Any] = {}
        skills = list(plan.skills)
        for skill in skills:
            # Lấy ra handler của skill, nếu không có thì trả NOT_READY
            handler = self._handlers.get(skill)
            if handler is None:
                if handler is None:
                    not_ready = True
                    continue
            step_before = state.current_step
            output = await handler.run(state, message)
            # Skill chỉ chuyển bước (câu trả lời rỗng) thì không thêm câu nào, skill của bước mới sẽ trả lời
            if output.answer:
                answers.append(output.answer)
            # Mỗi lượt chỉ hiện một bộ nút và một công cụ nhập: lấy của skill chạy sau cùng có đưa ra
            quick_replies = output.quick_replies or quick_replies
            widget = output.widget or widget
            citations += [c for c in output.citations if c not in citations]
            disclaimer = output.disclaimer or disclaimer
            if output.action:
                action, action_data = output.action, output.action_data
            # Rà soát hồ sơ xong (skill_ho_so vừa chuyển sang B3, không còn câu chờ): đánh giá luôn trong lượt này,
            # không bắt học sinh nhắn thêm một lần nữa mới thấy kết quả
            if state.current_step == "B3" and state.question_id is None and ASSESS_SKILL not in skills:
                skills.append(ASSESS_SKILL)
            if state.current_step != step_before and state.current_step in CHAIN_STEPS:
                next_skill = STEP_SKILL[state.current_step]
                if next_skill not in skills:
                    skills.append(next_skill)
        # Skill chưa có handler chỉ báo "đang hoàn thiện" khi chưa có skill nào trả lời được
        if not_ready and not answers:
            # Lượt đầu mà chưa skill nào trả lời được: chào như cũ thay vì báo "đang hoàn thiện"
            answers.append(QUESTIONS["B0.1"] if first_turn else NOT_READY)
        # Nếu bị chặn thì thêm câu trả lời
        # Bị chặn vì thiếu hồ sơ thì bắt đầu hỏi hồ sơ thay vì chỉ báo thiếu
        blocked_profile = next((b for b in plan.blocked if b.reason == "thieu_ho_so"), None)
        if blocked_profile is not None:
            intake = start_intake(state, REQUIRED_BY_SKILL.get(blocked_profile.skill, REQUIRED_B3))
            answers.append(intake.answer)
            quick_replies = intake.quick_replies or quick_replies
            widget = intake.widget or widget
        # Đến B4 mà chưa có ngành thì hiện ô chọn ngành
        elif any(b.reason == "thieu_nganh" for b in plan.blocked):
            major = ask_major(state)
            answers.append(major.answer)
            widget = major.widget or widget
        # Các lý do chặn khác thì báo cho học sinh biết (thiếu hồ sơ và thiếu ngành đã xử lý ở trên)
        # Đã có skill khác trả lời rồi thì không thêm câu "cần xem danh sách trường trước" (câu thừa, không liên quan)
        skip_blocked = {"thieu_ho_so", "thieu_nganh"}
        if any(answers):
            skip_blocked.add("chua_co_danh_sach")
        answers += blocked_answers(plan, skip=skip_blocked)

        # Trả lời xen ngang lúc đang hỏi hồ sơ: nhắc lại câu đang chờ (không gọi ask nên không tăng số lần hỏi)
        if (
            INTERRUPT_SKILLS & set(plan.skills)
            and all(pending)
            and pending[0] not in OPENING_QUESTIONS
            and (state.question_id, state.last_question) == pending
        ):
            answers.append(BACK_TO_QUESTION + pending[1])
        # 4. Ghi log (dữ liệu để train lại model và kiểm tra random check)
        log_turn(
            state, message, predicted, probs, plan, (time.perf_counter() - started) * 1000, req.session_id
        )

        return ChatResponse(
            answer="\n\n".join(dict.fromkeys(answers)) or NOT_READY,
            state=state,
            skills=skills,
            quick_replies=quick_replies,
            widget=widget,
            citations=citations,
            disclaimer=disclaimer,
            profile=state.profile,
            profile_changed=changed_keys(profile_before, state.profile),
            action=action,
            action_data=action_data,
        )

    # Gọi model; lỗi (chưa nạp model, Ollama tắt...) thì trả về "không chắc"
    async def _choose(self, state: ChatState, message: str) -> tuple[list[str], dict[str, float]]:
        if self._picker is None:
            return [], {}
        try:
            result = await self._picker.predict(
                state.current_step, state.last_question, message, missing_fields(state.profile)
            )
        except Exception:
            logger.exception("Model chọn skill lỗi, dùng skill của bước hiện tại")
            return [], {}
        return list(result.skills), dict(result.probs)

    # Tin nhắn mới nhất của học sinh


def last_user_message(req: ChatRequest) -> str:
    for message in reversed(req.messages):
        if message.role == "user":
            return message.content
    return req.messages[-1].content


# Tin nhắn là chữ trên một nút (hoặc câu ghép của ô nhập chứng chỉ, hoạt động...) của câu đang hỏi
def is_button(question_id: str | None, message: str) -> bool:
    # Nhập muộn vì buttons, skill_ho_so nhập chat_flow
    from app.services.skills import buttons, skill_ho_so

    return buttons.pressed(question_id, message, can_skip=True) is not None or skill_ho_so.is_form_text(
        question_id, message
    )


# Skill bị chặn thì nói cho học sinh biết vì sao
# Câu nói cho học sinh biết vì sao skill bị chặn, theo lý do
BLOCKED_ANSWER = {
    "thieu_ho_so": NEED_PROFILE,
    "thieu_nganh": NEED_MAJOR,
    "chua_co_danh_sach": NEED_RESULTS,
}


# Skill bị chặn thì nói cho học sinh biết vì sao (bỏ qua các lý do trong skip)
def blocked_answers(plan: Plan, skip: set[str] | None = None) -> list[str]:
    return [BLOCKED_ANSWER[item.reason] for item in plan.blocked if item.reason not in (skip or set())]


# Bắt đầu hỏi hồ sơ khi một skill bị chặn vì thiếu hồ sơ
def start_intake(state: ChatState, required: list[str] = REQUIRED_B3) -> SkillOutput:
    # Đang trả lời trong chat thì hỏi tiếp phần còn thiếu
    if state.intake_mode == "chat":
        return ask_next_missing(state, INTAKE_MORE)
    # Chưa chọn, hoặc đã chọn form mà vẫn còn thiếu: mời chọn chat hay form (nói còn thiếu mấy mục)
    state.question_id = INTAKE_ID
    # Câu mời này nằm ngoài 23 câu model đã học nên để rỗng, model sẽ nhìn như "không có câu nào đang chờ"
    state.last_question = ""
    return SkillOutput(intake_ask(state.profile, required), [CHAT_BUTTON, FORM_BUTTON])


# Đến B4 mà chưa có ngành: hiện ô chọn ngành, câu trả lời sẽ được skill_bac_hoc đọc ở bước B1
def ask_major(state: ChatState) -> SkillOutput:
    state.return_to = "B4"  # chọn xong ngành thì quay lại B4
    go_to(state, "B1")  # model chỉ được học câu hỏi ngành ở bước B1, nên trả về B1 để chọn đúng skill
    ask(state, "B1.3")  # lưu mã câu và chữ câu hỏi ngành (học sinh vẫn thấy câu NEED_MAJOR bên dưới)
    return SkillOutput(NEED_MAJOR, widget=Widget(type="major"))


# Học sinh đã chọn: form thì mở form, chat thì hỏi câu đầu tiên còn thiếu
def begin_intake(state: ChatState, choice: str) -> SkillOutput:
    if choice == "form":
        state.intake_mode = "form"
        return SkillOutput(INTAKE_FORM, widget=Widget(type="form"))
    return ask_next_missing(state)


# Hỏi lần lượt mọi mục của form còn thiếu (B1 rồi B2), kèm nút / ô chọn của từng câu;
# lượt sau skill của bước đó đọc câu trả lời
def ask_next_missing(state: ChatState, prefix: str = "") -> SkillOutput:
    # Nhập muộn vì các skill nhập chat_flow
    from app.services.skills import skill_bac_hoc

    state.intake_mode = "chat"
    go_to(state, "B1")  # skill_bac_hoc hết câu B1 thì tự sang B2
    output = skill_bac_hoc.ask_next(state)
    output.answer = prefix + output.answer
    return output


# Mỗi lượt ghi một dòng JSON: tin nhắn, model chọn gì, code chạy gì
def log_turn(
    state: ChatState,
    message: str,
    predicted: list[str],
    probs: dict[str, float],
    plan: Plan,
    ms: float,
    session_id: str | None = None,
) -> None:
    logger.info(
        "chat_turn %s",
        json.dumps(
            {
                "session_id": session_id,
                "step": state.current_step,
                "last_question": state.last_question,
                "message": mask_personal_info(message),
                "model_skills": predicted,
                "probs": {k: round(v, 3) for k, v in probs.items()},
                "source": plan.source,
                "ran": plan.skills,
                "blocked": [{"skill": b.skill, "reason": b.reason} for b in plan.blocked],
                "ms": round(ms),
            },
            ensure_ascii=False,
        ),
    )
