"""Đổi điều kiện tìm trường học sinh gõ sang bộ lọc chuẩn cho .NET: mã bang, tên ngành, USD, tỉ lệ, hạng; điều chưa đọc được thì báo, không bỏ qua."""

import pytest

from app.services import search_filters
from app.services.search_filters import read_cost_usd, read_rank, read_rate
from app.services.skills import exchange_rate, major_catalog, state_catalog
from app.services.skills.major_catalog import Major
from app.services.skills.state_catalog import UsState

STATES = [UsState("CA", "California"), UsState("TX", "Texas"), UsState("NY", "New York")]
MAJORS = [Major("Computer Science", "Khoa học máy tính"), Major("Business", "Kinh doanh")]


@pytest.fixture(autouse=True)
def catalogs(monkeypatch):
    state_catalog.set_states(STATES)
    major_catalog.set_majors(MAJORS)
    monkeypatch.setattr(exchange_rate, "_rate", 25_000.0)
    yield
    state_catalog.set_states(None)
    major_catalog.set_majors(None)


# ---- từng điều kiện
@pytest.mark.parametrize(
    ("text", "usd"),
    [("30 nghìn đô", 30000), ("30.000 USD", 30000), ("40k", 40000), ("30000", 30000), ("800 triệu", 32000)],
)
def test_cost_is_read_as_usd_per_year(text, usd):
    assert read_cost_usd(text) == usd


@pytest.mark.parametrize("text", ["rẻ", "800", "", "nhiều tiền"])
def test_a_cost_that_cannot_be_read_is_none(text):
    assert read_cost_usd(text) is None


@pytest.mark.parametrize(
    ("text", "rate"), [("50%", 0.5), ("50 phần trăm", 0.5), ("0,5", 0.5), ("30", 0.3), ("5%", 0.05)]
)
def test_rate_is_read_as_a_fraction(text, rate):
    assert read_rate(text) == pytest.approx(rate)


@pytest.mark.parametrize("text", ["nhiều", "0", "150%", ""])
def test_a_rate_out_of_range_or_unreadable_is_none(text):
    assert read_rate(text) is None


@pytest.mark.parametrize(("text", "rank"), [("top 50", 50), ("50", 50), ("trong 20 trường đầu", 20)])
def test_rank_is_a_positive_integer(text, rank):
    assert read_rank(text) == rank


async def test_every_filter_is_normalized_for_dotnet():
    plan = await search_filters.normalize(
        {
            "state": ["texas", "California"],
            "city": " Austin ",
            "major": "khoa học máy tính",
            "school_type": "public",
            "aid": "need_based",
            "test_policy": "optional",
            "max_cost": "30 nghìn đô",
            "max_ielts": "6,5",
            "max_toefl": "90",
            "max_duolingo": "110",
            "min_acceptance_rate": "50%",
            "max_acceptance_rate": "25%",
            "max_rank": "top 50",
            "min_graduation_rate": "80%",
            "ivy": "yes",
        },
        "lowest_cost",
    )

    assert plan.unresolved == []
    assert plan.sort == "lowestCost"
    assert plan.filters == {
        "states": ["TX", "CA"],
        "city": "Austin",
        "major": "Computer Science",
        "control": "public",
        "aid": "need",
        "testPolicy": "optional",
        "maxCostUsd": 30000,
        "maxIelts": 6.5,
        "maxToefl": 90.0,
        "maxDuolingo": 110.0,
        "minAcceptRate": 0.5,
        "maxAcceptRate": 0.25,
        "maxRank": 50,
        "minGradRate": 0.8,
        "ivy": True,
    }


async def test_only_the_conditions_the_student_said_are_in_the_filters():
    plan = await search_filters.normalize({"state": ["new york"], "aid": "yes"})

    assert plan.filters == {"states": ["NY"], "aid": "any"}
    assert plan.sort == "quality"  # mặc định


@pytest.mark.parametrize(
    ("filters", "expected"),
    [
        ({"state": ["Califonia"]}, "bang “Califonia”"),  # gõ sai: không đoán
        ({"major": "ngành không tồn tại xyz"}, "ngành “ngành không tồn tại xyz”"),
        ({"max_cost": "rẻ thôi"}, "mức chi phí “rẻ thôi”"),
        ({"max_ielts": "12"}, "mức IELTS “12”"),  # ngoài thang 0 đến 9
        ({"max_rank": "cao"}, "thứ hạng “cao”"),
        ({"max_acceptance_rate": "nhiều"}, "tỷ lệ nhận “nhiều”"),
    ],
)
async def test_what_cannot_be_read_is_reported_not_silently_dropped(filters, expected):
    plan = await search_filters.normalize(filters)

    assert plan.unresolved == [expected]
    assert plan.filters == {}


async def test_a_good_condition_survives_next_to_a_bad_one():
    plan = await search_filters.normalize({"state": ["texas", "Narnia"], "aid": "yes"})

    assert plan.filters == {"states": ["TX"], "aid": "any"}
    assert plan.unresolved == ["bang “Narnia”"]


async def test_state_may_be_one_name_as_the_model_returns_it():
    plan = await search_filters.normalize({"state": "texas"})

    assert plan.filters == {"states": ["TX"]}


async def test_when_the_state_catalog_is_unavailable_the_states_are_reported():
    state_catalog.set_states([])  # không đọc được danh mục bang: không dám đoán mã

    plan = await search_filters.normalize({"state": ["Texas"]})

    assert plan.filters == {}
    assert plan.unresolved == ["bang “Texas”"]
