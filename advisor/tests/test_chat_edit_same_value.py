"""Sửa hồ sơ mà giá trị nói ra trùng giá trị đang lưu: học sinh ĐÃ trả lời, không được hỏi lại câu của mục đó.

Lỗi thật 27/11/2026: "Thôi, mình vẫn thích Computer Science hơn" (đang lưu đúng ngành đó) bị hỏi "Bạn định học ngành gì?".
Giá trị không đổi khác với chưa nói giá trị: chưa nói thì vẫn phải hỏi (test_profile_handler giữ phần đó).
"""

import pytest

from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import ask
from app.services.replies import PROFILE_SAME
from tests.fakes import FakeLLM, tin
from tests.test_profile_handler import NO_BAC_HOC, NO_HO_SO, full_profile, make_handler

GPA = {"value": 3.8, "scale": 4}
SAME_GPA = {"values": {"gpa": GPA}, "none_fields": []}


def saved(**values) -> dict[str, ProfileField]:
    extra = {name: ProfileField(value=value, status="confirmed") for name, value in values.items()}
    return {**full_profile(), **extra}


def edit(**said):
    return tin(("collect_profile", {"tasks": ["edit"], **said}))


@pytest.mark.parametrize("at_review", [True, False])
async def test_restating_the_saved_gpa_is_not_asked_again(at_review):
    state = ChatState(current_step="B3", profile=saved(gpa=GPA), intake_mode="chat")
    if at_review:
        ask(state, "X1")
    handler = make_handler(FakeLLM(SAME_GPA, SAME_GPA))

    output = await handler.collect(state, "GPA em vẫn là 3.8 thang 4", edit(gpa="3.8"))

    assert state.question_id != "D2"
    assert output.widget is not None and output.widget.type == "review"


async def test_restating_the_saved_major_is_not_asked_again():
    state = ChatState(current_step="B3", profile=saved(major="Computer Science"), intake_mode="chat")
    handler = make_handler(FakeLLM({**NO_BAC_HOC, "major": "Computer Science"}))

    output = await handler.collect(
        state, "Thôi, em vẫn thích Computer Science hơn", edit(major="Computer Science")
    )

    assert state.question_id != "B1.3"
    assert output.widget is not None and output.widget.type == "review"
    assert output.answer.startswith(PROFILE_SAME)


async def test_only_the_item_without_a_value_is_asked():
    state = ChatState(current_step="B3", profile=saved(gpa=GPA), intake_mode="chat")
    handler = make_handler(FakeLLM(SAME_GPA, SAME_GPA))

    await handler.collect(state, "GPA vẫn 3.8 thang 4, còn SAT em muốn sửa", edit(gpa="3.8", sat="SAT"))

    assert (state.question_id, state.edit_queue) == ("D3", [])


# Chữ SLM tách có thể không phải giá trị ("sửa GPA"): bộ đọc không đọc ra gì thì vẫn phải hỏi
async def test_naming_an_item_without_a_value_still_asks():
    state = ChatState(current_step="B3", profile=saved(gpa=GPA), intake_mode="chat")

    await make_handler(FakeLLM(NO_HO_SO)).collect(state, "em muốn sửa GPA", edit(gpa="GPA"))

    assert state.question_id == "D2"


# Dấu "đã đọc trong lượt này" chỉ dùng trong lượt, không lưu theo phiên
def test_what_was_read_is_not_stored_with_the_session():
    assert "read_now" not in ChatState().model_dump()


# Lỗi thấy khi chạy Claude thật: bộ đọc được gửi kèm giá trị đang lưu và chép lại nó dù tin không nêu giá trị nào.
# Giá trị chép lại không phải học sinh nói, vẫn phải hỏi
@pytest.mark.parametrize(
    ("name", "stored", "echo", "question_id"),
    [
        ("gpa", GPA, GPA, "D2"),
        ("sat", 1400, 1400, "D3"),
        ("activities", [{"name": "a"}], [{"name": "a"}], "D4"),
    ],
)
async def test_a_value_the_reader_only_copied_still_asks(name, stored, echo, question_id):
    state = ChatState(current_step="B3", profile=saved(**{name: stored}), intake_mode="chat")
    llm = FakeLLM({"values": {name: echo}, "none_fields": []})

    await make_handler(llm).collect(state, "em muốn sửa mục này", edit(**{name: "mục này"}))

    assert state.question_id == question_id
