"""Bấm "Đúng rồi" mà còn câu đang chờ: advisor chưa trả lời ngay, báo .NET lưu hồ sơ rồi gọi lại, để câu trả lời dùng
lộ trình tính theo hồ sơ VỪA LƯU (UC-19, UC-154, UC-155).

Lỗi thật: .NET gửi lộ trình (đạt / chưa đạt mốc trường mục tiêu) tính theo hồ sơ đã lưu, còn hồ sơ mới chỉ được lưu
SAU khi advisor trả lời. Học sinh vừa đạt IELTS 7.5 vẫn bị nói "chưa đạt mốc 7.0".
"""

from pathlib import Path

from app.schemas.chat import ChatRequest, ChatState, ProfileField
from app.schemas.slm import SlmY
from app.services.branches import ProfileBranch, ScenarioBranch
from app.services.chat_flow import ChatFlow, SkillOutput, ask
from app.services.replies import PREMIUM_REQUIRED, RESUME_VALUE
from app.services.skills.skill_ho_so import SAVED
from tests.fakes import FakeLLM, tin
from tests.test_profile_handler import full_profile, make_handler

OLD_ROADMAP = {"mode": "target", "summary": "IELTS chưa đạt"}
NEW_ROADMAP = {"mode": "target", "summary": "IELTS đã đạt"}
ASSESS = SlmY(type="profile_review", span="đánh giá hồ sơ em", entities={"tasks": ["assessment"]})


class AdviceSpy:
    """Nhánh tư vấn giả: ghi lại lộ trình mà nó nhìn thấy lúc viết câu trả lời."""

    def __init__(self) -> None:
        self.roadmaps: list[dict | None] = []

    async def run(self, state, message, slm):
        self.roadmaps.append(state.roadmap)
        return SkillOutput(f"đánh giá theo: {(state.roadmap or {}).get('summary')}")


class CountingSlm:
    def __init__(self, result) -> None:
        self.result, self.calls = result, 0

    async def classify(self, state, message):
        self.calls += 1
        return self.result


def make_flow(spy: AdviceSpy, result=None) -> tuple[ChatFlow, CountingSlm]:
    slm = CountingSlm(result or tin("collect_profile"))
    branches = {
        "collect_profile": ProfileBranch(make_handler(FakeLLM()), spy, ScenarioBranch()),
        "profile_review": spy,
        "scenario": ScenarioBranch(),
        "knowledge": AdviceSpy(),
    }
    return ChatFlow(slm, branches), slm


# Đang ở bảng rà soát: đã đổi IELTS trong chat, còn câu xin đánh giá chờ xác nhận
def at_review(pending: SlmY | None = ASSESS) -> ChatState:
    profile = {**full_profile(), "english": ProfileField(value={"overall": 7.5}, status="said_in_B0")}
    state = ChatState(
        current_step="B3",
        profile=profile,
        intake_mode="chat",
        has_results=True,
        unsaved=True,
        pending_advice=pending,
        roadmap=OLD_ROADMAP,
    )
    ask(state, "X1")
    return state


def request(state: ChatState, message: str = "Đúng rồi", **extra) -> ChatRequest:
    return ChatRequest(messages=[{"role": "user", "content": message}], state=state, **extra)


async def test_confirming_asks_the_backend_to_save_before_answering():
    spy = AdviceSpy()
    flow, _ = make_flow(spy)

    response = await flow.reply(request(at_review(), roadmap=OLD_ROADMAP, can_resume=True))

    assert spy.roadmaps == []  # chưa viết gì trên lộ trình cũ
    assert response.resume_needed
    assert response.answer == SAVED
    assert response.state.pending_advice == ASSESS
    assert not response.state.unsaved


async def test_the_second_call_answers_with_the_new_roadmap_without_the_slm():
    spy = AdviceSpy()
    flow, slm = make_flow(spy)
    first = await flow.reply(request(at_review(), roadmap=OLD_ROADMAP, can_resume=True))

    second = await flow.reply(request(first.state, roadmap=NEW_ROADMAP, can_resume=True, resume=True))

    assert spy.roadmaps == [NEW_ROADMAP]
    assert second.answer == "đánh giá theo: IELTS đã đạt"
    assert second.state.pending_advice is None
    assert not second.resume_needed
    assert slm.calls == 0


# .NET bản cũ không báo làm được hai bước: trả lời luôn như trước, không để câu hỏi bị bỏ lửng
async def test_a_backend_that_cannot_resume_gets_the_answer_at_once():
    spy = AdviceSpy()
    flow, _ = make_flow(spy)

    response = await flow.reply(request(at_review(), roadmap=OLD_ROADMAP))

    assert spy.roadmaps == [OLD_ROADMAP]
    assert not response.resume_needed and response.state.pending_advice is None


async def test_nothing_waiting_means_nothing_to_resume():
    flow, _ = make_flow(AdviceSpy())

    response = await flow.reply(request(at_review(pending=None), can_resume=True))

    assert not response.resume_needed


# Gõ xác nhận kèm câu hỏi khác thì lượt đó đã có việc riêng cho .NET: trả lời câu đang chờ ngay như trước
async def test_confirming_together_with_another_question_answers_at_once():
    spy = AdviceSpy()
    state = at_review()
    state.can_resume = True
    both = tin("collect_profile", ("knowledge", {}, "visa F1 là gì"))

    await ProfileBranch(make_handler(FakeLLM()), spy).run(state, "Đúng rồi", both)

    assert len(spy.roadmaps) == 1
    assert not state.resume_needed


# Hồ sơ đủ mà không đi qua bảng rà soát thì .NET không lưu ở lượt này, nên hoãn là mất câu hỏi
async def test_only_a_confirmation_at_the_review_board_is_put_off():
    spy = AdviceSpy()
    state = at_review()
    state.can_resume, state.question_id, state.last_question = True, None, ""
    state.profile["english"] = ProfileField(value={"overall": 7.5}, status="confirmed")
    state.profile.pop("major")

    class Filling:
        def __init__(self) -> None:
            self.answers = [False, True]  # trước lượt này còn thiếu, sau lượt này đủ

        def ready(self, state, required):
            return self.answers.pop(0)

        async def collect(self, state, message, slm):
            return SkillOutput("đã ghi")

    await ProfileBranch(Filling(), spy).run(state, "ngành máy tính", tin("collect_profile"))

    assert len(spy.roadmaps) == 1
    assert not state.resume_needed


# Lần gọi thứ hai lỗi: học sinh bấm nút thử lại, vẫn không cần SLM
async def test_the_retry_button_answers_the_waiting_question():
    spy = AdviceSpy()
    flow, slm = make_flow(spy)
    first = await flow.reply(request(at_review(), can_resume=True))

    retry = await flow.reply(request(first.state, RESUME_VALUE, roadmap=NEW_ROADMAP, can_resume=True))

    assert spy.roadmaps == [NEW_ROADMAP]
    assert retry.state.pending_advice is None
    assert slm.calls == 0


async def test_resuming_with_nothing_waiting_adds_nothing():
    spy = AdviceSpy()
    flow, slm = make_flow(spy)
    state = ChatState(current_step="B3", profile=full_profile())

    response = await flow.reply(request(state, can_resume=True, resume=True))

    assert response.answer == "" and spy.roadmaps == [] and slm.calls == 0


async def test_resuming_respects_the_plan():
    spy = AdviceSpy()
    flow, _ = make_flow(spy)
    state = ChatState(current_step="B3", profile=full_profile(), pending_advice=ASSESS)

    response = await flow.reply(request(state, resume=True, allowed_intents=["knowledge"]))

    assert response.answer == PREMIUM_REQUIRED and spy.roadmaps == []
    assert response.state.pending_advice == ASSESS  # nâng cấp xong câu đó vẫn còn


# Nút trả lời lại do .NET tạo: chữ hai bên lệch nhau thì bấm nút sẽ rơi vào SLM
def test_the_retry_text_is_the_same_as_the_backend():
    code = (
        Path(__file__).parents[2] / "backend/src/StudyAbroad.Application/Advisor/ChatResume.cs"
    ).read_text(encoding="utf-8")

    assert f'RetryValue = "{RESUME_VALUE}"' in code
