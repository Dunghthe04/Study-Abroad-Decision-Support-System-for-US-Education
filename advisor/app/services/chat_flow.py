"""
Điều phối luồng chat theo kiến trúc mới: SLM đọc tin nhắn, chạy nhánh theo loại câu hỏi, trả câu trả lời và trạng thái.
Vòng xử lý một tin nhắn:
1. SLM phân loại (nhãn chính, nhãn phụ, thực thể) -> 2. chọn nhánh -> 3. chạy từng nhánh, ghép câu trả lời
-> 4. nhắc lại câu hồ sơ đang chờ nếu chưa được trả lời -> 5. ghi log, trả kết quả.
Các nhánh nằm ở branches.py (main.py tạo rồi truyền vào ChatFlow).
"""

import json
import logging
import time
from dataclasses import dataclass, field
from typing import Any, Protocol

from app.schemas.chat import (
    ChatRequest,
    ChatResponse,
    ChatState,
    Citation,
    ProfileField,
    QuickReply,
    Step,
    Widget,
)
from app.schemas.slm import SlmResult
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
from app.services.skill_rules import REQUIRED_B3
from app.services.slm_contract import SlmClassifier

# Tạo 1 logger
logger = logging.getLogger("advisor.chat")

# Câu hỏi lấy nguyên văn từ framework, AI không tự đặt câu hỏi
# Câu hỏi lấy nguyên văn từ framework, AI không tự đặt câu hỏi.
# Mỗi mã một câu riêng (có test kiểm tra), vì model chọn skill (SLM) nhìn chữ của câu này để biết đang chờ mã nào.
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
NEED_MAJOR = "Để gợi ý trường mình cần biết ngành bạn muốn học. Bạn chọn ngành giúp mình nhé."
BACK_TO_QUESTION = "Quay lại câu mình đang hỏi nhé: "
OPENING_QUESTIONS = {"B0.1", "B0.2"}


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


# Hợp đồng chung của mọi nhánh (branches.py): như SkillHandler nhưng nhận thêm kết quả SLM (thực thể, nhãn phụ...)
class Branch(Protocol):
    async def run(self, state: ChatState, message: str, slm: SlmResult) -> SkillOutput: ...


# Ghép câu trả lời của 2 phần chạy liền nhau: nối chữ, lấy nút / công cụ / action của phần sau nếu có
def combine(first: SkillOutput, second: SkillOutput) -> SkillOutput:
    return SkillOutput(
        answer="\n\n".join(a for a in (first.answer, second.answer) if a),
        quick_replies=second.quick_replies or first.quick_replies,
        widget=second.widget or first.widget,
        citations=first.citations + [c for c in second.citations if c not in first.citations],
        disclaimer=second.disclaimer or first.disclaimer,
        action=second.action or first.action,
        action_data=second.action_data if second.action else first.action_data,
    )


# Nhánh chạy cho một tin: nhãn chính, nhãn phụ, rồi nhánh hồ sơ cuối cùng nếu tin có kèm giá trị hồ sơ
# (vd "8.5 ạ, mà I-20 là gì": trả lời kiến thức trước, ghi GPA và hỏi câu hồ sơ kế tiếp sau)
def branches_to_run(slm: SlmResult) -> list[str]:
    names = [slm.type, *([slm.nhan_phu] if slm.nhan_phu else [])]
    if slm.touches_profile() and "mac_dinh" not in names:
        names.append("mac_dinh")
    return names


class ChatFlow:
    # slm: bộ phân loại tin nhắn (OllamaSlm, test dùng FixedSlm). branches: loại câu hỏi -> nhánh (build_branches)
    def __init__(self, slm: SlmClassifier, branches: dict[str, Branch]) -> None:
        self._slm = slm
        self._branches = branches

    async def reply(self, req: ChatRequest) -> ChatResponse:
        # Cuộc trò chuyện mới: giao diện đã tự hiện lời chào, nên tin nhắn đầu tiên là câu trả lời cho B0.1.
        # Ghi nhận đã chào rồi xử lý tin nhắn như mọi lượt khác, không bỏ qua câu học sinh vừa gõ.
        first_turn = req.state is None
        if first_turn:
            state = ChatState(study_level=req.study_level)
            ask(state, "B0.1")
        else:
            state = req.state.model_copy(deep=True)
        if req.study_level:  # .NET gửi bậc học mỗi lượt; không gửi thì giữ bậc đã nhớ
            state.study_level = req.study_level
        # Hồ sơ trong nháp (do .NET gửi) là nguồn thật; không gửi thì giữ hồ sơ trong state như cũ
        if req.profile is not None:
            state.profile = merge_profile(state.profile, req.profile)
        # Danh sách trường gợi ý do .NET gửi mỗi lượt; không gửi thì giữ như cũ
        if req.schools is not None:
            state.schools = req.schools
        # Chụp hồ sơ đầu lượt để cuối lượt biết khóa nào đổi (sao chép sâu vì nhánh sửa trực tiếp trên state)
        profile_before = {key: field.model_copy(deep=True) for key, field in state.profile.items()}
        message = last_user_message(req)
        started = time.perf_counter()

        # Đang chờ học sinh chọn cách điền hồ sơ: nhận lựa chọn bằng chữ trên nút, không cần SLM.
        # Nút "Tiếp tục bằng chat" trên form thì hỏi tiếp trong chat dù chat không đang chờ chọn
        continue_chat = message.strip() == CONTINUE_CHAT
        if state.question_id == INTAKE_ID or continue_chat:
            if state.question_id == INTAKE_ID:
                state.question_id = None
            choice = "chat" if continue_chat else intake_choice(message)
            if choice:
                output = begin_intake(state, choice)
                log_turn(state, message, None, [], (time.perf_counter() - started) * 1000, req.session_id)
                return self._response(state, output, [], profile_before)

        # Câu hồ sơ đang chờ, chụp trước khi chạy nhánh để cuối lượt biết đã được trả lời chưa
        pending = (state.question_id, state.last_question)
        # 1. SLM đọc tin nhắn. Bấm nút của câu đang hỏi là trả lời hồ sơ rõ ràng, không cần SLM đoán
        if state.question_id and is_button(state.question_id, message):
            slm = SlmResult(type="mac_dinh")
        else:
            slm = await self._slm.classify(state, message)

        # 2-3. Chạy từng nhánh theo thứ tự, ghép câu trả lời
        names = branches_to_run(slm)
        output = SkillOutput("")
        for name in names:
            output = combine(output, await self._branches[name].run(state, message, slm))

        # 4. Học sinh hỏi xen ngang lúc đang hỏi hồ sơ (câu đang chờ chưa được trả lời): nhắc lại câu đang chờ.
        # Không nhắc khi nhánh hồ sơ đã chạy (nó tự hỏi câu kế tiếp) hoặc khi câu đang chờ là câu chào mở đầu
        if (
            pending[0]
            and pending[0] not in OPENING_QUESTIONS
            and "mac_dinh" not in names
            and (state.question_id, state.last_question) == pending
        ):
            output = combine(output, SkillOutput(BACK_TO_QUESTION + pending[1]))
        # Lượt đầu mà chưa nhánh nào trả lời được: chào như cũ thay vì "đang hoàn thiện"
        if not output.answer:
            output.answer = QUESTIONS["B0.1"] if first_turn else NOT_READY

        # 5. Ghi log (dữ liệu để gán nhãn thêm và kiểm tra ngẫu nhiên)
        log_turn(state, message, slm, names, (time.perf_counter() - started) * 1000, req.session_id)
        return self._response(state, output, names, profile_before)

    @staticmethod
    def _response(
        state: ChatState, output: SkillOutput, names: list[str], profile_before: dict[str, ProfileField]
    ) -> ChatResponse:
        return ChatResponse(
            answer=output.answer,
            state=state,
            skills=names,
            quick_replies=output.quick_replies,
            widget=output.widget,
            citations=output.citations,
            disclaimer=output.disclaimer,
            profile=state.profile,
            profile_changed=changed_keys(profile_before, state.profile),
            action=output.action,
            action_data=output.action_data,
        )


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


# Mỗi lượt ghi một dòng JSON: tin nhắn, SLM đọc ra gì, nhánh nào chạy.
# Chỉ ghi tên khóa thực thể, không ghi giá trị (giá trị là chữ học sinh viết, có thể chứa thông tin cá nhân)
def log_turn(
    state: ChatState,
    message: str,
    slm: SlmResult | None,
    ran: list[str],
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
                "slm_type": slm.type if slm else None,
                "slm_nhan_phu": slm.nhan_phu if slm else None,
                "slm_thuc_the": sorted(slm.thuc_the) if slm else [],
                "slm_confidence": round(slm.confidence, 3) if slm else None,
                "ran": ran,
                "ms": round(ms),
            },
            ensure_ascii=False,
        ),
    )
