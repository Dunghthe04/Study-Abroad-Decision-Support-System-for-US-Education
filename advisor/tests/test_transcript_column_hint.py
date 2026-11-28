"""OCR mất dòng tiêu đề cột: suy thứ tự cột từ số (cột 3 = trung bình năm của cột 1, 2) rồi ghi một dòng gợi ý cho bước B."""

from app.services.transcript_text import TERM_COLUMNS_HINT, hint_term_columns

# Chữ OCR thật của ảnh 1654860257 (đã cắt tên giáo viên): không có dòng "Học kỳ I  Học kỳ II  Cả năm"
NO_HEADER = """Toàn 6,3 4,9 5,4
Vật lì 5,0 4,8 4,9
Hóa học 5,9 4,3 4,8
Sinh học 5,3 4,7 5,1
Ngũ văn 5,0 5,0 5,0
Lịch sử 5,3 5,1 5,2"""


def test_hint_added_when_third_column_is_year_average():
    lines = hint_term_columns(NO_HEADER).splitlines()
    assert lines[0] == TERM_COLUMNS_HINT
    assert lines[1:] == NO_HEADER.splitlines()
    assert not any(ch.isdigit() for ch in TERM_COLUMNS_HINT)  # không thêm số nào để R5 nhận nhầm


def test_simple_mean_and_lost_decimals_also_recognised():
    mean = "Toán 8,0 9,0 8,5\nVăn 6,0 7,0 6,5\nAnh 7,0 8,0 7,5"
    assert hint_term_columns(mean).splitlines()[0] == TERM_COLUMNS_HINT
    lost = "Toàn 42 47 45\nSinh 72 57 62\nĐịa 33 52 46"  # ảnh thật anh-chup, mất dấu ở mọi ô
    assert hint_term_columns(lost).splitlines()[0] == TERM_COLUMNS_HINT


def test_no_hint_when_header_already_there():
    text = "Hoc kg I Hoc kg II Ca nám\n" + NO_HEADER
    assert hint_term_columns(text) == text


def test_no_hint_when_numbers_do_not_fit():
    other = "Toán 9,0 5,0 7,5\nVăn 6,0 8,0 4,0\nAnh 7,0 8,0 9,5"
    assert hint_term_columns(other) == other
    constant = "Toán 5,0 5,0 5,0\nVăn 6,0 6,0 6,0\nAnh 7,0 7,0 7,0"  # toàn bằng nhau: không đủ bằng chứng
    assert hint_term_columns(constant) == constant
    assert hint_term_columns("Toán 6,3 4,9 5,4") == "Toán 6,3 4,9 5,4"  # quá ít dòng
