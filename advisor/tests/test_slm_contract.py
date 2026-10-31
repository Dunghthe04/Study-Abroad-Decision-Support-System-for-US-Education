"""Hợp đồng SLM: khuôn đầu ra phải đúng và SLM giả dùng được."""

from typing import get_args

import pytest
from app.services.slm_contract import FixedSlm
from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import INTENTS, SLM_SCHEMA, Intent, SlmRead, SlmResult


def test_a_typical_answer_from_the_model_is_read_with_defaults_for_the_rest():
    read = SlmRead.model_validate(
        {"loai": "trung_tam", "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Hà Nội"}}
    )

    assert read.loai == "trung_tam" and read.co_ho_so is False
    assert (read.thuc_the.dich_vu, read.thuc_the.thanh_pho, read.thuc_the.truong) == (
        "SUA_BAI_LUAN",
        "Hà Nội",
        None,
    )


def test_an_unknown_type_is_rejected():
    with pytest.raises(ValidationError):
        SlmRead.model_validate({"loai": "nhu_cau"})


@pytest.mark.parametrize("value", [-0.1, 1.2])
def test_confidence_must_be_between_0_and_1(value):
    with pytest.raises(ValidationError):
        SlmResult(loai="kien_thuc", tin_cay=value)


@pytest.mark.parametrize(
    ("loai", "co_ho_so", "expected"),
    [
        ("ho_so", False, True),  # chỉ trả lời câu hồ sơ đang chờ
        ("kien_thuc", True, True),  # trả lời hồ sơ và hỏi thêm một ý khác
        ("kien_thuc", False, False),
        ("ngoai_pham_vi", False, False),
    ],
)
def test_the_profile_handler_runs_for_profile_answers_or_messages_that_carry_profile_values(
    loai, co_ho_so, expected
):
    assert SlmResult(loai=loai, co_ho_so=co_ho_so).touches_profile() is expected


def test_the_json_schema_lists_exactly_the_types_the_code_knows():
    assert SLM_SCHEMA["properties"]["loai"]["enum"] == list(INTENTS) == list(get_args(Intent))
    assert set(SLM_SCHEMA["required"]) == {"loai", "co_ho_so", "thuc_the"}


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmResult(loai="ho_so", tin_cay=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.loai, result.tin_cay) == ("ho_so", 0.9)
