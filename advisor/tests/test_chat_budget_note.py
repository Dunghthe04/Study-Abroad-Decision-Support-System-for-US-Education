"""Ngân sách lưu theo khoảng nên phải nói cho học sinh biết hệ thống tính tới mức nào.

Lỗi thật 27/11/2026: học sinh nói 25.000, bị đáp "chưa thấy thông tin nào mới" (cùng khoảng đang lưu), rồi hệ thống
tính theo 35.000 mà không nói gì. Chỉ nói khi con số học sinh nói thấp hơn trần của khoảng; có danh sách gợi ý thì
kèm nút thử đúng con số đó (thử giả định, không lưu).
"""

import re
from pathlib import Path

from app.schemas.chat import ChatState, ProfileField
from app.services.chat_flow import ask
from app.services.replies import PROFILE_DONE
from app.services.skills.validators import BUDGET_TOLERANCE_PERCENT
from tests.fakes import FakeLLM, tin
from tests.test_profile_handler import NO_HO_SO, full_profile, make_handler

TIER = "tier_20k_35k"


def budget(amount, **extra):
    value = {"amount": amount, "currency": "USD", "tier": None, **extra}
    return {"values": {"budget": value}, "none_fields": []}


def state_with(tier=TIER, **fields) -> ChatState:
    profile = {**full_profile(), "budget": ProfileField(value=tier, status="confirmed")}
    return ChatState(current_step="B3", profile=profile, intake_mode="chat", **fields)


def said(amount: str, *tasks):
    entities = {"budget": amount, **({"tasks": list(tasks)} if tasks else {})}
    return tin(("collect_profile", entities))


async def test_a_new_amount_says_the_range_and_how_much_is_counted():
    state = state_with("tier_50k_70k")

    output = await make_handler(FakeLLM(budget(25_000))).collect(
        state, "nhà em lo được 25000 đô", said("25000 đô")
    )

    assert state.profile["budget"].value == TIER
    assert "25.000 USD" in output.answer and "20.000 – 35.000 USD" in output.answer
    assert "tính theo mức 35.000 USD" in output.answer


async def test_another_amount_in_the_same_range_says_why_nothing_changed():
    output = await make_handler(FakeLLM(budget(30_000))).collect(
        state_with(), "nhà em lo được 30000 đô", said("30000 đô")
    )

    assert "30.000 USD vẫn nằm trong mức 20.000 – 35.000 USD" in output.answer
    assert PROFILE_DONE not in output.answer and "trùng với hồ sơ" not in output.answer


async def test_editing_within_the_same_range_keeps_the_review_board():
    state = state_with()
    ask(state, "X1")
    llm = FakeLLM(budget(30_000), NO_HO_SO)

    output = await make_handler(llm).collect(state, "sửa ngân sách thành 30000 đô", said("30000 đô", "edit"))

    assert "vẫn nằm trong mức" in output.answer and "trùng với hồ sơ" not in output.answer
    assert output.widget is not None and output.widget.type == "review"


async def test_with_a_school_list_a_button_tries_the_exact_amount():
    state = state_with(has_results=True)

    output = await make_handler(FakeLLM(budget(25_000))).collect(state, "tầm 25000 đô", said("25000 đô"))

    button = next(b for b in output.quick_replies if b.label == "Thử với đúng 25.000 USD")
    assert button.value == "Nếu ngân sách của em là 25.000 USD một năm thì sao?"


async def test_without_a_school_list_there_is_no_button():
    output = await make_handler(FakeLLM(budget(25_000))).collect(
        state_with(), "tầm 25000 đô", said("25000 đô")
    )

    assert not [b for b in output.quick_replies if b.label.startswith("Thử với đúng")]


# Gõ số ở câu hỏi ngân sách không qua bộ đọc cũng được nói như vậy
async def test_a_typed_amount_gets_the_same_note():
    state = state_with("tier_50k_70k")
    ask(state, "F1")

    output = await make_handler(FakeLLM()).collect(state, "25.000 USD", tin("collect_profile"))

    assert "tính theo mức 35.000 USD" in output.answer


# Nói đúng trần, mức tối thiểu, hoặc khoảng không có trần: không có gì chênh để báo
async def test_nothing_is_added_when_the_amount_is_the_ceiling_or_a_minimum():
    for amount, extra in ((35_000, {}), (40_000, {"at_least": True}), (90_000, {})):
        output = await make_handler(FakeLLM(budget(amount, **extra))).collect(
            state_with("tier_50k_70k"), f"{amount} đô", said(f"{amount} đô")
        )
        assert "tính theo mức" not in output.answer, amount


# Bộ đọc chép lại mức đang lưu (không có số tiền) thì không được bịa ra con số học sinh nói
async def test_a_copied_range_adds_no_note():
    copied = {"values": {"budget": {"amount": None, "currency": None, "tier": TIER}}, "none_fields": []}

    output = await make_handler(FakeLLM(copied)).collect(
        state_with(), "em muốn sửa ngân sách", said("ngân sách", "edit")
    )

    assert "tính theo mức" not in output.answer and "vẫn nằm trong mức" not in output.answer


# UC-22: trường được xét khi chi phí không quá ngân sách cộng 10%. Lời nhắn phải nói cả phần cho vượt
async def test_the_note_includes_the_ten_percent_allowance():
    output = await make_handler(FakeLLM(budget(25_000))).collect(
        state_with("tier_50k_70k"), "nhà em lo được 25000 đô", said("25000 đô")
    )

    assert "tính theo mức 35.000 USD và cho vượt thêm 10% (tới 38.500 USD)" in output.answer
    assert "tối đa 35.000" not in output.answer


def test_the_allowance_matches_the_recommendation_engine():
    path = "backend/src/StudyAbroad.Application/Recommendations/RecommendationService.cs"
    source = (Path(__file__).resolve().parents[2] / path).read_text(encoding="utf-8")

    found = re.search(r"const decimal BudgetTolerance = ([0-9.]+)m;", source)

    assert BUDGET_TOLERANCE_PERCENT == round(float(found[1]) * 100)
