"""51 bang trong bảng app.us_states và cách đổi tên bang học sinh nói sang mã bang (CA, TX...).

Form lưu mã bang, trường đại học cũng lọc theo mã, nên chat phải lưu mã giống form.
Khớp đúng mã hoặc tên (không phân biệt dấu, hoa thường) thì đổi sang mã.
Gần giống ("Califonia") hoặc không thấy thì hỏi lại.
"""

import asyncio
import logging
import time
from dataclasses import dataclass, field
from difflib import SequenceMatcher

import psycopg

from app.core.config import get_settings
from app.services.skills.major_catalog import normalize
from app.services.skills.validators import AskAgain

logger = logging.getLogger("advisor.states")

STATES_SQL = "SELECT code, name, region FROM app.us_states ORDER BY name"
# Gõ sai chính tả: hai tên phải giống ít nhất ngần này mới gợi ý
MIN_TYPO_RATIO = 0.75
MAX_SUGGESTIONS = 3
# Đọc DB lỗi thì chờ ngần này giây mới thử lại
RETRY_AFTER = 300
# Chữ thừa học sinh hay nói kèm: "bang Texas", "Washington state"
EXTRA_WORDS = {"bang", "state", "tieu"}


@dataclass(frozen=True)
class UsState:
    code: str
    name: str
    region: str = ""


@dataclass
class StateMatch:
    exact: UsState | None = None
    near: list[UsState] = field(default_factory=list)


def clean(text: str) -> str:
    return " ".join(w for w in normalize(text).split() if w not in EXTRA_WORDS)


# Khớp đúng mã hoặc tên thì trả exact; không thì tối đa 3 bang gần giống
# (gõ sai chính tả, hoặc nói thiếu như "Carolina" ra North Carolina, South Carolina)
def match(text: str, states: list[UsState]) -> StateMatch:
    text = clean(text)
    if not text:
        return StateMatch()
    for state in states:
        if text in (state.code.lower(), clean(state.name)):
            return StateMatch(exact=state)
    scored = []
    for state in states:
        name = clean(state.name)
        inside = 0.9 if f" {text} " in f" {name} " else 0.0
        ratio = SequenceMatcher(None, text, name).ratio()
        best = max(inside, ratio if ratio >= MIN_TYPO_RATIO else 0.0)
        if best > 0:
            scored.append((best, state))
    scored.sort(key=lambda item: -item[0])
    return StateMatch(near=[state for _, state in scored[:MAX_SUGGESTIONS]])


# Đổi danh sách bang học sinh nói sang mã. Một bang không khớp đúng thì ném AskAgain:
# có bang gần giống thì hỏi có phải bang đó không, không có thì nhờ viết lại.
# Danh sách bang rỗng (không đọc được DB) thì giữ nguyên như trước
def to_codes(names: list[str], states: list[UsState]) -> list[str]:
    if not states:
        return names
    codes = []
    for name in names:
        found = match(name, states)
        if found.exact is None:
            if found.near:
                options = ", ".join(s.name for s in found.near)
                raise AskAgain(f'Mình thấy "{name}" gần với bang {options}. Bạn chọn lại bang giúp mình nhé?')
            raise AskAgain(
                f'Mình chưa tìm thấy bang "{name}". Bạn viết lại tên bang bằng tiếng Anh giúp mình nhé?'
            )
        if found.exact.code not in codes:
            codes.append(found.exact.code)
    return codes


def _read_db(database_url: str) -> list[UsState]:
    with psycopg.connect(database_url, connect_timeout=5) as conn:
        rows = conn.execute(STATES_SQL).fetchall()
    return [UsState(code, name, region) for code, name, region in rows]


_cache: list[UsState] | None = None
_failed_at = 0.0


# Danh sách bang, đọc DB một lần rồi giữ lại. Không có DATABASE_URL hoặc DB lỗi thì trả rỗng
async def states() -> list[UsState]:
    global _cache, _failed_at
    if _cache is not None:
        return _cache
    database_url = get_settings().database_url
    if not database_url or time.monotonic() - _failed_at < RETRY_AFTER:
        return []
    try:
        _cache = await asyncio.to_thread(_read_db, database_url)
    except Exception:
        logger.exception("Không đọc được bảng us_states, chat tạm giữ nguyên tên bang học sinh nói")
        _failed_at = time.monotonic()
        return []
    return _cache


# Danh sách đã đọc (không chờ DB), dùng trong code kiểm tra đồng bộ. Chưa đọc thì rỗng
def loaded() -> list[UsState]:
    return _cache or []


# Cho test đặt sẵn danh sách bang (None = xóa, đọc lại từ DB)
def set_states(items: list[UsState] | None) -> None:
    global _cache, _failed_at
    _cache, _failed_at = items, 0.0
