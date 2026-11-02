"""Dữ liệu của SLM: đủ 6 loại, đúng chuẩn (danh sách ý cac_y), không tự mâu thuẫn, và JSONL khớp với Excel."""

import json
from collections import Counter, defaultdict

import pytest
from openpyxl import Workbook

from app.schemas.slm import MAX_Y
from scripts.build_slm_jsonl import (
    COLUMNS,
    DEFAULT_EXCEL,
    DEFAULT_OUT,
    SKILL_TYPES,
    SPLITS,
    TYPES,
    read_records,
    to_record,
)

MIN_ROWS_PER_TYPE = 30
# Loại làm sau cùng, chưa có dữ liệu: gỡ khỏi danh sách này khi bắt đầu gán nhãn kịch bản
POSTPONED = {"kich_ban"}


@pytest.fixture(scope="module")
def records():
    return [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]


def test_the_jsonl_is_exactly_what_the_excel_builds_so_nobody_forgot_to_rebuild(records):
    assert records == read_records(DEFAULT_EXCEL)


def test_every_type_has_enough_rows_and_all_three_splits(records):
    by_type = defaultdict(Counter)
    for r in records:
        by_type[r["type"]][r["split"]] += 1

    assert set(by_type) == set(TYPES) - POSTPONED
    for intent, splits in by_type.items():
        assert sum(splits.values()) >= MIN_ROWS_PER_TYPE, intent
        assert all(splits[s] > 0 for s in SPLITS), f"{intent} thiếu phần {splits}"


def test_ids_are_unique(records):
    assert len({r["id"] for r in records}) == len(records)


def test_skills_exist_only_for_profile_and_scenario_rows_that_have_a_profile(records):
    with_skill = [r for r in records if r["skill_md"]]

    assert with_skill, "chưa có dòng nào có skill"
    assert {r["type"] for r in with_skill} <= set(SKILL_TYPES)
    assert all(r["input"]["ho_so"] for r in with_skill)


def test_the_same_message_in_the_same_context_never_has_two_different_types(records):
    seen: dict[tuple, set[str]] = defaultdict(set)
    for r in records:
        context = (
            r["input"]["cau_hoi"].lower(),
            r["input"]["cau_he_thong_dang_hoi"],
            json.dumps(r["input"]["ho_so"]),
        )
        seen[context].add(r["type"])

    conflicts = {key: types for key, types in seen.items() if len(types) > 1}
    assert not conflicts, conflicts


def test_scenarios_always_say_which_mode(records):
    for r in records:
        for y in r["output"]["cac_y"]:
            if y["type"] == "kich_ban":
                assert y["thuc_the"]["che_do"] in ("what_if", "goal_seeking"), r["id"]


def test_the_sheet_type_is_always_one_of_the_intents_of_the_row(records):
    for r in records:
        assert r["type"] in {y["type"] for y in r["output"]["cac_y"]}, r["id"]


def test_rows_with_several_intents_cover_the_cases_the_model_must_learn(records):
    rows = [[y["type"] for y in r["output"]["cac_y"]] for r in records]

    assert any("mac_dinh" in types and len(types) > 1 for types in rows), "thiếu: khai hồ sơ kèm câu hỏi"
    assert any(types.count("kien_thuc") >= 2 for types in rows), "thiếu: hai câu kiến thức liền nhau"
    assert any(len(types) >= 3 for types in rows), "thiếu: tin từ ba ý trở lên"
    assert any(len(types) > MAX_Y + 1 for types in rows), "thiếu: tin dài hơn mức trả lời ngay"


# ---- script đổi Excel sang JSONL ----
CENTER = {
    "type": "trung_tam",
    "doan": "tìm trung tâm sửa bài luận ở Sài Gòn",
    "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Sài Gòn"},
}
GREETING = {"type": "ngoai_pham_vi", "doan": "chào", "thuc_the": {"loai_con": "tam_biet"}}


def row(**overrides):
    base = dict.fromkeys(COLUMNS)
    base |= {"cau_hoi": "tìm trung tâm sửa bài luận ở Sài Gòn", "tap": "train", "cac_y": json.dumps([CENTER])}
    return base | overrides


def ys(*items):
    return json.dumps(list(items), ensure_ascii=False)


def check(intent, raw):
    return to_record(intent, raw, f"{intent}, dòng 2", f"{intent}_001")


def test_a_good_row_becomes_a_record():
    record = check("trung_tam", row())

    assert record["output"] == {"cac_y": [CENTER]}
    assert record["input"]["ho_so"] is None and record["skill_md"] is None


def test_a_row_with_several_intents_keeps_them_in_the_order_the_student_said_them():
    record = check(
        "mac_dinh",
        row(
            cau_hoi="em GPA 3.5, I-20 là gì, SAT là gì",
            cac_y=ys(
                {"type": "mac_dinh", "doan": "em GPA 3.5", "thuc_the": {"gpa": "GPA 3.5"}},
                {"type": "kien_thuc", "doan": "I-20 là gì", "thuc_the": {}},
                {"type": "kien_thuc", "doan": "SAT là gì", "thuc_the": {}},
            ),
        ),
    )

    assert [y["doan"] for y in record["output"]["cac_y"]] == ["em GPA 3.5", "I-20 là gì", "SAT là gì"]


def test_a_rewritten_doan_is_fine_when_it_only_uses_words_of_the_message():
    record = check(
        "kien_thuc",
        row(
            cau_hoi="I-20 và SAT là gì?",
            cac_y=ys(
                {"type": "kien_thuc", "doan": "I-20 là gì", "thuc_the": {}},
                {"type": "kien_thuc", "doan": "SAT là gì", "thuc_the": {}},
            ),
        ),
    )

    assert len(record["output"]["cac_y"]) == 2


def test_mac_dinh_must_come_first_even_when_the_student_says_the_profile_last():
    with pytest.raises(ValueError, match="mac_dinh phải đứng đầu"):
        check(
            "kien_thuc",
            row(
                cau_hoi="I-20 là gì, à em GPA 3.5",
                cac_y=ys(
                    {"type": "kien_thuc", "doan": "I-20 là gì", "thuc_the": {}},
                    {"type": "mac_dinh", "doan": "em GPA 3.5", "thuc_the": {"gpa": "GPA 3.5"}},
                ),
            ),
        )


def test_profile_values_belong_to_the_mac_dinh_intent_never_to_a_question_intent():
    with pytest.raises(ValueError, match="chỉ ghi ở ý mac_dinh"):
        check(
            "ho_so",
            row(
                cau_hoi="GPA 3.5, em nên nộp trường nào",
                cac_y=ys({"type": "ho_so", "doan": "em nên nộp trường nào", "thuc_the": {"gpa": "GPA 3.5"}}),
            ),
        )


@pytest.mark.parametrize(
    ("overrides", "message"),
    [
        ({"tap": "dev"}, "tap phải là"),
        ({"cau_hoi": None}, "thiếu cau_hoi"),
        ({"cac_y": None}, "ít nhất một ý"),
        ({"cac_y": "[]"}, "ít nhất một ý"),
        ({"cac_y": "{không phải json"}, "không phải JSON"),
        ({"cac_y": '{"type": "trung_tam"}'}, "phải là danh sách ý"),
        ({"cac_y": ys({**CENTER, "thuc_the": {"dich_vu": "LAM_VISA_HO"}})}, "mã dịch vụ lạ"),
        (
            {"cac_y": ys({**CENTER, "thuc_the": {"thanh_pho": "TP.HCM"}})},
            "có trong cau_hoi",
        ),  # phải ghi "Sài Gòn"
        ({"cac_y": ys({**CENTER, "thuc_the": {"mau_sac": "đỏ"}})}, "khóa thực thể lạ"),
        ({"cac_y": ys({**CENTER, "doan": "tìm trung tâm luyện visa"})}, "không có trong câu hỏi"),
        ({"cac_y": ys({**CENTER, "type": "kien_thuc", "thuc_the": {}})}, "không có ý nào loại trung_tam"),
        (
            {"cau_hoi": "chào, tìm trung tâm sửa bài luận ở Sài Gòn", "cac_y": ys(GREETING, CENTER)},
            "bị gộp hoặc bỏ",  # câu chào đi kèm ý thật thì không ghi vào dữ liệu
        ),
        ({"skill_md": "# skill"}, "không có skill riêng"),
        ({"cau_he_thong_dang_hoi": "Câu hỏi không có trong chat"}, "không khớp câu nào"),
    ],
)
def test_bad_rows_are_rejected_with_the_sheet_and_line(overrides, message):
    with pytest.raises(ValueError, match=message):
        check("trung_tam", row(**overrides))


def test_two_intents_of_a_type_that_must_merge_are_rejected():
    first = {"type": "ho_so", "doan": "đánh giá hồ sơ giúp em", "thuc_the": {}}
    second = {"type": "ho_so", "doan": "em nên làm gì", "thuc_the": {}}
    with pytest.raises(ValueError, match="bị gộp hoặc bỏ"):
        check("ho_so", row(cau_hoi="đánh giá hồ sơ giúp em, em nên làm gì", cac_y=ys(first, second)))


def test_a_row_with_more_intents_than_the_model_may_produce_is_rejected():
    many = [{"type": "kien_thuc", "doan": f"câu {i}", "thuc_the": {}} for i in range(1, 9)]
    with pytest.raises(ValueError, match="nhiều nhất"):
        check("kien_thuc", row(cau_hoi=", ".join(f"câu {i}" for i in range(1, 9)), cac_y=ys(*many)))


def test_profile_values_in_entities_must_be_text_from_the_message():
    with pytest.raises(ValueError, match="đoạn chữ"):
        check("trung_tam", row(cac_y=ys({**CENTER, "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "gpa": 8.2}})))


def test_a_profile_answer_without_a_pending_question_must_carry_a_value():
    with pytest.raises(ValueError, match="phải có giá trị hồ sơ"):
        only = {"type": "mac_dinh", "doan": "cập nhật SAT giúp em", "thuc_the": {}}
        check("mac_dinh", row(cau_hoi="cập nhật SAT giúp em", cac_y=ys(only)))


def test_a_skill_needs_a_profile():
    with pytest.raises(ValueError, match="phải có hồ sơ"):
        only = {"type": "ho_so", "doan": "đánh giá hồ sơ giúp em", "thuc_the": {}}
        check("ho_so", row(skill_md="# skill", cau_hoi="đánh giá hồ sơ giúp em", cac_y=ys(only)))


def test_a_scenario_needs_a_mode_and_a_change():
    with pytest.raises(ValueError, match="cần thay_doi"):
        only = {
            "type": "kich_ban",
            "doan": "tìm trung tâm sửa bài luận ở Sài Gòn",
            "thuc_the": {"che_do": "what_if"},
        }
        check("kich_ban", row(cac_y=ys(only)))


def test_the_form_is_checked_ids_come_from_the_sheet_and_blank_rows_are_skipped(tmp_path):
    wb = Workbook()
    wb.active.title = "trung_tam"
    wb.active.append(list(COLUMNS))
    wb.active.append([row()[c] for c in COLUMNS])
    wb.active.append([None] * len(COLUMNS))
    wb.active.append([row()[c] for c in COLUMNS])
    path = tmp_path / "du_lieu.xlsx"
    wb.save(path)
    assert [r["id"] for r in read_records(path)] == ["trung_tam_001", "trung_tam_002"]

    wb.active.cell(1, len(COLUMNS) + 1, "ghi_chu")
    wb.save(path)
    with pytest.raises(ValueError, match="đúng các cột"):
        read_records(path)

    wb.active.delete_cols(len(COLUMNS) + 1)
    wb.create_sheet("Huong dan")
    wb.save(path)
    with pytest.raises(ValueError, match="mỗi loại một sheet"):
        read_records(path)


def test_the_postponed_types_have_no_rows_yet_so_nobody_trains_on_half_finished_data(records):
    for r in records:
        for y in r["output"]["cac_y"]:
            assert y["type"] not in POSTPONED, (
                f"{r['id']}: {y['type']} chưa gán nhãn, bỏ dòng này hoặc gỡ khỏi POSTPONED"
            )


def test_the_hard_cases_exist_in_the_data(records):
    questions = [r["input"]["cau_hoi"] for r in records]
    types = {y["type"]: y for r in records for y in r["output"]["cac_y"]}

    assert any("visa f1 can giay to gi" in q for q in questions), "thiếu câu không dấu"
    assert {y["thuc_the"].get("loai_con") for r in records for y in r["output"]["cac_y"] if y["type"] == "ngoai_pham_vi"} == {
        "ngoai_le", "nhay_cam", "tam_biet", "mo_ho",
    }  # fmt: skip
    assert types
