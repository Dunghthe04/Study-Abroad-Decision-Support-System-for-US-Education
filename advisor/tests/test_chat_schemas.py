"""Hợp đồng chat (việc 1.4): các trường mới phải có mặc định rỗng, đúng kiểu, và trả camelCase cho .NET."""

import pytest
from pydantic import ValidationError

from app.schemas.chat import (
    ChatMessage,
    ChatRequest,
    ChatResponse,
    ChatState,
    ProfileField,
    QuickReply,
    Widget,
)


def test_new_state_fields_default_to_empty():
    state = ChatState()

    assert state.intake_mode is None
    assert state.has_results is False


def test_state_from_old_json_without_new_fields_still_loads():
    state = ChatState.model_validate({"currentStep": "B2", "lastQuestion": "Bạn đã thi SAT chưa?"})

    assert state.intake_mode is None and state.has_results is False


def test_intake_mode_only_accepts_chat_or_form():
    assert ChatState(intake_mode="form").intake_mode == "form"
    with pytest.raises(ValidationError):
        ChatState(intake_mode="email")


def test_widget_type_must_be_a_known_kind():
    assert Widget(type="major").data == {}
    assert Widget(type="form").data == {}  # widget báo frontend mở form hồ sơ
    with pytest.raises(ValidationError):
        Widget(type="slider")


def test_request_accepts_profile_from_dotnet_and_works_without_it():
    messages = [ChatMessage(role="user", content="chào")]
    with_profile = ChatRequest.model_validate(
        {
            "messages": [{"role": "user", "content": "chào"}],
            "profile": {"gpa": {"value": {"value": 8.5, "scale": 10}, "status": "confirmed"}},
        }
    )
    without = ChatRequest(messages=messages)

    assert with_profile.profile is not None and with_profile.profile["gpa"].status == "confirmed"
    assert without.profile is None


def test_request_accepts_null_profile_as_dotnet_sends_it():
    req = ChatRequest.model_validate({"messages": [{"role": "user", "content": "chào"}], "profile": None})

    assert req.profile is None


def test_response_defaults_are_empty_and_serialised_as_camel_case():
    body = ChatResponse(answer="ok", state=ChatState()).model_dump(by_alias=True)

    assert body["quickReplies"] == []
    assert body["widget"] is None
    assert body["profile"] == {}
    assert body["profileChanged"] == []
    assert body["state"]["intakeMode"] is None and body["state"]["hasResults"] is False


def test_response_with_buttons_and_widget_round_trips_as_camel_case():
    res = ChatResponse(
        answer="ok",
        state=ChatState(),
        quick_replies=[QuickReply(label="Điền bằng form", value="Điền bằng form")],
        widget=Widget(type="review", data={"sections": 5}),
        profile={"budget": ProfileField(value="tier_20k_35k", status="confirmed")},
        profile_changed=["budget"],
    )

    body = res.model_dump(by_alias=True)

    assert body["quickReplies"] == [{"label": "Điền bằng form", "value": "Điền bằng form", "href": None}]
    assert body["widget"] == {"type": "review", "data": {"sections": 5}}
    assert body["profileChanged"] == ["budget"]
    assert body["profile"]["budget"]["status"] == "confirmed"


def test_a_quick_reply_can_carry_a_link_that_the_browser_follows_instead_of_sending_the_text():
    body = QuickReply(
        label="Trang Trung tâm", value="Em muốn xem trang trung tâm", href="/centers"
    ).model_dump(by_alias=True)

    assert body == {"label": "Trang Trung tâm", "value": "Em muốn xem trang trung tâm", "href": "/centers"}


# ---------- media: ảnh minh họa của câu trả lời ----------


def test_media_is_serialized_in_camel_case_and_empty_by_default():
    from app.schemas.chat import Media

    empty = ChatResponse(answer="x", state=ChatState())
    with_image = ChatResponse(
        answer="x", state=ChatState(), media=[Media(kind="image", url="https://a.org/i.png", alt="Mẫu I-20")]
    )

    assert empty.model_dump(by_alias=True, mode="json")["media"] == []
    assert with_image.model_dump(by_alias=True, mode="json")["media"] == [
        {"kind": "image", "url": "https://a.org/i.png", "alt": "Mẫu I-20"}
    ]


def test_media_only_accepts_https_links():
    from pydantic import ValidationError

    from app.schemas.chat import Media

    for bad in ("http://a.org/i.png", "javascript:alert(1)", "//a.org/i.png", "i.png"):
        with pytest.raises(ValidationError):
            Media(kind="image", url=bad, alt="x")


def test_combine_merges_media_without_duplicates():
    from app.schemas.chat import Media
    from app.services.chat_flow import SkillOutput, combine

    a = Media(kind="image", url="https://a.org/1.png", alt="1")
    b = Media(kind="image", url="https://a.org/2.png", alt="2")

    merged = combine(SkillOutput("một", media=[a]), SkillOutput("hai", media=[a, b]))

    assert [m.url for m in merged.media] == ["https://a.org/1.png", "https://a.org/2.png"]
