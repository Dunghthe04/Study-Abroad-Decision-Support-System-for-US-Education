"""Câu hỏi có nhắc lại thông tin hồ sơ ("Em đang học lớp 11, ... có sớm quá không?") chỉ được trả lời câu hỏi:
không bật ô chọn sửa hồ sơ, không gắn thêm câu "Hồ sơ của bạn đã đầy đủ rồi" khi học sinh không yêu cầu."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import QUESTIONS, SkillOutput
from app.services.intake import INTAKE_ID
from app.services.replies import PROFILE_DONE
from app.services.skills import build_handlers
from app.services.skills.skill_ho_so import EDIT_MENU
from tests.fakes import FakeLLM, flow_with, tin

QUESTION = "Em đang học lớp 11, bây giờ chuẩn bị có sớm quá không?"
PROFILE = {
    "current_level": "high_school",
    "current_grade": "Lớp 11",
    "target_level": "undergraduate",
    "major": "Khoa học máy tính",
    "gpa": {"value": 8.7, "scale": 10},
    "sat": 1350,
    "english": {"type": "ielts", "value": 6.5},
    "budget": "tier_35k_50k",
    "scholarship": "preferred",
}
# Lượt trước đã hiện bảng rà soát nên đang chờ câu X1 (chữ câu chờ đi cùng mã câu, như chat thật)
AT_REVIEW = {"current_step": "B3", "question_id": "X1", "last_question": QUESTIONS["X1"]}
BOTH = tin(
    ("collect_profile", {"current_grade": "Em đang học lớp 11"}),
    ("profile_review", {"tasks": ["roadmap"]}),
)


def state(**extra) -> ChatState:
    profile = {k: ProfileField(value=v, status="confirmed") for k, v in PROFILE.items()}
    return ChatState(profile=profile, **extra)


def llm_repeats_the_grade() -> FakeLLM:
    # Claude đọc lại tin: chỉ thấy lớp 11 đã có trong hồ sơ, không có gì mới
    none_hoc = {
        "current_level": None,
        "current_grade": "Lớp 11",
        "target_level": None,
        "major": None,
        "intake": None,
    }
    empty = {"values": {}, "none_fields": []}
    return FakeLLM(none_hoc, empty, empty)


async def reply(chat_state: ChatState, slm, message: str = QUESTION):
    flow = flow_with(build_handlers(llm_repeats_the_grade(), "m"), slm)
    return await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content=message)], state=chat_state)
    )


async def test_a_question_asked_while_the_review_board_is_open_is_only_answered():
    # Lỗi thật 27/11/2026: lượt trước đã hiện bảng rà soát, học sinh hỏi lại một câu có nhắc lớp học thì
    # câu trả lời bị gắn thêm ô chọn mục cần sửa hồ sơ
    res = await reply(state(**AT_REVIEW), BOTH)

    assert res.answer == "lời tư vấn"
    assert EDIT_MENU not in res.answer
    assert res.widget is None
    assert res.state.question_id == "X1"  # bảng rà soát vẫn chờ xác nhận như cũ


async def test_a_question_that_repeats_a_saved_value_does_not_add_a_profile_done_line():
    res = await reply(state(), BOTH)

    assert res.answer == "lời tư vấn"
    assert PROFILE_DONE not in res.answer


async def test_a_question_that_states_a_new_value_still_asks_the_student_to_confirm_it():
    new_grade = tin(
        ("collect_profile", {"current_grade": "em lên lớp 12 rồi"}),
        ("profile_review", {"tasks": ["roadmap"]}),
    )
    llm = FakeLLM(
        {
            "current_level": None,
            "current_grade": "Lớp 12",
            "target_level": None,
            "major": None,
            "intake": None,
        },
        {"values": {}, "none_fields": []},
        {"values": {}, "none_fields": []},
    )
    flow = flow_with(build_handlers(llm, "m"), new_grade)
    res = await flow.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="em lên lớp 12 rồi, có kịp không")], state=state()
        )
    )

    assert res.state.profile["current_grade"].value == "Lớp 12"
    assert res.state.profile["current_grade"].status == "said_in_B0"
    assert res.widget is not None and res.widget.type == "review"


async def test_a_message_that_only_answers_the_review_board_still_shows_the_edit_menu():
    # Không có ý hỏi nào khác: đây là phản hồi cho bảng rà soát, giữ hành vi cũ
    res = await reply(
        state(**AT_REVIEW),
        tin(("collect_profile", {})),
        message="chưa đúng lắm",
    )

    assert res.answer == EDIT_MENU


async def test_a_profile_only_message_on_a_complete_profile_still_says_it_is_complete():
    res = await reply(
        state(),
        tin(("collect_profile", {"current_grade": "Em đang học lớp 11"})),
        message="Em đang học lớp 11",
    )

    assert res.answer == PROFILE_DONE


class Echo:
    """Nhánh kiến thức giả: chỉ trả lời câu hỏi."""

    async def run(self, state, message, slm):
        return SkillOutput("lời kiến thức")


async def empty_profile_reply(*intents):
    flow = flow_with(build_handlers(llm_repeats_the_grade(), "m"), tin(*intents))
    flow._branches["knowledge"] = Echo()
    return await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content=QUESTION)], state=ChatState())
    )


async def test_a_knowledge_question_that_mentions_a_value_does_not_invite_the_student_to_fill_the_profile():
    # Trước đây gắn thêm "Để làm bước này hồ sơ của bạn còn thiếu 5 mục" dù học sinh chỉ hỏi kiến thức
    res = await empty_profile_reply(("collect_profile", {"current_grade": "Em đang học lớp 11"}), "knowledge")

    assert res.answer == "lời kiến thức"
    assert (
        res.state.profile["current_grade"].value == "Lớp 11"
    )  # giá trị nói ra vẫn được ghi nháp để xác nhận sau


async def test_advice_that_needs_the_profile_asks_for_it_only_once():
    # Trước đây hai lời mời điền hồ sơ chồng nhau (của nhánh hồ sơ và của nhánh tư vấn)
    res = await empty_profile_reply(
        ("collect_profile", {"current_grade": "Em đang học lớp 11"}),
        ("profile_review", {"tasks": ["roadmap"]}),
    )

    assert res.state.question_id == INTAKE_ID
    assert chr(10) not in res.answer  # đúng một lời nhắc
