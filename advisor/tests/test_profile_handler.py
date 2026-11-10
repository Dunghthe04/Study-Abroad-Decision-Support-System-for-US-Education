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


async def test_edit_without_item_shows_review_and_asks_what_to_fix():
    llm = FakeLLM()
    state = ChatState(current_step="B3", profile=full_profile(), intake_mode="chat")
    slm = tin(("collect_profile", {"tasks": ["edit"]}))
    output = await make_handler(llm).collect(state, "em muốn sửa hồ sơ", slm)
    assert output.answer == skill_ho_so.ASK_WHAT_TO_FIX
    assert output.widget.type == "review"
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
