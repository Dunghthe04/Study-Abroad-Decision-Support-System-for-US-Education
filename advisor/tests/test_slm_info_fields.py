"""Hợp đồng SLM cho câu hỏi số liệu một trường: schools, tasks, fields (knowledge)."""

import pytest

from app.schemas.slm import INFO_FIELDS, KNOWLEDGE_TASKS, SlmY, check_entities


def test_a_knowledge_intent_may_carry_the_school_the_task_and_the_fields():
    y = SlmY(
        type="knowledge",
        span="học phí và học bổng của Harvard",
        entities={"schools": ["Harvard"], "tasks": ["school_info"], "fields": ["cost", "aid"]},
    )

    assert y.entities["fields"] == ["cost", "aid"]


@pytest.mark.parametrize("field", INFO_FIELDS)
def test_every_known_field_code_is_accepted(field):
    check_entities("knowledge", {"fields": [field]})


@pytest.mark.parametrize("task", KNOWLEDGE_TASKS)
def test_every_known_task_is_accepted(task):
    entities = {"tasks": [task]}
    if task == "compare_facts":
        entities["schools"] = ["BU", "NYU"]  # so sánh bắt buộc có các trường
    if task == "search":
        entities["filters"] = {"state": ["Texas"]}  # tìm trường bắt buộc có điều kiện
    check_entities("knowledge", entities)


@pytest.mark.parametrize(
    "entities",
    [
        {"fields": ["ky_tuc_xa"]},  # mã lạ
        {"fields": ["muc"]},
        {"fields": ["chi_phi"]},  # mã tiếng Việt cũ
        {"fields": []},  # rỗng
        {"fields": "cost"},  # không phải danh sách
        {"tasks": ["doan_mo"]},
        {"tasks": ["tra_cuu"]},  # mã tiếng Việt cũ
        {"tasks": "school_info"},  # không phải danh sách
        {"tasks": ["school_info", "search"]},  # kiến thức chỉ một nhiệm vụ mỗi ý
    ],
)
def test_unknown_or_malformed_values_are_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("knowledge", entities)


def test_the_fields_belong_to_knowledge_only():
    with pytest.raises(ValueError):
        check_entities("profile_review", {"fields": ["cost"]})


# ---- so sánh hai trường trở lên (tasks = ["compare_facts"], schools)


def test_a_comparison_carries_two_or_more_schools_in_the_order_said():
    y = SlmY(
        type="knowledge",
        span="học phí BU và NYU chênh nhau bao nhiêu",
        entities={"schools": ["BU", "NYU"], "tasks": ["compare_facts"], "fields": ["cost"]},
    )

    assert y.entities["schools"] == ["BU", "NYU"]


@pytest.mark.parametrize(
    "entities",
    [
        {"schools": []},
        {"schools": "BU, NYU"},  # không phải danh sách
        {"schools": ["BU", ""]},  # tên rỗng
        {"schools": ["BU", 7]},  # tên không phải chữ
        {"tasks": ["compare_facts"], "schools": ["BU"]},  # so sánh mà chỉ một trường
        {"tasks": ["compare_facts"], "fields": ["cost"]},  # so sánh mà không có các trường
    ],
)
def test_a_malformed_comparison_is_rejected(entities):
    with pytest.raises(ValueError):
        check_entities("knowledge", entities)


def test_the_schools_list_belongs_to_knowledge_and_profile_review_only():
    check_entities(
        "profile_review", {"tasks": ["compare"], "schools": ["BU", "NYU"]}
    )  # so sánh hai trường theo hồ sơ
    for kind in ("collect_profile", "center_referral", "scenario", "out_of_scope"):
        with pytest.raises(ValueError):
            check_entities(kind, {"schools": ["BU", "NYU"]})
