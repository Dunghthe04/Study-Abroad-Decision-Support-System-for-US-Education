"""Xuất dữ liệu fine-tune (tiếng Anh): hội thoại gồm prompt, đầu vào JSON, đáp án đúng thứ tự khóa, không lẫn tập test."""

import json
import re

from app.schemas.slm import (
    FILTER_ENUMS,
    FILTER_KEYS,
    INFO_FIELDS,
    INTENTS,
    PROFILE_KEYS,
    SCENARIO_MODES,
    SERVICE_CODES,
    SORTS,
    SUBTYPES,
    TARGET_GROUPS,
    TASKS_BY_INTENT,
    SlmTinResult,
)
from scripts.build_slm_jsonl import DEFAULT_OUT, check_intents
from scripts.export_slm_chat import (
    ENTITY_ORDER,
    PROMPT_PATH,
    answer_text,
    chat_rows,
    ordered_entities,
    to_chat,
    user_message,
)

RECORD = {
    "split": "train",
    "input": {
        "question": "8.5 ạ, mà I-20 là gì",
        "profile": {"sat": "1400"},
        "pending_question": "Điểm GPA?",
    },
    "output": {
        "intents": [
            {"type": "collect_profile", "span": "8.5 ạ", "entities": {"gpa": "8.5 ạ"}},
            {"type": "knowledge", "span": "I-20 là gì", "entities": {}},
        ]
    },
}


def real_records():
    return [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines() if line.strip()]


def test_the_user_message_is_compact_json_with_english_keys_and_the_profile_as_text():
    data = {**RECORD["input"], "profile": {"sat": "1400", "activities": ["CLB A", "CLB B"]}}

    user = json.loads(user_message(data))

    assert user == {
        "question": "8.5 ạ, mà I-20 là gì",
        "profile": {"sat": "1400", "activities": "CLB A; CLB B"},
        "pending_question": "Điểm GPA?",
    }
    assert " " not in user_message({"question": "a", "profile": None, "pending_question": ""})  # viết liền


def test_the_user_message_has_context_only_when_there_is_one_and_a_null_profile_when_none():
    plain = json.loads(user_message({"question": "a", "profile": None, "pending_question": ""}))
    with_context = json.loads(
        user_message(
            {"question": "a", "profile": None, "pending_question": "", "context": {"schools": ["Harvard"]}}
        )
    )

    assert plain["profile"] is None and "context" not in plain
    assert with_context["context"] == {"schools": ["Harvard"]}


def test_the_answer_is_the_list_of_intents_written_the_way_the_model_must_produce_it():
    answer = answer_text(RECORD["output"])

    assert json.loads(answer) == RECORD["output"]
    assert answer == answer.strip() and "\n" not in answer and ", " not in answer  # viết liền


def test_each_intent_is_written_with_type_first_then_span_then_entities():
    answer = json.loads(answer_text(RECORD["output"]))

    assert all(list(intent) == ["type", "span", "entities"] for intent in answer["intents"])


def test_entity_keys_are_written_in_one_fixed_order_whatever_the_order_in_the_data():
    messy = {"follow_up": True, "fields": ["cost"], "tasks": ["school_info"], "schools": ["Yale"], "gpa": "8"}

    assert list(ordered_entities(messy)) == ["gpa", "tasks", "schools", "fields", "follow_up"]
    assert [k for k in ENTITY_ORDER if k in PROFILE_KEYS] == list(PROFILE_KEYS)  # khóa hồ sơ đứng trước
    assert list(ordered_entities({"zzz": 1, "tasks": ["edit"]})) == ["tasks", "zzz"]  # khóa lạ viết sau cùng


def test_a_conversation_is_the_prompt_then_the_input_then_the_answer():
    chat = to_chat(RECORD, "PROMPT")["messages"]

    assert [m["role"] for m in chat] == ["system", "user", "assistant"]
    assert chat[0]["content"] == "PROMPT"
    assert json.loads(chat[1]["content"])["question"] == "8.5 ạ, mà I-20 là gì"
    assert json.loads(chat[2]["content"]) == RECORD["output"]


def test_only_the_requested_split_is_exported_and_questions_of_the_test_set_never_are():
    def row(question, split):
        return {
            "split": split,
            "input": {"question": question, "profile": None, "pending_question": ""},
            "output": RECORD["output"],
        }

    records = [
        row("câu train", "train"),
        row("Câu Test", "train"),
        row("câu test", "test"),
        row("câu val", "val"),
    ]

    assert len(chat_rows(records, "P", "train")) == 1  # câu train; câu trùng chữ với test bị bỏ
    assert len(chat_rows(records, "P", "val")) == 1
    assert chat_rows(records, "P", "test") == []


# Trên dữ liệu thật: train và val không chứa câu nào của tập test
def test_the_real_export_has_no_question_of_the_test_set():
    records = real_records()
    test_questions = {r["input"]["question"].lower() for r in records if r["split"] == "test"}

    for split in ("train", "val"):
        for chat in chat_rows(records, "P", split):
            assert json.loads(chat["messages"][1]["content"])["question"].lower() not in test_questions


def test_every_real_answer_is_read_back_by_the_contract():
    for record in real_records():
        intents = json.loads(answer_text(record["output"]))["intents"]
        checked = check_intents(
            record["type"],
            record["input"]["question"],
            record["input"]["pending_question"],
            intents,
            record["id"],
            record["input"].get("context"),
        )
        assert checked == intents
        assert SlmTinResult.from_raw({"intents": intents})


def prompt_text():
    return PROMPT_PATH.read_text(encoding="utf-8")


# Mọi mã và khóa của hợp đồng tiếng Anh: thêm một mã vào dữ liệu mà quên cập nhật prompt thì test này báo
def contract_words() -> set[str]:
    words = {
        "intents", "type", "span", "entities", "tasks", "schools", "school", "fields", "filters", "sort", "changes", "factor", "value", "unit",
        "action", "mode", "target_group", "service", "city", "centers", "subtype", "follow_up", "context", "profile", "question", "pending_question",
    }  # fmt: skip
    words |= set(PROFILE_KEYS) | set(INFO_FIELDS) | set(FILTER_KEYS) | set(SORTS)
    words |= {
        code for codes in TASKS_BY_INTENT.values() for code in codes
    }  # vắng tasks là việc mặc định, không có mã
    words |= set(INTENTS) | set(SUBTYPES) | set(SERVICE_CODES) | set(TARGET_GROUPS) | set(SCENARIO_MODES)
    words |= {code for codes in FILTER_ENUMS.values() for code in codes}
    words |= {"scale_4", "scale_10", "skip", "preferred", "mandatory"}
    return words


def test_the_prompt_names_every_key_and_code_of_the_english_contract():
    text = prompt_text()

    missing = sorted(w for w in contract_words() if w not in text)

    assert not missing, f"prompt thiếu: {missing}"


def test_every_example_in_the_prompt_is_a_valid_answer():
    lines = prompt_text().splitlines()
    checked = 0
    for i, line in enumerate(lines):
        if not line.startswith('{"intents":'):
            continue
        header = lines[i - 1]
        question = re.search(r'question "(.*)"$', header).group(1)
        context = (
            json.loads(re.search(r"context (\{.*?\}), question", header).group(1))
            if header.startswith("context")
            else None
        )
        intents = json.loads(line)["intents"]
        check_intents(intents[0]["type"], question, "", intents, f"prompt, ví dụ {checked + 1}", context)
        checked += 1
    assert checked >= 3


def test_the_prompt_says_that_spans_are_verbatim_and_that_fields_are_never_guessed():
    text = prompt_text()

    assert "nguyên văn" in text and "Không đoán mục" in text
