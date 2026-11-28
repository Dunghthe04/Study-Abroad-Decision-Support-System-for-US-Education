"""Kiểm tra JSON Claude trả ở bước B đọc bảng điểm, trước khi hậu kiểm và ghép kỳ.

Hàm thuần, không gọi AI. Mỗi lỗi là một dòng cụ thể (luật, ô nào, sai gì, đúng là gì): dòng này được gửi
lại cho Claude ở lần thử thứ 2, và dùng để bỏ đúng các ô lỗi nếu lần 2 vẫn trượt (giữ các ô đã đạt).

Luật (xem docs/KE_HOACH_OCR_SO_SANH_VA_KIEM_TRA_TIENG_VIET.md mục 2.2):
- R1 JSON đúng cấu trúc PARSE_SCHEMA
- R2 tên môn có trong danh sách chuẩn thì phải đúng dấu; tên gần giống tên chuẩn (chép sai chữ) là lỗi
- R3 tên môn đúng chữ tiếng Việt: không ký tự lạ / mojibake, mỗi âm tiết tối đa một dấu thanh
- R4 tên môn là tiếng Việt (không còn tên tiếng Anh); nguồn tiếng Anh phải kèm name_original có trong văn bản
- R4b môn tiếng Anh có trong SUBJECT_MAP phải dịch đúng tên chuẩn (Chemistry -> Hóa học)
- R5 điểm có nguyên văn trong văn bản, trong khoảng 0-10
- R6 không bỏ sót quá nhiều: số ô trả về so với số thập phân trong văn bản
- R7 "kind" của kỳ hợp lệ (hk1 / hk2 / ca_nam / khac) và không trái với chữ tên kỳ; không ép đổi tên kỳ
"""

from __future__ import annotations

import copy
import difflib
import json
import re
import unicodedata
from dataclasses import dataclass
from typing import Any

# Dấu thanh tiếng Việt (dạng tổ hợp sau NFD): huyền, sắc, ngã, hỏi, nặng
_TONE_MARKS = {"\u0300", "\u0301", "\u0303", "\u0309", "\u0323"}
# Dấu phụ của nguyên âm: mũ (â ê ô), trăng (ă), móc (ơ ư)
_VOWEL_MARKS = {"\u0302", "\u0306", "\u031b"}
_ALLOWED_PUNCT = set(" -&()/.,+'")
_DECIMAL_RE = re.compile(r"\d+[.,]\d+")
_SCORE_RE = re.compile(r"\d+(?:[.,]\d+)?")

# R6: đo trên 9 ảnh học bạ thật (benchmark 22/11), số ô điểm đúng / số thập phân 0-10 trong văn bản glm-ocr
# dao động 0,06-3,67 (văn bản còn điểm thành phần, dòng Cả năm, cột TB...). Ngưỡng 80% trượt oan gần hết ảnh,
# nên chỉ bắt trường hợp bỏ sót rõ ràng: trả dưới 20% số thập phân, và chỉ xét khi văn bản có từ 8 số trở lên.
MIN_CELL_RATIO = 0.2
MIN_SOURCE_DECIMALS = 8
FUZZY_SUBJECT_RATIO = 0.85


@dataclass(frozen=True)
class Issue:
    rule: str
    message: str
    term_index: int | None = None
    subject_index: int | None = None


def _plain(text: str) -> str:
    text = text.replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
    return re.sub(r"\s+", " ", text).strip().lower()


def _spelling_problem(name: str) -> str | None:
    """R3: trả lý do nếu tên có ký tự không phải chữ tiếng Việt hoặc âm tiết mang nhiều dấu thanh."""
    for word in unicodedata.normalize("NFD", name).split():
        tones = 0
        for ch in word:
            if ch in _TONE_MARKS:
                tones += 1
            elif ch in _VOWEL_MARKS or ch.isdigit() or ch in _ALLOWED_PUNCT:
                continue
            elif not (("a" <= ch.lower() <= "z") or ch in "đĐ"):
                return f"có ký tự lạ '{ch}'"
        if tones > 1:
            return f"âm tiết '{unicodedata.normalize('NFC', word)}' có {tones} dấu thanh"
    return None


def _looks_untranslated(name: str) -> bool:
    """R4: không có chữ nào mang dấu tiếng Việt và không phải chữ viết tắt in hoa (GDCD, GDQP-AN, KHTN)."""
    if any(not ch.isascii() for ch in name):
        return False
    words = re.findall(r"[A-Za-z]+", name)
    return bool(words) and not all(w.isupper() for w in words)


class _Subjects:
    """Tra tên môn theo SUBJECT_MAP của transcript_reader (nạp khi dùng để tránh import vòng)."""

    def __init__(self) -> None:
        from app.services.transcript_reader import SUBJECT_MAP, TranscriptReader

        self.map = SUBJECT_MAP
        self.resolve_term = TranscriptReader._resolve_term
        self.normalize = TranscriptReader._normalize_subject_name
        self.vietnamese = {k: v for k, v in SUBJECT_MAP.items() if not k.isascii()}
        self.plain_map = {_plain(k): v for k, v in self.vietnamese.items()}
        # Mẫu dò gần đúng: tên có dấu, đủ dài để "sử", "lý" không khớp bừa
        self.fuzzy = {_plain(k): v for k, v in self.vietnamese.items() if len(k) >= 4}

    def check(self, name: str, original: str, language: str, plain_source: str) -> tuple[str, str] | None:
        if (problem := _spelling_problem(name)) is not None:
            return "R3", f'Tên môn "{name}" sai chữ tiếng Việt ({problem}).'
        lower = unicodedata.normalize("NFC", name).strip().lower()
        if _looks_untranslated(name):
            hint = self.map.get(lower) or self.map.get(original.lower())
            tail = f' (đúng là "{hint}")' if hint else ""
            return "R4", f'Tên môn "{name}" chưa phải tiếng Việt có dấu, cần dịch / viết lại{tail}.'
        if language == "en" and not original:
            return (
                "R4",
                f'Môn "{name}": văn bản gốc tiếng Anh, cần ghi kèm "name_original" là tên trong văn bản.',
            )
        if original and _plain(original) not in plain_source:
            return "R4", f'Môn "{name}": "name_original" = "{original}" không có trong văn bản gốc.'
        if lower not in self.vietnamese:
            plain = _plain(name)
            right = self.plain_map.get(plain)
            if right is None:
                best = max(self.fuzzy, key=lambda k: difflib.SequenceMatcher(None, plain, k).ratio())
                if difflib.SequenceMatcher(None, plain, best).ratio() >= FUZZY_SUBJECT_RATIO:
                    right = self.fuzzy[best]
            if right is not None:
                return "R2", f'Tên môn "{name}" sai chính tả, đúng là "{right}".'
        if original and (expected := self.map.get(original.strip().lower())):
            if self.normalize(name) != expected:
                return "R4b", f'Môn "{original}" dịch thành "{name}" là sai nghĩa, đúng là "{expected}".'
        return None


def _score(raw: Any) -> float | None:
    text = str(raw).strip().replace(",", ".")
    if not _SCORE_RE.fullmatch(text):
        return None  # Đ, CĐ, A, B+: bước hậu kiểm bỏ, không coi là lỗi
    return float(text)


_KIND_TEXT = {"hk1": "học kỳ 1", "hk2": "học kỳ 2", "ca_nam": "cả năm", "khac": "không rõ"}


def _term_kind_problem(subjects: _Subjects, grade: str, term_name: str, kind: Any) -> str | None:
    """R7: "kind" (Claude phân loại kỳ theo nghĩa) phải hợp lệ và không trái với chữ tên kỳ đọc được.
    Không ép đổi tên kỳ: tên giữ nguyên văn, "khac" là câu trả lời hợp lệ khi thật sự không rõ."""
    label = f"{grade} {term_name}".strip()
    resolved = subjects.resolve_term(grade, term_name)
    text_kind = "ca_nam" if resolved is None else {1: "hk1", 2: "hk2"}.get(resolved.semester)
    if kind is None:
        if text_kind is None:
            return f'Kỳ "{label}": thiếu "kind", chọn hk1 / hk2 / ca_nam / khac theo nghĩa của kỳ.'
        return None
    if kind not in _KIND_TEXT:
        return f'Kỳ "{label}": "kind" = "{kind}" không hợp lệ, chỉ được hk1 / hk2 / ca_nam / khac.'
    if text_kind is not None and kind != "khac" and kind != text_kind:
        return (
            f'Kỳ "{label}": "kind" = "{kind}" ({_KIND_TEXT[kind]}) trái với tên kỳ '
            f"(chữ ghi {_KIND_TEXT[text_kind]}), xem lại kỳ này."
        )
    return None


def validate_parsed(data: Any, source: str) -> list[Issue]:
    """Kiểm tra JSON bước B so với văn bản nguồn (đã che thông tin cá nhân). Rỗng = đạt."""
    if not isinstance(data, dict) or not isinstance(data.get("terms"), list):
        return [Issue("R1", 'JSON thiếu danh sách "terms" hoặc sai cấu trúc.')]
    if str(data.get("scale", "10")) != "10" or not data["terms"]:
        return []  # thang 4 / điểm chữ / không phải bảng điểm: bước hậu kiểm tự báo

    subjects = _Subjects()
    language = str(data.get("language") or "vi")
    plain_source = _plain(source)
    source_numbers = {round(float(n.replace(",", ".")), 2) for n in _SCORE_RE.findall(source)}
    issues: list[Issue] = []
    cells = 0

    for ti, term in enumerate(data["terms"]):
        if not isinstance(term, dict) or not isinstance(term.get("subjects"), list):
            issues.append(Issue("R1", f'Kỳ thứ {ti + 1} sai cấu trúc (thiếu "subjects").', ti))
            continue
        grade, term_name = str(term.get("grade") or ""), str(term.get("term") or "")
        if (problem := _term_kind_problem(subjects, grade, term_name, term.get("kind"))) is not None:
            issues.append(Issue("R7", problem, ti))

        for si, item in enumerate(term["subjects"]):
            if not isinstance(item, dict):
                issues.append(Issue("R1", f"Kỳ thứ {ti + 1}, môn thứ {si + 1} sai cấu trúc.", ti, si))
                continue
            name = str(item.get("name") or "").strip()
            value = _score(item.get("score", ""))
            if value is None or subjects.normalize(name) is None:
                continue  # điểm chữ hoặc mục không chấm (TB, hạnh kiểm, thể dục)
            cells += 1
            original = str(item.get("name_original") or "").strip()
            if found := subjects.check(name, original, language, plain_source):
                issues.append(Issue(found[0], found[1], ti, si))
            if not 0.0 <= value <= 10.0 or round(value, 2) not in source_numbers:
                issues.append(
                    Issue(
                        "R5",
                        f'Điểm "{item.get("score")}" môn "{name}" không có nguyên văn trong văn bản hoặc ngoài thang 10.',
                        ti,
                        si,
                    )
                )

    decimals = sum(1 for n in _DECIMAL_RE.findall(source) if float(n.replace(",", ".")) <= 10)
    if decimals >= MIN_SOURCE_DECIMALS and cells < MIN_CELL_RATIO * decimals:
        issues.append(
            Issue("R6", f"Có thể bỏ sót điểm: văn bản có {decimals} số điểm, bạn mới trả {cells} ô.")
        )
    return issues


def drop_bad_cells(data: dict[str, Any], issues: list[Issue]) -> dict[str, Any]:
    """Lần 2 vẫn trượt: bỏ kỳ lỗi và ô lỗi, giữ phần đã đạt. Không sửa dữ liệu gốc."""
    bad_terms = {i.term_index for i in issues if i.term_index is not None and i.subject_index is None}
    bad_cells = {(i.term_index, i.subject_index) for i in issues if i.subject_index is not None}
    kept = copy.deepcopy(data)
    terms = []
    for ti, term in enumerate(kept.get("terms") or []):
        if ti in bad_terms or not isinstance(term, dict):
            continue
        term["subjects"] = [s for si, s in enumerate(term.get("subjects") or []) if (ti, si) not in bad_cells]
        if term["subjects"]:
            terms.append(term)
    kept["terms"] = terms
    return kept


def retry_message(issues: list[Issue], data: Any, source: str) -> str:
    """Lời nhắn lần 2 cho Claude: liệt kê lỗi, kèm văn bản gốc và JSON lần trước."""
    lines = "\n".join(f"- {i.message}" for i in issues)
    previous = json.dumps(data, ensure_ascii=False)
    return (
        f"Kết quả trước của bạn có lỗi:\n{lines}\n\n"
        "Sửa đúng các lỗi trên, giữ nguyên các mục đã đúng, trả lại toàn bộ JSON.\n\n"
        f"JSON trước:\n{previous}\n\nVăn bản gốc:\n{source}"
    )
