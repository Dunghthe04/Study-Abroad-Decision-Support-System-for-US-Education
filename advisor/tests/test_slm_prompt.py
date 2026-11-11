"""Prompt tách ý slm_intents: phải nêu đủ hợp đồng, ví dụ trong prompt phải hợp lệ, và chọn được prompt cho OllamaSlm."""

import json

import pytest

from app.core.config import Settings
from app.schemas.slm import (
    CENTER_TASKS,
    FILTER_ENUMS,
    FILTER_KEYS,
    INFO_FIELDS,
    INTENTS,
    KNOWLEDGE_TASKS,
    PROFILE_KEYS,
    PROFILE_TASKS,
    REVIEW_TASKS,
    SCENARIO_MODES,
    SERVICE_CODES,
    SORTS,
    SUBTYPES,
    TARGET_GROUPS,
    SlmTinResult,
)
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import PROMPT_PATH, OllamaSlm, prompt_path
from scripts.export_slm_chat import PROMPT_PATH as TRAIN_PROMPT_PATH
from scripts.export_slm_chat import TRAIN_PROMPT, to_chat

NAME = "slm_intents"


def text_of(name: str) -> str:
    return (SKILLS_DIR / f"{name}.md").read_text(encoding="utf-8")


# Mọi mã và khóa của hợp đồng: thêm một mã vào slm.py mà quên cập nhật prompt thì test này báo
def contract_words() -> set[str]:
    words = {
        "intents", "type", "span", "entities", "question", "profile", "pending_question", "context",
        "tasks", "schools", "school", "fields", "filters", "sort", "follow_up", "centers", "changes",
        "factor", "value", "unit", "action", "skip", "mode", "target_group", "subtype", "service", "city",
        "scale_4", "scale_10",
    }  # fmt: skip
    words |= set(INTENTS) | set(PROFILE_KEYS)
    words |= set(KNOWLEDGE_TASKS) | set(REVIEW_TASKS) | set(CENTER_TASKS) | set(PROFILE_TASKS)
    words |= set(INFO_FIELDS) | set(SORTS) | set(FILTER_KEYS) | set(SERVICE_CODES) | set(SUBTYPES)
    words |= set(SCENARIO_MODES) | set(TARGET_GROUPS)
    words |= {value for values in FILTER_ENUMS.values() for value in values}
    return words


def test_the_prompt_names_every_key_and_code_of_the_contract():
    text = text_of(NAME)

    missing = sorted(w for w in contract_words() if w not in text)

    assert not missing, f"{NAME} thiếu: {missing}"


def test_every_example_in_the_prompt_is_a_valid_answer():
    examples = [line for line in text_of(NAME).splitlines() if line.startswith('{"intents":[{')]

    assert examples
    for line in examples:
        SlmTinResult.from_raw(json.loads(line))


def test_the_prompt_does_not_use_the_old_vietnamese_contract():
    text = text_of(NAME)

    assert not [
        w
        for w in ("cac_y", "thuc_the", "doan", "nhiem_vu", "mac_dinh", "kien_thuc", "ngoai_pham_vi")
        if w in text
    ]


def test_the_default_prompt_is_the_one_training_uses():
    assert Settings(_env_file=None).slm_prompt == NAME
    assert PROMPT_PATH.stem == NAME
    assert TRAIN_PROMPT == NAME and TRAIN_PROMPT_PATH.stem == NAME


class CaptureSlm:
    def __init__(self):
        self.messages = None

    async def chat_json_scored(self, messages, schema):
        self.messages = messages
        raise ValueError("không cần kết quả")  # chỉ cần xem hội thoại gửi đi


async def test_the_slm_sends_the_prompt_that_was_chosen():
    slm = CaptureSlm()
    reader = OllamaSlm(slm, prompt=NAME)

    await reader.read("em lớp 12", None, "")

    assert slm.messages[0]["content"].startswith(f"# PHÂN LOẠI TIN NHẮN ({NAME})")
    assert reader.prompt_version == "3.0"


def test_an_unknown_prompt_name_is_rejected_at_start_up():
    with pytest.raises(ValueError, match="không có prompt"):
        prompt_path("khong_co_file_nay")
    with pytest.raises(ValueError, match="không có prompt"):
        OllamaSlm(CaptureSlm(), prompt="khong_co_file_nay")


def test_a_training_row_with_context_carries_it_to_the_user_message():
    record = {
        "split": "train",
        "input": {
            "question": "còn Yale thì sao",
            "profile": None,
            "pending_question": "",
            "context": {"schools": ["Harvard"], "fields": ["cost"]},
        },
        "output": {
            "intents": [
                {
                    "type": "knowledge",
                    "span": "còn Yale thì sao",
                    "entities": {
                        "tasks": ["school_info"],
                        "schools": ["Yale"],
                        "fields": ["cost"],
                        "follow_up": True,
                    },
                }
            ]
        },
    }

    chat = to_chat(record, load_skill(TRAIN_PROMPT_PATH)[0])

    assert json.loads(chat["messages"][1]["content"])["context"] == {
        "schools": ["Harvard"],
        "fields": ["cost"],
    }
    assert chat["messages"][0]["content"].startswith("# PHÂN LOẠI TIN NHẮN (slm_intents)")
