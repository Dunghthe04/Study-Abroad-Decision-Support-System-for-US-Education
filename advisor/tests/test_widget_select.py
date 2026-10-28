"""Widget "select": ô chọn trong chat (cấp học, lớp, bang, loại chứng chỉ...). Chọn xong, giao diện gửi value như tin nhắn."""

import pytest
from pydantic import ValidationError

from app.schemas.chat import ChatResponse, ChatState, Widget

STATES = {
    "field": "preferred_state",
    "options": [{"label": "California", "value": "California"}, {"label": "Texas", "value": "Texas"}],
    "multiple": True,
}


def test_select_is_an_allowed_widget_type():
    assert Widget(type="select", data=STATES).type == "select"


def test_unknown_widget_types_are_still_rejected():
    with pytest.raises(ValidationError):
        Widget(type="dropdown", data=STATES)


def test_select_reaches_the_client_as_json_with_its_options():
    res = ChatResponse(
        answer="Bạn muốn học ở bang nào?", state=ChatState(), widget=Widget(type="select", data=STATES)
    )

    body = res.model_dump(by_alias=True)

    assert body["widget"]["type"] == "select"
    assert body["widget"]["data"]["options"][1] == {"label": "Texas", "value": "Texas"}
    assert body["widget"]["data"]["multiple"] is True
