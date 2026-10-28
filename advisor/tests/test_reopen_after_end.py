"""Cuộc trò chuyện đã kết thúc (END) mà học sinh nhắn tiếp: mở lại từ B0 để model chọn skill nhìn đúng ngữ cảnh."""

from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import QUESTIONS, ChatFlow, SkillOutput


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class RecordingPicker:
    """Model giả: ghi lại bước và câu đang chờ mà model được hỏi."""

    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)
        self.seen: list[tuple[str, str]] = []

    async def predict(self, step, last_question, message, missing):
        self.seen.append((step, last_question))
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class NeedSkill:
    async def run(self, state, message):
        return SkillOutput("đã ghi nhu cầu")


def request(message: str, state: ChatState) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)


async def test_a_new_message_after_the_end_is_read_as_the_start_of_a_new_conversation():
    picker = RecordingPicker("skill_nhu_cau")
    flow = ChatFlow(picker, {"skill_nhu_cau": NeedSkill()})

    res = await flow.reply(request("em muốn nhập hồ sơ", ChatState(current_step="END")))

    # model nhìn như vừa được chào ở B0.1, không phải bước END mà nó chưa từng học
    assert picker.seen == [("B0", QUESTIONS["B0.1"])]
    assert res.answer == "đã ghi nhu cầu"


async def test_reopening_keeps_the_profile_already_collected():
    gpa = ProfileField(value=8.0, status="confirmed")
    flow = ChatFlow(RecordingPicker("skill_nhu_cau"), {"skill_nhu_cau": NeedSkill()})

    res = await flow.reply(request("em muốn xem lại", ChatState(current_step="END", profile={"gpa": gpa})))

    assert res.state.profile["gpa"] == gpa  # không bắt học sinh khai lại hồ sơ


async def test_a_conversation_that_has_not_ended_is_not_reset():
    picker = RecordingPicker("skill_ho_so")
    state = ChatState(current_step="B2", last_question=QUESTIONS["D2"], question_id="D2")

    await ChatFlow(picker, {}).reply(request("em được 8.0", state))

    assert picker.seen == [("B2", QUESTIONS["D2"])]
