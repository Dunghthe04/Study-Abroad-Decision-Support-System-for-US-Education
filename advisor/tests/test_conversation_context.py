"""Ngữ cảnh hội thoại: nhớ trường, mục, điều kiện tìm trường để hiểu câu hỏi tiếp; model quyết định tiếp nối, quy tắc làm dự phòng."""

import pytest

from app.schemas.chat import ChatContext, ChatState
from app.schemas.slm import SlmTinResult, SlmY, check_entities
from app.services.chat_flow import SkillOutput
from app.services.conversation_context import context_for_model, contextualize, update_context
from app.services.slm_classifier import build_messages, keep_verbatim, slm_input


def state(**ctx) -> ChatState:
    return ChatState(context=ChatContext(**ctx))


def knowledge(message: str, **entities) -> SlmTinResult:
    return SlmTinResult(intents=[SlmY(type="knowledge", span=message, entities=entities)])


def entities_of(slm: SlmTinResult) -> dict:
    return slm.intents[0].entities


# ---- model nói tiếp nối: điền phần câu còn thiếu từ ngữ cảnh
def test_the_model_says_follow_up_and_the_school_comes_from_the_context():
    out = contextualize(
        state(schools=["Harvard"], fields=["cost"]),
        "còn học bổng?",
        knowledge("còn học bổng?", follow_up=True, fields=["aid"]),
    )

    assert entities_of(out)["schools"] == ["Harvard"]
    assert entities_of(out)["fields"] == ["aid"]  # mục model đã trả không bị đè


def test_the_model_says_follow_up_with_only_a_new_school_so_the_field_is_kept():
    out = contextualize(
        state(schools=["Harvard"], fields=["aid"]),
        "còn Yale?",
        knowledge("còn Yale?", follow_up=True, schools=["Yale"]),
    )

    assert entities_of(out)["schools"] == ["Yale"]
    assert entities_of(out)["fields"] == ["aid"]
    assert entities_of(out)["tasks"] == ["school_info"]


def test_the_model_says_it_stands_alone_so_nothing_is_filled():
    out = contextualize(
        state(schools=["Harvard"]), "học bổng là gì", knowledge("học bổng là gì", follow_up=False)
    )

    assert "schools" not in entities_of(out)


def test_two_schools_in_the_context_become_a_comparison():
    out = contextualize(
        state(schools=["BU", "NYU"], fields=["cost"]),
        "còn học bổng?",
        knowledge("còn học bổng?", follow_up=True),
    )

    assert entities_of(out)["schools"] == ["BU", "NYU"]
    assert entities_of(out)["tasks"] == ["compare_facts"]


def test_a_follow_up_search_adds_the_new_condition_to_the_old_filters():
    ctx = state(filters={"state": ["California"], "aid": "yes"}, sort="ranking")
    out = contextualize(
        ctx,
        "chỉ lấy công lập thôi",
        knowledge(
            "chỉ lấy công lập thôi", follow_up=True, tasks=["search"], filters={"school_type": "public"}
        ),
    )

    assert entities_of(out)["filters"] == {"state": ["California"], "aid": "yes", "school_type": "public"}
    assert entities_of(out)["sort"] == "ranking"


def test_a_new_condition_of_the_same_kind_replaces_the_old_one():
    ctx = state(filters={"state": ["California"]})
    out = contextualize(
        ctx, "ở Texas", knowledge("ở Texas", follow_up=True, tasks=["search"], filters={"state": ["Texas"]})
    )

    assert entities_of(out)["filters"] == {"state": ["Texas"]}


# ---- quy tắc dự phòng (model chưa trả follow_up)
@pytest.mark.parametrize(
    "message",
    ["còn học bổng thì sao", "hạn nộp thì sao nhỉ", "GPA bao nhiêu", "trường này xếp hạng bao nhiêu"],
)
def test_without_the_model_a_missing_school_is_filled_when_the_sentence_asks_a_field_or_points_back(message):
    out = contextualize(state(schools=["Harvard"]), message, knowledge(message))

    assert entities_of(out)["schools"] == ["Harvard"]


@pytest.mark.parametrize(
    "message", ["học bổng là gì", "I-20 là gì", "xin chào", "visa F-1 nộp thế nào", "IELTS khác gì TOEFL"]
)
def test_without_the_model_general_questions_are_never_tied_to_the_remembered_school(message):
    out = contextualize(state(schools=["Harvard"]), message, knowledge(message))

    assert "schools" not in entities_of(out)


def test_without_the_model_a_short_follow_up_with_a_school_keeps_the_field():
    out = contextualize(
        state(schools=["Harvard"], fields=["aid"]), "còn Yale?", knowledge("còn Yale?", schools=["Yale"])
    )

    assert entities_of(out)["fields"] == ["aid"]


def test_without_the_model_a_long_sentence_with_a_school_is_not_treated_as_a_follow_up():
    message = "Yale nằm ở bang nào và có đẹp không vậy bạn"
    out = contextualize(
        state(schools=["Harvard"], fields=["aid"]), message, knowledge(message, schools=["Yale"])
    )

    assert "fields" not in entities_of(out)


def test_nothing_changes_without_a_context_or_for_other_intents():
    slm = knowledge("còn học bổng?")
    assert contextualize(ChatState(), "còn học bổng?", slm) is slm
    other = SlmTinResult(intents=[SlmY(type="out_of_scope", span="x", entities={"subtype": "farewell"})])
    assert contextualize(state(schools=["Harvard"]), "tạm biệt", other) is other


# ---- ghi nhớ sau mỗi câu trả lời
def test_a_school_answer_remembers_the_school_and_the_fields():
    s = ChatState()
    update_context(
        s, SkillOutput("x", action="school_info", action_data={"school": "Harvard", "fields": ["cost"]})
    )

    assert (s.context.schools, s.context.fields) == (["Harvard"], ["cost"])


def test_a_comparison_remembers_all_the_schools():
    s = ChatState()
    update_context(
        s,
        SkillOutput("x", action="school_compare", action_data={"schools": ["BU", "NYU"], "fields": ["cost"]}),
    )

    assert s.context.schools == ["BU", "NYU"]


def test_a_search_remembers_the_filters_and_forgets_the_school_because_none_was_chosen():
    s = state(schools=["Harvard"])
    update_context(
        s,
        SkillOutput(
            "x",
            action="school_search",
            action_data={"model_filters": {"state": ["Texas"]}, "model_sort": "lowest_cost"},
        ),
    )

    assert (s.context.filters, s.context.sort, s.context.schools) == ({"state": ["Texas"]}, "lowest_cost", [])


def test_other_actions_leave_the_context_alone():
    s = state(schools=["Harvard"])
    update_context(s, SkillOutput("x", action="simulate", action_data={"changes": {"sat": 1500}}))
    update_context(s, SkillOutput("x"))

    assert s.context.schools == ["Harvard"]


# ---- đầu vào của model và làm sạch đầu ra
def test_the_model_input_carries_the_context_only_when_there_is_one():
    assert "context" not in slm_input("học phí Harvard", None, "")
    ctx = context_for_model(state(schools=["Harvard"], fields=["cost"]))
    assert slm_input("còn học bổng?", None, "", ctx)["context"] == {
        "schools": ["Harvard"],
        "fields": ["cost"],
    }
    assert "context" in build_messages("prompt", "còn học bổng?", None, "", ctx)[1]["content"]
    assert context_for_model(ChatState()) is None


def test_the_center_service_and_city_are_part_of_the_context_the_model_sees():
    ctx = context_for_model(ChatState(context=ChatContext(service="ESSAY_EDITING", city="Hà Nội")))

    assert ctx == {"service": "ESSAY_EDITING", "city": "Hà Nội"}


def test_a_school_carried_from_the_context_survives_the_verbatim_cleaning_only_when_the_model_says_follow_up():
    raw = {
        "intents": [
            {
                "type": "knowledge",
                "span": "còn học bổng?",
                "entities": {"schools": ["Harvard"], "follow_up": True},
            }
        ]
    }
    stranger = {
        "intents": [{"type": "knowledge", "span": "còn học bổng?", "entities": {"schools": ["Harvard"]}}]
    }

    kept = keep_verbatim(raw, "còn học bổng?", {"harvard"})
    dropped = keep_verbatim(stranger, "còn học bổng?", {"harvard"})
    invented = keep_verbatim(raw, "còn học bổng?", {"yale"})

    assert kept["intents"][0]["entities"]["schools"] == ["Harvard"]
    assert (
        "schools" not in dropped["intents"][0]["entities"]
    )  # chép tên từ ngữ cảnh mà không nói tiếp nối: bỏ như trước
    assert "schools" not in invented["intents"][0]["entities"]  # tên không có trong ngữ cảnh: bỏ


def test_follow_up_must_be_a_boolean():
    check_entities("knowledge", {"follow_up": True})
    check_entities("scenario", {"mode": "goal_seeking", "follow_up": False})
    with pytest.raises(ValueError):
        check_entities("knowledge", {"follow_up": "co"})


# ---- cả luồng hai lượt qua ChatFlow: ngữ cảnh đi theo trạng thái phiên
class _Kb:
    async def run(self, state, message):
        return SkillOutput("câu của kho")


async def test_the_second_turn_uses_the_school_remembered_from_the_first_through_the_session_state():
    from app.schemas.chat import ChatMessage, ChatRequest
    from tests.fakes import flow_with, tin

    first = flow_with(
        {"skill_tra_loi": _Kb()},
        tin(
            (
                "knowledge",
                {"schools": ["Harvard"], "tasks": ["school_info"], "fields": ["cost"]},
                "học phí Harvard",
            )
        ),
    )
    r1 = await first.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="học phí Harvard")], state=ChatState(current_step="B4")
        )
    )

    assert r1.action == "school_info"
    assert r1.state.context.schools == ["Harvard"] and r1.state.context.fields == ["cost"]

    # phiên sau (trạng thái đi qua JSON như khi .NET lưu và gửi lại), model không trả trường
    saved = r1.state.model_dump(by_alias=True)
    second = flow_with({"skill_tra_loi": _Kb()}, tin(("knowledge", {}, "còn học bổng thì sao")))
    r2 = await second.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="còn học bổng thì sao")],
            state=ChatState.model_validate(saved),
        )
    )

    assert r2.action == "school_info"
    assert r2.action_data["school"] == "Harvard"


async def test_a_new_conversation_starts_without_a_context():
    from app.schemas.chat import ChatMessage, ChatRequest
    from tests.fakes import flow_with, tin

    flow = flow_with({"skill_tra_loi": _Kb()}, tin(("knowledge", {}, "còn học bổng thì sao")))
    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="còn học bổng thì sao")]))

    assert res.action_data["school"] is None


def test_a_follow_up_search_may_only_change_the_sort_order():
    check_entities("knowledge", {"tasks": ["search"], "follow_up": True, "sort": "lowest_cost"})
    with pytest.raises(ValueError):
        check_entities(
            "knowledge", {"tasks": ["search"], "follow_up": True}
        )  # không điều kiện mới cũng không đổi cách xếp
    with pytest.raises(ValueError):
        check_entities(
            "knowledge", {"tasks": ["search"], "sort": "lowest_cost"}
        )  # tự đứng được thì phải có điều kiện
