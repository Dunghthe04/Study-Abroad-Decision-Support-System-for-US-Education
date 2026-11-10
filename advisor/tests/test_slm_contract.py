"""Hợp đồng SLM (danh sách ý): một ý, gộp và cắt ý, kết quả cả tin, khuôn JSON, skill cá nhân hóa, và khớp với dữ liệu."""

import json
from pathlib import Path
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
    SERVICE_CODES,
    SLM_TIN_SCHEMA,
    Intent,
    SkillRequest,
    SlmTin,
    SlmTinResult,
    SlmY,
    chuan_hoa_y,
    single_school,
    task_of,
)
from app.services.skills import info_to_field
from app.services.slm_contract import FixedSlm


def y(kind: str, span: str = "x", **entities) -> SlmY:
    return SlmY(type=kind, span=span, entities=entities)


def raw(*items: SlmY) -> dict:
    return {"intents": [i.model_dump() for i in items]}


# ---- một ý ----
def test_a_typical_intent_is_read_with_defaults_for_the_rest():
    item = SlmY.model_validate({"type": "knowledge", "span": "I-20 là gì"})

    assert (item.type, item.span, item.entities) == ("knowledge", "I-20 là gì", {})


@pytest.mark.parametrize(
    "answer",
    [
        {"type": "nhu_cau", "span": "x"},  # loại lạ
        {"type": "kien_thuc", "span": "x"},  # tên loại cũ không còn
        {"type": "knowledge", "span": ""},  # đoạn chữ rỗng
        {"type": "knowledge"},  # thiếu đoạn chữ
        {"type": "knowledge", "span": "x", "entities": {"service": "ESSAY_EDITING"}},  # khóa của loại khác
        {"type": "center_referral", "span": "x", "entities": {"service": "LAM_VISA_HO"}},  # mã dịch vụ lạ
        {"type": "center_referral", "span": "x", "entities": {"service": "SUA_BAI_LUAN"}},  # mã dịch vụ cũ
        {"type": "scenario", "span": "x", "entities": {"mode": "what_if"}},  # what-if mà không nói đổi gì
        {"type": "scenario", "span": "x"},  # thiếu mode
        {
            "type": "scenario",
            "span": "x",
            "entities": {"mode": "goal_seeking", "target_group": "Vừa sức"},
        },  # nhóm lạ
        {"type": "out_of_scope", "span": "x"},  # thiếu subtype
        {"type": "out_of_scope", "span": "x", "entities": {"subtype": "la"}},  # subtype lạ
        {"type": "collect_profile", "span": "x", "entities": {"gpa": 8.5}},  # giá trị hồ sơ phải là chữ
        {"type": "collect_profile", "span": "x", "entities": {"mau_sac": "đỏ"}},  # khóa lạ
        {"type": "knowledge", "span": "x", "entities": {"schools": "Harvard"}},  # schools luôn là danh sách
    ],
)
def test_intents_that_break_the_rules_are_rejected(answer):
    with pytest.raises(ValidationError):
        SlmY.model_validate(answer)


def test_profile_values_of_an_intent_are_only_the_profile_keys():
    item = y("center_referral", service="TEST_PREP", english="IELTS 6.0")

    assert item.profile_values() == {"english": "IELTS 6.0"}


# ---- nhiệm vụ: nhập hồ sơ, tư vấn hồ sơ, trung tâm ----
@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        ("collect_profile", {"tasks": ["edit"], "sat": "SAT là 1400"}),  # sửa một mục đã nhập
        (
            "collect_profile",
            {"tasks": ["edit"], "sat": "1450", "follow_up": True},
        ),  # "à nhầm, 1450 mới đúng": mục lấy từ ngữ cảnh
        ("collect_profile", {"tasks": ["status"]}),
        ("collect_profile", {"tasks": ["restart"]}),
        ("collect_profile", {"gpa": "3.5"}),  # vắng tasks = trả lời câu đang chờ
        ("profile_review", {"tasks": ["assessment", "roadmap"]}),
        ("profile_review", {"tasks": []}),  # chưa rõ muốn gì: hệ thống hỏi lại bằng nút
        ("profile_review", {"tasks": ["explain_school"], "schools": ["Duke"], "follow_up": True}),
        ("profile_review", {"tasks": ["compare"], "schools": ["BU", "NYU"]}),
        ("center_referral", {"service": "ESSAY_EDITING", "city": "Hà Nội"}),  # vắng tasks = tìm trung tâm
        ("center_referral", {"tasks": ["booking"], "follow_up": True}),
        ("center_referral", {"tasks": ["compare"], "centers": ["PEC", "Kosam"]}),
        ("out_of_scope", {"subtype": "about_system"}),
        ("out_of_scope", {"subtype": "human_handoff"}),
    ],
)
def test_the_task_keys_are_accepted(kind, entities):
    assert y(kind, **entities).type == kind


@pytest.mark.parametrize(
    ("kind", "entities"),
    [
        ("collect_profile", {"tasks": ["edit"]}),  # sửa mà không nói mục nào
        ("collect_profile", {"tasks": ["status"], "gpa": "3.5"}),  # xem hồ sơ không kèm giá trị
        ("collect_profile", {"tasks": ["restart"], "gpa": "3.5"}),
        ("collect_profile", {"tasks": ["school_info"]}),  # nhiệm vụ của loại khác
        ("collect_profile", {"tasks": ["edit", "status"], "gpa": "3.5"}),  # loại này chỉ một nhiệm vụ mỗi ý
        ("collect_profile", {"follow_up": "true"}),  # phải là boolean
        ("profile_review", {"tasks": ["assessment", "assessment"]}),  # lặp
        ("profile_review", {"tasks": ["write_essay"]}),  # nhiệm vụ lạ
        ("profile_review", {"tasks": "assessment"}),  # phải là danh sách
        ("profile_review", {"tasks": ["compare"]}),  # so sánh mà không có hai trường
        ("profile_review", {"tasks": ["compare"], "schools": ["BU"]}),  # chỉ một trường
        ("profile_review", {"tasks": ["compare"], "schools": "BU"}),  # schools phải là danh sách
        ("center_referral", {"tasks": ["search"]}),  # nhiệm vụ của kiến thức
        ("center_referral", {"tasks": ["compare"]}),  # so sánh mà không có tên trung tâm
        ("center_referral", {"tasks": ["compare"], "centers": ["PEC"]}),  # chỉ một trung tâm
        (
            "center_referral",
            {"tasks": ["booking"], "centers": ["PEC", "Kosam"]},
        ),  # centers chỉ đi với compare
        ("center_referral", {"centers": ["PEC", "Kosam"]}),  # vắng tasks cũng không đi với centers
        (
            "center_referral",
            {"tasks": ["booking", "compare"], "centers": ["PEC", "Kosam"]},
        ),  # một nhiệm vụ mỗi ý
        ("scenario", {"mode": "goal_seeking", "tasks": ["search"]}),  # kịch bản không có tasks
    ],
)
def test_the_task_keys_that_break_the_rules_are_rejected(kind, entities):
    with pytest.raises(ValidationError):
        y(kind, **entities)


def test_centers_and_knowledge_both_have_a_compare_task_without_mixing_their_rules():
    y("center_referral", tasks=["compare"], centers=["PEC", "Kosam"])  # không đòi schools
    with pytest.raises(ValidationError):
        y("knowledge", tasks=["compare_facts"], centers=["PEC", "Kosam"])  # kiến thức không có centers
    with pytest.raises(ValidationError):
        y("knowledge", tasks=["compare_facts"], schools=["BU"])  # kiến thức đòi schools từ hai tên


def test_task_of_and_single_school_read_the_entities_the_way_the_branches_do():
    assert task_of({"tasks": ["school_info"]}) == "school_info"
    assert task_of({"tasks": []}) is None and task_of({}) is None
    assert single_school({"schools": [" Duke "]}) == "Duke"
    assert single_school({"schools": ["BU", "NYU"]}) is None
    assert single_school({"schools": ["BU"], "tasks": ["compare"]}) is None
    assert single_school({}) is None


def test_each_branch_of_the_json_schema_lists_the_tasks_of_its_own_type():
    props = {kind: branch["properties"]["entities"]["properties"] for kind, branch in branches().items()}
    assert props["collect_profile"]["tasks"]["items"]["enum"] == list(PROFILE_TASKS)
    assert props["center_referral"]["tasks"]["items"]["enum"] == list(CENTER_TASKS)
    assert props["profile_review"]["tasks"]["items"]["enum"] == list(REVIEW_TASKS)
    assert props["profile_review"]["tasks"]["uniqueItems"] is True
    assert props["collect_profile"]["tasks"]["maxItems"] == props["knowledge"]["tasks"]["maxItems"] == 1
    assert props["center_referral"]["centers"]["minItems"] == 2
    assert props["knowledge"]["schools"]["minItems"] == 1
    assert "about_system" in props["out_of_scope"]["subtype"]["enum"]


def test_merging_two_profile_review_intents_does_not_repeat_a_task():
    merged = chuan_hoa_y(
        [
            y("profile_review", "đánh giá", tasks=["assessment"]),
            y("profile_review", "đánh giá và lộ trình", tasks=["assessment", "roadmap"]),
        ]
    )

    assert merged[0].entities["tasks"] == ["assessment", "roadmap"]


# ---- gộp và sắp xếp ý ----
def types(items: list[SlmY]) -> list[str]:
    return [i.type for i in items]


def test_profile_intents_merge_into_one_and_come_first():
    items = chuan_hoa_y(
        [
            y("knowledge", "I-20 là gì"),
            y("collect_profile", "GPA 3.5", gpa="3.5"),
            y("collect_profile", "SAT 1400", sat="1400"),
        ]
    )

    assert types(items) == ["collect_profile", "knowledge"]
    assert items[0].span == "GPA 3.5, SAT 1400" and items[0].entities == {"gpa": "3.5", "sat": "1400"}


def test_a_later_profile_value_replaces_an_earlier_one():
    items = chuan_hoa_y(
        [y("collect_profile", "GPA 3.5", gpa="3.5"), y("collect_profile", "à nhầm 3.6", gpa="3.6")]
    )

    assert items[0].entities == {"gpa": "3.6"}


def test_advice_questions_merge_into_one_intent_with_all_the_text():
    items = chuan_hoa_y(
        [
            y("profile_review", "đánh giá hồ sơ"),
            y("knowledge", "OPT là gì"),
            y("profile_review", "nên làm gì"),
        ]
    )

    assert types(items) == ["profile_review", "knowledge"]
    assert items[0].span == "đánh giá hồ sơ, nên làm gì"


def test_lists_in_merged_entities_are_joined_not_replaced():
    first = y("scenario", "nếu SAT 1400", mode="what_if", changes=[{"factor": "sat", "value": "1400"}])
    second = y("scenario", "và IELTS 7", mode="what_if", changes=[{"factor": "english", "value": "7"}])

    items = chuan_hoa_y([first, second])

    assert [c["factor"] for c in items[0].entities["changes"]] == ["sat", "english"]


def test_knowledge_and_center_intents_are_never_merged_because_each_is_its_own_lookup():
    knowledge = chuan_hoa_y([y("knowledge", "I-20 là gì"), y("knowledge", "SAT là gì")])
    centers = chuan_hoa_y([y("center_referral", "ai sửa luận"), y("center_referral", "ai luyện thi")])

    assert [i.span for i in knowledge] == ["I-20 là gì", "SAT là gì"]
    assert [i.span for i in centers] == ["ai sửa luận", "ai luyện thi"]


def test_the_order_the_student_asked_is_kept_apart_from_the_profile_intent():
    items = chuan_hoa_y([y("center_referral", "a"), y("knowledge", "b"), y("profile_review", "c")])

    assert [i.span for i in items] == ["a", "b", "c"]  # không xếp lại theo loại


def test_a_greeting_next_to_a_real_intent_is_dropped():
    items = chuan_hoa_y([y("out_of_scope", "chào", subtype="farewell"), y("knowledge", "I-20 là gì")])

    assert types(items) == ["knowledge"]


def test_several_out_of_scope_intents_keep_only_the_first():
    items = chuan_hoa_y(
        [y("out_of_scope", "chào", subtype="farewell"), y("out_of_scope", "ừ", subtype="ambiguous")]
    )

    assert [i.span for i in items] == ["chào"]


def test_every_question_is_kept_in_order_however_many_there_are():
    asked = [y("collect_profile", "GPA", gpa="3.5")] + [y("knowledge", f"câu {n}") for n in range(1, 13)]

    items = chuan_hoa_y(asked)

    assert [i.span for i in items] == ["GPA"] + [f"câu {n}" for n in range(1, 13)]


def test_only_the_technical_ceiling_cuts_intents_and_the_profile_intent_is_never_cut():
    asked = [y("collect_profile", "GPA", gpa="3.5")] + [
        y("knowledge", f"câu {n}") for n in range(1, MAX_RAW_Y + 6)
    ]

    items = chuan_hoa_y(asked)

    assert len(items) == MAX_RAW_Y and items[0].type == "collect_profile"
    assert items[-1].span == f"câu {MAX_RAW_Y - 1}"


def test_the_technical_ceiling_is_far_above_any_real_question_list():
    assert MAX_RAW_Y >= 12


# ---- cả tin ----
@pytest.mark.parametrize(
    ("items", "message"),
    [
        ([y("out_of_scope", "chào", subtype="farewell"), y("knowledge")], "đứng một mình"),
        ([y("profile_review"), y("profile_review")], "chỉ được một ý"),
        ([y("collect_profile"), y("collect_profile")], "chỉ được một ý"),
        ([y("knowledge")] * (MAX_RAW_Y + 1), "quá"),
        ([y("knowledge"), y("collect_profile")], "đứng đầu"),
    ],
)
def test_a_message_that_was_not_cleaned_up_is_rejected(items, message):
    with pytest.raises(ValidationError, match=message):
        SlmTin(intents=items)


def test_a_message_with_many_questions_is_accepted():
    assert len(SlmTin(intents=[y("knowledge")] * 12).intents) == 12


def test_a_message_needs_at_least_one_intent():
    with pytest.raises(ValidationError):
        SlmTin(intents=[])


# ---- kết quả cả tin ----
def test_from_raw_cleans_up_what_the_model_returned():
    result = SlmTinResult.from_raw(
        raw(y("knowledge", "I-20 là gì"), y("collect_profile", "em GPA 3.5", gpa="em GPA 3.5")),
        confidence=0.9,
    )

    assert result.types() == ["collect_profile", "knowledge"] and result.confidence == 0.9


@pytest.mark.parametrize(
    "bad",
    [
        {},
        {"intents": []},
        {"intents": "x"},
        {"intents": ["x"]},
        {"intents": [{"type": "la"}]},
        {"cac_y": [{"type": "knowledge", "span": "x"}]},  # tên khóa cũ không còn được đọc
    ],
)
def test_from_raw_rejects_a_wrong_shape_with_a_value_error(bad):
    with pytest.raises(ValueError):
        SlmTinResult.from_raw(bad)


@pytest.mark.parametrize("value", [-0.1, 1.2])
def test_confidence_must_be_between_0_and_1(value):
    with pytest.raises(ValidationError):
        SlmTinResult(intents=[y("knowledge")], confidence=value)


def test_profile_values_are_gathered_from_every_intent_and_the_later_one_wins():
    result = SlmTinResult(
        intents=[y("collect_profile", "a", gpa="3.5", sat="1300"), y("profile_review", "b", sat="1400")]
    )

    assert result.profile_values() == {"gpa": "3.5", "sat": "1400"}


@pytest.mark.parametrize(
    ("items", "expected"),
    [
        ([y("collect_profile")], True),  # trả lời câu hồ sơ đang chờ (kể cả "ừ" mơ hồ, nhánh hồ sơ hỏi lại)
        ([y("knowledge", gpa="GPA em 8.5")], True),  # hỏi kèm giá trị hồ sơ
        ([y("knowledge", schools=["Iowa"])], False),
        ([y("out_of_scope", subtype="farewell")], False),
    ],
)
def test_the_profile_branch_runs_for_profile_answers_or_messages_that_carry_profile_values(items, expected):
    assert SlmTinResult(intents=items).touches_profile() is expected


# ---- khuôn JSON ép SLM ----
def branches() -> dict:
    return {
        b["properties"]["type"]["const"]: b for b in SLM_TIN_SCHEMA["properties"]["intents"]["items"]["anyOf"]
    }


def test_the_json_schema_has_one_branch_per_type_and_a_list_of_bounded_size():
    intents = SLM_TIN_SCHEMA["properties"]["intents"]

    assert set(branches()) == set(INTENTS) == set(get_args(Intent))
    assert (intents["minItems"], intents["maxItems"]) == (1, MAX_RAW_Y)
    assert SLM_TIN_SCHEMA["required"] == ["intents"]


# Khuôn ép SLM và class đọc kết quả phải cùng tên khóa, nếu lệch thì mọi câu trả lời của model đều bị từ chối
def test_every_branch_has_the_same_keys_as_the_class_that_reads_it():
    for branch in branches().values():
        assert set(branch["required"]) == set(branch["properties"]) == set(SlmY.model_fields)


def test_a_branch_only_allows_the_keys_of_its_own_type_and_the_profile():
    for kind, branch in branches().items():
        keys = set(branch["properties"]["entities"]["properties"])
        assert keys == ENTITY_KEYS[kind] | set(PROFILE_KEYS), kind
    assert "service" not in branches()["scenario"]["properties"]["entities"]["properties"]


def test_the_schema_requires_what_the_contract_requires_and_knows_the_services():
    assert branches()["out_of_scope"]["properties"]["entities"]["required"] == ["subtype"]
    assert branches()["scenario"]["properties"]["entities"]["required"] == ["mode"]
    assert branches()["knowledge"]["properties"]["entities"]["required"] == []
    assert branches()["center_referral"]["properties"]["entities"]["properties"]["service"]["enum"] == list(
        SERVICE_CODES
    )


def test_profile_keys_are_the_same_as_the_ones_the_chat_saves():
    assert PROFILE_KEYS == info_to_field.PROFILE_KEYS


# ---- đầu vào dựng skill hồ sơ ----
def request(**overrides) -> SkillRequest:
    base = {
        "loai": "ho_so",
        "cau_hoi": "em nên chọn trường nào",
        "ho_so": {"gpa": "9 thang 10", "sat": "1450"},
    }
    return SkillRequest.model_validate(base | overrides)


@pytest.mark.parametrize(
    ("overrides", "message"),
    [
        ({"ho_so": {}}, "hồ sơ rỗng"),
        ({"ho_so": {"mau_sac": "đỏ"}}, "khóa hồ sơ lạ"),
        ({"tinh_san": {"mau_sac": "đỏ"}}, "khóa tính sẵn lạ"),
        ({"cau_hoi": ""}, ""),
        ({"loai": "kich_ban"}, ""),  # chỉ còn skill hồ sơ
        ({"loai": "kien_thuc"}, ""),
    ],
)
def test_a_skill_request_must_be_complete(overrides, message):
    with pytest.raises(ValidationError, match=message or None):
        request(**overrides)


def test_a_skill_request_carries_the_calculated_lines():
    assert request(tinh_san={"gpa_level": "3.2 thang 4, thấp (TB các trường 3.6)"}).tinh_san


# ---- khớp với dữ liệu ----
DATA = Path(__file__).resolve().parents[1] / "data" / "slm" / "du_lieu_slm.jsonl"


def labelled_outputs() -> list[dict]:
    return [
        json.loads(line)["output"] for line in DATA.read_text(encoding="utf-8").splitlines() if line.strip()
    ]


# Đầu ra trong dữ liệu chính là thứ SLM học để trả, nên dòng nào cũng phải đọc được bằng hợp đồng
def test_every_labelled_output_in_the_dataset_is_a_valid_answer():
    for output in labelled_outputs():
        SlmTinResult.from_raw(output)


# Mỗi ý trong dữ liệu phải nằm gọn trong nhánh của loại nó ở khuôn SLM_TIN_SCHEMA, nếu không model bị khuôn chặn không học được
def test_every_labelled_intent_fits_the_branch_of_its_type():
    for output in labelled_outputs():
        for item in output["intents"]:
            entities = branches()[item["type"]]["properties"]["entities"]
            assert set(item["entities"]) <= set(entities["properties"]), item
            assert set(entities["required"]) <= set(item["entities"]), item


async def test_the_fixed_slm_returns_its_result_whatever_the_message_is():
    slm = FixedSlm(SlmTinResult(intents=[y("collect_profile")], confidence=0.9))

    result = await slm.classify(ChatState(), "6.5 ạ")

    assert (result.types(), result.confidence) == (["collect_profile"], 0.9)
