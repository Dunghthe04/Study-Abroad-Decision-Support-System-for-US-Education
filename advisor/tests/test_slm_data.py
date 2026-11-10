"""Dữ liệu của SLM: đủ 6 loại, đúng chuẩn (danh sách ý intents), không tự mâu thuẫn, và JSONL khớp với Excel."""

import json
from collections import Counter, defaultdict

import pytest
from openpyxl import Workbook

from app.schemas.slm import MAX_RAW_Y, SUBTYPES
from scripts.build_slm_jsonl import (
    COLUMNS,
    DEFAULT_EXCEL,
    DEFAULT_OUT,
    SPLITS,
    TYPES,
    read_records,
    to_record,
)

MIN_ROWS_PER_TYPE = 30


@pytest.fixture(scope="module")
def records():
    return read_records(DEFAULT_EXCEL)  # dựng thẳng từ Excel


def test_the_jsonl_is_exactly_what_the_excel_builds_so_nobody_forgot_to_rebuild():
    on_disk = [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]

    assert on_disk == read_records(DEFAULT_EXCEL)


def test_every_type_has_enough_rows_and_all_three_splits(records):
    by_type = defaultdict(Counter)
    for r in records:
        by_type[r["type"]][r["split"]] += 1

    assert set(by_type) == set(TYPES)
    for intent, splits in by_type.items():
        assert sum(splits.values()) >= MIN_ROWS_PER_TYPE, intent
        assert all(splits[s] > 0 for s in SPLITS), f"{intent} thiếu phần {splits}"


def test_ids_are_unique(records):
    assert len({r["id"] for r in records}) == len(records)


def test_a_record_has_exactly_the_fields_of_the_jsonl_line(records):
    assert all(set(r) == {"id", "type", "split", "input", "output"} for r in records)
    assert all(set(r["output"]) == {"intents"} for r in records)
    assert all({"question", "profile", "pending_question"} <= set(r["input"]) for r in records)


def test_the_same_message_in_the_same_context_never_has_two_different_types(records):
    seen: dict[tuple, set[str]] = defaultdict(set)
    for r in records:
        context = (
            r["input"]["question"].lower(),
            r["input"]["pending_question"],
            json.dumps(r["input"]["profile"]),
            json.dumps(
                r["input"].get("context"), sort_keys=True
            ),  # cùng chữ nhưng ngữ cảnh khác thì được khác loại
        )
        seen[context].add(r["type"])

    conflicts = {key: types for key, types in seen.items() if len(types) > 1}
    assert not conflicts, conflicts


def test_scenarios_always_say_which_mode(records):
    for r in records:
        for y in r["output"]["intents"]:
            if y["type"] == "scenario":
                assert y["entities"]["mode"] in ("what_if", "goal_seeking"), r["id"]


def test_the_sheet_type_is_always_one_of_the_intents_of_the_row(records):
    for r in records:
        assert r["type"] in {y["type"] for y in r["output"]["intents"]}, r["id"]


def test_rows_with_several_intents_cover_the_cases_the_model_must_learn(records):
    rows = [[y["type"] for y in r["output"]["intents"]] for r in records]

    assert any("collect_profile" in types and len(types) > 1 for types in rows), (
        "thiếu: khai hồ sơ kèm câu hỏi"
    )
    assert any(types.count("knowledge") >= 2 for types in rows), "thiếu: hai câu kiến thức liền nhau"
    assert any(len(types) >= 3 for types in rows), "thiếu: tin từ ba ý trở lên"


def test_the_hard_cases_exist_in_the_data(records):
    questions = [r["input"]["question"] for r in records]
    subtypes = {
        y["entities"].get("subtype")
        for r in records
        for y in r["output"]["intents"]
        if y["type"] == "out_of_scope"
    }

    assert any("visa f1 can giay to gi" in q for q in questions), "thiếu câu không dấu"
    assert subtypes == set(SUBTYPES)


# ---- script đổi Excel sang JSONL ----
CENTER = {
    "type": "center_referral",
    "span": "tìm trung tâm sửa bài luận ở Sài Gòn",
    "entities": {"service": "ESSAY_EDITING", "city": "Sài Gòn"},
}
GREETING = {"type": "out_of_scope", "span": "chào", "entities": {"subtype": "farewell"}}


def row(**overrides):
    base = dict.fromkeys(COLUMNS)
    base |= {
        "question": "tìm trung tâm sửa bài luận ở Sài Gòn",
        "split": "train",
        "intents": json.dumps([CENTER]),
    }
    return base | overrides


def ys(*items):
    return json.dumps(list(items), ensure_ascii=False)


def check(intent, raw):
    return to_record(intent, raw, f"{intent}, dòng 2", f"{intent}_001")


def test_a_good_row_becomes_a_record():
    record = check("center_referral", row())

    assert record["output"] == {"intents": [CENTER]}
    assert record["input"]["profile"] is None
    assert set(record) == {"id", "type", "split", "input", "output"}


def test_a_row_with_several_intents_keeps_them_in_the_order_the_student_said_them():
    record = check(
        "collect_profile",
        row(
            question="em GPA 3.5, I-20 là gì, SAT là gì",
            intents=ys(
                {"type": "collect_profile", "span": "em GPA 3.5", "entities": {"gpa": "GPA 3.5"}},
                {"type": "knowledge", "span": "I-20 là gì", "entities": {}},
                {"type": "knowledge", "span": "SAT là gì", "entities": {}},
            ),
        ),
    )

    assert [y["span"] for y in record["output"]["intents"]] == ["em GPA 3.5", "I-20 là gì", "SAT là gì"]


def test_a_rewritten_span_is_fine_when_it_only_uses_words_of_the_message():
    record = check(
        "knowledge",
        row(
            question="I-20 và SAT là gì?",
            intents=ys(
                {"type": "knowledge", "span": "I-20 là gì", "entities": {}},
                {"type": "knowledge", "span": "SAT là gì", "entities": {}},
            ),
        ),
    )

    assert len(record["output"]["intents"]) == 2


def test_collect_profile_must_come_first_even_when_the_student_says_the_profile_last():
    with pytest.raises(ValueError, match="collect_profile phải đứng đầu"):
        check(
            "knowledge",
            row(
                question="I-20 là gì, à em GPA 3.5",
                intents=ys(
                    {"type": "knowledge", "span": "I-20 là gì", "entities": {}},
                    {"type": "collect_profile", "span": "em GPA 3.5", "entities": {"gpa": "GPA 3.5"}},
                ),
            ),
        )


def test_profile_values_belong_to_the_collect_profile_intent_never_to_a_question_intent():
    with pytest.raises(ValueError, match="chỉ ghi ở ý collect_profile"):
        check(
            "profile_review",
            row(
                question="GPA 3.5, em nên nộp trường nào",
                intents=ys(
                    {
                        "type": "profile_review",
                        "span": "em nên nộp trường nào",
                        "entities": {"gpa": "GPA 3.5"},
                    }
                ),
            ),
        )


@pytest.mark.parametrize(
    ("overrides", "message"),
    [
        ({"split": "dev"}, "split phải là"),
        ({"question": None}, "thiếu question"),
        ({"intents": None}, "ít nhất một ý"),
        ({"intents": "[]"}, "ít nhất một ý"),
        ({"intents": "{không phải json"}, "không phải JSON"),
        ({"intents": '{"type": "center_referral"}'}, "phải là danh sách ý"),
        ({"intents": ys({**CENTER, "entities": {"service": "LAM_VISA_HO"}})}, "mã dịch vụ lạ"),
        (
            {"intents": ys({**CENTER, "entities": {"city": "TP.HCM"}})},
            "có trong question",
        ),  # phải ghi "Sài Gòn"
        ({"intents": ys({**CENTER, "entities": {"mau_sac": "đỏ"}})}, "khóa entities lạ"),
        ({"intents": ys({**CENTER, "span": "tìm trung tâm luyện visa"})}, "không có trong câu hỏi"),
        (
            {"intents": ys({**CENTER, "type": "knowledge", "entities": {}})},
            "không có ý nào loại center_referral",
        ),
        (
            {"question": "chào, tìm trung tâm sửa bài luận ở Sài Gòn", "intents": ys(GREETING, CENTER)},
            "bị gộp hoặc bỏ",  # câu chào đi kèm ý thật thì không ghi vào dữ liệu
        ),
        ({"pending_question": "Câu hỏi không có trong chat"}, "không khớp câu nào"),
        ({"profile": '{"mau_sac": "đỏ"}'}, "profile có khóa lạ"),
        ({"profile": '["gpa"]'}, "object JSON"),
        ({"context": '["Harvard"]'}, "object JSON"),
        (
            {"intents": ys({**CENTER, "entities": {"tasks": "booking"}})},
            "tasks của loại",
        ),  # tasks luôn là danh sách
    ],
)
def test_bad_rows_are_rejected_with_the_sheet_and_line(overrides, message):
    with pytest.raises(ValueError, match=message):
        check("center_referral", row(**overrides))


def test_two_intents_of_a_type_that_must_merge_are_rejected():
    first = {"type": "profile_review", "span": "đánh giá hồ sơ giúp em", "entities": {}}
    second = {"type": "profile_review", "span": "em nên làm gì", "entities": {}}
    with pytest.raises(ValueError, match="bị gộp hoặc bỏ"):
        check(
            "profile_review",
            row(question="đánh giá hồ sơ giúp em, em nên làm gì", intents=ys(first, second)),
        )


def test_a_row_with_more_intents_than_the_technical_ceiling_is_rejected():
    asked = [f"câu {i}" for i in range(1, MAX_RAW_Y + 2)]
    many = [{"type": "knowledge", "span": span, "entities": {}} for span in asked]
    with pytest.raises(ValueError, match="nhiều nhất"):
        check("knowledge", row(question=", ".join(asked), intents=ys(*many)))


def test_profile_values_in_entities_must_be_text_from_the_message():
    with pytest.raises(ValueError, match="đoạn chữ"):
        check(
            "center_referral",
            row(intents=ys({**CENTER, "entities": {"service": "ESSAY_EDITING", "gpa": 8.2}})),
        )


def test_a_profile_answer_without_a_pending_question_must_carry_a_value():
    with pytest.raises(ValueError, match="phải có giá trị hồ sơ"):
        only = {"type": "collect_profile", "span": "cập nhật SAT giúp em", "entities": {}}
        check("collect_profile", row(question="cập nhật SAT giúp em", intents=ys(only)))


def test_a_scenario_needs_a_mode_and_a_change():
    with pytest.raises(ValueError, match="cần changes"):
        only = {
            "type": "scenario",
            "span": "tìm trung tâm sửa bài luận ở Sài Gòn",
            "entities": {"mode": "what_if"},
        }
        check("scenario", row(intents=ys(only)))


def test_the_form_is_checked_ids_come_from_the_sheet_and_blank_rows_are_skipped(tmp_path):
    wb = Workbook()
    wb.active.title = "center_referral"
    wb.active.append(list(COLUMNS))
    wb.active.append([row()[c] for c in COLUMNS])
    wb.active.append([None] * len(COLUMNS))
    wb.active.append([row()[c] for c in COLUMNS])
    path = tmp_path / "du_lieu.xlsx"
    wb.save(path)
    assert [r["id"] for r in read_records(path)] == ["center_referral_001", "center_referral_002"]

    wb.active.cell(1, len(COLUMNS) + 1, "ghi_chu")
    wb.save(path)
    with pytest.raises(ValueError, match="đúng các cột"):
        read_records(path)

    wb.active.delete_cols(len(COLUMNS) + 1)
    wb.create_sheet("Huong dan")
    wb.save(path)
    with pytest.raises(ValueError, match="mỗi loại một sheet"):
        read_records(path)


def test_the_readme_and_summary_sheets_are_not_data(tmp_path):
    wb = Workbook()
    wb.active.title = "README"
    wb.active.append(["giải thích cho người đọc"])
    wb.create_sheet("summary").append(["sheet", "v3"])
    ws = wb.create_sheet("knowledge")
    ws.append(list(COLUMNS))
    ws.append(
        ["I-20 là gì", None, None, '[{"type": "knowledge", "span": "I-20 là gì", "entities": {}}]', "train"]
    )
    path = tmp_path / "du_lieu.xlsx"
    wb.save(path)

    assert [r["id"] for r in read_records(path)] == ["knowledge_001"]


def test_a_row_with_context_keeps_it_in_the_input_in_the_same_form_the_model_will_get():
    follow_up = {
        "type": "knowledge",
        "span": "còn Yale thì sao",
        "entities": {"tasks": ["school_info"], "schools": ["Yale"], "follow_up": True},
    }
    context = {"schools": ["Harvard"], "fields": ["cost"]}
    record = check(
        "knowledge", row(question="còn Yale thì sao", intents=ys(follow_up), context=json.dumps(context))
    )

    assert record["input"]["context"] == context
    assert "context" not in check("center_referral", row())["input"]  # không ngữ cảnh: không có khóa


def test_a_follow_up_may_take_the_city_from_the_context_but_only_that_city():
    followup = {
        "type": "center_referral",
        "span": "thế còn luyện phỏng vấn",
        "entities": {"service": "VISA_INTERVIEW_PREP", "city": "Hà Nội", "follow_up": True},
    }
    good = row(
        question="thế còn luyện phỏng vấn",
        intents=ys(followup),
        context=json.dumps({"service": "ESSAY_EDITING", "city": "Hà Nội"}),
    )
    check("center_referral", good)

    with pytest.raises(ValueError, match="phải là đoạn chữ"):  # thành phố khác ngữ cảnh
        check("center_referral", good | {"context": json.dumps({"city": "Huế"})})
    not_follow_up = {**followup, "entities": {"service": "VISA_INTERVIEW_PREP", "city": "Hà Nội"}}
    with pytest.raises(
        ValueError, match="phải là đoạn chữ"
    ):  # không nối tiếp thì thành phố phải nằm trong câu
        check("center_referral", good | {"intents": ys(not_follow_up)})


def test_the_context_column_is_optional_on_every_sheet(tmp_path):
    item = {
        "type": "center_referral",
        "span": "thế còn luyện phỏng vấn",
        "entities": {"service": "VISA_INTERVIEW_PREP", "city": "Hà Nội", "follow_up": True},
    }
    wb = Workbook()
    wb.active.title = "center_referral"
    wb.active.append(["question", "profile", "pending_question", "intents", "split", "context"])
    wb.active.append(
        [
            "thế còn luyện phỏng vấn",
            None,
            None,
            json.dumps([item], ensure_ascii=False),
            "train",
            json.dumps({"service": "ESSAY_EDITING", "city": "Hà Nội"}),
        ]
    )
    path = tmp_path / "ctx.xlsx"
    wb.save(path)

    record = read_records(path)[0]

    assert record["input"]["context"] == {"service": "ESSAY_EDITING", "city": "Hà Nội"}
    assert record["output"]["intents"] == [item]


def test_the_review_column_is_ignored_by_the_build_and_does_not_reach_the_data(tmp_path):
    path = tmp_path / "du_lieu.xlsx"
    wb = Workbook()
    ws = wb.active
    ws.title = "knowledge"
    ws.append(["question", "profile", "pending_question", "intents", "split", "context", "review"])
    ws.append(
        [
            "I-20 là gì",
            None,
            None,
            '[{"type": "knowledge", "span": "I-20 là gì", "entities": {}}]',
            "train",
            None,
            "v10",
        ]
    )
    wb.save(path)

    records = read_records(path)

    assert len(records) == 1 and "review" not in json.dumps(records[0])
