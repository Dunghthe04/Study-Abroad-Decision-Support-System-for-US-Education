"""Ưu tiên của học sinh nói trong chat ("ưu tiên học bổng", "quan tâm chi phí thấp nhất").

Trang gợi ý có 3 thanh ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng), mỗi thanh 0..4, chỉ đổi thứ tự trường trong cùng một nhóm.
SLM chưa được huấn luyện cho ý này nên đọc bằng quy tắc: cần có tên yếu tố VÀ một từ chỉ mức quan tâm,
để "học bổng là gì" hay "em cần học bổng 50%" (thông tin hồ sơ) không bị hiểu nhầm là đổi ưu tiên.
Kết quả chỉ gồm yếu tố học sinh nhắc tới; .NET ghép với ưu tiên hiện tại của họ.
"""

import re
import unicodedata

# Khóa khớp thứ tự ba thanh của trang: 0 chi phí thấp, 1 học bổng nhiều, 2 danh tiếng
KEYS = ("cost", "aid", "prestige")
TEXT = {"cost": "chi phí thấp", "aid": "học bổng nhiều", "prestige": "danh tiếng"}

_TOPICS = {
    "cost": r"chi phi|hoc phi|\bre\b|gia re|tiet kiem|it ton kem|tai chinh",
    "aid": r"hoc bong",
    "prestige": r"danh tieng|uy tin|xep hang|ranking|truong top|top truong",
}

# Từ chỉ mức quan tâm. Cụm phủ định và giảm mức kiểm trước vì chúng cũng chứa từ "quan tâm"
_NONE = r"khong (con )?(quan tam|can|uu tien|quan trong)|bo qua|khong can nhac"
_LOW = r"it (quan tam|uu tien)|bot (quan tam|uu tien)|giam (muc )?uu tien|khong qua (quan trong|quan tam)"
_CARE = r"uu tien|quan tam|chu trong|coi trong|quan trong|chuong|can nhac"
# Mức cao nhất cần có từ chỉ mức quan tâm đi kèm, để "trường học bổng nhiều nhất" (câu tìm kiếm) không bị tính
_TOP = r"\bnhat\b|hang dau|tren het|tuyet doi|\b(rat|het suc|cuc ky|cuc)\b"

LEVEL_MAX = 4
# Khóa giữ chỗ trong changes khi tin chỉ nói về ưu tiên (ý scenario cần changes không rỗng)
PRIORITY_KEY = "priority"


def _plain(text: str) -> str:
    text = unicodedata.normalize("NFD", text.lower().replace("đ", "d"))
    return "".join(c for c in text if unicodedata.category(c) != "Mn")


# Chữ thường, bỏ dấu: dùng chung cho các quy tắc đọc câu (ưu tiên, hỏi số liệu trường)
unaccent = _plain


def detect_priorities(message: str) -> dict[str, int]:
    """Yếu tố học sinh nhắc tới kèm mức 0..4; rỗng nếu không có ý nào về ưu tiên."""
    found: dict[str, int] = {}
    for clause in re.split(r"[,;.!?]|\bnhung\b|\bcon\b", _plain(message)):
        topics = [key for key, pattern in _TOPICS.items() if re.search(pattern, clause)]
        if not topics:
            continue
        if re.search(_NONE, clause):
            level = 0
        elif re.search(_LOW, clause):
            level = 1
        elif re.search(_CARE, clause):
            level = 4 if re.search(_TOP, clause) else 3
        else:
            level = None
        if level is None:
            continue
        for key in topics:
            found[key] = level
    return found


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
