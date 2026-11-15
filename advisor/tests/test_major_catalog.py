"""Đổi chữ ngành học sinh gõ sang ngành trong bảng majors, và chat hỏi lại khi chỉ gần giống hoặc không khớp."""

import pytest

from app.services.skills import major_catalog
from app.services.skills.major_catalog import Major, match, normalize
from tests.fakes import FakeLLM
from tests.test_chat_skills_flow import field, nothing_bac_hoc, turn

CS = Major("Computer and information sciences", "Khoa học máy tính và thông tin", ("cntt", "may tinh"), True)
BUSINESS = Major("Business/marketing", "Kinh doanh và marketing", ("quan tri kinh doanh",), True)
PSYCHOLOGY = Major("Psychology", "Tâm lý học")
LAW = Major("Law/legal studies", "Luật", ("luat",))
MAJORS = [CS, BUSINESS, PSYCHOLOGY, LAW]

# Đang hỏi B1.3, đã có cấp học và bậc học
B13 = {
    "current_step": "B1",
    "question_id": "B1.3",
    "asked": {"B1.3": 1},
    "profile": {"current_level": field("high_school"), "target_level": field("undergraduate")},
}


# Qwen đọc ra ngành này (None: Qwen không được gọi)
def qwen(major: str | None = None) -> FakeLLM:
    return FakeLLM({**nothing_bac_hoc(), "major": major}) if major else FakeLLM()


@pytest.fixture
def catalog():
    major_catalog.set_majors(MAJORS)
    yield
    major_catalog.set_majors(None)


def test_normalize_drops_accents_case_and_punctuation():
    assert normalize("  Khoa học Máy tính, Đồ họa! ") == "khoa hoc may tinh do hoa"


def test_exact_match_by_english_vietnamese_or_alias():
    assert match("Computer and information sciences", MAJORS).exact == CS
    assert match("kinh doanh và Marketing", MAJORS).exact == BUSINESS
    assert match("CNTT", MAJORS).exact == CS
    assert match("luật", MAJORS).exact == LAW


def test_near_match_returns_suggestions_without_exact():
    found = match("khoa học máy tính", MAJORS)
    assert found.exact is None
    assert found.near == [CS]
    assert match("tâm lý hok", MAJORS).near == [PSYCHOLOGY]


def test_no_match_returns_nothing():
    found = match("vũ trụ", MAJORS)
    assert (found.exact, found.near) == (None, [])
    assert match("   ", MAJORS).near == []


async def test_without_catalog_major_is_kept_as_typed():
    major_catalog.set_majors([])
    try:
        res = await turn(qwen("CS"), "CS", **B13)
    finally:
        major_catalog.set_majors(None)

    assert res.state.profile["major"] == field("CS")


async def test_picking_vietnamese_label_saves_english_name_without_qwen(catalog):
    llm = qwen()
    res = await turn(llm, "Khoa học máy tính và thông tin", **B13)

    assert res.state.profile["major"] == field(CS.name)
    assert llm.calls == []
    assert res.state.question_id == "B1.4"


async def test_major_read_by_qwen_is_saved_as_english_name(catalog):
    res = await turn(qwen("luật"), "em muốn học luật", **B13)

    assert res.state.profile["major"] == field(LAW.name)


async def test_near_major_asks_again_with_suggestion_buttons(catalog):
    res = await turn(qwen("khoa học máy tính"), "em thích khoa học máy tính", **B13)

    assert "major" not in res.state.profile
    assert res.state.question_id == "B1.3"
    assert res.answer.startswith(
        'Mình thấy câu trả lời của bạn gần với ngành "Khoa học máy tính và thông tin"'
    )
    assert [r.label for r in res.quick_replies] == [CS.label, "Em chưa rõ ngành"]
    assert res.widget.type == "major"


async def test_several_near_majors_are_all_shown(catalog):
    major_catalog.set_majors([*MAJORS, Major("Computer engineering", "Kỹ thuật máy tính")])
    res = await turn(qwen("computer"), "ngành computer", **B13)

    assert "gần với các ngành" in res.answer
    assert [r.label for r in res.quick_replies][:2] == [CS.label, "Kỹ thuật máy tính"]


async def test_unmatched_major_asks_to_type_again(catalog):
    res = await turn(qwen("vũ trụ"), "em muốn học vũ trụ", **B13)

    assert "major" not in res.state.profile
    assert res.answer.startswith('Mình chưa tìm thấy ngành "vũ trụ"')
    assert [r.label for r in res.quick_replies] == ["Em chưa rõ ngành"]
