"""Các nhánh theo loại câu hỏi SLM: đủ 6 nhánh, ngoài phạm vi trả câu cố định, nhánh tạm dùng skill cũ đúng chỗ."""

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField, QuickReply, Widget
from app.schemas.slm import INTENTS
from app.services.branches import (
    ASK_AGAIN_LABEL,
    ASK_SCHOOL,
    ASK_WHICH_SCHOOLS,
    CANNOT_ANSWER,
    DEFAULT_QUESTION,
    LOOKING_UP,
    NEED_RESULTS,
    NOTHING_TO_TRY,
    OUT_OF_SCOPE,
    RECOMMENDING,
    KnowledgeBranch,
    NotReadyBranch,
    build_branches,
)
from app.services.chat_flow import NEED_MAJOR, NEED_PROFILE, QUESTIONS, SkillOutput, ask, go_to
from app.services.intake import CHAT_BUTTON, FORM_BUTTON, INTAKE_ASK, INTAKE_ID, INTAKE_MORE
from app.services.skill_rules import REQUIRED_B3, REQUIRED_B4
from app.services.what_if_writer import WhatIfWriter
from tests.fakes import FakeLLM, flow_with, tin


class Recording:
    """Skill cũ giả: ghi tên khi chạy; có thể chuyển bước như skill hỏi hồ sơ làm."""

    def __init__(self, name: str, ran: list[str], moves_to: str | None = None) -> None:
        self.name, self.ran, self.moves_to = name, ran, moves_to

    async def run(self, state, message, entities=None):
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

    assert isinstance(branches["knowledge"], NotReadyBranch) and isinstance(
        branches["center_referral"], NotReadyBranch
    )
    assert isinstance(build_branches(skills([]))["knowledge"], KnowledgeBranch)


def scenario(**entities):
    return tin(("scenario", entities))


async def run_scenario(state: ChatState, **entities):
    return await build_branches(skills([]))["scenario"].run(state, "nếu SAT 1400", scenario(**entities))


def what_if(*changes: dict) -> dict:
    return {"mode": "what_if", "changes": list(changes)}


async def test_what_if_hands_the_checked_values_to_dotnet():
    out = await run_scenario(ChatState(has_results=True), **what_if({"factor": "sat", "value": "1400"}))

    assert out.action == "simulate"
    assert out.action_data == {"changes": {"sat": 1400}, "unsupported": []}
    assert "SAT 1400" in out.answer


async def test_what_if_needs_a_recommendation_first():
    out = await run_scenario(ChatState(), **what_if({"factor": "sat", "value": "1400"}))

    assert out.answer == NEED_RESULTS and out.action is None


async def test_what_if_asks_again_instead_of_simulating_a_bad_value():
    out = await run_scenario(ChatState(has_results=True), **what_if({"factor": "sat", "value": "2000"}))

    assert "400 đến 1600" in out.answer and out.action is None


async def test_what_if_says_what_it_cannot_try():
    state = ChatState(has_results=True)

    only_major = await run_scenario(state, **what_if({"factor": "major", "value": "CS"}))
    mixed = await run_scenario(
        state, **what_if({"factor": "sat", "value": "1450"}, {"factor": "major", "value": "CS"})
    )

    assert only_major.answer.startswith("Ngành là điều kiện lọc trường") and only_major.action is None
    assert mixed.action == "simulate" and "ngành" in mixed.answer
    assert mixed.action_data["unsupported"] == ["ngành"]


# Mục lọc trường: kèm nút mở luồng sửa đúng nhóm của ô chọn sửa hồ sơ, mỗi nhóm một nút
async def test_what_if_filter_factors_offer_edit_buttons():
    state = ChatState(has_results=True)
    out = await run_scenario(
        state,
        **what_if(
            {"factor": "major", "value": "CS"},
            {"factor": "school_type", "value": "public"},
            {"factor": "target_level", "value": "graduate"},
        ),
    )

    assert out.answer.startswith("Ngành, loại trường và bậc học là điều kiện lọc trường")
    assert "gợi ý trường lại" in out.answer
    assert [(r.label, r.value) for r in out.quick_replies] == [
        ("Sửa Mục tiêu", "Em muốn sửa Mục tiêu"),
        ("Sửa Nguyện vọng", "Em muốn sửa Nguyện vọng"),
    ]
    assert out.action is None


# Mục phần gợi ý trường không dùng: nói không làm đổi danh sách, gợi ý mục thử được, không có nút
@pytest.mark.parametrize(("factor", "name"), [("visa_refused", "Lịch sử visa"), ("intake", "Kỳ nhập học")])
async def test_what_if_no_effect_factors_say_list_unchanged(factor, name):
    out = await run_scenario(ChatState(has_results=True), **what_if({"factor": factor, "value": "x"}))

    assert out.answer.startswith(f"{name} không làm thay đổi danh sách trường gợi ý.")
    assert "Bạn thử đổi được: GPA" in out.answer
    assert out.quick_replies == [] and out.action is None


# Mục không thuộc hai nhóm trên (hoạt động, giải thưởng...) vẫn trả câu chung
async def test_what_if_other_factors_keep_the_general_answer():
    out = await run_scenario(ChatState(has_results=True), **what_if({"factor": "activities", "value": "x"}))

    assert out.answer == NOTHING_TO_TRY


# Thử được kèm mục không thử được: câu ghi chú nói rõ từng loại
async def test_what_if_note_explains_each_group():
    out = await run_scenario(
        ChatState(has_results=True),
        **what_if(
            {"factor": "sat", "value": "1450"},
            {"factor": "activities", "value": "x"},
            {"factor": "visa_refused", "value": "x"},
        ),
    )

    assert out.action == "simulate"
    assert "Mình chưa thử được: hoạt động ngoại khóa." in out.answer
    assert "Lịch sử visa không làm thay đổi danh sách trường gợi ý." in out.answer


async def test_what_if_hands_priorities_to_dotnet_even_without_profile_values():
    state = ChatState(has_results=True)
    entities = what_if({"factor": "priority", "value": "priority"})

    out = await build_branches(skills([]))["scenario"].run(
        state, "em ưu tiên học bổng nhất, chi phí thì ít quan tâm", scenario(**entities)
    )

    assert out.action == "simulate"
    assert out.action_data == {"changes": {}, "unsupported": [], "priorities": {"aid": 4, "cost": 1}}
    assert "học bổng nhiều mức 4/4" in out.answer


async def test_goal_seeking_hands_the_school_and_target_to_dotnet():
    out = await run_scenario(
        ChatState(), mode="goal_seeking", school="MIT", target_group="target", factor=["sat"]
    )

    assert out.action == "goal_seek"
    assert out.action_data == {"school": "MIT", "target": "match"}
    assert "MIT" in out.answer


async def test_goal_seeking_without_a_target_leaves_it_to_the_system():
    out = await run_scenario(ChatState(), mode="goal_seeking", school="Boston University")

    assert out.action_data == {"school": "Boston University", "target": None}


async def test_goal_seeking_works_without_a_saved_recommendation():
    # Tính từ hồ sơ và dữ liệu trường, không cần danh sách gợi ý có sẵn (khác what-if)
    out = await run_scenario(ChatState(has_results=False), mode="goal_seeking", school="MIT")

    assert out.action == "goal_seek"


async def test_goal_seeking_without_a_school_asks_which_one():
    out = await run_scenario(ChatState(has_results=True), mode="goal_seeking", factor=["sat"])

    assert out.answer == ASK_SCHOOL and out.action is None
    # Hiện ô chọn trường thay vì để học sinh tự gõ đúng tên; nhóm đích để trống vì học sinh chưa nói
    assert out.widget is not None and out.widget.type == "school" and out.widget.data == {"target": None}


async def test_goal_seeking_without_a_school_keeps_the_target_in_the_picker():
    out = await run_scenario(ChatState(), mode="goal_seeking", target_group="safety")

    assert out.widget is not None and out.widget.data == {"target": "safety"}


async def test_knowledge_and_center_branches_run_their_skill():
    ran: list[str] = []
    branches = build_branches(skills(ran))

    await branches["knowledge"].run(ChatState(), "I-20 là gì", tin("knowledge"))
    await branches["center_referral"].run(ChatState(), "ai sửa bài luận", tin("center_referral"))

    assert ran == ["skill_tra_loi", "skill_chuyen_trung_tam"]


async def test_the_center_branch_hands_the_entities_the_model_extracted_to_the_skill():
    seen: list[dict] = []

    class Skill:
        async def run(self, state, message, entities=None):
            seen.append(entities)
            return SkillOutput("ok")

    branch = build_branches({"skill_chuyen_trung_tam": Skill()})["center_referral"]

    await branch.run(
        ChatState(),
        "sửa bài luận ở Sài Gòn",
        tin(("center_referral", {"service": "ESSAY_EDITING", "city": "Sài Gòn"})),
    )

    assert seen == [{"service": "ESSAY_EDITING", "city": "Sài Gòn"}]


async def test_knowledge_branch_hands_the_question_to_dotnet_so_it_can_try_the_database_first():
    ran: list[str] = []
    branches = build_branches(skills(ran))

    out = await branches["knowledge"].run(
        ChatState(),
        "học phí Harvard bao nhiêu",
        tin(("knowledge", {"schools": ["Harvard"]}, "học phí Harvard bao nhiêu")),
    )

    assert ran == ["skill_tra_loi"]  # kho kiến thức vẫn chạy: là câu trả lời dự phòng
    assert out.answer == "trả lời của skill_tra_loi"
    assert out.action == "school_info"
    assert out.action_data == {
        "question": "học phí Harvard bao nhiêu",
        "school": "Harvard",
        "fields": [],
        "task": None,
        "kb_answer": "trả lời của skill_tra_loi",
        "kb_found": False,
        "kb_skipped": False,
    }


async def test_a_comparison_the_model_read_goes_to_dotnet_without_the_knowledge_base():
    ran: list[str] = []
    branches = build_branches(skills(ran))
    entities = {"schools": ["BU", "NYU"], "tasks": ["compare_facts"], "fields": ["cost"]}

    out = await branches["knowledge"].run(
        ChatState(),
        "học phí BU và NYU chênh nhau bao nhiêu",
        tin(("knowledge", entities, "học phí BU và NYU chênh nhau bao nhiêu")),
    )

    assert ran == []
    assert out.action == "school_compare"
    assert out.action_data == {
        "question": "học phí BU và NYU chênh nhau bao nhiêu",
        "schools": ["BU", "NYU"],
        "fields": ["cost"],
        "kb_answer": LOOKING_UP,
    }
    assert out.answer == LOOKING_UP


async def test_a_search_the_model_read_is_normalized_and_goes_to_dotnet_without_the_knowledge_base(
    monkeypatch,
):
    from app.services.skills import exchange_rate, major_catalog, state_catalog
    from app.services.skills.state_catalog import UsState

    state_catalog.set_states([UsState("TX", "Texas")])
    major_catalog.set_majors([])
    monkeypatch.setattr(exchange_rate, "_rate", 25_000.0)
    try:
        ran: list[str] = []
        branches = build_branches(skills(ran))
        entities = {
            "tasks": ["search"],
            "filters": {"state": ["texas"], "max_cost": "30 nghìn đô", "aid": "yes"},
            "sort": "lowest_cost",
        }

        out = await branches["knowledge"].run(
            ChatState(), "x", tin(("knowledge", entities, "trường rẻ nhất ở texas có học bổng"))
        )
    finally:
        state_catalog.set_states(None)
        major_catalog.set_majors(None)

    assert ran == []
    assert out.action == "school_search"
    assert out.action_data == {
        "question": "trường rẻ nhất ở texas có học bổng",
        "filters": {"states": ["TX"], "maxCostUsd": 30000, "aid": "any"},
        "sort": "lowestCost",
        "unresolved": [],
        "model_filters": {"state": ["texas"], "max_cost": "30 nghìn đô", "aid": "yes"},
        "model_sort": "lowest_cost",
        "kb_answer": LOOKING_UP,
    }


async def test_when_the_model_returns_the_school_and_the_fields_the_knowledge_base_is_skipped():
    ran: list[str] = []
    branches = build_branches(skills(ran))
    entities = {"schools": ["harvard"], "tasks": ["school_info"], "fields": ["gpa", "ranking"]}

    out = await branches["knowledge"].run(
        ChatState(),
        "gpa đại học harvard là bao nhiêu",
        tin(("knowledge", entities, "gpa đại học harvard là bao nhiêu")),
    )

    assert ran == []  # model đã đọc trường và mục: tin model, không chạy kho kiến thức
    assert out.answer == LOOKING_UP
    assert out.action == "school_info"
    assert out.action_data["fields"] == ["gpa", "ranking"]
    assert out.action_data["school"] == "harvard"
    assert out.action_data["kb_skipped"] is True
    assert out.action_data["kb_answer"] == LOOKING_UP  # để .NET thay đúng câu tạm này


@pytest.mark.parametrize(
    "entities",
    [
        {"schools": ["Harvard"]},  # có trường, chưa có mục
        {"fields": ["cost"]},  # có mục, chưa có trường
        {"schools": ["Harvard"], "fields": ["other"]},  # điều hệ thống không lưu: kho có thể có
        {"schools": ["Harvard"], "tasks": ["school_info"]},  # có trường và nhiệm vụ, chưa có mục
    ],
)
async def test_the_knowledge_base_still_runs_when_the_model_is_not_sure_enough(entities):
    ran: list[str] = []
    branches = build_branches(skills(ran))

    out = await branches["knowledge"].run(ChatState(), "x", tin(("knowledge", entities, "x")))

    assert ran == ["skill_tra_loi"]
    assert out.action_data["kb_skipped"] is False


async def test_knowledge_branch_without_a_school_still_hands_over_the_question():
    branches = build_branches(skills([]))

    out = await branches["knowledge"].run(ChatState(), "I-20 là gì", tin(("knowledge", {}, "I-20 là gì")))

    assert out.action_data["school"] is None
    assert out.action_data["question"] == "I-20 là gì"


# ---- ngoài phạm vi ----
@pytest.mark.parametrize("kind", ["exception", "sensitive", "farewell", "ambiguous"])
async def test_out_of_scope_answers_with_the_fixed_sentence_of_its_kind(kind):
    slm = tin(("out_of_scope", {"subtype": kind}))
    state = ChatState()
    ask(state, "D2")

    out = await build_branches({})["out_of_scope"].run(state, "x", slm)

    assert out.answer == OUT_OF_SCOPE[kind]  # câu đang chờ do ChatFlow nhắc lại, nhánh không tự nhắc


def test_the_greeting_thanks_goodbye_sentence_does_not_sound_like_a_goodbye():
    assert OUT_OF_SCOPE["farewell"].endswith("?")  # học sinh mở đầu bằng "chào" vẫn hợp


# ---- tư vấn theo hồ sơ (tạm dùng skill cũ) ----
@pytest.mark.parametrize(
    ("state_fields", "school", "expected"),
    [
        ({"profile": filled(REQUIRED_B3)}, None, "skill_danh_gia"),  # chưa có danh sách: đánh giá trước
        ({"has_results": True, "profile": filled(REQUIRED_B4)}, "Iowa", "skill_explain"),  # hỏi một trường
        ({"has_results": True, "profile": filled(REQUIRED_B4)}, None, "skill_lo_trinh"),
    ],
)
async def test_advice_picks_the_old_skill_from_the_results_and_the_school_asked(
    state_fields, school, expected
):
    ran: list[str] = []
    slm = tin(("profile_review", {"schools": [school]} if school else {}))

    await build_branches(skills(ran))["profile_review"].run(ChatState(**state_fields), "x", slm)

    assert ran == [expected]


async def test_advice_without_a_profile_offers_chat_or_form():
    ran: list[str] = []

    out = await build_branches(skills(ran))["profile_review"].run(
        state := ChatState(), "đánh giá hồ sơ em", tin("profile_review")
    )

    assert ran == [] and out.answer == INTAKE_ASK
    assert out.quick_replies == [CHAT_BUTTON, FORM_BUTTON] and state.question_id == INTAKE_ID


async def test_advice_on_the_opening_question_offers_chat_or_form():
    state = ChatState()
    ask(state, "B0.1")

    out = await build_branches(skills([]))["profile_review"].run(
        state, "tôi muốn đi du học", tin("profile_review")
    )

    assert out.quick_replies == [CHAT_BUTTON, FORM_BUTTON] and state.question_id == INTAKE_ID


async def test_advice_with_a_partial_profile_says_how_many_items_are_missing():
    out = await build_branches(skills([]))["profile_review"].run(ChatState(profile=filled(["target_level"])), "x",
                                                         tin("profile_review"))  # fmt: skip

    assert "còn thiếu 3 mục" in out.answer


async def test_advice_while_answering_in_chat_keeps_asking_the_missing_items():
    state = ChatState(profile=filled(["target_level"]), intake_mode="chat")

    out = await build_branches(skills([]))["profile_review"].run(state, "x", tin("profile_review"))

    assert out.answer.startswith(INTAKE_MORE)
    assert (state.current_step, state.question_id) == ("B1", "B1.1")


async def test_advice_asked_in_the_middle_of_the_profile_does_not_start_the_profile_again():
    state = ChatState(current_step="B2", profile=filled(["target_level"]))
    ask(state, "D2")

    out = await build_branches(skills([]))["profile_review"].run(
        state, "em nên chọn trường nào", tin("profile_review")
    )

    assert out.answer == NEED_PROFILE
    assert (state.question_id, state.last_question) == ("D2", QUESTIONS["D2"])  # câu đang chờ vẫn giữ nguyên


async def test_roadmap_with_only_the_major_missing_shows_the_major_widget():
    profile = filled([k for k in REQUIRED_B4 if k != "major"]) | {"major": ProfileField(status="unknown")}
    state = ChatState(has_results=True, profile=profile)

    out = await build_branches(skills([]))["profile_review"].run(
        state, "gợi ý trường cho em", tin("profile_review")
    )

    assert out.answer == NEED_MAJOR and out.widget == Widget(type="major")
    assert (state.current_step, state.question_id, state.return_to) == ("B1", "B1.3", "B4")


# ---- hồ sơ ----
@pytest.mark.parametrize(("question", "expected"), [("B1.1", "skill_bac_hoc"), ("D2", "skill_ho_so")])
async def test_profile_answer_goes_to_the_skill_owning_the_pending_question(question, expected):
    ran: list[str] = []
    state = ChatState(current_step="B2")
    ask(state, question)

    await build_branches(skills(ran))["collect_profile"].run(state, "x", tin("collect_profile"))

    assert ran == [expected]


async def test_the_last_profile_answer_is_followed_by_the_advice_in_the_same_turn():
    ran: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    out = await branches["collect_profile"].run(state, "8.5", tin("collect_profile"))

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert out.answer == "trả lời của skill_ho_so\n\ntrả lời của skill_danh_gia"


async def test_advice_without_a_profile_remembers_the_question():
    state = ChatState()

    await build_branches(skills([]))["profile_review"].run(
        state, "em cần lên lộ trình", tin(("profile_review", {}, "em cần lên lộ trình"))
    )

    assert state.pending_advice is not None and state.pending_advice.span == "em cần lên lộ trình"


async def test_a_second_advice_question_while_waiting_keeps_the_first_one():
    state = ChatState()
    branch = build_branches(skills([]))["profile_review"]

    await branch.run(state, "em cần lên lộ trình", tin(("profile_review", {}, "em cần lên lộ trình")))
    await branch.run(state, "em nên chọn trường nào", tin(("profile_review", {}, "em nên chọn trường nào")))

    assert state.pending_advice.span == "em cần lên lộ trình"


class RecordingMessage(Recording):
    """Skill cũ giả ghi cả tin nhận được, để biết nhánh tư vấn trả lời câu nào."""

    def __init__(self, name: str, ran: list[str], messages: list[str]) -> None:
        super().__init__(name, ran)
        self.messages = messages

    async def run(self, state, message, entities=None):
        self.messages.append(message)
        return await super().run(state, message)


async def test_confirming_the_profile_answers_the_question_that_was_waiting():
    ran: list[str] = []
    messages: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3"),
                                     skill_danh_gia=RecordingMessage("skill_danh_gia", ran, messages)))  # fmt: skip
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    state.pending_advice = tin(("profile_review", {}, "em cần lên lộ trình")).intents[0]
    ask(state, "X1")

    await branches["collect_profile"].run(state, "Đúng rồi", tin("collect_profile"))

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert messages == ["em cần lên lộ trình"]  # câu hỏi gốc, không phải "Đúng rồi"
    assert state.pending_advice is None


async def test_a_profile_answer_that_still_leaves_questions_does_not_run_the_advice():
    ran: list[str] = []
    state = ChatState(current_step="B2")
    ask(state, "D3")

    await build_branches(skills(ran))["collect_profile"].run(state, "8.5", tin("collect_profile"))

    assert ran == ["skill_ho_so"]


async def test_nothing_new_on_a_ready_profile_does_not_run_the_advice_again():
    ran: list[str] = []
    state = ChatState(current_step="B3", profile=filled(REQUIRED_B3), intake_mode="chat")

    await build_branches(skills(ran))["collect_profile"].run(state, "ok", tin("collect_profile"))

    assert ran == []


async def test_the_profile_branch_leaves_the_advice_to_the_advice_intent_of_the_same_message():
    ran: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    await branches["collect_profile"].run(
        state, "8.5, em nên nộp trường nào", tin("collect_profile", "profile_review")
    )

    assert ran == ["skill_ho_so"]  # nhánh tư vấn của ý profile_review sẽ chạy sau, không tư vấn trước ở đây


async def test_a_profile_answer_with_an_advice_question_gives_the_advice_once():
    ran: list[str] = []
    handlers = skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3"))
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")
    message = ChatMessage(role="user", content="8.5, em nên nộp trường nào")

    res = await flow_with(handlers, tin("collect_profile", "profile_review")).reply(
        ChatRequest(messages=[message], state=state)
    )

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert res.answer.count("trả lời của skill_danh_gia") == 1


# ---- tư vấn theo hồ sơ (nhánh mới: code dựng skill từ khuôn, Claude viết) ----
KHUON = "# SKILL TƯ VẤN HỒ SƠ (skill_ho_so_khuon)"


class FakeWriter:
    """Claude giả: trả câu soạn sẵn (None = Claude sai), ghi lại skill đã nhận."""

    def __init__(self, answer: str | None = "lời tư vấn") -> None:
        self.answer = answer
        self.skills: list[str] = []
        self.tasks: list[list[str] | None] = []
        self.questions: list[str] = []

    async def write(self, skill, req, schools, tasks=None):
        self.skills.append(skill)
        self.tasks.append(tasks)
        self.questions.append(req.cau_hoi)
        return self.answer


def new_advice(ran: list[str], writer=None):
    return build_branches(skills(ran), writer or FakeWriter())["profile_review"]


@pytest.mark.parametrize(
    "question", ["đánh giá hồ sơ giúp em", "em cần lên lộ trình thế nào", "học bổng em có không"]
)
async def test_profile_advice_goes_through_the_template_skill_then_claude(question):
    ran: list[str] = []
    writer = FakeWriter()
    state = ChatState(profile=filled(REQUIRED_B3))

    out = await new_advice(ran, writer).run(state, question, tin(("profile_review", {}, question)))

    assert out.answer == "lời tư vấn" and out.action is None
    assert writer.questions == [question] and writer.skills[0].startswith(KHUON)
    assert ran == []  # không chạy skill tĩnh nào


# Writer chỉ nhận việc của khuôn, theo thứ tự SLM trả; việc của Dũng bị bỏ; không có việc nào thì làm cả hai
@pytest.mark.parametrize(
    ("tasks", "expected"),
    [
        (["roadmap", "assessment"], ["roadmap", "assessment"]),
        (["assessment", "school_list", "roadmap"], ["assessment", "roadmap"]),
        (["assessment", "compare"], ["assessment"]),
        ([], ["assessment", "roadmap"]),
        (None, ["assessment", "roadmap"]),
    ],
)
async def test_only_the_template_tasks_go_to_the_skill_and_the_writer(tasks, expected):
    writer = FakeWriter()
    entities = {"tasks": tasks} if tasks is not None else {}
    if tasks and "compare" in tasks:
        entities["schools"] = ["Iowa", "Purdue"]  # schema: compare luôn kèm từ hai trường
    question = "đánh giá hồ sơ và cho em biết nên làm gì"

    await new_advice([], writer=writer).run(
        ChatState(profile=filled(REQUIRED_B3)), question, tin(("profile_review", entities, question))
    )

    assert writer.tasks == [expected]
    assert "Chọn trường" not in writer.skills[0].split("## 2.")[0]  # mục 1 chỉ có việc của khuôn


async def test_only_choosing_schools_runs_the_recommendation_even_without_the_school_list_words():
    writer = FakeWriter()
    question = "em thuộc nhóm trường nào"
    state = ChatState(profile=filled(REQUIRED_B4))

    out = await new_advice([], writer=writer).run(
        state, question, tin(("profile_review", {"tasks": ["school_list"]}, question))
    )

    assert out.action == "recommend" and out.answer == RECOMMENDING
    assert writer.skills == []


@pytest.mark.parametrize("tasks", [["compare"], ["compare", "explain_school"]])
async def test_only_comparing_runs_the_school_data_comparison(tasks):
    writer = FakeWriter()
    question = "Iowa và Purdue trường nào hợp với em hơn"
    entities = {"tasks": tasks, "schools": ["Iowa", "Purdue"]}

    out = await new_advice([], writer=writer).run(
        ChatState(), question, tin(("profile_review", entities, question))
    )

    assert out.action == "school_compare"
    assert out.action_data["schools"] == ["Iowa", "Purdue"] and out.action_data["question"] == question
    assert writer.skills == []  # không cần hồ sơ, không gọi Claude


async def test_explaining_a_school_without_its_name_asks_which_school():
    writer = FakeWriter()
    question = "trường đó có hợp với em không"

    out = await new_advice([], writer=writer).run(
        ChatState(profile=filled(REQUIRED_B4)),
        question,
        tin(("profile_review", {"tasks": ["explain_school"]}, question)),
    )

    assert out.answer == ASK_WHICH_SCHOOLS and out.action is None
    assert writer.skills == []


async def test_explaining_a_named_school_before_any_recommendation_runs_the_recommendation():
    ran: list[str] = []
    question = "vì sao Iowa hợp với em"
    entities = {"tasks": ["explain_school"], "schools": ["Iowa"]}

    out = await new_advice(ran).run(
        ChatState(has_results=False, profile=filled(REQUIRED_B4)),
        question,
        tin(("profile_review", entities, question)),
    )

    assert out.action == "recommend" and out.answer == RECOMMENDING
    assert ran == []  # chưa có kết quả gợi ý thì chưa giải thích được


@pytest.mark.parametrize(
    "question",
    ["em nên chọn những trường nào ạ?", "Em nen nop truong nao", "Which US universities should I apply to?"],
)
async def test_asking_for_a_school_list_runs_the_recommendation_without_the_slm(question):
    state = ChatState(profile=filled(REQUIRED_B4))

    out = await new_advice([]).run(state, question, tin(("profile_review", {}, question)))

    assert out.action == "recommend" and out.answer == RECOMMENDING


async def test_a_school_list_without_the_major_shows_the_major_widget():
    profile = filled([k for k in REQUIRED_B4 if k != "major"]) | {"major": ProfileField(status="unknown")}
    state = ChatState(profile=profile)

    out = await new_advice([]).run(
        state, "gợi ý trường cho em", tin(("profile_review", {}, "gợi ý trường cho em"))
    )

    assert out.answer == NEED_MAJOR and out.widget == Widget(type="major")


async def test_asking_about_a_suggested_school_runs_the_explanation_without_the_slm():
    ran: list[str] = []
    state = ChatState(has_results=True, profile=filled(REQUIRED_B4))

    await new_advice(ran).run(
        state, "x", tin(("profile_review", {"schools": ["Iowa"]}, "vì sao Iowa vừa sức"))
    )

    assert ran == ["skill_explain"]


async def test_new_advice_without_a_profile_remembers_the_question_and_asks_for_the_profile():
    state = ChatState()

    out = await new_advice([]).run(state, "x", tin(("profile_review", {}, "đánh giá hồ sơ giúp em")))

    assert out.answer == INTAKE_ASK
    assert state.pending_advice.span == "đánh giá hồ sơ giúp em"


async def test_claude_failing_offers_to_ask_again_instead_of_a_static_skill():
    ran: list[str] = []
    state = ChatState(profile=filled(REQUIRED_B3))

    out = await new_advice(ran, FakeWriter(None)).run(
        state, "x", tin(("profile_review", {}, "đánh giá hồ sơ giúp em"))
    )

    assert out.answer == CANNOT_ANSWER and ran == []
    assert out.quick_replies == [QuickReply(label=ASK_AGAIN_LABEL, value="đánh giá hồ sơ giúp em")]


async def test_confirming_the_profile_without_a_question_waiting_evaluates_the_profile():
    ran: list[str] = []
    writer = FakeWriter()
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")), writer)
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    await branches["collect_profile"].run(state, "Đúng rồi", tin("collect_profile"))

    assert writer.questions == [DEFAULT_QUESTION]  # không đem "Đúng rồi" đi dựng skill


CLAUDE_FILTER = {"answer": "Ngành là điều kiện lọc trường. Bạn bấm Sửa Mục tiêu rồi nhờ mình gợi ý lại nhé."}


async def run_with_writer(llm: FakeLLM, *changes: dict):
    branch = build_branches(skills([]), None, WhatIfWriter(llm, "claude-test"))["scenario"]
    return await branch.run(ChatState(has_results=True), "nếu đổi", scenario(**what_if(*changes)))


# Có writer: lời do Claude viết, nút sửa vẫn do code gắn
async def test_what_if_writer_writes_cannot_try_answer():
    llm = FakeLLM(CLAUDE_FILTER)
    out = await run_with_writer(llm, {"factor": "major", "value": "CS"})

    assert out.answer == CLAUDE_FILTER["answer"]
    assert [r.label for r in out.quick_replies] == ["Sửa Mục tiêu"]
    assert out.action is None and len(llm.calls) == 1


# Claude sai cả hai lần (thiếu tên mục): dùng câu cố định, vẫn có nút
async def test_what_if_writer_failure_keeps_fixed_answer():
    bad = {"answer": "Mục này mình không thử tạm được đâu bạn nhé."}
    llm = FakeLLM(bad, bad)
    out = await run_with_writer(llm, {"factor": "major", "value": "CS"})

    assert out.answer.startswith("Ngành là điều kiện lọc trường")
    assert [r.label for r in out.quick_replies] == ["Sửa Mục tiêu"]
    assert len(llm.calls) == 2


# Không gọi Claude khi: mục không thuộc hai nhóm, hoặc có thay đổi thử được (đường simulate của GPA, SAT giữ nguyên)
async def test_what_if_writer_not_called_for_other_factors_or_simulate():
    llm = FakeLLM()
    other = await run_with_writer(llm, {"factor": "activities", "value": "x"})
    sat = await run_with_writer(llm, {"factor": "sat", "value": "1450"}, {"factor": "major", "value": "CS"})

    assert other.answer == NOTHING_TO_TRY
    assert sat.action == "simulate" and sat.action_data["changes"] == {"sat": 1450}
    assert llm.calls == []
