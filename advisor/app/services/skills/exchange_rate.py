"""Tỷ giá 1 USD = ? VNĐ để đổi ngân sách VNĐ sang USD.

Lấy từ open.er-api.com (miễn phí, không cần khóa), nhớ 1 ngày rồi mới lấy lại.
Lấy lỗi thì giữ tỷ giá cũ, 1 giờ sau thử lại. Chưa lấy được lần nào thì dùng 25.000.
"""

import logging
import os
from collections.abc import Awaitable, Callable
from datetime import datetime, timedelta

import httpx

logger = logging.getLogger(__name__)

RATE_URL = "https://open.er-api.com/v6/latest/USD"
FALLBACK_VND_PER_USD = 25_000
MAX_AGE = timedelta(days=1)
RETRY_AFTER = timedelta(hours=1)
TIMEOUT_SECONDS = 5
# Tỷ giá ngoài khoảng này coi là dữ liệu hỏng, không dùng
SANE_RANGE = (10_000, 100_000)

_rate: float = FALLBACK_VND_PER_USD
_next_check: datetime | None = None


def vnd_per_usd() -> float:
    return _rate


async def fetch_rate() -> float:
    async with httpx.AsyncClient(timeout=TIMEOUT_SECONDS) as http:
        response = await http.get(RATE_URL)
        response.raise_for_status()
        return float(response.json()["rates"]["VND"])


# Lấy tỷ giá mới nếu đã quá hạn. fetch để test thay cách lấy; chạy test (pytest) thì không gọi mạng thật
async def refresh(fetch: Callable[[], Awaitable[float]] | None = None, now: datetime | None = None) -> float:
    global _rate, _next_check
    now = now or datetime.now()
    if _next_check is not None and now < _next_check:
        return _rate
    if fetch is None:
        if os.environ.get("PYTEST_CURRENT_TEST"):
            return _rate
        fetch = fetch_rate
    try:
        rate = await fetch()
        if not SANE_RANGE[0] < rate < SANE_RANGE[1]:
            raise ValueError(f"tỷ giá lạ: {rate}")
    except (httpx.HTTPError, KeyError, TypeError, ValueError) as err:
        logger.warning("exchange_rate: không lấy được tỷ giá (%s), dùng %s", err, _rate)
        _next_check = now + RETRY_AFTER
        return _rate
    _rate, _next_check = rate, now + MAX_AGE
    return _rate


# Cho test: về tỷ giá mặc định, lần sau lấy lại
def reset() -> None:
    global _rate, _next_check
    _rate, _next_check = FALLBACK_VND_PER_USD, None
