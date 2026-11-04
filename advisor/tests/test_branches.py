"""Các nhánh theo loại câu hỏi SLM: đủ 6 nhánh, ngoài phạm vi trả câu cố định, nhánh tạm dùng skill cũ đúng chỗ."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField, Widget
from app.schemas.slm import INTENTS
from app.services.branches import (
    NEED_RESULTS,
    NOTHING_TO_TRY,
    OUT_OF_SCOPE,
    NotReadyBranch,
    SkillBranch,
    build_branches,
)
from app.services.chat_flow import NEED_MAJOR, NEED_PROFILE, NOT_READY, QUESTIONS, SkillOutput, ask, go_to
from app.services.intake import CHAT_BUTTON, FORM_BUTTON, INTAKE_ASK, INTAKE_ID, INTAKE_MORE
from app.services.skill_rules import REQUIRED_B3, REQUIRED_B4
from tests.fakes import flow_with, tin


class Recording:
    """Skill cũ giả: ghi tên khi chạy; có thể chuyển bước như skill hỏi hồ sơ làm."""

    def __init__(self, name: str, ran: list[str], moves_to: str | None = None) -> None:
        self.name, self.ran, self.moves_to = name, ran, moves_to

    async def run(self, state, message):
        self.ran.append(self.name)
        if self.moves_to:
            go_to(state, self.moves_to)
        return SkillOutput(f"trả lời của {self.name}")


SKILL_NAMES = ("skill_bac_hoc", "skill_ho_so", "skill_danh_gia", "skill_lo_trinh", "skill_explain",
               "skill_tra_loi", "skill_chuyen_trung_tam")  # fmt: skip


def skills(ran: list[str], **overrides) -> dict:
    return {name: Recording(name, ran) for name in SKILL_NAMES} | overrides


def filled(keys: list[str]) -> dict[str, ProfileField]:
    return {k: ProfileField(value="x", status="confirmed") for k in keys}


def test_there_is_one_branch_for_every_slm_type():
    assert set(build_branches(skills([]))) == set(INTENTS)


def test_knowledge_and_centers_say_not_ready_when_their_skill_is_not_registered():
    branches = build_branches({})

    assert isinstance(branches["kien_thuc"], NotReadyBranch) and isinstance(
        branches["trung_tam"], NotReadyBranch
    )
    assert isinstance(build_branches(skills([]))["kien_thuc"], SkillBranch)


def scenario(**entities):
    return tin(("kich_ban", entities))


async def run_scenario(state: ChatState, **entities):
    return await build_branches(skills([]))["kich_ban"].run(state, "nếu SAT 1400", scenario(**entities))


def what_if(*changes: dict) -> dict:
    return {"che_do": "what_if", "thay_doi": list(changes)}


async def test_what_if_hands_the_checked_values_to_dotnet():
    out = await run_scenario(ChatState(has_results=True), **what_if({"yeu_to": "sat", "gia_tri": "1400"}))

    assert out.action == "simulate"
    assert out.action_data == {"changes": {"sat": 1400}, "unsupported": []}
    assert "SAT 1400" in out.answer


async def test_what_if_needs_a_recommendation_first():
    out = await run_scenario(ChatState(), **what_if({"yeu_to": "sat", "gia_tri": "1400"}))

    assert out.answer == NEED_RESULTS and out.action is None


async def test_what_if_asks_again_instead_of_simulating_a_bad_value():
    out = await run_scenario(ChatState(has_results=True), **what_if({"yeu_to": "sat", "gia_tri": "2000"}))

    assert "400 đến 1600" in out.answer and out.action is None


async def test_what_if_says_what_it_cannot_try():
    state = ChatState(has_results=True)

    only_major = await run_scenario(state, **what_if({"yeu_to": "major", "gia_tri": "CS"}))
    mixed = await run_scenario(
        state, **what_if({"yeu_to": "sat", "gia_tri": "1450"}, {"yeu_to": "major", "gia_tri": "CS"})
    )

    assert only_major.answer == NOTHING_TO_TRY and only_major.action is None
    assert mixed.action == "simulate" and "ngành" in mixed.answer
    assert mixed.action_data["unsupported"] == ["ngành"]


async def test_goal_seeking_is_not_ready_yet():
    out = await run_scenario(ChatState(has_results=True), che_do="goal_seeking", truong="MIT", yeu_to=["sat"])

    assert out.answer == NOT_READY


async def test_knowledge_and_center_branches_run_their_skill():
    ran: list[str] = []
    branches = build_branches(skills(ran))

    await branches["kien_thuc"].run(ChatState(), "I-20 là gì", tin("kien_thuc"))
    await branches["trung_tam"].run(ChatState(), "ai sửa bài luận", tin("trung_tam"))

    assert ran == ["skill_tra_loi", "skill_chuyen_trung_tam"]


# ---- ngoài phạm vi ----
@pytest.mark.parametrize("kind", ["ngoai_le", "nhay_cam", "tam_biet", "mo_ho"])
async def test_out_of_scope_answers_with_the_fixed_sentence_of_its_kind(kind):
    slm = tin(("ngoai_pham_vi", {"loai_con": kind}))
    state = ChatState()
    ask(state, "D2")

    out = await build_branches({})["ngoai_pham_vi"].run(state, "x", slm)

    assert out.answer == OUT_OF_SCOPE[kind]  # câu đang chờ do ChatFlow nhắc lại, nhánh không tự nhắc


def test_the_greeting_thanks_goodbye_sentence_does_not_sound_like_a_goodbye():
    assert OUT_OF_SCOPE["tam_biet"].endswith("?")  # học sinh mở đầu bằng "chào" vẫn hợp


# ---- tư vấn theo hồ sơ (tạm dùng skill cũ) ----
@pytest.mark.parametrize(
    ("state_fields", "truong", "expected"),
    [
        ({"profile": filled(REQUIRED_B3)}, None, "skill_danh_gia"),  # chưa có danh sách: đánh giá trước
        ({"has_results": True, "profile": filled(REQUIRED_B4)}, "Iowa", "skill_explain"),  # hỏi một trường
        ({"has_results": True, "profile": filled(REQUIRED_B4)}, None, "skill_lo_trinh"),
    ],
)
async def test_advice_picks_the_old_skill_from_the_results_and_the_school_asked(
    state_fields, truong, expected
):
    ran: list[str] = []
    slm = tin(("ho_so", {"truong": truong} if truong else {}))

    await build_branches(skills(ran))["ho_so"].run(ChatState(**state_fields), "x", slm)

    assert ran == [expected]


async def test_advice_without_a_profile_offers_chat_or_form():
    ran: list[str] = []

    out = await build_branches(skills(ran))["ho_so"].run(
        state := ChatState(), "đánh giá hồ sơ em", tin("ho_so")
    )

    assert ran == [] and out.answer == INTAKE_ASK
    assert out.quick_replies == [CHAT_BUTTON, FORM_BUTTON] and state.question_id == INTAKE_ID


async def test_advice_on_the_opening_question_offers_chat_or_form():
    state = ChatState()
    ask(state, "B0.1")

    out = await build_branches(skills([]))["ho_so"].run(state, "tôi muốn đi du học", tin("ho_so"))

    assert out.quick_replies == [CHAT_BUTTON, FORM_BUTTON] and state.question_id == INTAKE_ID


async def test_advice_with_a_partial_profile_says_how_many_items_are_missing():
    out = await build_branches(skills([]))["ho_so"].run(ChatState(profile=filled(["target_level"])), "x",
                                                         tin("ho_so"))  # fmt: skip

    assert "còn thiếu 3 mục" in out.answer


async def test_advice_while_answering_in_chat_keeps_asking_the_missing_items():
    state = ChatState(profile=filled(["target_level"]), intake_mode="chat")

    out = await build_branches(skills([]))["ho_so"].run(state, "x", tin("ho_so"))

    assert out.answer.startswith(INTAKE_MORE)
    assert (state.current_step, state.question_id) == ("B1", "B1.1")


async def test_advice_asked_in_the_middle_of_the_profile_does_not_start_the_profile_again():
    state = ChatState(current_step="B2", profile=filled(["target_level"]))
    ask(state, "D2")

    out = await build_branches(skills([]))["ho_so"].run(state, "em nên chọn trường nào", tin("ho_so"))

    assert out.answer == NEED_PROFILE
    assert (state.question_id, state.last_question) == ("D2", QUESTIONS["D2"])  # câu đang chờ vẫn giữ nguyên


async def test_roadmap_with_only_the_major_missing_shows_the_major_widget():
    profile = filled([k for k in REQUIRED_B4 if k != "major"]) | {"major": ProfileField(status="unknown")}
    state = ChatState(has_results=True, profile=profile)

    out = await build_branches(skills([]))["ho_so"].run(state, "gợi ý trường cho em", tin("ho_so"))

    assert out.answer == NEED_MAJOR and out.widget == Widget(type="major")
    assert (state.current_step, state.question_id, state.return_to) == ("B1", "B1.3", "B4")


# ---- hồ sơ ----
@pytest.mark.parametrize(("question", "expected"), [("B1.1", "skill_bac_hoc"), ("D2", "skill_ho_so")])
async def test_profile_answer_goes_to_the_skill_owning_the_pending_question(question, expected):
    ran: list[str] = []
    state = ChatState(current_step="B2")
    ask(state, question)

    await build_branches(skills(ran))["mac_dinh"].run(state, "x", tin("mac_dinh"))

    assert ran == [expected]


async def test_the_last_profile_answer_is_followed_by_the_advice_in_the_same_turn():
    ran: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    out = await branches["mac_dinh"].run(state, "8.5", tin("mac_dinh"))

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert out.answer == "trả lời của skill_ho_so\n\ntrả lời của skill_danh_gia"


async def test_a_profile_answer_that_still_leaves_questions_does_not_run_the_advice():
    ran: list[str] = []
    state = ChatState(current_step="B2")
    ask(state, "D3")

    await build_branches(skills(ran))["mac_dinh"].run(state, "8.5", tin("mac_dinh"))

    assert ran == ["skill_ho_so"]


async def test_nothing_new_on_a_ready_profile_does_not_run_the_advice_again():
    ran: list[str] = []
    state = ChatState(current_step="B3", profile=filled(REQUIRED_B3), intake_mode="chat")

    await build_branches(skills(ran))["mac_dinh"].run(state, "ok", tin("mac_dinh"))

    assert ran == []


async def test_the_profile_branch_leaves_the_advice_to_the_advice_intent_of_the_same_message():
    ran: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    await branches["mac_dinh"].run(state, "8.5, em nên nộp trường nào", tin("mac_dinh", "ho_so"))

    assert ran == ["skill_ho_so"]  # nhánh tư vấn của ý ho_so sẽ chạy sau, không tư vấn trước ở đây


async def test_a_profile_answer_with_an_advice_question_gives_the_advice_once():
    ran: list[str] = []
    handlers = skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3"))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")
    message = ChatMessage(role="user", content="8.5, em nên nộp trường nào")

    res = await flow_with(handlers, tin("mac_dinh", "ho_so")).reply(
        ChatRequest(messages=[message], state=state)
    )

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert res.answer.count("trả lời của skill_danh_gia") == 1
