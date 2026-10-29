from datetime import date

from app.schemas.chat import ChatState, ProfileField
from app.services.ollama import OllamaError
from app.services.skills.skill_danh_gia import (
    Assessor,
    DanhGiaHandler,
    assess_levels,
    ec_tier_of,
    read_facts,
)
from tests.fakes import FakeLLM

TODAY = date(2026, 10, 28)


def profile(**values):
    return {name: ProfileField(value=value, status="confirmed") for name, value in values.items()}


GOOD = profile(
    gpa={"value": 9.0, "scale": 10},
    english={"type": "ielts", "value": 7.0, "date": None},
    budget="tier_above_70k",
    sat=1450,
)
GOOD_LEVELS = {
    "academic": "strong",
    "sat": "strong",
    "english": "excellent",
    "finance": "ok",
    "ec_tier": None,
    "scholarship": "high",
}


def levels_of(**values):
    return assess_levels(read_facts(profile(**values), TODAY))


# F1: ngưỡng giống assessProfile bên frontend
def test_levels_good_profile():
    assert assess_levels(read_facts(GOOD, TODAY)) == GOOD_LEVELS


def test_gpa_10_converted_to_4_scale():
    # 8.5/10 → 3.4/4: trung bình (ngưỡng mạnh là 3.5)
    assert levels_of(gpa={"value": 8.5, "scale": 10})["academic"] == "medium"
    assert levels_of(gpa={"value": 3.5, "scale": 4})["academic"] == "strong"
    assert levels_of(gpa={"value": 7.0, "scale": 10})["academic"] == "weak"


def test_missing_data_is_none_not_guessed():
    levels = levels_of()
    assert levels == {
        "academic": "none",
        "sat": "none",
        "english": "none",
        "finance": "none",
        "ec_tier": None,
        "scholarship": "low",
    }


def test_toefl_and_duolingo_use_ielts_equivalent():
    assert levels_of(english={"type": "toefl", "value": 94})["english"] == "excellent"
    assert levels_of(english={"type": "toefl", "value": 80})["english"] == "good"
    assert levels_of(english={"type": "duolingo", "value": 100})["english"] == "near"
    assert levels_of(english={"type": "ielts", "value": 5.0})["english"] == "low"


def test_pte_not_converted_like_frontend():
    assert levels_of(english={"type": "pte", "value": 65})["english"] == "none"


def test_budget_tier_thresholds_unchanged():
    assert levels_of(budget="tier_under_20k")["finance"] == "short"
    assert levels_of(budget="tier_20k_35k")["finance"] == "tight"
    assert levels_of(budget="tier_35k_50k")["finance"] == "tight"
    assert levels_of(budget="tier_50k_70k")["finance"] == "ok"


def test_scholarship_chance():
    gpa, ielts = {"value": 9.0, "scale": 10}, {"type": "ielts", "value": 6.5}
    assert levels_of(gpa=gpa, english=ielts)["scholarship"] == "high"
    assert levels_of(gpa=gpa)["scholarship"] == "medium"
    assert levels_of(english=ielts)["scholarship"] == "medium"


def test_ec_tier_like_frontend():
    leader = {
        "role": "founder",
        "impactLevel": "national",
        "startDate": "01/2024",
        "endDate": None,
        "isOngoing": True,
    }
    # 0.4*4/5 + 0.35*1 + 0.25*1 = 0.92 → m = 0.23 → bậc 2
    assert ec_tier_of([leader], [], TODAY) == 2
    # 4 hoạt động như vậy → m = 0.92 → bậc 5
    assert ec_tier_of([leader] * 4, [], TODAY) == 5
    # chỉ 1 giải quốc gia: 0.05*4 = 0.2 → m = 0.05 → bậc 1
    assert ec_tier_of([], [{"impactLevel": "national"}], TODAY) == 1


def ai_answer(levels, **text):
    return {
        "levels": levels,
        "summary": text.get("summary", "Hồ sơ của bạn khá mạnh."),
        "strengths": text.get("strengths", ["GPA 9 trên thang 10 rất tốt.", "IELTS 7 đạt yêu cầu."]),
        "weaknesses": text.get("weaknesses", ["Hoạt động ngoại khóa còn ít."]),
    }


# TH-29: Qwen chép đúng mức → dùng lời của Qwen
async def test_th29_ai_text_used_when_levels_match():
    llm = FakeLLM(ai_answer(GOOD_LEVELS))
    res = await Assessor(llm, "qwen3:8b").assess(GOOD, TODAY)

    assert res.ai_used is True
    assert res.levels == GOOD_LEVELS
    assert res.strengths == ["GPA 9 trên thang 10 rất tốt.", "IELTS 7 đạt yêu cầu."]
    assert len(llm.calls) == 1


# TH-30: Qwen đổi mức → gọi lại, vẫn sai thì giữ mức của code và dùng câu mẫu
async def test_th30_changed_level_retried_then_code_levels(caplog):
    wrong = {**GOOD_LEVELS, "finance": "short"}
    llm = FakeLLM(ai_answer(wrong), ai_answer({**GOOD_LEVELS, "academic": "excellent"}))
    res = await Assessor(llm, "qwen3:8b").assess(GOOD, TODAY)

    assert len(llm.calls) == 2
    assert res.ai_used is False
    assert res.levels == GOOD_LEVELS
    assert "skill_danh_gia" in caplog.text


async def test_th30_retry_succeeds_on_second_try():
    llm = FakeLLM(ai_answer({**GOOD_LEVELS, "sat": "medium"}), ai_answer(GOOD_LEVELS))
    res = await Assessor(llm, "qwen3:8b").assess(GOOD, TODAY)

    assert res.ai_used is True


# TH-31: bịa số hoặc hứa đỗ → gọi lại, vẫn sai thì câu mẫu
async def test_th31_made_up_number_or_promise_falls_back_to_template():
    llm = FakeLLM(
        ai_answer(GOOD_LEVELS, summary="Tỉ lệ đỗ của bạn khoảng 85%."),
        ai_answer(GOOD_LEVELS, summary="Bạn chắc chắn đỗ trường top."),
    )
    res = await Assessor(llm, "qwen3:8b").assess(GOOD, TODAY)

    assert res.ai_used is False
    assert "GPA 9 (thang 10) ở mức mạnh." in res.strengths
    assert "SAT 1450 ở mức mạnh." in res.strengths


async def test_number_formats_from_profile_allowed():
    tight = profile(gpa={"value": 8.5, "scale": 10}, budget="tier_20k_35k")
    levels = assess_levels(read_facts(tight, TODAY))
    text = ai_answer(
        levels,
        summary="GPA 8,5 tương đương 3.4 thang 4; ngân sách khoảng 35.000 USD.",
        strengths=["GPA 8.5 ổn định."],
        weaknesses=["Chưa có SAT."],
    )
    res = await Assessor(FakeLLM(text), "qwen3:8b").assess(tight, TODAY)

    assert res.ai_used is True


async def test_ollama_down_uses_template():
    llm = FakeLLM(OllamaError("down"), OllamaError("down"))
    res = await Assessor(llm, "qwen3:8b").assess(profile(), TODAY)

    assert res.ai_used is False
    assert "Chưa có chứng chỉ tiếng Anh. Cần thi trước hạn nộp ít nhất 2 tháng." in res.weaknesses
    assert len(res.weaknesses) <= 3


async def test_handler_moves_to_b4_and_asks_x2():
    state = ChatState(current_step="B3", profile=GOOD)
    out = await DanhGiaHandler(FakeLLM(ai_answer(GOOD_LEVELS)), "qwen3:8b").run(state, "hồ sơ em thế nào")

    assert state.current_step == "B4"
    assert state.question_id == "X2"
    assert "Điểm mạnh:" in out.answer
    assert out.answer.endswith("Bạn có muốn mình đề xuất lộ trình và danh sách trường phù hợp không?")
    assert [r.value for r in out.quick_replies] == ["Có", "Để sau"]
