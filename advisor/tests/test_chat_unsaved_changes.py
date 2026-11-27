"""Chat có thay đổi hồ sơ chưa xác nhận thì không tính gì trên hồ sơ đó: hiện bảng rà soát, xác nhận xong mới trả lời
câu đã hỏi (UC-19, UC-154, UC-155).

Lỗi thật 27/11/2026: học sinh nói IELTS, SAT, ngân sách mới rồi hỏi gợi ý mà chưa bấm "Đúng rồi"; .NET chỉ lưu hồ sơ
khi xác nhận nên gợi ý và so trường mục tiêu chạy trên hồ sơ cũ, trong khi lời đánh giá dùng một phần số mới. Không
dựa vào trạng thái từng ô để biết còn chờ xác nhận, vì trạng thái đó mất khi .NET gửi lại hồ sơ nháp.
"""

import pytest

from app.schemas.chat import ChatState, ProfileField
from app.services.branches import GeneratedAdviceBranch, ProfileBranch, ScenarioBranch
from app.services.chat_flow import ask, back_from_form
from app.services.replies import UNSAVED_REVIEW
from tests.fakes import FakeLLM, tin
from tests.test_profile_handler import NO_BAC_HOC, full_profile, make_handler

SAT = {"values": {"sat": 1500}, "none_fields": []}
SCHOOLS = tin(("profile_review", {"tasks": ["school_list"]}, "gợi ý trường cho em"))
WHAT_IF = tin(
    ("scenario", {"mode": "what_if", "changes": [{"factor": "sat", "value": "1550"}]}, "nếu SAT 1550")
)


def saved_state(**extra) -> ChatState:
    profile = {**full_profile(), "sat": ProfileField(value=1300, status="confirmed")}
    return ChatState(current_step="B3", profile=profile, intake_mode="chat", has_results=True, **extra)


def advice() -> GeneratedAdviceBranch:
    return GeneratedAdviceBranch({}, writer=object())  # xin danh sách trường không cần Claude viết


async def changed_state() -> ChatState:
    state = saved_state()
    handler = make_handler(FakeLLM(SAT))
    await handler.collect(state, "SAT em giờ 1500", tin(("collect_profile", {"sat": "1500"})))
    state.question_id, state.last_question = None, ""  # bảng rà soát bị câu khác đẩy đi, như khi chat tiếp
    return state


async def test_changing_a_value_in_chat_marks_the_profile_unsaved():
    assert (await changed_state()).unsaved


async def test_restating_the_saved_value_changes_nothing():
    state = saved_state()
    same = {"values": {"sat": 1300}, "none_fields": []}

    await make_handler(FakeLLM(same)).collect(state, "SAT em 1300", tin(("collect_profile", {"sat": "1300"})))

    assert not state.unsaved


@pytest.mark.parametrize(("branch", "slm"), [(advice(), SCHOOLS), (ScenarioBranch(), WHAT_IF)])
async def test_nothing_is_computed_before_the_change_is_confirmed(branch, slm):
    state = await changed_state()

    output = await branch.run(state, slm.intents[0].span, slm)

    assert output.action is None
    assert output.widget is not None and output.widget.type == "review"
    assert output.answer.startswith(UNSAVED_REVIEW)
    assert state.question_id == "X1" and state.pending_advice == slm.intents[0]


@pytest.mark.parametrize(
    ("slm", "action"), [(SCHOOLS, "recommend"), (WHAT_IF, "simulate")], ids=["goi_y", "gia_dinh"]
)
async def test_confirming_saves_and_answers_the_waiting_question(slm, action):
    state = await changed_state()
    scenario = ScenarioBranch()
    branch = advice() if slm is SCHOOLS else scenario
    await branch.run(state, slm.intents[0].span, slm)
    profile = ProfileBranch(make_handler(FakeLLM()), advice(), scenario)

    output = await profile.run(state, "Đúng rồi", tin("collect_profile"))

    assert not state.unsaved and state.pending_advice is None
    assert output.action == action


async def test_the_latest_question_is_the_one_answered():
    state = await changed_state()
    await ScenarioBranch().run(state, "nếu SAT 1550", WHAT_IF)

    await advice().run(state, "gợi ý trường cho em", SCHOOLS)

    assert state.pending_advice == SCHOOLS.intents[0]


# Chỉ xem lại hồ sơ (bảng rà soát đang hiện) mà không đổi gì thì không bị chặn
async def test_looking_at_the_saved_profile_does_not_block():
    state = saved_state()
    ask(state, "X1")

    output = await ScenarioBranch().run(state, "nếu SAT 1550", WHAT_IF)

    assert output.action == "simulate"


# Số giả định trong câu what_if không phải thay đổi hồ sơ
async def test_a_what_if_value_is_not_a_profile_change():
    state = saved_state()

    await ScenarioBranch().run(state, "nếu SAT 1550", WHAT_IF)

    assert not state.unsaved


async def test_saving_with_the_form_clears_the_mark():
    state = await changed_state()

    back_from_form(state)

    assert not state.unsaved


# Dấu đi cùng phiên chat: lượt sau vẫn còn
def test_the_mark_is_kept_with_the_session():
    assert ChatState.model_validate(ChatState(unsaved=True).model_dump(by_alias=True)).unsaved


# Trường do code suy ra (năm tốt nghiệp từ lớp) và trường phụ .NET thêm khi gửi lại (nguồn GPA) không phải thay đổi
@pytest.mark.parametrize(
    ("entities", "reader"),
    [
        ({"gpa": "3.8"}, {"values": {"gpa": {"value": 3.8, "scale": 4}}, "none_fields": []}),
        (
            {"current_grade": "lớp 11"},
            {**NO_BAC_HOC, "current_grade": "Lớp 11", "current_level": "high_school"},
        ),
    ],
)
async def test_fields_added_by_the_system_are_not_changes(entities, reader):
    state = saved_state()
    state.profile["gpa"] = ProfileField(
        value={"value": 3.8, "scale": 4, "source": "overall"}, status="confirmed"
    )
    state.profile["current_level"] = ProfileField(value="high_school", status="confirmed")
    state.profile["current_grade"] = ProfileField(value="Lớp 11", status="confirmed")

    await make_handler(FakeLLM(reader)).collect(
        state, "3.8 thang 4, em lớp 11", tin(("collect_profile", entities))
    )

    assert not state.unsaved


# Nói lại đúng GPA thì ô đó vẫn là đã xác nhận, không bị coi là thông tin mới chỉ vì hồ sơ .NET gửi có thêm nguồn GPA
async def test_restating_a_gpa_sent_back_by_the_backend_stays_confirmed():
    state = saved_state()
    stored = {"value": 3.8, "scale": 4, "source": "overall"}
    state.profile["gpa"] = ProfileField(value=stored, status="confirmed")
    reader = {"values": {"gpa": {"value": 3.8, "scale": 4}}, "none_fields": []}

    await make_handler(FakeLLM(reader)).collect(
        state, "GPA em 3.8 thang 4", tin(("collect_profile", {"gpa": "3.8"}))
    )

    assert (state.profile["gpa"].value, state.profile["gpa"].status) == (stored, "confirmed")
