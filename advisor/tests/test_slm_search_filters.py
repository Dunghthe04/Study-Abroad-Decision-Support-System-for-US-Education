"""Hợp đồng SLM cho câu tìm trường theo điều kiện: tasks = ["search"], filters, sort (knowledge)."""

import pytest

from app.schemas.slm import FILTER_ENUMS, FILTER_KEYS, FILTER_TEXT_KEYS, SORTS, SlmY, check_entities

SEARCH = ["search"]


def test_a_search_carries_the_filters_the_student_said():
    y = SlmY(
        type="knowledge",
        span="Trường nào ở California có học bổng cho du học sinh",
        entities={"tasks": SEARCH, "filters": {"state": ["California"], "aid": "yes"}},
    )

    assert y.entities["filters"] == {"state": ["California"], "aid": "yes"}


def test_many_filters_and_a_sort_order_together():
    check_entities(
        "knowledge",
        {
            "tasks": SEARCH,
            "filters": {
                "state": ["texas", "California"],
                "school_type": "public",
                "max_cost": "30 nghìn đô",
                "major": "khoa học máy tính",
                "test_policy": "optional",
                "max_ielts": "6.5",
                "max_rank": "top 50",
                "ivy": "yes",
            },
            "sort": "lowest_cost",
        },
    )


def test_state_may_be_one_name_or_a_list_of_names():
    check_entities("knowledge", {"tasks": SEARCH, "filters": {"state": "California"}})
    check_entities("knowledge", {"tasks": SEARCH, "filters": {"state": ["California", "Texas"]}})


@pytest.mark.parametrize("key", FILTER_TEXT_KEYS)
def test_every_text_filter_is_accepted(key):
    check_entities("knowledge", {"tasks": SEARCH, "filters": {key: "chữ học sinh gõ"}})


@pytest.mark.parametrize(("key", "values"), FILTER_ENUMS.items())
def test_every_enum_filter_value_is_accepted_and_others_are_not(key, values):
    for value in values:
        check_entities("knowledge", {"tasks": SEARCH, "filters": {key: value}})
    with pytest.raises(ValueError):
        check_entities("knowledge", {"tasks": SEARCH, "filters": {key: "khong_hop_le"}})


@pytest.mark.parametrize("sort", SORTS)
def test_every_sort_order_is_accepted(sort):
    check_entities("knowledge", {"tasks": SEARCH, "filters": {"state": ["Texas"]}, "sort": sort})


def test_a_follow_up_may_only_change_the_sort_because_the_filters_come_from_the_context():
    check_entities("knowledge", {"tasks": SEARCH, "sort": "lowest_cost", "follow_up": True})
    with pytest.raises(ValueError):
        check_entities("knowledge", {"tasks": SEARCH, "sort": "lowest_cost"})  # không phải câu tiếp nối


@pytest.mark.parametrize(
    "entities",
    [
        {"tasks": SEARCH},  # tìm mà không có điều kiện nào
        {"tasks": SEARCH, "filters": {}},
        {"tasks": SEARCH, "filters": {"sao_hoa": "yes"}},  # điều kiện lạ
        {"tasks": SEARCH, "filters": {"bang": ["Texas"]}},  # khóa tiếng Việt cũ
        {"tasks": SEARCH, "filters": {"state": []}},
        {"tasks": SEARCH, "filters": {"state": ["Texas", ""]}},
        {"tasks": SEARCH, "filters": {"max_cost": ""}},  # chữ rỗng
        {"tasks": SEARCH, "filters": {"school_type": "international"}},
        {"tasks": SEARCH, "filters": {"school_type": "cong_lap"}},  # giá trị tiếng Việt cũ
        {"tasks": SEARCH, "filters": {"state": ["Texas"]}, "sort": "random"},
        {"filters": {"state": ["Texas"]}},  # filters mà không phải search
        {"tasks": ["school_info"], "sort": "ranking"},
        {"tasks": "search", "filters": {"state": ["Texas"]}},  # tasks phải là danh sách
    ],
)
def test_a_malformed_search_is_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("knowledge", entities)


def test_filters_belong_to_knowledge_only():
    with pytest.raises(ValueError):
        check_entities("profile_review", {"tasks": SEARCH, "filters": {"state": ["Texas"]}})


def test_the_filter_key_list_is_the_one_in_the_contract():
    assert FILTER_KEYS[0] == "state" and len(FILTER_KEYS) == len(set(FILTER_KEYS)) == 15
