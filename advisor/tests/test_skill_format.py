"""Khuôn chung của mọi file skill trong app/skills (xem docs/SKILL_FORMAT.md).

Mọi skill (trừ prompt của SLM) có cùng dòng tiêu đề và cùng bộ mục theo cùng thứ tự, nên người mới đọc skill nào cũng biết tìm gì ở đâu.
Skill mới chưa khai báo trong WRITERS hoặc READERS thì test này đỏ: phải chọn khuôn rồi mới thêm được.
"""

import re
from pathlib import Path

import pytest

from app.services.skill_call import SKILLS_DIR

# Skill viết lời (Claude viết chữ cho học sinh đọc): mục 4 là NỘI DUNG CẦN NÓI
WRITERS = {
    "skill_explain.md",
    "skill_giai_thich_trung_tam.md",
    "skill_goal_seek.md",
    "skill_ho_so_khuon.md",
    "skill_kich_ban.md",
    "skill_neu_doi.md",
    "skill_so_sanh.md",
    "skill_tra_loi.md",
    "viet_tra_loi_ho_so.md",
}
# Skill đọc hiểu (đọc câu của học sinh ra JSON, hoặc chọn một mục trong danh sách): mục 4 là CÁCH ĐỌC
READERS = {
    "skill_bac_hoc.md",
    "skill_chon_truong.md",
    "skill_ec.md",
    "skill_ho_so.md",
}
# Prompt của SLM: model đã học đúng chữ này nên không đổi khuôn (xem slm/README.md)
EXEMPT = {"slm_intents.md"}

PRINCIPLE = "NGUYÊN TẮC DỮ LIỆU (BẮT BUỘC)"


def sections(kind_of_four: str) -> list[str]:
    return [
        PRINCIPLE,
        "0. VAI TRÒ",
        "1. NHIỆM VỤ",
        "2. DỮ LIỆU BẠN NHẬN",
        "3. CÁCH HIỂU DỮ LIỆU",
        f"4. {kind_of_four}",
        "5. GIỚI HẠN VÀ ĐIỀU CẤM",
        "6. ĐỊNH DẠNG TRẢ LỜI",
        "7. VÍ DỤ",
    ]


# viet_tra_loi_ho_so là phần luật chung đứng trước skill riêng của từng học sinh, nên được thêm đúng một mục cuối
TRAILING = {"viet_tra_loi_ho_so.md": "SKILL CỦA HỌC SINH NÀY"}

ALL_FILES = sorted(p.name for p in Path(SKILLS_DIR).glob("*.md"))
FORMATTED = sorted(WRITERS | READERS)


def read(name: str) -> str:
    return (Path(SKILLS_DIR) / name).read_text(encoding="utf-8")


def headings(text: str) -> list[str]:
    return [line[3:].strip() for line in text.splitlines() if line.startswith("## ")]


def test_every_skill_file_is_declared_in_exactly_one_family():
    declared = WRITERS | READERS | EXEMPT

    assert set(ALL_FILES) == declared, (
        f"chưa khai báo: {set(ALL_FILES) - declared}, đã khai báo nhưng không có file: {declared - set(ALL_FILES)}"
    )
    assert not (WRITERS & READERS), "một skill không thể vừa viết lời vừa đọc hiểu"


@pytest.mark.parametrize("name", FORMATTED)
def test_the_first_line_is_a_title_with_the_version(name):
    first = read(name).splitlines()[0]

    assert re.fullmatch(r"# .+ - skill_version \d+\.\d+", first), first


@pytest.mark.parametrize("name", sorted(WRITERS))
def test_a_writer_skill_has_the_common_sections_in_order(name):
    expected = sections("NỘI DUNG CẦN NÓI") + ([TRAILING[name]] if name in TRAILING else [])

    assert headings(read(name)) == expected


@pytest.mark.parametrize("name", sorted(READERS))
def test_a_reader_skill_has_the_common_sections_in_order(name):
    assert headings(read(name)) == sections("CÁCH ĐỌC")


@pytest.mark.parametrize("name", FORMATTED)
def test_the_principle_block_comes_first_and_is_not_empty(name):
    text = read(name)
    block = text.split(f"## {PRINCIPLE}", 1)[1].split("\n## ", 1)[0]

    assert block.strip().count("\n- ") >= 1 or block.strip().startswith("- ")


@pytest.mark.parametrize("name", sorted(WRITERS))
def test_every_writing_skill_says_to_use_only_the_data_sent_and_never_invent(name):
    block = read(name).split(f"## {PRINCIPLE}", 1)[1].split("\n## ", 1)[0]

    assert "không bịa" in block.lower() and "CÓ TRONG" in block


def test_the_slm_prompt_is_left_alone_but_still_has_a_version():
    assert re.search(r"skill_version \d+\.\d+", read("slm_intents.md").splitlines()[0])
