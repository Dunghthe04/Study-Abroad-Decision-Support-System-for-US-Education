from app.schemas.chat import ChatState
from app.services.skills import buttons, profile_handler, skill_ho_so
from tests.fakes import FakeLLM
from tests.test_chat_budget import ASKED, PROFILE
from tests.test_chat_skills_flow import field, turn

# UC-94: học sinh nhận hồ sơ phụ huynh nhập hộ có số tiền tối đa/năm chính xác (budget_usd). Ngân sách cố định theo
# phụ huynh: chat không hỏi F1, "sửa ngân sách" chỉ báo không sửa được, số học sinh nói không ghi đè


def locked_profile() -> dict:
    return dict(PROFILE, budget=field("tier_50k_70k"), budget_usd=field(50000))


def test_locked_only_for_student_with_parent_amount():
    assert buttons.budget_locked(ChatState(profile=locked_profile()))
    # Phụ huynh đang nhập hộ con managed vẫn sửa được
    assert not buttons.budget_locked(ChatState(profile=locked_profile(), exact_budget=True))
    # Chỉ có mức thì không khóa
    assert not buttons.budget_locked(ChatState(profile=dict(PROFILE, budget=field("tier_50k_70k"))))


def test_locked_text_says_amount_and_parent():
    text = buttons.locked_budget_text(ChatState(profile=locked_profile()))
    assert "50.000 USD" in text
    assert "phụ huynh" in text


def test_f1_not_asked_when_locked():
    assert not skill_ho_so.needed(ChatState(profile=locked_profile()), "F1")


def test_edit_budget_says_locked_and_moves_on():
    state = ChatState(
        current_step="B2", question_id="X1", asked=ASKED, profile=locked_profile(), edit_queue=[]
    )
    out = profile_handler.ask_edit_question(state, "F1")

    assert "do phụ huynh đã nhập" in out.answer
    assert state.question_id != "F1"
    assert state.profile["budget_usd"] == field(50000)


async def test_student_saying_budget_does_not_overwrite():
    llm = FakeLLM({"values": {"budget": "tier_20k_35k"}}, {"values": {}})
    res = await turn(
        llm,
        "nhà mình chi được 800 triệu",
        current_step="B2",
        question_id="F2",
        asked=ASKED,
        profile=locked_profile(),
    )

    assert llm.calls  # Qwen có đọc ra mức ngân sách, nhưng không được ghi
    assert res.state.profile["budget"] == field("tier_50k_70k")
    assert res.state.profile["budget_usd"] == field(50000)
