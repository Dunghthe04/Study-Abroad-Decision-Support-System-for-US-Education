from datetime import date

import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import SkillOutput
from app.services.intake import CONTINUE_CHAT, FORM_DONE
from app.services.skills import build_handlers, buttons
from app.services.skills.skill_ho_so import (
    NO_MORE_CERT,
    NO_MORE_PLAN,
    OVERDUE_DROP,
    OVERDUE_MOVE,
    OVERDUE_TAKEN,
)
from app.services.skills.validators import AskAgain, check_certification
from tests.fakes import FakeLLM, flow_with, tin
from tests.test_chat_skills_flow import PAGE1_ASKED, field, turn

# Ô nhập chứng chỉ ở E1 / D3: giống thẻ chứng chỉ ở bước 3 form, nhập được nhiều chứng chỉ

GPA = {"gpa": field({"value": 8.5, "scale": 10})}


def certs_of(res) -> list[dict]:
    return res.state.profile["certifications"].value


async def test_english_cert_text_saves_card_and_asks_for_more_without_qwen():
    llm = FakeLLM()
    res = await turn(
        llm,
        "Chứng chỉ IELTS Academic, điểm 6.5, thi tháng 5/2025",
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1},
        profile=GPA,
    )

    assert llm.calls == []
    [cert] = certs_of(res)
    assert cert["type"] == "ielts" and cert["overall"] == 6.5 and cert["testDate"] == "2025-05"
    assert set(cert) == {
        "id",
        "category",
        "type",
        "customName",
        "subject",
        "overall",
        "scaleText",
        "sections",
        "testDate",
        "isTarget",
    }
    assert res.state.profile["english"] == field({"type": "ielts", "value": 6.5, "date": "2025-05"})
    # Vẫn ở E1, hỏi còn chứng chỉ nào nữa, ô nhập vẫn hiện
    assert res.state.question_id == "E1"
    assert [r.label for r in res.quick_replies] == [NO_MORE_CERT]
    assert res.widget is not None and res.widget.type == "select"


async def test_second_cert_is_added_and_latest_english_becomes_main():
    llm = FakeLLM()
    first = await turn(
        llm,
        "Chứng chỉ IELTS Academic, điểm 6.5, thi tháng 5/2025",
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1},
        profile=GPA,
    )
    res = await turn(
        llm,
        "Chứng chỉ TOEFL iBT, điểm 95, thi tháng 3/2026",
        **first.state.model_dump(exclude={"profile"}),
        profile=first.state.profile,
    )

    assert [c["type"] for c in certs_of(res)] == ["ielts", "toefl"]
    assert res.state.profile["english"].value == {"type": "toefl", "value": 95, "date": "2026-03"}


async def test_no_more_cert_moves_to_next_question():
    profile = {**GPA, "english": field({"type": "ielts", "value": 6.5, "date": "2025-05"})}
    res = await turn(
        FakeLLM(),
        NO_MORE_CERT,
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 2},
        profile=profile,
    )

    assert res.state.question_id != "E1"
    assert res.state.current_step == "B2"


async def test_ap_with_subject_answers_d3_without_sat():
    res = await turn(
        FakeLLM(),
        "Chứng chỉ AP, môn Calculus BC, điểm 5, thi tháng 5/2025",
        current_step="B2",
        question_id="D3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1},
        profile=GPA,
    )

    [cert] = certs_of(res)
    assert (cert["category"], cert["type"], cert["subject"], cert["overall"]) == (
        "standardized",
        "ap",
        "Calculus BC",
        5,
    )
    assert "sat" not in res.state.profile


async def test_wrong_sat_score_is_asked_again_and_not_saved():
    res = await turn(
        FakeLLM(),
        "Chứng chỉ SAT, điểm 2000, thi tháng 5/2025",
        current_step="B2",
        question_id="D3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1},
        profile=GPA,
    )

    assert "certifications" not in res.state.profile
    assert "400 đến 1600" in res.answer


async def test_not_tested_english_button_removes_english_cards():
    cards = [
        {"id": "a", "category": "english", "type": "ielts", "overall": 6.5},
        {"id": "b", "category": "standardized", "type": "sat", "overall": 1400},
    ]
    profile = {**GPA, "certifications": field(cards), "sat": field(1400)}
    res = await turn(
        FakeLLM(),
        "Em chưa thi tiếng Anh",
        current_step="B2",
        question_id="E1",
        asked={"D2": 1, "E1": 1},
        profile=profile,
    )

    assert [c["type"] for c in certs_of(res)] == ["sat"]


async def test_english_read_by_qwen_updates_card():
    from tests.test_chat_skills_flow import ho_so

    # Lượt 1: chat chưa có ngày thi -> hỏi ngày thi và lưu cert_pending
    llm1 = FakeLLM(ho_so({"english": {"type": "ielts", "score": 7.0, "date": None}}))
    res1 = await turn(
        llm1, "em được ielts 7.0", current_step="B2", question_id="E1", asked={"D2": 1, "E1": 1}, profile=GPA
    )
    assert "cert_pending" in res1.state.profile
    assert res1.state.profile["cert_pending"].value["score"] == 7.0
    assert "tháng mấy, năm nào" in res1.answer
    assert res1.widget is not None and res1.widget.type == "select"
    assert [f["name"] for f in res1.widget.data["fields"]] == ["test_month", "test_year"]
    assert [r.label for r in res1.quick_replies] == ["Không nhớ ngày thi"]

    # Lượt 2: trả lời ngày thi -> cập nhật thẻ chứng chỉ
    res2 = await turn(FakeLLM(), "10/2024", **res1.state.model_dump())
    [cert] = certs_of(res2)
    assert (cert["category"], cert["type"], cert["overall"], cert["testDate"]) == (
        "english",
        "ielts",
        7.0,
        "2024-10",
    )
    assert "cert_pending" not in res2.state.profile


def test_e1_widget_lists_english_certs_like_form():
    data = buttons.select_fields("E1")
    [type_field] = [f for f in data["fields"] if f["name"] == "type"]
    assert [o["value"] for o in type_field["options"]][:4] == [
        "IELTS Academic",
        "TOEFL iBT",
        "Duolingo English Test",
        "PTE Academic",
    ]
    # Không có AP / A-Level nên không có ô môn và điểm chữ
    assert {f["name"] for f in data["fields"]} == {"type", "score", "test_month", "test_year"}


# validators.check_certification


def test_check_certification_alevel_grade():
    cert = check_certification("alevel", "a*", "2025-06", "Math", today=date(2026, 1, 1))
    assert (cert["overall"], cert["scaleText"]) == (None, "A*")


def test_check_certification_needs_subject_for_ap():
    with pytest.raises(AskAgain):
        check_certification("ap", "5", "2025-05", None, today=date(2026, 1, 1))


def test_check_certification_unknown_type():
    with pytest.raises(AskAgain):
        check_certification("abc", "5", "2025-05", today=date(2026, 1, 1))


def test_check_certification_target_future_date_succeeds():
    cert = check_certification("ielts", "7.5", "2028-06", today=date(2026, 1, 1), is_target=True)
    assert cert["isTarget"] is True
    assert cert["testDate"] == "2028-06"
    assert cert["overall"] == 7.5


def test_check_certification_target_past_date_raises():
    with pytest.raises(AskAgain, match="quá khứ"):
        check_certification("ielts", "7.5", "2024-06", today=date(2026, 1, 1), is_target=True)


def test_check_certification_actual_future_date_raises():
    with pytest.raises(AskAgain, match="tương lai"):
        check_certification("ielts", "7.5", "2028-06", today=date(2026, 1, 1), is_target=False)


async def test_e2_custom_test_type_and_score_saved_without_qwen():
    llm = FakeLLM()
    profile = {"english": field(None, "none"), "english_plan": field({"planned": True, "date": None})}
    res = await turn(
        llm,
        "Em dự kiến thi IELTS, mục tiêu 7.5, tháng 10/2027",
        current_step="B2",
        question_id="E2",
        profile=profile,
    )

    assert llm.calls == []
    assert res.state.profile["english_plan"] == field(
        {"planned": True, "date": "2027-10", "test_type": "ielts", "target_score": 7.5}
    )


async def test_e3_standardized_plan_from_d3_button_and_widget_save():
    llm = FakeLLM()
    # Ở câu D3, bấm nút "Em chưa thi bài nào"
    profile = {
        **GPA,
        "english": field({"type": "ielts", "value": 7.0, "date": "2025-05"}),
        "english_plan": field({"planned": False, "date": None}),
    }
    res = await turn(
        llm,
        "Em chưa thi bài nào",
        current_step="B2",
        question_id="D3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1},
        profile=profile,
    )

    assert res.state.profile["sat"].status == "none"
    assert res.state.profile["act"].status == "none"
    assert "standardized_plan" not in res.state.profile  # chưa thi không còn là "sẽ thi sau"
    assert res.state.question_id == "E3"
    assert res.widget is not None and res.widget.type == "select"

    # Ở câu E3, chọn SAT mục tiêu 1450 tháng 12/2027
    res2 = await turn(
        FakeLLM(),
        "Em dự kiến thi SAT, mục tiêu 1450, tháng 12/2027",
        **res.state.model_dump(exclude={"profile"}),
        profile=res.state.profile,
    )

    assert res2.state.profile["standardized_plan"] == field(
        {"planned": True, "date": "2027-12", "test_type": "sat", "target_score": 1450.0}
    )
    # Dự định là thẻ mục tiêu như form; hỏi còn dự định nào nữa không
    [card] = [c for c in res2.state.profile["certifications"].value if c["isTarget"]]
    assert (card["type"], card["overall"], card["testDate"], card["category"]) == (
        "sat",
        1450.0,
        "2027-12",
        "standardized",
    )
    assert res2.state.question_id == "E3"
    assert [r.label for r in res2.quick_replies] == [NO_MORE_PLAN]

    res3 = await turn(
        FakeLLM(),
        NO_MORE_PLAN,
        **res2.state.model_dump(exclude={"profile"}),
        profile=res2.state.profile,
    )
    assert res3.state.question_id != "E3"


async def test_e3_no_plan_button_ticks_no_plan():
    profile = {
        **GPA,
        "english": field({"type": "ielts", "value": 7.0, "date": "2025-05"}),
        "sat": field(None, "none"),
        "act": field(None, "none"),
        "standardized_plan": field({"planned": True, "date": None}),
    }
    res = await turn(
        FakeLLM(),
        "Em chưa có dự định thi",
        current_step="B2",
        question_id="E3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1, "E3": 1},
        profile=profile,
    )

    assert res.state.profile["standardized_plan"] == field({"planned": False, "date": None})
    assert res.state.question_id != "E3"


async def test_sat_without_date_asks_for_date_and_saves_on_second_turn():
    from tests.test_chat_skills_flow import ho_so

    # Lượt 1: chat "em thi SAT 1450" chưa có ngày thi -> hỏi ngày thi và lưu cert_pending
    llm1 = FakeLLM(ho_so({"sat": 1450}))
    res1 = await turn(
        llm1,
        "em thi SAT 1450",
        current_step="B2",
        question_id="D3",
        asked={"D2": 1, "E1": 1, "D3": 1},
        profile=GPA,
    )
    assert "cert_pending" in res1.state.profile
    assert res1.state.profile["cert_pending"].value["score"] == 1450
    assert "Bạn thi SAT (1450) vào tháng mấy, năm nào" in res1.answer

    # Lượt 2: trả lời ngày thi -> cập nhật thẻ SAT
    res2 = await turn(FakeLLM(), "12/2024", **res1.state.model_dump())
    [cert] = [c for c in certs_of(res2) if c["type"] == "sat"]
    assert (cert["category"], cert["type"], cert["overall"], cert["testDate"]) == (
        "standardized",
        "sat",
        1450,
        "2024-12",
    )
    assert "cert_pending" not in res2.state.profile


async def test_sat_with_date_saves_immediately():
    from tests.test_chat_skills_flow import ho_so

    llm = FakeLLM(ho_so({"sat": 1500}))
    res = await turn(
        llm,
        "em thi SAT 1500 tháng 11/2024",
        current_step="B2",
        question_id="D3",
        asked={"D2": 1, "E1": 1, "D3": 1},
        profile=GPA,
    )
    [cert] = [c for c in certs_of(res) if c["type"] == "sat"]
    assert (cert["category"], cert["type"], cert["overall"], cert["testDate"]) == (
        "standardized",
        "sat",
        1500,
        "2024-11",
    )


async def test_cert_pending_skip_saves_without_date():
    from tests.test_chat_skills_flow import ho_so

    # Lượt 1: chat "IELTS 6.5"
    llm1 = FakeLLM(ho_so({"english": {"type": "ielts", "score": 6.5, "date": None}}))
    res1 = await turn(
        llm1, "IELTS 6.5", current_step="B2", question_id="E1", asked={"D2": 1, "E1": 1}, profile=GPA
    )
    assert "cert_pending" in res1.state.profile

    # Lượt 2: học sinh bảo "em không nhớ" -> lưu điểm và testDate là None
    res2 = await turn(FakeLLM(), "em không nhớ rõ", **res1.state.model_dump())
    [cert] = certs_of(res2)
    assert (cert["category"], cert["type"], cert["overall"], cert["testDate"]) == (
        "english",
        "ielts",
        6.5,
        None,
    )
    assert "cert_pending" not in res2.state.profile


# --- Dự định thi là thẻ isTarget như form ---

TAKEN_IELTS = {
    "id": "t1",
    "category": "english",
    "type": "ielts",
    "overall": 6.5,
    "testDate": "2025-05",
    "isTarget": False,
}
TAKEN_SAT = {
    "id": "t2",
    "category": "standardized",
    "type": "sat",
    "overall": 1400,
    "testDate": "2025-06",
    "isTarget": False,
}
OVERDUE_IELTS = {
    "id": "p1",
    "category": "english",
    "type": "ielts",
    "overall": 7.5,
    "testDate": "2025-01",
    "isTarget": True,
}


def plan_profile(*cards):
    return {
        **GPA,
        "english": field({"type": "ielts", "value": 6.5, "date": "2025-05"}),
        "sat": field(1400),
        "certifications": field([dict(c) for c in cards]),
    }


async def test_e2_no_plan_button_drops_plan_cards_and_ticks():
    plan = {
        "id": "p2",
        "category": "english",
        "type": "toefl",
        "overall": 100,
        "testDate": "2099-05",
        "isTarget": True,
    }
    res = await turn(
        FakeLLM(),
        "Em chưa có dự định thi",
        current_step="B2",
        question_id="E2",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "E2": 1},
        profile=plan_profile(TAKEN_IELTS, TAKEN_SAT, plan),
    )

    assert [c["id"] for c in certs_of(res)] == ["t1", "t2"]
    assert res.state.profile["english_plan"] == field({"planned": False, "date": None})
    assert res.state.question_id != "E2"


async def test_e2_target_must_beat_taken_score():
    llm = FakeLLM()
    res = await turn(
        llm,
        "Em dự kiến thi IELTS, mục tiêu 6.0, tháng 5/2099",
        current_step="B2",
        question_id="E2",
        profile=plan_profile(TAKEN_IELTS),
    )

    assert llm.calls == []
    assert res.state.question_id == "E2"
    assert "cao hơn" in res.answer
    assert [c["id"] for c in certs_of(res)] == ["t1"]


async def test_e2_several_plans_and_same_test_updates_card():
    res = await turn(
        FakeLLM(),
        "Em dự kiến thi IELTS, mục tiêu 7.5, tháng 5/2099",
        current_step="B2",
        question_id="E2",
        profile=plan_profile(TAKEN_IELTS),
    )
    res = await turn(FakeLLM(), "Em dự kiến thi TOEFL iBT, tháng 6/2099", **res.state.model_dump())
    # Cùng bài: sửa thẻ cũ (giữ id, giữ điểm mục tiêu khi không nói lại)
    first_id = next(c["id"] for c in certs_of(res) if c["isTarget"] and c["type"] == "ielts")
    res = await turn(FakeLLM(), "Em dự kiến thi IELTS, tháng 8/2099", **res.state.model_dump())

    plans = [(c["type"], c["overall"], c["testDate"]) for c in certs_of(res) if c["isTarget"]]
    assert plans == [("ielts", 7.5, "2099-08"), ("toefl", None, "2099-06")]
    assert next(c["id"] for c in certs_of(res) if c["isTarget"] and c["type"] == "ielts") == first_id
    # english_plan là tóm tắt dự định đầu tiên
    assert res.state.profile["english_plan"] == field(
        {"planned": True, "date": "2099-08", "test_type": "ielts", "target_score": 7.5}
    )


async def overdue_turn():
    return await turn(
        FakeLLM(),
        NO_MORE_CERT,
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1},
        profile=plan_profile(TAKEN_IELTS, TAKEN_SAT, OVERDUE_IELTS),
    )


async def test_overdue_plan_is_asked_with_three_buttons():
    res = await overdue_turn()

    assert res.state.question_id == "E2"
    assert "IELTS" in res.answer and "01/2025" in res.answer
    assert [r.label for r in res.quick_replies] == [OVERDUE_TAKEN, OVERDUE_MOVE, OVERDUE_DROP]
    assert res.state.profile["plan_overdue"].value == {"id": "p1"}


async def test_overdue_drop_removes_card():
    res = await overdue_turn()
    llm = FakeLLM()
    res = await turn(llm, OVERDUE_DROP, **res.state.model_dump())

    assert llm.calls == []
    assert [c["id"] for c in certs_of(res)] == ["t1", "t2"]
    assert "plan_overdue" not in res.state.profile
    assert "english_plan" not in res.state.profile


async def test_overdue_move_month_keeps_card_and_target():
    res = await overdue_turn()
    res = await turn(FakeLLM(), OVERDUE_MOVE, **res.state.model_dump())
    assert res.state.question_id == "E2"
    assert res.widget is not None

    res = await turn(FakeLLM(), "Em dự kiến thi IELTS, tháng 3/2099", **res.state.model_dump())
    [card] = [c for c in certs_of(res) if c["isTarget"]]
    assert (card["id"], card["overall"], card["testDate"]) == ("p1", 7.5, "2099-03")
    assert "plan_overdue" not in res.state.profile
    assert res.state.question_id != "E2"


async def test_overdue_taken_turns_card_into_real_score():
    res = await overdue_turn()
    res = await turn(FakeLLM(), OVERDUE_TAKEN, **res.state.model_dump())
    assert res.state.question_id == "E1"

    res = await turn(
        FakeLLM(), "Chứng chỉ IELTS Academic, điểm 7.0, thi tháng 1/2025", **res.state.model_dump()
    )
    card = next(c for c in certs_of(res) if c["id"] == "p1")
    assert (card["isTarget"], card["overall"], card["testDate"]) == (False, 7.0, "2025-01")
    assert not any(c["isTarget"] for c in certs_of(res))
    assert "plan_overdue" not in res.state.profile
    assert "english_plan" not in res.state.profile


# --- Hồ sơ đã đủ mà còn dự định quá hạn: về từ form hoặc hỏi tư vấn thẳng vẫn hỏi dự định trước ---


class AdviceSkill:
    """Skill tư vấn giả: ghi lại câu hỏi được trả lời."""

    def __init__(self) -> None:
        self.questions: list[str] = []

    async def run(self, state, message, entities=None):
        self.questions.append(message)
        return SkillOutput("lời tư vấn")


def ready_profile(*cards):
    return {
        **plan_profile(*cards),
        "current_level": field("high_school"),
        "target_level": field("undergraduate"),
        "budget": field("tier_35k_50k"),
        "scholarship": field("preferred"),
    }


def advice_flow(slm=None):
    advice = AdviceSkill()
    handlers = build_handlers(FakeLLM(), "qwen3:8b") | {"skill_danh_gia": advice}
    return flow_with(handlers, slm), advice


async def say(flow, message, state, from_form=False):
    req = ChatRequest(messages=[ChatMessage(role="user", content=message)], state=state, from_form=from_form)
    return await flow.reply(req)


def waiting_advice():
    return tin(("profile_review", {}, "đánh giá hồ sơ giúp em")).intents[0]


async def test_back_from_form_with_overdue_plan_asks_it_instead_of_done():
    flow, advice = advice_flow()
    state = ChatState(current_step="B3", profile=ready_profile(TAKEN_IELTS, TAKEN_SAT, OVERDUE_IELTS))

    res = await say(flow, CONTINUE_CHAT, state, from_form=True)

    assert res.answer != FORM_DONE and "IELTS" in res.answer
    assert [r.label for r in res.quick_replies] == [OVERDUE_TAKEN, OVERDUE_MOVE, OVERDUE_DROP]
    assert res.state.question_id == "E2" and res.state.intake_mode == "chat"
    assert advice.questions == []


async def test_back_from_form_with_overdue_plan_and_waiting_advice_answers_after_review():
    flow, advice = advice_flow()
    state = ChatState(
        current_step="B3",
        profile=ready_profile(TAKEN_IELTS, TAKEN_SAT, OVERDUE_IELTS),
        pending_advice=waiting_advice(),
    )

    res = await say(flow, CONTINUE_CHAT, state, from_form=True)
    assert [r.label for r in res.quick_replies] == [OVERDUE_TAKEN, OVERDUE_MOVE, OVERDUE_DROP]
    assert res.state.pending_advice.span == "đánh giá hồ sơ giúp em"

    res = await say(flow, OVERDUE_DROP, res.state)
    assert res.state.question_id == "X1" and res.widget.type == "review"
    assert [c["id"] for c in certs_of(res)] == ["t1", "t2"]

    res = await say(flow, "Đúng rồi", res.state)
    assert advice.questions == ["đánh giá hồ sơ giúp em"]
    assert res.state.pending_advice is None


async def test_direct_advice_with_overdue_plan_asks_it_first():
    flow, advice = advice_flow(tin(("profile_review", {}, "đánh giá hồ sơ giúp em")))
    state = ChatState(current_step="B3", profile=ready_profile(TAKEN_IELTS, TAKEN_SAT, OVERDUE_IELTS))

    res = await say(flow, "đánh giá hồ sơ giúp em", state)
    assert advice.questions == []
    assert [r.label for r in res.quick_replies] == [OVERDUE_TAKEN, OVERDUE_MOVE, OVERDUE_DROP]
    assert res.state.pending_advice.span == "đánh giá hồ sơ giúp em"

    res = await say(flow, OVERDUE_TAKEN, res.state)
    assert res.state.question_id == "E1"
    res = await say(flow, "Chứng chỉ IELTS Academic, điểm 7.0, thi tháng 1/2025", res.state)
    assert not any(c["isTarget"] for c in certs_of(res))


@pytest.mark.parametrize("plan_month", [None, "this_month"])
async def test_advice_without_overdue_plan_runs_as_before(plan_month):
    flow, advice = advice_flow(tin(("profile_review", {}, "đánh giá hồ sơ giúp em")))
    cards = [TAKEN_IELTS, TAKEN_SAT]
    if plan_month:
        cards.append({**OVERDUE_IELTS, "testDate": date.today().strftime("%Y-%m")})
    state = ChatState(current_step="B3", profile=ready_profile(*cards))

    res = await say(flow, "đánh giá hồ sơ giúp em", state)

    assert advice.questions == ["đánh giá hồ sơ giúp em"] and res.answer == "lời tư vấn"


async def test_back_from_form_without_overdue_plan_still_says_done():
    flow, _ = advice_flow()
    state = ChatState(current_step="B3", profile=ready_profile(TAKEN_IELTS, TAKEN_SAT))

    res = await say(flow, CONTINUE_CHAT, state, from_form=True)

    assert res.answer == FORM_DONE
