"""Kết quả gợi ý trung tâm luôn nói rõ không trung tâm nào bảo đảm được visa hay trúng tuyển.

Ngày 27/11/2026 học sinh hỏi "trung tâm nào cam kết đậu visa 100% thì giới thiệu mình" chỉ nhận danh sách trung tâm.
Câu lưu ý đi kèm MỌI danh sách, không dò chữ trong câu hỏi để quyết định có nói hay không."""

from app.services.chat_flow import SkillOutput, combine
from app.services.replies import CENTER_DISCLAIMER, KNOWLEDGE_DISCLAIMER


def test_the_center_note_says_nobody_can_guarantee_a_visa_or_admission():
    note = CENTER_DISCLAIMER.lower()

    assert "không trung tâm nào bảo đảm được" in note
    assert "visa" in note and "trúng tuyển" in note
    assert "viên chức lãnh sự" in note  # nói ai mới là người quyết định


def test_a_message_with_two_answers_keeps_both_notes():
    # Trước đây chỉ giữ một câu lưu ý: hỏi kiến thức kèm xin trung tâm thì một trong hai câu bị mất
    both = combine(
        SkillOutput("kiến thức", disclaimer=KNOWLEDGE_DISCLAIMER),
        SkillOutput("trung tâm", disclaimer=CENTER_DISCLAIMER),
    )

    assert both.disclaimer == f"{KNOWLEDGE_DISCLAIMER}\n{CENTER_DISCLAIMER}"


def test_the_same_note_is_said_once_and_a_missing_note_changes_nothing():
    same = combine(SkillOutput("a", disclaimer="X"), SkillOutput("b", disclaimer="X"))
    one = combine(SkillOutput("a", disclaimer="X"), SkillOutput("b"))

    assert (same.disclaimer, one.disclaimer) == ("X", "X")
    assert combine(SkillOutput("a"), SkillOutput("b")).disclaimer is None


def test_joining_three_answers_does_not_repeat_a_note():
    first = combine(SkillOutput("a", disclaimer="X"), SkillOutput("b", disclaimer="Y"))

    assert combine(first, SkillOutput("c", disclaimer="X")).disclaimer == "X\nY"
