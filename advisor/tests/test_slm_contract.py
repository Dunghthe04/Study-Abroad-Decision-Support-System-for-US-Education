"""Hợp đồng SLM: khuôn đầu ra phải đúng và SLM giả dùng được."""

from typing import get_args

import pytest
from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import INTENTS, SLM_SCHEMA, Intent, SlmEntities, SlmRead, SlmResult
from app.services.slm_contract import FixedSlm


def test_a_typical_answer_from_the_model_is_read_with_defaults_for_the_rest():
    read = SlmRead.model_validate(
        {"type": "trung_tam", "entities": {"service": "SUA_BAI_LUAN", "city": "Hà Nội"}}
    )

    assert read.type == "trung_tam" and read.has_profile is False
    assert (read.entities.service, read.entities.city, read.entities.school) == (
        "SUA_BAI_LUAN",
        "Hà Nội",
        None,
    )


def test_an_unknown_type_is_rejected():
    with pytest.raises(ValidationError):
        SlmRead.model_validate({"type": "nhu_cau"})


@pytest.mark.parametrize("value", [-0.1, 1.2])
def test_confidence_must_be_between_0_and_1(value):
    with pytest.raises(ValidationError):
        SlmResult(type="kien_thuc", confidence=value)


@pytest.mark.parametrize(
    ("intent", "has_profile", "expected"),
    [
        ("ho_so", False, True),  # chỉ trả lời câu hồ sơ đang chờ
        ("kien_thuc", True, True),  # trả lời hồ sơ và hỏi thêm một ý khác
        ("kien_thuc", False, False),
        ("ngoai_pham_vi", False, False),
    ],
)
def test_the_profile_handler_runs_for_profile_answers_or_messages_that_carry_profile_values(
    intent, has_profile, expected
):
    assert SlmResult(type=intent, has_profile=has_profile).touches_profile() is expected


def test_the_json_schema_lists_exactly_the_types_the_code_knows():
    assert SLM_SCHEMA["properties"]["type"]["enum"] == list(INTENTS) == list(get_args(Intent))


# Khuôn ép SLM và class đọc kết quả phải cùng tên khóa, nếu lệch thì mọi câu trả lời của model đều bị từ chối
def test_the_json_schema_has_the_same_keys_as_the_classes_that_read_it():
    assert set(SLM_SCHEMA["required"]) == set(SLM_SCHEMA["properties"]) == set(SlmRead.model_fields)
    entities = SLM_SCHEMA["properties"]["entities"]
    assert set(entities["required"]) == set(entities["properties"]) == set(SlmEntities.model_fields)


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmResult(type="ho_so", confidence=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.type, result.confidence) == ("ho_so", 0.9)
