"""OCR làm mất dấu thập phân ("85" thay "8.5", "100" thay "10.0"), gặp thật ở ảnh diem-so-hoc-ba, Hoc-ba-dep.

Trên thang 10, số nguyên 11-100 không thể là điểm đúng: hiểu là mất dấu, chỉ khi bảng có ô thập phân thật (chắc là thang 10).
"""

from app.core.config import Settings
from app.services.transcript_reader import TranscriptReader
from app.services.transcript_validate import has_real_decimal_scores, lost_decimal_score, validate_parsed

# Chữ OCR thật của ảnh diem-so-hoc-ba (rút gọn): cột HK2, CN mất dấu ở nhiều môn
SOURCE = """Hký I Hký II CN
Toán 10.0 10.0 10.0
Sinh học 9.5 100 98
Ngữ văn 7.5 80 78
Tiếng Anh 8.0 85 83
GD QP-AN 9,6 9,7 9,7"""


def _data(*terms: tuple[str, str, dict[str, str]]) -> dict:
    return {
        "scale": "10",
        "level": "thpt",
        "terms": [
            {
                "grade": "",
                "term": term,
                "kind": kind,
                "subjects": [{"name": k, "score": v} for k, v in scores.items()],
            }
            for term, kind, scores in terms
        ],
    }


HK1 = ("Hký I", "hk1", {"Toán": "10.0", "Sinh học": "9.5", "Ngữ văn": "7.5", "Tiếng Anh": "8.0"})
HK2 = (
    "Hký II",
    "hk2",
    {"Toán": "10.0", "Sinh học": "100", "Ngữ văn": "80", "Tiếng Anh": "85", "GDQP": "9,7"},
)


def _read(data: dict, source: str = SOURCE):
    reader = TranscriptReader(None, Settings())  # type: ignore[arg-type]
    result = reader._check_parsed(data, source, "hoc_ba.png")
    return result, reader._merge_terms(result.terms)


def test_lost_decimal_score_only_for_integers_above_ten():
    assert lost_decimal_score("85") == 8.5
    assert lost_decimal_score("100") == 10.0
    assert lost_decimal_score("11") == 1.1
    for kept in ("10", "9", "4", "8.5", "8,5", "101", "2010", "01", "Đ"):
        assert lost_decimal_score(kept) is None, kept


def test_lost_decimals_are_restored_and_flagged():
    result, merged = _read(_data(HK1, HK2))
    assert merged["Hký II"] == {"Toán": 10.0, "Sinh học": 10.0, "Ngữ văn": 8.0, "Tiếng Anh": 8.5, "GDQP": 9.7}
    assert merged["Hký I"]["Sinh học"] == 9.5
    assert any("mất dấu thập phân ở 3 ô" in w for w in result.warnings)
    assert not any("không khớp" in w for w in result.warnings)


def test_validate_accepts_lost_decimal_written_verbatim_or_restored():
    assert validate_parsed(_data(HK1, HK2), SOURCE) == []
    restored = ("Hký II", "hk2", {"Toán": "10.0", "Sinh học": "10.0", "Ngữ văn": "8.0", "Tiếng Anh": "8.5"})
    assert validate_parsed(_data(HK1, restored), SOURCE) == []


def test_number_not_in_text_is_still_rejected():
    invented = ("Hký II", "hk2", {"Toán": "10.0", "Ngữ văn": "72"})
    assert [i.rule for i in validate_parsed(_data(HK1, invented), SOURCE)] == ["R5"]
    result, merged = _read(_data(HK1, invented))
    assert "Ngữ văn" not in merged["Hký II"]
    assert any("không khớp" in w for w in result.warnings)


def test_all_integer_table_is_not_guessed():
    # Không có ô thập phân nào: có thể là thang 100, không tự chia 10
    data = _data(("HK1", "hk1", {"Toán": "85", "Ngữ văn": "78"}))
    assert not has_real_decimal_scores(data)
    assert [i.rule for i in validate_parsed(data, "HK1 85 78")] == ["R5", "R5"]
    _, merged = _read(data, "HK1 85 78")
    assert merged == {}


def test_whole_year_column_with_lost_decimals_is_still_recognised():
    # Cột cả năm "98", "78", "83" sau khi khôi phục bằng (HK1 + 2·HK2) / 3 nên vẫn bị lớp 3 bỏ dù Claude gắn "khac"
    data = _data(
        ("HK1", "hk1", {"Sinh học": "9.5", "Ngữ văn": "7.5", "Tiếng Anh": "8.0"}),
        ("HK2", "hk2", {"Sinh học": "100", "Ngữ văn": "80", "Tiếng Anh": "85"}),
        ("CN?", "khac", {"Sinh học": "98", "Ngữ văn": "78", "Tiếng Anh": "83"}),
    )
    for term in data["terms"]:
        term["grade"] = "Lớp 12"
    _, merged = _read(data, "Lớp 12\n" + SOURCE)
    assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}
