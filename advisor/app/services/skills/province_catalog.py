"""Tỉnh / thành Việt Nam, lấy từ backend (.NET VietnamProvinces) để chỉ có một nguồn.

- names(): danh sách tên chính thức cho ô chọn (GET api/v1/dashboard/provinces), đọc một lần rồi giữ lại.
- resolve(chữ): tên chính thức nếu chữ là một tỉnh / thành. Khớp tên trong danh sách (không phân biệt dấu, hoa thường);
  không khớp thì hỏi backend (GET .../provinces/resolve) để nhận cả tên gọi khác ("Sài Gòn", "TP.HCM").
Backend lỗi thì trả rỗng / None: chat vẫn hỏi bằng chữ, câu trả lời để model đọc, không đoán bừa là tên tỉnh.
"""

import logging
import time

import httpx

from app.services.skills.major_catalog import normalize

logger = logging.getLogger("advisor.provinces")

LIST_PATH = "api/v1/dashboard/provinces"
RESOLVE_PATH = "api/v1/dashboard/provinces/resolve"
# Đọc lỗi thì chờ ngần này giây mới thử lại
RETRY_AFTER = 300

_http: httpx.AsyncClient | None = None
_names: list[str] | None = None
_failed_at: float | None = None


def configure(http: httpx.AsyncClient | None) -> None:
    """main.py đưa client gọi backend vào (None = chưa cấu hình); cũng xóa danh sách đã đọc."""
    global _http, _names, _failed_at
    _http, _names, _failed_at = http, None, None


def loaded() -> list[str]:
    """Danh sách đã đọc (không gọi mạng), dùng ở chỗ code chạy đồng bộ. Chưa đọc thì rỗng."""
    return _names or []


async def names() -> list[str]:
    global _names, _failed_at
    if _names is not None:
        return _names
    if _http is None or (_failed_at is not None and time.monotonic() - _failed_at < RETRY_AFTER):
        return []
    try:
        response = await _http.get(LIST_PATH)
        response.raise_for_status()
        _names = [str(name) for name in response.json()]
    except (httpx.HTTPError, ValueError, TypeError):
        logger.warning("Không lấy được danh sách tỉnh / thành từ backend", exc_info=True)
        _failed_at = time.monotonic()
        return []
    return _names


async def resolve(text: str) -> str | None:
    wanted = normalize(text)
    if not wanted:
        return None
    if exact := next((name for name in await names() if normalize(name) == wanted), None):
        return exact
    if _http is None:
        return None
    try:
        response = await _http.get(RESOLVE_PATH, params={"name": text})
        response.raise_for_status()
        found = response.json().get("province")
    except (httpx.HTTPError, ValueError, AttributeError):
        logger.warning("Không hỏi được backend tên tỉnh / thành", exc_info=True)
        return None
    return found if isinstance(found, str) and found else None
