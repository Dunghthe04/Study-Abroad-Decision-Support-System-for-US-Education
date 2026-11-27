"""Phần thống kê của báo cáo đo SLM: tin lùi về mơ hồ không phải dự đoán, ma trận nhầm lẫn nhất quán với precision/recall,
khoảng tin cậy, kiểm định so cặp, hiệu chuẩn, kiểm tra dữ liệu và ví dụ lỗi. Không cần Ollama."""

import json
import sys

import pytest

from app.services.ollama import OllamaError
from slm.eval import slm_audit, slm_report
from slm.eval.eval_slm import classify_failure
from slm.eval.slm_metrics import (
    INVALID,
    NONE,
    calibration,
    common_valid,
    confusion,
    f1_ci,
    judge,
    labels_of,
    mcnemar_exact,
    paired_compare,
    per_label,
    predicted_intents,
    roc,
    summarize,
    valid_rows,
    wilson,
)


def y(kind: str, span: str = "x", **entities) -> dict:
    return {"type": kind, "span": span, "entities": entities}


def row(row_id: str, gold: list, pred: list, **extra) -> dict:
    return {
        "id": row_id,
        "question": f"cau {row_id}",
        "nhan": {"intents": gold},
        "du_doan": {"intents": pred},
        "confidence": 0.9,
        "giay": 1.0,
        **extra,
    }


def fallback(row_id: str, gold: list) -> dict:
    """Tin model trả sai luật hai lần: hệ thống tự điền out_of_scope/ambiguous và confidence 0."""
    return row(
        row_id,
        gold,
        [y("out_of_scope", subtype="ambiguous")],
        confidence=0.0,
        lui_ve_mo_ho=True,
        loi_ly_do="invalid_json",
    )


# ---------------------------------------------------------------- tin lùi về mơ hồ không phải dự đoán
def test_a_fallback_answer_is_not_a_prediction_of_the_model():
    rows = [fallback("a", [y("collect_profile")])]

    assert predicted_intents(rows[0]) == []
    table = per_label(rows)

    assert table["collect_profile"]["fn"] == 1  # ý đúng bị bỏ sót
    assert (
        "out_of_scope" not in table
    )  # ý out_of_scope giả của hệ thống không tính là dự đoán, nên không có precision bị kéo thấp


def test_valid_rows_drop_only_the_fallback_answers():
    rows = [fallback("a", [y("knowledge")]), row("b", [y("knowledge")], [y("knowledge")])]

    assert [r["id"] for r in valid_rows(rows)] == ["b"]


def test_the_confusion_matrix_has_an_invalid_column_and_matches_precision_and_recall():
    rows = [
        row("a", [y("knowledge")], [y("knowledge")]),
        row("b", [y("knowledge")], [y("center_referral")]),  # nhầm loại
        row("c", [y("knowledge"), y("scenario")], [y("knowledge")]),  # thiếu một ý
        row("d", [y("scenario")], [y("scenario"), y("profile_review")]),  # thừa một ý
        fallback("e", [y("collect_profile"), y("knowledge")]),
    ]

    c = confusion(rows)
    rows_idx, cols_idx = c["labels"], c["col_labels"]
    cell = lambda g, p: c["matrix"][rows_idx.index(g)][cols_idx.index(p)]  # noqa: E731

    assert cols_idx[-1] == INVALID and rows_idx[-1] == NONE
    assert cell("collect_profile", INVALID) == 1 and cell("knowledge", INVALID) == 1
    assert cell("knowledge", "center_referral") == 1
    assert cell("scenario", NONE) == 1  # bị bỏ sót
    assert cell(NONE, "profile_review") == 1  # thừa
    table = per_label(rows)
    for kind, stats in table.items():
        j = cols_idx.index(kind)
        # Đường chéo là tp; tổng cột (trừ cột INVALID, không phải dự đoán) là tp + fp; tổng hàng (cả cột INVALID) là tp + fn
        assert cell(kind, kind) == stats["tp"]
        assert sum(r[j] for r in c["matrix"]) == stats["tp"] + stats["fp"]
        if kind in rows_idx:
            assert sum(c["matrix"][rows_idx.index(kind)]) == stats["tp"] + stats["fn"]


def test_a_profile_review_with_several_tasks_counts_once_per_task():
    gold = [y("profile_review", tasks=["assessment", "school_list"])]
    pred = [y("profile_review", tasks=["assessment"])]

    assert labels_of(gold[0]) == ["profile_review/assessment", "profile_review/school_list"]
    table = per_label([row("a", gold, pred)], "task")

    assert table["profile_review/assessment"]["tp"] == 1
    assert table["profile_review/school_list"]["fn"] == 1
    assert "profile_review/assessment,school_list" not in table  # không còn nhãn ghép hiếm


# ---------------------------------------------------------------- khoảng tin cậy và kiểm định
def test_wilson_interval_is_inside_zero_one_and_handles_empty_groups():
    assert wilson(0, 0) == (0.0, 0.0)
    lo, hi = wilson(5, 10)
    assert lo == pytest.approx(0.2366, abs=1e-3) and hi == pytest.approx(0.7634, abs=1e-3)
    assert wilson(0, 5)[0] == 0.0 and wilson(5, 5)[1] == 1.0


def test_mcnemar_uses_only_the_messages_where_the_two_models_differ():
    same = mcnemar_exact([True, False, True], [True, False, True])
    assert same == {"only_a": 0, "only_b": 0, "p": 1.0}

    one_sided = mcnemar_exact([True] * 10, [False] * 10)  # 10 tin chỉ A đúng, 0 tin chỉ B đúng
    assert one_sided["only_a"] == 10 and one_sided["only_b"] == 0
    assert one_sided["p"] == pytest.approx(2 / 1024)

    balanced = mcnemar_exact([True, False] * 5, [False, True] * 5)  # 5 so 5: không có khác biệt
    assert balanced["p"] == 1.0


def sample(good: int, bad: int) -> list[dict]:
    rows = [
        row(f"r{i}", [y("knowledge", schools=["MIT"])], [y("knowledge", schools=["MIT"])])
        for i in range(good)
    ]
    rows += [
        row(f"r{good + i}", [y("knowledge", schools=["MIT"])], [y("knowledge", schools=["Yale"])])
        for i in range(bad)
    ]
    return rows


def test_a_model_compared_with_itself_has_no_difference():
    rows = sample(6, 4)

    result = paired_compare(rows, rows)

    assert result["exact"]["hieu"] == 0 and result["exact"]["p"] == 1.0
    lo, hi = result["exact"]["khoang_tin_cay_95"]
    assert lo <= 0 <= hi
    assert result["macro_f1"]["hieu"] == 0


def test_a_clearly_better_model_has_an_interval_above_zero_and_a_small_p():
    result = paired_compare(sample(180, 20), sample(100, 100))

    exact = result["exact"]
    assert exact["hieu"] == pytest.approx(0.4)
    assert exact["khoang_tin_cay_95"][0] > 0
    assert exact["p"] < 0.001 and exact["only_b"] == 0


def test_paired_comparison_refuses_runs_on_different_messages():
    with pytest.raises(ValueError, match="khác id"):
        paired_compare(sample(3, 1), sample(3, 2))


def test_the_f1_interval_is_reproducible_and_contains_the_point_estimate():
    rows = [row(f"a{i}", [y("knowledge")], [y("knowledge")]) for i in range(60)]
    rows += [row(f"b{i}", [y("knowledge")], [y("center_referral")]) for i in range(20)]  # nhầm loại
    rows += [row(f"c{i}", [y("scenario")], [y("scenario")]) for i in range(30)]
    rows += [row(f"d{i}", [y("scenario")], [y("knowledge")]) for i in range(10)]  # nhầm loại

    first, second = f1_ci(rows), f1_ci(rows)
    point = summarize(rows)["f1_loai"]

    assert first == second
    for key in ("macro_f1", "micro_f1"):
        lo, hi = first[key]
        assert lo <= point[key] <= hi and hi > lo


def test_the_summary_carries_the_new_fields():
    s = summarize([*sample(5, 2), fallback("f", [y("knowledge")])])

    assert s["so_tin"] == 8 and s["so_tin_hop_le"] == 7
    assert s["ly_do_loi"] == {"invalid_json": 1}
    assert set(s["khoang_tin_cay_f1"]) == {"macro_f1", "micro_f1"}
    assert s["thoi_gian"]["max"] == 1.0
    assert all("khoang_tin_cay_dung_het" in g for g in s["theo_so_y"].values())
    assert s["tin_cay"]["khi_sai"] == []  # tin lùi về mơ hồ không nằm trong confidence "khi sai"


def test_calibration_compares_confidence_with_the_category_accuracy_and_skips_fallbacks():
    rows = [row(f"a{i}", [y("knowledge")], [y("knowledge")], confidence=0.95) for i in range(9)]
    rows += [row("b", [y("knowledge")], [y("center_referral")], confidence=0.95)]  # 9 đúng / 10 ở khoảng 0.9
    rows += [fallback("f", [y("knowledge")])]

    cal = calibration(rows)

    assert cal["so_tin"] == 10  # tin lùi về mơ hồ bị loại
    assert cal["khoang"][0]["so_tin"] == 10 and cal["khoang"][0]["accuracy"] == pytest.approx(0.9)
    assert cal["ece"] == pytest.approx(abs(0.9 - 0.95))
    assert calibration([fallback("f", [y("knowledge")])]) is None


def test_roc_auc_is_the_chance_that_a_correct_message_scores_above_a_wrong_one():
    right = [
        row(f"r{i}", [y("knowledge")], [y("knowledge")], confidence=c) for i, c in enumerate((0.99, 0.8))
    ]
    wrong = [
        row(f"w{i}", [y("knowledge")], [y("center_referral")], confidence=c) for i, c in enumerate((0.9, 0.4))
    ]

    curve = roc(right + wrong)

    # 4 cặp (đúng, sai): 0.99 hơn cả hai, 0.8 chỉ hơn 0.4 → 3/4
    assert curve["auc"] == pytest.approx(0.75)
    assert (curve["so_tin_dung"], curve["so_tin_sai"]) == (2, 2)
    assert (curve["fpr"][0], curve["tpr"][0], curve["fpr"][-1], curve["tpr"][-1]) == (0.0, 0.0, 1.0, 1.0)


def test_roc_ties_count_half_and_do_not_depend_on_row_order():
    rows = [
        row("r", [y("knowledge")], [y("knowledge")], confidence=0.9),
        row("w", [y("knowledge")], [y("center_referral")], confidence=0.9),
    ]

    assert roc(rows)["auc"] == pytest.approx(0.5) and roc(rows[::-1])["auc"] == pytest.approx(0.5)


def test_roc_can_leave_out_fallbacks_and_needs_both_outcomes():
    rows = [row("r", [y("knowledge")], [y("knowledge")], confidence=0.9), fallback("f", [y("knowledge")])]

    assert roc(rows)["auc"] == pytest.approx(1.0)  # tính cả tin lùi về mơ hồ (confidence 0)
    assert roc(rows, valid_only=True) is None  # bỏ đi thì không còn tin sai nào để so


# ---------------------------------------------------------------- kiểm tra dữ liệu và ví dụ lỗi
def record(split: str, question: str, n_intents: int = 1) -> dict:
    return {
        "type": "knowledge",
        "split": split,
        "input": {"question": question},
        "output": {"intents": [y("knowledge")] * n_intents},
    }


def test_the_data_audit_counts_each_split_and_finds_questions_shared_between_splits():
    records = [
        record("train", "Cho em hỏi về MIT"),
        record("train", "hello"),
        record("val", "hello"),
        record("test", "cho em  hỏi về mit", 2),  # chỉ khác hoa thường và khoảng trắng với câu của train
        record("test", "câu riêng của test"),
        record("test", "câu riêng của test"),
    ]

    audit = slm_audit.data_audit(records)

    assert audit["splits"]["test"] == {"messages": 3, "intents": 4, "multi_intent": 1}
    assert audit["test_cung_chu_voi_train"] == 1 and audit["test_cung_chu_voi_val"] == 0
    assert audit["val_cung_chu_voi_train"] == 1
    assert audit["trung_trong_test"] == 1


def test_the_audit_checks_the_files_really_used_for_fine_tuning(tmp_path):
    def line(question: str) -> str:
        user = json.dumps({"question": question}, ensure_ascii=False)
        return (
            json.dumps({"messages": [{"role": "system", "content": "s"}, {"role": "user", "content": user}]})
            + "\n"
        )

    (tmp_path / "train.jsonl").write_text(line("khác hẳn") + line("Câu của test"), encoding="utf-8")
    (tmp_path / "val.jsonl").write_text(line("khác nữa"), encoding="utf-8")

    found = slm_audit.finetune_overlap(tmp_path, {slm_audit.normalize_question("câu của test")})

    assert found == {"train": 1, "val": 0}
    assert slm_audit.finetune_overlap(tmp_path / "khong_co", set()) is None


def test_error_examples_are_chosen_by_a_fixed_rule_and_show_every_model():
    base = [row(f"x{i}", [y("knowledge")], [y("center_referral")]) for i in range(6)] + [
        fallback(f"f{i}", [y("knowledge")]) for i in range(4)
    ]
    other = [row(r["id"], r["nhan"]["intents"], r["nhan"]["intents"]) for r in base]  # model thứ hai đúng hết

    first = slm_audit.error_examples({"A": base, "B": other}, per_kind=2)
    second = slm_audit.error_examples({"A": base, "B": other}, per_kind=2)

    assert first == second
    assert {e["loi_cua_model_dau"] for e in first} == {"sai_loai", "json_loi"}
    assert all(len([e for e in first if e["loi_cua_model_dau"] == k]) == 2 for k in ("sai_loai", "json_loi"))
    assert all(set(e["du_doan"]) == {"A", "B"} and e["du_doan"]["B"]["loi"] == "dung_het" for e in first)
    assert any(e["du_doan"]["A"]["du_doan"] == "(no usable response)" for e in first)


# ---------------------------------------------------------------- lý do tin hỏng
def test_the_failure_reason_is_grouped_into_a_few_countable_causes():
    assert classify_failure(OllamaError("Ollama trả về nội dung không đọc được thành JSON")) == "invalid_json"
    assert classify_failure(OllamaError("Ollama trả về nội dung rỗng")) == "empty_output"
    assert classify_failure(OllamaError("Gọi Ollama thất bại: ReadTimeout")) == "timeout"
    assert classify_failure(ValueError("thiếu danh sách intents")) == "schema_violation"
    assert classify_failure(KeyError("intents")) == "malformed_structure"


# ---------------------------------------------------------------- báo cáo đầy đủ
def write_rows(path, rows):
    path.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")


def test_the_report_adds_valid_only_paired_audit_baseline_and_example_sections(tmp_path, monkeypatch):
    base = [
        *sample(3, 5),  # r0..r7
        fallback("r8", [y("knowledge", schools=["MIT"])]),
        fallback("r9", [y("knowledge", schools=["MIT"])]),
    ]
    better = sample(8, 2)
    tfidf = [row(r["id"], r["nhan"]["intents"], [y("knowledge")]) for r in better]
    for r in (*base, *better, *tfidf):
        r["split"] = "test"
    data = tmp_path / "du_lieu.jsonl"
    data.write_text(
        "".join(
            json.dumps(record(s, f"cau {s} {i}")) + "\n" for s in ("train", "val", "test") for i in range(3)
        ),
        encoding="utf-8",
    )
    paths = {n: tmp_path / f"{n}.jsonl" for n in ("a", "b", "t")}
    write_rows(paths["a"], base)
    write_rows(paths["b"], better)
    write_rows(paths["t"], tfidf)
    out = tmp_path / "bc"
    monkeypatch.setattr(
        sys,
        "argv",
        ["slm_report", "--run", f"A={paths['a']}", "--run", f"B={paths['b']}", "--baseline", f"T={paths['t']}",
         "--out", str(out), "--data", str(data), "--models-info", str(tmp_path / "khong_co.json")],
    )  # fmt: skip

    slm_report.main()

    for name in ("01_model_comparison.png", "01b_same_messages.png", "01c_paired_difference.png", "08_confidence_reliability.png", "04_confusion_matrix.png",
                 "table_own_valid.csv", "table_paired.csv", "error_examples.csv"):  # fmt: skip
        assert (out / name).is_file() and (out / name).stat().st_size > 0, name
    assert (
        not (out / "05b_invalid_causes.png").exists() or (out / "05b_invalid_causes.png").stat().st_size > 0
    )
    report = (out / "REPORT.md").read_text(encoding="utf-8")
    for heading in (
        "## Experiment setup",
        "## Understanding intent, each model's own usable responses",
        "## Understanding intent on the same messages (every model usable)",
        "## Paired comparison with A",
        "## Confidence calibration",
        "## Why responses were invalid",
        "## Reference baselines (category level only)",
        "## Data audit",
        "## Representative errors",
    ):
        assert heading in report, heading
    assert "McNemar" in report and "invalid_json" in report  # lý do tin hỏng được ghi khi file có loi_ly_do
    metrics = json.loads((out / "metrics.json").read_text(encoding="utf-8"))
    assert set(metrics) >= {"lan_chay", "chi_tin_hop_le", "so_cap", "moc_tham_chieu", "kiem_tra_du_lieu"}


# ---------------------------------------------------------------- cách chấm thực thể và tập giao
def test_entity_strings_differing_only_in_case_or_spacing_are_the_same_answer():
    gold = [y("knowledge", schools=["michigan state university"], tasks=["school_info"], fields=["gpa"])]
    pred = [y("knowledge", schools=["Michigan  State University"], tasks=["school_info"], fields=["gpa"])]

    verdict = judge(row("a", gold, pred))

    assert verdict["dung_het"] and verdict["loi"] == "dung_het"
    assert not verdict["dung_het_chat"]  # bản chặt hơn vẫn thấy khác biệt hoa thường


def test_a_really_different_entity_is_still_wrong_in_both_versions():
    gold = [y("knowledge", schools=["MIT"], tasks=["school_info"])]
    pred = [y("knowledge", schools=["Yale"], tasks=["school_info"])]

    verdict = judge(row("a", gold, pred))

    assert not verdict["dung_het"] and not verdict["dung_het_chat"] and verdict["loi"] == "sai_thuc_the"


def test_the_common_valid_set_keeps_only_messages_every_model_answered():
    a = [
        row("1", [y("knowledge")], [y("knowledge")]),
        fallback("2", [y("knowledge")]),
        row("3", [y("knowledge")], [y("knowledge")]),
    ]
    b = [
        row("1", [y("knowledge")], [y("knowledge")]),
        row("2", [y("knowledge")], [y("knowledge")]),
        fallback("3", [y("knowledge")]),
    ]

    common = common_valid({"A": a, "B": b})

    assert [r["id"] for r in common["A"]] == [r["id"] for r in common["B"]] == ["1"]


def test_a_label_with_f1_zero_reports_how_many_of_its_intents_were_invalid_responses():
    rows = [fallback(f"f{i}", [y("out_of_scope", subtype="farewell")]) for i in range(10)]
    table = per_label(rows, "task")

    found = slm_audit.zero_f1_labels(rows, table)

    assert found == [{"label": "out_of_scope/farewell", "so_y": 10, "trong_tin_hong": 10}]
    assert slm_audit.zero_f1_labels(rows, table, min_support=11) == []  # nhãn quá ít mẫu thì bỏ qua


def test_the_slowest_message_says_whether_its_response_was_usable():
    rows = [row("a", [y("knowledge")], [y("knowledge")], giay=1.0), fallback("b", [y("knowledge")])]
    rows[1]["giay"] = 163.6

    slowest = slm_audit.slowest_message(rows)

    assert slowest["giay"] == 163.6 and slowest["hop_le"] is False and slowest["ly_do"] == "invalid_json"
