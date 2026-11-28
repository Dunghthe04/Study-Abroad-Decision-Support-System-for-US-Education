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
    "ngu van",
    "ngoai ngu",
    "cong nghe",
    "the duc",
    "gd cd",
    "gd qp",
    "gdqp an",
    "tb mon",
    "tb cm",
    "lich su",
    "dia ly",
    "dia li",
    "vat ly",
    "vat li",
    "hoa hoc",
    "sinh hoc",
    "tin hoc",
    "toan hoc",
    "tieng anh",
    "anh van",
    "am nhac",
    "my thuat",
    "hoc luc",
    "hanh kiem",
    "xep loai",
    "diem tb",
    "giao duc",
    "quoc phong",
    "the chat",
    "ca nam",
    "hoc ky",
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
    for line in lines[start + 1 :]:
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


# --- keep_score_lines: chỉ gửi bước B những dòng cần để đọc điểm (danh sách trắng) ---
# Chữ (không dấu) cho biết dòng thuộc bảng điểm. Chữ "mạnh" đứng cạnh một con số là đủ tin dòng có điểm; chữ dễ trùng
# tên người / địa chỉ (văn, anh, địa, sinh, hoa...) chỉ được tính khi đếm chữ của dòng tiêu đề bảng.
_STRONG_SCORE_WORDS = frozenset(
    "mon subject course term semester score grade gpa credit credits diem tb hk hl toan gdcd gdqp mathematics math "
    "physics chemistry biology literature english history geography informatics".split()
)
_SCORE_WORDS = _STRONG_SCORE_WORDS | frozenset("ly li hoa sinh van su dia anh tin tieng science economics".split())
_SCORE_PHRASE_RE = re.compile(r"\b(?:" + "|".join(MULTI_WORD_HEADERS) + r")\b")
# Lớp / kỳ / năm học ở bất kỳ đâu trong dòng: "Họ và tên: [HỌ TÊN]   Lớp: 10A1" giữ lại "Lớp: 10A1" để biết lớp.
# "Học kỳ" OCR hay chép sai chữ "kỳ" (Hoc kg, Hoc kj, Hoc ki): nhận "hoc k" + tối đa 1 chữ, không liệt kê từng kiểu sai
_CONTEXT_RE = re.compile(
    r"(?<![a-z0-9])(?:lop\W*(?:6|7|8|9|10|11|12)(?![0-9])|hk\s*[12i]+(?![a-z0-9])|hoc k[a-z]?(?![a-z])|ca nam|nam hoc|semester"
    r"|(?:fall|spring|summer)\s*20\d\d)"
)
_SCORE_TOKEN_RE = re.compile(r"(?<![\w.,])(?:\d{1,2}(?:[.,]\d{1,2})?)(?![\w.,]?\d)")
MIN_SCORES_PLAIN_LINE = 3  # "Số 12, ngõ 5" (địa chỉ) có 2 số, không được lọt
MIN_HEADER_WORDS = 3  # "Trần Văn Anh" trùng 2 chữ tên môn (văn, anh), tiêu đề bảng có nhiều hơn
# Tiêu đề bảng gần như toàn chữ môn/cột; câu văn xuôi chỉ tình cờ trùng vài chữ ("cô Di Li post toàn bộ số điểm...")
MIN_HEADER_DENSITY = 1 / 4
# Họ tên người: 2-4 chữ đều viết hoa đầu, không chữ nào là tên môn ("Vũ Thị Hà"; "Tiếng Anh", "Ngoại ngữ" không phải)
NAME_WORDS = range(2, 5)
# Xếp loại học lực / hạnh kiểm đứng cuối dòng điểm ("Khá Khá", "TB Tốt", "Giỏi Tốt HSG"): bộ từ cố định theo quy chế
_RATING_WORDS = frozenset("gioi kha tot yeu kem dat chua trung binh xuat sac".split())
# Model OCR hay chép bảng dọc: mỗi ô một dòng ("Toán" / "8,2" / "8,2" / "8,2"). Các dòng dưới đây giữ cho kiểu đó.
_LONE_VALUES = frozenset("t k tb y g x | - ⊗".split())  # xếp loại viết tắt, ô trống OCR chép thành | hoặc -
_COLUMN_WORD_RE = re.compile(r"(?<![a-z])(?:hk\w*|dtb\w*|ddg\w*|tbm\w*|cn|kqht)(?![a-z])")  # HKý I, CN, ĐTBmhkI, ĐĐGtx
MAX_LABEL_WORDS = 6  # nhãn hàng của bảng dọc ngắn ("Lich sú và dja lý"); câu dài trước một con số không được tính


def keep_score_lines(text: str) -> str:
    """Giữ dòng bảng điểm, dòng lớp/kỳ/năm học, dòng chữ có điểm; bỏ quốc hiệu, tên trường, họ tên, lời phê, chữ ký.

    Thứ không gửi thì không thể lộ, nên đây là lớp che cuối trước LLM. Dòng bị bỏ thay bằng dòng trống để hai bảng liền
    nhau vẫn tách ra như cũ (read_term_tables coi dòng không bắt đầu bằng "|" là hết bảng). Bảng chép dọc giữ dòng chỉ có
    điểm, dòng tiêu đề cột ngắn và dòng nhãn ngắn đứng ngay trước dòng điểm (tên môn); tên giáo viên đứng sau điểm bị bỏ.
    """
    lines = text.splitlines()
    out: list[str] = []
    for i, line in enumerate(lines):
        next_line = next((ln for ln in lines[i + 1 :] if ln.strip()), "")
        label = _short_label(line) and not _looks_like_name(line) and _lone_value_line(next_line)
        useful = _useful_line(line) or _vertical_cell(line) or label
        kept = _cut_trailing_name(line) if useful else _context_tail(line)
        if kept or (out and out[-1]):
            out.append(kept)
    return "\n".join(out).strip()


def _context_tail(line: str) -> str:
    """Dòng không có điểm nhưng có lớp/kỳ/năm học: chỉ giữ từ chữ "Lớp…" trở đi.

    OCR hay làm mất nhãn "Họ và tên:", còn lại "Nguyễn Văn A  Lớp: 12A1  Năm học 2010-2011": cắt phần trước
    để không gửi họ tên không nhãn. Không có lớp/kỳ/năm học thì bỏ cả dòng.
    """
    tokens = line.split()
    for i in range(len(tokens)):
        if _CONTEXT_RE.match(plain(" ".join(tokens[i:]))):
            return " ".join(tokens[i:])
    return ""


def _lone_value_line(line: str) -> bool:
    tokens = line.split()
    return bool(tokens) and all(_VALUE_RE.match(t) or plain(t) in _LONE_VALUES for t in tokens)


def _short_label(line: str) -> bool:
    words = line.split()
    return 0 < len(words) <= MAX_LABEL_WORDS and "|" not in line


def _cut_trailing_name(line: str) -> str:
    """Tên giáo viên bộ môn hay đứng cuối dòng điểm ("Toán 6,3 4,9 5,4 Hoàng Lưu"): cắt phần sau số cuối nếu giống họ tên."""
    if line.lstrip().startswith("|"):
        return line
    scores = list(_SCORE_TOKEN_RE.finditer(line))
    if not scores or not _looks_like_name(line[scores[-1].end() :]):
        return line
    return line[: scores[-1].end()]


def _looks_like_name(line: str) -> bool:
    words = re.findall(r"[^\W\d_]+", line)
    if len(words) not in NAME_WORDS or not all(w[0].isupper() and not w.isupper() for w in words):
        return False  # chữ viết tắt in hoa (HSG, TB, HL) không phải họ tên
    flat = plain(" ".join(words))
    return not (set(flat.split()) & (_SCORE_WORDS | _RATING_WORDS) or _SCORE_PHRASE_RE.search(flat))


def _vertical_cell(line: str) -> bool:
    """Ô của bảng chép dọc: chỉ có điểm, tiêu đề cột ngắn (HKý I, CN), hoặc tên môn nhiều chữ đứng riêng (Ngoại ngữ)."""
    if _lone_value_line(line):
        return True
    if not _short_label(line):
        return False
    flat = plain(line)
    return bool(_COLUMN_WORD_RE.search(flat) or (len(line.split()) <= 4 and _SCORE_PHRASE_RE.search(flat)))


def _useful_line(line: str) -> bool:
    stripped = line.strip()
    if not stripped:
        return False
    flat = plain(stripped)
    tokens = re.findall(r"[a-z]+", flat)
    words = set(tokens)
    phrases = set(_SCORE_PHRASE_RE.findall(flat))
    score_words = len(words & _SCORE_WORDS) + len(phrases)
    # Mật độ đếm cả chữ lặp và chữ cột ("Điểm kiểm tra HS I Điểm kiểm tra HS II HK TBM")
    column_tokens = sum(t in _SCORE_WORDS or bool(_COLUMN_WORD_RE.fullmatch(t)) for t in tokens) + len(phrases)
    header_words = score_words >= MIN_HEADER_WORDS and column_tokens >= MIN_HEADER_DENSITY * len(tokens)
    if stripped.startswith("|"):
        cells = [c.strip() for c in stripped.strip("|").split("|")]
        if _CONTEXT_RE.search(flat) or all(set(c) <= set("-: ") for c in cells) or any(_VALUE_RE.match(c) for c in cells if c):
            return True
        return bool(_header_lead(cells[0].split())) or header_words
    if _header_lead(stripped.split()) or header_words:
        return True
    scores = len(_SCORE_TOKEN_RE.findall(stripped))
    return scores >= MIN_SCORES_PLAIN_LINE or (scores >= 1 and bool(phrases or words & _STRONG_SCORE_WORDS))


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
        terms.append(
            {
                "grade": grade,
                "term": cells[0],
                "subjects": [
                    {"name": name, "score": score} for name, score in zip(header[1:], cells[1:], strict=True)
                ],
            }
        )
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
