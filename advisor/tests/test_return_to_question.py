"""Việc E1 (TH-59): trả lời kiến thức xen giữa lúc đang hỏi hồ sơ thì nhắc lại câu đang chờ, không bỏ dở cuộc trò chuyện."""

from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import (
    BACK_TO_QUESTION,
    NOT_READY,
    QUESTIONS,
    ChatFlow,
    SkillOutput,
    ask,
)

KNOWLEDGE_ANSWER = "Phí SEVIS là 350 USD [1]."
GPA_QUESTION = QUESTIONS["D2"]


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class PickerOf:
    """Model giả: luôn trả các skill soạn sẵn, ghi lại câu hệ thống đang chờ mà model được hỏi."""

    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)
        self.last_questions: list[str] = []

    async def predict(self, step, last_question, message, missing):
        self.last_questions.append(last_question)
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class KnowledgeSkill:
    async def run(self, state, message):
        return SkillOutput(KNOWLEDGE_ANSWER)


class AskNextSkill:
    """Skill giả của bước hồ sơ: nhận câu trả lời rồi hỏi sang câu khác."""

    async def run(self, state, message):
        return SkillOutput(ask(state, "E1"))


def waiting_for_gpa() -> ChatState:
    state = ChatState(current_step="B2")
    ask(state, "D2")  # hệ thống vừa hỏi GPA và đang chờ học sinh trả lời
    return state


def request(message: str, state: ChatState) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)


async def test_reminds_the_pending_question_after_a_knowledge_answer():
    flow = ChatFlow(PickerOf("skill_tra_loi"), {"skill_tra_loi": KnowledgeSkill()})

    res = await flow.reply(request("Phí SEVIS bao nhiêu?", waiting_for_gpa()))

    assert res.answer == f"{KNOWLEDGE_ANSWER}\n\n{BACK_TO_QUESTION}{GPA_QUESTION}"


async def test_the_pending_question_stays_pending_and_is_not_counted_again():
    flow = ChatFlow(PickerOf("skill_tra_loi"), {"skill_tra_loi": KnowledgeSkill()})

    res = await flow.reply(request("Phí SEVIS bao nhiêu?", waiting_for_gpa()))

    assert res.state.current_step == "B2"
    assert res.state.question_id == "D2" and res.state.last_question == GPA_QUESTION
    assert res.state.asked == {"D2": 1}  # nhắc lại không tính là hỏi thêm lần nữa (tối đa 2 lần)


async def test_the_next_turn_still_answers_the_pending_question():
    picker = PickerOf("skill_tra_loi")
    flow = ChatFlow(picker, {"skill_tra_loi": KnowledgeSkill()})
    first = await flow.reply(request("Phí SEVIS bao nhiêu?", waiting_for_gpa()))

    await flow.reply(request("GPA của em là 8.0", first.state))

    # model vẫn nhìn thấy câu GPA đang chờ ở lượt sau, nên chọn được skill đọc câu trả lời GPA
    assert picker.last_questions == [GPA_QUESTION, GPA_QUESTION]


async def test_no_reminder_when_no_question_is_pending():
    flow = ChatFlow(PickerOf("skill_tra_loi"), {"skill_tra_loi": KnowledgeSkill()})

    res = await flow.reply(request("Phí SEVIS bao nhiêu?", ChatState(current_step="B5")))

    assert res.answer == KNOWLEDGE_ANSWER


async def test_no_reminder_when_another_skill_already_moved_on_to_a_new_question():
    handlers = {"skill_tra_loi": KnowledgeSkill(), "skill_ho_so": AskNextSkill()}
    flow = ChatFlow(PickerOf("skill_tra_loi", "skill_ho_so"), handlers)

    res = await flow.reply(request("Phí SEVIS bao nhiêu? GPA em 8.0", waiting_for_gpa()))

    assert BACK_TO_QUESTION not in res.answer  # câu hỏi đã sang E1, không nhắc câu GPA cũ nữa
    assert QUESTIONS["E1"] in res.answer
    assert res.state.question_id == "E1"


async def test_no_reminder_after_a_skill_that_is_not_an_interruption():
    flow = ChatFlow(PickerOf("skill_ho_so"), {"skill_ho_so": AskNextSkill()})

    res = await flow.reply(request("GPA em 8.0", waiting_for_gpa()))

    assert BACK_TO_QUESTION not in res.answer


async def test_a_skill_without_handler_does_not_add_not_ready_when_another_skill_answered():
    # model chọn thêm skill chưa có handler (skill_bac_hoc): học sinh không được thấy câu "đang hoàn thiện" kèm câu trả lời
    flow = ChatFlow(PickerOf("skill_tra_loi", "skill_bac_hoc"), {"skill_tra_loi": KnowledgeSkill()})

    res = await flow.reply(request("Quy trình xin visa F-1?", ChatState(current_step="B5")))

    assert NOT_READY not in res.answer
    assert res.answer == KNOWLEDGE_ANSWER


async def test_not_ready_is_still_shown_when_no_skill_could_answer():
    flow = ChatFlow(PickerOf("skill_bac_hoc"), {})

    res = await flow.reply(request("em muốn học bậc nào", ChatState(current_step="B1")))

    assert res.answer == NOT_READY
