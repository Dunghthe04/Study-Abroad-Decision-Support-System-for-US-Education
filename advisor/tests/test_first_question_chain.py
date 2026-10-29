"""Câu đầu hỏi kiến thức, xin dịch vụ: skill_nhu_cau chuyển sang B5, B6 và skill của bước đó trả lời ngay trong lượt.

Trước đây skill_nhu_cau trả "Phần này mình đang hoàn thiện" thay vì để skill_tra_loi trả lời.
"""

from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import NEED_RESULTS, NOT_READY, ChatFlow, SkillOutput, go_to


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class PickerOf:
    """Model giả: luôn trả các skill soạn sẵn ([] = không chắc, ChatFlow dùng skill của bước)."""

    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)

    async def predict(self, step, last_question, message, missing):
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class NeedSkill:
    """Giả skill_nhu_cau: nhận ra nhu cầu rồi chỉ chuyển bước, không viết câu trả lời (như bản thật)."""

    def __init__(self, step: str) -> None:
        self._step = step

    async def run(self, state, message):
        go_to(state, self._step)
        state.return_to = "B1"
        return SkillOutput("")


class Answer:
    def __init__(self, text: str) -> None:
        self.text, self.calls = text, 0

    async def run(self, state, message):
        self.calls += 1
        return SkillOutput(self.text)


def first(message: str) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=message)])


async def test_a_knowledge_question_as_the_first_message_is_answered_in_the_same_turn():
    knowledge = Answer("IELTS là bài thi tiếng Anh 4 kỹ năng.")
    flow = ChatFlow(PickerOf(), {"skill_nhu_cau": NeedSkill("B5"), "skill_tra_loi": knowledge})

    res = await flow.reply(first("IELTS là gì"))

    assert res.answer == "IELTS là bài thi tiếng Anh 4 kỹ năng."  # không còn "đang hoàn thiện"
    assert res.skills == ["skill_nhu_cau", "skill_tra_loi"]
    assert res.state.current_step == "B5" and res.state.return_to == "B1"


async def test_a_service_request_as_the_first_message_goes_to_the_center_skill():
    centers = Answer("Dưới đây là một số trung tâm đã xác minh...")
    flow = ChatFlow(PickerOf(), {"skill_nhu_cau": NeedSkill("B6"), "skill_chuyen_trung_tam": centers})

    res = await flow.reply(first("em muốn tìm trung tâm sửa bài luận"))

    assert res.answer.startswith("Dưới đây là một số trung tâm")
    assert res.skills[-1] == "skill_chuyen_trung_tam"


async def test_the_answer_skill_runs_once_when_the_model_also_picked_it():
    knowledge = Answer("Câu trả lời.")
    flow = ChatFlow(
        PickerOf("skill_nhu_cau", "skill_tra_loi"),
        {"skill_nhu_cau": NeedSkill("B5"), "skill_tra_loi": knowledge},
    )

    await flow.reply(first("SAT là gì"))

    assert knowledge.calls == 1


async def test_it_says_not_ready_when_the_answer_skill_is_missing():
    # chưa có DB kiến thức: skill_tra_loi không đăng ký, học sinh vẫn được báo thay vì im lặng
    flow = ChatFlow(PickerOf(), {"skill_nhu_cau": NeedSkill("B5")})

    # có state (không phải lượt đầu, lượt đầu chưa skill nào trả lời được thì chào như cũ)
    res = await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content="SAT là gì")], state=ChatState())
    )

    assert res.answer == NOT_READY


async def test_standing_at_b5_does_not_run_the_answer_skill_for_other_skills():
    # đang đứng ở B5 (từ lượt trước) mà học sinh trả lời hồ sơ: chỉ skill hồ sơ chạy, skill_tra_loi không chạy theo
    knowledge = Answer("không được chạy")
    ho_so = Answer("Đã ghi GPA.")
    state = ChatState(current_step="B5")
    flow = ChatFlow(PickerOf("skill_ho_so"), {"skill_tra_loi": knowledge, "skill_ho_so": ho_so})

    res = await flow.reply(
        ChatRequest(messages=[ChatMessage(role="user", content="GPA em 8.5")], state=state)
    )

    assert knowledge.calls == 0 and res.answer == "Đã ghi GPA."


async def test_no_need_for_a_school_list_message_when_another_skill_already_answered():
    # model chọn cả skill_tra_loi và skill_explain: skill_explain bị chặn (chưa có danh sách) nhưng không thêm câu thừa
    flow = ChatFlow(
        PickerOf("skill_explain", "skill_tra_loi"), {"skill_tra_loi": Answer("IELTS là bài thi tiếng Anh.")}
    )

    res = await flow.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="IELTS là gì")], state=ChatState(current_step="B5")
        )
    )

    assert res.answer == "IELTS là bài thi tiếng Anh."
    assert NEED_RESULTS not in res.answer


async def test_still_asks_to_see_the_school_list_first_when_nothing_else_answered():
    flow = ChatFlow(PickerOf("skill_explain"), {})

    res = await flow.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="vì sao trường này khó vào")],
            state=ChatState(current_step="B4"),
        )
    )

    assert res.answer == NEED_RESULTS
