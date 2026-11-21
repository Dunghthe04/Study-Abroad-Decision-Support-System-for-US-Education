"""Các phép kiểm dùng chung cho lời Claude viết từ dữ kiện code đã tính (goal-seeking, so sánh, gợi ý trường, kịch bản).

Mỗi writer ghép các phép kiểm nó cần trong check_answer của mình; từ cấm và cách đọc số chỉ nằm ở đây nên thêm một từ cấm
là áp dụng cho mọi writer. Phép kiểm sai thì ném ValueError với một câu tiếng Việt nêu rõ lỗi: câu đó được gửi lại cho
Claude ở lần gọi thứ hai (xem skill_call.RETRY_NOTE) nên phải đủ rõ để model sửa đúng chỗ.
"""

import re
from collections.abc import Collection, Iterable
from typing import Protocol

NUMBER = re.compile(r"\d+(?:[.,]\d+)*")
THOUSANDS = re.compile(r"\d{1,3}(?:[.,]\d{3})+")

# Lời hứa kết quả tuyển sinh: hệ thống chỉ so với sinh viên đã trúng tuyển, không nói được khả năng đỗ
PROMISES = (
    "chắc chắn đậu",
    "chắc chắn đỗ",
    "đảm bảo đậu",
    "đảm bảo đỗ",
    "chắc chắn được nhận",
    "sẽ đậu",
    "sẽ đỗ",
    "100%",
)


# Các số trong một đoạn chữ. "40.000" và "40,000" là bốn mươi nghìn, "7,5" là bảy phẩy năm
def numbers(text: str) -> list[float]:
    return [
        float(re.sub(r"[.,]", "", token)) if THOUSANDS.fullmatch(token) else float(token.replace(",", "."))
        for token in NUMBER.findall(text)
    ]


# Mọi số trong lời phải có trong tập số được phép. source: tên nguồn dữ liệu, dùng trong câu báo lỗi ("dữ kiện", "số liệu"...)
def check_numbers(text: str, allowed: Collection[float], source: str) -> None:
    extra = sorted(set(numbers(text)) - set(allowed))
    if extra:
        raise ValueError(f"lời nhắc số không có trong {source}: {extra}")


def check_no_promise(text: str) -> None:
    promised = [p for p in PROMISES if p in text.lower()]
    if promised:
        raise ValueError(f"lời hứa kết quả tuyển sinh: {promised}")


# words: các cụm bị cấm riêng của từng writer (phán trường nào tốt hơn, xếp hạng trường...); message: câu báo lỗi
def check_no_verdict(text: str, words: Iterable[str], message: str) -> None:
    judged = [w for w in words if w in text.lower()]
    if judged:
        raise ValueError(f"{message}: {judged}")


# Lời phải nhắc đủ các tên. what: mô tả nhóm tên đó, dùng trong câu báo lỗi ("mọi trường đang hiện trong danh sách"...)
def check_mentions_all(text: str, names: Iterable[str], what: str) -> None:
    lowered = text.lower()
    missing = [name for name in names if name.lower() not in lowered]
    if missing:
        raise ValueError(f"lời phải nhắc đủ {what}, thiếu: {missing}")


# Bỏ các tên trường hợp lệ (known) khỏi lời rồi tìm tên trường khác trong others: nhắc tới là sai (có thể nhầm hoặc bịa).
# Bỏ tên dài trước để tên ngắn nằm trong tên dài hợp lệ ("Washington University" trong "Washington University in St. Louis")
# không bị coi là trường lạ
def check_no_strangers(text: str, known: Collection[str], others: Iterable[str]) -> None:
    rest = text.lower()
    for name in sorted(set(known), key=len, reverse=True):
        rest = rest.replace(name.lower(), " ")
    strangers = [name for name in others if name.lower() in rest]
    if strangers:
        raise ValueError(f"lời nhắc tên trường ngoài danh sách: {strangers[:3]}")


class Note(Protocol):
    intro: str
    points: list[str]
    closing: str


# Nối các phần Claude viết thành tin nhắn: câu mở, các ý (mỗi ý một dòng gạch đầu dòng), câu kết
def render_note(result: Note) -> str:
    lines = [result.intro.strip()]
    if result.points:
        lines.append("")
        lines += [f"- {point.strip()}" for point in result.points if point.strip()]
    if result.closing.strip():
        lines += ["", result.closing.strip()]
    return "\n".join(lines)
