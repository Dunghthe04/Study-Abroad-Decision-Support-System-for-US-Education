from datetime import date

from app.services.pii_mask import (
    BIRTH_MASK,
    CONTACT_MASK,
    DATE_MASK,
    EMAIL_MASK,
    ID_MASK,
    NAME_MASK,
    mask_known_values,
    mask_personal_info,
)
from tests.test_transcript_ocr_api import (
    UNIVERSITY_MARKDOWN,
    UNIVERSITY_PARSED,
    FakeOllama,
    make_client,
    post_image,
)


def test_colon_labels_are_masked_but_class_and_school_year_are_kept():
    text = "Họ và tên: Nguyễn Văn An   Lớp: 12A1\nNgày sinh: 05/03/2007    Năm học: 2023-2024\nMã học sinh: HS0123"

    masked = mask_personal_info(text)

    assert "Nguyễn Văn An" not in masked
    assert "05/03/2007" not in masked
    assert "HS0123" not in masked
    assert f"Họ và tên: {NAME_MASK}" in masked
    assert f"Ngày sinh: {BIRTH_MASK}" in masked
    assert f"Mã học sinh: {ID_MASK}" in masked
    assert "Lớp: 12A1" in masked
    assert "Năm học: 2023-2024" in masked


def test_labels_match_without_diacritics_and_in_bold():
    masked = mask_personal_info("**HO TEN:** Tran Thi B\nSo CCCD: 001207012345\nFull name: Le C")

    assert "Tran Thi B" not in masked
    assert "001207012345" not in masked
    assert "Le C" not in masked


def test_two_labels_on_one_line_are_masked_separately():
    masked = mask_personal_info("Họ tên: Phạm D Ngày sinh: 1/2/2008")

    assert masked == f"Họ tên: {NAME_MASK} Ngày sinh: {BIRTH_MASK}"


def test_table_cell_label_masks_next_cell_only():
    text = "| Họ và tên | Nguyễn Văn An | Lớp | 10A2 |\n| Ngày sinh | 05/03/2007 | Năm học | 2023-2024 |"

    masked = mask_personal_info(text)

    assert masked.splitlines() == [
        f"| Họ và tên | {NAME_MASK} | Lớp | 10A2 |",
        f"| Ngày sinh | {BIRTH_MASK} | Năm học | 2023-2024 |",
    ]


def test_parent_name_and_contacts_are_masked():
    text = "Họ tên cha: Nguyễn Văn E\nĐiện thoại: 0912 345 678\nEmail: an@example.com"

    masked = mask_personal_info(text)

    assert "Nguyễn Văn E" not in masked
    assert "0912 345 678" not in masked
    assert "an@example.com" not in masked
    assert f"Điện thoại: {CONTACT_MASK}" in masked


def test_unlabelled_dates_and_long_numbers_are_masked():
    masked = mask_personal_info("Cấp ngày 12/09/2021, số 079207001234, ĐT +84 912.345.678")

    assert masked == f"Cấp ngày {DATE_MASK}, số {ID_MASK}, ĐT {ID_MASK}"


def test_score_rows_and_years_are_untouched():
    text = (
        "Lớp 10\n"
        "| Môn | Toán | Lý | Văn | TB |\n"
        "|---|---|---|---|---|\n"
        "| HK1 | 7,2 | 4,2 | 6,7 | 6,0 |\n"
        "HK2 7.2 4.2 6.7 7.6 6.6 5.7 8.1 9.0 6.6 TB Tốt\n"
        "Cả năm 8 9 7 6 8 9 7 6 10\n"
        "Năm học 2023 2024, Fall2023, HK1 2024-2025"
    )

    assert mask_personal_info(text) == text


def test_llm_only_receives_masked_text_and_scores_still_read():
    markdown = (
        "Họ và tên: Nguyễn Văn An   Ngày sinh: 05/03/2007\nMã sinh viên: 21012345\n" + UNIVERSITY_MARKDOWN
    )
    fake = FakeOllama(markdown, UNIVERSITY_PARSED)

    body = post_image(make_client(fake))

    parse_calls = [c for c in fake.calls if "format" in c]
    assert parse_calls
    prompt = parse_calls[0]["messages"][0]["content"]
    for secret in ("Nguyễn Văn An", "05/03/2007", "21012345"):
        assert secret not in prompt
    assert "| Fall2023 | Mathematics | 8,2 |" in prompt
    assert len(body["terms"]) > 0


# --- mask_known_values: che đúng thông tin hệ thống đã biết của chủ hồ sơ ---


def test_known_name_alone_on_a_line_is_masked_in_any_case_and_without_diacritics():
    text = "SỞ GIÁO DỤC VÀ ĐÀO TẠO\nNGUYỄN VĂN MINH ANH\nLời phê: em nguyen van minh anh chăm ngoan"

    masked = mask_known_values(text, names=["Nguyễn Văn Minh Anh"])

    assert "MINH ANH" not in masked
    assert "minh anh" not in masked
    assert masked.splitlines()[1] == NAME_MASK
    assert "SỞ GIÁO DỤC VÀ ĐÀO TẠO" in masked


def test_given_name_is_masked_but_never_a_single_word():
    masked = mask_known_values("Bạn Minh Anh học tốt. Anh trai: Tuấn", names=["Nguyễn Văn Minh Anh"])

    assert masked == f"Bạn {NAME_MASK} học tốt. Anh trai: Tuấn"


def test_name_sharing_subject_words_keeps_subject_scores():
    text = "| Môn | Ngữ văn | Tiếng Anh |\n| HK1 | 7,5 | 8,0 |\nHọc sinh Nguyễn Văn Anh"

    masked = mask_known_values(text, names=["Nguyễn Văn Anh"])

    assert "| Môn | Ngữ văn | Tiếng Anh |" in masked
    assert "| HK1 | 7,5 | 8,0 |" in masked
    assert masked.endswith(f"Học sinh {NAME_MASK}")


def test_single_word_name_is_ignored():
    assert mask_known_values("Tiếng Anh 8,0", names=["Anh"]) == "Tiếng Anh 8,0"


def test_known_birth_date_in_every_spelling():
    text = "05/03/2008 | 5-3-2008 | 05.03.2008 | 2008-03-05 | ngày 5 tháng 3 năm 2008 | Sinh 5 tháng 03 2008"

    masked = mask_known_values(text, date_of_birth=date(2008, 3, 5))

    assert "2008" not in masked
    assert masked.count(BIRTH_MASK) == 6


def test_other_dates_and_scores_are_kept():
    text = "Ngày thi 15/03/2008 | HK1 5,3 | Năm học 2023-2024"

    assert mask_known_values(text, date_of_birth=date(2008, 3, 5)) == text


def test_known_phone_matches_with_any_separator_and_country_code():
    text = "ĐT: +84 912.345.678, gọi 0912-345-678 hoặc 84912345678; điểm 9.1 2.3"

    masked = mask_known_values(text, phone="0912 345 678")

    assert masked == f"ĐT: {CONTACT_MASK}, gọi {CONTACT_MASK} hoặc {CONTACT_MASK}; điểm 9.1 2.3"


def test_known_email_ignores_case():
    masked = mask_known_values("Liên hệ AN.Nguyen@Example.com", email="an.nguyen@example.com")

    assert masked == f"Liên hệ {EMAIL_MASK}"


def test_nothing_known_returns_text_unchanged():
    text = "Họ tên Nguyễn Văn An 05/03/2008"

    assert mask_known_values(text) == text
