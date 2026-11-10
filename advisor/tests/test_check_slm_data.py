"""Test cho scripts/check_slm_data.py: mỗi kiểm tra bắt đúng lỗi trên dữ liệu giả nhỏ, và không báo oan dòng đúng."""

from scripts.check_slm_data import (
    Item,
    check_label_has_enough_test,
    check_no_skill,
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
    skill: str | None = None,
    sheet: str = "knowledge",
) -> Item:
    record = {
        "id": f"{sheet}_{row}",
        "type": ys[0]["type"],
        "split": split,
        "input": {"cau_hoi": question, "ho_so": profile, "cau_he_thong_dang_hoi": pending},
        "output": {"cac_y": ys},
        "skill_md": skill,
    }
    return Item(sheet, row, record)


def y(doan: str, type_: str = "kien_thuc", **thuc_the) -> dict:
    return {"type": type_, "doan": doan, "thuc_the": thuc_the}


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
        5, "GPA 9.2 và IELTS 8", [y("GPA 9.2", "mac_dinh", gpa="9.2"), y("IELTS 8", "mac_dinh", english="8")]
    )
    one = make(6, "GPA 9.2", [y("GPA 9.2", "mac_dinh", gpa="9.2")])

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
    assert labels_of(y("x", "kien_thuc", nhiem_vu="tra_cuu")) == ["kien_thuc/tra_cuu"]
    assert labels_of(y("x", "ho_so", cac_nhiem_vu=["lo_trinh", "danh_gia"])) == [
        "ho_so/lo_trinh",
        "ho_so/danh_gia",
    ]
    assert labels_of(y("x", "ngoai_pham_vi")) == ["ngoai_pham_vi/-"]


def test_a_row_with_two_tasks_is_counted_once_for_each_task():
    item = make(
        1, "a", [y("a", "ho_so", cac_nhiem_vu=["danh_gia", "lo_trinh"])], split="test", sheet="profile_review"
    )

    counts = label_counts([item])

    assert counts["ho_so/danh_gia"]["test"] == 1 and counts["ho_so/lo_trinh"]["test"] == 1


def test_a_label_with_fewer_test_rows_than_the_minimum_is_reported():
    items = [make(i, f"q{i}", [y(f"q{i}", nhiem_vu="tra_cuu")], split="test") for i in range(10)]
    items += [make(20 + i, f"r{i}", [y(f"r{i}", nhiem_vu="tim_kiem")], split="test") for i in range(3)]
    items += [make(40, "s", [y("s", nhiem_vu="tim_kiem")], split="train")]

    issues = check_label_has_enough_test(items, 10)

    assert [i.where for i in issues] == ["kien_thuc/tim_kiem"]
    assert "test 3" in issues[0].detail


def test_a_leftover_skill_is_reported():
    items = [make(1, "a", [y("a")], skill="# SKILL"), make(2, "b", [y("b")])]

    assert [i.where for i in check_no_skill(items)] == ["knowledge, dòng 1"]


def test_pending_questions_are_counted_per_type():
    items = [
        make(1, "a", [y("a")], pending="Hiện bạn đang học lớp mấy?"),
        make(2, "b", [y("b")]),
        make(3, "c", [y("c", "trung_tam")], pending="Bạn ở thành phố nào?", sheet="center_referral"),
    ]

    assert pending_summary(items) == {"kien_thuc": 1, "trung_tam": 1}


def test_a_clean_dataset_has_no_issue_in_any_check():
    from app.schemas.slm import INFO_FIELDS

    # Mỗi mục thông tin trường cần đủ 10 dòng test, nên mỗi mục có một nhóm 10 dòng riêng
    items = [
        make(
            100 * k + i,
            f"hỏi mục {k} lần {chr(97 + i)}",
            [y(f"hỏi mục {k} lần {chr(97 + i)}", nhiem_vu="tra_cuu", muc=[field])],
            split="test",
        )
        for k, field in enumerate(INFO_FIELDS)
        for i in range(10)
    ]

    results = run_checks(items, 10)

    # Kiểm tra 8 (câu nhiều ý) và 9 (khóa hồ sơ) cần dữ liệu riêng nên được thử ở các test bên dưới
    assert all(not issues for name, issues in results.items() if not name.startswith(("8.", "9.")))


def test_an_info_field_with_too_few_test_rows_is_reported_and_a_covered_one_is_not():
    from scripts.check_slm_data import check_every_info_field_has_enough_test

    items = [
        make(i, f"hỏi học phí {chr(97 + i)}", [y("a", muc=["chi_phi"])], split="test") for i in range(10)
    ]
    items += [make(30, "hỏi ký túc xá", [y("b", muc=["nha_o"])], split="test")]

    issues = check_every_info_field_has_enough_test(items, 10)

    where = {i.where for i in issues}
    assert "chi_phi" not in where and "nha_o" in where


def test_the_new_school_info_fields_are_part_of_the_contract():
    from app.schemas.slm import INFO_FIELDS

    assert {"nha_o", "ty_le_sv_giang_vien", "hoat_dong_sv", "khuon_vien"} <= set(INFO_FIELDS)


def test_a_dataset_with_too_few_multi_intent_rows_is_reported_for_test_and_val():
    from scripts.check_slm_data import check_multi_intent_has_enough_test_and_val

    three = [y("a"), y("b"), y("c")]
    items = [make(i, f"câu nhiều ý {chr(97 + i)}", three, split="test") for i in range(10)]
    items += [make(50 + i, f"câu nhiều ý khác {chr(97 + i)}", three, split="val") for i in range(4)]
    items += [make(90, "câu một ý", [y("a")], split="test")]

    issues = check_multi_intent_has_enough_test_and_val(items, 10)

    assert [i.where for i in issues] == ["val"]


def test_a_profile_key_without_test_or_val_rows_is_reported():
    from scripts.check_slm_data import check_every_profile_key_has_test_and_val

    def with_key(i, split):
        return make(i, f"trả lời {chr(97 + i)}", [y("a", "mac_dinh", gpa="a")], split=split)

    items = [with_key(i, "test") for i in range(3)] + [with_key(10 + i, "val") for i in range(2)]

    issues = check_every_profile_key_has_test_and_val(items, 3)

    assert ("gpa", True) not in {(i.where, True) for i in issues if "test" in i.detail}
    assert any(i.where == "gpa" and i.detail.startswith("val 2") for i in issues)
    assert any(i.where == "visa_refused" for i in issues)
