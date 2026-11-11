"""Test cho scripts/check_slm_data.py: mỗi kiểm tra bắt đúng lỗi trên dữ liệu giả nhỏ, và không báo oan dòng đúng."""

from app.schemas.slm import INFO_FIELDS
from scripts.check_slm_data import (
    Item,
    check_every_info_field_has_enough_test,
    check_every_profile_key_has_test_and_val,
    check_label_has_enough_test,
    check_multi_intent_has_enough_test_and_val,
    check_one_default_per_row,
    check_profile_not_in_two_splits,
    check_spans,
    check_test_questions_not_in_train,
    label_counts,
    labels_of,
    pending_summary,
    run_checks,
)


def make(
    row: int,
    question: str,
    ys: list[dict],
    split: str = "train",
    profile: dict | None = None,
    pending: str = "",
    sheet: str = "knowledge",
) -> Item:
    record = {
        "id": f"{sheet}_{row}",
        "type": ys[0]["type"],
        "split": split,
        "input": {"question": question, "profile": profile, "pending_question": pending},
        "output": {"intents": ys},
    }
    return Item(sheet, row, record)


def y(span: str, type_: str = "knowledge", **entities) -> dict:
    return {"type": type_, "span": span, "entities": entities}


def test_a_span_that_is_not_part_of_the_question_is_reported_with_its_row():
    bad = make(153, "I-20 và SAT là gì?", [y("I-20 là gì"), y("SAT là gì")])
    good = make(154, "OPT và CPT là gì ạ", [y("OPT"), y("CPT là gì ạ")])

    issues = check_spans([bad, good])

    assert [i.where for i in issues] == ["knowledge, dòng 153"]


def test_spans_are_compared_ignoring_case_and_extra_spaces():
    item = make(2, "Học  phí  Purdue?", [y("học phí purdue")])

    assert check_spans([item]) == []


def test_two_default_ideas_in_one_row_are_reported_but_one_is_not():
    two = make(
        5,
        "GPA 9.2 và IELTS 8",
        [y("GPA 9.2", "collect_profile", gpa="9.2"), y("IELTS 8", "collect_profile", english="8")],
    )
    one = make(6, "GPA 9.2", [y("GPA 9.2", "collect_profile", gpa="9.2")])

    assert [i.where for i in check_one_default_per_row([two, one])] == ["knowledge, dòng 5"]


def test_a_test_question_that_also_exists_in_train_is_reported_once_on_the_test_row():
    train = make(1, "Visa F-1 là gì?", [y("Visa F-1 là gì")], split="train")
    test = make(2, "visa f-1 là gì?", [y("visa f-1 là gì")], split="test")
    other = make(3, "OPT là gì", [y("OPT là gì")], split="test")

    issues = check_test_questions_not_in_train([train, test, other])

    assert [i.where for i in issues] == ["knowledge, dòng 2"]


def test_the_same_question_in_train_and_val_only_is_not_a_test_leak():
    a = make(1, "Visa là gì", [y("Visa là gì")], split="train")
    b = make(2, "Visa là gì", [y("Visa là gì")], split="val")

    assert check_test_questions_not_in_train([a, b]) == []


def test_a_profile_in_two_splits_is_reported_once_per_profile():
    p = {"gpa": "8 thang 10"}
    items = [
        make(1, "a", [y("a")], split="train", profile=p),
        make(2, "b", [y("b")], split="test", profile=p),
        make(3, "c", [y("c")], split="val", profile={"gpa": "9 thang 10"}),
    ]

    issues = check_profile_not_in_two_splits(items)

    assert len(issues) == 1
    assert "test" in issues[0].detail and "train" in issues[0].detail


def test_rows_without_a_profile_never_count_as_a_shared_profile():
    items = [make(1, "a", [y("a")], split="train"), make(2, "b", [y("b")], split="test")]

    assert check_profile_not_in_two_splits(items) == []


def test_labels_are_the_type_plus_its_task_and_a_task_list_counts_each_task():
    assert labels_of(y("x", "knowledge", tasks=["school_info"])) == ["knowledge/school_info"]
    assert labels_of(y("x", "profile_review", tasks=["roadmap", "assessment"])) == [
        "profile_review/roadmap",
        "profile_review/assessment",
    ]
    assert labels_of(y("x", "scenario", mode="what_if")) == ["scenario/what_if"]
    assert labels_of(y("x", "out_of_scope", subtype="farewell")) == ["out_of_scope/farewell"]


def test_an_intent_without_tasks_is_counted_under_the_default_task_of_its_type():
    assert labels_of(y("x", "collect_profile")) == ["collect_profile/answer"]
    assert labels_of(y("x", "knowledge")) == ["knowledge/general"]
    assert labels_of(y("x", "center_referral")) == ["center_referral/find"]
    assert labels_of(y("x", "profile_review", tasks=[])) == ["profile_review/-"]


def test_a_row_with_two_tasks_is_counted_once_for_each_task():
    item = make(
        1,
        "a",
        [y("a", "profile_review", tasks=["assessment", "roadmap"])],
        split="test",
        sheet="profile_review",
    )

    counts = label_counts([item])

    assert counts["profile_review/assessment"]["test"] == 1 and counts["profile_review/roadmap"]["test"] == 1


def test_a_label_with_fewer_test_rows_than_the_minimum_is_reported():
    items = [make(i, f"q{i}", [y(f"q{i}", tasks=["school_info"])], split="test") for i in range(10)]
    items += [make(20 + i, f"r{i}", [y(f"r{i}", tasks=["search"])], split="test") for i in range(3)]
    items += [make(40, "s", [y("s", tasks=["search"])], split="train")]

    issues = check_label_has_enough_test(items, 10)

    assert [i.where for i in issues] == ["knowledge/search"]
    assert "test 3" in issues[0].detail


def test_pending_questions_are_counted_per_type():
    items = [
        make(1, "a", [y("a")], pending="Hiện bạn đang học lớp mấy?"),
        make(2, "b", [y("b")]),
        make(3, "c", [y("c", "center_referral")], pending="Bạn ở thành phố nào?", sheet="center_referral"),
    ]

    assert pending_summary(items) == {"knowledge": 1, "center_referral": 1}


def test_a_clean_dataset_has_no_issue_in_any_check():
    # Mỗi mục thông tin trường cần đủ 10 dòng test, nên mỗi mục có một nhóm 10 dòng riêng
    items = [
        make(
            100 * k + i,
            f"hỏi mục {k} lần {chr(97 + i)}",
            [y(f"hỏi mục {k} lần {chr(97 + i)}", tasks=["school_info"], fields=[field])],
            split="test",
        )
        for k, field in enumerate(INFO_FIELDS)
        for i in range(10)
    ]

    results = run_checks(items, 10)

    # Kiểm tra 7 (câu nhiều ý) và 8 (khóa hồ sơ) cần dữ liệu riêng nên được thử ở các test bên dưới
    assert all(not issues for name, issues in results.items() if not name.startswith(("7.", "8.")))


def test_an_info_field_with_too_few_test_rows_is_reported_and_a_covered_one_is_not():
    items = [
        make(i, f"hỏi học phí {chr(97 + i)}", [y("a", fields=["cost"])], split="test") for i in range(10)
    ]
    items += [make(30, "hỏi ký túc xá", [y("b", fields=["housing"])], split="test")]

    issues = check_every_info_field_has_enough_test(items, 10)

    where = {i.where for i in issues}
    assert "cost" not in where and "housing" in where


def test_the_new_school_info_fields_are_part_of_the_contract():
    assert {"housing", "student_faculty_ratio", "student_activities", "campus_setting"} <= set(INFO_FIELDS)


def test_a_dataset_with_too_few_multi_intent_rows_is_reported_for_test_and_val():
    three = [y("a"), y("b"), y("c")]
    items = [make(i, f"câu nhiều ý {chr(97 + i)}", three, split="test") for i in range(10)]
    items += [make(50 + i, f"câu nhiều ý khác {chr(97 + i)}", three, split="val") for i in range(4)]
    items += [make(90, "câu một ý", [y("a")], split="test")]

    issues = check_multi_intent_has_enough_test_and_val(items, 10)

    assert [i.where for i in issues] == ["val"]


def test_a_profile_key_without_test_or_val_rows_is_reported():
    def with_key(i, split):
        return make(i, f"trả lời {chr(97 + i)}", [y("a", "collect_profile", gpa="a")], split=split)

    items = [with_key(i, "test") for i in range(3)] + [with_key(10 + i, "val") for i in range(2)]

    issues = check_every_profile_key_has_test_and_val(items, 3)

    assert ("gpa", True) not in {(i.where, True) for i in issues if "test" in i.detail}
    assert any(i.where == "gpa" and i.detail.startswith("val 2") for i in issues)
    assert any(i.where == "visa_refused" for i in issues)
