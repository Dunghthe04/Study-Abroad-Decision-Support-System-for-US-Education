"""
Điều phối luồng chat theo kiến trúc mới: SLM đọc tin nhắn, chạy nhánh theo loại câu hỏi, trả câu trả lời và trạng thái.
Vòng xử lý một tin nhắn:
1. SLM tách tin thành danh sách ý -> 2. mỗi ý chạy nhánh theo loại của nó (ý mac_dinh chạy trước), ghép câu trả lời
-> 3. nhắc lại câu hồ sơ đang chờ nếu chưa được trả lời -> 4. ghi log, trả kết quả.
Các nhánh nằm ở branches.py (main.py tạo rồi truyền vào ChatFlow).
"""

import asyncio
import json
import logging
import re
import time
from collections.abc import Callable
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
from app.schemas.slm import SlmTinResult, SlmY
from app.services import usage_meter
from app.services.conversation_context import contextualize, update_context
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
from app.services.priority import PRIORITY_KEY, detect_priorities, unaccent
from app.services.profile_sync import changed_keys, merge_profile
from app.services.skill_rules import REQUIRED_B3, missing_fields
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
    "E3": "Bạn có kế hoạch thi bài chuẩn hóa nào (SAT, ACT...) không, dự kiến khi nào và mục tiêu bao nhiêu điểm?",
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
# Loại ý chỉ ĐỌC state (kien_thuc, trung_tam chỉ đọc study_level), nên nhiều ý cùng loại này chạy song song được.
# Loại khác (mac_dinh, ho_so...) ghi state nên vẫn chạy lần lượt. Thêm loại vào đây chỉ khi nhánh đó không ghi state
PARALLEL_SAFE = {"kien_thuc", "trung_tam"}
# Số ý chạy cùng lúc nhiều nhất trong một tin: tin hỏi 12 câu thì không gửi 12 yêu cầu Claude cùng một lúc
PARALLEL_LIMIT = 4
# Ý mac_dinh thay thế khi SLM không tách ý hồ sơ nhưng tin vẫn có giá trị hồ sơ (nằm trong ý khác)
PROFILE_ONLY = SlmY(type="mac_dinh", doan="thông tin hồ sơ")
# Chế độ stream: ChatFlow gọi hàm này mỗi khi có tin để gửi cho trình duyệt ngay, không chờ cả lượt xong.
# Hai loại tin: {"type": "progress", "answers": số ý sắp trả lời} và {"type": "partial", "text": câu trả lời của một ý}
Emit = Callable[[dict[str, Any]], None]


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


# Hợp đồng chung của mọi nhánh (branches.py): như SkillHandler nhưng nhận thêm kết quả SLM.
# message và slm là phần dành cho nhánh này: nhánh hồ sơ nhận cả tin (gom giá trị hồ sơ của mọi ý),
# nhánh khác nhận đoạn chữ và kết quả của đúng một ý (slm.cac_y chỉ có ý đó)
class Branch(Protocol):
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput: ...


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


# Các ý chạy cho một tin, ý mac_dinh đứng đầu để hồ sơ được ghi trước khi các ý khác chạy.
# SLM không tách ý hồ sơ mà tin vẫn có giá trị hồ sơ (nằm trong ý khác) thì thêm một ý mac_dinh
def intents_to_run(slm: SlmTinResult) -> list[SlmY]:
    if slm.touches_profile() and "mac_dinh" not in slm.types():
        return [PROFILE_ONLY, *slm.cac_y]
    return list(slm.cac_y)


# Câu nói về ưu tiên (chi phí thấp, học bổng, danh tiếng) SLM chưa được huấn luyện nên có thể xếp nhầm loại.
# Quy tắc (priority.py) nhận ra thì đổi thành ý what-if (chưa có kết quả gợi ý thì nhánh kịch bản nhắc cần gợi ý trước),
# trừ khi đang chờ trả lời một câu hồ sơ (để "em cần học bổng" trả lời câu hỏi hồ sơ không bị hiểu nhầm).
# Câu chào mở đầu (B0.1, B0.2) không phải câu hồ sơ nên không chặn
def with_priority_intent(state: ChatState, message: str, slm: SlmTinResult) -> SlmTinResult:
    waiting = state.question_id is not None and state.question_id not in OPENING_QUESTIONS
    if waiting or not detect_priorities(message):
        return slm
    # SLM thật xếp "ưu tiên chi phí thấp" vào goal_seeking không có trường: đó là ưu tiên, đổi thành what-if.
    # Giữ nguyên khi SLM đã nhận là what-if (nhánh kịch bản tự đọc thêm ưu tiên) hoặc goal-seeking có nêu trường thật
    scenarios = [y for y in slm.cac_y if y.type == "kich_ban"]
    if any(y.thuc_the.get("che_do") == "what_if" or y.thuc_the.get("truong") for y in scenarios):
        return slm
    scenario = SlmY(
        type="kich_ban",
        doan=message,
        thuc_the={"che_do": "what_if", "thay_doi": [{"yeu_to": PRIORITY_KEY, "gia_tri": "uu_tien"}]},
    )
    # Tin có số liệu thì giữ các ý khác (có thể là hồ sơ); chỉ nói về ưu tiên thì bỏ ý SLM đoán sai
    keep = [y for y in slm.cac_y if y.type != "kich_ban"] if re.search(r"\d", message) else []
    return slm.model_copy(update={"cac_y": [scenario, *keep]})


# Mục số liệu trường học sinh hay hỏi (khớp bộ từ khóa của .NET ChatSchoolInfo) và dấu hiệu của câu thật sự nhạy cảm (làm giả, khai gian)
_INFO_WORDS = re.compile(
    r"\b(gpa|hoc phi|chi phi|hoc bong|ielts|toefl|duolingo|sat|act|ty le nhan|han nop|xep hang|ranking|ty le tot nghiep|nganh)\b"
)
_SENSITIVE = re.compile(
    r"lam gia|gia mao|gian lan|\bbia\b|khai gian|khai khong|sua (gpa|diem|bang)|nang (gpa|diem)|\bfake\b|khong that|mua (bang|diem)|thue (viet|lam)|cheat|hack"
)


# SLM thật xếp nhầm "GPA của The University of Texas at Austin là bao nhiêu" vào ngoại phạm vi nhạy cảm ("hồ sơ cần trung thực").
# Câu nhắc một mục số liệu trường mà không có dấu hiệu làm giả hay khai gian thì đổi thành câu kiến thức, để nhánh số liệu trường trả lời.
# Câu nhạy cảm thật (sửa GPA, làm giả bảng điểm) vẫn bị chặn như cũ
def with_info_intent(message: str, slm: SlmTinResult) -> SlmTinResult:
    if len(slm.cac_y) != 1:
        return slm
    y = slm.cac_y[0]
    if y.type != "ngoai_pham_vi" or y.thuc_the.get("loai_con") not in ("nhay_cam", "mo_ho"):
        return slm
    plain = unaccent(message)
    if not _INFO_WORDS.search(plain) or _SENSITIVE.search(plain):
        return slm
    return slm.model_copy(update={"cac_y": [SlmY(type="kien_thuc", doan=message)]})


# Phần kết quả SLM giao cho nhánh của một ý: nhánh hồ sơ cần cả tin, nhánh khác chỉ cần ý của nó
def view_of(slm: SlmTinResult, y: SlmY) -> SlmTinResult:
    return slm if y.type == "mac_dinh" else slm.model_copy(update={"cac_y": [y]})


# Tái tạo đầy đủ SkillOutput (gồm quick_replies và widget) của câu hỏi đang chờ khi người dùng hỏi xen ngang
def recreate_question_output(state: ChatState, question_id: str, question_text: str) -> SkillOutput:
    from app.services.skills import buttons, skill_bac_hoc, skill_ho_so

    saved_asked = dict(state.asked)
    if question_id == "X1":
        remind_out = skill_ho_so.review(state)
    elif question_id in skill_bac_hoc.QUESTION_FIELDS:
        remind_out = skill_bac_hoc.ask_question(state, question_id)
    elif (
        question_id in skill_ho_so.QUESTION_FIELDS
        or question_id in buttons.WIDGETS
        or buttons.select_fields(question_id)
    ):
        remind_out = skill_ho_so.ask_question(state, question_id, question_text)
    else:
        remind_out = buttons.ask_with_buttons(state, question_id)
    state.asked = saved_asked

    return SkillOutput(
        answer=BACK_TO_QUESTION + (question_text or remind_out.answer),
        quick_replies=remind_out.quick_replies,
        widget=remind_out.widget,
    )


class ChatFlow:
    # slm: bộ phân loại tin nhắn (OllamaSlm, test dùng FixedSlm). branches: loại câu hỏi -> nhánh (build_branches)
    def __init__(self, slm: SlmClassifier, branches: dict[str, Branch]) -> None:
        self._slm = slm
        self._branches = branches

    # emit: chỉ truyền khi stream (route /api/v1/chat/stream); không truyền thì chạy như cũ, trả một lần cả câu
    async def reply(self, req: ChatRequest, emit: Emit | None = None) -> ChatResponse:
        # Mở danh sách token cho lượt này: mọi lần gọi AI trong lượt (kể cả chạy song song) ghi vào đây
        calls = usage_meter.start()
        response = await self._reply(req, emit)
        response.ai_usage = list(calls)
        return response

    async def _reply(self, req: ChatRequest, emit: Emit | None = None) -> ChatResponse:
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
            # Học bạ đi cùng hồ sơ: lượt .NET gửi hồ sơ thì thay luôn học bạ, kể cả None (đã chuyển sang GPA tổng)
            state.transcript = req.transcript
        # Mốc trường đổi theo bậc học; không gửi thì giữ mốc đã nhớ
        if req.benchmarks is not None:
            state.benchmarks = req.benchmarks
        # Danh sách trường gợi ý do .NET gửi mỗi lượt (lấy từ lần gợi ý đã lưu); không gửi thì giữ như cũ.
        # Có danh sách nghĩa là học sinh đã có kết quả, dù gợi ý ở trang Gợi ý trường chứ không phải trong chat
        if req.schools is not None:
            state.schools = req.schools
            state.has_results = state.has_results or bool(req.schools)
        if req.other_schools is not None:
            state.other_schools = req.other_schools
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
            # Điền xong form rồi quay về chat: form đã là bước rà soát nên không hỏi X1 nữa,
            # trả lời luôn câu tư vấn bị hoãn. Hồ sơ còn thiếu thì begin_intake hỏi tiếp trong chat như cũ
            if continue_chat and state.pending_advice is not None and profile_ready(state):
                original = state.pending_advice
                state.pending_advice = None
                state.intake_mode = "chat"
                output = await self._branches["ho_so"].run(
                    state, original.doan, SlmTinResult(cac_y=[original])
                )
                log_turn(
                    state, message, None, ["ho_so"], (time.perf_counter() - started) * 1000, req.session_id
                )
                return self._response(state, output, ["ho_so"], profile_before)
            if choice:
                output = begin_intake(state, choice)
                log_turn(state, message, None, [], (time.perf_counter() - started) * 1000, req.session_id)
                return self._response(state, output, [], profile_before)

        # Câu hồ sơ đang chờ, chụp trước khi chạy nhánh để cuối lượt biết đã được trả lời chưa
        pending = (state.question_id, state.last_question)
        # 1. SLM đọc tin nhắn. Bấm nút của câu đang hỏi là trả lời hồ sơ rõ ràng, không cần SLM đoán
        if state.question_id and is_button(state.question_id, message):
            slm = SlmTinResult(cac_y=[SlmY(type="mac_dinh", doan=message)])
        else:
            slm = await self._slm.classify(state, message)
            slm = with_priority_intent(state, message, slm)
            slm = with_info_intent(message, slm)
            slm = contextualize(state, message, slm)

        # 2. Chạy từng ý theo thứ tự, ghép câu trả lời
        names, output = await self._run_intents(state, message, slm, emit)

        # 3-4. Học sinh hỏi xen ngang lúc đang hỏi hồ sơ (câu đang chờ chưa được trả lời): nhắc lại câu đang chờ.
        # Không nhắc khi nhánh hồ sơ đã chạy (nó tự hỏi câu kế tiếp) hoặc khi câu đang chờ là câu chào mở đầu
        remind = bool(
            pending[0]
            and pending[0] not in OPENING_QUESTIONS
            and "mac_dinh" not in names
            and (state.question_id, state.last_question) == pending
        )
        if remind:
            remind_output = recreate_question_output(state, pending[0], pending[1])
            output = combine(output, remind_output)

        # Lượt đầu mà chưa nhánh nào trả lời được: chào như cũ thay vì "đang hoàn thiện"
        if not output.answer:
            output.answer = QUESTIONS["B0.1"] if first_turn else NOT_READY

        # Nhớ trường, mục, điều kiện vừa nói để hiểu câu hỏi tiếp
        update_context(state, output)

        # 5. Ghi log (dữ liệu để gán nhãn thêm và kiểm tra ngẫu nhiên)
        log_turn(state, message, slm, names, (time.perf_counter() - started) * 1000, req.session_id)
        return self._response(state, output, names, profile_before)

    # Mỗi ý chạy nhánh của loại nó. Ý mac_dinh chạy TRƯỚC để các ý khác thấy hồ sơ mới, nhưng câu trả lời của nó
    # đặt CUỐI để tin nhắn kết thúc bằng câu hồ sơ kế tiếp (hoặc lời xác nhận).
    # Các ý chỉ đọc state (PARALLEL_SAFE) chạy song song (tối đa PARALLEL_LIMIT ý cùng lúc) cho nhanh;
    # Khi stream (emit), mỗi ý xong thì gửi đi ngay, nhưng chỉ khi mọi ý đứng trước nó đã gửi (đúng thứ tự hỏi)
    async def _run_intents(
        self, state: ChatState, message: str, slm: SlmTinResult, emit: Emit | None = None
    ) -> tuple[list[str], SkillOutput]:
        intents = intents_to_run(slm)
        results: dict[int, SkillOutput] = {}
        sent = 0  # số ý đầu danh sách đã xét gửi đi
        if emit:
            emit({"type": "progress", "answers": sum(y.type != "mac_dinh" for y in intents)})

        def flush() -> None:
            nonlocal sent
            while sent in results:
                answer = results[sent].answer
                # Câu trả lời ý mac_dinh đặt cuối tin (câu hồ sơ kế tiếp) nên không gửi theo từng ý
                if emit and intents[sent].type != "mac_dinh" and answer:
                    emit({"type": "partial", "text": answer})
                sent += 1

        # Ý ghi state chạy lần lượt theo thứ tự (mac_dinh đứng đầu)
        for i, y in enumerate(intents):
            if y.type not in PARALLEL_SAFE:
                results[i] = await self._run_one(state, message, slm, y)
                flush()
        # Ý chỉ đọc state chạy cùng lúc, sau khi hồ sơ đã được ghi xong
        parallel = [i for i, y in enumerate(intents) if y.type in PARALLEL_SAFE]
        gate = asyncio.Semaphore(PARALLEL_LIMIT)

        async def limited(i: int) -> None:
            async with gate:
                results[i] = await self._run_one(state, message, slm, intents[i])
            flush()

        await asyncio.gather(*(limited(i) for i in parallel))
        output = SkillOutput("")
        profile_output: SkillOutput | None = None
        for i, y in enumerate(intents):
            if y.type == "mac_dinh":
                profile_output = results[i]
            else:
                output = combine(output, results[i])
        if profile_output is not None:
            output = combine(output, profile_output)
        return [y.type for y in intents], output

    # Chạy nhánh của một ý. Ý khác nhận đoạn chữ của ý (nhánh kiến thức nhờ đó tìm kho bằng đúng câu hỏi),
    # ý hồ sơ nhận cả tin
    async def _run_one(self, state: ChatState, message: str, slm: SlmTinResult, y: SlmY) -> SkillOutput:
        return await self._branches[y.type].run(
            state, message if y.type == "mac_dinh" else y.doan, view_of(slm, y)
        )

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


# Hồ sơ đủ mục bắt buộc và không còn giá trị nào chờ xác nhận (said_in_B0)
def profile_ready(state: ChatState) -> bool:
    if missing_fields(state.profile, REQUIRED_B3):
        return False
    return all(field.status != "said_in_B0" for field in state.profile.values())


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


# Khóa ghi cả giá trị: mã cố định của hợp đồng (nhiệm vụ, chế độ, loại con, dịch vụ, mục, cách xếp, tiếp nối) và tên
# trường, trung tâm (tên công khai, có trong danh sách của hệ thống; không phải thông tin cá nhân của học sinh)
LOGGED_VALUE_KEYS = (
    "nhiem_vu",
    "cac_nhiem_vu",
    "che_do",
    "loai_con",
    "dich_vu",
    "muc",
    "sap_xep",
    "tiep_noi",
    "truong",
    "cac_truong",
    "cac_trung_tam",
)


# Từng ý SLM đọc ra: loại, các giá trị ở trên, và tên các khóa còn lại. Giá trị còn lại là chữ học sinh viết
# (số điểm, thành phố, điều kiện lọc) nên chỉ ghi tên khóa
def log_item(y: Any) -> dict[str, Any]:
    values = {key: y.thuc_the[key] for key in LOGGED_VALUE_KEYS if key in y.thuc_the}
    return {"type": y.type, **values, "khoa": sorted(key for key in y.thuc_the if key not in values)}


# Mỗi lượt ghi một dòng JSON: tin nhắn, SLM đọc ra gì, nhánh nào chạy.
# Chỉ ghi mã cố định, tên trường/trung tâm và tên khóa của các thực thể còn lại (số điểm, thành phố... có thể là thông tin cá nhân)
def log_turn(
    state: ChatState,
    message: str,
    slm: SlmTinResult | None,
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
                "slm_types": slm.types() if slm else [],
                "slm_thuc_the": sorted({key for y in slm.cac_y for key in y.thuc_the}) if slm else [],
                "slm_cac_y": [log_item(y) for y in slm.cac_y] if slm else [],
                "slm_confidence": round(slm.confidence, 3) if slm else None,
                "ran": ran,
                "ms": round(ms),
            },
            ensure_ascii=False,
        ),
    )
