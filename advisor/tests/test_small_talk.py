"""Nhận ra câu xã giao ngắn (chào, cảm ơn, tạm biệt) bằng luật."""

import pytest

from app.services.small_talk import classify


@pytest.mark.parametrize(
    ("message", "kind"),
    [
        ("chào bạn", "greeting"),
        ("Xin chào!", "greeting"),
        ("hello", "greeting"),
        ("chao ban", "greeting"),
        ("cảm ơn bạn", "thanks"),
        ("em cảm ơn ạ", "thanks"),
        ("thanks nhé", "thanks"),
        ("cảm ơn bạn, tạm biệt", "goodbye"),
        ("tạm biệt", "goodbye"),
        ("bye", "goodbye"),
        ("hẹn gặp lại", "goodbye"),
    ],
)
def test_short_social_messages_are_recognised(message, kind):
    assert classify(message) == kind


@pytest.mark.parametrize(
    "message",
    [
        "chào bạn, IELTS là gì",  # có câu hỏi đi kèm
        "cảm ơn, vậy còn học bổng thì sao",
        "bạn tên gì",
        "hôm nay trời đẹp nhỉ",
        "ok",
        "???",
        "",
        "tạm biệt bạn nhưng trước đó cho em hỏi visa F-1 là gì",  # dài và có nội dung
    ],
)
def test_messages_with_content_or_off_topic_are_not_small_talk(message):
    assert classify(message) is None
