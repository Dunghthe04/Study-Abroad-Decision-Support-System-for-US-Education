"""Bước 2.3: đọc dữ liệu train của model chọn skill. Không cần Ollama."""

import pytest

from app.services.skill_rules import REQUIRED_B4
from ml.skill_picker.data import LABELS, build_text, labels_to_vector, load_rows, split_rows
from ml.skill_picker.embed import texts_hash


def test_build_text_joins_the_four_fields():
    row = {
        "current_step": "B2",
        "last_question": "Bạn đã thi SAT chưa?",
        "message": "em được 1420",
        "missing": "english, budget",
    }

    assert (
        build_text(row) == "[BƯỚC] B2 [HỎI] Bạn đã thi SAT chưa? [TIN] em được 1420 [THIẾU] english, budget"
    )


def test_build_text_marks_empty_fields():
    row = {"current_step": "B4", "last_question": "", "message": "cảm ơn", "missing": ""}

    assert build_text(row) == "[BƯỚC] B4 [HỎI] (không có) [TIN] cảm ơn [THIẾU] (không thiếu)"


def test_labels_to_vector_is_multi_hot_in_fixed_order():
    vector = labels_to_vector("skill_ho_so, skill_tra_loi")

    assert len(vector) == len(LABELS) == 11
    assert vector[LABELS.index("skill_ho_so")] == 1
    assert vector[LABELS.index("skill_tra_loi")] == 1
    assert sum(vector) == 2


def test_labels_to_vector_rejects_unknown_label():
    with pytest.raises(ValueError):
        labels_to_vector("skill_la")


def test_dataset_is_clean():
    rows = load_rows()
    parts = split_rows(rows)

    assert len(rows) == len(parts["train"]) + len(parts["val"]) + len(parts["test"])
    assert len({r["id"] for r in rows}) == len(rows)  # id không trùng
    assert not parts["test"]  # chưa có test thật
    for row in rows:
        assert 1 <= sum(labels_to_vector(row["labels"])) <= 2  # mỗi dòng 1 hoặc 2 nhãn hợp lệ
        assert row["message"].strip()


def test_missing_column_matches_the_required_fields_in_code():
    # Lúc chạy thật model nhận danh sách thiếu theo khóa và thứ tự của REQUIRED_B4, dữ liệu train phải giống vậy
    for row in load_rows():
        keys = [k.strip() for k in row["missing"].split(",") if k.strip()]
        assert set(keys) <= set(REQUIRED_B4), f"{row['id']}: khóa lạ {set(keys) - set(REQUIRED_B4)}"
        assert keys == [k for k in REQUIRED_B4 if k in keys], f"{row['id']}: sai thứ tự khóa"


def test_texts_hash_changes_when_any_text_changes():
    rows = load_rows()[:3]
    same = [dict(r) for r in rows]
    changed = [dict(r) for r in rows]
    changed[1]["missing"] = changed[1]["missing"] + ", major"

    assert texts_hash(rows) == texts_hash(same)
    assert texts_hash(rows) != texts_hash(changed)
