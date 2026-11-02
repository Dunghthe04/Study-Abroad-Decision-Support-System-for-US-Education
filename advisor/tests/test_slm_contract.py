"""Hợp đồng SLM: khuôn đầu ra phải đúng, khớp với dữ liệu gán nhãn, và SLM giả dùng được."""

import json
from typing import get_args

import pytest
from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import (
    ENTITY_KEYS,
    INTENTS,
    PROFILE_KEYS,
    SLM_SCHEMA,
    SLM_TIN_SCHEMA,
    Intent,
    SlmRead,
    SlmResult,
    SlmTinResult,
    check_labels,
)
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


def branches():
    return {
        (b["properties"]["type"]["const"], b["properties"]["nhan_phu"]["const"]): b
        for b in SLM_SCHEMA["anyOf"]
    }


# Mỗi cặp (nhãn chính, nhãn phụ) hợp lệ là một nhánh của khuôn, không thừa không thiếu
def test_the_json_schema_has_one_branch_per_allowed_label_pair():
    pairs = set(branches())

    assert {intent for intent, _ in pairs} == set(INTENTS) == set(get_args(Intent))
    assert len(pairs) == 12  # 6 nhãn chính không phụ + ho_so 3 + kich_ban 2 + kien_thuc 1
    assert ("kien_thuc", "trung_tam") in pairs
    assert ("trung_tam", "trung_tam") not in pairs and ("trung_tam", "kien_thuc") not in pairs
    for intent, sub in pairs:
        check_labels(intent, sub)  # nhánh nào cũng đúng luật nhãn phụ


# Khuôn ép SLM và class đọc kết quả phải cùng tên khóa, nếu lệch thì mọi câu trả lời của model đều bị từ chối
def test_every_branch_has_the_same_keys_as_the_classes_that_read_it():
    for branch in SLM_SCHEMA["anyOf"]:
        assert set(branch["required"]) == set(branch["properties"]) == set(SlmRead.model_fields)
    keys = set().union(*(b["properties"]["thuc_the"]["properties"] for b in SLM_SCHEMA["anyOf"]))
    assert keys == set().union(*ENTITY_KEYS.values(), PROFILE_KEYS)
    assert branches()[("trung_tam", None)]["properties"]["thuc_the"]["properties"]["dich_vu"]["enum"] == list(
        SERVICES
    )


# Nhánh kich_ban chỉ có khóa của kich_ban và hồ sơ, nên model không sinh được dich_vu khi chọn kich_ban
def test_a_branch_only_allows_the_keys_of_its_labels():
    keys = set(branches()[("kich_ban", None)]["properties"]["thuc_the"]["properties"])

    assert keys == ENTITY_KEYS["kich_ban"] | set(PROFILE_KEYS)
    assert "dich_vu" in branches()[("kich_ban", "trung_tam")]["properties"]["thuc_the"]["properties"]


def test_profile_keys_are_the_same_as_the_ones_the_chat_saves():
    assert PROFILE_KEYS == info_to_field.PROFILE_KEYS


# Đầu ra trong dữ liệu chính là thứ SLM học để trả, nên dòng nào cũng phải đọc được bằng hợp đồng
def test_every_labelled_output_in_the_dataset_is_a_valid_answer():
    for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        SlmTinResult.from_raw(record["output"])


# Mỗi ý trong dữ liệu phải nằm gọn trong nhánh của loại nó ở khuôn SLM_TIN_SCHEMA, nếu không model bị khuôn chặn không học được
def test_every_labelled_intent_fits_the_branch_of_its_type():
    by_type = {
        b["properties"]["type"]["const"]: b["properties"]["thuc_the"]
        for b in SLM_TIN_SCHEMA["properties"]["cac_y"]["items"]["anyOf"]
    }
    for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines():
        for y in json.loads(line)["output"]["cac_y"]:
            thuc_the = by_type[y["type"]]
            assert set(y["thuc_the"]) <= set(thuc_the["properties"]), y
            assert set(thuc_the["required"]) <= set(y["thuc_the"]), y


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmResult(type="mac_dinh", confidence=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.type, result.confidence) == ("mac_dinh", 0.9)
