"""Việc B6 bước 6: nối skill_chuyen_trung_tam vào chat (đăng ký, cấu hình, nhắc lại câu đang dở)."""

from dataclasses import dataclass

from app.core.config import Settings
from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import BACK_TO_QUESTION, INTERRUPT_SKILLS, QUESTIONS, ChatFlow, SkillOutput, ask
from app.services.skills import build_handlers
from app.services.skills.skill_chuyen_trung_tam import ChuyenTrungTamHandler

CENTER_ANSWER = "Dưới đây là một số trung tâm đã xác minh có hỗ trợ sửa bài luận: ..."


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class PickerOf:
    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)

    async def predict(self, step, last_question, message, missing):
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class CenterSkill:
    async def run(self, state, message):
        return SkillOutput(CENTER_ANSWER)


def test_backend_base_url_defaults_to_the_local_backend():
    assert Settings(_env_file=None).backend_base_url == "http://localhost:5080"


def test_center_skill_is_registered_only_when_a_finder_is_given():
    assert "skill_chuyen_trung_tam" not in build_handlers(llm=object(), model="m")

    handlers = build_handlers(llm=object(), model="m", centers=object())

    assert isinstance(handlers["skill_chuyen_trung_tam"], ChuyenTrungTamHandler)


def test_center_skill_is_an_interrupting_skill():
    # gợi ý trung tâm không đổi bước, nên trả lời xong phải nhắc lại câu hồ sơ đang chờ (như B5)
    assert "skill_chuyen_trung_tam" in INTERRUPT_SKILLS


async def test_asking_for_a_center_while_answering_the_profile_repeats_the_pending_question():
    state = ChatState(current_step="B2")
    ask(state, "D2")  # đang chờ học sinh trả lời GPA
    flow = ChatFlow(PickerOf("skill_chuyen_trung_tam"), {"skill_chuyen_trung_tam": CenterSkill()})

    res = await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content="có chỗ nào sửa bài luận không")], state=state)
    )

    assert res.answer == CENTER_ANSWER + "\n\n" + BACK_TO_QUESTION + QUESTIONS["D2"]
    assert res.state.current_step == "B2" and res.state.question_id == "D2"  # không bỏ dở câu đang hỏi
