"""B1 đến B3: skill_nhu_cau, skill_bac_hoc, skill_ho_so chạy trong ChatFlow (chưa có model: dùng skill của bước)."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import NOT_READY, QUESTIONS, ChatFlow
from app.services.skills import build_handlers
from app.services.skills.skill_ho_so import OPTIONAL_QUESTIONS, SAVED
from tests.fakes import FakeLLM


async def turn(llm: FakeLLM, message: str, **state_fields):
    flow = ChatFlow(handlers=build_handlers(llm, "qwen3:8b"))
    req = ChatRequest(messages=[ChatMessage(role="user", content=message)], state=ChatState(**state_fields))
    return await flow.reply(req)


def field(value, status: str = "confirmed") -> ProfileField:
    return ProfileField(value=value, status=status)


def nothing_bac_hoc() -> dict:
    return {"current_level": None, "current_grade": None, "target_level": None, "major": None, "intake": None}


def ho_so(values: dict | None = None, none_fields: list | None = None) -> dict:
    return {"values": values or {}, "none_fields": none_fields or []}


# B1: skill_nhu_cau


async def test_first_message_saves_said_info_and_skips_known_questions():
    llm = FakeLLM(
        {
            "need": "profile_advice",
            "branch": "DH",
            "info_given": {
                "current_level": "lớp 12",
                "target_level": "đại học",
                "gpa_text": "8.5",
                "english_text": "IELTS 6.5",
            },
        },
        {
            **nothing_bac_hoc(),
            "current_level": "high_school",
            "current_grade": "Lớp 12",
            "target_level": "bachelor",
        },
        ho_so(
            {"gpa": {"value": 8.5, "scale": None}, "english": {"type": "ielts", "score": 6.5, "date": None}}
        ),
    )
    res = await turn(llm, "Em lớp 12, muốn học đại học ở Mỹ, GPA 8.5, IELTS 6.5")

    assert (res.state.need, res.state.branch, res.state.current_step) == ("profile_advice", "DH", "B1")
    # B1.1 và B1.2 đã nói nên hỏi thẳng B1.3
    assert res.state.question_id == "B1.3"
    assert res.answer == QUESTIONS["B1.3"]
    profile = res.state.profile
    assert profile["gpa"] == field({"value": 8.5, "scale": 10}, "said_in_B0")
    assert profile["english"] == field({"type": "ielts", "value": 6.5, "date": None}, "said_in_B0")
    assert profile["target_level"] == field("undergraduate", "said_in_B0")
    assert profile["current_grade"] == field("Lớp 12", "said_in_B0")
    assert {"gpa", "english", "target_level", "current_level"} <= set(res.profile_changed)


async def test_first_message_without_info_only_calls_qwen_once():
    llm = FakeLLM({"need": "profile_advice", "branch": "DH", "info_given": {}})
    res = await turn(llm, "Em muốn tìm trường ở Mỹ")

    assert len(llm.calls) == 1
    assert res.state.question_id == "B1.1"
    assert res.state.profile == {}


async def test_said_info_does_not_overwrite_the_draft():
    llm = FakeLLM(
        {"need": "profile_advice", "branch": "DH", "info_given": {"gpa_text": "8.5"}},
        ho_so({"gpa": {"value": 8.5, "scale": None}}),
    )
    res = await turn(llm, "GPA em 8.5", profile={"gpa": field({"value": 3.6, "scale": 4})})

    assert res.state.profile["gpa"] == field({"value": 3.6, "scale": 4})


async def test_unclear_asks_b0_2_with_buttons_then_goes_on_after_two_tries():
    res = await turn(FakeLLM({"need": "unclear", "branch": None, "info_given": {}}), "chào bạn")

    assert (res.state.current_step, res.state.question_id) == ("B0", "B0.2")
    assert [r.label for r in res.quick_replies] == ["Tìm trường theo hồ sơ", "Em có câu hỏi"]

    res = await turn(
        FakeLLM({"need": "unclear", "branch": None, "info_given": {}}),
        "ừm",
        asked={"B0.1": 1, "B0.2": 2},
        question_id="B0.2",
    )
    assert (res.state.current_step, res.state.question_id) == ("B1", "B1.1")


async def test_knowledge_question_moves_to_b5_and_remembers_to_come_back():
    res = await turn(FakeLLM({"need": "knowledge", "branch": None, "info_given": {}}), "Visa cần gì ạ?")

    assert (res.state.current_step, res.state.return_to) == ("B5", "B1")
    assert res.answer == NOT_READY


# B2: skill_bac_hoc


async def test_unknown_major_is_saved_as_unknown_and_next_question_is_asked():
    llm = FakeLLM({**nothing_bac_hoc(), "major": "unknown"})
    profile = {"current_level": field("high_school"), "target_level": field("undergraduate")}
    res = await turn(
        llm, "em chưa biết ngành", current_step="B1", question_id="B1.3", asked={"B1.3": 1}, profile=profile
    )

    assert res.state.profile["major"] == field(None, "unknown")
    assert res.state.question_id == "B1.4"


async def test_no_answer_is_asked_again_once():
    res = await turn(
        FakeLLM(nothing_bac_hoc()), "dạ", current_step="B1", question_id="B1.1", asked={"B1.1": 1}
    )

    assert (res.state.question_id, res.state.asked["B1.1"]) == ("B1.1", 2)


async def test_after_two_tries_and_last_question_moves_to_b2():
    profile = {"current_level": field("high_school"), "target_level": field("undergraduate")}
    res = await turn(
        FakeLLM(nothing_bac_hoc()),
        "dạ",
        current_step="B1",
        question_id="B1.4",
        asked={"B1.1": 1, "B1.2": 1, "B1.3": 2, "B1.4": 2},
        profile={**profile, "gpa": field({"value": 8.5, "scale": 10}, "said_in_B0")},
    )

    # Hết câu B1 nên sang B2; GPA đã nói ở B0 nên bỏ qua D2, hỏi E1
    assert (res.state.current_step, res.state.question_id) == ("B2", "E1")
    assert res.answer == QUESTIONS["E1"]


# B3: skill_ho_so


async def test_wrong_gpa_is_not_saved_and_asked_again():
    llm = FakeLLM(ho_so({"gpa": {"value": 85, "scale": None}}))
    res = await turn(llm, "GPA em 85", current_step="B2", question_id="D2", asked={"D2": 1})

    assert "gpa" not in res.state.profile
    assert res.state.question_id == "D2"
    assert res.state.asked["D2"] == 2
    assert res.answer != QUESTIONS["D2"]  # câu hỏi lại của validators


async def test_right_answer_moves_to_next_required_question():
    llm = FakeLLM(ho_so({"english": {"type": "ielts", "score": 6.5, "date": "2026-05"}}))
    res = await turn(
        llm,
        "IELTS 6.5 thi tháng 5",
        current_step="B2",
        question_id="E1",
        asked={"E1": 1},
        profile={"gpa": field({"value": 8.5, "scale": 10})},
    )

    assert res.state.profile["english"] == field({"type": "ielts", "value": 6.5, "date": "2026-05"})
    assert res.state.question_id == "F1"


async def test_budget_in_vnd_is_saved_as_tier():
    llm = FakeLLM(ho_so({"budget": {"amount": 800_000_000, "currency": "VND", "tier": None}}))
    res = await turn(llm, "800 triệu", current_step="B2", question_id="F1", asked={"F1": 1})

    assert res.state.profile["budget"] == field("tier_20k_35k")


async def test_no_english_asks_test_plan():
    llm = FakeLLM(ho_so(none_fields=["english"]))
    res = await turn(
        llm,
        "em chưa thi",
        current_step="B2",
        question_id="E1",
        asked={"D2": 1, "E1": 1, "F1": 1, "F2": 1},
        profile={"gpa": field({"value": 8.5, "scale": 10}), "budget": field("tier_20k_35k")},
    )

    assert res.state.profile["english"] == field(None, "none")
    assert res.state.question_id == "E2"


async def test_skipped_optional_question_is_not_asked_again():
    profile = {
        "gpa": field({"value": 8.5, "scale": 10}),
        "english": field({"type": "ielts", "value": 6.5, "date": None}),
        "budget": field("tier_20k_35k"),
    }
    res = await turn(
        FakeLLM(ho_so()), "để sau ạ", current_step="B2", question_id="F2", asked={"F2": 1}, profile=profile
    )

    assert "scholarship" not in res.state.profile
    assert res.state.question_id == "D1"  # E2 bỏ qua vì đã có chứng chỉ


def full_profile() -> dict:
    return {
        "target_level": field("undergraduate", "said_in_B0"),
        "gpa": field({"value": 8.5, "scale": 10}, "said_in_B0"),
        "english": field({"type": "ielts", "value": 6.5, "date": None}),
    }


async def test_all_questions_done_shows_review_with_buttons():
    asked = {q: 1 for q in OPTIONAL_QUESTIONS}
    llm = FakeLLM(ho_so({"budget": {"amount": None, "currency": None, "tier": "tier_35k_50k"}}))
    res = await turn(
        llm, "tier_35k_50k", current_step="B2", question_id="F1", asked=asked, profile=full_profile()
    )

    assert res.state.question_id == "X1"
    assert res.widget.type == "review"
    assert res.widget.data["profile"]["budget"] == {"value": "tier_35k_50k", "status": "confirmed"}
    assert res.widget.data["missing"] == ["target_level", "gpa"]  # said_in_B0 chưa xác nhận
    assert [r.label for r in res.quick_replies] == ["Đúng rồi", "Cần sửa"]


async def test_confirm_at_review_confirms_said_info_and_moves_to_b3():
    llm = FakeLLM()
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    res = await turn(llm, "Đúng rồi", current_step="B2", question_id="X1", profile=profile)

    assert llm.calls == []  # xác nhận không cần gọi Qwen
    assert all(f.status == "confirmed" for f in res.state.profile.values())
    assert (res.state.current_step, res.state.question_id) == ("B3", "X3")
    assert res.answer.startswith(SAVED)
    assert "gpa" in res.profile_changed


async def test_confirm_with_missing_budget_asks_budget():
    res = await turn(FakeLLM(), "Đúng", current_step="B2", question_id="X1", profile=full_profile())

    assert (res.state.current_step, res.state.question_id) == ("B2", "F1")


async def test_fix_at_review_overwrites_and_shows_review_again():
    llm = FakeLLM(ho_so({"gpa": {"value": 8.7, "scale": 10}}))
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    res = await turn(llm, "GPA em 8.7 chứ", current_step="B2", question_id="X1", profile=profile)

    assert res.state.profile["gpa"] == field({"value": 8.7, "scale": 10})
    assert res.state.question_id == "X1"
    assert res.widget.type == "review"


# B5: nút gợi ý và widget


async def test_budget_question_comes_with_budget_widget():
    llm = FakeLLM(ho_so({"english": {"type": "ielts", "score": 6.5, "date": None}}))
    res = await turn(
        llm,
        "IELTS 6.5",
        current_step="B2",
        question_id="E1",
        profile={"gpa": field({"value": 8.5, "scale": 10})},
    )

    assert res.state.question_id == "F1"
    assert res.widget.type == "budget"
    assert res.widget.data["options"][0] == {"value": "tier_under_20k", "label": "Dưới 20.000 USD"}
    assert "Bỏ qua" not in [r.label for r in res.quick_replies]  # F1 bắt buộc nên không có nút bỏ qua


async def test_pressing_budget_button_saves_tier_without_qwen():
    llm = FakeLLM()
    res = await turn(llm, "Dưới 20.000 USD", current_step="B2", question_id="F1", profile=full_profile())

    assert llm.calls == []
    assert res.state.profile["budget"] == field("tier_under_20k")
    assert res.state.question_id == "F2"
    assert res.widget.type == "scholarship"
    assert [r.label for r in res.quick_replies][-1] == "Bỏ qua"


async def test_skip_button_moves_on_without_saving():
    llm = FakeLLM()
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    res = await turn(llm, "Bỏ qua", current_step="B2", question_id="F2", asked={"F2": 1}, profile=profile)

    assert llm.calls == []
    assert "scholarship" not in res.state.profile
    assert res.state.question_id == "D1"


async def test_target_level_button_in_b1():
    llm = FakeLLM()
    res = await turn(
        llm,
        "Thạc sĩ",
        current_step="B1",
        question_id="B1.2",
        profile={"current_level": field("college_university")},
    )

    assert llm.calls == []
    assert res.state.profile["target_level"] == field("master")
    assert res.state.question_id == "B1.3"
    assert [r.label for r in res.quick_replies] == ["Em chưa rõ ngành"]


# B4: hoạt động thiếu ý thì hỏi tiếp


def at_d4() -> dict:
    asked = {"F2": 1, "D1": 1, "D3": 1, "D4": 1}
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    return {"current_step": "B2", "question_id": "D4", "asked": asked, "profile": profile}


ENGLISH_CLUB = {
    "name": "CLB Tiếng Anh",
    "impact_level": "school",
    "start_date": "01/2024",
    "is_ongoing": True,
}


async def test_activity_without_role_is_kept_and_role_asked_then_saved():
    res = await turn(
        FakeLLM(ho_so({"activities": [ENGLISH_CLUB]})), "em ở CLB Tiếng Anh từ 01/2024", **at_d4()
    )

    assert "activities" not in res.state.profile
    assert res.state.profile["activity_pending"].value[0]["name"] == "CLB Tiếng Anh"
    assert res.state.question_id == "D4"
    assert "sáng lập" in res.answer

    llm = FakeLLM(ho_so({"activities": [{"name": "CLB Tiếng Anh", "role": "member"}]}))
    res = await turn(llm, "em là thành viên", **res.state.model_dump())

    assert "activity_pending" not in res.state.profile
    assert res.state.profile["activities"].value == [
        {
            "name": "CLB Tiếng Anh",
            "category": "activity",
            "role": "member",
            "impactLevel": "school",
            "startDate": "01/2024",
            "endDate": None,
            "isOngoing": True,
        }
    ]
    assert res.state.question_id == "D5"


async def test_answer_without_new_info_drops_the_unfinished_activity():
    state = at_d4()
    state["profile"]["activity_pending"] = field([ENGLISH_CLUB], "unknown")
    res = await turn(FakeLLM(ho_so()), "dạ em không nhớ", **state)

    assert "activity_pending" not in res.state.profile
    assert "activities" not in res.state.profile
    assert res.state.question_id == "D5"


async def test_unfinished_activity_from_b0_is_asked_at_d4_and_hidden_in_review():
    state = at_d4()
    state["asked"].pop("D4")
    state["question_id"] = "D3"
    state["profile"]["activity_pending"] = field([ENGLISH_CLUB], "unknown")
    res = await turn(FakeLLM(ho_so(none_fields=["sat", "act"])), "em chưa thi", **state)

    assert res.state.question_id == "D4"
    assert "CLB Tiếng Anh" in res.answer and "sáng lập" in res.answer

    # Bấm "Em chưa có hoạt động" thì bỏ mục dở; tới bảng rà soát không còn khóa *_pending
    asked = {q: 1 for q in OPTIONAL_QUESTIONS}
    res = await turn(FakeLLM(), "Em chưa có hoạt động", **{**res.state.model_dump(), "asked": asked})

    assert res.state.profile["activities"] == field(None, "none")
    assert res.widget.type == "review"
    assert "activity_pending" not in res.widget.data["profile"]
