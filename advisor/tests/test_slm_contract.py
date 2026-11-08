"""Hợp đồng SLM (danh sách ý): một ý, gộp và cắt ý, kết quả cả tin, khuôn JSON, skill cá nhân hóa, và khớp với dữ liệu."""

import json
from typing import get_args

import pytest
from pydantic import ValidationError

from app.schemas.chat import ChatState
from app.schemas.slm import (
    CENTER_TASKS,
    ENTITY_KEYS,
    INTENTS,
    MAX_RAW_Y,
    PROFILE_KEYS,
    PROFILE_TASKS,
    REVIEW_TASKS,
    SKILL_SECTIONS,
    SLM_TIN_SCHEMA,
    Intent,
    SkillRequest,
    SlmTin,
    SlmTinResult,
    SlmY,
    check_skill,
    chuan_hoa_y,
    skill_key,
)
from app.services.centers import SERVICES
from app.services.skills import info_to_field
from app.services.slm_contract import FixedSlm
from scripts.build_slm_jsonl import DEFAULT_OUT


def y(kind: str, doan: str = "x", **thuc_the) -> SlmY:
    return SlmY(type=kind, doan=doan, thuc_the=thuc_the)


def raw(*items: SlmY) -> dict:
    return {"cac_y": [i.model_dump() for i in items]}


# ---- một ý ----
def test_a_typical_intent_is_read_with_defaults_for_the_rest():
    item = SlmY.model_validate({"type": "kien_thuc", "doan": "I-20 là gì"})

    assert (item.type, item.doan, item.thuc_the) == ("kien_thuc", "I-20 là gì", {})


@pytest.mark.parametrize(
    "answer",
    [
        {"type": "nhu_cau", "doan": "x"},  # loại lạ
        {"type": "kien_thuc", "doan": ""},  # đoạn chữ rỗng
        {"type": "kien_thuc"},  # thiếu đoạn chữ
        {"type": "kien_thuc", "doan": "x", "thuc_the": {"dich_vu": "SUA_BAI_LUAN"}},  # khóa của loại khác
        {"type": "trung_tam", "doan": "x", "thuc_the": {"dich_vu": "LAM_VISA_HO"}},  # mã dịch vụ lạ
        {"type": "kich_ban", "doan": "x", "thuc_the": {"che_do": "what_if"}},  # what-if mà không nói đổi gì
        {"type": "kich_ban", "doan": "x"},  # thiếu che_do
        {"type": "ngoai_pham_vi", "doan": "x"},  # thiếu loai_con
        {"type": "ngoai_pham_vi", "doan": "x", "thuc_the": {"loai_con": "la"}},  # loai_con lạ
        {"type": "mac_dinh", "doan": "x", "thuc_the": {"gpa": 8.5}},  # giá trị hồ sơ phải là chữ
        {"type": "mac_dinh", "doan": "x", "thuc_the": {"mau_sac": "đỏ"}},  # khóa lạ
    ],
)
def test_intents_that_break_the_rules_are_rejected(answer):
    with pytest.raises(ValidationError):
        SlmY.model_validate(answer)


def test_profile_values_of_an_intent_are_only_the_profile_keys():
    item = y("trung_tam", dich_vu="LUYEN_THI", english="IELTS 6.0")

    assert item.profile_values() == {"english": "IELTS 6.0"}


# ---- nhiệm vụ mới: nhập hồ sơ, tư vấn hồ sơ, trung tâm (khuôn v2) ----
@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        ("mac_dinh", {"nhiem_vu": "sua", "sat": "SAT là 1400"}),  # sửa một mục đã nhập
        (
            "mac_dinh",
            {"nhiem_vu": "sua", "sat": "1450", "tiep_noi": True},
        ),  # "à nhầm, 1450 mới đúng": mục lấy từ ngữ cảnh
        ("mac_dinh", {"nhiem_vu": "xem_ho_so"}),
        ("mac_dinh", {"nhiem_vu": "lam_lai"}),
        ("mac_dinh", {"nhiem_vu": "tra_loi", "gpa": "3.5"}),
        ("ho_so", {"cac_nhiem_vu": ["danh_gia", "lo_trinh"]}),
        ("ho_so", {"cac_nhiem_vu": []}),  # chưa rõ muốn gì: hệ thống hỏi lại bằng nút
        ("ho_so", {"cac_nhiem_vu": ["giai_thich_truong"], "truong": "Duke", "tiep_noi": True}),
        ("ho_so", {"cac_nhiem_vu": ["so_sanh"], "cac_truong": ["BU", "NYU"]}),
        ("trung_tam", {"nhiem_vu": "tim", "dich_vu": "SUA_BAI_LUAN", "thanh_pho": "Hà Nội"}),
        ("trung_tam", {"nhiem_vu": "dat_lich", "tiep_noi": True}),
        ("trung_tam", {"nhiem_vu": "so_sanh", "cac_trung_tam": ["PEC", "Kosam"]}),
        ("ngoai_pham_vi", {"loai_con": "ve_he_thong"}),
        ("ngoai_pham_vi", {"loai_con": "gap_nguoi"}),
    ],
)
def test_the_new_task_keys_are_accepted(kind, entities):
    assert y(kind, **entities).type == kind


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        ("mac_dinh", {"nhiem_vu": "sua"}),  # sửa mà không nói mục nào
        ("mac_dinh", {"nhiem_vu": "xem_ho_so", "gpa": "3.5"}),  # xem hồ sơ không kèm giá trị
        ("mac_dinh", {"nhiem_vu": "lam_lai", "gpa": "3.5"}),
        ("mac_dinh", {"nhiem_vu": "tra_cuu"}),  # nhiệm vụ của loại khác
        ("mac_dinh", {"tiep_noi": "true"}),  # phải là boolean
        ("ho_so", {"cac_nhiem_vu": ["danh_gia", "danh_gia"]}),  # lặp
        ("ho_so", {"cac_nhiem_vu": ["viet_luan"]}),  # nhiệm vụ lạ
        ("ho_so", {"cac_nhiem_vu": "danh_gia"}),  # phải là danh sách
        ("ho_so", {"cac_nhiem_vu": ["so_sanh"]}),  # so sánh mà không có hai trường
        ("ho_so", {"cac_nhiem_vu": ["so_sanh"], "cac_truong": ["BU"]}),  # chỉ một trường
        ("ho_so", {"nhiem_vu": "so_sanh"}),  # ho_so dùng cac_nhiem_vu, không dùng nhiem_vu
        ("trung_tam", {"nhiem_vu": "tim_kiem"}),  # nhiệm vụ của kiến thức
        ("trung_tam", {"nhiem_vu": "so_sanh"}),  # so sánh mà không có tên trung tâm
        ("trung_tam", {"nhiem_vu": "so_sanh", "cac_trung_tam": ["PEC"]}),  # chỉ một trung tâm
        (
            "trung_tam",
            {"nhiem_vu": "tim", "cac_trung_tam": ["PEC", "Kosam"]},
        ),  # danh sách trung tâm chỉ đi với so_sanh
        ("kich_ban", {"che_do": "goal_seeking", "nhiem_vu": "tim"}),  # kịch bản không có nhiem_vu
    ],
)
def test_the_new_task_keys_that_break_the_rules_are_rejected(kind, entities):
    with pytest.raises(ValidationError):
        y(kind, **entities)


def test_centers_and_knowledge_both_have_a_compare_task_without_mixing_their_rules():
    y("trung_tam", nhiem_vu="so_sanh", cac_trung_tam=["PEC", "Kosam"])  # không đòi cac_truong
    with pytest.raises(ValidationError):
        y("kien_thuc", nhiem_vu="so_sanh", cac_trung_tam=["PEC", "Kosam"])  # kiến thức vẫn đòi cac_truong


def test_each_branch_of_the_json_schema_lists_the_tasks_of_its_own_type():
    props = {kind: branch["properties"]["thuc_the"]["properties"] for kind, branch in branches().items()}
    assert props["mac_dinh"]["nhiem_vu"]["enum"] == list(PROFILE_TASKS)
    assert props["trung_tam"]["nhiem_vu"]["enum"] == list(CENTER_TASKS)
    assert props["ho_so"]["cac_nhiem_vu"]["items"]["enum"] == list(REVIEW_TASKS)
    assert props["ho_so"]["cac_nhiem_vu"]["uniqueItems"] is True
    assert props["trung_tam"]["cac_trung_tam"]["minItems"] == 2
    assert "ve_he_thong" in props["ngoai_pham_vi"]["loai_con"]["enum"]


def test_merging_two_profile_review_intents_does_not_repeat_a_task():
    merged = chuan_hoa_y(
        [
            y("ho_so", "đánh giá", cac_nhiem_vu=["danh_gia"]),
            y("ho_so", "đánh giá và lộ trình", cac_nhiem_vu=["danh_gia", "lo_trinh"]),
        ]
    )

    assert merged[0].thuc_the["cac_nhiem_vu"] == ["danh_gia", "lo_trinh"]


# ---- gộp và sắp xếp ý ----
def types(items: list[SlmY]) -> list[str]:
    return [i.type for i in items]


def test_profile_intents_merge_into_one_and_come_first():
    items = chuan_hoa_y(
        [
            y("kien_thuc", "I-20 là gì"),
            y("mac_dinh", "GPA 3.5", gpa="3.5"),
            y("mac_dinh", "SAT 1400", sat="1400"),
        ]
    )

    assert types(items) == ["mac_dinh", "kien_thuc"]
    assert items[0].doan == "GPA 3.5, SAT 1400" and items[0].thuc_the == {"gpa": "3.5", "sat": "1400"}


def test_a_later_profile_value_replaces_an_earlier_one():
    items = chuan_hoa_y([y("mac_dinh", "GPA 3.5", gpa="3.5"), y("mac_dinh", "à nhầm 3.6", gpa="3.6")])

    assert items[0].thuc_the == {"gpa": "3.6"}


def test_advice_questions_merge_into_one_intent_with_all_the_text():
    items = chuan_hoa_y([y("ho_so", "đánh giá hồ sơ"), y("kien_thuc", "OPT là gì"), y("ho_so", "nên làm gì")])

    assert types(items) == ["ho_so", "kien_thuc"]
    assert items[0].doan == "đánh giá hồ sơ, nên làm gì"


def test_lists_in_merged_entities_are_joined_not_replaced():
    first = y("kich_ban", "nếu SAT 1400", che_do="what_if", thay_doi=[{"yeu_to": "sat", "gia_tri": "1400"}])
    second = y("kich_ban", "và IELTS 7", che_do="what_if", thay_doi=[{"yeu_to": "english", "gia_tri": "7"}])

    items = chuan_hoa_y([first, second])

    assert [c["yeu_to"] for c in items[0].thuc_the["thay_doi"]] == ["sat", "english"]


def test_knowledge_and_center_intents_are_never_merged_because_each_is_its_own_lookup():
    knowledge = chuan_hoa_y([y("kien_thuc", "I-20 là gì"), y("kien_thuc", "SAT là gì")])
    centers = chuan_hoa_y([y("trung_tam", "ai sửa luận"), y("trung_tam", "ai luyện thi")])

    assert [i.doan for i in knowledge] == ["I-20 là gì", "SAT là gì"]
    assert [i.doan for i in centers] == ["ai sửa luận", "ai luyện thi"]


def test_the_order_the_student_asked_is_kept_apart_from_the_profile_intent():
    items = chuan_hoa_y([y("trung_tam", "a"), y("kien_thuc", "b"), y("ho_so", "c")])

    assert [i.doan for i in items] == ["a", "b", "c"]  # không xếp lại theo loại


def test_a_greeting_next_to_a_real_intent_is_dropped():
    items = chuan_hoa_y([y("ngoai_pham_vi", "chào", loai_con="tam_biet"), y("kien_thuc", "I-20 là gì")])

    assert types(items) == ["kien_thuc"]


def test_several_out_of_scope_intents_keep_only_the_first():
    items = chuan_hoa_y(
        [y("ngoai_pham_vi", "chào", loai_con="tam_biet"), y("ngoai_pham_vi", "ừ", loai_con="mo_ho")]
    )

    assert [i.doan for i in items] == ["chào"]


def test_every_question_is_kept_in_order_however_many_there_are():
    asked = [y("mac_dinh", "GPA", gpa="3.5")] + [y("kien_thuc", f"câu {n}") for n in range(1, 13)]

    items = chuan_hoa_y(asked)

    assert [i.doan for i in items] == ["GPA"] + [f"câu {n}" for n in range(1, 13)]


def test_only_the_technical_ceiling_cuts_intents_and_the_profile_intent_is_never_cut():
    asked = [y("mac_dinh", "GPA", gpa="3.5")] + [y("kien_thuc", f"câu {n}") for n in range(1, MAX_RAW_Y + 6)]

    items = chuan_hoa_y(asked)

    assert len(items) == MAX_RAW_Y and items[0].type == "mac_dinh"
    assert items[-1].doan == f"câu {MAX_RAW_Y - 1}"


def test_the_technical_ceiling_is_far_above_any_real_question_list():
    assert MAX_RAW_Y >= 12


# ---- cả tin ----
@pytest.mark.parametrize(
    ("items", "message"),
    [
        ([y("ngoai_pham_vi", "chào", loai_con="tam_biet"), y("kien_thuc")], "đứng một mình"),
        ([y("ho_so"), y("ho_so")], "chỉ được một ý"),
        ([y("mac_dinh"), y("mac_dinh")], "chỉ được một ý"),
        ([y("kien_thuc")] * (MAX_RAW_Y + 1), "quá"),
        ([y("kien_thuc"), y("mac_dinh")], "đứng đầu"),
    ],
)
def test_a_message_that_was_not_cleaned_up_is_rejected(items, message):
    with pytest.raises(ValidationError, match=message):
        SlmTin(cac_y=items)


def test_a_message_with_many_questions_is_accepted():
    assert len(SlmTin(cac_y=[y("kien_thuc")] * 12).cac_y) == 12


def test_a_message_needs_at_least_one_intent():
    with pytest.raises(ValidationError):
        SlmTin(cac_y=[])


# ---- kết quả cả tin ----
def test_from_raw_cleans_up_what_the_model_returned():
    result = SlmTinResult.from_raw(
        raw(y("kien_thuc", "I-20 là gì"), y("mac_dinh", "em GPA 3.5", gpa="em GPA 3.5")), confidence=0.9
    )

    assert result.types() == ["mac_dinh", "kien_thuc"] and result.confidence == 0.9


@pytest.mark.parametrize(
    "bad", [{}, {"cac_y": []}, {"cac_y": "x"}, {"cac_y": ["x"]}, {"cac_y": [{"type": "la"}]}]
)
def test_from_raw_rejects_a_wrong_shape_with_a_value_error(bad):
    with pytest.raises(ValueError):
        SlmTinResult.from_raw(bad)


@pytest.mark.parametrize("value", [-0.1, 1.2])
def test_confidence_must_be_between_0_and_1(value):
    with pytest.raises(ValidationError):
        SlmTinResult(cac_y=[y("kien_thuc")], confidence=value)


def test_profile_values_are_gathered_from_every_intent_and_the_later_one_wins():
    result = SlmTinResult(cac_y=[y("mac_dinh", "a", gpa="3.5", sat="1300"), y("ho_so", "b", sat="1400")])

    assert result.profile_values() == {"gpa": "3.5", "sat": "1400"}


@pytest.mark.parametrize(
    ("items", "expected"),
    [
        ([y("mac_dinh")], True),  # trả lời câu hồ sơ đang chờ (kể cả "ừ" mơ hồ, nhánh hồ sơ hỏi lại)
        ([y("kien_thuc", gpa="GPA em 8.5")], True),  # hỏi kèm giá trị hồ sơ
        ([y("kien_thuc", truong="Iowa")], False),
        ([y("ngoai_pham_vi", loai_con="tam_biet")], False),
    ],
)
def test_the_profile_branch_runs_for_profile_answers_or_messages_that_carry_profile_values(items, expected):
    assert SlmTinResult(cac_y=items).touches_profile() is expected


# ---- khuôn JSON ép SLM ----
def branches() -> dict:
    return {
        b["properties"]["type"]["const"]: b for b in SLM_TIN_SCHEMA["properties"]["cac_y"]["items"]["anyOf"]
    }


def test_the_json_schema_has_one_branch_per_type_and_a_list_of_bounded_size():
    cac_y = SLM_TIN_SCHEMA["properties"]["cac_y"]

    assert set(branches()) == set(INTENTS) == set(get_args(Intent))
    assert (cac_y["minItems"], cac_y["maxItems"]) == (1, MAX_RAW_Y)
    assert SLM_TIN_SCHEMA["required"] == ["cac_y"]


# Khuôn ép SLM và class đọc kết quả phải cùng tên khóa, nếu lệch thì mọi câu trả lời của model đều bị từ chối
def test_every_branch_has_the_same_keys_as_the_class_that_reads_it():
    for branch in branches().values():
        assert set(branch["required"]) == set(branch["properties"]) == set(SlmY.model_fields)


def test_a_branch_only_allows_the_keys_of_its_own_type_and_the_profile():
    for kind, branch in branches().items():
        keys = set(branch["properties"]["thuc_the"]["properties"])
        assert keys == ENTITY_KEYS[kind] | set(PROFILE_KEYS), kind
    assert "dich_vu" not in branches()["kich_ban"]["properties"]["thuc_the"]["properties"]


def test_the_schema_requires_what_the_contract_requires_and_knows_the_services():
    assert branches()["ngoai_pham_vi"]["properties"]["thuc_the"]["required"] == ["loai_con"]
    assert branches()["kich_ban"]["properties"]["thuc_the"]["required"] == ["che_do"]
    assert branches()["kien_thuc"]["properties"]["thuc_the"]["required"] == []
    assert branches()["trung_tam"]["properties"]["thuc_the"]["properties"]["dich_vu"]["enum"] == list(
        SERVICES
    )


def test_profile_keys_are_the_same_as_the_ones_the_chat_saves():
    assert PROFILE_KEYS == info_to_field.PROFILE_KEYS


# ---- skill cá nhân hóa (SLM lần 2) ----
def request(**overrides) -> SkillRequest:
    base = {
        "loai": "ho_so",
        "cau_hoi": "em nên chọn trường nào",
        "ho_so": {"gpa": "9 thang 10", "sat": "1450"},
    }
    return SkillRequest.model_validate(base | overrides)


def skill(extra: str = "") -> str:
    return "\n".join(f"## {name}\nGPA 9/10, SAT 1450 {extra}" for name in SKILL_SECTIONS["ho_so"])


@pytest.mark.parametrize(
    ("overrides", "message"),
    [
        ({"ho_so": {}}, "hồ sơ rỗng"),
        ({"ho_so": {"mau_sac": "đỏ"}}, "khóa hồ sơ lạ"),
        ({"tinh_san": {"mau_sac": "đỏ"}}, "khóa tính sẵn lạ"),
        (
            {"loai": "kich_ban", "muc_tieu": "Vừa sức", "tinh_san": {"timeline": "x"}},
            "chỉ skill ho_so có tinh_san",
        ),
        ({"loai": "kich_ban"}, "cần thay_doi hoặc muc_tieu"),
        ({"cau_hoi": ""}, ""),
        ({"loai": "kien_thuc"}, ""),
    ],
)
def test_a_skill_request_must_be_complete(overrides, message):
    with pytest.raises(ValidationError, match=message or None):
        request(**overrides)


def test_a_scenario_request_is_complete_with_a_change_or_a_goal():
    assert request(loai="kich_ban", muc_tieu="Vừa sức").muc_tieu == "Vừa sức"
    assert request(loai="kich_ban", thay_doi=[{"yeu_to": "sat", "gia_tri": "1500"}]).thay_doi


def test_the_skill_key_is_stable_and_changes_with_any_value():
    same = request(ho_so={"sat": "1450", "gpa": "9 thang 10"})  # khác thứ tự khóa nhưng cùng nội dung

    assert skill_key(request()) == skill_key(same)
    assert skill_key(request()) != skill_key(request(ho_so={"gpa": "9 thang 10", "sat": "1460"}))
    assert skill_key(request()) != skill_key(request(cau_hoi="em hợp trường nào"))
    assert len(skill_key(request())) == 16


def test_a_complete_skill_passes_the_check():
    check_skill(skill(), request())


def test_extra_sections_are_ignored_but_a_missing_or_empty_one_is_not():
    check_skill(skill() + "\n## 8. Ví dụ cách viết\nGPA 9/10", request())

    with pytest.raises(ValueError, match="đủ các mục"):
        check_skill(skill().replace("## 5. Đề xuất trường", "## 5x"), request())
    with pytest.raises(ValueError, match="mục rỗng"):
        check_skill(skill().replace("## 6. Lộ trình\nGPA 9/10, SAT 1450 ", "## 6. Lộ trình\n"), request())


def test_a_skill_that_drops_a_number_of_the_profile_is_rejected():
    with pytest.raises(ValueError, match="bỏ mất số"):
        check_skill(skill().replace("1450", "1400"), request())


def test_a_skill_that_drops_a_number_of_the_calculated_lines_is_rejected():
    req = request(tinh_san={"gpa_level": "3.6 thang 4, trung bình (TB các trường 3.6)"})

    check_skill(skill("GPA 3.6 thang 4, TB các trường 3.6"), req)
    with pytest.raises(ValueError, match="bỏ mất số"):
        check_skill(skill(), req)


def test_numbers_are_compared_the_way_a_student_writes_them():
    profile = request(ho_so={"budget": "khoảng 40.000 USD", "english": "IELTS 6.", "gpa": "9 thang 10"})
    body = "\n".join(f"## {name}\n40000 USD, IELTS 6.0, GPA 9/10" for name in SKILL_SECTIONS["ho_so"])

    check_skill(body, profile)  # 40.000 và 40000 là một số, 6 và 6.0 là một số


def test_list_numbers_at_the_start_of_a_line_are_not_counted_as_profile_numbers():
    body = skill("\n1. Giữ GPA\n2. Thi lại SAT")  # "1." và "2." chỉ là số thứ tự

    check_skill(body, request())


def test_each_type_of_skill_has_its_own_sections():
    assert len(SKILL_SECTIONS["ho_so"]) == 8 and len(SKILL_SECTIONS["kich_ban"]) == 5
    assert SKILL_SECTIONS["ho_so"][0] == SKILL_SECTIONS["kich_ban"][0] == "0. Câu hỏi kích hoạt"


# ---- khớp với dữ liệu ----
# Đầu ra trong dữ liệu chính là thứ SLM học để trả, nên dòng nào cũng phải đọc được bằng hợp đồng
def test_every_labelled_output_in_the_dataset_is_a_valid_answer():
    for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        SlmTinResult.from_raw(record["output"])


# Mỗi ý trong dữ liệu phải nằm gọn trong nhánh của loại nó ở khuôn SLM_TIN_SCHEMA, nếu không model bị khuôn chặn không học được
def test_every_labelled_intent_fits_the_branch_of_its_type():
    for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines():
        for item in json.loads(line)["output"]["cac_y"]:
            thuc_the = branches()[item["type"]]["properties"]["thuc_the"]
            assert set(item["thuc_the"]) <= set(thuc_the["properties"]), item
            assert set(thuc_the["required"]) <= set(item["thuc_the"]), item


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmTinResult(cac_y=[y("mac_dinh")], confidence=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.types(), result.confidence) == (["mac_dinh"], 0.9)
