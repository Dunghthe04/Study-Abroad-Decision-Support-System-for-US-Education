"""Dữ liệu SLM (du_lieu_slm.jsonl): đúng tên khóa và mã của hợp đồng, không còn tên tiếng Việt, mọi dòng qua được bộ kiểm của hệ thống."""

import json
import re

import pytest

from app.schemas.slm import INTENTS, TARGET_GROUPS, SlmTinResult
from scripts.build_slm_jsonl import DEFAULT_OUT

# Mọi khóa được phép trong dữ liệu train (ngoài các khóa hồ sơ, là chữ học sinh gõ nên không đi qua đây)
RECORD_KEYS = {"id", "type", "split", "input", "output"}
INPUT_KEYS = {"question", "profile", "pending_question", "context"}
VIETNAMESE_NAMES = {
    "mac_dinh", "ho_so", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi", "cac_y", "doan", "thuc_the", "cau_hoi",
    "cac_nhiem_vu", "nhiem_vu", "tiep_noi", "dang_noi_ve", "cau_he_thong_dang_hoi", "cac_truong", "truong", "muc", "bo_loc",
    "sap_xep", "loai_con", "che_do", "thay_doi", "yeu_to", "gia_tri", "don_vi", "muc_tieu", "hanh_dong", "dich_vu", "thanh_pho",
    "cac_trung_tam", "skill_md", "tinh_san",
}  # fmt: skip


@pytest.fixture(scope="module")
def records():
    return [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]


def keys_of(value, skip=()):
    found = set()
    if isinstance(value, dict):
        for key, item in value.items():
            found.add(key)
            if key not in skip:
                found |= keys_of(item, skip)
    elif isinstance(value, list):
        for item in value:
            found |= keys_of(item, skip)
    return found


def test_ids_are_unique_and_the_record_type_is_a_sheet_name(records):
    assert len({r["id"] for r in records}) == len(records)
    assert {r["type"] for r in records} <= set(INTENTS)
    assert all(r["id"].startswith(r["type"]) for r in records)


def test_record_and_input_keys_are_exactly_the_contract_ones(records):
    assert all(set(r) == RECORD_KEYS and set(r["output"]) == {"intents"} for r in records)
    assert all(
        set(r["input"]) <= INPUT_KEYS and {"question", "profile", "pending_question"} <= set(r["input"])
        for r in records
    )


def test_no_vietnamese_schema_name_is_left_in_any_key_of_the_intents_or_the_context(records):
    # Hồ sơ (profile) là khóa tiếng Anh sẵn có; câu hỏi và đoạn chữ là lời học sinh nên không kiểm
    found = set()
    for r in records:
        found |= keys_of(r["output"]) | keys_of(r["input"].get("context", {}))
    assert not found & VIETNAMESE_NAMES, sorted(found & VIETNAMESE_NAMES)
    assert all(re.fullmatch(r"[a-z_0-9]+", key) for key in found)


def test_every_intent_has_exactly_type_span_and_entities(records):
    for r in records:
        for intent in r["output"]["intents"]:
            assert set(intent) == {"type", "span", "entities"}, r["id"]
            assert intent["type"] in INTENTS


def test_a_span_is_always_a_piece_of_the_question(records):
    for r in records:
        question = re.sub(r"\s+", " ", r["input"]["question"].strip().lower())
        for intent in r["output"]["intents"]:
            assert re.sub(r"\s+", " ", intent["span"].strip().lower()) in question, r["id"]


def test_tiers_are_the_three_contract_groups_in_output_and_context(records):
    tiers = set()

    def walk(value):
        if isinstance(value, dict):
            for key, item in value.items():
                if key == "target_group":
                    tiers.add(item)
                else:
                    walk(item)
        elif isinstance(value, list):
            for item in value:
                walk(item)

    for r in records:
        walk(r["output"])
        walk(r["input"].get("context", {}))
    assert tiers == set(TARGET_GROUPS)


def test_schools_and_fields_are_always_lists_and_a_single_school_is_only_for_scenario(records):
    for r in records:
        for intent in r["output"]["intents"]:
            entities = intent["entities"]
            assert "field" not in entities, r["id"]
            assert isinstance(entities.get("schools", []), list) and isinstance(
                entities.get("fields", []), list
            )
            assert isinstance(entities.get("tasks", []), list)
            if "school" in entities:
                assert intent["type"] == "scenario", r["id"]


def test_every_answer_passes_the_same_check_the_system_applies_to_what_the_model_returns(records):
    for r in records:
        result = SlmTinResult.from_raw(r["output"])

        assert [y.type for y in result.intents] == [y["type"] for y in r["output"]["intents"]], r["id"]
