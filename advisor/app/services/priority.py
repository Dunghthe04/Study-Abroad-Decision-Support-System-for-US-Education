"""Ưu tiên của học sinh nói trong chat ("ưu tiên học bổng", "quan tâm chi phí thấp nhất").

Trang gợi ý có 3 thanh ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng), mỗi thanh 0..4, chỉ đổi thứ tự trường trong cùng một nhóm.
Model (SLM) tự phân biệt ưu tiên với thông tin hồ sơ và câu hỏi kiến thức, trả priority_cost | priority_aid | priority_prestige trong changes;
không có quy tắc từ khóa nào đọc thay model. Kết quả chỉ gồm yếu tố học sinh nhắc tới; .NET ghép với ưu tiên hiện tại của họ.
"""

import unicodedata

# Khóa khớp thứ tự ba thanh của trang: 0 chi phí thấp, 1 học bổng nhiều, 2 danh tiếng
KEYS = ("cost", "aid", "prestige")
TEXT = {"cost": "chi phí thấp", "aid": "học bổng nhiều", "prestige": "danh tiếng"}

LEVEL_MAX = 4
# Khóa giữ chỗ trong changes khi tin chỉ nói về ưu tiên (ý scenario cần changes không rỗng)
PRIORITY_KEY = "priority"


def _plain(text: str) -> str:
    text = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


# Chữ thường, bỏ dấu: dùng chung cho các quy tắc đọc câu (ưu tiên, hỏi số liệu trường)
unaccent = _plain


def from_model(changes: list[dict]) -> dict[str, int]:
    """Ưu tiên model trả trong changes (factor = priority_cost | priority_aid | priority_prestige, value = 0..4). Giá trị sai thì bỏ."""
    found: dict[str, int] = {}
    for change in changes:
        key = str(change.get("factor", ""))
        if not key.startswith("priority_") or key.removeprefix("priority_") not in KEYS:
            continue
        try:
            level = int(str(change.get("value", "")).strip())
        except ValueError:
            continue
        if 0 <= level <= LEVEL_MAX:
            found[key.removeprefix("priority_")] = level
    return found


def label(priorities: dict[str, int]) -> str:
    """Nói lại bằng lời để học sinh kiểm tra hệ thống hiểu đúng."""
    return "ưu tiên " + ", ".join(
        f"{TEXT[k]} mức {priorities[k]}/{LEVEL_MAX}" for k in KEYS if k in priorities
    )
