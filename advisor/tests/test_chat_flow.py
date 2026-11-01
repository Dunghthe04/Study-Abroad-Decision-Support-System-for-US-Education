"""ChatFlow theo kiến trúc mới: SLM đọc tin, chạy nhánh theo loại câu hỏi, ghép câu trả lời, nhắc lại câu hồ sơ đang chờ.
Dùng SLM giả và nhánh giả nên không cần Ollama.
"""

import json
import logging

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField, QuickReply, Widget
from app.schemas.slm import INTENTS, SlmResult
from app.services.chat_flow import (
    BACK_TO_QUESTION,
    NOT_READY,
    QUESTIONS,
    ChatFlow,
    SkillOutput,
    ask,
    branches_to_run,
    combine,
    go_to,
    last_user_message,
)
from app.services.intake import CHAT_BUTTON, CONTINUE_CHAT, FORM_BUTTON, INTAKE_FORM, INTAKE_ID


class CountingSlm:
    """SLM giả: luôn đọc ra cùng một kết quả và đếm số lần được gọi."""

    def __init__(self, result: SlmResult) -> None:
        self._result = result
        self.calls: list[str] = []

    async def classify(self, state, message):
        self.calls.append(message)
        return self._result


class FakeBranch:
    """Nhánh giả: ghi lại đã chạy và kết quả SLM nó nhận; có thể ghi hồ sơ, hỏi câu mới, trả nút / công cụ."""

    def __init__(
        self,
        name: str,
        ran: list[str],
        output: SkillOutput | None = None,
        writes: dict | None = None,
        asks: str | None = None,
    ):
        self.name, self.ran, self.output, self.writes, self.asks = name, ran, output, writes or {}, asks
        self.seen: list[SlmResult] = []

    async def run(self, state, message, slm):
        self.ran.append(self.name)
        self.seen.append(slm)
        for key, value in self.writes.items():
            state.profile[key] = ProfileField(value=value, status="confirmed")
        if self.asks:
            ask(state, self.asks)
        return self.output if self.output is not None else SkillOutput(f"trả lời của {self.name}")


def make_flow(slm: SlmResult, ran: list[str] | None = None, **branches) -> tuple[ChatFlow, CountingSlm]:
    ran = [] if ran is None else ran
    all_branches = {intent: FakeBranch(intent, ran) for intent in INTENTS} | branches
    counting = CountingSlm(slm)
    return ChatFlow(counting, all_branches), counting


def request(message: str, pending: str | None = None, **state_fields) -> ChatRequest:
    state = ChatState(**{"current_step": "B2", **state_fields})
    if pending:
        ask(state, pending)
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)


def confirmed(*keys: str) -> dict[str, ProfileField]:
    return {k: ProfileField(value="x", status="confirmed") for k in keys}


# ---- chọn nhánh ----
def test_branches_are_the_main_label_then_the_secondary_then_the_profile_branch_last():
    assert branches_to_run(SlmResult(type="kien_thuc")) == ["kien_thuc"]
    assert branches_to_run(SlmResult(type="kien_thuc", nhan_phu="trung_tam")) == ["kien_thuc", "trung_tam"]
    assert branches_to_run(SlmResult(type="kien_thuc", thuc_the={"gpa": "8.5 ạ"})) == [
        "kien_thuc",
        "mac_dinh",
    ]
    assert branches_to_run(SlmResult(type="mac_dinh", thuc_the={"gpa": "8.5"})) == ["mac_dinh"]


async def test_one_label_runs_its_branch_and_returns_its_answer():
    ran: list[str] = []
    flow, _ = make_flow(SlmResult(type="kien_thuc"), ran)

    res = await flow.reply(request("I-20 là gì?"))

    assert ran == ["kien_thuc"] and res.skills == ["kien_thuc"]
    assert res.answer == "trả lời của kien_thuc"


async def test_a_message_with_two_questions_runs_both_branches_in_order_and_joins_the_answers():
    ran: list[str] = []
    flow, _ = make_flow(SlmResult(type="kien_thuc", nhan_phu="trung_tam"), ran)

    res = await flow.reply(request("phỏng vấn visa hỏi gì, có chỗ nào luyện không"))

    assert ran == ["kien_thuc", "trung_tam"]
    assert res.answer == "trả lời của kien_thuc\n\ntrả lời của trung_tam"


async def test_profile_values_in_a_question_also_run_the_profile_branch_after_the_answer():
    ran: list[str] = []
    flow, _ = make_flow(SlmResult(type="kien_thuc", thuc_the={"gpa": "8.5 ạ"}), ran)

    res = await flow.reply(request("8.5 ạ, mà I-20 là gì?", pending="D2"))

    assert ran == ["kien_thuc", "mac_dinh"]  # trả lời trước, ghi GPA và hỏi câu kế tiếp sau
    assert BACK_TO_QUESTION not in res.answer  # nhánh hồ sơ đã chạy nên không nhắc lại câu cũ


async def test_the_branch_gets_the_slm_result_with_its_entities():
    slm = SlmResult(type="trung_tam", thuc_the={"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "HN"})
    branch = FakeBranch("trung_tam", [])
    flow, _ = make_flow(slm, trung_tam=branch)

    await flow.reply(request("ai sửa bài luận ở HN"))

    assert branch.seen == [slm]


async def test_pressing_a_button_of_the_pending_question_goes_to_the_profile_branch_without_the_slm():
    ran: list[str] = []
    flow, counting = make_flow(SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "tam_biet"}), ran)

    await flow.reply(request("Bỏ qua", pending="F2"))

    assert counting.calls == [] and ran == ["mac_dinh"]


# ---- nhắc lại câu hồ sơ đang chờ ----
async def test_a_question_asked_in_the_middle_of_the_profile_reminds_the_pending_question():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request("I-20 là gì?", pending="D2"))

    assert res.answer == "trả lời của kien_thuc\n\n" + BACK_TO_QUESTION + QUESTIONS["D2"]
    assert (res.state.question_id, res.state.asked) == ("D2", {"D2": 1})  # vẫn chờ, không tính hỏi thêm lần


async def test_off_topic_and_scenario_messages_also_remind_the_pending_question():
    for slm in (SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "ngoai_le"}),
                SlmResult(type="kich_ban", thuc_the={"che_do": "goal_seeking"})):  # fmt: skip
        flow, _ = make_flow(slm)
        res = await flow.reply(request("x", pending="D2"))
        assert res.answer.endswith(BACK_TO_QUESTION + QUESTIONS["D2"])


async def test_no_reminder_when_nothing_is_pending():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request("I-20 là gì?"))

    assert BACK_TO_QUESTION not in res.answer


async def test_no_reminder_when_the_profile_branch_answered_the_pending_question():
    flow, _ = make_flow(SlmResult(type="mac_dinh", thuc_the={"gpa": "8.5"}))

    res = await flow.reply(request("8.5", pending="D2"))

    assert BACK_TO_QUESTION not in res.answer


async def test_no_reminder_when_a_branch_already_moved_on_to_a_new_question():
    ran: list[str] = []
    flow, _ = make_flow(SlmResult(type="ho_so"), ran, ho_so=FakeBranch("ho_so", ran, asks="X2"))

    res = await flow.reply(request("đánh giá hồ sơ em", pending="D2"))

    assert BACK_TO_QUESTION not in res.answer and res.state.question_id == "X2"


async def test_no_reminder_for_the_opening_questions():
    for opening in ("B0.1", "B0.2"):
        flow, _ = make_flow(SlmResult(type="kien_thuc"))
        res = await flow.reply(request("I-20 là gì?", pending=opening))
        assert BACK_TO_QUESTION not in res.answer


# ---- câu trả lời mặc định ----
async def test_a_first_turn_that_nobody_answered_greets_like_before():
    flow, _ = make_flow(SlmResult(type="kien_thuc"), kien_thuc=FakeBranch("kien_thuc", [], SkillOutput("")))

    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="chào")]))

    assert res.answer == QUESTIONS["B0.1"]
    assert res.state.asked == {"B0.1": 1}


async def test_a_later_turn_that_nobody_answered_says_not_ready():
    flow, _ = make_flow(SlmResult(type="kich_ban", thuc_the={"che_do": "goal_seeking"}),
                        kich_ban=FakeBranch("kich_ban", [], SkillOutput("")))  # fmt: skip

    res = await flow.reply(request("cần SAT bao nhiêu để vào MIT"))

    assert res.answer == NOT_READY


# ---- ghép câu trả lời: nút, công cụ, action ----
def test_combine_joins_answers_and_the_later_part_wins_buttons_widget_and_action():
    a = SkillOutput("1", [QuickReply(label="A", value="a")], Widget(type="major"), action="recommend")
    b = SkillOutput("2", [QuickReply(label="B", value="b")])

    joined = combine(a, b)

    assert joined.answer == "1\n\n2"
    assert joined.quick_replies == b.quick_replies  # phần sau có nút thì thắng
    # phần sau không có công cụ hay action thì giữ của phần trước
    assert joined.widget == a.widget and joined.action == "recommend"
    assert combine(SkillOutput(""), b).answer == "2"


async def test_response_carries_buttons_widget_and_action_of_the_branch():
    buttons = [QuickReply(label="Trả lời trong chat", value="Trả lời trong chat")]
    output = SkillOutput("hỏi ngành", buttons, Widget(type="major"), action="recommend", action_data={"x": 1})
    flow, _ = make_flow(SlmResult(type="ho_so"), ho_so=FakeBranch("ho_so", [], output))

    res = await flow.reply(request("gợi ý trường cho em"))

    assert res.quick_replies == buttons and res.widget == Widget(type="major")
    assert (res.action, res.action_data) == ("recommend", {"x": 1})


async def test_response_has_no_buttons_widget_or_action_by_default():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request("I-20 là gì?"))

    assert res.quick_replies == [] and res.widget is None and res.action is None


# ---- hồ sơ trong bản nháp (.NET gửi) và các khóa đã đổi ----
def request_with_draft(message: str, draft: dict | None, **state_fields) -> ChatRequest:
    return request(message, **state_fields).model_copy(update={"profile": draft})


async def test_without_draft_the_profile_in_state_is_kept():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request_with_draft("I-20 là gì?", None, profile=confirmed("gpa")))

    assert "gpa" in res.state.profile and res.profile_changed == []


async def test_empty_draft_clears_what_the_form_removed():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request_with_draft("I-20 là gì?", {}, profile=confirmed("gpa")))

    assert "gpa" not in res.state.profile


async def test_said_in_b0_is_not_promoted_when_the_draft_has_the_same_value():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))
    state_profile = {"gpa": ProfileField(value="x", status="said_in_B0")}

    res = await flow.reply(request_with_draft("ok", confirmed("gpa"), profile=state_profile))

    assert res.state.profile["gpa"].status == "said_in_B0"  # vẫn chờ học sinh xác nhận


async def test_new_chat_preloads_the_profile_from_the_form():
    flow, _ = make_flow(SlmResult(type="kien_thuc"), kien_thuc=FakeBranch("kien_thuc", [], SkillOutput("")))
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="chào")], profile=confirmed("gpa", "english")
    )

    res = await flow.reply(req)

    assert set(res.profile) == {"gpa", "english"}
    assert res.profile_changed == []  # nạp sẵn không phải do chat sửa


async def test_a_field_written_by_a_branch_is_reported_as_changed():
    ran: list[str] = []
    branch = FakeBranch("mac_dinh", ran, writes={"english": {"type": "ielts", "total": 7.0}})
    flow, _ = make_flow(
        SlmResult(type="mac_dinh", thuc_the={"english": "IELTS em 7.0"}), ran, mac_dinh=branch
    )

    res = await flow.reply(request_with_draft("IELTS em 7.0", confirmed("gpa")))

    assert res.profile_changed == ["english"]  # gpa lấy từ nháp nên không tính là chat đổi


async def test_changes_to_chat_only_keys_are_not_reported():
    branch = FakeBranch("mac_dinh", [], writes={"relatives_state": "CA"})
    flow, _ = make_flow(
        SlmResult(type="mac_dinh", thuc_the={"relatives_state": "chú em ở CA"}), mac_dinh=branch
    )

    res = await flow.reply(request_with_draft("chú em ở CA", confirmed("gpa")))

    assert res.profile_changed == [] and "relatives_state" in res.profile


# ---- chọn cách điền hồ sơ (chat hay form): nhận bằng chữ trên nút, không cần SLM ----
async def test_choosing_chat_asks_the_first_missing_question_without_the_slm():
    flow, counting = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request(CHAT_BUTTON.value, current_step="B3", question_id=INTAKE_ID))

    assert res.answer == QUESTIONS["B1.1"]
    assert (res.state.intake_mode, res.state.current_step, res.state.question_id) == ("chat", "B1", "B1.1")
    assert counting.calls == []


async def test_choosing_form_opens_the_form_widget():
    flow, _ = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request(FORM_BUTTON.value, current_step="B3", question_id=INTAKE_ID))

    assert res.answer == INTAKE_FORM and res.widget == Widget(type="form")
    assert res.state.intake_mode == "form" and res.state.question_id is None


async def test_continue_in_chat_button_from_the_form_asks_the_missing_items():
    flow, counting = make_flow(SlmResult(type="kien_thuc"))

    res = await flow.reply(request(CONTINUE_CHAT, current_step="B3"))

    assert res.answer == QUESTIONS["B1.1"] and counting.calls == []


async def test_another_message_while_choosing_goes_through_the_slm():
    ran: list[str] = []
    flow, _ = make_flow(SlmResult(type="kien_thuc"), ran)

    res = await flow.reply(request("I-20 là gì ạ?", current_step="B3", question_id=INTAKE_ID))

    assert ran == ["kien_thuc"] and res.state.question_id is None  # không còn chờ lựa chọn


# ---- hàm dùng chung ----
def test_ask_stores_question_text_id_and_counts():
    state = ChatState()

    assert ask(state, "B0.1") == QUESTIONS["B0.1"]
    ask(state, "B0.1")

    assert (state.last_question, state.question_id, state.asked) == (QUESTIONS["B0.1"], "B0.1", {"B0.1": 2})


def test_go_to_changes_step_and_clears_pending_question():
    state = ChatState(current_step="B1")
    ask(state, "B0.1")

    go_to(state, "B2")

    assert (state.current_step, state.last_question, state.question_id) == ("B2", "", None)
    assert state.asked == {"B0.1": 1}  # lịch sử đã hỏi vẫn giữ


def test_old_state_without_question_id_still_loads():
    state = ChatState.model_validate({"currentStep": "B2", "lastQuestion": "Bạn đã thi SAT chưa?"})

    assert state.question_id is None


def test_last_user_message_ignores_assistant_messages():
    req = ChatRequest(
        messages=[
            ChatMessage(role="user", content="chào"),
            ChatMessage(role="assistant", content="Bạn cần gì?"),
            ChatMessage(role="user", content="tìm trường"),
        ]
    )

    assert last_user_message(req) == "tìm trường"


# ---- log mỗi lượt ----
def last_log_record(caplog) -> dict:
    line = next(r.getMessage() for r in reversed(caplog.records) if r.getMessage().startswith("chat_turn "))
    return json.loads(line.removeprefix("chat_turn "))


async def test_each_turn_logs_what_the_slm_read_and_which_branches_ran(caplog):
    slm = SlmResult(type="kien_thuc", nhan_phu="trung_tam", thuc_the={"thanh_pho": "HN"}, confidence=0.93)
    flow, _ = make_flow(slm)

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await flow.reply(request("visa hỏi gì, ở HN ai luyện").model_copy(update={"session_id": "abc"}))

    record = last_log_record(caplog)
    assert record["session_id"] == "abc"
    assert (record["slm_type"], record["slm_nhan_phu"], record["slm_confidence"]) == (
        "kien_thuc",
        "trung_tam",
        0.93,
    )
    assert record["slm_thuc_the"] == ["thanh_pho"]  # chỉ tên khóa, không ghi chữ học sinh viết
    assert record["ran"] == ["kien_thuc", "trung_tam"]


async def test_log_masks_personal_info_but_the_reply_is_not_masked(caplog):
    flow, _ = make_flow(SlmResult(type="kien_thuc"), kien_thuc=FakeBranch("kien_thuc", [], SkillOutput("ok")))

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        res = await flow.reply(request("email em là hoa@gmail.com, số 0912 345 678, SAT 1420"))

    logged = last_log_record(caplog)["message"]
    assert "hoa@gmail.com" not in logged and "0912" not in logged and "SAT 1420" in logged
    assert res.answer == "ok"


async def test_choice_turn_is_logged_with_the_session_id_too(caplog):
    flow, _ = make_flow(SlmResult(type="kien_thuc"))
    req = request(CHAT_BUTTON.value, current_step="B3", question_id=INTAKE_ID).model_copy(
        update={"session_id": "abc"}
    )

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await flow.reply(req)

    record = last_log_record(caplog)
    assert record["session_id"] == "abc" and record["slm_type"] is None
