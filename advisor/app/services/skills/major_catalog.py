"""Danh sách ngành trong bảng app.majors (38 nhóm CIP) và cách đổi chữ học sinh gõ sang đúng ngành.

Hồ sơ lưu tên tiếng Anh (cột name) giống form, vì .NET chỉ nhận ngành có trong danh sách.
Khớp đúng tên Anh, tên Việt hoặc tên gọi khác (không phân biệt dấu, hoa thường) thì lưu luôn.
Không khớp đúng thì trả vài ngành gần giống để chat hỏi lại.
"""

import asyncio
import logging
import re
import time
import unicodedata
from dataclasses import dataclass, field
from difflib import SequenceMatcher

import psycopg

from app.core.config import get_settings

logger = logging.getLogger("advisor.majors")

# Chỉ lấy ngành có trường đào tạo, giống GetMajorOptionsAsync của .NET
MAJORS_SQL = """
SELECT m.name, m.name_vi, m.aliases, m.is_popular
FROM app.majors m
WHERE EXISTS (SELECT 1 FROM app.university_offering_majors l WHERE l.major_id = m.id)
ORDER BY coalesce(m.name_vi, m.name)
"""
# Điểm giống tối thiểu để đưa ra gợi ý, và số gợi ý tối đa
MIN_SCORE = 0.6
MAX_SUGGESTIONS = 3
# Gõ sai chính tả ("tam ly hok"): hai chuỗi phải giống ít nhất ngần này mới tính
MIN_TYPO_RATIO = 0.75
# Đọc DB lỗi thì chờ ngần này giây mới thử lại, để mỗi tin nhắn không phải chờ kết nối
RETRY_AFTER = 300
# Chữ không nói lên ngành nào, bỏ khi so từng chữ ("ngành khoa học máy tính" → "may tinh")
STOP_WORDS = {
    "nganh",
    "hoc",
    "khoa",
    "va",
    "em",
    "toi",
    "minh",
    "muon",
    "chuyen",
    "ve",
    "and",
    "of",
    "studies",
}


@dataclass(frozen=True)
class Major:
    name: str
    name_vi: str | None = None
    aliases: tuple[str, ...] = ()
    is_popular: bool = False

    # Chữ hiện cho học sinh và cũng là chữ trên nút gợi ý
    @property
    def label(self) -> str:
        return self.name_vi or self.name

    # Các cách gọi ngành này, đã bỏ dấu
    def keys(self) -> list[str]:
        return [normalize(k) for k in (self.name, self.name_vi, *self.aliases) if k]


@dataclass
class MajorMatch:
    exact: Major | None = None
    near: list[Major] = field(default_factory=list)


# Bỏ dấu, đổi đ → d, chữ thường, bỏ dấu câu: "Khoa học Máy tính!" → "khoa hoc may tinh"
def normalize(text: str) -> str:
    text = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    return " ".join(re.sub(r"[^a-z0-9]+", " ", text).split())


def content(text: str) -> str:
    return " ".join(w for w in text.split() if w not in STOP_WORDS)


# Điểm giống (0–1) giữa chữ học sinh gõ và một cách gọi ngành, chỉ xét chữ có nghĩa (bỏ "ngành", "khoa học"...)
def score(text: str, key: str) -> float:
    text, key = content(text), content(key)
    if not text or not key:
        return 0.0
    # Chữ gõ nằm trọn trong tên ngành hoặc ngược lại ("may tinh" nằm trong "may tinh thong tin")
    inside = 0.9 if f" {text} " in f" {key} " or f" {key} " in f" {text} " else 0.0
    words = set(text.split())
    shared = len(words & set(key.split())) / len(words)
    ratio = SequenceMatcher(None, text, key).ratio()
    return max(inside, shared, ratio if ratio >= MIN_TYPO_RATIO else 0.0)


# Khớp đúng thì trả exact; không thì trả tối đa 3 ngành gần nhất (ngành phổ biến đứng trước khi bằng điểm)
def match(text: str, majors: list[Major]) -> MajorMatch:
    text = normalize(text)
    if not text:
        return MajorMatch()
    for major in majors:
        if text in major.keys():
            return MajorMatch(exact=major)
    scored = [(max(score(text, k) for k in major.keys()), major) for major in majors]
    scored = [item for item in scored if item[0] >= MIN_SCORE]
    scored.sort(key=lambda item: (-item[0], not item[1].is_popular))
    return MajorMatch(near=[major for _, major in scored[:MAX_SUGGESTIONS]])


def _read_db(database_url: str) -> list[Major]:
    with psycopg.connect(database_url, connect_timeout=5) as conn:
        rows = conn.execute(MAJORS_SQL).fetchall()
    return [
        Major(name, name_vi, tuple(aliases or ()), bool(popular)) for name, name_vi, aliases, popular in rows
    ]


_cache: list[Major] | None = None
_failed_at = 0.0


# Danh sách ngành, đọc DB một lần rồi giữ lại. Không có DATABASE_URL hoặc DB lỗi thì trả rỗng:
# khi đó chat giữ nguyên chữ học sinh gõ như trước
async def majors() -> list[Major]:
    global _cache, _failed_at
    if _cache is not None:
        return _cache
    database_url = get_settings().database_url
    if not database_url or time.monotonic() - _failed_at < RETRY_AFTER:
        return []
    try:
        _cache = await asyncio.to_thread(_read_db, database_url)
    except Exception:
        logger.exception("Không đọc được bảng majors, chat tạm giữ nguyên tên ngành học sinh gõ")
        _failed_at = time.monotonic()
        return []
    return _cache


# Khớp chữ học sinh gõ với danh sách ngành. Danh sách rỗng thì None (không kiểm được, giữ nguyên chữ)
async def find(text: str) -> MajorMatch | None:
    items = await majors()
    return match(text, items) if items else None


# Cho test đặt sẵn danh sách ngành (None = xóa, đọc lại từ DB)
def set_majors(items: list[Major] | None) -> None:
    global _cache, _failed_at
    _cache, _failed_at = items, 0.0
