"""Việc 2.5: file log chat. Lỗi ghi file không được làm sập dịch vụ, log phải giữ đúng số ngày cấu hình."""

import logging
from logging.handlers import TimedRotatingFileHandler
from types import SimpleNamespace

import pytest

from app import main
from app.core.config import Settings


@pytest.fixture
def chat_logger():
    """Logger riêng cho test (không dính log thật của advisor), dọn sạch handler khi xong."""
    log = logging.getLogger("advisor.test_chat_log_file")
    log.setLevel(logging.INFO)
    log.propagate = False
    yield log
    for handler in list(log.handlers):
        handler.close()
        log.removeHandler(handler)


def file_handlers(log: logging.Logger) -> list[logging.Handler]:
    """Chỉ các handler ghi file (pytest tự gắn thêm handler thu log vào logger)."""
    return [h for h in log.handlers if isinstance(h, logging.FileHandler)]


def use_settings(monkeypatch, path, keep_days=90):
    fake = SimpleNamespace(chat_log_file=str(path) if path else "", chat_log_keep_days=keep_days)
    monkeypatch.setattr(main, "get_settings", lambda: fake)


def test_default_keeps_90_days_of_chat_logs():
    assert Settings(_env_file=None).chat_log_keep_days == 90


def test_chat_turns_are_written_to_the_file_and_other_lines_are_not(tmp_path, monkeypatch, chat_logger):
    path = tmp_path / "logs" / "chat.log"
    use_settings(monkeypatch, path)
    main.add_chat_file_handler(chat_logger)

    chat_logger.info('chat_turn {"message": "xin chào"}')
    chat_logger.info("một dòng log khác")
    for handler in file_handlers(chat_logger):
        handler.flush()

    text = path.read_text(encoding="utf-8")  # thư mục logs/ chưa có cũng được tạo, tiếng Việt không hỏng
    assert "xin chào" in text
    assert "một dòng log khác" not in text


def test_log_files_are_kept_for_the_configured_number_of_days(tmp_path, monkeypatch, chat_logger):
    use_settings(monkeypatch, tmp_path / "chat.log", keep_days=120)

    main.add_chat_file_handler(chat_logger)

    (handler,) = file_handlers(chat_logger)
    assert isinstance(handler, TimedRotatingFileHandler)
    assert handler.backupCount == 120  # file cũ hơn số ngày này bị xóa
    assert handler.when == "MIDNIGHT"  # mỗi ngày một file


def test_no_file_handler_when_no_path_is_configured(monkeypatch, chat_logger):
    use_settings(monkeypatch, "")

    main.add_chat_file_handler(chat_logger)

    assert file_handlers(chat_logger) == []


def test_unwritable_path_does_not_crash_the_service(tmp_path, monkeypatch, chat_logger):
    blocker = tmp_path / "file.txt"
    blocker.write_text("x", encoding="utf-8")
    use_settings(monkeypatch, blocker / "chat.log")  # "thư mục" cha lại là một file nên không tạo được

    main.add_chat_file_handler(chat_logger)  # không ném lỗi

    assert file_handlers(chat_logger) == []
