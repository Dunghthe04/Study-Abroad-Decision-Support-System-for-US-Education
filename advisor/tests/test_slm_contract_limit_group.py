"""Hợp đồng chốt 28/11 (rà toàn bộ câu học sinh đã gửi): H5 max_tuition, H7 group khi xin gợi ý, H8 limit,
và unsupported dùng được ở mọi kịch bản và ở trung tâm. Phần chưa làm được trong chat thì không được bỏ lặng lẽ."""

import pytest

from app.schemas.slm import SLM_TIN_SCHEMA, SlmY
from app.services import search_filters


def y(kind: str, **entities) -> SlmY:
    return SlmY(type=kind, span="x", entities=entities)


SEARCH = {"tasks": ["search"]}
LIST = {"tasks": ["school_list"]}


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        # H8: số trường học sinh xin
        ("knowledge", {**SEARCH, "filters": {"state": "Texas"}, "sort": "ranking", "limit": "3"}),
        ("knowledge", {**SEARCH, "sort": "most_selective", "limit": "10"}),
        ("profile_review", {**LIST, "limit": "5"}),
        # H7: nhóm khi xin gợi ý theo hồ sơ
        ("profile_review", {**LIST, "group": "safety"}),
        ("profile_review", {**LIST, "group": "reach", "limit": "3"}),
        # H5: mức học phí tối đa, khác tổng chi phí
        ("knowledge", {**SEARCH, "filters": {"max_tuition": "35 nghìn đô"}}),
        # unsupported ở mọi kịch bản và ở trung tâm
        (
            "scenario",
            {
                "mode": "what_if",
                "changes": [{"factor": "priority_aid", "value": "4"}],
                "unsupported": ["thực tập tốt"],
            },
        ),
        ("center_referral", {"unsupported": ["mở cuối tuần"]}),
    ],
)
def test_the_new_labels_are_accepted(kind, entities):
    assert y(kind, **entities).entities == entities


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        ("knowledge", {**SEARCH, "sort": "ranking", "limit": 3}),  # limit là chuỗi số như value của kịch bản
        ("knowledge", {**SEARCH, "sort": "ranking", "limit": "ba"}),
        ("knowledge", {**SEARCH, "sort": "ranking", "limit": "0"}),
        ("knowledge", {"tasks": ["school_info"], "schools": ["BU"], "limit": "3"}),
        ("profile_review", {"tasks": ["assessment"], "limit": "3"}),
        ("profile_review", {"tasks": ["assessment"], "group": "safety"}),
        ("profile_review", {**LIST, "group": "aid"}),  # nhóm cần học bổng lớn chưa có
        ("scenario", {"mode": "what_if", "limit": "3"}),
    ],
)
def test_wrong_use_is_rejected(kind, entities):
    with pytest.raises(ValueError):
        y(kind, **entities)


def test_the_output_schema_knows_the_new_keys():
    branches = {
        b["properties"]["type"]["const"]: b["properties"]["entities"]["properties"]
        for b in SLM_TIN_SCHEMA["properties"]["intents"]["items"]["anyOf"]
    }

    assert "limit" in branches["knowledge"] and "limit" in branches["profile_review"]
    assert "group" in branches["profile_review"]
    assert "max_tuition" in branches["knowledge"]["filters"]["properties"]
    assert "unsupported" in branches["center_referral"] and "unsupported" in branches["scenario"]


# Backend chưa lọc theo học phí: phải hỏi lại, không được trả danh sách như thể đã lọc
@pytest.mark.parametrize("sort", [None, "lowest_tuition"])
async def test_a_tuition_limit_is_reported_and_never_silently_dropped(sort):
    plan = await search_filters.normalize({"max_tuition": "35 nghìn đô"}, sort)

    assert "maxCostUsd" not in plan.filters
    assert plan.unresolved and "học phí" in plan.unresolved[0] and "35 nghìn đô" in plan.unresolved[0]
