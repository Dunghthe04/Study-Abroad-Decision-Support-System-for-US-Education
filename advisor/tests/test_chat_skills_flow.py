"""B1 đến B3: skill_nhu_cau, skill_bac_hoc, skill_ho_so chạy trong ChatFlow (chưa có model: dùng skill của bước)."""

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import NOT_READY, QUESTIONS, ChatFlow
from app.services.intake import CHAT_BUTTON, FORM_BUTTON, INTAKE_ASK, INTAKE_ID
from app.services.ollama import OllamaError
from app.services.skills import build_handlers
from app.services.skills.skill_ho_so import OPTIONAL_QUESTIONS, SAVED, next_question
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


# Các câu trang 1 của form (hỏi trước GPA) coi như đã hỏi, để test đi thẳng vào câu cần kiểm tra
PAGE1_ASKED = {"D1": 1, "N1": 1, "N2": 1, "N3": 1}


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
    res = await turn(llm, "Em lớp 12, muốn học đại học ở Mỹ, GPA 8.5, IELTS 6.5", intake_mode="chat")

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
    res = await turn(llm, "Em muốn tìm trường ở Mỹ", intake_mode="chat")

    assert len(llm.calls) == 1
    assert res.state.question_id == "B1.1"
    assert res.state.profile == {}


async def test_profile_advice_with_missing_items_offers_chat_or_form_first():
    llm = FakeLLM({"need": "profile_advice", "branch": "DH", "info_given": {}})
    res = await turn(llm, "Em muốn tìm trường ở Mỹ")

    assert res.state.question_id == INTAKE_ID
    assert res.answer == INTAKE_ASK
    assert [r.label for r in res.quick_replies] == [CHAT_BUTTON.label, FORM_BUTTON.label]


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
        intake_mode="chat",
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
    profile = {
        "current_level": field("high_school"),
        "target_level": field("undergraduate"),
        "major": field("CS"),
    }
    res = await turn(
        FakeLLM(nothing_bac_hoc()),
        "dạ",
        current_step="B1",
        question_id="B1.4",
        asked={"B1.1": 1, "B1.2": 1, "B1.3": 2, "B1.4": 2},
        profile={**profile, "gpa": field({"value": 8.5, "scale": 10}, "said_in_B0")},
    )

    # Hết câu B1 nên sang B2, hỏi tiếp câu trang 1 của form (D1 vì đang học cấp 3)
    assert (res.state.current_step, res.state.question_id) == ("B2", "D1")
    assert res.answer == QUESTIONS["D1"]


async def test_major_is_required_and_asked_until_answered():
    profile = {"current_level": field("high_school"), "target_level": field("undergraduate")}
    res = await turn(
        FakeLLM(nothing_bac_hoc()),
        "dạ",
        current_step="B1",
        question_id="B1.3",
        asked={"B1.3": 2},
        profile=profile,
    )

    # Giống form: chưa chọn ngành hoặc bấm "Em chưa rõ ngành" thì chưa qua câu sau
    assert (res.state.question_id, res.state.asked["B1.3"]) == ("B1.3", 3)
    assert res.widget.type == "major"


# B3: skill_ho_so


async def test_wrong_gpa_is_not_saved_and_asked_again():
    llm = FakeLLM(ho_so({"gpa": {"value": 85, "scale": None}}))
    res = await turn(llm, "GPA em 85", current_step="B2", question_id="D2", asked={"D2": 1})

    assert "gpa" not in res.state.profile
    assert res.state.question_id == "D2"
    assert res.state.asked["D2"] == 2
    assert res.answer != QUESTIONS["D2"]  # câu hỏi lại của validators


async def test_gpa_without_scale_is_asked_again_with_confirm_button():
    llm = FakeLLM(ho_so({"gpa": {"value": 8.5, "scale": None}}))
    res = await turn(llm, "GPA em 8.5", current_step="B2", question_id="D2", asked={"D2": 1})

    assert "gpa" not in res.state.profile
    assert res.state.question_id == "D2"
    assert "thang 10" in res.answer
    assert [r.value for r in res.quick_replies] == ["GPA 8.5 thang 10"]


async def test_small_gpa_without_scale_offers_both_scales():
    llm = FakeLLM(ho_so({"gpa": {"value": 3, "scale": None}}))
    res = await turn(llm, "GPA em 3", current_step="B2", question_id="D2", asked={"D2": 1})

    assert "gpa" not in res.state.profile
    assert [r.label for r in res.quick_replies] == ["Đúng, thang 4", "Không, thang 10"]
    assert [r.value for r in res.quick_replies] == ["GPA 3 thang 4", "GPA 3 thang 10"]


async def test_gpa_with_scale_text_is_saved_without_qwen():
    llm = FakeLLM()
    res = await turn(
        llm, "GPA 3 thang 10", current_step="B2", question_id="D2", asked={**PAGE1_ASKED, "D2": 2}
    )

    assert llm.calls == []
    assert res.state.profile["gpa"] == field({"value": 3.0, "scale": 10})
    assert res.state.question_id == "E1"


async def test_gpa_said_with_scale_is_saved_directly():
    llm = FakeLLM(ho_so({"gpa": {"value": 8.5, "scale": 10}}))
    res = await turn(
        llm, "GPA em 8.5 thang 10 ạ", current_step="B2", question_id="D2", asked={**PAGE1_ASKED, "D2": 1}
    )

    assert res.state.profile["gpa"] == field({"value": 8.5, "scale": 10})
    assert res.state.question_id == "E1"


async def test_not_tested_english_ticks_plan_and_asks_month():
    llm = FakeLLM()
    profile = {"gpa": field({"value": 8.5, "scale": 10})}
    res = await turn(
        llm,
        "Em chưa thi tiếng Anh",
        current_step="B2",
        question_id="E1",
        asked={"D2": 1, "E1": 1},
        profile=profile,
    )

    assert llm.calls == []
    assert res.state.profile["english"].status == "none"
    assert res.state.profile["english_plan"] == field({"planned": True, "date": None})


async def test_not_tested_english_read_by_qwen_also_ticks_plan():
    llm = FakeLLM(ho_so(none_fields=["english"]))
    profile = {"gpa": field({"value": 8.5, "scale": 10})}
    res = await turn(
        llm, "em chưa thi", current_step="B2", question_id="E1", asked={"D2": 1, "E1": 1}, profile=profile
    )

    assert res.state.profile["english_plan"] == field({"planned": True, "date": None})


async def test_planned_without_month_asks_e2_and_skip_keeps_tick():
    base = {
        "gpa": field({"value": 8.5, "scale": 10}),
        "english": field(None, "none"),
        "english_plan": field({"planned": True, "date": None}),
        "budget": field("tier_20k_35k"),
    }
    asked = {**PAGE1_ASKED, "D2": 1, "E1": 1, "F1": 1, "F2": 1}
    state = ChatState(current_step="B2", question_id="F2", asked=asked, profile=base)
    assert next_question(state) == "E2"

    res = await turn(
        FakeLLM(), "Bỏ qua", current_step="B2", question_id="E2", asked={**asked, "E2": 1}, profile=base
    )
    assert res.state.profile["english_plan"] == field({"planned": True, "date": None})
    assert res.state.question_id != "E2"


async def test_no_english_asks_test_month_before_moving_to_budget():
    # Hỏi hết trang tiếng Anh (E1, E2, D3) rồi mới sang trang tài chính, không quay lại hỏi E2 sau F1, F2
    llm = FakeLLM(ho_so(none_fields=["english"]))
    res = await turn(
        llm,
        "Không",
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1},
        profile={"gpa": field({"value": 8.5, "scale": 10})},
    )
    assert res.state.question_id == "E2"

    res = await turn(FakeLLM(), "Bỏ qua", **res.state.model_dump())
    assert res.state.question_id == "D3"

    res = await turn(FakeLLM(), "Bỏ qua", **res.state.model_dump())
    assert res.state.question_id == "F1"


async def test_e2_month_picker_saves_without_qwen():
    llm = FakeLLM()
    profile = {"english": field(None, "none"), "english_plan": field({"planned": True, "date": None})}
    res = await turn(llm, "Em dự kiến thi tháng 5/2099", current_step="B2", question_id="E2", profile=profile)

    assert llm.calls == []
    assert res.state.profile["english_plan"] == field({"planned": True, "date": "2099-05"})


async def test_right_answer_moves_to_next_required_question():
    llm = FakeLLM(ho_so({"english": {"type": "ielts", "score": 6.5, "date": "2026-05"}}))
    res = await turn(
        llm,
        "IELTS 6.5 thi tháng 5",
        current_step="B2",
        question_id="E1",
        asked={**PAGE1_ASKED, "E1": 1, "D3": 1},
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
        asked={**PAGE1_ASKED, "D2": 1, "E1": 1, "F1": 1, "F2": 1},
        profile={"gpa": field({"value": 8.5, "scale": 10}), "budget": field("tier_20k_35k")},
    )

    assert res.state.profile["english"] == field(None, "none")
    assert res.state.question_id == "E2"


async def test_skipped_optional_question_is_not_asked_again():
    profile = {
        "current_level": field("high_school"),
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


async def test_confirm_at_review_confirms_said_info_and_assesses_right_away():
    llm = FakeLLM(OllamaError("tắt máy"), OllamaError("tắt máy"))  # đánh giá dùng câu mẫu
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    res = await turn(llm, "Đúng rồi", current_step="B2", question_id="X1", profile=profile)

    assert all(f.status == "confirmed" for f in res.state.profile.values())
    # Xác nhận xong thì đánh giá hồ sơ luôn trong lượt này rồi hỏi X2, không hỏi lại "có muốn xem không"
    assert res.skills == ["skill_ho_so", "skill_danh_gia"]
    assert (res.state.current_step, res.state.question_id) == ("B4", "X2")
    assert res.answer.startswith(SAVED)
    assert res.answer.endswith(QUESTIONS["X2"])
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
        asked={**PAGE1_ASKED, "D3": 1},
        profile={"gpa": field({"value": 8.5, "scale": 10})},
    )

    assert res.state.question_id == "F1"
    assert res.widget.type == "budget"
    assert res.widget.data["options"][0] == {"value": "tier_under_20k", "label": "Dưới 20.000 USD"}
    assert "Bỏ qua" not in [r.label for r in res.quick_replies]  # F1 bắt buộc nên không có nút bỏ qua


async def test_pressing_budget_button_saves_tier_without_qwen():
    llm = FakeLLM()
    res = await turn(
        llm,
        "Dưới 20.000 USD",
        current_step="B2",
        question_id="F1",
        asked={**PAGE1_ASKED, "D3": 1},
        profile=full_profile(),
    )

    assert llm.calls == []
    assert res.state.profile["budget"] == field("tier_under_20k")
    assert res.state.question_id == "F2"
    assert res.widget.type == "scholarship"
    assert [r.label for r in res.quick_replies][-1] == "Bỏ qua"


async def test_skip_button_moves_on_without_saving():
    llm = FakeLLM()
    profile = {**full_profile(), "current_level": field("high_school"), "budget": field("tier_35k_50k")}
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


async def test_current_level_select_saves_level_and_grade_without_qwen():
    llm = FakeLLM()
    res = await turn(llm, "Trung học phổ thông, Lớp 11", current_step="B1", question_id="B1.1")

    assert llm.calls == []
    assert res.state.profile["current_level"] == field("high_school")
    assert res.state.profile["current_grade"] == field("Lớp 11")
    assert res.state.question_id == "B1.2"


async def test_intake_select_saves_intake_without_qwen():
    llm = FakeLLM()
    profile = {
        "current_level": field("high_school"),
        "target_level": field("undergraduate"),
        "major": field("CS"),
    }
    res = await turn(llm, "Fall 2027", current_step="B1", question_id="B1.4", profile=profile)

    assert llm.calls == []
    assert res.state.profile["intake"] == field("Fall 2027")


async def test_major_question_comes_with_major_widget():
    res = await turn(
        FakeLLM(),
        "Thạc sĩ",
        current_step="B1",
        question_id="B1.2",
        profile={"current_level": field("college_university")},
    )

    assert res.widget.type == "major"


async def test_select_questions_come_with_form_fields():
    res = await turn(
        FakeLLM(),
        "Thạc sĩ",
        current_step="B1",
        question_id="B1.2",
        profile={"current_level": field("college_university"), "major": field("CS")},
    )

    assert res.state.question_id == "B1.4"
    assert res.widget.type == "select"
    assert [f["name"] for f in res.widget.data["fields"]] == ["term", "year"]
    assert res.widget.data["format"] == "{term} {year}"
    assert res.quick_replies[-1].label == "Bỏ qua"  # kỳ nhập học không bắt buộc, giống form


async def test_skipping_intake_moves_on_without_asking_again():
    llm = FakeLLM()
    profile = {
        "current_level": field("high_school"),
        "target_level": field("undergraduate"),
        "major": field("CS"),
    }
    res = await turn(llm, "Bỏ qua", current_step="B1", question_id="B1.4", asked={"B1.4": 1}, profile=profile)

    assert llm.calls == []
    assert "intake" not in res.state.profile
    assert res.state.current_step == "B2" and res.state.question_id != "B1.4"


# B4: hoạt động thiếu ý thì hỏi tiếp


def at_d4() -> dict:
    asked = {**PAGE1_ASKED, "F2": 1, "F3": 1, "D3": 1, "D4": 1}
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
    # Ghi xong một hoạt động thì hỏi còn hoạt động nữa không; bấm "Không còn hoạt động" thì sang D5
    assert res.state.question_id == "D4"
    assert "còn hoạt động nào nữa" in res.answer
    assert [r.label for r in res.quick_replies] == ["Không còn hoạt động"]

    llm = FakeLLM()
    res = await turn(llm, "Không còn hoạt động", **res.state.model_dump())

    assert llm.calls == []
    assert res.state.question_id == "D5"


# TH-22: học sinh không nói thêm thì ý còn thiếu lấy mức thấp nhất (thành viên, cấp trường), không bỏ mục
async def test_answer_without_new_info_saves_activity_with_lowest_values():
    state = at_d4()
    state["profile"]["activity_pending"] = field([ENGLISH_CLUB], "unknown")
    res = await turn(FakeLLM(ho_so()), "dạ em không nhớ", **state)

    assert "activity_pending" not in res.state.profile
    saved = res.state.profile["activities"].value
    assert [(a["name"], a["role"], a["impactLevel"]) for a in saved] == [
        ("CLB Tiếng Anh", "member", "school")
    ]


def test_lowest_values_fill_only_missing_parts():
    from datetime import date

    from app.services.skills.skill_ho_so import lowest

    today = date(2026, 10, 29)
    assert lowest("activities", {"name": "CLB", "role": "leader"}, today) == {
        "name": "CLB",
        "role": "leader",
        "impact_level": "school",
        "start_date": "10/2026",
        "end_date": "10/2026",
    }
    assert lowest("awards", {"name": "HSG"}, today) == {"name": "HSG", "impact_level": "school", "year": 2026}


# Ô nhập D4 / D5 (giống bước 5 của form) gửi câu ghép: ghi thẳng, không gọi Qwen
async def test_activity_widget_text_saves_without_qwen():
    llm = FakeLLM()
    text = "Hoạt động CLB Bóng rổ, vai trò Trưởng / Chủ nhiệm, phạm vi Tỉnh / Thành phố, từ 9/2024 đến 5/2025"
    res = await turn(llm, text, **at_d4())

    assert llm.calls == []
    assert res.state.profile["activities"].value == [
        {
            "name": "CLB Bóng rổ",
            "category": "activity",
            "role": "leader",
            "impactLevel": "province",
            "startDate": "09/2024",
            "endDate": "05/2025",
            "isOngoing": False,
        }
    ]
    assert res.state.question_id == "D4" and res.widget.type == "select"


async def test_ongoing_activity_and_award_widget_text():
    llm = FakeLLM()
    text = "Hoạt động CLB Tiếng Anh, vai trò Thành viên, phạm vi Trường, từ 1/2024, đang tham gia"
    res = await turn(llm, text, **at_d4())

    assert res.state.profile["activities"].value[0]["isOngoing"] is True

    state = {**res.state.model_dump(), "question_id": "D5"}
    res = await turn(llm, "Giải thưởng Giải Nhì HSG Toán, phạm vi Quốc gia, năm 2025", **state)

    assert llm.calls == []
    assert res.state.profile["awards"].value == [
        {"name": "Giải Nhì HSG Toán", "category": "achievement", "impactLevel": "national", "year": 2025}
    ]
    assert "còn giải thưởng nào nữa" in res.answer


async def test_activity_widget_text_with_end_before_start_asks_again():
    text = "Hoạt động CLB, vai trò Phó, phạm vi Trường, từ 9/2025 đến 5/2024"
    res = await turn(FakeLLM(), text, **at_d4())

    assert "activities" not in res.state.profile
    assert res.state.question_id == "D4"
    assert "Tháng kết thúc" in res.answer


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


# 29/10: D1 chỉ hỏi học sinh cấp 3; N1 chỉ hỏi khi chưa có bang muốn học; N4 không hỏi ở B2
def required_done(**extra) -> ChatState:
    profile = {
        "gpa": field({"value": 8.5, "scale": 10}),
        "english": field({"type": "ielts", "value": 6.5, "date": None}),
        "budget": field("tier_20k_35k"),
        **extra,
    }
    return ChatState(current_step="B2", profile=profile, asked={"F2": 1, "D3": 1, "D4": 1, "D5": 1, "F3": 1})


def test_education_system_only_asked_in_high_school():
    assert next_question(required_done(current_level=field("high_school"))) == "D1"
    assert next_question(required_done(current_level=field("college_university"))) == "N2"


# Hỏi theo thứ tự trang form; N1 (người thân, không có trong form) hỏi sau cùng
def test_relatives_question_is_asked_after_form_questions():
    state = required_done(school_type=field("all"))
    state.asked["N2"] = 1
    assert next_question(state) == "N1"


def test_relatives_question_skipped_when_states_known():
    state = required_done(preferred_states=field(["CA"]))
    assert next_question(state) == "N3"


def test_visa_question_is_not_asked_in_b2():
    assert "N4" not in OPTIONAL_QUESTIONS
    state = required_done(preferred_states=field(["CA"]), school_type=field("all"))
    assert next_question(state) is None


async def test_funding_button_saves_db_code():
    profile = {**full_profile(), "budget": field("tier_35k_50k")}
    res = await turn(
        FakeLLM(),
        "Vay vốn du học ngân hàng",
        current_step="B2",
        question_id="F3",
        asked={"F3": 1},
        profile=profile,
    )

    assert res.state.profile["funding_source"].value == "bank_loan"
