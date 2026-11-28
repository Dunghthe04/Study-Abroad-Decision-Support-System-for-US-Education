from app.services.transcript_text import (
    cut_repetition,
    keep_score_lines,
    read_term_tables,
    tabulate_spaced_tables,
)

# Văn bản glm-ocr chép thật từ ảnh học bạ chụp nghiêng (giữ nguyên lỗi dấu của OCR)
SPACED_HIGH_SCHOOL = """Lớp 10:
Môn Toán Lý Hóa Sinh Ngữ vân Sù Dja Ngoại ngữ Công nghề Thể dµc GD CD GD QP Tin TB môn HL HK
HK1 7.2 4.2 7.8 5.1 6.7 7.6 6.6 5.7 7 D 6 8.4 7.3 6.6 TB Tôt
HK2 6.7 6.8 8.9 7.9 6.3 6.8 7.5 7.2 7.2 D 8 7.3 6.8 7.3 Khá Khá
Cá nám 6.9 6.9 8.5 7 6.4 7.1 7.2 6.7 7.1 D 7.3 7.7 7 7.1 Khá Khá
Ghi chú: không"""


def _cells(line: str) -> list[str]:
    return [c.strip() for c in line.strip("|").split("|")]


def test_spaced_table_becomes_markdown_with_aligned_columns():
    lines = tabulate_spaced_tables(SPACED_HIGH_SCHOOL).splitlines()

    header, hk1 = _cells(lines[1]), _cells(lines[3])
    assert len(header) == len(hk1) == 17
    by_subject = dict(zip(header, hk1, strict=True))
    assert by_subject["Sù"] == "7.6"
    assert by_subject["Dja"] == "6.6"
    assert by_subject["Ngoại ngữ"] == "5.7"
    assert by_subject["GD CD"] == "6"
    assert by_subject["Tin"] == "7.3"
    assert _cells(lines[5])[0] == "Cá nám"
    assert lines[-1] == "Ghi chú: không"


def test_header_that_cannot_be_split_reliably_is_left_unchanged():
    text = "Môn Abc Xyz Qwe Rty\nHK1 7.2 8.1 6.5"
    assert tabulate_spaced_tables(text) == text


def test_numbered_list_layout_is_left_unchanged():
    text = "TT Môn học Kỳ 1 Kỳ 2\n1 Toán học 9,9 9,9\n2 Vật lí 9,8 9,8"
    assert tabulate_spaced_tables(text) == text


def test_cut_repetition_drops_repeated_page_and_fences():
    page = "KẾT QUẢ HỌC TẬP NĂM 2019\n1 Toán học 9,9 9,9 9,9\n2 Vật lí 9,8 9,8 9,8\n3 Hóa học 10,0 10,0 10,0"
    text = f"{page}\n```\n{page}\n```\n```"
    assert cut_repetition(text) == page


def test_cut_repetition_keeps_header_repeated_per_grade_block():
    header = "Môn Toán Lý Hóa Sinh Ngữ văn"
    text = (
        f"{header}\nHK1 7.2 4.2 7.8 5.1 6.7\n{header}\nHK1 7.7 7.3 9 7.8 6.5\n{header}\nHK1 8.1 7.4 8.5 7.7 7"
    )
    assert cut_repetition(text) == text


TRANSCRIPT_WITH_PERSONAL_LINES = """CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
Độc lập - Tự do - Hạnh phúc
TRƯỜNG THPT CHU VĂN AN
Họ và tên: [HỌ TÊN]   Lớp: 10A1
Địa chỉ: Số 12, ngõ 5, phố Đội Cấn
| Họ và tên | [HỌ TÊN] |
| Ngày sinh | [NGÀY SINH] |
Lớp 10
| Môn | Toán | Ngữ văn | Tiếng Anh |
|---|---|---|---|
| HK1 | 8.5 | 7.0 | 9.1 |
| HK2 | 8.8 | 7.4 | 9.3 |
| | Toán | Vật lí | Hóa học |
|---|---|---|---|
| Cả năm | 8.6 | 7.9 | 8.0 |
Điểm trung bình Toán: 8.6
Nhận xét: Em chăm ngoan, cần cố gắng hơn
Giáo viên chủ nhiệm: Trần Văn Anh
Hiệu trưởng (ký, đóng dấu)"""


def test_keep_score_lines_keeps_grades_terms_and_tables():
    kept = keep_score_lines(TRANSCRIPT_WITH_PERSONAL_LINES)

    for line in (
        "Lớp: 10A1",
        "Lớp 10",
        "| Môn | Toán | Ngữ văn | Tiếng Anh |",
        "| HK1 | 8.5 | 7.0 | 9.1 |",
        "| | Toán | Vật lí | Hóa học |",
        "| Cả năm | 8.6 | 7.9 | 8.0 |",
        "Điểm trung bình Toán: 8.6",
    ):
        assert line in kept.splitlines()
    assert [t["term"] for t in read_term_tables(kept)] == ["HK1", "HK2", "Cả năm"]


def test_keep_score_lines_drops_personal_and_decorative_lines():
    kept = keep_score_lines(TRANSCRIPT_WITH_PERSONAL_LINES)

    for text in (
        "CỘNG HÒA",
        "Độc lập",
        "CHU VĂN AN",
        "Địa chỉ",
        "| Họ và tên |",
        "Ngày sinh",
        "Nhận xét",
        "Trần Văn Anh",
        "Hiệu trưởng",
    ):
        assert text not in kept
    assert "\n\n\n" not in kept


def test_keep_score_lines_keeps_spaced_table_and_university_rows():
    assert keep_score_lines(SPACED_HIGH_SCHOOL).splitlines()[:5] == SPACED_HIGH_SCHOOL.splitlines()[:5]
    university = "| Term | Course | Score |\n|---|---|---|\n| Fall2023 | Mathematics | 8,2 |"
    assert keep_score_lines(university) == university


def test_keep_score_lines_without_scores_is_empty():
    assert keep_score_lines("Họ tên: [HỌ TÊN]\nTrường THPT Nguyễn Trãi\nGhi chú: không") == ""


def test_keep_score_lines_keeps_plain_line_with_multi_word_subject():
    assert keep_score_lines("Ngữ văn: 7.5\nGiáo viên: Trần Văn Anh 5") == "Ngữ văn: 7.5"


# Model OCR chép bảng dọc: mỗi ô một dòng, tên giáo viên bộ môn đứng sau điểm
VERTICAL_TABLE = """Môn học/
Hký I
Hký II
CN
Toán
6.2
7.7
7.2
Nguyen Huong Ngun
Ngoại ngũ
Gung...inh
6.2
7.5
Thé dúc
⊗
⊗
Trần Thị Luận Minh
DTB
các môn
7.4"""


def test_keep_score_lines_keeps_vertical_table_and_drops_teacher_names():
    kept = keep_score_lines(VERTICAL_TABLE).splitlines()
    for line in ("Hký I", "Hký II", "CN", "Toán", "6.2", "7.7", "7.2", "Ngoại ngũ", "Gung...inh", "Thé dúc", "⊗", "DTB", "7.4"):
        assert line in kept
    assert "Nguyen Huong Ngun" not in kept
    assert "Trần Thị Luận Minh" not in kept


def test_keep_score_lines_drops_long_sentence_before_number():
    assert keep_score_lines("Nhận xét của giáo viên chủ nhiệm về học sinh này là rất tốt\n9") == "9"


def test_keep_score_lines_cuts_unlabeled_name_before_class():
    # OCR làm mất nhãn "Họ và tên:" (gặp thật ở ảnh học bạ): chỉ gửi phần lớp / năm học
    kept = keep_score_lines("Nguyễn Thanh Trà Lớp: 12Tg Năm học 2010. 2011\nToán 10.0 10.0 10.0")
    assert kept.splitlines() == ["Lớp: 12Tg Năm học 2010. 2011", "Toán 10.0 10.0 10.0"]


def test_keep_score_lines_keeps_whole_table_row_with_term():
    row = "| Lớp 10 HK1 | 8.5 | 7.0 |"
    assert keep_score_lines(row) == row


def test_keep_score_lines_keeps_term_header_with_misread_ky():
    # Ảnh thật anh-chup-man-hinh: OCR chép "Học kỳ" thành "Hoc kg", trước đây chỉ còn "Ca nám" nên mất cột HK1, HK2
    header = "Hoc kg I Hoc kg II Ca nám"
    assert keep_score_lines(f"{header}\nToàn 42 47 45").splitlines()[0] == header
    assert keep_score_lines("Nguyễn Văn Học Kim Lớp 12A\nToán 8.0 8.5 8.3").splitlines()[0] == "Lớp 12A"
