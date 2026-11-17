"""Câu cố định (app/services/replies.py): xưng hô, và mọi câu ngoài phạm vi đều có nút đi tiếp."""

import re

import pytest

from app.schemas.chat import ChatState, QuickReply
from app.schemas.slm import SUBTYPES
from app.services import replies
from app.services.branches import build_branches
from tests.fakes import tin

BRANCH = build_branches({})["out_of_scope"]
MENU_LABELS = [b.label for b in replies.menu_buttons()]
# Quy ước xưng hô chốt theo số đông của dữ liệu: trợ lý xưng "mình", gọi học sinh là "bạn" (câu hệ thống);
# chữ gửi đi khi học sinh bấm nút là tin của học sinh nên theo giọng học sinh: "em". Không dùng "tôi" ở đâu cả.
BOT_FORBIDDEN = re.compile(r"\b(tôi|em)\b", re.IGNORECASE)
STUDENT_FORBIDDEN = re.compile(r"\b(tôi|mình)\b", re.IGNORECASE)
# Mẫu câu hệ thống gửi lại như tin của học sinh (không phải lời trợ lý nói)
STUDENT_TEMPLATES = {
    "DEFAULT_QUESTION",
    "EDIT_SECTION_VALUE",
    "EXPLAIN_BUTTON_VALUE",
    "CENTER_CITY_ASK",
    "CENTER_SERVICE_VALUE",
    "CENTER_SERVICE_VALUE_IN_CITY",
}


def bot_texts(value):
    """Câu trợ lý nói ra, và nhãn nút (nhãn là chữ trợ lý đặt)."""
    if isinstance(value, str):
        yield value
    elif isinstance(value, QuickReply):
        yield value.label
    elif isinstance(value, dict):
        for item in value.values():
            yield from bot_texts(item)
    elif isinstance(value, list):
        for item in value:
            yield from bot_texts(item)


def student_texts(value):
    """Chữ gửi đi khi học sinh bấm nút (value)."""
    if isinstance(value, QuickReply):
        yield value.value
    elif isinstance(value, dict):
        for item in value.values():
            yield from student_texts(item)
    elif isinstance(value, list):
        for item in value:
            yield from student_texts(item)


def test_the_assistant_says_minh_and_calls_the_student_ban():
    bad = []
    for name, value in vars(replies).items():
        if name.isupper() and name not in STUDENT_TEMPLATES:
            bad += [(name, t) for t in bot_texts(value) if BOT_FORBIDDEN.search(t)]
    assert bad == []


def test_text_sent_when_a_student_presses_a_button_is_in_the_student_voice():
    bad = []
    for name, value in vars(replies).items():
        if not name.isupper():
            continue
        texts = [value] if name in STUDENT_TEMPLATES else list(student_texts(value))
        bad += [(name, t) for t in texts if STUDENT_FORBIDDEN.search(t)]
    assert bad == []


def test_the_menu_offers_the_main_ways_forward_and_copies_are_independent():
    buttons = replies.menu_buttons()

    assert [b.label for b in buttons] == [
        "Gợi ý trường từ hồ sơ",
        "Đánh giá hồ sơ",
        "Thử thay đổi hồ sơ",
        "Hỏi về visa, học phí",
        "Tìm trung tâm tư vấn",
    ]
    buttons[0].label = "đã sửa"
    assert replies.menu_buttons()[0].label == "Gợi ý trường từ hồ sơ"


def test_menu_can_leave_out_the_button_for_the_current_task():
    labels = [b.label for b in replies.menu_buttons("knowledge", "centers")]

    assert "Hỏi về visa, học phí" not in labels and "Tìm trung tâm tư vấn" not in labels
    assert len(labels) == 3


@pytest.mark.parametrize("kind", SUBTYPES)
async def test_no_out_of_scope_answer_is_a_dead_end(kind):
    out = await BRANCH.run(ChatState(), "xin hỏi", tin(("out_of_scope", {"subtype": kind})))

    assert out.answer
    assert [b.label for b in out.quick_replies] == MENU_LABELS


async def test_asking_what_the_system_is_gets_its_own_answer_not_the_unclear_one():
    out = await BRANCH.run(ChatState(), "bạn là ai", tin(("out_of_scope", {"subtype": "about_system"})))

    assert out.answer == replies.ABOUT_SYSTEM
    assert out.answer != replies.OUT_OF_SCOPE["ambiguous"]
