"""Xuất dữ liệu fine-tune: hội thoại giống hệt lúc chạy, đáp án đúng thứ tự khuôn, không lẫn tập test."""

import json

from app.schemas.slm import SlmTinResult
from app.services.slm_classifier import OllamaSlm
from scripts.build_slm_jsonl import DEFAULT_OUT
from scripts.export_slm_chat import _KEY_ORDER, PROMPT_PATH, answer_text, chat_rows, to_chat

RECORD = {
    "split": "train",
    "input": {
        "cau_hoi": "8.5 ạ, mà I-20 là gì",
        "ho_so": {"sat": "1400"},
        "cau_he_thong_dang_hoi": "Điểm GPA?",
    },
    "output": {
        "cac_y": [
            {"type": "mac_dinh", "doan": "8.5 ạ", "thuc_the": {"gpa": "8.5 ạ"}},
            {"type": "kien_thuc", "doan": "I-20 là gì", "thuc_the": {}},
        ]
    },
}


class CaptureSlm:
    """SLM giả: ghi lại hội thoại OllamaSlm gửi đi rồi trả một đáp án hợp lệ."""

    def __init__(self):
        self.messages = None

    async def chat_json_scored(self, messages, schema):
        self.messages = messages
        answer = {"type": "ngoai_pham_vi", "nhan_phu": None, "thuc_the": {"loai_con": "tam_biet"}}
        return answer, json.dumps(answer), []


# Test quan trọng nhất: hai tin đầu của dữ liệu train phải trùng từng ký tự với thứ model nhận lúc chạy thật
async def test_the_training_conversation_is_exactly_what_the_model_gets_at_run_time():
    slm = CaptureSlm()
    classifier = OllamaSlm(slm)

    await classifier.read(
        RECORD["input"]["cau_hoi"], RECORD["input"]["ho_so"], RECORD["input"]["cau_he_thong_dang_hoi"]
    )
    chat = to_chat(RECORD, classifier.prompt)["messages"]

    assert chat[:2] == slm.messages
    assert chat[2]["role"] == "assistant"


def test_the_answer_is_the_list_of_intents_written_the_way_the_model_must_produce_it():
    assert answer_text(RECORD["output"]) == (
        '{"cac_y":[{"type":"mac_dinh","doan":"8.5 ạ","thuc_the":{"gpa":"8.5 ạ"}},'
        '{"type":"kien_thuc","doan":"I-20 là gì","thuc_the":{}}]}'
    )


def test_the_answer_lists_entity_keys_in_the_order_the_schema_forces():
    output = {
        "cac_y": [
            {
                "type": "kich_ban",
                "doan": "nếu em lên SAT 1400 thì sao",
                "thuc_the": {"thay_doi": [{"yeu_to": "sat", "gia_tri": "1400"}], "che_do": "what_if"},
            }
        ]
    }

    keys = list(json.loads(answer_text(output))["cac_y"][0]["thuc_the"])

    assert keys == ["che_do", "thay_doi"]  # theo thứ tự của khuôn, không theo thứ tự trong dữ liệu
    assert keys == [key for key in _KEY_ORDER["kich_ban"] if key in keys]


def test_each_intent_is_written_with_type_first_then_doan_then_thuc_the():
    y = json.loads(answer_text(RECORD["output"]))["cac_y"][1]

    assert list(y) == ["type", "doan", "thuc_the"]


def test_the_answer_of_a_record_is_read_back_by_the_contract():
    result = SlmTinResult.from_raw(json.loads(answer_text(RECORD["output"])))

    assert [y.type for y in result.cac_y] == ["mac_dinh", "kien_thuc"]


def test_the_prompt_describes_the_list_of_intents_and_the_answer_the_example_shows():
    prompt = PROMPT_PATH.read_text(encoding="utf-8")

    assert "skill_version 2." in prompt and '"cac_y"' in prompt and "nhan_phu" not in prompt


def record(question, split):
    return {"split": split, "input": {"cau_hoi": question, "ho_so": None, "cau_he_thong_dang_hoi": ""},
            "output": {"cac_y": [{"type": "ngoai_pham_vi", "doan": question, "thuc_the": {"loai_con": "tam_biet"}}]}}  # fmt: skip


def test_only_the_requested_split_is_exported_and_questions_of_the_test_set_never_are():
    records = [record("cảm ơn bạn", "test"), record("Cảm ơn bạn", "train"), record("bye nhé", "train")]

    rows = chat_rows(records, "prompt", "train")

    assert [json.loads(row["messages"][1]["content"])["cau_hoi"] for row in rows] == ["bye nhé"]
    assert chat_rows(records, "prompt", "val") == []


# Trên dữ liệu thật: train và val không chứa câu nào của tập test
def test_the_real_export_has_no_question_of_the_test_set():
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    test_questions = {r["input"]["cau_hoi"].lower() for r in records if r["split"] == "test"}

    for split in ("train", "val"):
        rows = chat_rows(records, "prompt", split)
        users = {json.loads(row["messages"][1]["content"])["cau_hoi"].lower() for row in rows}
        assert rows and not users & test_questions


# Mọi đáp án trong dữ liệu thật phải đọc lại được bằng hợp đồng, nếu không model được dạy sinh thứ hệ thống từ chối
def test_every_real_answer_is_read_back_by_the_contract():
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]

    for r in records:
        SlmTinResult.from_raw(json.loads(answer_text(r["output"])))
