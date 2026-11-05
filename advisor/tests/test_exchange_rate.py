from datetime import datetime, timedelta

import httpx
import pytest

from app.services.skills import buttons, exchange_rate
from app.services.skills.validators import check_budget

NOW = datetime(2026, 10, 29, 8, 0)


@pytest.fixture(autouse=True)
def fresh_rate():
    exchange_rate.reset()
    yield
    exchange_rate.reset()


def fetch_returning(*rates):
    calls = []

    async def fetch():
        calls.append(1)
        value = rates[len(calls) - 1]
        if isinstance(value, Exception):
            raise value
        return value

    return fetch, calls


async def test_new_rate_is_used_to_convert_vnd():
    fetch, _ = fetch_returning(26_000)
    assert await exchange_rate.refresh(fetch, NOW) == 26_000
    # 910 triệu / 26.000 = 35.000 USD → mức 35k–50k (tỷ giá 25.000 thì là 36.400, cũng mức này)
    assert check_budget(910_000_000, "VND") == "tier_35k_50k"
    # 900 triệu / 26.000 ≈ 34.615 USD → mức 20k–35k (tỷ giá 25.000 thì là 36.000, mức 35k–50k)
    assert check_budget(900_000_000, "VND") == "tier_20k_35k"


async def test_rate_is_kept_for_one_day():
    fetch, calls = fetch_returning(26_000, 27_000)
    await exchange_rate.refresh(fetch, NOW)
    await exchange_rate.refresh(fetch, NOW + timedelta(hours=23))
    assert len(calls) == 1
    assert await exchange_rate.refresh(fetch, NOW + timedelta(days=1)) == 27_000


async def test_network_error_keeps_old_rate_and_retries_after_an_hour():
    fetch, calls = fetch_returning(26_000, httpx.ConnectError("mất mạng"), 26_500)
    await exchange_rate.refresh(fetch, NOW)
    day = NOW + timedelta(days=1)
    assert await exchange_rate.refresh(fetch, day) == 26_000
    assert await exchange_rate.refresh(fetch, day + timedelta(minutes=30)) == 26_000
    assert len(calls) == 2
    assert await exchange_rate.refresh(fetch, day + timedelta(hours=1)) == 26_500


async def test_strange_rate_is_ignored():
    fetch, _ = fetch_returning(2.6)
    assert await exchange_rate.refresh(fetch, NOW) == exchange_rate.FALLBACK_VND_PER_USD


async def test_no_network_call_while_testing():
    assert await exchange_rate.refresh() == exchange_rate.FALLBACK_VND_PER_USD


async def test_budget_widget_sends_rate():
    fetch, _ = fetch_returning(25_806.8)
    await exchange_rate.refresh(fetch, NOW)
    assert buttons.widget("F1").data["rate"] == 25_807
