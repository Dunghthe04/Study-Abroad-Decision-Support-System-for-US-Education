"""Bảng câu hỏi trong code: mỗi mã phải có một câu riêng."""

from app.services.chat_flow import QUESTIONS


def test_question_ids_are_unique_texts():
    # Hai mã không được dùng chung một câu, nếu không từ câu không biết đang chờ mã nào
    assert len(set(QUESTIONS.values())) == len(QUESTIONS)
