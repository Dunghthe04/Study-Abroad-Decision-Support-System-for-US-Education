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
    assert isinstance(build_branches(skills([]))["kien_thuc"], KnowledgeBranch)


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


async def test_what_if_hands_priorities_to_dotnet_even_without_profile_values():
    state = ChatState(has_results=True)
    entities = what_if({"yeu_to": "uu_tien", "gia_tri": "uu_tien"})

    out = await build_branches(skills([]))["kich_ban"].run(
        state, "em ưu tiên học bổng nhất, chi phí thì ít quan tâm", scenario(**entities)
    )

    assert out.action == "simulate"
    assert out.action_data == {"changes": {}, "unsupported": [], "priorities": {"aid": 4, "cost": 1}}
    assert "học bổng nhiều mức 4/4" in out.answer


async def test_goal_seeking_hands_the_school_and_target_to_dotnet():
    out = await run_scenario(
        ChatState(), che_do="goal_seeking", truong="MIT", muc_tieu="Vừa sức", yeu_to=["sat"]
    )

    assert out.action == "goal_seek"
    assert out.action_data == {"school": "MIT", "target": "match"}
    assert "MIT" in out.answer


async def test_goal_seeking_without_a_target_leaves_it_to_the_system():
    out = await run_scenario(ChatState(), che_do="goal_seeking", truong="Boston University")

    assert out.action_data == {"school": "Boston University", "target": None}


async def test_goal_seeking_works_without_a_saved_recommendation():
    # Tính từ hồ sơ và dữ liệu trường, không cần danh sách gợi ý có sẵn (khác what-if)
    out = await run_scenario(ChatState(has_results=False), che_do="goal_seeking", truong="MIT")

    assert out.action == "goal_seek"


async def test_goal_seeking_without_a_school_asks_which_one():
    out = await run_scenario(ChatState(has_results=True), che_do="goal_seeking", yeu_to=["sat"])

    assert out.answer == ASK_SCHOOL and out.action is None
    # Hiện ô chọn trường thay vì để học sinh tự gõ đúng tên; nhóm đích để trống vì học sinh chưa nói
    assert out.widget is not None and out.widget.type == "school" and out.widget.data == {"target": None}


async def test_goal_seeking_without_a_school_keeps_the_target_in_the_picker():
    out = await run_scenario(ChatState(), che_do="goal_seeking", muc_tieu="An toàn")

    assert out.widget is not None and out.widget.data == {"target": "safety"}


async def test_knowledge_and_center_branches_run_their_skill():
    ran: list[str] = []
    branches = build_branches(skills(ran))

    await branches["kien_thuc"].run(ChatState(), "I-20 là gì", tin("kien_thuc"))
    await branches["trung_tam"].run(ChatState(), "ai sửa bài luận", tin("trung_tam"))

    assert ran == ["skill_tra_loi", "skill_chuyen_trung_tam"]


async def test_knowledge_branch_hands_the_question_to_dotnet_so_it_can_try_the_database_first():
    ran: list[str] = []
    branches = build_branches(skills(ran))

    out = await branches["kien_thuc"].run(
        ChatState(),
        "học phí Harvard bao nhiêu",
        tin(("kien_thuc", {"truong": "Harvard"}, "học phí Harvard bao nhiêu")),
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
    thuc_the = {"cac_truong": ["BU", "NYU"], "nhiem_vu": "so_sanh", "muc": ["chi_phi"]}

    out = await branches["kien_thuc"].run(
        ChatState(),
        "học phí BU và NYU chênh nhau bao nhiêu",
        tin(("kien_thuc", thuc_the, "học phí BU và NYU chênh nhau bao nhiêu")),
    )

    assert ran == []
    assert out.action == "school_compare"
    assert out.action_data == {
        "question": "học phí BU và NYU chênh nhau bao nhiêu",
        "schools": ["BU", "NYU"],
        "fields": ["chi_phi"],
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
        thuc_the = {
            "nhiem_vu": "tim_kiem",
            "bo_loc": {"bang": ["texas"], "chi_phi_toi_da": "30 nghìn đô", "hoc_bong": "co"},
            "sap_xep": "chi_phi_thap",
        }

        out = await branches["kien_thuc"].run(
            ChatState(), "x", tin(("kien_thuc", thuc_the, "trường rẻ nhất ở texas có học bổng"))
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
        "bo_loc": {"bang": ["texas"], "chi_phi_toi_da": "30 nghìn đô", "hoc_bong": "co"},
        "sap_xep": "chi_phi_thap",
        "kb_answer": LOOKING_UP,
    }


async def test_when_the_model_returns_the_school_and_the_fields_the_knowledge_base_is_skipped():
    ran: list[str] = []
    branches = build_branches(skills(ran))
    thuc_the = {"truong": "harvard", "nhiem_vu": "tra_cuu", "muc": ["gpa", "xep_hang"]}

    out = await branches["kien_thuc"].run(
        ChatState(),
        "gpa đại học harvard là bao nhiêu",
        tin(("kien_thuc", thuc_the, "gpa đại học harvard là bao nhiêu")),
    )

    assert ran == []  # model đã đọc trường và mục: tin model, không chạy kho kiến thức
    assert out.answer == LOOKING_UP
    assert out.action == "school_info"
    assert out.action_data["fields"] == ["gpa", "xep_hang"]
    assert out.action_data["school"] == "harvard"
    assert out.action_data["kb_skipped"] is True
    assert out.action_data["kb_answer"] == LOOKING_UP  # để .NET thay đúng câu tạm này


@pytest.mark.parametrize(
    "thuc_the",
    [
        {"truong": "Harvard"},  # có trường, chưa có mục
        {"muc": ["chi_phi"]},  # có mục, chưa có trường
        {"truong": "Harvard", "muc": ["khac"]},  # điều hệ thống không lưu: kho có thể có
        {"truong": "Harvard", "nhiem_vu": "tra_cuu"},  # có trường và nhiệm vụ, chưa có mục
    ],
)
async def test_the_knowledge_base_still_runs_when_the_model_is_not_sure_enough(thuc_the):
    ran: list[str] = []
    branches = build_branches(skills(ran))

    out = await branches["kien_thuc"].run(ChatState(), "x", tin(("kien_thuc", thuc_the, "x")))

    assert ran == ["skill_tra_loi"]
    assert out.action_data["kb_skipped"] is False


async def test_knowledge_branch_without_a_school_still_hands_over_the_question():
    branches = build_branches(skills([]))

    out = await branches["kien_thuc"].run(ChatState(), "I-20 là gì", tin(("kien_thuc", {}, "I-20 là gì")))

    assert out.action_data["school"] is None
    assert out.action_data["question"] == "I-20 là gì"


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


async def test_advice_without_a_profile_remembers_the_question():
    state = ChatState()

    await build_branches(skills([]))["ho_so"].run(
        state, "em cần lên lộ trình", tin(("ho_so", {}, "em cần lên lộ trình"))
    )

    assert state.pending_advice is not None and state.pending_advice.doan == "em cần lên lộ trình"


async def test_a_second_advice_question_while_waiting_keeps_the_first_one():
    state = ChatState()
    branch = build_branches(skills([]))["ho_so"]

    await branch.run(state, "em cần lên lộ trình", tin(("ho_so", {}, "em cần lên lộ trình")))
    await branch.run(state, "em nên chọn trường nào", tin(("ho_so", {}, "em nên chọn trường nào")))

    assert state.pending_advice.doan == "em cần lên lộ trình"


class RecordingMessage(Recording):
    """Skill cũ giả ghi cả tin nhận được, để biết nhánh tư vấn trả lời câu nào."""

    def __init__(self, name: str, ran: list[str], messages: list[str]) -> None:
        super().__init__(name, ran)
        self.messages = messages

    async def run(self, state, message):
        self.messages.append(message)
        return await super().run(state, message)


async def test_confirming_the_profile_answers_the_question_that_was_waiting():
    ran: list[str] = []
    messages: list[str] = []
    branches = build_branches(skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3"),
                                     skill_danh_gia=RecordingMessage("skill_danh_gia", ran, messages)))  # fmt: skip
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    state.pending_advice = tin(("ho_so", {}, "em cần lên lộ trình")).cac_y[0]
    ask(state, "X1")

    await branches["mac_dinh"].run(state, "Đúng rồi", tin("mac_dinh"))

    assert ran == ["skill_ho_so", "skill_danh_gia"]
    assert messages == ["em cần lên lộ trình"]  # câu hỏi gốc, không phải "Đúng rồi"
    assert state.pending_advice is None


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


# ---- tư vấn theo hồ sơ (nhánh mới: code dựng skill từ khuôn, Claude viết) ----
KHUON = "# SKILL TƯ VẤN HỒ SƠ (skill_ho_so_khuon)"


class FakeGenerator:
    """Bộ sinh skill giả: trả skill soạn sẵn (None = SLM sai), ghi lại câu hỏi đã nhận."""

    def __init__(self, skill: str | None = "skill riêng") -> None:
        self.skill = skill
        self.questions: list[str] = []

    async def generate(self, req):
        self.questions.append(req.cau_hoi)
        return self.skill


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


def new_advice(ran: list[str], generator=None, writer=None):
    return build_branches(skills(ran), generator or FakeGenerator(), writer or FakeWriter())["ho_so"]


@pytest.mark.parametrize(
    "question", ["đánh giá hồ sơ giúp em", "em cần lên lộ trình thế nào", "học bổng em có không"]
)
async def test_profile_advice_goes_through_the_template_skill_then_claude(question):
    ran: list[str] = []
    generator, writer = FakeGenerator(), FakeWriter()
    state = ChatState(profile=filled(REQUIRED_B3))

    out = await new_advice(ran, generator, writer).run(state, question, tin(("ho_so", {}, question)))

    assert out.answer == "lời tư vấn" and out.action is None
    assert writer.questions == [question] and writer.skills[0].startswith(KHUON)
    assert generator.questions == []  # B2: không gọi SLM sinh skill
    assert ran == []  # không chạy skill tĩnh nào


# Writer chỉ nhận việc của khuôn, theo thứ tự SLM trả; việc của Dũng bị bỏ; không có việc nào thì làm cả hai
@pytest.mark.parametrize(
    ("tasks", "expected"),
    [
        (["lo_trinh", "danh_gia"], ["lo_trinh", "danh_gia"]),
        (["danh_gia", "chon_truong", "lo_trinh"], ["danh_gia", "lo_trinh"]),
        (["danh_gia", "so_sanh"], ["danh_gia"]),
        ([], ["danh_gia", "lo_trinh"]),
        (None, ["danh_gia", "lo_trinh"]),
    ],
)
async def test_only_the_template_tasks_go_to_the_skill_and_the_writer(tasks, expected):
    writer = FakeWriter()
    thuc_the = {"cac_nhiem_vu": tasks} if tasks is not None else {}
    if tasks and "so_sanh" in tasks:
        thuc_the["cac_truong"] = ["Iowa", "Purdue"]  # schema: so_sanh luôn kèm từ hai trường
    question = "đánh giá hồ sơ và cho em biết nên làm gì"

    await new_advice([], writer=writer).run(
        ChatState(profile=filled(REQUIRED_B3)), question, tin(("ho_so", thuc_the, question))
    )

    assert writer.tasks == [expected]
    assert "Chọn trường" not in writer.skills[0].split("## 2.")[0]  # mục 1 chỉ có việc của khuôn


async def test_only_choosing_schools_runs_the_recommendation_even_without_the_school_list_words():
    writer = FakeWriter()
    question = "em thuộc nhóm trường nào"
    state = ChatState(profile=filled(REQUIRED_B4))

    out = await new_advice([], writer=writer).run(
        state, question, tin(("ho_so", {"cac_nhiem_vu": ["chon_truong"]}, question))
    )

    assert out.action == "recommend" and out.answer == RECOMMENDING
    assert writer.skills == []


@pytest.mark.parametrize("tasks", [["so_sanh"], ["so_sanh", "giai_thich_truong"]])
async def test_only_comparing_runs_the_school_data_comparison(tasks):
    writer = FakeWriter()
    question = "Iowa và Purdue trường nào hợp với em hơn"
    thuc_the = {"cac_nhiem_vu": tasks, "cac_truong": ["Iowa", "Purdue"]}

    out = await new_advice([], writer=writer).run(ChatState(), question, tin(("ho_so", thuc_the, question)))

    assert out.action == "school_compare"
    assert out.action_data["schools"] == ["Iowa", "Purdue"] and out.action_data["question"] == question
    assert writer.skills == []  # không cần hồ sơ, không gọi Claude


async def test_explaining_a_school_without_its_name_asks_which_school():
    writer = FakeWriter()
    question = "trường đó có hợp với em không"

    out = await new_advice([], writer=writer).run(
        ChatState(profile=filled(REQUIRED_B4)),
        question,
        tin(("ho_so", {"cac_nhiem_vu": ["giai_thich_truong"]}, question)),
    )

    assert out.answer == ASK_WHICH_SCHOOLS and out.action is None
    assert writer.skills == []


async def test_explaining_a_named_school_before_any_recommendation_runs_the_recommendation():
    ran: list[str] = []
    question = "vì sao Iowa hợp với em"
    thuc_the = {"cac_nhiem_vu": ["giai_thich_truong"], "truong": "Iowa"}

    out = await new_advice(ran).run(
        ChatState(has_results=False, profile=filled(REQUIRED_B4)),
        question,
        tin(("ho_so", thuc_the, question)),
    )

    assert out.action == "recommend" and out.answer == RECOMMENDING
    assert ran == []  # chưa có kết quả gợi ý thì chưa giải thích được


@pytest.mark.parametrize(
    "question",
    ["em nên chọn những trường nào ạ?", "Em nen nop truong nao", "Which US universities should I apply to?"],
)
async def test_asking_for_a_school_list_runs_the_recommendation_without_the_slm(question):
    generator = FakeGenerator()
    state = ChatState(profile=filled(REQUIRED_B4))

    out = await new_advice([], generator).run(state, question, tin(("ho_so", {}, question)))

    assert out.action == "recommend" and out.answer == RECOMMENDING
    assert generator.questions == []


async def test_a_school_list_without_the_major_shows_the_major_widget():
    profile = filled([k for k in REQUIRED_B4 if k != "major"]) | {"major": ProfileField(status="unknown")}
    state = ChatState(profile=profile)

    out = await new_advice([]).run(state, "gợi ý trường cho em", tin(("ho_so", {}, "gợi ý trường cho em")))

    assert out.answer == NEED_MAJOR and out.widget == Widget(type="major")


async def test_asking_about_a_suggested_school_runs_the_explanation_without_the_slm():
    ran: list[str] = []
    generator = FakeGenerator()
    state = ChatState(has_results=True, profile=filled(REQUIRED_B4))

    await new_advice(ran, generator).run(
        state, "x", tin(("ho_so", {"truong": "Iowa"}, "vì sao Iowa vừa sức"))
    )

    assert ran == ["skill_explain"] and generator.questions == []


async def test_new_advice_without_a_profile_remembers_the_question_and_asks_for_the_profile():
    generator = FakeGenerator()
    state = ChatState()

    out = await new_advice([], generator).run(state, "x", tin(("ho_so", {}, "đánh giá hồ sơ giúp em")))

    assert out.answer == INTAKE_ASK and generator.questions == []
    assert state.pending_advice.doan == "đánh giá hồ sơ giúp em"


async def test_claude_failing_offers_to_ask_again_instead_of_a_static_skill():
    ran: list[str] = []
    state = ChatState(profile=filled(REQUIRED_B3))

    out = await new_advice(ran, FakeGenerator(None), FakeWriter(None)).run(
        state, "x", tin(("ho_so", {}, "đánh giá hồ sơ giúp em"))
    )

    assert out.answer == CANNOT_ANSWER and ran == []
    assert out.quick_replies == [QuickReply(label=ASK_AGAIN_LABEL, value="đánh giá hồ sơ giúp em")]


async def test_confirming_the_profile_without_a_question_waiting_evaluates_the_profile():
    ran: list[str] = []
    writer = FakeWriter()
    branches = build_branches(
        skills(ran, skill_ho_so=Recording("skill_ho_so", ran, moves_to="B3")), FakeGenerator(), writer
    )
    state = ChatState(current_step="B2", profile=filled(REQUIRED_B3))
    ask(state, "X1")

    await branches["mac_dinh"].run(state, "Đúng rồi", tin("mac_dinh"))

    assert writer.questions == [DEFAULT_QUESTION]  # không đem "Đúng rồi" đi dựng skill
