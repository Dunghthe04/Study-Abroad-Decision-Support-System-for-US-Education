"""Cơ chế hiểu tên kỳ khi mỗi trường ghi một kiểu (CN, TBcn, Cả năm học, HK3, tên lạ...).

Lớp 1: Claude trả "kind" theo nghĩa (hk1 / hk2 / ca_nam / khac), code tin "kind" trước, chữ là dự phòng.
Lớp 2: R7 chỉ bắt "kind" sai hoặc trái chữ, không ép đổi tên kỳ (ép từng làm Claude bịa "HK3").
Lớp 3: kỳ đáng ngờ có điểm từng môn bằng trung bình HK1, HK2 cùng lớp là cột cả năm, bỏ dù tên là gì.
"""

from app.core.config import Settings
from app.services.transcript_reader import TranscriptReader
from app.services.transcript_validate import validate_parsed

HK1 = {"Toán": "7,2", "Ngữ văn": "6,7", "Vật lý": "4,2", "Hóa học": "8,0"}
HK2 = {"Toán": "7,5", "Ngữ văn": "7,1", "Vật lý": "7,8", "Hóa học": "8,6"}
# Cả năm THPT = (HK1 + 2·HK2) / 3, làm tròn 1 chữ số
WHOLE_YEAR = {"Toán": "7,4", "Ngữ văn": "7,0", "Vật lý": "6,6", "Hóa học": "8,4"}
OTHER = {"Toán": "9,5", "Ngữ văn": "5,5", "Vật lý": "9,0", "Hóa học": "6,0"}


def _subjects(scores: dict[str, str]) -> list[dict]:
    return [{"name": k, "score": v} for k, v in scores.items()]


def _term(term: str, scores: dict[str, str], kind: str | None = None, grade: str = "Lớp 12") -> dict:
    t = {"grade": grade, "term": term, "subjects": _subjects(scores)}
    if kind is not None:
        t["kind"] = kind
    return t


def _source(*tables: dict[str, str]) -> str:
    return "Lớp 12\n" + "\n".join(" ".join(t.values()) for t in tables)


def _read(*terms: dict, source: str):
    reader = TranscriptReader(None, Settings())  # type: ignore[arg-type]
    result = reader._check_parsed(
        {"scale": "10", "level": "thpt", "terms": list(terms)}, source, "hoc_ba.png"
    )
    return result, reader._merge_terms(result.terms)


SOURCE = _source(HK1, HK2, WHOLE_YEAR, OTHER)


def _scores(merged: dict, name: str) -> dict[str, float]:
    return merged[name]


# --- Lớp 1: "kind" ---


def test_kind_ca_nam_drops_column_whatever_its_name():
    for name in ("TBcn", "Cả năm học", "T.Kết", "Năm", "ĐTB năm"):
        _, merged = _read(
            _term("HK1", HK1, "hk1"),
            _term("HK2", HK2, "hk2"),
            _term(name, WHOLE_YEAR, "ca_nam"),
            source=SOURCE,
        )
        assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}, name


def test_kind_names_strange_semester_labels():
    _, merged = _read(_term("Đợt I", HK1, "hk1"), _term("Giai đoạn cuối", HK2, "hk2"), source=SOURCE)
    assert _scores(merged, "Lớp 12 HK1")["Toán"] == 7.2
    assert _scores(merged, "Lớp 12 HK2")["Toán"] == 7.5


def test_written_semester_wins_and_kind_khac_keeps_scores_flagged():
    result, merged = _read(_term("HK1", HK1, "hk1"), _term("Kỳ thi riêng", OTHER, "khac"), source=SOURCE)
    assert "Kỳ thi riêng" in " ".join(merged)
    assert any("chưa rõ" in w and "Kỳ thi riêng" in w for w in result.warnings)


# --- Không có "kind" (bảng code tự đọc): chữ là dự phòng ---


def test_without_kind_common_whole_year_spellings_are_dropped():
    for name in ("CN", "Cả năm", "TB cn", "TBcn", "TB năm", "Tổng kết"):
        _, merged = _read(_term("HK1", HK1), _term("HK2", HK2), _term(name, WHOLE_YEAR), source=SOURCE)
        assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}, name


# --- Lớp 3: nhận cột cả năm bằng số ---


def test_high_school_hk3_holding_whole_year_scores_is_dropped_and_does_not_overwrite_hk1():
    # Lỗi thật ảnh 3794687: cột CN thành "Lớp 12 HK3", trước đây bị đổi thành HK1 và ghi đè HK1 thật
    for kind in (None, "khac"):
        result, merged = _read(
            _term("HK1", HK1, kind and "hk1"),
            _term("HK2", HK2, kind and "hk2"),
            _term("HK3", WHOLE_YEAR, kind),
            source=SOURCE,
        )
        assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}
        assert _scores(merged, "Lớp 12 HK1")["Toán"] == 7.2
        assert any("cả năm" in w for w in result.warnings)


def test_whole_year_mislabelled_as_a_second_hk2_is_dropped():
    _, merged = _read(
        _term("HK1", HK1, "hk1"), _term("HK2", HK2, "hk2"), _term("HK2", WHOLE_YEAR, "hk2"), source=SOURCE
    )
    assert _scores(merged, "Lớp 12 HK2")["Toán"] == 7.5


def test_simple_average_whole_year_is_also_recognised():
    hk1 = {"Toán": "8,0", "Ngữ văn": "6,0", "Vật lý": "7,0"}
    hk2 = {"Toán": "9,0", "Ngữ văn": "7,0", "Vật lý": "8,0"}
    mean = {"Toán": "8,5", "Ngữ văn": "6,5", "Vật lý": "7,5"}
    _, merged = _read(
        _term("Kỳ 1", hk1, "hk1"),
        _term("Kỳ 2", hk2, "hk2"),
        _term("Xyz", mean, "khac"),
        source=_source(hk1, hk2, mean),
    )
    assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}


def test_all_tens_keeps_both_real_semesters():
    tens = {"Toán": "10", "Ngữ văn": "10", "Vật lý": "10"}
    _, merged = _read(
        _term("HK1", tens, "hk1"),
        _term("HK2", tens, "hk2"),
        _term("Tổng", tens, "khac"),
        source=_source(tens),
    )
    assert set(merged) == {"Lớp 12 HK1", "Lớp 12 HK2"}


def test_unclear_term_with_its_own_scores_is_kept():
    _, merged = _read(
        _term("HK1", HK1, "hk1"), _term("HK2", HK2, "hk2"), _term("HK3", OTHER, "khac"), source=SOURCE
    )
    assert len(merged) == 3 and _scores(merged, "Lớp 12 HK1")["Toán"] == 7.2


def test_terms_of_other_grades_are_not_compared():
    _, merged = _read(
        _term("HK1", HK1, "hk1", grade="Lớp 11"),
        _term("HK2", HK2, "hk2", grade="Lớp 11"),
        _term("HK2", WHOLE_YEAR, "hk2"),
        source="Lớp 11\n" + SOURCE,
    )
    assert _scores(merged, "Lớp 12 HK2")["Toán"] == 7.4


def test_university_semester_numbers_still_count_from_course_start():
    third = TranscriptReader._resolve_term("", "Học kỳ 3")
    assert (third.grade_name, third.semester) == ("Năm 2", 1)
    assert TranscriptReader._resolve_term("Lớp 12", "HK3").semester is None


# --- Lớp 2: R7 ---

R7_SOURCE = "Lớp 12\n7,2 6,7 7,5 7,1"


def _r7(term: str, kind: str | None) -> list[str]:
    t = {"grade": "Lớp 12", "term": term, "subjects": [{"name": "Toán", "score": "7,2"}]}
    if kind is not None:
        t["kind"] = kind
    return [i.rule for i in validate_parsed({"scale": "10", "terms": [t]}, R7_SOURCE)]


def test_r7_accepts_any_spelling_when_kind_is_given():
    assert _r7("HKý I", "hk1") == []
    assert _r7("CN", "ca_nam") == []
    assert _r7("HK3", "khac") == []
    assert _r7("HK3", "ca_nam") == []
    assert _r7("HK1", "khac") == []


def test_r7_flags_bad_or_contradicting_kind():
    assert _r7("HK1", "hk2") == ["R7"]
    assert _r7("Cả năm", "hk1") == ["R7"]
    assert _r7("HK1", "semester1") == ["R7"]
    assert _r7("HKý I", None) == ["R7"]
    assert _r7("HK2", None) == []
