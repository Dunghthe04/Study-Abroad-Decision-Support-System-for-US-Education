"""Xuất dữ liệu fine-tune (tiếng Anh): hội thoại gồm prompt, đầu vào JSON, đáp án đúng thứ tự khóa, không lẫn tập test."""

import json
import re

from app.schemas.slm import PROFILE_KEYS, SKILL_SECTIONS, SkillRequest, check_skill
from scripts.build_slm_jsonl import (
    DEFAULT_EXCEL,
    DEFAULT_OUT,
    EN_CENTER_TASKS,
    EN_FIELDS,
    EN_FILTER_KEYS,
    EN_KNOWLEDGE_TASKS,
    EN_PROFILE_TASKS,
    EN_REVIEW_TASKS,
    EN_SHEETS,
    EN_SORTS,
    EN_VALUES,
    check_cac_y,
    context_to_internal,
    intents_to_internal,
    read_records,
)
from scripts.export_slm_chat import (
    ENTITY_ORDER,
    PROMPT_PATH,
    SKILL_HOLDOUT_EVERY,
    SKILL_PROMPT_PATH,
    SKILL_REPEAT,
    answer_text,
    chat_rows,
    ordered_entities,
    skill_messages,
    skill_request,
    skill_rows,
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
        internal = check_cac_y(
            EN_SHEETS[record["type"]],
            record["input"]["question"],
            record["input"]["pending_question"],
            intents_to_internal(intents) or None,
            record["id"],
            context_to_internal(record["input"].get("context")),
        )
        assert internal


def prompt_text():
    return PROMPT_PATH.read_text(encoding="utf-8")


# Mọi mã và khóa của hợp đồng tiếng Anh: thêm một mã vào dữ liệu mà quên cập nhật prompt thì test này báo
def contract_words() -> set[str]:
    words = {
        "intents", "type", "span", "entities", "tasks", "schools", "school", "fields", "filters", "sort", "changes", "factor", "value", "unit",
        "action", "mode", "target_group", "service", "city", "centers", "subtype", "follow_up", "context", "profile", "question", "pending_question",
    }  # fmt: skip
    words |= set(PROFILE_KEYS) | set(EN_FIELDS) | set(EN_FILTER_KEYS) | set(EN_SORTS)
    words |= set(EN_PROFILE_TASKS) - {"answer"}
    words |= (
        set(EN_REVIEW_TASKS) | set(EN_KNOWLEDGE_TASKS) | (set(EN_CENTER_TASKS) - {"find"})
    )  # find là mặc định (vắng tasks), prompt không cần nêu mã
    words |= set(EN_SHEETS)
    for key in ("dich_vu", "loai_con", "don_vi", "muc_tieu", "hanh_dong"):
        words |= set(EN_VALUES[key])
    words |= {"what_if", "goal_seeking", "optional", "preferred", "mandatory"}
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
        check_cac_y(
            EN_SHEETS[intents[0]["type"]],
            question,
            "",
            intents_to_internal(intents),
            f"prompt, ví dụ {checked + 1}",
            context_to_internal(context),
        )
        checked += 1
    assert checked >= 3


def test_the_prompt_says_that_spans_are_verbatim_and_that_fields_are_never_guessed():
    text = prompt_text()

    assert "nguyên văn" in text and "Không đoán mục" in text


# ---- skill (công cụ kiểm skill cũ, giữ lại; skill sẽ do code điền khuôn) ----


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


def test_the_skill_request_carries_the_calculated_lines_or_an_empty_object():
    calc = {"gpa_level": "3.2 thang 4"}

    assert skill_request(skill_record(1))["tinh_san"] == {}
    assert skill_request(skill_record(1) | {"tinh_san": calc})["tinh_san"] == calc
    SkillRequest.model_validate(skill_request(skill_record(1) | {"tinh_san": calc}))


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

    assert "slm_sinh_skill_ho_so" in prompt and "## 6. Yêu cầu câu trả lời" in prompt and "# Mốc:" in prompt
    # Prompt phải liệt kê đúng các mục của khuôn chuẩn (7 mục), không còn mục "Đề xuất trường" của khuôn cũ
    assert all(f'"## {name}"' in prompt for name in SKILL_SECTIONS["ho_so"])
    assert "Đề xuất trường" not in prompt


# Mức do code so với mốc trường (tinh_san); prompt còn ngưỡng cứng thì model được dặn ngược với dữ liệu skill
def test_the_skill_prompt_takes_levels_from_tinh_san_and_sets_no_threshold_of_its_own():
    prompt = SKILL_PROMPT_PATH.read_text(encoding="utf-8")

    assert "tinh_san" in prompt
    assert not re.search(r"\b(?:cao|trung bình|thấp) (?:là )?(?:từ|dưới) \d", prompt)


def real_skill_rows():
    records = read_records(DEFAULT_EXCEL)
    prompt = SKILL_PROMPT_PATH.read_text(encoding="utf-8")
    return [row for split in ("train", "val") for row in skill_rows(records, prompt, split)]


def test_every_real_exported_skill_passes_the_skill_check_with_the_request_it_answers():
    rows = real_skill_rows()

    assert (
        not rows
    )  # v9: dữ liệu thật không còn skill (code điền khuôn); khi có skill lại thì kiểm từng dòng ở dưới
    for row in rows:
        request = SkillRequest.model_validate(json.loads(row["messages"][1]["content"]))
        check_skill(row["messages"][2]["content"], request)


# Mức chữ trong một dòng tính sẵn: "3.2 thang 4, thấp (TB các trường 3.73)" → "thấp", "1200, mức trung bình" →
# "trung bình". Chưa có mốc trường thì dòng chỉ có điểm ("3.2 thang 4") → None
def level_in(line: str) -> str | None:
    match = re.search(r", (?:mức )?(thấp|trung bình|cao)\b", line)
    return match.group(1) if match else None


# Dòng Mốc phải lấy mức từ tinh_san (code đã so với mốc trường), không tự xếp theo ngưỡng cứng. Thiếu dòng tính sẵn
# vì học sinh chưa có điểm / hoạt động thì ghi "chưa thi" / "chưa có"; có điểm mà chưa có mốc trường thì "chưa có mốc"
def moc_words(tinh_san):
    def scored(name, key, missing):
        if key not in tinh_san:
            return f"{name} {missing}"
        return f"{name} {level_in(tinh_san[key]) or 'chưa có mốc'}"

    ec = tinh_san.get("ec_level")
    budget = re.search(r"mức (\S+)$", tinh_san["budget_level"]).group(1)
    return [
        scored("GPA", "gpa_level", "chưa có"),
        scored("tiếng Anh", "english_level", "chưa thi"),
        scored("SAT", "sat_level", "chưa thi"),
        f"hoạt động {ec}" if ec else "hoạt động chưa có",
        f"ngân sách {budget}",
    ]


def test_the_moc_line_of_every_real_skill_copies_the_levels_code_calculated():
    rows = real_skill_rows()

    assert (
        not rows
    )  # v9: dữ liệu thật không còn skill; khi có skill lại thì mỗi dòng Mốc phải khớp mức code tính
    for row in rows:
        request = json.loads(row["messages"][1]["content"])
        moc = re.search(r"# Mốc: (.*)", row["messages"][2]["content"]).group(1)
        for words in moc_words(request["tinh_san"]):
            assert words in moc, (request["cau_hoi"], words, moc)
