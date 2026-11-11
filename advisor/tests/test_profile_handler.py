import pytest

from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import QUESTIONS, ask
from app.services.intake import INTAKE_ID
from app.services.skill_rules import REQUIRED_B3
from app.services.skills import skill_ho_so
from app.services.skills.profile_handler import ChatProfileHandler
from app.services.skills.skill_bac_hoc import BacHocHandler
from app.services.skills.skill_ho_so import HoSoHandler
from tests.fakes import FakeLLM, tin


def make_handler(llm) -> ChatProfileHandler:
    return ChatProfileHandler(
        {"skill_bac_hoc": BacHocHandler(llm, "test"), "skill_ho_so": HoSoHandler(llm, "test")}
    )


handler = make_handler(FakeLLM())
MAC_DINH = tin("collect_profile")


def test_pending_question_none_when_no_question():
    assert handler.pending_question(ChatState()) is None


def test_pending_question_skips_opening_question():
    state = ChatState()
    ask(state, "B0.1")
    assert handler.pending_question(state) is None


def test_pending_question_returns_asked_text():
    state = ChatState()
    text = ask(state, "B1.1")
    assert handler.pending_question(state) == text


def test_version_ignores_field_order():
    first = ChatState(
        profile={
            "gpa": ProfileField(value=8.5, status="confirmed"),
            "major": ProfileField(value="CS", status="confirmed"),
        }
    )
    second = ChatState(
        profile={
            "major": ProfileField(value="CS", status="confirmed"),
            "gpa": ProfileField(value=8.5, status="confirmed"),
        }
    )
    assert handler.version(first) == handler.version(second)


def test_version_changes_with_value():
    first = ChatState(profile={"gpa": ProfileField(value=8.5, status="confirmed")})
    second = ChatState(profile={"gpa": ProfileField(value=9.0, status="confirmed")})
    assert handler.version(first) != handler.version(second)


def test_version_changes_with_status():
    first = ChatState(profile={"gpa": ProfileField(value=8.5, status="said_in_B0")})
    second = ChatState(profile={"gpa": ProfileField(value=8.5, status="confirmed")})
    assert handler.version(first) != handler.version(second)


def test_ready_when_complete():
    state = ChatState(profile={"gpa": ProfileField(value=8.5, status="confirmed")})
    assert handler.ready(state, ["gpa"])


def test_not_ready_when_field_missing():
    assert not handler.ready(ChatState(), ["gpa"])


def test_not_ready_while_question_pending():
    state = ChatState(profile={"gpa": ProfileField(value=8.5, status="confirmed")})
    ask(state, "B1.1")
    assert not handler.ready(state, ["gpa"])


def test_not_ready_with_unconfirmed_field():
    state = ChatState(
        profile={
            "gpa": ProfileField(value=8.5, status="confirmed"),
            "major": ProfileField(value="CS", status="said_in_B0"),
        }
    )
    assert not handler.ready(state, ["gpa"])


async def test_collect_bac_hoc_question_saves_and_asks_next():
    llm = FakeLLM(
        {
            "current_level": "high_school",
            "current_grade": "Lớp 12",
            "target_level": None,
            "major": None,
            "intake": None,
        }
    )
    state = ChatState(current_step="B1")
    ask(state, "B1.1")
    await make_handler(llm).collect(state, "Em lớp 12", MAC_DINH)
    assert state.profile["current_level"].value == "high_school"
    assert state.question_id == "B1.2"


async def test_collect_ho_so_question_saves_answer():
    llm = FakeLLM({"values": {"gpa": {"value": 8.5, "scale": 10}}, "none_fields": []})
    state = ChatState(current_step="B2")
    ask(state, "D2")
    await make_handler(llm).collect(state, "GPA em 8.5 hệ 10", MAC_DINH)
    assert state.profile["gpa"].status == "confirmed"
    assert state.question_id != "D2"


async def test_collect_confirm_review_makes_profile_ready():
    profile = {name: ProfileField(value="x", status="said_in_B0") for name in REQUIRED_B3}
    state = ChatState(current_step="B2", profile=profile)
    ask(state, "X1")
    assert not handler.ready(state, REQUIRED_B3)
    await handler.collect(state, "Đúng rồi", MAC_DINH)
    assert state.current_step == "B3"
    assert state.question_id is None
    assert handler.ready(state, REQUIRED_B3)


def full_profile() -> dict[str, ProfileField]:
    names = ["current_level", "target_level", "major", "intake", "gpa", "english", "budget"]
    profile = {name: ProfileField(value="x", status="confirmed") for name in names}
    profile["activities"] = ProfileField(value=[{"name": "a"}], status="confirmed")
    return profile


async def test_update_after_confirm_returns_to_review():
    llm = FakeLLM({"values": {"gpa": {"value": 9.0, "scale": 10}}, "none_fields": []})
    state = ChatState(
        current_step="B3",
        profile=full_profile(),
        intake_mode="chat",
        asked=dict.fromkeys(skill_ho_so.QUESTION_ORDER, 1),
    )
    slm = tin(("collect_profile", {"gpa": "9.0"}))
    await make_handler(llm).collect(state, "GPA em giờ 9.0 hệ 10", slm)
    assert state.profile["gpa"].value["value"] == 9.0
    assert state.profile["gpa"].status == "said_in_B0"
    assert state.profile["activities"].value == [{"name": "a"}]
    assert (state.current_step, state.question_id) == ("B2", "X1")
    assert not handler.ready(state, REQUIRED_B3)


async def test_said_value_of_other_group_saved_with_pending_question():
    llm = FakeLLM(
        {"current_level": None, "current_grade": None, "target_level": None, "major": "CS", "intake": None},
        {"values": {"gpa": {"value": 8.5, "scale": 10}}, "none_fields": []},
    )
    state = ChatState(current_step="B2", intake_mode="chat")
    ask(state, "D2")
    slm = tin(("collect_profile", {"gpa": "8.5", "major": "CS"}))
    await make_handler(llm).collect(state, "GPA 8.5 hệ 10, ngành CS", slm)
    assert state.profile["major"].status == "said_in_B0"
    assert state.profile["gpa"].status == "confirmed"


async def test_opening_message_saves_values_and_invites():
    llm = FakeLLM({"values": {"gpa": {"value": 8.5, "scale": 10}}, "none_fields": []})
    state = ChatState()
    ask(state, "B0.1")
    slm = tin(("collect_profile", {"gpa": "8.5"}))
    await make_handler(llm).collect(state, "GPA 8.5", slm)
    assert state.profile["gpa"].status == "said_in_B0"
    assert state.question_id == INTAKE_ID


async def test_opening_message_without_values_invites_without_calling_llm():
    llm = FakeLLM()
    state = ChatState()
    ask(state, "B0.1")
    await make_handler(llm).collect(state, "Chào bạn", MAC_DINH)
    assert state.question_id == INTAKE_ID
    assert llm.calls == []


async def test_nothing_new_keeps_ready_profile():
    llm = FakeLLM()
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    output = await make_handler(llm).collect(state, "ok", MAC_DINH)
    assert output.answer
    assert (state.current_step, state.question_id) == ("B3", None)
    assert handler.ready(state, REQUIRED_B3)
    assert llm.calls == []


async def test_edit_without_item_shows_edit_menu():
    llm = FakeLLM()
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    slm = tin(("collect_profile", {"tasks": ["edit"]}))
    output = await make_handler(llm).collect(state, "em muốn sửa hồ sơ", slm)
    assert output.answer == skill_ho_so.EDIT_MENU
    assert output.widget.type == "select"
    assert state.question_id == "X1"
    assert llm.calls == []


async def test_unreadable_value_keeps_ready_profile():
    llm = FakeLLM({"values": {}, "none_fields": []})
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    slm = tin(("collect_profile", {"gpa": "cao"}))
    await make_handler(llm).collect(state, "GPA em cao", slm)
    assert (state.current_step, state.question_id) == ("B3", None)
    assert handler.ready(state, REQUIRED_B3)


NO_HO_SO = {"values": {}, "none_fields": []}
NO_BAC_HOC = {
    "current_level": None,
    "current_grade": None,
    "target_level": None,
    "major": None,
    "intake": None,
}


# Sửa một mục bất kỳ mà chưa nói giá trị mới: hỏi lại đúng câu của mục đó (vài mục ở các nhóm khác nhau làm ví dụ)
@pytest.mark.parametrize(
    ("name", "said", "question_id", "llm_answer"),
    [
        ("gpa", "GPA", "D2", NO_HO_SO),
        ("english", "tiếng Anh", "E1", NO_HO_SO),
        ("budget", "ngân sách", "F1", NO_HO_SO),
        ("preferred_states", "bang", "N2", NO_HO_SO),
        ("awards", "giải thưởng", "D5", NO_HO_SO),
        ("major", "ngành", "B1.3", NO_BAC_HOC),
        ("intake", "kỳ nhập học", "B1.4", NO_BAC_HOC),
    ],
)
async def test_edit_item_without_value_asks_that_item_question(name, said, question_id, llm_answer):
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    slm = tin(("collect_profile", {"tasks": ["edit"], name: said}))
    output = await make_handler(FakeLLM(llm_answer)).collect(state, f"em muốn sửa {said}", slm)
    assert state.question_id == question_id
    assert output.answer == QUESTIONS[question_id]
    assert state.profile == full_profile()  # chưa đổi gì


# Đang ở bảng rà soát, bấm sửa một mục cũng hỏi đúng câu của mục đó
async def test_edit_item_at_review_asks_that_item_question():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat")
    ask(state, "X1")
    slm = tin(("collect_profile", {"tasks": ["edit"], "sat": "SAT"}))
    await make_handler(FakeLLM(NO_HO_SO)).collect(state, "sửa SAT", slm)
    assert state.question_id == "D3"


# Nói luôn giá trị mới thì ghi luôn, không hỏi lại câu của mục
async def test_edit_item_with_value_saves_and_shows_review():
    llm = FakeLLM({"values": {"gpa": {"value": 3.8, "scale": 4}}, "none_fields": []})
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat")
    ask(state, "X1")
    slm = tin(("collect_profile", {"tasks": ["edit"], "gpa": "3.8"}))
    output = await make_handler(llm).collect(state, "sửa GPA thành 3.8 hệ 4", slm)
    assert state.profile["gpa"].value == {"value": 3.8, "scale": 4}
    assert state.question_id == "X1"
    assert output.widget.type == "review"


# Đã hiện bảng rà soát rồi (đang sửa): trả lời xong câu của mục thì quay về bảng, không hỏi tiếp câu khác.
# Hồ sơ điền bằng form nên chat chưa hỏi câu tùy chọn nào; trước đây sẽ hỏi tiếp cả loạt
async def test_after_editing_ho_so_item_returns_to_review():
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    ask(state, "D2")
    output = await make_handler(FakeLLM()).collect(state, "GPA 3.8 thang 4", MAC_DINH)
    assert state.profile["gpa"].value == {"value": 3.8, "scale": 4}
    assert state.question_id == "X1"
    assert output.widget.type == "review"


async def test_after_editing_bac_hoc_item_returns_to_review():
    llm = FakeLLM({**NO_BAC_HOC, "intake": "Fall 2027"})
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    ask(state, "B1.4")
    output = await make_handler(llm).collect(state, "kỳ Fall 2027", MAC_DINH)
    assert state.question_id == "X1"
    assert output.widget.type == "review"


# Chưa từng hiện bảng (đang điền lần đầu): vẫn hỏi câu tiếp như cũ
async def test_first_fill_still_asks_next_question():
    state = ChatState(current_step="B2", intake_mode="chat")
    ask(state, "D2")
    await make_handler(FakeLLM()).collect(state, "GPA 3.8 thang 4", MAC_DINH)
    assert state.question_id not in ("D2", "X1")


# Sửa nhiều mục một lúc mà chưa có giá trị mới: hỏi lần lượt từng mục theo thứ tự hồ sơ, hết thì về bảng rà soát
async def test_edit_many_items_asks_each_in_turn_then_review():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat")
    ask(state, "X1")
    slm = tin(("collect_profile", {"tasks": ["edit"], "sat": "SAT", "gpa": "GPA"}))
    chat = make_handler(FakeLLM(NO_HO_SO))
    await chat.collect(state, "sửa GPA và SAT", slm)
    assert (state.question_id, state.edit_queue) == ("D2", ["D3"])
    await chat.collect(state, "GPA 3.8 thang 4", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("D3", [])
    output = await chat.collect(state, "Em chưa thi SAT/ACT", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("X1", None)  # hết mục: về bảng, thôi đánh dấu đang sửa
    assert output.widget.type == "review"


# Hàng đợi đi qua được cả câu bậc học (B1) lẫn câu hồ sơ (B2), không đổi bước
async def test_edit_queue_crosses_bac_hoc_and_ho_so_questions():
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    slm = tin(("collect_profile", {"tasks": ["edit"], "gpa": "GPA", "major": "ngành"}))
    chat = make_handler(FakeLLM(NO_BAC_HOC, NO_HO_SO))
    await chat.collect(state, "sửa ngành và GPA", slm)
    assert (state.question_id, state.edit_queue) == ("B1.3", ["D2"])
    # Hồ sơ điền bằng form, chat chưa từng hiện bảng: vẫn sang mục kế tiếp chứ không hỏi tiếp cả loạt câu khác
    await chat.collect(state, "Em chưa rõ ngành", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("D2", [])
    assert state.current_step == "B3"
    output = await chat.collect(state, "GPA 3.8 thang 4", MAC_DINH)
    assert (state.question_id, output.widget.type) == ("X1", "review")


# Mục có giá trị mới thì ghi luôn, chỉ hỏi mục chưa có giá trị
async def test_edit_saves_items_with_value_and_asks_the_rest():
    llm = FakeLLM({"values": {"gpa": {"value": 3.8, "scale": 4}}, "none_fields": []})
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat")
    ask(state, "X1")
    slm = tin(("collect_profile", {"tasks": ["edit"], "gpa": "3.8", "sat": "SAT"}))
    await make_handler(llm).collect(state, "sửa GPA thành 3.8 hệ 4 và sửa SAT", slm)
    assert state.profile["gpa"].value == {"value": 3.8, "scale": 4}
    assert (state.question_id, state.edit_queue) == ("D3", [])


# SAT và ACT chung một câu D3: chỉ hỏi một lần
async def test_edit_sat_and_act_asks_once():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat")
    ask(state, "X1")
    slm = tin(("collect_profile", {"tasks": ["edit"], "sat": "SAT", "act": "ACT"}))
    await make_handler(FakeLLM(NO_HO_SO)).collect(state, "sửa SAT và ACT", slm)
    assert (state.question_id, state.edit_queue) == ("D3", [])


# Bấm "Bỏ qua" ở câu tùy chọn cũng sang mục kế tiếp
async def test_skip_moves_to_next_item_in_queue():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    state.edit_queue = ["F1"]
    ask(state, "D3")
    await make_handler(FakeLLM()).collect(state, "Bỏ qua", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("F1", [])


# Giá trị sai thì hỏi lại đúng câu đó, các mục sau vẫn còn trong hàng đợi
async def test_invalid_answer_keeps_queue():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    state.edit_queue = ["D3"]
    ask(state, "D2")
    await make_handler(FakeLLM()).collect(state, "GPA 7 thang 4", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("D2", ["D3"])


# Đang sửa dở mà muốn sửa thêm mục khác: mục mới hỏi trước, mục đang hỏi và các mục chưa hỏi vẫn giữ
async def test_new_edit_while_editing_keeps_unanswered_items():
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    state.edit_queue = ["F1"]
    ask(state, "D3")
    slm = tin(("collect_profile", {"tasks": ["edit"], "awards": "giải thưởng"}))
    await make_handler(FakeLLM(NO_HO_SO)).collect(state, "à sửa cả giải thưởng", slm)
    assert (state.question_id, state.edit_queue) == ("D5", ["D3", "F1"])


# Đang hỏi một mục trong hàng đợi mà học sinh trả lời kèm chữ "sửa" (SLM gắn edit): vẫn là câu trả lời, sang mục kế
async def test_answer_tagged_edit_while_editing_moves_on():
    llm = FakeLLM({"values": {"gpa": {"value": 3.8, "scale": 4}}, "none_fields": []})
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    state.edit_queue = ["F1"]
    ask(state, "D2")
    slm = tin(("collect_profile", {"tasks": ["edit"], "gpa": "3.8"}))
    await make_handler(llm).collect(state, "sửa GPA thành 3.8 hệ 4", slm)
    assert state.profile["gpa"].value == {"value": 3.8, "scale": 4}
    assert (state.question_id, state.edit_queue) == ("F1", [])


# Ô chọn mục cần sửa: học sinh tích nhiều nhóm, chat hỏi lần lượt câu của từng nhóm rồi về bảng rà soát
KEEP = skill_ho_so.KEEP_REPLY.value


def at_review(profile: dict[str, ProfileField] | None = None) -> ChatState:
    state = ChatState(current_step="B2", profile=profile or full_profile(), intake_mode="chat")
    ask(state, "X1")
    return state


def labels(output) -> list[str]:
    return [reply.label for reply in output.quick_replies]


# Bấm "Cần sửa": hiện ô chọn nhiều nhóm và 3 nút, không gọi Qwen
async def test_fix_button_shows_edit_menu():
    llm = FakeLLM()
    state = at_review()
    assert skill_ho_so.is_form_text("X1", skill_ho_so.FIX_REPLY.value)
    output = await make_handler(llm).collect(state, skill_ho_so.FIX_REPLY.value, MAC_DINH)
    assert output.answer == skill_ho_so.EDIT_MENU
    assert output.widget.type == "select"
    field = output.widget.data["fields"][0]
    assert field["multiple"] is True
    assert [o["label"] for o in field["options"]] == list(skill_ho_so.EDIT_SECTIONS)
    assert labels(output) == ["Sửa trên form", "Điền lại từ đầu", "Đúng rồi"]
    assert (state.question_id, state.edit_queue) == ("X1", None)
    assert llm.calls == []


# Chọn hai nhóm: hỏi lần lượt theo thứ tự form, bỏ câu không cần hỏi (D1 chỉ hỏi học sinh cấp 3), hết thì về bảng
async def test_pick_sections_asks_each_question_then_review():
    llm = FakeLLM()
    chat = make_handler(llm)
    state = at_review()
    message = "Em muốn sửa Học lực (GPA), SAT/ACT"
    assert skill_ho_so.is_form_text("X1", message)
    output = await chat.collect(state, message, MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("D2", ["D3", "E3"])
    assert output.answer == QUESTIONS["D2"]
    assert KEEP in labels(output)  # GPA đã có giá trị
    assert llm.calls == []
    output = await chat.collect(state, "GPA 3.8 thang 4", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("D3", ["E3"])
    assert KEEP not in labels(output)  # chưa có SAT/ACT thì không có gì để giữ
    await chat.collect(state, "Em chưa thi SAT/ACT", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("E3", [])  # chưa thi thì hỏi kế hoạch thi
    output = await chat.collect(state, "Bỏ qua", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("X1", None)
    assert output.widget.type == "review"


# Hàng chờ đi từ câu bậc học (B1) sang câu tài chính (F)
async def test_pick_goal_and_finance_crosses_steps():
    state = at_review()
    await make_handler(FakeLLM()).collect(state, "Em muốn sửa Mục tiêu, Tài chính", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("B1.1", ["B1.2", "B1.3", "B1.4", "F1", "F2", "F3"])


# Câu chỉ hỏi khi cần được xét lúc tới lượt: đã có chứng chỉ tiếng Anh thì không hỏi kế hoạch thi
@pytest.mark.parametrize(("status", "next_id"), [("confirmed", "X1"), ("none", "E2")])
async def test_english_plan_asked_only_when_needed(status, next_id):
    english = ProfileField(value=None if status == "none" else "x", status=status)
    state = at_review({**full_profile(), "english": english})
    chat = make_handler(FakeLLM())
    await chat.collect(state, "Em muốn sửa Tiếng Anh", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("E1", ["E2"])
    await chat.collect(state, KEEP, MAC_DINH)
    assert state.question_id == next_id


# Nhóm Nguyện vọng: N1 (người thân ở bang) chỉ hỏi khi chưa có bang muốn học
async def test_pick_wishes_skips_relatives_when_states_known():
    states = ProfileField(value=["Texas"], status="confirmed")
    state = at_review({**full_profile(), "preferred_states": states})
    chat = make_handler(FakeLLM())
    await chat.collect(state, "Em muốn sửa Nguyện vọng", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("N2", ["N3", "N1"])
    await chat.collect(state, KEEP, MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("N3", ["N1"])
    output = await chat.collect(state, "Bỏ qua", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("X1", None)
    assert output.widget.type == "review"


# Có tên không phải nhóm của ô chọn (học sinh tự gõ) thì không coi là bấm ô chọn, để SLM đọc như tin thường
@pytest.mark.parametrize(
    "message", ["Em muốn sửa Abc", "Em muốn sửa GPA", "Em muốn sửa ", "Em muốn sửa Tiếng Anh, Abc"]
)
def test_unknown_section_is_not_menu_text(message):
    assert skill_ho_so.read_edit_menu(message) is None
    assert not skill_ho_so.is_form_text("X1", message)


async def test_edit_on_form_shows_form_link():
    state = at_review()
    output = await make_handler(FakeLLM()).collect(state, "Sửa trên form", MAC_DINH)
    assert output.widget.type == "form"
    assert state.edit_queue is None


# Điền lại từ đầu: hỏi lại mọi câu theo thứ tự form, bắt đầu từ B1.1; không hỏi visa (N4)
async def test_restart_asks_every_question_from_start():
    state = at_review()
    output = await make_handler(FakeLLM()).collect(state, "Điền lại từ đầu", MAC_DINH)
    assert state.question_id == "B1.1"
    assert state.edit_queue == list(skill_ho_so.EDIT_ORDER[1:])
    assert "N4" not in state.edit_queue
    assert KEEP in labels(output)


# Bấm "Giữ như cũ": không đổi hồ sơ, không gọi Qwen, sang câu kế tiếp
async def test_keep_moves_on_without_change():
    llm = FakeLLM()
    state = ChatState(current_step="B2", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    state.edit_queue = ["F1"]
    ask(state, "D2")
    await make_handler(llm).collect(state, KEEP, MAC_DINH)
    assert state.profile == full_profile()
    assert (state.question_id, state.edit_queue) == ("F1", [])
    assert llm.calls == []


# Giữ hết hoặc bỏ qua câu chưa có giá trị: đi hết lượt điền lại thì về bảng, hồ sơ cũ còn nguyên
async def test_restart_keeping_everything_returns_to_review():
    state = at_review()
    chat = make_handler(FakeLLM())
    output = await chat.collect(state, "Điền lại từ đầu", MAC_DINH)
    for _ in range(len(skill_ho_so.EDIT_ORDER)):
        if state.edit_queue is None:
            break
        output = await chat.collect(state, KEEP if KEEP in labels(output) else "Bỏ qua", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("X1", None)
    assert output.widget.type == "review"
    assert state.profile == full_profile()


# Nút sửa của câu trả lời kịch bản: không có câu đang hỏi mà nhận câu ô chọn thì mở luồng sửa nhóm đó, không gọi Qwen
async def test_edit_menu_text_without_pending_question_starts_edit():
    llm = FakeLLM()
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat", asked={"X1": 1})
    output = await make_handler(llm).collect(state, "Em muốn sửa Mục tiêu", MAC_DINH)
    assert (state.question_id, state.edit_queue) == ("B1.1", ["B1.2", "B1.3", "B1.4"])
    assert KEEP in labels(output)
    assert llm.calls == []
