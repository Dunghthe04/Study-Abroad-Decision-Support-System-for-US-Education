"""Hợp đồng SLM cho câu tìm trường theo điều kiện: nhiem_vu = tim_kiem, bo_loc, sap_xep (kien_thuc)."""

import pytest

from app.schemas.slm import FILTER_ENUMS, FILTER_KEYS, FILTER_TEXT_KEYS, SORTS, SlmY, check_entities


def test_a_search_carries_the_filters_the_student_said():
    y = SlmY(
        type="kien_thuc",
        doan="Trường nào ở California có học bổng cho du học sinh",
        thuc_the={"nhiem_vu": "tim_kiem", "bo_loc": {"bang": ["California"], "hoc_bong": "co"}},
    )

    assert y.thuc_the["bo_loc"] == {"bang": ["California"], "hoc_bong": "co"}


def test_many_filters_and_a_sort_order_together():
    check_entities(
        "kien_thuc",
        {
            "nhiem_vu": "tim_kiem",
            "bo_loc": {
                "bang": ["texas", "California"],
                "loai_truong": "cong_lap",
                "chi_phi_toi_da": "30 nghìn đô",
                "nganh": "khoa học máy tính",
                "chinh_sach_thi": "khong_bat_buoc",
                "ielts_toi_da": "6.5",
                "xep_hang_toi_da": "top 50",
                "ivy": "co",
            },
            "sap_xep": "chi_phi_thap",
        },
    )


@pytest.mark.parametrize("key", FILTER_TEXT_KEYS)
def test_every_text_filter_is_accepted(key):
    check_entities("kien_thuc", {"nhiem_vu": "tim_kiem", "bo_loc": {key: "chữ học sinh gõ"}})


@pytest.mark.parametrize(("key", "values"), FILTER_ENUMS.items())
def test_every_enum_filter_value_is_accepted_and_others_are_not(key, values):
    for value in values:
        check_entities("kien_thuc", {"nhiem_vu": "tim_kiem", "bo_loc": {key: value}})
    with pytest.raises(ValueError):
        check_entities("kien_thuc", {"nhiem_vu": "tim_kiem", "bo_loc": {key: "khong_hop_le"}})


@pytest.mark.parametrize("sort", SORTS)
def test_every_sort_order_is_accepted(sort):
    check_entities("kien_thuc", {"nhiem_vu": "tim_kiem", "bo_loc": {"bang": ["Texas"]}, "sap_xep": sort})


@pytest.mark.parametrize(
    "entities",
    [
        {"nhiem_vu": "tim_kiem"},  # tìm mà không có điều kiện nào
        {"nhiem_vu": "tim_kiem", "bo_loc": {}},
        {"nhiem_vu": "tim_kiem", "bo_loc": {"sao_hoa": "co"}},  # điều kiện lạ
        {"nhiem_vu": "tim_kiem", "bo_loc": {"bang": "California"}},  # bang phải là danh sách
        {"nhiem_vu": "tim_kiem", "bo_loc": {"bang": []}},
        {"nhiem_vu": "tim_kiem", "bo_loc": {"chi_phi_toi_da": ""}},  # chữ rỗng
        {"nhiem_vu": "tim_kiem", "bo_loc": {"loai_truong": "quoc_te"}},
        {"nhiem_vu": "tim_kiem", "bo_loc": {"bang": ["Texas"]}, "sap_xep": "ngau_nhien"},
        {"bo_loc": {"bang": ["Texas"]}},  # bo_loc mà không phải tim_kiem
        {"nhiem_vu": "tra_cuu", "sap_xep": "xep_hang"},
    ],
)
def test_a_malformed_search_is_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("kien_thuc", entities)


def test_filters_belong_to_knowledge_only():
    with pytest.raises(ValueError):
        check_entities("ho_so", {"nhiem_vu": "tim_kiem", "bo_loc": {"bang": ["Texas"]}})


def test_the_filter_key_list_is_the_one_in_the_contract():
    assert FILTER_KEYS[0] == "bang" and len(FILTER_KEYS) == len(set(FILTER_KEYS)) == 15
