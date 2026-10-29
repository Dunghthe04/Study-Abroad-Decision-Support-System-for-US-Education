"""Việc B1, B2: dữ liệu kiến thức (kho của Đức) đã chuyển thành JSONL đúng, đủ, và thêm dòng mới sau này vẫn an toàn."""

import json
from datetime import date
from pathlib import Path

import pytest

from scripts.build_knowledge_jsonl import (
    LEGACY_CENTER_INFO_STT,
    OUT_PATH,
    build_records,
    find_columns,
    map_level,
    map_scope,
    map_status,
    parse_source,
    source_type_of,
    split_quote,
    to_record,
)

LEVELS = {"general", "secondary", "community_college", "undergraduate", "master", "phd"}
SCOPES = {"system_advisory", "center_info"}
SOURCE_TYPES = {"official", "center", "internal"}
STATUSES = {"draft", "verified", "rejected"}

# Dòng tiêu đề thật của sheet 2
HEADER = (
    "STT",
    "Nhãn Phân Loại\n(Data Label)",
    "Bậc áp dụng\n(Level)",
    "Mã Skill AI\n(AI Skill Code)",
    "❓ Câu hỏi tư vấn (Học sinh hỏi)",
    "💬 Câu trả lời (Từ trung tâm)",
    "ℹ️ Thông tin chung (Mặt bằng thị trường)",
    "🛡️ Kết quả (Câu trả lời chuẩn Train AI)",
    "🏢 Nguồn tư vấn & Ngày thu",
    "Logic Phân Hóa & Rule Nghiệp Vụ",
)
SOURCE = "Edu4life\nMã: RAW-ON-003\nNgày thu: 27/09/2026"
DEFAULT_RESULT = "Phí SEVIS là $350. (Nguồn kiểm chứng: travel.state.gov)."


def sheet_row(stt, question="Phí SEVIS bao nhiêu?", result=DEFAULT_RESULT, level="Chung", extra=()):
    base = (str(stt), "LABEL_X", level, "Skill_X", question, "trả lời trung tâm", "thông tin chung")
    return base + (result, SOURCE, "rule") + tuple(extra)


@pytest.fixture(scope="module")
def records() -> list[dict]:
    with Path(OUT_PATH).open(encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


# --- nhận ra cột theo tên ---


def test_columns_are_found_by_name_not_by_position():
    columns = find_columns(HEADER)

    assert columns["question"] == 4 and columns["result"] == 7 and columns["source"] == 8
    assert (
        "code" not in columns and "scope" not in columns and "status" not in columns
    )  # chưa có cột tùy chọn


def test_the_existing_skill_code_column_is_not_mistaken_for_the_knowledge_code():
    assert "code" not in find_columns(HEADER)  # "Mã Skill AI" khác "Mã kiến thức"


def test_optional_columns_are_found_wherever_they_are_added():
    header = ("Trạng thái kiểm chứng", "Mã kiến thức", *HEADER, "Phạm vi")  # cột chèn lung tung vẫn nhận ra

    columns = find_columns(header)

    assert columns["status"] == 0 and columns["code"] == 1 and columns["scope"] == 12
    assert columns["question"] == 6  # vị trí các cột cũ lệch theo nhưng vẫn đúng tên


def test_missing_required_column_is_reported_by_name():
    header = tuple(h for h in HEADER if "Kết quả" not in h)

    with pytest.raises(ValueError, match="kết quả"):
        find_columns(header)


# --- hàm chuyển đổi ---


def test_split_quote_separates_the_source_from_the_answer():
    text = "Gia đình cần chứng minh tài chính. (Nguồn kiểm chứng: EducationUSA - U.S. Department of State)."

    content, quote = split_quote(text)

    assert content == "Gia đình cần chứng minh tài chính."
    assert quote == "EducationUSA - U.S. Department of State"


def test_split_quote_handles_a_stray_closing_quote_mark():
    content, quote = split_quote(
        'Bước cuối là phỏng vấn. (Nguồn kiểm chứng: travel.state.gov - Student Visa)."'
    )

    assert content == "Bước cuối là phỏng vấn."
    assert quote == "travel.state.gov - Student Visa"


def test_split_quote_keeps_parentheses_inside_the_source_and_text():
    content, quote = split_quote("Theo luật (8 CFR). (Nguồn kiểm chứng: SEVP & 8 U.S.C. 1184(m)).")

    assert content == "Theo luật (8 CFR)."
    assert quote == "SEVP & 8 U.S.C. 1184(m)"


def test_split_quote_without_source_returns_no_quote():
    assert split_quote("Chỉ có câu trả lời.") == ("Chỉ có câu trả lời.", None)


def test_parse_source_reads_name_code_and_date():
    note, collected = parse_source(SOURCE)

    assert note == "Edu4life, RAW-ON-003"
    assert collected == date(2026, 9, 27)


def test_parse_source_rejects_unknown_format():
    with pytest.raises(ValueError):
        parse_source("không rõ nguồn")


def test_map_level_follows_the_agreed_rules():
    assert map_level("Chung") == "general"
    assert map_level("Đại học") == "undergraduate"
    assert map_level("Thạc sĩ") == "master"
    assert map_level("THCS; THPT") == "secondary"
    assert map_level("Tiến sĩ") == "phd"
    assert map_level("Đại học; Thạc sĩ") == "general"  # áp dụng hai bậc nên để chung
    with pytest.raises(ValueError):
        map_level("Mầm non")


def test_status_accepts_english_and_vietnamese_and_defaults_to_draft():
    assert map_status("verified") == "verified"
    assert map_status("Đã kiểm chứng") == "verified"
    assert map_status("  Chưa kiểm chứng ") == "draft"
    assert map_status("") == "draft"
    with pytest.raises(ValueError):
        map_status("đang xem")


def test_scope_comes_from_the_column_when_it_exists_else_from_the_legacy_list():
    assert map_scope("center_info", 5, has_column=True) == "center_info"
    assert map_scope("", 5, has_column=True) == "system_advisory"  # ô trống thì mặc định
    assert map_scope("", 46, has_column=False) == "center_info"  # chưa có cột: dùng danh sách cũ
    assert map_scope("", 5, has_column=False) == "system_advisory"
    with pytest.raises(ValueError):
        map_scope("trung_tam", 5, has_column=True)


def test_system_own_policy_is_not_counted_as_an_outside_official_source():
    assert source_type_of("EducationUSA - U.S. Department of State") == "official"
    assert source_type_of("USAS Core Principles & EducationUSA") == "internal"
    assert source_type_of(None) == "internal"


def test_to_record_builds_a_complete_record_and_flags_time_sensitive_money():
    columns = find_columns(HEADER)

    record = to_record(sheet_row(26, question="  Phí  SEVIS bao nhiêu?  "), columns)

    assert record["code"] == "KB-026"  # chưa có cột Mã thì sinh từ STT
    assert record["question"] == "Phí SEVIS bao nhiêu?"  # khoảng trắng thừa được gọn lại
    assert record["content"] == "Phí SEVIS là $350."
    assert record["quote"] == "travel.state.gov"
    assert record["scope"] == "system_advisory" and record["status"] == "draft"
    assert record["review_due_at"] == "2027-03-26"  # 180 ngày sau ngày thu vì có số tiền
    assert record["source_note"] == "Edu4life, RAW-ON-003"


# --- thêm dòng mới sau này ---

HEADER_WITH_NEW_COLUMNS = (*HEADER, "Mã kiến thức", "Phạm vi", "Trạng thái kiểm chứng")


def test_a_new_row_with_its_own_code_scope_and_status_is_used_as_given():
    row = sheet_row(89, question="Câu mới?", extra=("KB-089", "center_info", "Đã kiểm chứng"))

    record = to_record(row, find_columns(HEADER_WITH_NEW_COLUMNS))

    assert (record["code"], record["scope"], record["status"]) == ("KB-089", "center_info", "verified")


def test_once_the_code_column_exists_every_row_must_have_a_code():
    row = sheet_row(89, extra=("", "", ""))

    with pytest.raises(ValueError, match="Mã kiến thức"):
        to_record(row, find_columns(HEADER_WITH_NEW_COLUMNS))


def test_a_code_must_look_like_kb_001():
    row = sheet_row(89, extra=("ABC-1", "", ""))

    with pytest.raises(ValueError, match="KB-001"):
        to_record(row, find_columns(HEADER_WITH_NEW_COLUMNS))


def test_an_answer_without_a_verified_source_is_rejected():
    with pytest.raises(ValueError, match="nguồn kiểm chứng"):
        to_record(sheet_row(5, result="Câu trả lời không có nguồn."), find_columns(HEADER))


def test_build_records_reports_every_bad_row_and_duplicates_instead_of_stopping_at_the_first():
    rows = [
        sheet_row(1, question="Câu A?"),
        sheet_row(2, question="Câu A?"),  # trùng câu hỏi với STT 1
        sheet_row(3, question="Câu B?", level="Mầm non"),  # bậc lạ
        sheet_row(4, question="Câu C?", result="Không có nguồn."),  # thiếu nguồn
    ]

    records, errors = build_records(HEADER, rows)

    assert [r["stt"] for r in records] == [1, 2]
    assert len(errors) == 3
    assert any("STT 2" in e and "câu hỏi trùng" in e for e in errors)
    assert any("STT 3" in e and "Bậc lạ" in e for e in errors)
    assert any("STT 4" in e and "nguồn kiểm chứng" in e for e in errors)


def test_build_records_rejects_duplicate_codes():
    rows = [
        sheet_row(1, question="Câu A?", extra=("KB-010", "", "")),
        sheet_row(2, question="Câu B?", extra=("KB-010", "", "")),
    ]

    _, errors = build_records(HEADER_WITH_NEW_COLUMNS, rows)

    assert errors == ["STT 2: mã trùng với STT 1: 'KB-010'"]


def test_clean_rows_give_no_errors():
    records, errors = build_records(
        HEADER, [sheet_row(1, question="Câu A?"), sheet_row(2, question="Câu B?")]
    )

    assert errors == [] and [r["code"] for r in records] == ["KB-001", "KB-002"]


# --- file JSONL đã sinh ---


def test_there_are_88_records_with_unique_codes_and_questions(records):
    assert len(records) == 443
    assert len({r["question"] for r in records}) == 443
    assert [r["code"] for r in records] == [f"KB-{n:03d}" for n in range(1, 89)]
    assert sorted(r["stt"] for r in records) == list(range(1, 89))


def test_every_record_has_the_text_needed_to_search_and_answer(records):
    for r in records:
        assert r["question"].strip(), r["code"]
        assert r["content"].strip(), r["code"]
        assert r["quote"], f"{r['code']} thiếu nguồn kiểm chứng"
        assert "Nguồn kiểm chứng" not in r["content"], r[
            "code"
        ]  # nguồn đã tách ra, không lặp trong câu trả lời
        assert len(r["title"]) <= 300, r["code"]


def test_no_stray_quote_marks_around_the_text(records):
    for r in records:
        for field in ("question", "content"):
            assert r[field][0] not in '"“' and r[field][-1] not in '"”', (r["code"], field)


def test_values_belong_to_the_allowed_sets(records):
    for r in records:
        assert r["study_level"] in LEVELS, r["code"]
        assert r["scope"] in SCOPES, r["code"]
        assert r["source_type"] in SOURCE_TYPES, r["code"]
        assert r["status"] in STATUSES, r["code"]
        assert r["doc_type"] == "faq", r["code"]


def test_only_rows_with_a_named_center_price_are_center_info(records):
    center = {r["stt"] for r in records if r["scope"] == "center_info"}

    assert center == LEGACY_CENTER_INFO_STT == {46, 62}  # giá dịch vụ cụ thể của Mission Edu và Summit


def test_dates_are_valid_and_review_dates_come_after_collection(records):
    for r in records:
        collected = date.fromisoformat(r["retrieved_at"])
        if r["review_due_at"]:
            assert date.fromisoformat(r["review_due_at"]) > collected, r["code"]


def test_answers_with_money_have_a_review_date_and_others_do_not(records):
    for r in records:
        has_money = "$" in r["content"] or "USD" in r["content"]
        assert bool(r["review_due_at"]) == has_money, r["code"]


def test_the_levels_of_the_sheet_are_all_represented(records):
    levels = {r["study_level"] for r in records}

    assert {"general", "secondary", "undergraduate", "master", "phd"} <= levels
