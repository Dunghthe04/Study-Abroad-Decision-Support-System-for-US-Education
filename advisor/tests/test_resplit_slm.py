"""Test cho scripts/resplit_slm.py: chia theo nhóm (không rò hồ sơ, không rò đầu vào) và sắp dòng train, val, test."""

import shutil
from pathlib import Path

from openpyxl import Workbook, load_workbook

from scripts.check_slm_data import (
    Item,
    check_profile_not_in_two_splits,
    check_test_questions_not_in_train,
    read_items,
)
from scripts.resplit_slm import apply_to_excel, assign_splits, group_items, skeleton


def words(i: int) -> str:
    return chr(97 + i % 26) + chr(97 + i // 26 % 26) + chr(97 + i // 676)


def make(
    row: int,
    question: str,
    profile: dict | None = None,
    sheet: str = "knowledge",
    split: str = "train",
    **thuc_the,
) -> Item:
    record = {
        "id": f"{sheet}_{row}",
        "type": "kien_thuc",
        "split": split,
        "input": {"cau_hoi": question, "ho_so": profile, "cau_he_thong_dang_hoi": ""},
        "output": {
            "cac_y": [
                {"type": "kien_thuc", "doan": question, "thuc_the": {"nhiem_vu": "tra_cuu", **thuc_the}}
            ]
        },
        "skill_md": None,
    }
    return Item(sheet, row, record)


def test_the_skeleton_hides_school_names_and_digits():
    a = make(1, "học phí của Purdue năm 2026", truong="Purdue")
    b = make(2, "học phí của Stanford năm 2027", truong="Stanford")

    assert skeleton(a) == skeleton(b)


def test_rows_with_the_same_profile_are_one_group_even_in_different_sheets():
    p = {"gpa": "8 thang 10"}
    items = [
        make(1, "câu một", p, "profile_review"),
        make(2, "câu hai khác hẳn", p, "scenario"),
        make(3, "câu ba", {"gpa": "9"}),
    ]

    groups = sorted(sorted(g) for g in group_items(items))

    assert groups == [[0, 1], [2]]


def test_the_same_text_with_different_profiles_is_not_forced_into_one_group():
    items = [make(i, "so sánh hai trường đầu", {"gpa": str(i)}, "profile_review") for i in range(1, 5)]

    assert len(group_items(items)) == 4


def test_no_profile_and_no_identical_input_is_shared_between_splits():
    items = []
    for i in range(300):
        items.append(
            make(i + 1, f"câu {words(i)} xyz", {"gpa": f"g{i // 3}"}, "profile_review")
        )  # 3 dòng chung một hồ sơ
    for i in range(300):
        items.append(
            make(1000 + i, f"cho em hỏi về chuyện {words(i % 40)}")
        )  # 40 khuôn câu, mỗi khuôn nhiều dòng
    splits = assign_splits(items)
    done = [Item(it.sheet, it.row, {**it.record, "split": s}) for it, s in zip(items, splits, strict=True)]

    assert check_profile_not_in_two_splits(done) == []
    assert check_test_questions_not_in_train(done) == []
    assert {"train", "val", "test"} == set(splits)


def test_the_split_is_the_same_every_time_with_the_same_seed():
    items = [make(i, f"câu {words(i)} khác nhau hoàn toàn") for i in range(200)]

    assert assign_splits(items) == assign_splits(items)
    assert assign_splits(items, seed="khác") != assign_splits(items)


def test_the_ratio_is_close_to_70_15_15():
    items = [make(i, f"câu {words(i)} nói về điều gì đó") for i in range(1000)]

    splits = assign_splits(items)

    share = {s: splits.count(s) / 1000 for s in ("train", "val", "test")}
    assert 0.62 <= share["train"] <= 0.78 and 0.10 <= share["val"] <= 0.20 and 0.10 <= share["test"] <= 0.20


def test_applying_sorts_every_sheet_train_val_test_and_keeps_every_cell_even_empty_ones(tmp_path: Path):
    from scripts.build_slm_jsonl import DEFAULT_EXCEL

    path = tmp_path / "du_lieu.xlsx"
    shutil.copy(DEFAULT_EXCEL, path)
    items, issues = read_items(path)
    assert not issues
    splits = assign_splits(items)

    apply_to_excel(path, items, splits)

    again, issues = read_items(path)
    assert not issues and len(again) == len(items)
    for sheet in {it.sheet for it in again}:
        order = [("train", "val", "test").index(it.record["split"]) for it in again if it.sheet == sheet]
        assert order == sorted(order)

    def key(it):
        return (
            it.record["input"]["cau_hoi"],
            str(it.record["input"]["ho_so"]),
            it.record["input"]["cau_he_thong_dang_hoi"],
            str(it.record["output"]),
        )

    assert sorted(map(key, again)) == sorted(map(key, items))


def test_applying_does_not_leave_old_values_in_cells_that_should_be_empty(tmp_path: Path):
    path = tmp_path / "nho.xlsx"
    wb = Workbook()
    ws = wb.active
    ws.title = "knowledge"
    ws.append(["question", "profile", "pending_question", "intents", "split", "context"])
    ws.append(["a", None, "câu hỏi cũ", "[]", "test", None])
    ws.append(["b", None, None, "[]", "train", None])
    wb.save(path)
    items = [make(2, "a", sheet="knowledge"), make(3, "b", sheet="knowledge")]

    apply_to_excel(path, items, ["test", "train"])

    rows = list(load_workbook(path)["knowledge"].iter_rows(min_row=2, values_only=True))
    assert rows[0][:3] == ("b", None, None)
    assert rows[1][:3] == ("a", None, "câu hỏi cũ")


def test_rebalance_moves_groups_to_train_but_never_below_the_minimum_of_a_label():
    from scripts.resplit_slm import rebalance

    items = []
    for i in range(100):  # nhãn A: 60 dòng đang ở test, vượt xa 15%
        items.append(make(i + 1, f"câu a {words(i)} nói về chuyện A", split="test" if i < 60 else "train"))
    for i in range(100):
        items.append(make(1000 + i, f"câu b {words(i)} nói về chuyện B", split="train"))
    for it in items:
        it.record["output"]["cac_y"][0]["thuc_the"]["nhiem_vu"] = "tra_cuu" if it.row < 1000 else "so_sanh"
    splits = [it.record["split"] for it in items]

    after = rebalance(items, splits, min_test=10)

    assert 10 <= after.count("test") < 60  # có chuyển bớt, nhưng nhãn A còn ít nhất 10 dòng test
    assert all(
        a == "train" for a, b in zip(after, splits, strict=True) if b == "train"
    )  # train không bị lấy bớt


def test_rebalance_keeps_a_group_together_and_does_not_touch_an_already_balanced_split():
    from scripts.resplit_slm import rebalance

    p = {"gpa": "8 thang 10"}
    items = [
        make(
            i + 1,
            f"câu {words(i)} khác",
            p if i < 3 else None,
            "profile_review",
            split="train" if i < 3 else "val",
        )
        for i in range(10)
    ]
    splits = [it.record["split"] for it in items]

    after = rebalance(items, splits, min_test=1)

    assert len({after[i] for i in range(3)}) == 1
