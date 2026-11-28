"""Hợp đồng SLM mở rộng 28/11: H1 so sánh không đủ tên trường, H2 cộng / trừ ngân sách, H3 unsupported,
H4 xếp theo học phí, H9 hỏi cách tính điểm phù hợp. Mọi phần mới đều không bắt buộc nên đầu ra cũ vẫn hợp lệ."""

import pytest

from app.schemas.slm import SLM_TIN_SCHEMA, SlmY
from app.services.scenario import AskAgain, parse_what_if


def y(kind: str, **entities) -> SlmY:
    return SlmY(type=kind, span="x", entities=entities)


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        # H1
        ("knowledge", {"tasks": ["compare_facts"], "schools": ["2 trường đầu"], "fields": ["cost"]}),
        ("knowledge", {"tasks": ["compare_facts"], "fields": ["aid"], "follow_up": True}),
        ("knowledge", {"tasks": ["compare_facts"]}),
        ("profile_review", {"tasks": ["compare"], "follow_up": True}),
        ("profile_review", {"tasks": ["compare"], "schools": ["hai trường cuối"]}),
        # H2
        (
            "scenario",
            {"mode": "what_if", "changes": [{"factor": "budget", "value": "5000", "action": "add"}]},
        ),
        (
            "scenario",
            {"mode": "what_if", "changes": [{"factor": "budget", "value": "5000", "action": "subtract"}]},
        ),
        # H3
        ("knowledge", {"tasks": ["search"], "filters": {"major": "kế toán"}, "unsupported": ["gần biển"]}),
        ("profile_review", {"tasks": ["school_list"], "unsupported": ["ở Texas"]}),
        ("scenario", {"mode": "goal_seeking", "school": "Purdue", "unsupported": ["có học bổng"]}),
        # H4
        ("knowledge", {"tasks": ["search"], "sort": "lowest_tuition"}),
        ("knowledge", {"tasks": ["search"], "sort": "highest_tuition"}),
        # H9
        ("knowledge", {"tasks": ["fit_method"]}),
    ],
)
def test_the_new_labels_are_accepted(kind, entities):
    assert y(kind, **entities).entities == entities


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        # H2: add / subtract chỉ cho ngân sách và phải có số
        ("scenario", {"mode": "what_if", "changes": [{"factor": "sat", "value": "50", "action": "add"}]}),
        ("scenario", {"mode": "what_if", "changes": [{"factor": "budget", "action": "add"}]}),
        # H3: đúng chỗ và là danh sách chữ
        ("knowledge", {"unsupported": ["gần biển"]}),
        ("profile_review", {"tasks": ["assessment"], "unsupported": ["ở Texas"]}),
        ("knowledge", {"tasks": ["search"], "sort": "ranking", "unsupported": []}),
        ("knowledge", {"tasks": ["search"], "sort": "ranking", "unsupported": "gần biển"}),
        # search chỉ có unsupported thì không còn gì để lọc: phải là câu kiến thức thường
        ("knowledge", {"tasks": ["search"], "unsupported": ["gần biển"]}),
        # H9: hỏi cách tính nói chung, không về một trường
        ("knowledge", {"tasks": ["fit_method"], "schools": ["Purdue"]}),
        ("profile_review", {"tasks": ["fit_method"]}),
        # H1: đã ghi schools thì phải là danh sách tên
        ("knowledge", {"tasks": ["compare_facts"], "schools": []}),
    ],
)
def test_wrong_use_of_the_new_labels_is_rejected(kind, entities):
    with pytest.raises(ValueError):
        y(kind, **entities)


def test_the_output_schema_knows_the_new_keys():
    branches = {
        b["properties"]["type"]["const"]: b["properties"]["entities"]["properties"]
        for b in SLM_TIN_SCHEMA["properties"]["intents"]["items"]["anyOf"]
    }

    for kind in ("knowledge", "profile_review", "scenario"):
        assert "unsupported" in branches[kind], kind
    assert {"lowest_tuition", "highest_tuition"} <= set(branches["knowledge"]["sort"]["enum"])


# Mô phỏng chưa cộng trừ được: không được hiểu "thêm 5000" thành tổng ngân sách 5000
@pytest.mark.parametrize("action", ["add", "subtract"])
def test_a_budget_increase_is_never_read_as_the_new_total(action):
    with pytest.raises(AskAgain, match="tổng"):
        parse_what_if([{"factor": "budget", "value": "5000", "unit": "USD", "action": action}])
