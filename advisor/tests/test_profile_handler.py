from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import ask
from app.services.skills.profile_handler import ChatProfileHandler

handler = ChatProfileHandler()


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
    first = ChatState(profile={
        "gpa": ProfileField(value=8.5, status="confirmed"),
        "major": ProfileField(value="CS", status="confirmed"),
    })
    second = ChatState(profile={
        "major": ProfileField(value="CS", status="confirmed"),
        "gpa": ProfileField(value=8.5, status="confirmed"),
    })
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
    state = ChatState(profile={
        "gpa": ProfileField(value=8.5, status="confirmed"),
        "major": ProfileField(value="CS", status="said_in_B0"),
    })
    assert not handler.ready(state, ["gpa"])