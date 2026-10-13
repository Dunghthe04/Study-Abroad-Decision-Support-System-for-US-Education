from app.services.transcript_text import cut_repetition, tabulate_spaced_tables

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
    text = f"{header}\nHK1 7.2 4.2 7.8 5.1 6.7\n{header}\nHK1 7.7 7.3 9 7.8 6.5\n{header}\nHK1 8.1 7.4 8.5 7.7 7"
    assert cut_repetition(text) == text
