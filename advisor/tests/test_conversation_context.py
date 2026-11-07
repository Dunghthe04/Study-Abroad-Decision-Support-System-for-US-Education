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
    return SlmTinResult(cac_y=[SlmY(type="kien_thuc", doan=message, thuc_the=entities)])


def entities_of(slm: SlmTinResult) -> dict:
    return slm.cac_y[0].thuc_the


# ---- model nói tiếp nối: điền phần câu còn thiếu từ ngữ cảnh
def test_the_model_says_follow_up_and_the_school_comes_from_the_context():
    out = contextualize(
        state(schools=["Harvard"], fields=["chi_phi"]),
        "còn học bổng?",
        knowledge("còn học bổng?", tiep_noi=True, muc=["hoc_bong"]),
    )

    assert entities_of(out)["truong"] == "Harvard"
    assert entities_of(out)["muc"] == ["hoc_bong"]  # mục model đã trả không bị đè


def test_the_model_says_follow_up_with_only_a_new_school_so_the_field_is_kept():
    out = contextualize(
        state(schools=["Harvard"], fields=["hoc_bong"]),
        "còn Yale?",
        knowledge("còn Yale?", tiep_noi=True, truong="Yale"),
    )

    assert entities_of(out)["truong"] == "Yale"
    assert entities_of(out)["muc"] == ["hoc_bong"]
    assert entities_of(out)["nhiem_vu"] == "tra_cuu"


def test_the_model_says_it_stands_alone_so_nothing_is_filled():
    out = contextualize(
        state(schools=["Harvard"]), "học bổng là gì", knowledge("học bổng là gì", tiep_noi=False)
    )

    assert "truong" not in entities_of(out)


def test_two_schools_in_the_context_become_a_comparison():
    out = contextualize(
        state(schools=["BU", "NYU"], fields=["chi_phi"]),
        "còn học bổng?",
        knowledge("còn học bổng?", tiep_noi=True),
    )

    assert entities_of(out)["cac_truong"] == ["BU", "NYU"]
    assert entities_of(out)["nhiem_vu"] == "so_sanh"


def test_a_follow_up_search_adds_the_new_condition_to_the_old_filters():
    ctx = state(bo_loc={"bang": ["California"], "hoc_bong": "co"}, sort="xep_hang")
    out = contextualize(
        ctx,
        "chỉ lấy công lập thôi",
        knowledge(
            "chỉ lấy công lập thôi", tiep_noi=True, nhiem_vu="tim_kiem", bo_loc={"loai_truong": "cong_lap"}
        ),
    )

    assert entities_of(out)["bo_loc"] == {"bang": ["California"], "hoc_bong": "co", "loai_truong": "cong_lap"}
    assert entities_of(out)["sap_xep"] == "xep_hang"


def test_a_new_condition_of_the_same_kind_replaces_the_old_one():
    ctx = state(bo_loc={"bang": ["California"]})
    out = contextualize(
        ctx, "ở Texas", knowledge("ở Texas", tiep_noi=True, nhiem_vu="tim_kiem", bo_loc={"bang": ["Texas"]})
    )

    assert entities_of(out)["bo_loc"] == {"bang": ["Texas"]}


# ---- quy tắc dự phòng (model chưa trả tiep_noi)
@pytest.mark.parametrize(
    "message",
    ["còn học bổng thì sao", "hạn nộp thì sao nhỉ", "GPA bao nhiêu", "trường này xếp hạng bao nhiêu"],
)
def test_without_the_model_a_missing_school_is_filled_when_the_sentence_asks_a_field_or_points_back(message):
    out = contextualize(state(schools=["Harvard"]), message, knowledge(message))

    assert entities_of(out)["truong"] == "Harvard"


@pytest.mark.parametrize(
    "message", ["học bổng là gì", "I-20 là gì", "xin chào", "visa F-1 nộp thế nào", "IELTS khác gì TOEFL"]
)
def test_without_the_model_general_questions_are_never_tied_to_the_remembered_school(message):
    out = contextualize(state(schools=["Harvard"]), message, knowledge(message))

    assert "truong" not in entities_of(out)


def test_without_the_model_a_short_follow_up_with_a_school_keeps_the_field():
    out = contextualize(
        state(schools=["Harvard"], fields=["hoc_bong"]), "còn Yale?", knowledge("còn Yale?", truong="Yale")
    )

    assert entities_of(out)["muc"] == ["hoc_bong"]


def test_without_the_model_a_long_sentence_with_a_school_is_not_treated_as_a_follow_up():
    message = "Yale nằm ở bang nào và có đẹp không vậy bạn"
    out = contextualize(
        state(schools=["Harvard"], fields=["hoc_bong"]), message, knowledge(message, truong="Yale")
    )

    assert "muc" not in entities_of(out)


def test_nothing_changes_without_a_context_or_for_other_intents():
    slm = knowledge("còn học bổng?")
    assert contextualize(ChatState(), "còn học bổng?", slm) is slm
    other = SlmTinResult(cac_y=[SlmY(type="ngoai_pham_vi", doan="x", thuc_the={"loai_con": "tam_biet"})])
    assert contextualize(state(schools=["Harvard"]), "tạm biệt", other) is other


# ---- ghi nhớ sau mỗi câu trả lời
def test_a_school_answer_remembers_the_school_and_the_fields():
    s = ChatState()
    update_context(
        s, SkillOutput("x", action="school_info", action_data={"school": "Harvard", "fields": ["chi_phi"]})
    )

    assert (s.context.schools, s.context.fields) == (["Harvard"], ["chi_phi"])


def test_a_comparison_remembers_all_the_schools():
    s = ChatState()
    update_context(
        s,
        SkillOutput(
            "x", action="school_compare", action_data={"schools": ["BU", "NYU"], "fields": ["chi_phi"]}
        ),
    )

    assert s.context.schools == ["BU", "NYU"]


def test_a_search_remembers_the_filters_and_forgets_the_school_because_none_was_chosen():
    s = state(schools=["Harvard"])
    update_context(
        s,
        SkillOutput(
            "x",
            action="school_search",
            action_data={"bo_loc": {"bang": ["Texas"]}, "sap_xep": "chi_phi_thap"},
        ),
    )

    assert (s.context.bo_loc, s.context.sort, s.context.schools) == ({"bang": ["Texas"]}, "chi_phi_thap", [])


def test_other_actions_leave_the_context_alone():
    s = state(schools=["Harvard"])
    update_context(s, SkillOutput("x", action="simulate", action_data={"changes": {"sat": 1500}}))
    update_context(s, SkillOutput("x"))

    assert s.context.schools == ["Harvard"]


# ---- đầu vào của model và làm sạch đầu ra
def test_the_model_input_carries_the_context_only_when_there_is_one():
    assert "dang_noi_ve" not in slm_input("học phí Harvard", None, "")
    ctx = context_for_model(state(schools=["Harvard"], fields=["chi_phi"]))
    assert slm_input("còn học bổng?", None, "", ctx)["dang_noi_ve"] == {
        "truong": ["Harvard"],
        "muc": ["chi_phi"],
    }
    assert "dang_noi_ve" in build_messages("prompt", "còn học bổng?", None, "", ctx)[1]["content"]
    assert context_for_model(ChatState()) is None


def test_a_school_carried_from_the_context_survives_the_verbatim_cleaning_only_when_the_model_says_follow_up():
    raw = {
        "cac_y": [
            {
                "type": "kien_thuc",
                "doan": "còn học bổng?",
                "thuc_the": {"truong": "Harvard", "tiep_noi": True},
            }
        ]
    }
    stranger = {"cac_y": [{"type": "kien_thuc", "doan": "còn học bổng?", "thuc_the": {"truong": "Harvard"}}]}

    kept = keep_verbatim(raw, "còn học bổng?", {"harvard"})
    dropped = keep_verbatim(stranger, "còn học bổng?", {"harvard"})
    invented = keep_verbatim(raw, "còn học bổng?", {"yale"})

    assert kept["cac_y"][0]["thuc_the"]["truong"] == "Harvard"
    assert (
        "truong" not in dropped["cac_y"][0]["thuc_the"]
    )  # chép tên từ ngữ cảnh mà không nói tiếp nối: bỏ như trước
    assert "truong" not in invented["cac_y"][0]["thuc_the"]  # tên không có trong ngữ cảnh: bỏ


def test_tiep_noi_must_be_a_boolean():
    check_entities("kien_thuc", {"tiep_noi": True})
    check_entities("kich_ban", {"che_do": "goal_seeking", "tiep_noi": False})
    with pytest.raises(ValueError):
        check_entities("kien_thuc", {"tiep_noi": "co"})


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
            ("kien_thuc", {"truong": "Harvard", "nhiem_vu": "tra_cuu", "muc": ["chi_phi"]}, "học phí Harvard")
        ),
    )
    r1 = await first.reply(
        ChatRequest(
            messages=[ChatMessage(role="user", content="học phí Harvard")], state=ChatState(current_step="B4")
        )
    )

    assert r1.action == "school_info"
    assert r1.state.context.schools == ["Harvard"] and r1.state.context.fields == ["chi_phi"]

    # phiên sau (trạng thái đi qua JSON như khi .NET lưu và gửi lại), model không trả trường
    saved = r1.state.model_dump(by_alias=True)
    second = flow_with({"skill_tra_loi": _Kb()}, tin(("kien_thuc", {}, "còn học bổng thì sao")))
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

    flow = flow_with({"skill_tra_loi": _Kb()}, tin(("kien_thuc", {}, "còn học bổng thì sao")))
    res = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content="còn học bổng thì sao")]))

    assert res.action_data["school"] is None


def test_a_follow_up_search_may_only_change_the_sort_order():
    check_entities("kien_thuc", {"nhiem_vu": "tim_kiem", "tiep_noi": True, "sap_xep": "chi_phi_thap"})
    with pytest.raises(ValueError):
        check_entities(
            "kien_thuc", {"nhiem_vu": "tim_kiem", "tiep_noi": True}
        )  # không điều kiện mới cũng không đổi cách xếp
    with pytest.raises(ValueError):
        check_entities(
            "kien_thuc", {"nhiem_vu": "tim_kiem", "sap_xep": "chi_phi_thap"}
        )  # tự đứng được thì phải có điều kiện
