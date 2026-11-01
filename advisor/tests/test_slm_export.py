"""Xuất dữ liệu fine-tune: hội thoại giống hệt lúc chạy, đáp án đúng thứ tự khuôn, không lẫn tập test."""

import json

from app.services.slm_classifier import OllamaSlm
from scripts.build_slm_jsonl import DEFAULT_OUT
from scripts.export_slm_chat import _KEY_ORDER, answer_text, chat_rows, to_chat

RECORD = {
    "split": "train",
    "input": {
        "cau_hoi": "8.5 ạ, mà I-20 là gì",
        "ho_so": {"sat": "1400"},
        "cau_he_thong_dang_hoi": "Điểm GPA?",
    },
    "output": {"type": "kien_thuc", "nhan_phu": None, "thuc_the": {"gpa": "8.5 ạ"}},
}


class CaptureSlm:
    """SLM giả: ghi lại hội thoại OllamaSlm gửi đi rồi trả đúng đáp án."""

    def __init__(self, answer):
        self.answer = answer
        self.messages = None

    async def chat_json_scored(self, messages, schema):
        self.messages = messages
        return self.answer, json.dumps(self.answer), []


# Test quan trọng nhất: dữ liệu train phải trùng từng ký tự với thứ model nhận lúc chạy thật
async def test_the_training_conversation_is_exactly_what_the_model_gets_at_run_time():
    slm = CaptureSlm(RECORD["output"])
    classifier = OllamaSlm(slm)

    await classifier.read(
        RECORD["input"]["cau_hoi"], RECORD["input"]["ho_so"], RECORD["input"]["cau_he_thong_dang_hoi"]
    )
    chat = to_chat(RECORD, classifier.prompt)["messages"]

    assert chat[:2] == slm.messages
    assert chat[2] == {
        "role": "assistant",
        "content": '{"type":"kien_thuc","nhan_phu":null,"thuc_the":{"gpa":"8.5 ạ"}}',
    }


def test_the_answer_lists_entity_keys_in_the_order_the_schema_forces():
    output = {
        "type": "kich_ban",
        "nhan_phu": None,
        "thuc_the": {
            "thay_doi": [{"yeu_to": "sat", "gia_tri": "1400"}],
            "che_do": "what_if",
            "sat": "SAT 1300",
        },
    }

    keys = list(json.loads(answer_text(output))["thuc_the"])

    assert keys == ["sat", "che_do", "thay_doi"]  # khóa hồ sơ trước, rồi khóa riêng theo thứ tự của khuôn
    assert keys == [key for key in _KEY_ORDER[("kich_ban", None)] if key in keys]


def record(question, split):
    return {"split": split, "input": {"cau_hoi": question, "ho_so": None, "cau_he_thong_dang_hoi": ""},
            "output": {"type": "ngoai_pham_vi", "nhan_phu": None, "thuc_the": {"loai_con": "tam_biet"}}}  # fmt: skip


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
