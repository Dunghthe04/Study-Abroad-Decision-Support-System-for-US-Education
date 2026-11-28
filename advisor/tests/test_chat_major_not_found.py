"""Ngành học sinh nói mà danh mục không có, hoặc muốn đổi ngành mà chưa nói ngành mới.

Lỗi thật 27/11/2026: "đổi ngành sang Artificial Intelligence" bị bỏ im lặng rồi hỏi "Bạn định học ngành gì?" mãi;
"em muốn đổi ngành" xóa mất ngành đang lưu vì bộ đọc trả "chưa rõ".
"""

import pytest

from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import ask, is_button
from app.services.skills import major_catalog, skill_ho_so
from app.services.skills.major_catalog import Major
from tests.fakes import FakeLLM, tin
from tests.test_profile_handler import NO_BAC_HOC, NO_HO_SO, full_profile, make_handler

CS = "Computer and information sciences"


@pytest.fixture(autouse=True)
def catalog():
    major_catalog.set_majors([Major(CS, "Khoa học máy tính", ("AI",))])
    yield
    major_catalog.set_majors(None)


def saved_state(at_review=False) -> ChatState:
    profile = {**full_profile(), "major": ProfileField(value=CS, status="confirmed")}
    state = ChatState(current_step="B3", profile=profile, intake_mode="chat")
    if at_review:
        ask(state, "X1")
    return state


def reads(major):
    # ở bảng rà soát có hai bộ đọc cùng chạy: bộ đọc cấp học / ngành rồi bộ đọc hồ sơ
    return FakeLLM({**NO_BAC_HOC, "major": major}, NO_HO_SO)


@pytest.mark.parametrize("at_review", [True, False])
async def test_an_unknown_major_is_reported_by_name_and_the_old_one_is_kept(at_review):
    state = saved_state(at_review)
    slm = tin(("collect_profile", {"tasks": ["edit"], "major": "Artificial Intelligence"}))

    output = await make_handler(reads("Artificial Intelligence")).collect(
        state, "đổi ngành sang Artificial Intelligence", slm
    )

    assert 'chưa tìm thấy ngành "Artificial Intelligence"' in output.answer
    assert state.question_id == "B1.3"
    assert state.profile["major"].value == CS
    assert skill_ho_so.KEEP_REPLY in output.quick_replies  # không muốn đổi nữa thì giữ ngành cũ


async def test_an_unknown_major_said_for_the_first_time_is_reported():
    state = ChatState(current_step="B3", intake_mode="chat")

    output = await make_handler(reads("Artificial Intelligence")).collect(
        state,
        "em muốn học Artificial Intelligence",
        tin(("collect_profile", {"major": "Artificial Intelligence"})),
    )

    assert 'chưa tìm thấy ngành "Artificial Intelligence"' in output.answer
    assert state.question_id == "B1.3" and "major" not in state.profile


async def test_wanting_to_change_major_does_not_erase_the_saved_one():
    state = saved_state()
    slm = tin(("collect_profile", {"tasks": ["edit"], "major": "em muốn đổi ngành"}))

    await make_handler(reads("unknown")).collect(state, "em muốn đổi ngành", slm)

    assert (state.profile["major"].value, state.profile["major"].status) == (CS, "confirmed")
    assert state.question_id == "B1.3"  # hỏi ngành mới


# Chưa lưu ngành nào thì "chưa rõ ngành" vẫn được ghi như cũ
async def test_unsure_is_still_recorded_when_nothing_was_saved():
    state = ChatState(current_step="B3", intake_mode="chat")

    await make_handler(reads("unknown")).collect(
        state, "em chưa biết học ngành gì", tin(("collect_profile", {"major": "chưa biết"}))
    )

    assert state.profile["major"].status == "unknown"


# Nút "Giữ như cũ" phải được nhận là bấm nút (không qua SLM) và đưa về bảng hồ sơ với ngành cũ
async def test_keeping_the_old_major_returns_to_the_review():
    state = saved_state()
    handler = make_handler(reads("Artificial Intelligence"))
    await handler.collect(
        state,
        "đổi ngành sang AI học máy",
        tin(("collect_profile", {"tasks": ["edit"], "major": "AI học máy"})),
    )
    assert is_button(state.question_id, skill_ho_so.KEEP_REPLY.value)

    output = await handler.collect(state, skill_ho_so.KEEP_REPLY.value, tin("collect_profile"))

    assert (state.question_id, state.profile["major"].value) == ("X1", CS)
    assert output.widget.type == "review"
