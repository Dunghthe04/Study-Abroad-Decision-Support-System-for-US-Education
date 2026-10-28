"""Bước 2.10: ChatFlow nối model chọn skill, luật code và các skill. Dùng model giả, skill giả nên không cần Ollama."""

import json
import logging
from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField, QuickReply, Widget
from app.services.chat_flow import (
    NEED_MAJOR,
    NEED_RESULTS,
    NOT_READY,
    QUESTIONS,
    ChatFlow,
    SkillOutput,
    ask,
    go_to,
    last_user_message,
)
from app.services.intake import CHAT_BUTTON, FORM_BUTTON, INTAKE_ASK, INTAKE_FORM, INTAKE_ID, INTAKE_MORE


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class FakePicker:
    """Model giả: luôn trả danh sách skill soạn sẵn, và ghi lại thông tin được hỏi."""

    def __init__(self, *skills: str, error: Exception | None = None) -> None:
        self._skills = list(skills)
        self._error = error
        self.calls: list[tuple] = []

    async def predict(self, step, last_question, message, missing):
        self.calls.append((step, last_question, message, missing))
        if self._error:
            raise self._error
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class RichHandler:
    """Skill giả: trả lời kèm nút gợi ý và/hoặc công cụ nhập."""

    def __init__(
        self, answer: str, quick_replies: list[QuickReply] | None = None, widget: Widget | None = None
    ):
        self._output = SkillOutput(answer, quick_replies or [], widget)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        return self._output


class RecordingHandler:
    """Skill giả: ghi lại thứ tự được chạy."""

    def __init__(self, name: str, ran: list[str]) -> None:
        self._name, self._ran = name, ran

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        self._ran.append(self._name)
        return SkillOutput(f"trả lời của {self._name}")


def request(message: str, **state_fields) -> ChatRequest:
    state = ChatState(**{"current_step": "B2", "last_question": "Bạn đã thi SAT chưa?", **state_fields})
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)


async def test_without_model_uses_current_step_skill_and_keeps_state():
    res = await ChatFlow().reply(request("em được 1420"))

    assert res.skills == ["skill_ho_so"]  # chưa có model: dùng skill của bước B2
    assert res.answer == NOT_READY  # chưa có skill nào đăng ký nên trả câu mẫu
    assert res.state.current_step == "B2"


async def test_two_skills_run_in_fixed_order_and_answers_are_joined():
    ran: list[str] = []
    handlers = {name: RecordingHandler(name, ran) for name in ("skill_ho_so", "skill_tra_loi")}
    picker = FakePicker("skill_tra_loi", "skill_ho_so")  # model gợi ý tra_loi trước

    res = await ChatFlow(picker, handlers).reply(request("SAT 1420, I-20 là gì ạ?"))

    assert ran == ["skill_ho_so", "skill_tra_loi"]  # nhưng chạy skill của bước trước
    assert res.answer == "trả lời của skill_ho_so\n\ntrả lời của skill_tra_loi"
    assert res.skills == ["skill_ho_so", "skill_tra_loi"]


class WritingHandler:
    """Skill giả: ghi một thông tin vào hồ sơ trong state, như skill_ho_so làm."""

    def __init__(self, key: str, value, status: str = "confirmed") -> None:
        self._key, self._field = key, ProfileField(value=value, status=status)

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        state.profile[self._key] = self._field
        return SkillOutput("đã ghi")


def request_with_draft(message: str, draft: dict | None, **state_fields) -> ChatRequest:
    """Như request(), nhưng kèm hồ sơ từ bản nháp (.NET gửi ở ChatRequest.profile)."""
    base = request(message, **state_fields)
    return base.model_copy(update={"profile": draft})


def confirmed(*keys: str) -> dict[str, ProfileField]:
    return {k: ProfileField(value="x", status="confirmed") for k in keys}


async def test_draft_profile_is_used_to_find_missing_fields():
    picker = FakePicker("skill_ho_so")

    await ChatFlow(picker).reply(request_with_draft("em được 1420", confirmed("gpa", "english")))

    *_, missing = picker.calls[0]
    assert "gpa" not in missing and "english" not in missing  # đã có trong nháp, không hỏi lại
    assert "budget" in missing


async def test_without_draft_the_profile_in_state_is_kept():
    picker = FakePicker("skill_ho_so")
    profile = {"gpa": ProfileField(value="x", status="confirmed")}

    res = await ChatFlow(picker).reply(request_with_draft("em được 1420", None, profile=profile))

    assert "gpa" in res.state.profile
    assert res.profile_changed == []


async def test_empty_draft_clears_what_the_form_removed():
    picker = FakePicker("skill_ho_so")
    profile = {"gpa": ProfileField(value="x", status="confirmed")}

    res = await ChatFlow(picker).reply(request_with_draft("em được 1420", {}, profile=profile))

    assert "gpa" not in res.state.profile  # học sinh xóa GPA ở form
    assert "gpa" in picker.calls[0][3]  # nên GPA lại thuộc nhóm còn thiếu


async def test_said_in_b0_is_not_promoted_when_the_draft_has_the_same_value():
    picker = FakePicker("skill_ho_so")
    state_profile = {"gpa": ProfileField(value="x", status="said_in_B0")}

    res = await ChatFlow(picker).reply(request_with_draft("ok", confirmed("gpa"), profile=state_profile))

    assert res.state.profile["gpa"].status == "said_in_B0"  # vẫn chờ học sinh xác nhận
    assert "gpa" in picker.calls[0][3]


async def test_new_chat_preloads_the_profile_from_the_form():
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="chào")], profile=confirmed("gpa", "english")
    )

    res = await ChatFlow().reply(req)

    assert res.answer == QUESTIONS["B0.1"]
    assert set(res.state.profile) == {"gpa", "english"}
    assert set(res.profile) == {"gpa", "english"}
    assert res.profile_changed == []  # nạp sẵn không phải do chat sửa


async def test_a_field_written_by_a_skill_is_reported_as_changed():
    handlers = {"skill_ho_so": WritingHandler("english", {"type": "ielts", "total": 7.0})}

    res = await ChatFlow(FakePicker("skill_ho_so"), handlers).reply(
        request_with_draft("IELTS em 7.0", confirmed("gpa"))
    )

    assert res.profile_changed == ["english"]  # gpa lấy từ nháp nên không tính là chat đổi
    assert res.profile["english"].value["total"] == 7.0


async def test_changes_to_chat_only_keys_are_not_reported():
    handlers = {"skill_ho_so": WritingHandler("funding_source", "bố mẹ")}

    res = await ChatFlow(FakePicker("skill_ho_so"), handlers).reply(
        request_with_draft("bố mẹ chi trả", confirmed("gpa"))
    )

    assert res.profile_changed == []  # form không có chỗ cho khóa này nên không cần nạp lại
    assert "funding_source" in res.profile  # nhưng chat vẫn nhớ


async def test_nothing_changed_when_no_skill_wrote_anything():
    handlers = {"skill_tra_loi": RichHandler("I-20 là giấy ...")}

    res = await ChatFlow(FakePicker("skill_tra_loi"), handlers).reply(
        request_with_draft("I-20 là gì ạ?", confirmed("gpa"))
    )

    assert res.profile_changed == []


async def test_response_carries_quick_replies_and_widget_from_the_skill():
    buttons = [QuickReply(label="Trả lời trong chat", value="Trả lời trong chat")]
    widget = Widget(type="major", data={"options": ["Computer Science"]})
    handlers = {"skill_ho_so": RichHandler("hỏi ngành", buttons, widget)}

    res = await ChatFlow(FakePicker("skill_ho_so"), handlers).reply(request("em chưa rõ"))

    assert res.quick_replies == buttons
    assert res.widget == widget


async def test_response_has_no_buttons_or_widget_by_default():
    handlers = {"skill_ho_so": RichHandler("trả lời thường")}

    res = await ChatFlow(FakePicker("skill_ho_so"), handlers).reply(request("em được 1420"))

    assert res.quick_replies == []
    assert res.widget is None
    assert res.profile == {} and res.profile_changed == []  # điền ở việc 1.5


async def test_when_two_skills_give_buttons_the_later_one_wins_but_earlier_is_kept_if_later_has_none():
    first = [QuickReply(label="A", value="a")]
    second = [QuickReply(label="B", value="b")]
    both = {"skill_ho_so": RichHandler("1", first), "skill_tra_loi": RichHandler("2", second)}
    only_first = {"skill_ho_so": RichHandler("1", first), "skill_tra_loi": RichHandler("2")}
    picker = ("skill_tra_loi", "skill_ho_so")

    later_wins = await ChatFlow(FakePicker(*picker), both).reply(request("SAT 1420, I-20 là gì ạ?"))
    kept = await ChatFlow(FakePicker(*picker), only_first).reply(request("SAT 1420, I-20 là gì ạ?"))

    assert later_wins.quick_replies == second  # skill_ho_so chạy trước, skill_tra_loi chạy sau và thắng
    assert kept.quick_replies == first  # skill chạy sau không đưa nút thì giữ nút của skill trước


async def test_picker_receives_step_question_message_and_missing_fields():
    picker = FakePicker("skill_ho_so")

    await ChatFlow(picker).reply(request("em được 1420", profile={"gpa": ProfileField(status="confirmed")}))

    step, question, message, missing = picker.calls[0]
    assert (step, question, message) == ("B2", "Bạn đã thi SAT chưa?", "em được 1420")
    assert "gpa" not in missing and "english" in missing  # chỉ gửi những thông tin còn thiếu


async def test_explain_is_blocked_until_the_state_has_results():
    ran_blocked: list[str] = []
    ran_allowed: list[str] = []

    blocked = await ChatFlow(
        FakePicker("skill_explain"), {"skill_explain": RecordingHandler("skill_explain", ran_blocked)}
    ).reply(request("vì sao trường A Vừa sức", current_step="B4"))
    allowed = await ChatFlow(
        FakePicker("skill_explain"), {"skill_explain": RecordingHandler("skill_explain", ran_allowed)}
    ).reply(request("vì sao trường A Vừa sức", current_step="B4", has_results=True))

    assert blocked.skills == [] and ran_blocked == []  # chưa có danh sách trường thì chưa giải thích được
    assert blocked.answer == NEED_RESULTS
    assert allowed.skills == ["skill_explain"] and ran_allowed == ["skill_explain"]
    assert allowed.state.has_results is True  # cờ được giữ trong state để lượt sau còn dùng


async def test_assessment_blocked_with_no_profile_offers_chat_or_form():
    picker = FakePicker("skill_danh_gia")

    res = await ChatFlow(picker).reply(request("đánh giá hồ sơ em", current_step="B3", last_question=""))

    assert res.skills == []  # chưa đủ hồ sơ nên chưa đánh giá
    assert res.answer == INTAKE_ASK
    assert res.quick_replies == [CHAT_BUTTON, FORM_BUTTON]
    assert res.state.question_id == INTAKE_ID  # lượt sau biết học sinh đang chọn cách điền
    assert res.state.last_question == ""  # câu mời nằm ngoài các câu model đã học


async def test_choosing_chat_asks_the_first_missing_question():
    picker = FakePicker("skill_ho_so")

    res = await ChatFlow(picker).reply(
        request(CHAT_BUTTON.value, current_step="B3", last_question="", question_id=INTAKE_ID)
    )

    assert res.answer == QUESTIONS["B1.2"]  # bậc học là thông tin đầu tiên còn thiếu
    assert res.state.intake_mode == "chat"
    assert res.state.current_step == "B1"
    assert res.state.question_id == "B1.2" and res.state.last_question == QUESTIONS["B1.2"]
    assert picker.calls == []  # lựa chọn nhận bằng chữ trên nút, không nhờ model


async def test_choosing_form_opens_the_form_widget():
    res = await ChatFlow(FakePicker("skill_ho_so")).reply(
        request(FORM_BUTTON.value, current_step="B3", last_question="", question_id=INTAKE_ID)
    )

    assert res.answer == INTAKE_FORM
    assert res.widget == Widget(type="form")
    assert res.state.intake_mode == "form"
    assert res.state.question_id is None


async def test_other_message_while_choosing_goes_through_the_normal_flow():
    ran: list[str] = []
    handlers = {"skill_tra_loi": RecordingHandler("skill_tra_loi", ran)}

    res = await ChatFlow(FakePicker("skill_tra_loi"), handlers).reply(
        request("I-20 là gì ạ?", current_step="B3", last_question="", question_id=INTAKE_ID)
    )

    assert ran == ["skill_tra_loi"]
    assert res.state.question_id is None  # không còn chờ lựa chọn


async def test_partial_profile_gets_the_next_missing_question_in_chat():
    profile = {"target_level": ProfileField(value="x", status="confirmed")}

    res = await ChatFlow(FakePicker("skill_danh_gia")).reply(
        request("đánh giá hồ sơ em", current_step="B3", last_question="", profile=profile)
    )

    assert res.answer == INTAKE_MORE + QUESTIONS["D2"]  # đã có bậc học, hỏi tiếp GPA
    assert res.state.intake_mode == "chat"
    assert res.state.current_step == "B2" and res.state.question_id == "D2"
    assert res.quick_replies == []  # đã có hồ sơ nên không mời chọn lại


async def test_form_chosen_but_nothing_filled_reminds_the_form():
    res = await ChatFlow(FakePicker("skill_danh_gia")).reply(
        request("đánh giá hồ sơ em", current_step="B3", last_question="", intake_mode="form")
    )

    assert res.answer == INTAKE_FORM
    assert res.widget == Widget(type="form")


async def test_no_intake_when_nothing_is_blocked():
    ran: list[str] = []
    handlers = {"skill_tra_loi": RecordingHandler("skill_tra_loi", ran)}

    res = await ChatFlow(FakePicker("skill_tra_loi"), handlers).reply(request("I-20 là gì ạ?"))

    assert res.answer == "trả lời của skill_tra_loi"  # hỏi kiến thức thì không kéo sang hỏi hồ sơ
    assert res.quick_replies == [] and res.state.intake_mode is None


async def test_roadmap_shows_the_major_widget_when_only_major_is_missing():
    profile = {
        k: ProfileField(status="confirmed")
        for k in ("target_level", "gpa", "english", "budget", "scholarship")
    }
    profile["major"] = ProfileField(status="unknown")  # "chưa rõ" ngành

    res = await ChatFlow(FakePicker("skill_lo_trinh")).reply(
        request("gợi ý trường cho em", current_step="B4", profile=profile)
    )

    assert res.skills == []
    assert res.answer == NEED_MAJOR
    assert res.widget == Widget(type="major")  # frontend hiện ô chọn ngành
    assert res.state.current_step == "B1" and res.state.return_to == "B4"  # chọn xong thì quay lại B4
    assert res.state.question_id == "B1.3" and res.state.last_question == QUESTIONS["B1.3"]


async def test_the_chosen_major_is_read_as_the_answer_to_the_major_question():
    profile = {
        k: ProfileField(status="confirmed")
        for k in ("target_level", "gpa", "english", "budget", "scholarship")
    }
    first = await ChatFlow(FakePicker("skill_lo_trinh")).reply(
        request("gợi ý trường cho em", current_step="B4", profile=profile)
    )
    picker = FakePicker("skill_bac_hoc")

    # Học sinh chọn ngành trong ô, frontend gửi tên ngành như một tin nhắn
    await ChatFlow(picker).reply(
        ChatRequest(messages=[ChatMessage(role="user", content="Computer Science")], state=first.state)
    )

    step, question, message, _ = picker.calls[0]
    assert (step, question, message) == (
        "B1",
        QUESTIONS["B1.3"],
        "Computer Science",
    )  # đúng ngữ cảnh model đã học


async def test_no_major_widget_when_the_major_is_known_or_other_reason_blocks():
    full = {
        k: ProfileField(status="confirmed")
        for k in ("target_level", "gpa", "english", "budget", "major", "scholarship")
    }

    known = await ChatFlow(FakePicker("skill_lo_trinh")).reply(
        request("gợi ý trường cho em", current_step="B4", profile=full)
    )
    other = await ChatFlow(FakePicker("skill_explain")).reply(
        request("vì sao trường A Vừa sức", current_step="B4", profile=full)
    )

    assert known.widget is None  # đủ ngành thì skill_lo_trinh chạy (chưa có handler nên trả câu mẫu)
    assert (
        other.widget is None and other.answer == NEED_RESULTS
    )  # chặn vì chưa có danh sách, không phải thiếu ngành


async def test_model_error_falls_back_instead_of_crashing():
    res = await ChatFlow(FakePicker(error=RuntimeError("Ollama tắt"))).reply(request("em được 1420"))

    assert res.skills == ["skill_ho_so"]


async def test_off_topic_repeats_the_current_question():
    res = await ChatFlow(FakePicker("ngoai_le")).reply(request("bạn thích ăn gì"))

    assert "Bạn đã thi SAT chưa?" in res.answer


async def test_goodbye_ends_the_conversation():
    res = await ChatFlow(FakePicker("ket_thuc")).reply(request("em cảm ơn ạ"))

    assert res.state.current_step == "END"


def test_ask_stores_question_text_id_and_counts():
    state = ChatState()

    assert ask(state, "B0.1") == QUESTIONS["B0.1"]
    ask(state, "B0.1")

    assert state.last_question == QUESTIONS["B0.1"]
    assert state.question_id == "B0.1"  # lượt sau biết học sinh đang trả lời câu nào
    assert state.asked == {"B0.1": 2}


def test_go_to_changes_step_and_clears_pending_question():
    state = ChatState(current_step="B1")
    ask(state, "B0.1")

    go_to(state, "B2")

    assert state.current_step == "B2"
    assert state.last_question == ""  # câu của bước cũ không còn chờ trả lời
    assert state.question_id is None
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
            ChatMessage(role="assistant", content="Bạn học lớp mấy?"),
        ]
    )

    assert last_user_message(req) == "tìm trường"


def last_log_record(caplog) -> dict:
    line = next(r.getMessage() for r in reversed(caplog.records) if r.getMessage().startswith("chat_turn "))
    return json.loads(line.removeprefix("chat_turn "))


async def test_log_line_carries_the_session_id(caplog):
    req = request("em được 1420").model_copy(update={"session_id": "3f2a-session"})

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(req)

    assert last_log_record(caplog)["session_id"] == "3f2a-session"


async def test_log_session_id_is_empty_when_not_given(caplog):
    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(request("em được 1420"))

    assert last_log_record(caplog)["session_id"] is None


async def test_log_masks_personal_info_but_keeps_the_rest_of_the_message(caplog):
    message = "email em là hoa@gmail.com, số 0912 345 678, em được SAT 1420"

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(request(message))

    logged = last_log_record(caplog)["message"]
    assert "hoa@gmail.com" not in logged and "0912" not in logged
    assert "[EMAIL]" in logged
    assert "SAT 1420" in logged  # điểm không bị che vì còn cần để gắn nhãn dữ liệu


async def test_the_reply_to_the_student_is_not_masked_only_the_log(caplog):
    handlers = {"skill_ho_so": RichHandler("ok")}

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        res = await ChatFlow(FakePicker("skill_ho_so"), handlers).reply(request("sđt em 0912 345 678"))

    assert res.answer == "ok"
    assert "0912" not in last_log_record(caplog)["message"]


async def test_choice_turn_is_logged_with_the_session_id_too(caplog):
    req = request(CHAT_BUTTON.value, current_step="B3", last_question="", question_id=INTAKE_ID).model_copy(
        update={"session_id": "abc"}
    )

    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(req)

    assert last_log_record(caplog)["session_id"] == "abc"


async def test_each_turn_writes_one_json_log_line(caplog):
    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(request("em được 1420"))

    line = next(r.getMessage() for r in caplog.records if r.getMessage().startswith("chat_turn "))
    record = json.loads(line.removeprefix("chat_turn "))
    assert record["message"] == "em được 1420"
    assert record["model_skills"] == ["skill_ho_so"]
    assert record["ran"] == ["skill_ho_so"]
    assert record["source"] == "model"
