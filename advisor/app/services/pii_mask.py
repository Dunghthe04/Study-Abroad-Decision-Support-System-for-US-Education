"""Che thông tin cá nhân trong chữ đọc từ bảng điểm trước khi gửi cho LLM (GB-06 trong SRS).

Che theo nhãn ("Họ và tên:", "Ngày sinh:", "Mã học sinh:", "Số CCCD:"...), cả dạng "Nhãn: giá trị" lẫn ô bảng
Markdown "| Nhãn | giá trị |"; thêm ngày dạng dd/mm/yyyy, email và dãy số dài (CCCD, số điện thoại, mã số).
Không đụng tới điểm, tên môn, "Lớp 10", "HK1", năm học "2023-2024" vì bước sau cần các thông tin này.
"""

from __future__ import annotations

import re
import unicodedata

NAME_MASK = "[HỌ TÊN]"
BIRTH_MASK = "[NGÀY SINH]"
ID_MASK = "[MÃ SỐ]"
CONTACT_MASK = "[LIÊN HỆ]"
DATE_MASK = "[NGÀY]"
EMAIL_MASK = "[EMAIL]"

# Nhãn viết không dấu, chữ thường (so trên bản không dấu của văn bản)
_LABELS: dict[str, str] = {
    "ho va ten": NAME_MASK,
    "ho ten": NAME_MASK,
    "ten hoc sinh": NAME_MASK,
    "ten sinh vien": NAME_MASK,
    "hoc sinh": NAME_MASK,
    "sinh vien": NAME_MASK,
    "full name": NAME_MASK,
    "student name": NAME_MASK,
    "name": NAME_MASK,
    "ngay thang nam sinh": BIRTH_MASK,
    "ngay sinh": BIRTH_MASK,
    "sinh ngay": BIRTH_MASK,
    "noi sinh": BIRTH_MASK,
    "date of birth": BIRTH_MASK,
    "place of birth": BIRTH_MASK,
    "dob": BIRTH_MASK,
    "ma so hoc sinh": ID_MASK,
    "ma so sinh vien": ID_MASK,
    "ma hoc sinh": ID_MASK,
    "ma sinh vien": ID_MASK,
    "ma hs": ID_MASK,
    "ma sv": ID_MASK,
    "mshs": ID_MASK,
    "mssv": ID_MASK,
    "ma so": ID_MASK,
    "so bao danh": ID_MASK,
    "sbd": ID_MASK,
    "student id": ID_MASK,
    "student no": ID_MASK,
    "student number": ID_MASK,
    "so cccd": ID_MASK,
    "cccd": ID_MASK,
    "so cmnd": ID_MASK,
    "cmnd": ID_MASK,
    "can cuoc cong dan": ID_MASK,
    "so dinh danh": ID_MASK,
    "ma dinh danh": ID_MASK,
    "ho chieu": ID_MASK,
    "passport": ID_MASK,
    "so dien thoai": CONTACT_MASK,
    "dien thoai": CONTACT_MASK,
    "sdt": CONTACT_MASK,
    "phone": CONTACT_MASK,
    "dia chi": CONTACT_MASK,
    "address": CONTACT_MASK,
    "email": CONTACT_MASK,
}
# Họ tên cha, mẹ, người giám hộ cũng là thông tin cá nhân: "Họ tên cha:", "Họ và tên mẹ:"
_OWNER = r"(?:\s+(?:cha|me|bo|phu huynh|nguoi giam ho|cua hoc sinh|cua sinh vien))?"
_LABEL_ALT = "|".join(sorted((re.escape(k) for k in _LABELS), key=len, reverse=True))
# "Nhãn:" trong dòng chữ, cho phép in đậm Markdown "**Họ và tên:**"
_COLON_LABEL_RE = re.compile(rf"(?<![a-z0-9])({_LABEL_ALT}){_OWNER}\s*\**\s*[:：]\s*\**\s*")
# Ô bảng chỉ chứa nhãn: "| Họ và tên | Nguyễn Văn A |"
_CELL_LABEL_RE = re.compile(rf"^\**\s*({_LABEL_ALT}){_OWNER}\s*\**\s*[:：]?\s*\**$")
# Giá trị dừng ở ô bảng kế tiếp, tab hoặc khoảng trắng dài (thường là cột kế bên trên cùng dòng)
_VALUE_END_RE = re.compile(r"\||\t| {2,}")

_DATE_RE = re.compile(r"(?<![\d.,])\d{1,2}\s*[/.\-]\s*\d{1,2}\s*[/.\-]\s*(?:19|20)\d{2}(?!\d)")
_EMAIL_RE = re.compile(r"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")
# CCCD 12 số, CMND 9 số, số điện thoại "0912 345 678", "+84 912.345.678". Nhóm sau phải có từ 3 chữ số
# để dòng điểm "7.2 4.2 6.7" hay "8 9 7 6" không bị coi là một dãy số
_LONG_NUMBER_RE = re.compile(r"(?<![\d.,])\+?\d{2,}(?:[ .]\d{3,})*(?!\d|[.,]\d)")
MIN_ID_DIGITS = 8
_YEAR_RE = re.compile(r"(?:19|20)\d{2}")


def mask_personal_info(text: str) -> str:
    """Trả về văn bản đã che thông tin cá nhân. Hàm thuần, không gọi mạng, không ghi log nội dung."""
    lines = [_mask_line(line) for line in text.splitlines()]
    masked = "\n".join(lines)
    masked = _EMAIL_RE.sub(EMAIL_MASK, masked)
    masked = _DATE_RE.sub(DATE_MASK, masked)
    return _LONG_NUMBER_RE.sub(_mask_long_number, masked)


def _mask_long_number(m: re.Match[str]) -> str:
    groups = re.split(r"[ .]", m.group().lstrip("+"))
    if sum(len(g) for g in groups) < MIN_ID_DIGITS:
        return m.group()
    # "2023 2024" là năm học, không phải mã số
    if len(groups) > 1 and all(_YEAR_RE.fullmatch(g) for g in groups):
        return m.group()
    return ID_MASK


def _mask_line(line: str) -> str:
    if "|" in line:
        line = _mask_table_cells(line)
    return _mask_colon_labels(line)


def _mask_table_cells(line: str) -> str:
    cells = line.split("|")
    for i in range(len(cells) - 1):
        label = _CELL_LABEL_RE.match(_plain_keep_length(cells[i]).strip())
        if label and cells[i + 1].strip():
            mask = _LABELS[label.group(1)]
            if cells[i + 1].strip() != mask:
                cells[i + 1] = f" {mask} "
    return "|".join(cells)


def _mask_colon_labels(line: str) -> str:
    plain = _plain_keep_length(line)
    matches = list(_COLON_LABEL_RE.finditer(plain))
    if not matches:
        return line
    out: list[str] = []
    pos = 0
    for i, m in enumerate(matches):
        if m.start() < pos:
            continue
        value_start = m.end()
        limit = matches[i + 1].start() if i + 1 < len(matches) else len(line)
        end = _VALUE_END_RE.search(line, value_start, limit)
        value_end = end.start() if end else limit
        if not line[value_start:value_end].strip():
            continue
        out.append(line[pos:value_start])
        out.append(_LABELS[m.group(1)])
        # Giữ lại khoảng trắng trước nhãn kế tiếp để dòng vẫn dễ đọc
        trailing = len(line[value_start:value_end]) - len(line[value_start:value_end].rstrip())
        pos = value_end - trailing
    out.append(line[pos:])
    return "".join(out)


def _plain_keep_length(text: str) -> str:
    """Bỏ dấu, chữ thường, giữ nguyên độ dài để vị trí khớp được trên chuỗi gốc."""
    chars: list[str] = []
    for ch in text:
        if ch in "đĐ":
            chars.append("d")
            continue
        base = unicodedata.normalize("NFD", ch)[0].lower()
        chars.append(base if len(base) == 1 else ch)
    return "".join(chars)
