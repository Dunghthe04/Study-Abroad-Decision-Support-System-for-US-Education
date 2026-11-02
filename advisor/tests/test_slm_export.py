"""Xuất dữ liệu fine-tune: hội thoại giống hệt lúc chạy, đáp án đúng thứ tự khuôn, không lẫn tập test."""

import json
import re

from app.schemas.slm import SkillRequest, SlmTinResult, check_skill
from app.services.slm_classifier import OllamaSlm
from scripts.build_slm_jsonl import DEFAULT_OUT
from scripts.export_slm_chat import (
    _KEY_ORDER,
    PROMPT_PATH,
    SKILL_HOLDOUT_EVERY,
    SKILL_PROMPT_PATH,
    SKILL_REPEAT,
    answer_text,
    chat_rows,
    skill_messages,
    skill_request,
    skill_rows,
    to_chat,
)

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


# ---- dữ liệu sinh skill ----
PROFILE = {
    "gpa": "9 thang 10",
    "sat": "1250",
    "activities": ["trưởng CLB Toán cấp tỉnh", "thành viên CLB Văn"],
}


def skill_record(number, split="train", kind="ho_so", skill="## 0. Câu hỏi kích hoạt\nx"):
    return {
        "id": f"ho_so_{number:03d}", "type": kind, "split": split,
        "input": {"cau_hoi": "em nên nộp trường nào", "ho_so": PROFILE, "cau_he_thong_dang_hoi": ""},
        "output": {"cac_y": []}, "skill_md": skill,
    }  # fmt: skip


def test_the_skill_request_is_what_skill_request_in_the_contract_accepts():
    request = skill_request(skill_record(1))

    SkillRequest.model_validate(request)
    assert (
        request["ho_so"]["activities"] == "trưởng CLB Toán cấp tỉnh; thành viên CLB Văn"
    )  # danh sách thành một chuỗi


def test_the_skill_conversation_is_the_skill_prompt_then_the_request_then_the_skill_as_the_answer():
    row = skill_rows([skill_record(1)], "PROMPT", "val")[0]["messages"]

    assert row[0] == {"role": "system", "content": "PROMPT"}
    assert json.loads(row[1]["content"]) == skill_request(skill_record(1))
    assert row[2] == {"role": "assistant", "content": "## 0. Câu hỏi kích hoạt\nx"}
    assert row[:2] == skill_messages("PROMPT", skill_request(skill_record(1)))


def test_skill_rows_come_only_from_profile_rows_that_have_a_skill():
    records = [skill_record(1), skill_record(2, kind="kich_ban"), skill_record(3, skill=None)]

    assert (
        len(skill_rows(records, "P", "val")) == 1
    )  # chỉ dòng 1 (hold-out); kịch bản chưa có prompt, dòng không skill bỏ
    assert len(skill_rows(records, "P", "train")) == 0


def test_train_skills_are_repeated_and_every_few_are_kept_for_the_skill_check():
    records = [skill_record(n) for n in range(1, 10)] + [skill_record(10, split="val")]

    train = skill_rows(records, "P", "train")
    val = skill_rows(records, "P", "val")

    kept = len(records[:9][::SKILL_HOLDOUT_EVERY])
    assert len(train) == (9 - kept) * SKILL_REPEAT
    assert len(val) == 1 + kept  # dòng val cộng các dòng train giữ lại
    asked = lambda rows: {json.loads(r["messages"][1]["content"])["cau_hoi"] for r in rows}  # noqa: E731
    assert asked(train) == asked(
        val
    )  # cùng loại câu hỏi nhưng khác hồ sơ ở dữ liệu thật; ở đây chỉ kiểm không lẫn tập


def test_a_skill_of_the_test_split_is_never_exported():
    records = [skill_record(n, split="test") for n in range(1, 4)]

    assert skill_rows(records, "P", "train") == [] and skill_rows(records, "P", "val") == []


def test_the_skill_prompt_names_the_sections_of_the_skill():
    prompt = SKILL_PROMPT_PATH.read_text(encoding="utf-8")

    assert "slm_sinh_skill_ho_so" in prompt and "## 7. Yêu cầu câu trả lời" in prompt and "# Mốc:" in prompt


def real_skill_rows():
    records = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]
    prompt = SKILL_PROMPT_PATH.read_text(encoding="utf-8")
    return [row for split in ("train", "val") for row in skill_rows(records, prompt, split)]


def test_every_real_exported_skill_passes_the_skill_check_with_the_request_it_answers():
    rows = real_skill_rows()

    assert rows
    for row in rows:
        request = SkillRequest.model_validate(json.loads(row["messages"][1]["content"]))
        check_skill(row["messages"][2]["content"], request)


# Quy tắc đặt Mốc trong prompt phải đúng với dữ liệu, nếu không model học một đằng được dặn một nẻo
def levels(profile):
    gpa = float(re.search(r"[\d.]+", profile["gpa"]).group())
    gpa4 = gpa / 10 * 4 if gpa > 4.0 else gpa
    sat = re.search(r"\d{3,4}", profile.get("sat", ""))
    ielts = re.search(r"\d(?:\.\d)?", profile.get("english", ""))
    budget = int(re.search(r"\d+(?:\.\d{3})+", profile["budget"]).group().replace(".", ""))
    return {
        "GPA": "cao" if gpa4 >= 3.5 else "trung bình" if gpa4 >= 3.0 else "thấp",
        "SAT": "chưa thi" if not sat else "cao" if int(sat.group()) >= 1400 else "trung bình" if int(sat.group()) >= 1200 else "thấp",
        "tiếng Anh": "chưa thi" if not ielts else "cao" if float(ielts.group()) >= 7 else "trung bình" if float(ielts.group()) >= 6 else "thấp",
        "ngân sách": "cao" if budget >= 60000 else "trung bình" if budget >= 35000 else "thấp",
    }  # fmt: skip


def test_the_moc_line_of_every_real_skill_follows_the_rules_written_in_the_prompt():
    for row in real_skill_rows():
        request = json.loads(row["messages"][1]["content"])
        moc = re.search(r"# Mốc: (.*)", row["messages"][2]["content"]).group(1)
        for name, level in levels(request["ho_so"]).items():
            assert f"{name} {level}" in moc, (request["cau_hoi"], name, level, moc)
