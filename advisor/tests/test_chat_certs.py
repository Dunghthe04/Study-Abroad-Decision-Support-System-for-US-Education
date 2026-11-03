from datetime import date

import pytest

from app.services.skills import buttons
from app.services.skills.skill_ho_so import NO_MORE_CERT
from app.services.skills.validators import AskAgain, check_certification
from tests.fakes import FakeLLM
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

    llm = FakeLLM(ho_so({"english": {"type": "ielts", "score": 7.0, "date": None}}))
    res = await turn(
        llm, "em được ielts 7.0", current_step="B2", question_id="E1", asked={"D2": 1, "E1": 1}, profile=GPA
    )

    [cert] = certs_of(res)
    assert (cert["category"], cert["type"], cert["overall"]) == ("english", "ielts", 7.0)


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
    # Ở câu D3, bấm nút "Em chưa thi SAT/ACT"
    profile = {**GPA, "english": field({"type": "ielts", "value": 7.0, "date": "2025-05"})}
    res = await turn(
        llm,
        "Em chưa thi SAT/ACT",
        current_step="B2",
        question_id="D3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1},
        profile=profile,
    )

    assert res.state.profile["sat"].status == "none"
    assert res.state.profile["act"].status == "none"
    assert res.state.profile["standardized_plan"] == field({"planned": True, "date": None})
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
    assert res2.state.question_id != "E3"


async def test_e3_skip_keeps_standardized_plan_tick():
    profile = {
        **GPA,
        "english": field({"type": "ielts", "value": 7.0, "date": "2025-05"}),
        "sat": field(None, "none"),
        "act": field(None, "none"),
        "standardized_plan": field({"planned": True, "date": None}),
    }
    res = await turn(
        FakeLLM(),
        "Bỏ qua",
        current_step="B2",
        question_id="E3",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "D3": 1, "E3": 1},
        profile=profile,
    )

    assert res.state.profile["standardized_plan"] == field({"planned": True, "date": None})
    assert res.state.question_id != "E3"

