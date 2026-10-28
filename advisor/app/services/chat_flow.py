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

from app.schemas.chat import ChatRequest, ChatResponse, ChatState, QuickReply, Step, Widget
from app.services.profile_sync import changed_keys, merge_profile
from app.services.skill_rules import Plan, missing_fields, plan_skills

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
        # Cuộc trò chuyện mới chưa có trạng thái -> mở đầu bằng B0.1
        if req.state is None:
            state = ChatState()
            # Đã có hồ sơ (vào chat từ form) thì nạp sẵn để không hỏi lại
            if req.profile is not None:
                state.profile = merge_profile(state.profile, req.profile)
            return ChatResponse(answer=ask(state, "B0.1"), state=state, profile=state.profile)

        # Sao chép đối tượng
        state = req.state.model_copy(deep=True)
        # Hồ sơ trong nháp (do .NET gửi) là nguồn thật; không gửi thì giữ hồ sơ trong state như cũ
        if req.profile is not None:
            state.profile = merge_profile(state.profile, req.profile)
        # Chụp hồ sơ đầu lượt để cuối lượt biết khóa nào đổi (sao chép sâu vì skill sửa trực tiếp trên state)
        profile_before = {key: field.model_copy(deep=True) for key, field in state.profile.items()}
        message = last_user_message(req)
        # chương trình lấy thời điểm kết thúc trừ thời điểm bắt đầu để tính thời gian xử lý.
        started = time.perf_counter()

        # 1. Model chọn skill(lỗi thì coi như không chắc, dùng skill của bước hiện tại)
        predicted, probs = await self._choose(state, message)

        # 2. Code kiểm luật: tối đa 2 skill, thứ tự chạy, điều kiện chạy
        plan = plan_skills(state, predicted, has_results=state.has_results)

        # 3. Chạy từng skill, ghép các câu trả lời
        answers: list[str] = []
        quick_replies: list[QuickReply] = []
        widget: Widget | None = None
        for skill in plan.skills:
            # Lấy ra handler của skill, nếu không có thì trả NOT_READY
            handler = self._handlers.get(skill)
            if handler is None:
                answers.append(NOT_READY)
                continue
            output = await handler.run(state, message)
            answers.append(output.answer)
            # Mỗi lượt chỉ hiện một bộ nút và một công cụ nhập: lấy của skill chạy sau cùng có đưa ra
            quick_replies = output.quick_replies or quick_replies
            widget = output.widget or widget
        # Nếu bị chặn thì thêm câu trả lời
        answers += blocked_answers(plan)

        # 4. Ghi log (dữ liệu để train lại model và kiểm tra random check)
        log_turn(state, message, predicted, probs, plan, (time.perf_counter() - started) * 1000)

        return ChatResponse(
            answer="\n\n".join(dict.fromkeys(answers)) or NOT_READY,
            state=state,
            skills=plan.skills,
            quick_replies=quick_replies,
            widget=widget,
            profile=state.profile,
            profile_changed=changed_keys(profile_before, state.profile),
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


# Skill bị chặn thì nói cho học sinh biết vì sao
# Câu nói cho học sinh biết vì sao skill bị chặn, theo lý do
BLOCKED_ANSWER = {
    "thieu_ho_so": NEED_PROFILE,
    "thieu_nganh": NEED_MAJOR,
    "chua_co_danh_sach": NEED_RESULTS,
}


# Skill bị chặn thì nói cho học sinh biết vì sao
def blocked_answers(plan: Plan) -> list[str]:
    return [BLOCKED_ANSWER[item.reason] for item in plan.blocked]


# Mỗi lượt ghi một dòng JSON: tin nhắn, model chọn gì, code chạy gì
def log_turn(
    state: ChatState, message: str, predicted: list[str], probs: dict[str, float], plan: Plan, ms: float
) -> None:
    logger.info(
        "chat_turn %s",
        json.dumps(
            {
                "step": state.current_step,
                "last_question": state.last_question,
                "message": message,
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
