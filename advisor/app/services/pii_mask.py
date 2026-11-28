"""Che thông tin cá nhân trước khi gửi cho LLM (GB-06 trong SRS). Ba mức:

- mask_known_values: chữ đọc từ BẢNG ĐIỂM, chạy trước tiên. Che đúng họ tên, ngày sinh, email, SĐT mà .NET gửi kèm
  (thông tin đã có trong DB của chủ hồ sơ và phụ huynh), nên bắt được cả tên đứng riêng không có nhãn.

- mask_personal_info: chữ đọc từ BẢNG ĐIỂM và chữ ghi log. Che mạnh (mọi ngày, mọi dãy số dài).
- mask_outgoing: mọi nội dung gửi sang Claude, gọi ở một cửa duy nhất (ClaudeClient). Chỉ che thứ nhận ra chắc chắn, vì
  câu chat và hồ sơ có số tiền, điểm, ngày thi mà Claude cần để tư vấn. Họ tên viết tự do trong câu không nhận ra được.

Mức bảng điểm che theo nhãn ("Họ và tên:", "Ngày sinh:", "Mã học sinh:", "Số CCCD:"...), cả dạng "Nhãn: giá trị" lẫn ô bảng
Markdown "| Nhãn | giá trị |"; thêm ngày dạng dd/mm/yyyy, email và dãy số dài (CCCD, số điện thoại, mã số).
Không đụng tới điểm, tên môn, "Lớp 10", "HK1", năm học "2023-2024" vì bước sau cần các thông tin này.
"""

from __future__ import annotations

import re
import unicodedata
from datetime import date

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


# --- Mức gửi đi (mask_outgoing) ---
# Di động 0[35789] + 8 số, máy bàn 02 + 9 số, đầu +84 thay cho 0; cho phép cách, chấm, gạch giữa các số.
# Số tiền không bắt đầu bằng 0 nên không dính
_PHONE_RE = re.compile(
    r"(?<![\d.,])(?:(?:\(\+84\)|\+84)[ .\-]?|0)(?:[35789](?:[ .\-]?\d){8}|2(?:[ .\-]?\d){9})(?!\d|[.,]\d)"
    # "84912345678" không có dấu cộng: chỉ nhận khi viết liền đúng 11 chữ số, để không trúng số tiền viết tách nhóm
    r"|(?<![\d.,])84[35789]\d{8}(?!\d|[.,]\d)"
)
_CITIZEN_ID_RE = re.compile(r"(?<![\d.,])0\d{11}(?!\d|[.,]\d)")
# Ngày sinh chỉ khi câu nói rõ là ngày sinh, để không che ngày thi (so trên bản không dấu)
_BIRTH_RE = re.compile(
    r"(?<![a-z0-9])(?:ngay thang nam sinh|ngay sinh|sinh ngay|(?:em|minh|con) sinh|date of birth|dob)"
    r"(?:\s+(?:cua\s+)?(?:em|minh|con|toi))?(?:\s+la)?\s*[:：]?\s*"
    r"(?P<value>\d{1,2}\s*[/.\-]\s*\d{1,2}\s*[/.\-]\s*(?:19|20)\d{2}"
    r"|\d{1,2}\s+thang\s+\d{1,2}\s+nam\s+(?:19|20)\d{2})"
)
# Nhãn định danh có dấu hai chấm. Không dùng "name", "email", "học sinh" như mức bảng điểm vì dễ trúng chữ thường.
# Giá trị dừng ở dấu phẩy, chấm, ngoặc kép... để không nuốt phần còn lại của câu hay của JSON
_OUTGOING_LABELS = (
    "ho va ten",
    "ho ten",
    "dia chi",
    "so dien thoai",
    "dien thoai",
    "sdt",
    "so cccd",
    "cccd",
    "so cmnd",
    "cmnd",
    "can cuoc cong dan",
    "so dinh danh",
    "ma dinh danh",
    "ho chieu",
    "passport",
    "ngay thang nam sinh",
    "ngay sinh",
)
_OUTGOING_LABEL_RE = re.compile(
    rf"(?<![a-z0-9])(?P<label>{'|'.join(sorted(_OUTGOING_LABELS, key=len, reverse=True))}){_OWNER}"
    r"\s*[:：]\s*(?P<value>[^\s,;.|\[\]\"\\{}][^,;.|\n\[\]\"\\{}]{0,59})"
)


def mask_outgoing(text: str) -> str:
    """Che những thông tin cá nhân nhận ra chắc chắn trong nội dung gửi sang LLM bên ngoài. Hàm thuần."""
    text = unicodedata.normalize("NFC", text)
    text = _EMAIL_RE.sub(EMAIL_MASK, text)
    text = _mask_group(text, _BIRTH_RE, lambda m: BIRTH_MASK)
    text = _CITIZEN_ID_RE.sub(ID_MASK, text)
    text = _PHONE_RE.sub(CONTACT_MASK, text)
    return _mask_group(text, _OUTGOING_LABEL_RE, lambda m: _LABELS[m["label"]])


# Tìm trên bản không dấu (cùng độ dài), thay đúng đoạn "value" trên chữ gốc
def _mask_group(text: str, pattern: re.Pattern[str], mask_of) -> str:
    out: list[str] = []
    pos = 0
    for m in pattern.finditer(_plain_keep_length(text)):
        start, end = m.span("value")
        out += [text[pos:start], mask_of(m)]
        pos = end
    return "".join(out) + text[pos:]


# --- Mức giá trị đã biết (mask_known_values) ---
# Chữ hay gặp trong tên môn: tên gọi chỉ gồm các chữ này ("Văn Anh") thì không che riêng, kẻo trúng "Ngữ văn Tiếng Anh"
_SUBJECT_WORDS = frozenset(
    "toan van anh ly li hoa sinh su dia tin hoc gdcd cong nghe the duc quoc phong ngu tieng phap nhat trung".split()
)


def mask_known_values(
    text: str,
    names: list[str] | tuple[str, ...] = (),
    date_of_birth: date | None = None,
    email: str | None = None,
    phone: str | None = None,
) -> str:
    """Che đúng họ tên, ngày sinh, email, số điện thoại mà hệ thống đã biết của chủ hồ sơ. Hàm thuần, không log giá trị.

    Bắt được tên đứng riêng không có nhãn (đầu trang, lời phê), chỗ regex theo nhãn bỏ sót. Tên so trên bản không dấu,
    chữ thường; che cả họ tên và tên gọi (2 chữ cuối) nhưng không bao giờ che một chữ đơn lẻ, vì "Anh", "Văn" trùng tên môn.
    """
    text = unicodedata.normalize("NFC", text)
    for pattern in _name_patterns(names):
        text = _replace_on_plain(text, pattern, NAME_MASK)
    if date_of_birth is not None:
        text = _replace_on_plain(text, _birth_pattern(date_of_birth), BIRTH_MASK)
    if email and (email := email.strip()):
        text = re.sub(re.escape(email), EMAIL_MASK, text, flags=re.IGNORECASE)
    if phone_pattern := _phone_pattern(phone):
        text = phone_pattern.sub(CONTACT_MASK, text)
    return text


def _name_patterns(names) -> list[re.Pattern[str]]:
    variants: set[tuple[str, ...]] = set()
    for name in names:
        words = tuple(_plain_keep_length(unicodedata.normalize("NFC", name or "")).split())
        if len(words) < 2:
            continue
        variants.add(words)
        given = words[-2:]
        if not all(w in _SUBJECT_WORDS for w in given):
            variants.add(given)
    # Dài trước để "Nguyễn Văn Minh Anh" được che trọn trước khi xét "Minh Anh"
    return [
        re.compile(r"(?<![a-z0-9])" + r"\s+".join(re.escape(w) for w in words) + r"(?![a-z0-9])")
        for words in sorted(variants, key=len, reverse=True)
    ]


def _birth_pattern(born: date) -> re.Pattern[str]:
    d, m, y = rf"0?{born.day}", rf"0?{born.month}", str(born.year)
    sep = r"\s*[/.\-]\s*"
    return re.compile(
        rf"(?<!\d)(?:{d}{sep}{m}{sep}{y}|{y}{sep}{m}{sep}{d})(?!\d)"
        rf"|(?<![a-z0-9])(?:ngay\s+)?{d}\s+thang\s+{m}\s+(?:nam\s+)?{y}(?!\d)"
    )


def _phone_pattern(phone: str | None) -> re.Pattern[str] | None:
    digits = re.sub(r"\D", "", phone or "")
    if digits.startswith("84") and len(digits) == 11:
        digits = "0" + digits[2:]
    if len(digits) < 9 or not digits.startswith("0"):
        return None
    local = r"[ .\-]?".join(digits[1:])
    return re.compile(rf"(?<![\d.,])(?:0|\(?\+?84\)?[ .\-]?)[ .\-]?{local}(?!\d|[.,]\d)")


def _replace_on_plain(text: str, pattern: re.Pattern[str], mask: str) -> str:
    out: list[str] = []
    pos = 0
    for m in pattern.finditer(_plain_keep_length(text)):
        out += [text[pos : m.start()], mask]
        pos = m.end()
    return "".join(out) + text[pos:]


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
