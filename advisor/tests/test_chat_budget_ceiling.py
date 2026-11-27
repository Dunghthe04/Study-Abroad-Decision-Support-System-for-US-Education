"""Số tiền học sinh nói là mức TỐI ĐA gia đình chi được, nên đúng bằng trần của một khoảng thì thuộc khoảng đó.

Lỗi thấy khi đọc code 27/11/2026: "tối đa 35.000" bị xếp 35-50 nghìn (tính tới 50.000), "50 đến 70 nghìn" bị xếp
trên 70 nghìn (không giới hạn). Nói "trên 70 nghìn" thì vẫn là khoảng trên.
Ngân sách phụ huynh nhập số chính xác giữ quy tắc cũ (budget_tier, cùng quy tắc với .NET).
"""

import pytest

from app.schemas.chat import ChatState
from app.services.chat_flow import ask
from app.services.skills.skill_ho_so import HoSoHandler
from app.services.skills.validators import check_budget, read_money
from tests.fakes import FakeLLM


@pytest.mark.parametrize(
    ("usd", "tier"),
    [
        (20_000, "tier_under_20k"),
        (20_001, "tier_20k_35k"),
        (25_000, "tier_20k_35k"),
        (35_000, "tier_20k_35k"),
        (35_001, "tier_35k_50k"),
        (50_000, "tier_35k_50k"),
        (70_000, "tier_50k_70k"),
        (70_001, "tier_above_70k"),
    ],
)
def test_a_stated_amount_on_a_ceiling_stays_in_that_range(usd, tier):
    assert check_budget(usd) == tier


@pytest.mark.parametrize(("usd", "tier"), [(35_000, "tier_35k_50k"), (70_000, "tier_above_70k")])
def test_an_amount_said_as_a_minimum_goes_to_the_range_above(usd, tier):
    assert check_budget(usd, at_least=True) == tier


async def read_budget(message: str, budget: dict) -> str:
    state = ChatState(current_step="B2", intake_mode="chat")
    ask(state, "F1")
    await HoSoHandler(FakeLLM({"values": {"budget": budget}, "none_fields": []}), "test").run(state, message)
    return state.profile["budget"].value


async def test_the_reader_marks_a_minimum():
    budget = {"amount": 70_000, "currency": "USD", "tier": None, "at_least": True}

    assert await read_budget("nhà em lo được trên 70 nghìn đô", budget) == "tier_above_70k"


async def test_a_range_read_from_chat_keeps_its_upper_number_inside_the_range():
    budget = {"amount": 70_000, "currency": "USD", "tier": None}

    assert await read_budget("tầm 50 đến 70 nghìn đô gì đó", budget) == "tier_50k_70k"


# Gõ gọn số tiền (không qua bộ đọc) cũng theo cùng quy tắc; câu "trên ..." không đi đường này
async def test_a_typed_amount_follows_the_same_rule():
    assert await read_budget("35.000 USD", {}) == "tier_20k_35k"
    assert read_money("trên 70.000 USD") is None
