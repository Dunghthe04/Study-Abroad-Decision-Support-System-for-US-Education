"""Từ điển dữ liệu SLM (slm/data/ENTITIES_REFERENCE.md): sinh từ schema nên không được lệch với mã. Không cần Ollama."""

import json

import pytest

from app.schemas import slm as schema
from slm.prepare import build_entities_reference as ref


def record(kind: str, question: str = "câu", **entities) -> dict:
    return {
        "input": {"question": question},
        "output": {"intents": [{"type": kind, "span": question, "entities": entities}]},
    }


def test_every_key_and_value_of_the_schema_has_a_description():
    assert ref.missing_descriptions() == []


def test_a_new_schema_key_without_a_description_stops_the_build(monkeypatch):
    monkeypatch.setitem(schema.ENTITY_KEYS, "knowledge", schema.ENTITY_KEYS["knowledge"] | {"khoa_moi"})

    assert "khóa knowledge.khoa_moi" in ref.missing_descriptions()
    with pytest.raises(ValueError, match="khoa_moi"):
        ref.build([])


def test_the_reference_lists_every_type_key_and_allowed_value():
    text = ref.build([record("collect_profile", "em GPA 3.5", gpa="em GPA 3.5")])

    for kind in schema.INTENTS:
        assert f"`{kind}`" in text
    for keys in schema.ENTITY_KEYS.values():
        for key in keys:
            assert f"`{key}`" in text
    for values in (
        schema.SORTS,
        schema.SUBTYPES,
        schema.SERVICE_CODES,
        schema.INFO_FIELDS,
        schema.PROFILE_KEYS,
        schema.FILTER_KEYS,
    ):
        for value in values:
            assert f"`{value}`" in text
    assert "`about_system`" in text and "`human_handoff`" in text  # hai subtype hướng dẫn gán nhãn từng thiếu


def test_counts_and_examples_come_from_the_data():
    records = [
        record("collect_profile", "em GPA 3.5", gpa="em GPA 3.5"),
        record("collect_profile", "GPA 4.0", gpa="GPA 4.0"),
        record("knowledge", "trường nào rẻ nhất", tasks=["search"], sort="lowest_cost"),
    ]

    data = ref.stats(records)

    assert data["by_type"]["collect_profile"] == 2 and data["by_type"]["knowledge"] == 1
    assert data["key_count"][("collect_profile", "gpa")] == 2
    assert "GPA 4.0" in ref.example_text(data, "collect_profile", "gpa")  # chọn câu ngắn nhất
    assert ref.example_text(data, "scenario", "mode") == "-"  # chưa có ví dụ thì ghi dấu gạch


def test_the_committed_reference_is_up_to_date():
    records = [
        json.loads(line) for line in ref.DEFAULT_OUT.read_text(encoding="utf-8").splitlines() if line.strip()
    ]

    expected = ref.build(records)

    assert ref.OUT.read_text(encoding="utf-8") == expected, (
        "chạy lại: python -m slm.prepare.build_entities_reference"
    )
