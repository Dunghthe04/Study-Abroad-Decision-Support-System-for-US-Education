"""Bước 2.10: ChatFlow nối model chọn skill, luật code và các skill. Dùng model giả, skill giả nên không cần Ollama."""

import json
import logging
from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import NEED_PROFILE, NOT_READY, ChatFlow, SkillOutput, last_user_message


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class FakePicker:
    """Model giả: luôn trả danh sách skill soạn sẵn, và ghi lại thông tin được hỏi."""

    def __init__(self, *skills: str, error: Exception | None = None) -> None:
        self._skills = list(skills)
        self._error = error
        self.calls: list[tuple] = []

    async def predict(self, step, last_question, message, missing):
        self.calls.append((step, last_question, message, missing))
        if self._error:
            raise self._error
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class RecordingHandler:
    """Skill giả: ghi lại thứ tự được chạy."""

    def __init__(self, name: str, ran: list[str]) -> None:
        self._name, self._ran = name, ran

    async def run(self, state: ChatState, message: str) -> SkillOutput:
        self._ran.append(self._name)
        return SkillOutput(f"trả lời của {self._name}")


def request(message: str, **state_fields) -> ChatRequest:
    state = ChatState(**{"current_step": "B2", "last_question": "Bạn đã thi SAT chưa?", **state_fields})
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state)


async def test_without_model_uses_current_step_skill_and_keeps_state():
    res = await ChatFlow().reply(request("em được 1420"))

    assert res.skills == ["skill_ho_so"]  # chưa có model: dùng skill của bước B2
    assert res.answer == NOT_READY  # chưa có skill nào đăng ký nên trả câu mẫu
    assert res.state.current_step == "B2"


async def test_two_skills_run_in_fixed_order_and_answers_are_joined():
    ran: list[str] = []
    handlers = {name: RecordingHandler(name, ran) for name in ("skill_ho_so", "skill_tra_loi")}
    picker = FakePicker("skill_tra_loi", "skill_ho_so")  # model gợi ý tra_loi trước

    res = await ChatFlow(picker, handlers).reply(request("SAT 1420, I-20 là gì ạ?"))

    assert ran == ["skill_ho_so", "skill_tra_loi"]  # nhưng chạy skill của bước trước
    assert res.answer == "trả lời của skill_ho_so\n\ntrả lời của skill_tra_loi"
    assert res.skills == ["skill_ho_so", "skill_tra_loi"]


async def test_picker_receives_step_question_message_and_missing_fields():
    picker = FakePicker("skill_ho_so")

    await ChatFlow(picker).reply(request("em được 1420", profile={"gpa": ProfileField(status="confirmed")}))

    step, question, message, missing = picker.calls[0]
    assert (step, question, message) == ("B2", "Bạn đã thi SAT chưa?", "em được 1420")
    assert "gpa" not in missing and "english" in missing  # chỉ gửi những thông tin còn thiếu


async def test_assessment_is_blocked_when_profile_is_incomplete():
    res = await ChatFlow(FakePicker("skill_danh_gia")).reply(request("đánh giá hồ sơ em", current_step="B3"))

    assert res.skills == []
    assert res.answer == NEED_PROFILE


async def test_model_error_falls_back_instead_of_crashing():
    res = await ChatFlow(FakePicker(error=RuntimeError("Ollama tắt"))).reply(request("em được 1420"))

    assert res.skills == ["skill_ho_so"]


async def test_off_topic_repeats_the_current_question():
    res = await ChatFlow(FakePicker("ngoai_le")).reply(request("bạn thích ăn gì"))

    assert "Bạn đã thi SAT chưa?" in res.answer


async def test_goodbye_ends_the_conversation():
    res = await ChatFlow(FakePicker("ket_thuc")).reply(request("em cảm ơn ạ"))

    assert res.state.current_step == "END"


def test_last_user_message_ignores_assistant_messages():
    req = ChatRequest(
        messages=[
            ChatMessage(role="user", content="chào"),
            ChatMessage(role="assistant", content="Bạn cần gì?"),
            ChatMessage(role="user", content="tìm trường"),
            ChatMessage(role="assistant", content="Bạn học lớp mấy?"),
        ]
    )

    assert last_user_message(req) == "tìm trường"


async def test_each_turn_writes_one_json_log_line(caplog):
    with caplog.at_level(logging.INFO, logger="advisor.chat"):
        await ChatFlow(FakePicker("skill_ho_so")).reply(request("em được 1420"))

    line = next(r.getMessage() for r in caplog.records if r.getMessage().startswith("chat_turn "))
    record = json.loads(line.removeprefix("chat_turn "))
    assert record["message"] == "em được 1420"
    assert record["model_skills"] == ["skill_ho_so"]
    assert record["ran"] == ["skill_ho_so"]
    assert record["source"] == "model"
