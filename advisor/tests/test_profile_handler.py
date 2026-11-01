from app.schemas.chat import ChatState, ProfileField
from app.schemas.slm import SlmResult
from app.services.chat_flow import ask
from app.services.skill_rules import REQUIRED_B3
from app.services.skills.profile_handler import ChatProfileHandler
from tests.fakes import FakeLLM

handler = ChatProfileHandler(FakeLLM(), "test")
MAC_DINH = SlmResult(type="mac_dinh")


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
    await ChatProfileHandler(llm, "test").collect(state, "Em lớp 12", MAC_DINH)
    assert state.profile["current_level"].value == "high_school"
    assert state.question_id == "B1.2"


async def test_collect_ho_so_question_saves_answer():
    llm = FakeLLM({"values": {"gpa": {"value": 8.5, "scale": 10}}, "none_fields": []})
    state = ChatState(current_step="B2")
    ask(state, "D2")
    await ChatProfileHandler(llm, "test").collect(state, "GPA em 8.5 hệ 10", MAC_DINH)
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
