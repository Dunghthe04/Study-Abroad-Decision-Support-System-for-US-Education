"""Dữ liệu của SLM: đủ 6 loại, đúng chuẩn (danh sách ý cac_y), không tự mâu thuẫn, và JSONL khớp với Excel."""

import json
from collections import Counter, defaultdict

import pytest
from openpyxl import Workbook

from app.schemas.slm import MAX_RAW_Y, SKILL_SECTIONS
from scripts.build_slm_jsonl import (
    CALC_COLUMN,
    COLUMNS,
    DEFAULT_EXCEL,
    DEFAULT_OUT,
    SKILL_TYPES,
    SPLITS,
    TYPES,
    context_to_internal,
    intents_to_internal,
    read_records,
    to_record,
    v2_entities_to_internal,
)

MIN_ROWS_PER_TYPE = 30


@pytest.fixture(scope="module")
def records():
    return [json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines()]


def test_the_jsonl_is_exactly_what_the_excel_builds_so_nobody_forgot_to_rebuild(records):
    assert records == read_records(DEFAULT_EXCEL)


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


def test_skills_exist_only_for_profile_and_scenario_rows_that_have_a_profile(records):
    with_skill = [r for r in records if r["skill_md"]]

    assert with_skill, "chưa có dòng nào có skill"
    assert {r["type"] for r in with_skill} <= set(SKILL_TYPES)
    assert all(r["input"]["ho_so"] for r in with_skill)


def test_the_same_message_in_the_same_context_never_has_two_different_types(
    records,
):  # ngữ cảnh gồm cả dang_noi_ve
    seen: dict[tuple, set[str]] = defaultdict(set)
    for r in records:
        context = (
            r["input"]["cau_hoi"].lower(),
            r["input"]["cau_he_thong_dang_hoi"],
            json.dumps(r["input"]["ho_so"]),
            json.dumps(
                r["input"].get("dang_noi_ve"), sort_keys=True
            ),  # cùng chữ nhưng ngữ cảnh khác thì được khác loại
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


def test_a_row_with_more_intents_than_the_technical_ceiling_is_rejected():
    asked = [f"câu {i}" for i in range(1, MAX_RAW_Y + 2)]
    many = [{"type": "kien_thuc", "doan": doan, "thuc_the": {}} for doan in asked]
    with pytest.raises(ValueError, match="nhiều nhất"):
        check("kien_thuc", row(cau_hoi=", ".join(asked), cac_y=ys(*many)))


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


PROFILE_ROW = {
    "cau_hoi": "đánh giá hồ sơ giúp em",
    "ho_so": json.dumps({"gpa": "8 thang 10"}),
    "cac_y": ys({"type": "ho_so", "doan": "đánh giá hồ sơ giúp em", "thuc_the": {}}),
}
CALC = {"gpa_level": "3.2 thang 4, thấp (TB các trường 3.6)"}


def skill_of(text):
    return "\n".join(f"## {name}\n{text}" for name in SKILL_SECTIONS["ho_so"])


def test_calculated_lines_are_kept_apart_from_the_input_and_the_skill_must_keep_their_numbers():
    good = skill_of("GPA 8 thang 10, 3.2 thang 4, TB các trường 3.6")
    record = check(
        "ho_so", row(**PROFILE_ROW, skill_md=good, **{CALC_COLUMN: json.dumps(CALC, ensure_ascii=False)})
    )

    assert record["tinh_san"] == CALC
    assert "tinh_san" not in record["input"]  # đầu vào tách ý không đổi
    assert "tinh_san" not in check("ho_so", row(**PROFILE_ROW, skill_md=good))  # cột trống thì như dòng cũ
    with pytest.raises(ValueError, match="bỏ mất số"):
        check(
            "ho_so",
            row(**PROFILE_ROW, skill_md=skill_of("GPA 8 thang 10"), **{CALC_COLUMN: json.dumps(CALC)}),
        )


@pytest.mark.parametrize(
    ("calc", "message"),
    [('{"mau_sac": "đỏ"}', "khóa lạ"), ('["3.2"]', "object JSON"), ("{x", "không phải JSON")],
)
def test_bad_calculated_lines_are_rejected(calc, message):
    with pytest.raises(ValueError, match=message):
        check("ho_so", row(**PROFILE_ROW, **{CALC_COLUMN: calc}))


def test_the_profile_sheet_may_have_the_calculated_column_after_the_profile(tmp_path):
    header = ["question", "profile", "profile_calc", "pending_question", "intents", "skill_md", "split"]
    wb = Workbook()
    wb.active.title = "ho_so"
    wb.active.append(header)
    wb.active.append(
        [
            PROFILE_ROW["cau_hoi"],
            PROFILE_ROW["ho_so"],
            json.dumps(CALC),
            None,
            PROFILE_ROW["cac_y"],
            None,
            "train",
        ]
    )
    path = tmp_path / "calc.xlsx"
    wb.save(path)

    assert read_records(path)[0]["tinh_san"] == CALC


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


def test_an_english_workbook_builds_the_same_record_as_the_internal_names(tmp_path):
    english = [
        {
            "type": "scenario",
            "span": "nếu em đạt GPA 9,0 thang 10 và ngân sách 800 triệu thì sao",
            "entities": {
                "mode": "what_if",
                "changes": [
                    {"factor": "gpa", "value": "9.0", "unit": "scale_10"},
                    {"factor": "budget", "value": "800000000", "unit": "VND"},
                    {"factor": "sat", "action": "skip"},
                ],
            },
        }
    ]
    internal = [
        {
            "type": "kich_ban",
            "doan": "nếu em đạt GPA 9,0 thang 10 và ngân sách 800 triệu thì sao",
            "thuc_the": {
                "che_do": "what_if",
                "thay_doi": [
                    {"yeu_to": "gpa", "gia_tri": "9.0", "don_vi": "thang_10"},
                    {"yeu_to": "budget", "gia_tri": "800000000", "don_vi": "VND"},
                    {"yeu_to": "sat", "hanh_dong": "bo_qua"},
                ],
            },
        }
    ]
    question = english[0]["span"]

    wb = Workbook()
    ws = wb.active
    ws.title = "scenario"
    ws.append(["question", "profile", "pending_question", "intents", "split"])
    ws.append([question, None, None, json.dumps(english, ensure_ascii=False), "train"])
    path = tmp_path / "en.xlsx"
    wb.save(path)

    [record] = read_records(path)

    assert record["type"] == "kich_ban" and record["id"] == "kich_ban_001"
    assert record["input"]["cau_hoi"] == question and record["output"]["cac_y"] == internal
    assert record["skill_md"] is None  # kịch bản do code tính, không có skill


def test_the_scenario_sheet_has_no_skill_column_but_an_old_empty_one_is_still_read(tmp_path):
    ys = json.dumps(
        [
            {
                "type": "kich_ban",
                "doan": "cần gì để vào MIT",
                "thuc_the": {"che_do": "goal_seeking", "truong": "MIT"},
            }
        ]
    )
    for header in (COLUMNS, COLUMNS[:-1] + ("skill_md", "tap")):
        wb = Workbook()
        wb.active.title = "kich_ban"
        wb.active.append(list(header))
        wb.active.append(
            ["cần gì để vào MIT", None, None, ys] + ([None] if "skill_md" in header else []) + ["train"]
        )
        path = tmp_path / f"{len(header)}.xlsx"
        wb.save(path)
        assert [r["id"] for r in read_records(path)] == ["kich_ban_001"]


def test_the_hard_cases_exist_in_the_data(records):
    questions = [r["input"]["cau_hoi"] for r in records]
    types = {y["type"]: y for r in records for y in r["output"]["cac_y"]}

    assert any("visa f1 can giay to gi" in q for q in questions), "thiếu câu không dấu"
    assert {y["thuc_the"].get("loai_con") for r in records for y in r["output"]["cac_y"] if y["type"] == "ngoai_pham_vi"} == {
        "ngoai_le", "nhay_cam", "tam_biet", "mo_ho", "ve_he_thong", "gap_nguoi",
    }  # fmt: skip
    assert types


# ---- khuôn v2 viết bằng tiếng Anh trong Excel: đổi về tên nội bộ đúng và đủ ----
@pytest.mark.parametrize(
    ("kind", "english", "internal"),
    [
        (
            "collect_profile",
            {"tasks": ["edit"], "sat": "SAT là 1400"},
            {"nhiem_vu": "sua", "sat": "SAT là 1400"},
        ),
        (
            "collect_profile",
            {"tasks": ["edit"], "sat": "1450", "follow_up": True},
            {"nhiem_vu": "sua", "sat": "1450", "tiep_noi": True},
        ),
        ("collect_profile", {"tasks": ["status"]}, {"nhiem_vu": "xem_ho_so"}),
        ("collect_profile", {"tasks": ["restart"]}, {"nhiem_vu": "lam_lai"}),
        ("collect_profile", {"gpa": "3.5"}, {"gpa": "3.5"}),
        ("profile_review", {"tasks": []}, {"cac_nhiem_vu": []}),
        ("profile_review", {"tasks": ["assessment", "roadmap"]}, {"cac_nhiem_vu": ["danh_gia", "lo_trinh"]}),
        (
            "profile_review",
            {"tasks": ["compare"], "schools": ["BU", "NYU"], "follow_up": True},
            {"cac_nhiem_vu": ["so_sanh"], "cac_truong": ["BU", "NYU"], "tiep_noi": True},
        ),
        (
            "profile_review",
            {"tasks": ["explain_school"], "schools": ["Duke"]},
            {"cac_nhiem_vu": ["giai_thich_truong"], "truong": "Duke"},
        ),
        (
            "knowledge",
            {"tasks": ["school_info"], "schools": ["Harvard"], "fields": ["cost", "aid"]},
            {"nhiem_vu": "tra_cuu", "truong": "Harvard", "muc": ["chi_phi", "hoc_bong"]},
        ),
        (
            "knowledge",
            {"tasks": ["compare_facts"], "schools": ["A", "B", "C", "D"], "fields": ["ranking"]},
            {"nhiem_vu": "so_sanh", "cac_truong": ["A", "B", "C", "D"], "muc": ["xep_hang"]},
        ),
        (
            "knowledge",
            {
                "tasks": ["search"],
                "filters": {"state": "Texas", "school_type": "public", "aid": "yes", "max_cost": "30k"},
                "sort": "lowest_cost",
            },
            {
                "nhiem_vu": "tim_kiem",
                "bo_loc": {
                    "bang": ["Texas"],
                    "loai_truong": "cong_lap",
                    "hoc_bong": "co",
                    "chi_phi_toi_da": "30k",
                },
                "sap_xep": "chi_phi_thap",
            },
        ),
        ("knowledge", {"tasks": ["general"]}, {}),  # kiến thức chung không ghi nhiệm vụ
        (
            "knowledge",
            {"tasks": ["school_info"], "schools": ["Purdue"], "field": "cost"},
            {"nhiem_vu": "tra_cuu", "truong": "Purdue", "muc": ["chi_phi"]},
        ),
        (
            "center_referral",
            {"tasks": ["find"], "service": "ESSAY_EDITING", "city": "Hà Nội", "follow_up": True},
            # service và city đổi tên ở bước đổi tên chung (EN_ENTITY_KEYS), không phải ở đây
            {"nhiem_vu": "tim", "service": "ESSAY_EDITING", "city": "Hà Nội", "tiep_noi": True},
        ),
        ("center_referral", {"tasks": ["booking"]}, {"nhiem_vu": "dat_lich"}),
        (
            "center_referral",
            {"tasks": ["compare"], "centers": ["PEC", "Kosam"]},
            {"nhiem_vu": "so_sanh", "cac_trung_tam": ["PEC", "Kosam"]},
        ),
    ],
)
def test_the_v2_english_entities_become_the_internal_names(kind, english, internal):
    assert v2_entities_to_internal(kind, english) == internal


def test_an_item_written_with_internal_type_names_is_left_alone():
    item = {"type": "kien_thuc", "doan": "x", "thuc_the": {"nhiem_vu": "tra_cuu", "truong": "MIT"}}

    assert intents_to_internal([item]) == [item]


def test_a_full_english_item_becomes_a_valid_internal_item():
    items = intents_to_internal(
        [{"type": "out_of_scope", "span": "bạn là ai", "entities": {"subtype": "about_system"}}]
    )

    assert items == [{"type": "ngoai_pham_vi", "doan": "bạn là ai", "thuc_the": {"loai_con": "ve_he_thong"}}]


def test_the_context_column_is_renamed_like_the_entities():
    english = {
        "schools": ["Emory"],
        "fields": ["gpa"],
        "filters": {"state": ["Texas"], "aid": "yes"},
        "last_field": "sat",
        "last_task": "school_list",
        "service": "TEST_PREP",
        "city": "Hà Nội",
        "centers": ["PEC", "Kosam"],
        "scenario": {"mode": "what_if", "changes": [{"factor": "english", "value": "7.0", "unit": "ielts"}]},
    }

    assert context_to_internal(english) == {
        "truong": ["Emory"],
        "muc": ["gpa"],
        "bo_loc": {"bang": ["Texas"], "hoc_bong": "co"},
        "muc_vua_nhap": "sat",
        "viec_vua_lam": "chon_truong",
        "dich_vu": "LUYEN_THI",
        "thanh_pho": "Hà Nội",
        "cac_trung_tam": ["PEC", "Kosam"],
        "kich_ban": {
            "che_do": "what_if",
            "thay_doi": [{"yeu_to": "english", "gia_tri": "7.0", "don_vi": "ielts"}],
        },
    }
    assert context_to_internal({}) is None
    assert context_to_internal(None) is None


def test_a_row_with_context_keeps_it_in_the_input_in_the_same_form_the_model_will_get():
    follow_up = {
        "type": "kien_thuc",
        "doan": "còn Yale thì sao",
        "thuc_the": {"truong": "Yale", "tiep_noi": True},
    }
    record = check(
        "kien_thuc",
        row(
            cau_hoi="còn Yale thì sao",
            cac_y=ys(follow_up),
            context=json.dumps({"schools": ["Harvard"], "fields": ["cost"]}),
        ),
    )

    assert record["input"]["dang_noi_ve"] == {"truong": ["Harvard"], "muc": ["chi_phi"]}
    assert "dang_noi_ve" not in check("trung_tam", row())["input"]  # không ngữ cảnh: giữ khuôn cũ


def test_a_follow_up_may_take_the_city_from_the_context_but_only_that_city():
    followup = {
        "type": "trung_tam",
        "doan": "thế còn luyện phỏng vấn",
        "thuc_the": {"dich_vu": "LUYEN_PHONG_VAN", "thanh_pho": "Hà Nội", "tiep_noi": True},
    }
    good = row(
        cau_hoi="thế còn luyện phỏng vấn",
        cac_y=ys(followup),
        context=json.dumps({"service": "ESSAY_EDITING", "city": "Hà Nội"}),
    )
    check("trung_tam", good)

    with pytest.raises(ValueError, match="phải là đoạn chữ"):  # thành phố khác ngữ cảnh
        check("trung_tam", good | {"context": json.dumps({"city": "Huế"})})
    not_follow_up = {**followup, "thuc_the": {"dich_vu": "LUYEN_PHONG_VAN", "thanh_pho": "Hà Nội"}}
    with pytest.raises(
        ValueError, match="phải là đoạn chữ"
    ):  # không nối tiếp thì thành phố phải nằm trong câu
        check("trung_tam", good | {"cac_y": ys(not_follow_up)})


def test_the_context_column_is_optional_on_every_sheet(tmp_path):
    item = {
        "type": "center_referral",
        "span": "thế còn luyện phỏng vấn",
        "entities": {
            "tasks": ["find"],
            "service": "VISA_INTERVIEW_PREP",
            "city": "Hà Nội",
            "follow_up": True,
        },
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

    assert record["input"]["dang_noi_ve"] == {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Hà Nội"}
    assert record["output"]["cac_y"][0]["thuc_the"] == {
        "nhiem_vu": "tim",
        "dich_vu": "LUYEN_PHONG_VAN",
        "thanh_pho": "Hà Nội",
        "tiep_noi": True,
    }
