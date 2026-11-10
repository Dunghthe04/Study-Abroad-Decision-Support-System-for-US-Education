"""Bản tiếng Anh của dữ liệu SLM (du_lieu_slm_en.jsonl): đúng tên Excel, không còn tên tiếng Việt, và đổi ngược được về bản nội bộ."""

import json
import re

import pytest

from scripts.build_slm_jsonl import (
    DEFAULT_EXCEL,
    DEFAULT_OUT,
    EN_SHEETS,
    EN_VALUES,
    check_cac_y,
    context_to_internal,
    intents_to_internal,
    read_records,
)

# Mọi khóa được phép trong dữ liệu train tiếng Anh (ngoài các khóa hồ sơ, là chữ học sinh gõ nên không đi qua đây)
RECORD_KEYS = {"id", "type", "split", "input", "output"}
INPUT_KEYS = {"question", "profile", "pending_question", "context"}
VIETNAMESE_NAMES = {
    "mac_dinh", "ho_so", "kich_ban", "kien_thuc", "trung_tam", "ngoai_pham_vi", "cac_y", "doan", "thuc_the", "cau_hoi",
    "cac_nhiem_vu", "nhiem_vu", "tiep_noi", "dang_noi_ve", "cau_he_thong_dang_hoi", "cac_truong", "truong", "muc", "bo_loc",
    "sap_xep", "loai_con", "che_do", "thay_doi", "yeu_to", "gia_tri", "don_vi", "muc_tieu", "hanh_dong", "dich_vu", "thanh_pho",
    "cac_trung_tam", "skill_md", "tinh_san",
}  # fmt: skip


@pytest.fixture(scope="module")
def english():
    return [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]


@pytest.fixture(scope="module")
def vietnamese():
    return read_records(DEFAULT_EXCEL)


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


def test_the_english_file_has_the_same_rows_in_the_same_order_as_the_internal_file(english, vietnamese):
    assert len(english) == len(vietnamese)
    assert [e["split"] for e in english] == [v["split"] for v in vietnamese]
    assert [e["input"]["question"] for e in english] == [v["input"]["cau_hoi"] for v in vietnamese]


def test_ids_are_unique_and_the_record_type_is_an_english_sheet_name(english):
    assert len({e["id"] for e in english}) == len(english)
    assert {e["type"] for e in english} <= set(EN_SHEETS)
    assert all(e["id"].startswith(e["type"]) for e in english)


def test_record_and_input_keys_are_exactly_the_english_ones(english):
    assert all(set(e) == RECORD_KEYS and set(e["output"]) == {"intents"} for e in english)
    assert all(
        set(e["input"]) <= INPUT_KEYS and {"question", "profile", "pending_question"} <= set(e["input"])
        for e in english
    )


def test_no_vietnamese_schema_name_is_left_in_any_key_of_the_intents_or_the_context(english):
    # Hồ sơ (profile) là khóa tiếng Anh sẵn có; câu hỏi và đoạn chữ là lời học sinh nên không kiểm
    found = set()
    for e in english:
        found |= keys_of(e["output"]) | keys_of(e["input"].get("context", {}))
    assert not found & VIETNAMESE_NAMES, sorted(found & VIETNAMESE_NAMES)
    assert all(re.fullmatch(r"[a-z_0-9]+", key) for key in found)


def test_every_intent_has_exactly_type_span_and_entities(english):
    for e in english:
        for intent in e["output"]["intents"]:
            assert set(intent) == {"type", "span", "entities"}, e["id"]
            assert intent["type"] in EN_SHEETS


def test_a_span_is_always_a_piece_of_the_question(english):
    for e in english:
        question = re.sub(r"\s+", " ", e["input"]["question"].strip().lower())
        for intent in e["output"]["intents"]:
            assert re.sub(r"\s+", " ", intent["span"].strip().lower()) in question, e["id"]


def test_tiers_are_english_and_the_vietnamese_tier_names_are_gone(english):
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

    for e in english:
        walk(e["output"])
        walk(e["input"].get("context", {}))
    assert tiers == set(EN_VALUES["muc_tieu"])


def test_schools_and_fields_are_always_lists_and_a_single_school_is_only_for_scenario(english):
    for e in english:
        for intent in e["output"]["intents"]:
            entities = intent["entities"]
            assert "field" not in entities, e["id"]
            assert isinstance(entities.get("schools", []), list) and isinstance(
                entities.get("fields", []), list
            )
            assert isinstance(entities.get("tasks", []), list)
            if "school" in entities:
                assert intent["type"] == "scenario", e["id"]


def test_converting_the_english_row_back_gives_exactly_the_internal_row(english, vietnamese):
    for e, v in zip(english, vietnamese, strict=True):
        context = context_to_internal(e["input"].get("context"))
        back = check_cac_y(
            v["type"],
            e["input"]["question"],
            e["input"]["pending_question"],
            intents_to_internal(e["output"]["intents"]) or None,
            e["id"],
            context,
        )
        assert back == v["output"]["cac_y"], e["id"]
        assert context == v["input"].get("dang_noi_ve"), e["id"]
        assert (e["input"]["profile"] or None) == v["input"]["ho_so"], e["id"]
