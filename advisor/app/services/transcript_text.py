"""Dọn văn bản model đọc ảnh chép ra (bước A) trước khi đưa LLM bóc điểm (bước B).

Model OCR nhỏ hay chép bảng không kẻ ô thành chữ cách nhau bằng dấu cách:
    Môn Toán Lý Ngữ văn Sử Địa Ngoại ngữ ... TB môn HL HK
    HK1 7.2 4.2 6.7 7.6 6.6 5.7 ... 6.6 TB Tốt
Tên môn nhiều chữ làm LLM xếp lệch cột. Ở đây đếm số ô trong dòng điểm rồi ghép chữ tiêu đề
cho đủ số cột (dựa vào tên môn nhiều chữ thường gặp), đổi thành bảng Markdown có ô rõ ràng.
Không ghép khớp thì giữ nguyên văn bản, để bước B tự xử lý như cũ.
"""

from __future__ import annotations

import re
import unicodedata
from difflib import SequenceMatcher

# Tiêu đề cột nhiều chữ hay gặp trong học bạ / bảng điểm (viết không dấu)
MULTI_WORD_HEADERS = (
    "ngu van", "ngoai ngu", "cong nghe", "the duc", "gd cd", "gd qp", "gdqp an", "tb mon", "tb cm",
    "lich su", "dia ly", "dia li", "vat ly", "vat li", "hoa hoc", "sinh hoc", "tin hoc", "toan hoc",
    "tieng anh", "anh van", "am nhac", "my thuat", "hoc luc", "hanh kiem", "xep loai", "diem tb",
    "giao duc", "quoc phong", "the chat", "ca nam", "hoc ky",
)
MIN_PAIR_SIMILARITY = 0.8  # chịu được OCR sai dấu: "Thể dµc" ~ "the duc", "Ngữ vân" ~ "ngu van"
HEADER_LEADS = ("mon hoc", "mon", "subject", "course")

_VALUE_RE = re.compile(r"^(?:\d{1,2}(?:[.,]\d{1,2})?|đ|cđ|d|cd)$", re.IGNORECASE)
_NUMBER_RE = re.compile(r"^\d{1,2}(?:[.,]\d{1,2})?$")
_FENCE_RE = re.compile(r"^\s*`{3,}\s*$")
MIN_REPEAT_LINE = 15
REPEAT_RUN = 3


def plain(text: str) -> str:
    text = text.replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
    return re.sub(r"\s+", " ", text).strip().lower()


def tidy_transcription(text: str) -> str:
    return tabulate_spaced_tables(cut_repetition(text))


def cut_repetition(text: str) -> str:
    """Bỏ hàng rào ``` và cắt chỗ model bắt đầu chép lại cả trang (vài dòng dài liên tiếp đã gặp)."""
    lines = [ln for ln in text.splitlines() if not _FENCE_RE.match(ln)]
    seen: set[str] = set()
    run_start: int | None = None
    run = 0
    for i, line in enumerate(lines):
        key = line.strip()
        if len(key) >= MIN_REPEAT_LINE and key in seen:
            run_start = i if run == 0 else run_start
            run += 1
            if run >= REPEAT_RUN:
                lines = lines[:run_start]
                break
        elif key:
            run = 0
        if len(key) >= MIN_REPEAT_LINE:
            seen.add(key)
    return "\n".join(lines).strip()


def tabulate_spaced_tables(text: str) -> str:
    lines = text.splitlines()
    out: list[str] = []
    i = 0
    while i < len(lines):
        table = _try_table(lines, i)
        if table is None:
            out.append(lines[i])
            i += 1
            continue
        markdown, consumed = table
        out.extend(markdown)
        i += consumed
    return "\n".join(out)


def _try_table(lines: list[str], start: int) -> tuple[list[str], int] | None:
    header = lines[start].split()
    lead = _header_lead(header)
    if lead == 0 or "|" in lines[start]:
        return None

    rows: list[tuple[str, list[str]]] = []
    for line in lines[start + 1:]:
        row = _split_row(line)
        if row is None or (rows and len(row[1]) != len(rows[0][1])):
            break
        rows.append(row)
    if not rows:
        return None

    columns = _segment_header(header[lead:], len(rows[0][1]))
    if columns is None:
        return None
    md = [
        "| " + " | ".join([" ".join(header[:lead]), *columns]) + " |",
        "|" + "---|" * (len(columns) + 1),
        *("| " + " | ".join([label, *values]) + " |" for label, values in rows),
    ]
    return md, len(rows) + 1


_GRADE_LINE_RE = re.compile(r"^\W*lop\W*(6|7|8|9|10|11|12)\b")
_TERM_LABEL_RE = re.compile(r"^(?:hk\s*\w|hoc ky|ky\b|ca nam|semester)")


def read_term_tables(text: str) -> list[dict]:
    """Đọc thẳng bảng "| Môn | Toán | ... |" có dòng kỳ (HK1, HK2, Cả năm) mà không cần LLM.

    Trả cùng dạng JSON bước B: [{"grade", "term", "subjects": [{"name", "score"}]}]; lớp lấy từ dòng
    "Lớp 10:" gần nhất phía trên. Hậu kiểm (tên môn, thang điểm, bỏ Cả năm) để code bước B lo như cũ.
    """
    terms: list[dict] = []
    grade = ""
    header: list[str] | None = None
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped.startswith("|"):
            header = None
            if m := _GRADE_LINE_RE.match(plain(stripped)):
                grade = f"Lớp {m.group(1)}"
            continue
        cells = [c.strip() for c in stripped.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells):
            continue
        if header is None:
            header = cells if _header_lead(cells[0].split()) else None
            continue
        if len(cells) != len(header) or not _TERM_LABEL_RE.match(plain(cells[0])):
            continue
        terms.append({
            "grade": grade,
            "term": cells[0],
            "subjects": [{"name": name, "score": score} for name, score in zip(header[1:], cells[1:], strict=True)],
        })
    return terms


def _header_lead(words: list[str]) -> int:
    """Số chữ đầu dòng là nhãn cột đầu ("Môn", "Môn học"); 0 nếu không phải dòng tiêu đề."""
    joined = plain(" ".join(words[:2]))
    for lead in HEADER_LEADS:
        if joined == lead or joined.startswith(lead + " "):
            return len(lead.split())
    return 0


def _split_row(line: str) -> tuple[str, list[str]] | None:
    """'Cả năm 6.9 6.9 D ... Khá Tốt' -> ('Cả năm', [...]). Nhãn là các chữ trước số đầu tiên."""
    tokens = line.split()
    first = next((k for k, t in enumerate(tokens) if _NUMBER_RE.match(t)), None)
    if first is None or first == 0 or first > 3:
        return None
    values = tokens[first:]
    if sum(1 for v in values if _VALUE_RE.match(v)) < max(3, len(values) // 2):
        return None
    return " ".join(tokens[:first]), values


def _segment_header(words: list[str], columns: int) -> list[str] | None:
    """Ghép (len(words) - columns) cặp chữ liền nhau giống tên nhiều chữ nhất; không đủ cặp tin cậy thì None."""
    merges = len(words) - columns
    if merges < 0:
        return None
    candidates = sorted(
        ((_pair_score(words[k], words[k + 1]), k) for k in range(len(words) - 1)),
        reverse=True,
    )
    chosen: set[int] = set()
    for score, k in candidates:
        if len(chosen) == merges or score < MIN_PAIR_SIMILARITY:
            break
        if k - 1 in chosen or k in chosen or k + 1 in chosen:
            continue
        chosen.add(k)
    if len(chosen) != merges:
        return None

    out: list[str] = []
    k = 0
    while k < len(words):
        if k in chosen:
            out.append(f"{words[k]} {words[k + 1]}")
            k += 2
        else:
            out.append(words[k])
            k += 1
    return out


def _pair_score(a: str, b: str) -> float:
    pair = plain(f"{a} {b}")
    return max(SequenceMatcher(None, pair, known).ratio() for known in MULTI_WORD_HEADERS)
