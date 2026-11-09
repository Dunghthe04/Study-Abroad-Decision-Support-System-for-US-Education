"""Hợp đồng SLM cho câu hỏi số liệu một trường: truong, nhiem_vu, muc (kien_thuc)."""

import pytest

from app.schemas.slm import INFO_FIELDS, KNOWLEDGE_TASKS, SlmY, check_entities


def test_a_knowledge_intent_may_carry_the_school_the_task_and_the_fields():
    y = SlmY(
        type="kien_thuc",
        doan="học phí và học bổng của Harvard",
        thuc_the={"truong": "Harvard", "nhiem_vu": "tra_cuu", "muc": ["chi_phi", "hoc_bong"]},
    )

    assert y.thuc_the["muc"] == ["chi_phi", "hoc_bong"]


@pytest.mark.parametrize("field", INFO_FIELDS)
def test_every_known_field_code_is_accepted(field):
    check_entities("kien_thuc", {"muc": [field]})


@pytest.mark.parametrize("task", KNOWLEDGE_TASKS)
def test_every_known_task_is_accepted(task):
    entities = {"nhiem_vu": task}
    if task == "so_sanh":
        entities["cac_truong"] = ["BU", "NYU"]  # so sánh bắt buộc có các trường
    if task == "tim_kiem":
        entities["bo_loc"] = {"bang": ["Texas"]}  # tìm trường bắt buộc có điều kiện
    check_entities("kien_thuc", entities)


@pytest.mark.parametrize(
    "entities",
    [
        {"muc": ["ky_tuc_xa"]},  # mã lạ
        {"muc": []},  # rỗng
        {"muc": "chi_phi"},  # không phải danh sách
        {"nhiem_vu": "doan_mo"},
    ],
)
def test_unknown_or_malformed_values_are_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("kien_thuc", entities)


def test_the_fields_belong_to_knowledge_only():
    with pytest.raises(ValueError):
        check_entities("ho_so", {"muc": ["chi_phi"]})


# ---- so sánh hai trường trở lên (nhiem_vu = so_sanh, cac_truong)


def test_a_comparison_carries_two_or_more_schools_in_the_order_said():
    y = SlmY(
        type="kien_thuc",
        doan="học phí BU và NYU chênh nhau bao nhiêu",
        thuc_the={"cac_truong": ["BU", "NYU"], "nhiem_vu": "so_sanh", "muc": ["chi_phi"]},
    )

    assert y.thuc_the["cac_truong"] == ["BU", "NYU"]


@pytest.mark.parametrize(
    "entities",
    [
        {"cac_truong": ["BU"]},  # một trường không phải so sánh
        {"cac_truong": []},
        {"cac_truong": "BU, NYU"},  # không phải danh sách
        {"cac_truong": ["BU", ""]},  # tên rỗng
        {"nhiem_vu": "so_sanh", "muc": ["chi_phi"]},  # so sánh mà không có các trường
    ],
)
def test_a_malformed_comparison_is_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("kien_thuc", entities)


def test_the_schools_list_belongs_to_knowledge_and_profile_review_only():
    check_entities("ho_so", {"cac_truong": ["BU", "NYU"]})  # so sánh hai trường theo hồ sơ
    for kind in ("mac_dinh", "trung_tam", "kich_ban", "ngoai_pham_vi"):
        with pytest.raises(ValueError):
            check_entities(kind, {"cac_truong": ["BU", "NYU"]})
