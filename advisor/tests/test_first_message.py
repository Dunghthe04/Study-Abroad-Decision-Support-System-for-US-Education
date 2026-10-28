"""Tin nhắn đầu tiên không bị nuốt: giao diện đã tự chào, nên câu học sinh gõ đầu tiên được xử lý như câu trả lời cho B0.1."""

from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ProfileField
from app.services.chat_flow import QUESTIONS, ChatFlow, SkillOutput
from app.services.intake import CHAT_BUTTON, FORM_BUTTON, INTAKE_ASK


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class RecordingPicker:
    """Model giả: ghi lại bước, câu đang chờ và tin nhắn mà model được hỏi."""

    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)
        self.seen: list[tuple[str, str, str]] = []

    async def predict(self, step, last_question, message, missing):
        self.seen.append((step, last_question, message))
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class KnowledgeSkill:
    async def run(self, state, message):
        return SkillOutput("Phí SEVIS là 350 USD.")


def first(message: str, **fields) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], **fields)


async def test_the_first_message_is_answered_instead_of_only_greeting():
    picker = RecordingPicker("skill_tra_loi")
    flow = ChatFlow(picker, {"skill_tra_loi": KnowledgeSkill()})

    res = await flow.reply(first("Phí SEVIS bao nhiêu?"))

    assert res.answer == "Phí SEVIS là 350 USD."
    # model nhìn tin nhắn như câu trả lời cho lời chào B0.1 mà giao diện đã hiện
    assert picker.seen == [("B0", QUESTIONS["B0.1"], "Phí SEVIS bao nhiêu?")]


async def test_asking_for_an_evaluation_first_with_no_profile_goes_straight_to_the_profile_questions():
    flow = ChatFlow(RecordingPicker("skill_danh_gia"), {})

    res = await flow.reply(first("Em muốn đánh giá hồ sơ"))

    assert res.answer == INTAKE_ASK  # thiếu hồ sơ: mời điền bằng chat hay form
    assert res.quick_replies == [CHAT_BUTTON, FORM_BUTTON]


async def test_still_greets_when_no_skill_can_answer_the_first_message_yet():
    # chưa có model và chưa có skill_nhu_cau: chào như cũ thay vì báo "đang hoàn thiện"
    res = await ChatFlow().reply(first("chào"))

    assert res.answer == QUESTIONS["B0.1"]
    assert res.state.current_step == "B0" and res.state.question_id == "B0.1"
    assert res.state.asked == {"B0.1": 1}


async def test_the_first_turn_keeps_the_study_level_and_the_profile_from_the_form():
    gpa = ProfileField(value=8.0, status="confirmed")

    res = await ChatFlow().reply(first("chào", study_level="undergraduate", profile={"gpa": gpa}))

    assert res.state.study_level == "undergraduate"
    assert res.state.profile["gpa"] == gpa
    assert res.profile_changed == []  # nạp sẵn từ form không phải do chat sửa
