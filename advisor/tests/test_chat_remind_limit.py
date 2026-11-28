"""Câu hồ sơ đang chờ chỉ được nhắc lại vài lần khi học sinh hỏi chuyện khác, không bám theo mọi câu trả lời.

Lỗi thật 27/11/2026: câu "Bạn định học ngành gì?" dính đuôi hơn 100 lượt liền. Việc cần hồ sơ bị chặn thì vẫn
phải hiện câu hỏi, vì lời báo chặn không tự nói đang thiếu gì.
"""

from app.schemas.chat import ChatState
from app.services.chat_flow import MAX_REMINDERS, QUESTIONS, SkillOutput
from app.services.replies import BACK_TO_QUESTION, NEED_PROFILE
from tests.fakes import tin
from tests.test_chat_flow import FakeBranch, make_flow, request


async def ask_aside(flow, state: ChatState | None, times: int) -> tuple[list[bool], ChatState]:
    """Hỏi chuyện khác nhiều lượt khi câu D2 đang chờ; trả về lượt nào có nhắc lại."""
    reminded = []
    for _ in range(times):
        turn = request("I-20 là gì?", pending=None if state else "D2")
        if state:
            turn.state = state
        res = await flow.reply(turn)
        reminded.append(BACK_TO_QUESTION + QUESTIONS["D2"] in res.answer)
        state = res.state
    return reminded, state


async def test_the_question_is_repeated_only_a_few_times():
    flow, _ = make_flow(tin("knowledge"))

    reminded, state = await ask_aside(flow, None, MAX_REMINDERS + 3)

    assert reminded == [True] * MAX_REMINDERS + [False] * 3
    assert state.question_id == "D2"  # vẫn đang chờ: trả lời lúc nào cũng được ghi


async def test_blocked_advice_still_shows_what_is_being_asked():
    flow, _ = make_flow(tin("knowledge"))
    _, state = await ask_aside(flow, None, MAX_REMINDERS + 1)
    blocked, _ = make_flow(
        tin("profile_review"),
        profile_review=FakeBranch("profile_review", [], SkillOutput(NEED_PROFILE)),
    )
    turn = request("gợi ý trường cho em")
    turn.state = state

    res = await blocked.reply(turn)

    assert res.answer == NEED_PROFILE + "\n\n" + BACK_TO_QUESTION + QUESTIONS["D2"]


# Học sinh vừa làm việc với hồ sơ thì đếm lại từ đầu
async def test_answering_about_the_profile_resets_the_count():
    flow, _ = make_flow(tin("knowledge"))
    _, state = await ask_aside(flow, None, MAX_REMINDERS)
    profile_flow, _ = make_flow(tin(("collect_profile", {"sat": "1400"})))
    turn = request("SAT em 1400")
    turn.state = state
    state = (await profile_flow.reply(turn)).state

    reminded, _ = await ask_aside(flow, state, 1)

    assert reminded == [True]
