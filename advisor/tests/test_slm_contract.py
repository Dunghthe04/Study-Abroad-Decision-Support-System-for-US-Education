"""Hợp đồng SLM: khuôn đầu ra phải đúng, khớp với dữ liệu gán nhãn, và SLM giả dùng được."""

import json
from typing import get_args

import pytest
from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import ENTITY_KEYS, INTENTS, PROFILE_KEYS, SLM_SCHEMA, Intent, SlmRead, SlmResult
from app.services.centers import SERVICES
from app.services.skills import info_to_field
from app.services.slm_contract import FixedSlm
from scripts.build_slm_jsonl import DEFAULT_OUT


def test_a_typical_answer_from_the_model_is_read_with_defaults_for_the_rest():
    read = SlmRead.model_validate(
        {"type": "trung_tam", "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Hà Nội"}}
    )

    assert read.type == "trung_tam" and read.nhan_phu is None
    assert read.thuc_the == {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Hà Nội"}


@pytest.mark.parametrize(
    "answer",
    [
        {"type": "nhu_cau"},  # loại lạ
        {"type": "kien_thuc", "nhan_phu": "kien_thuc"},  # nhãn phụ trùng nhãn chính
        {"type": "trung_tam", "nhan_phu": "kien_thuc"},  # sai thứ tự ưu tiên: phải là kien_thuc + trung_tam
        {"type": "kien_thuc", "nhan_phu": "mac_dinh"},  # giá trị hồ sơ nằm trong thuc_the, không làm nhãn phụ
        {
            "type": "ngoai_pham_vi",
            "nhan_phu": "kien_thuc",
            "thuc_the": {"loai_con": "tam_biet"},
        },  # câu chào bị bỏ
        {"type": "kien_thuc", "thuc_the": {"dich_vu": "SUA_BAI_LUAN"}},  # khóa của loại khác
        {"type": "trung_tam", "thuc_the": {"dich_vu": "LAM_VISA_HO"}},  # mã dịch vụ không có trong hệ thống
        {"type": "kich_ban", "thuc_the": {"che_do": "what_if"}},  # what-if mà không nói đổi gì
        {"type": "ngoai_pham_vi"},  # thiếu loai_con
    ],
)
def test_answers_that_break_the_rules_are_rejected(answer):
    with pytest.raises(ValidationError):
        SlmRead.model_validate(answer)


@pytest.mark.parametrize("value", [-0.1, 1.2])
def test_confidence_must_be_between_0_and_1(value):
    with pytest.raises(ValidationError):
        SlmResult(type="kien_thuc", confidence=value)


@pytest.mark.parametrize(
    ("result", "expected"),
    [
        (SlmResult(type="mac_dinh"), True),  # trả lời câu hồ sơ đang chờ (kể cả "ừ" mơ hồ, Việt hỏi lại)
        (SlmResult(type="kien_thuc", thuc_the={"gpa": "GPA em 8.5"}), True),  # hỏi kèm giá trị hồ sơ
        (SlmResult(type="kien_thuc", thuc_the={"truong": "Iowa"}), False),
        (SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "tam_biet"}), False),
    ],
)
def test_the_profile_handler_runs_for_profile_answers_or_messages_that_carry_profile_values(result, expected):
    assert result.touches_profile() is expected


def test_profile_values_are_only_the_profile_keys_in_the_entities():
    result = SlmResult(type="trung_tam", thuc_the={"dich_vu": "LUYEN_THI", "english": "IELTS 6.0"})

    assert result.profile_values() == {"english": "IELTS 6.0"}


def test_the_json_schema_lists_exactly_the_types_the_code_knows():
    assert SLM_SCHEMA["properties"]["type"]["enum"] == list(INTENTS) == list(get_args(Intent))
    assert SLM_SCHEMA["properties"]["thuc_the"]["properties"]["dich_vu"]["enum"] == list(SERVICES)


# Khuôn ép SLM và class đọc kết quả phải cùng tên khóa, nếu lệch thì mọi câu trả lời của model đều bị từ chối
def test_the_json_schema_has_the_same_keys_as_the_classes_that_read_it():
    assert set(SLM_SCHEMA["required"]) == set(SLM_SCHEMA["properties"]) == set(SlmRead.model_fields)
    entity_keys = set().union(*ENTITY_KEYS.values(), PROFILE_KEYS)
    assert set(SLM_SCHEMA["properties"]["thuc_the"]["properties"]) == entity_keys


def test_profile_keys_are_the_same_as_the_ones_the_chat_saves():
    assert PROFILE_KEYS == info_to_field.PROFILE_KEYS


# Đầu ra trong dữ liệu chính là thứ SLM học để trả, nên dòng nào cũng phải đọc được bằng hợp đồng
def test_every_labelled_output_in_the_dataset_is_a_valid_answer():
    for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        SlmRead.model_validate(record["output"])


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmResult(type="mac_dinh", confidence=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.type, result.confidence) == ("mac_dinh", 0.9)
