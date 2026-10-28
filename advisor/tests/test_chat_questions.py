"""Bảng câu hỏi trong code phải khớp đúng với câu hỏi trong dữ liệu train của model chọn skill."""

from app.services.chat_flow import QUESTIONS
from ml.skill_picker.data import load_rows


# Các câu last_question khác nhau trong dữ liệu train (bỏ dòng rỗng: nghĩa là không có câu nào đang chờ)
def train_questions() -> set[str]:
    return {r["last_question"].strip() for r in load_rows() if r["last_question"].strip()}


def test_every_code_question_is_in_train_data():
    missing_in_data = set(QUESTIONS.values()) - train_questions()

    assert not missing_in_data, f"Câu trong code không có trong dữ liệu train: {missing_in_data}"


def test_every_train_question_is_in_code():
    missing_in_code = train_questions() - set(QUESTIONS.values())

    assert not missing_in_code, f"Câu trong dữ liệu train chưa có trong QUESTIONS: {missing_in_code}"


def test_question_ids_are_unique_texts():
    # Hai mã không được dùng chung một câu, nếu không từ câu không biết đang chờ mã nào
    assert len(set(QUESTIONS.values())) == len(QUESTIONS)
