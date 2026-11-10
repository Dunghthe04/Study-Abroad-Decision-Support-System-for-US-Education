"""Prompt tách ý: hai bản (đầy đủ và ngắn) phải nêu đủ hợp đồng, ví dụ trong prompt phải hợp lệ, và chọn được prompt cho OllamaSlm."""

import json

import pytest

from app.core.config import Settings
from app.schemas.slm import (
    CENTER_TASKS,
    FILTER_KEYS,
    INFO_FIELDS,
    KNOWLEDGE_TASKS,
    PROFILE_TASKS,
    REVIEW_TASKS,
    SCENARIO_MODES,
    SORTS,
    SUBTYPES,
    SlmTinResult,
)
from app.services.centers import SERVICES
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import PROMPT_PATH, OllamaSlm, prompt_path
from scripts.export_slm_chat import PROMPT_PATH as TRAIN_PROMPT_PATH
from scripts.export_slm_chat import TRAIN_PROMPT, to_chat

FULL = "slm_phan_loai_cac_y"
SHORT = "slm_phan_loai_cac_y_ngan"


def text_of(name: str) -> str:
    return (SKILLS_DIR / f"{name}.md").read_text(encoding="utf-8")


# Mọi mã và khóa của hợp đồng: thêm một mã vào slm.py mà quên cập nhật prompt thì test này báo
def contract_words() -> set[str]:
    words = {
        "nhiem_vu", "cac_nhiem_vu", "cac_truong", "cac_trung_tam", "tiep_noi", "dang_noi_ve", "muc", "bo_loc", "sap_xep",
        "muc_tieu", "thay_doi", "yeu_to", "gia_tri", "don_vi", "che_do", "loai_con", "dich_vu", "thanh_pho",
    }  # fmt: skip
    words |= set(KNOWLEDGE_TASKS) | set(REVIEW_TASKS) | set(CENTER_TASKS) | set(INFO_FIELDS) | set(SORTS)
    words |= set(PROFILE_TASKS) - {"tra_loi"}  # tra_loi là mặc định (vắng nhiem_vu), prompt không cần nêu
    words |= set(FILTER_KEYS) | set(SERVICES) | set(SUBTYPES) | set(SCENARIO_MODES)
    return words


@pytest.mark.parametrize("name", [FULL, SHORT])
def test_each_prompt_names_every_key_and_code_of_the_contract(name):
    text = text_of(name)

    missing = sorted(w for w in contract_words() if w not in text)

    assert not missing, f"{name} thiếu: {missing}"


@pytest.mark.parametrize("name", [FULL, SHORT])
def test_every_example_in_a_prompt_is_a_valid_answer(name):
    examples = [line for line in text_of(name).splitlines() if line.startswith('{"cac_y":[{')]

    assert examples
    for line in examples:
        SlmTinResult.from_raw(json.loads(line))


def test_the_short_prompt_stays_short_so_training_and_serving_stay_cheap():
    short, full = text_of(SHORT), text_of(FULL)

    assert len(short) <= 5000  # khoảng 1.5k token; bản đầy đủ khoảng 4.6k token
    assert len(short) < len(full) / 2


def test_both_prompts_are_the_same_version_so_a_change_to_one_is_not_forgotten():
    assert load_skill(SKILLS_DIR / f"{FULL}.md")[1] == load_skill(SKILLS_DIR / f"{SHORT}.md")[1]


def test_the_default_prompt_is_the_full_one_and_training_uses_the_short_one():
    assert Settings(_env_file=None).slm_prompt == FULL
    assert PROMPT_PATH.stem == FULL
    assert TRAIN_PROMPT == SHORT and TRAIN_PROMPT_PATH.stem == SHORT


class CaptureSlm:
    def __init__(self):
        self.messages = None

    async def chat_json_scored(self, messages, schema):
        self.messages = messages
        raise ValueError("không cần kết quả")  # chỉ cần xem hội thoại gửi đi


async def test_the_slm_sends_the_prompt_that_was_chosen():
    for name in (FULL, SHORT):
        slm = CaptureSlm()
        reader = OllamaSlm(slm, prompt=name)

        await reader.read("em lớp 12", None, "")

        assert slm.messages[0]["content"].startswith(f"# PHÂN LOẠI TIN NHẮN ({name})")
        assert reader.prompt_version == "2.3"


def test_an_unknown_prompt_name_is_rejected_at_start_up():
    with pytest.raises(ValueError, match="không có prompt"):
        prompt_path("khong_co_file_nay")
    with pytest.raises(ValueError, match="không có prompt"):
        OllamaSlm(CaptureSlm(), prompt="khong_co_file_nay")


def test_a_training_row_with_context_carries_it_to_the_user_message():
    record = {
        "split": "train",
        "input": {
            "cau_hoi": "còn Yale thì sao",
            "ho_so": None,
            "cau_he_thong_dang_hoi": "",
            "dang_noi_ve": {"truong": ["Harvard"], "muc": ["chi_phi"]},
        },
        "output": {
            "cac_y": [
                {
                    "type": "kien_thuc",
                    "doan": "còn Yale thì sao",
                    "thuc_the": {"truong": "Yale", "tiep_noi": True},
                }
            ]
        },
    }

    chat = to_chat(record, load_skill(prompt_path(SHORT))[0])

    assert json.loads(chat["messages"][1]["content"])["dang_noi_ve"] == {
        "truong": ["Harvard"],
        "muc": ["chi_phi"],
    }
    assert chat["messages"][0]["content"].startswith(f"# PHÂN LOẠI TIN NHẮN ({SHORT})")


def test_the_frozen_prompt_for_model_v3_keeps_its_old_version_and_contract():
    # usas-slm-v3 được train với prompt 2.2 (13 mục, chưa có nhà ở, khuôn viên...). Prompt mới 2.3 chỉ dùng cho model train lại
    frozen = text_of("slm_phan_loai_cac_y_ngan_v22")

    assert "skill_version 2.2" in frozen.splitlines()[0]
    assert "nha_o" not in frozen and "khuon_vien" not in frozen
    assert "nha_o" in text_of(SHORT) and "skill_version 2.3" in text_of(SHORT).splitlines()[0]
