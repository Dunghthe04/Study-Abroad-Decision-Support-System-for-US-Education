"""Lỗi 422 của advisor phải ghi rõ trường nào sai, nhưng không ghi giá trị học sinh đã gõ."""

import logging

from fastapi.testclient import TestClient

from app.api.deps import get_chat_flow
from app.main import create_app


def test_a_422_logs_the_field_and_reason_but_not_the_value(caplog):
    app = create_app()
    app.dependency_overrides[get_chat_flow] = lambda: (
        None
    )  # không cần ChatFlow thật: lỗi 422 xảy ra trước khi gọi
    client = TestClient(app)
    secret = "x" * 4001  # quá dài: ChatMessage.content tối đa 4000 ký tự

    with caplog.at_level(logging.WARNING, logger="advisor.api"):
        response = client.post(
            "/api/v1/chat/stream", json={"messages": [{"role": "user", "content": secret}]}
        )

    assert response.status_code == 422
    text = " ".join(record.getMessage() for record in caplog.records)
    assert "body.messages.0.content" in text and "string_too_long" in text
    assert secret not in text
