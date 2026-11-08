"""Chỉ số và báo cáo đo SLM: chấm từng tin, F1 theo loại, ma trận nhầm lẫn, log huấn luyện, và dựng được báo cáo đầy đủ. Không cần Ollama."""

import json
import sys

import pytest

from scripts import slm_report
from scripts.slm_metrics import (
    NONE,
    align,
    bootstrap_ci,
    confusion,
    follow_up_scores,
    judge,
    load_rows,
    load_training_log,
    per_label,
    prf,
    summarize,
    training_summary,
)


def y(kind: str, doan: str = "x", **entities) -> dict:
    return {"type": kind, "doan": doan, "thuc_the": entities}


def row(row_id: str, gold: list, pred: list, **extra) -> dict:
    return {
        "id": row_id,
        "cau_hoi": "câu hỏi",
        "nhan": {"cac_y": gold},
        "du_doan": {"cac_y": pred},
        "confidence": 1.0,
        "giay": 1.0,
        **extra,
    }


# ---- chấm một tin ----
def test_a_perfect_answer_passes_every_level_and_the_wording_of_doan_does_not_matter():
    j = judge(
        row(
            "a",
            [y("kien_thuc", "học phí Harvard", truong="Harvard")],
            [y("kien_thuc", "Harvard học phí", truong="Harvard")],
        )
    )

    assert j["loi"] == "dung_het" and all(
        j[k] for k in ("hop_le", "dung_tap_loai", "dung_thu_tu", "dung_nhiem_vu", "dung_thuc_the", "dung_het")
    )


@pytest.mark.parametrize(
    ("gold", "pred", "error", "last_level"),
    [
        ([y("kien_thuc"), y("trung_tam")], [y("kien_thuc")], "thieu_y", None),
        ([y("kien_thuc")], [y("kien_thuc"), y("trung_tam")], "thua_y", None),
        ([y("kien_thuc")], [y("ho_so")], "sai_loai", None),
        ([y("kien_thuc"), y("trung_tam")], [y("trung_tam"), y("kien_thuc")], "sai_thu_tu", "dung_tap_loai"),
        (
            [y("ho_so", cac_nhiem_vu=["danh_gia"])],
            [y("ho_so", cac_nhiem_vu=["lo_trinh"])],
            "sai_nhiem_vu",
            "dung_thu_tu",
        ),
        ([y("kien_thuc", truong="MIT")], [y("kien_thuc", truong="Yale")], "sai_thuc_the", "dung_nhiem_vu"),
        (
            [y("kien_thuc", truong="MIT", tiep_noi=True)],
            [y("kien_thuc", truong="MIT")],
            "sai_tiep_noi",
            "dung_thuc_the",
        ),
    ],
)
def test_each_kind_of_mistake_is_named_and_stops_at_the_right_level(gold, pred, error, last_level):
    j = judge(row("a", gold, pred))

    assert j["loi"] == error and not j["dung_het"]
    levels = ["hop_le", "dung_tap_loai", "dung_thu_tu", "dung_nhiem_vu", "dung_thuc_the"]
    if last_level:  # các mức lỏng hơn mức này vẫn đúng, mức chặt hơn thì sai
        i = levels.index(last_level)
        assert all(j[k] for k in levels[: i + 1]) and not any(j[k] for k in levels[i + 1 :])


def test_a_fallback_answer_counts_as_unusable_whatever_it_says():
    j = judge(
        row(
            "a",
            [y("ngoai_pham_vi", loai_con="mo_ho")],
            [y("ngoai_pham_vi", loai_con="mo_ho")],
            lui_ve_mo_ho=True,
        )
    )

    assert j["loi"] == "json_loi" and not j["hop_le"] and not j["dung_het"]


def test_an_old_file_without_the_flag_uses_confidence_zero_as_the_fallback_mark():
    j = judge(row("a", [y("kien_thuc")], [y("kien_thuc")], confidence=0.0))

    assert j["loi"] == "json_loi"


def test_the_task_of_each_type_is_read_from_its_own_key():
    same = [
        (y("kich_ban", che_do="what_if"), y("kich_ban", che_do="goal_seeking")),
        (y("ngoai_pham_vi", loai_con="tam_biet"), y("ngoai_pham_vi", loai_con="mo_ho")),
        (y("mac_dinh"), y("mac_dinh", nhiem_vu="sua")),
        (y("trung_tam"), y("trung_tam", nhiem_vu="dat_lich")),
        (y("ho_so", cac_nhiem_vu=[]), y("ho_so", cac_nhiem_vu=["danh_gia"])),
    ]
    for g, p in same:
        assert judge(row("a", [g], [p]))["loi"] == "sai_nhiem_vu", g["type"]
    assert judge(
        row(
            "a",
            [y("ho_so", cac_nhiem_vu=["lo_trinh", "danh_gia"])],
            [y("ho_so", cac_nhiem_vu=["danh_gia", "lo_trinh"])],
        )
    )["dung_het"]  # thứ tự nhiệm vụ không quan trọng


# ---- F1 ----
def test_precision_recall_f1_count_every_intent_of_a_message():
    rows = [row("a", [y("kien_thuc"), y("kien_thuc")], [y("kien_thuc"), y("trung_tam")])]

    t = per_label(rows)

    assert (t["kien_thuc"]["tp"], t["kien_thuc"]["fn"], t["kien_thuc"]["fp"]) == (1, 1, 0)
    assert (
        t["kien_thuc"]["precision"] == 1
        and t["kien_thuc"]["recall"] == 0.5
        and round(t["kien_thuc"]["f1"], 3) == 0.667
    )
    assert (t["trung_tam"]["tp"], t["trung_tam"]["fp"], t["trung_tam"]["fn"]) == (0, 1, 0) and t["trung_tam"][
        "f1"
    ] == 0


def test_f1_by_type_and_task_label_differ_when_only_the_task_is_wrong():
    rows = [row("a", [y("kich_ban", che_do="what_if")], [y("kich_ban", che_do="goal_seeking")])]

    assert per_label(rows, "type")["kich_ban"]["f1"] == 1
    assert per_label(rows, "task")["kich_ban/what_if"]["f1"] == 0


def test_prf_handles_empty_counts():
    assert prf(0, 0, 0)["f1"] == 0 and prf(3, 0, 0)["f1"] == 1


# ---- ma trận nhầm lẫn ----
def test_the_confusion_matrix_pairs_intents_in_order_and_marks_missing_and_extra():
    rows = [
        row("a", [y("mac_dinh"), y("kien_thuc")], [y("mac_dinh"), y("ho_so")]),
        row("b", [y("kien_thuc"), y("trung_tam")], [y("kien_thuc")]),
        row("c", [y("kien_thuc")], [y("kien_thuc"), y("kich_ban")]),
    ]

    c = confusion(rows)
    at = {
        (g, p): c["matrix"][c["labels"].index(g)][c["labels"].index(p)]
        for g in c["labels"]
        for p in c["labels"]
    }

    assert at[("kien_thuc", "ho_so")] == 1  # nhầm loại
    assert at[("trung_tam", NONE)] == 1  # thiếu ý
    assert at[(NONE, "kich_ban")] == 1  # thừa ý
    assert at[("mac_dinh", "mac_dinh")] == 1 and at[("kien_thuc", "kien_thuc")] == 2


def test_align_pairs_equal_blocks_first():
    assert align(["a", "b", "c"], ["a", "c"]) == [(0, 0), (1, None), (2, 1)]
    assert align([], ["a"]) == [(None, 0)]


# ---- tiep_noi ----
def test_follow_up_scores_only_use_messages_that_have_context():
    ctx = {"truong": ["Harvard"]}
    rows = [
        row(
            "a", [y("kien_thuc", tiep_noi=True)], [y("kien_thuc", tiep_noi=True)], dang_noi_ve=ctx
        ),  # đúng, là nối tiếp
        row("b", [y("kien_thuc", tiep_noi=True)], [y("kien_thuc")], dang_noi_ve=ctx),  # bỏ sót nối tiếp
        row("c", [y("kien_thuc")], [y("kien_thuc", tiep_noi=True)], dang_noi_ve=ctx),  # nối tiếp nhầm
        row("d", [y("kien_thuc")], [y("kien_thuc")], dang_noi_ve=ctx),  # đúng, không nối tiếp
        row("e", [y("kien_thuc")], [y("kien_thuc", tiep_noi=True)]),  # không có ngữ cảnh: không tính
    ]

    s = follow_up_scores(rows)

    assert s["so_y"] == 4 and s["accuracy"] == 0.5 and s["precision"] == 0.5 and s["recall"] == 0.5
    assert follow_up_scores([row("e", [y("kien_thuc")], [y("kien_thuc")])]) is None


# ---- khoảng tin cậy ----
def test_the_confidence_interval_is_reproducible_and_contains_the_mean():
    values = [1.0] * 70 + [0.0] * 30

    lo, hi = bootstrap_ci(values)

    assert (lo, hi) == bootstrap_ci(values) and lo < 0.7 < hi and 0 <= lo and hi <= 1
    assert bootstrap_ci([1.0] * 20) == (1.0, 1.0) and bootstrap_ci([]) == (0.0, 0.0)


# ---- tổng hợp ----
def sample_rows(good: int, bad: int) -> list[dict]:
    """good tin đúng rồi bad tin sai (sai thực thể). Id là r0, r1, ... nên hai lần chạy cùng tổng số tin thì cùng một tập tin."""
    rows = [
        row(f"r{i}", [y("kien_thuc", truong="MIT")], [y("kien_thuc", truong="MIT")], giay=0.5 + i / 100)
        for i in range(good)
    ]
    rows += [
        row(
            f"r{good + i}",
            [y("kien_thuc", truong="MIT")],
            [y("kien_thuc", truong="Yale")],
            giay=1.0,
            dang_noi_ve={"truong": ["A"]},
        )
        for i in range(bad)
    ]
    return rows


def test_the_summary_counts_every_message_once_and_splits_by_group():
    s = summarize(sample_rows(6, 4))

    assert s["so_tin"] == 10 and sum(s["loi"].values()) == 10
    assert s["loi"]["dung_het"] == 6 and s["loi"]["sai_thuc_the"] == 4
    assert s["muc_dung"]["dung_het"] == 0.6 and s["muc_dung"]["dung_tap_loai"] == 1.0
    assert (
        s["theo_ngu_canh"]["co_ngu_canh"]["so_tin"] == 4
        and s["theo_ngu_canh"]["khong_ngu_canh"]["dung_het"] == 1.0
    )
    assert s["theo_so_y"]["1"]["so_tin"] == 10 and sorted(s["id_sai"]) == ["r6", "r7", "r8", "r9"]
    assert s["thoi_gian"]["p90"] >= s["thoi_gian"]["p50"]


def test_a_file_from_the_old_single_intent_format_is_rejected_with_a_clear_message(tmp_path):
    path = tmp_path / "cu.jsonl"
    path.write_text(
        json.dumps({"id": "x", "nhan": {"type": "mac_dinh"}, "du_doan": {"type": "mac_dinh"}}) + "\n",
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="bản cũ"):
        load_rows(path)


# ---- log huấn luyện ----
CSV_LOG = "step,epoch,train_loss,eval_loss,val_dung_het\n10,0.1,2.0,,\n20,0.2,1.2,,\n30,0.3,,1.0,0.40\n40,0.4,0.8,,\n60,0.6,,0.7,0.62\n80,0.8,0.5,,\n90,0.9,,0.9,0.61\n"


def test_the_training_log_csv_is_read_with_blank_cells(tmp_path):
    path = tmp_path / "training_log.csv"
    path.write_text(CSV_LOG, encoding="utf-8")

    log = load_training_log(path)

    assert [p["step"] for p in log] == [10, 20, 30, 40, 60, 80, 90]
    assert (
        log[0]["train_loss"] == 2.0
        and "eval_loss" not in log[0]
        and log[2]["eval_loss"] == 1.0
        and log[2]["val_dung_het"] == 0.40
    )


def test_the_huggingface_trainer_state_is_read_the_same_way(tmp_path):
    state = {
        "log_history": [
            {"step": 10, "epoch": 0.1, "loss": 2.0},
            {"step": 30, "epoch": 0.3, "eval_loss": 1.0, "eval_runtime": 3.2, "eval_dung_het": 0.4},
        ]
    }
    path = tmp_path / "trainer_state.json"
    path.write_text(json.dumps(state), encoding="utf-8")

    log = load_training_log(path)

    assert log == [
        {"step": 10.0, "epoch": 0.1, "train_loss": 2.0},
        {"step": 30.0, "epoch": 0.3, "eval_loss": 1.0, "val_dung_het": 0.4},
    ]


def test_the_training_summary_finds_the_best_step_and_flags_overfit_or_not_converged(tmp_path):
    path = tmp_path / "training_log.csv"
    path.write_text(CSV_LOG, encoding="utf-8")

    s = training_summary(load_training_log(path))

    assert s["buoc_tot_nhat"] == 60 and s["val_loss_tot_nhat"] == 0.7 and s["val_loss_cuoi"] == 0.9
    assert s["overfit"] is True and s["chua_hoi_tu"] is False
    assert round(s["chenh_val_train_tai_tot_nhat"], 3) == round(0.7 - 0.8, 3)

    still_falling = [
        {"step": 1.0, "train_loss": 2.0},
        {"step": 2.0, "eval_loss": 1.5},
        {"step": 3.0, "eval_loss": 1.2},
    ]
    s2 = training_summary(still_falling)
    assert s2["chua_hoi_tu"] is True and s2["overfit"] is False
    assert training_summary([{"step": 1.0, "train_loss": 2.0}]) == {"co_val": False}


# ---- báo cáo đầy đủ ----
def write_predictions(path, rows):
    path.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")


def test_the_report_builds_every_table_and_chart(tmp_path, monkeypatch):
    base, tuned = tmp_path / "goc.jsonl", tmp_path / "moi.jsonl"
    write_predictions(base, sample_rows(3, 7))
    write_predictions(tuned, sample_rows(9, 1))  # cùng 10 tin (r0..r9), model mới sai ít hơn
    log = tmp_path / "training_log.csv"
    log.write_text(CSV_LOG, encoding="utf-8")
    data = tmp_path / "du_lieu.jsonl"
    data.write_text(
        "".join(
            json.dumps({"type": t, "split": s}) + "\n"
            for t in ("kien_thuc", "ho_so")
            for s in ("train", "val", "test")
        ),
        encoding="utf-8",
    )
    out = tmp_path / "bao_cao"
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "slm_report",
            "--run",
            f"Model gốc={base}",
            "--run",
            f"Đã huấn luyện={tuned}",
            "--out",
            str(out),
            "--log",
            str(log),
            "--baseline-val-loss",
            "1.8",
            "--data",
            str(data),
        ],
    )

    slm_report.main()

    for name in (
        "BAO_CAO.md", "chi_so.json", "bang_so_sanh.csv", "bang_theo_loai.csv", "bang_theo_nhan.csv", "nham_lan_1.csv", "cau_sai_2.csv",
        "01_so_sanh.png", "02_f1_theo_loai.png", "03_f1_theo_nhan.png", "04_nham_lan.png", "05_phan_loai_ket_qua.png", "06_theo_nhom_tin.png",
        "07_thoi_gian.png", "09_du_lieu.png", "10_huan_luyen.png", "11_chenh_val_train.png", "12_chi_so_val_theo_buoc.png",
    ):  # fmt: skip
        assert (out / name).is_file() and (out / name).stat().st_size > (
            3000 if name.endswith(".png") else 0
        ), name
    report = (out / "BAO_CAO.md").read_text(encoding="utf-8")
    assert (
        "Model gốc" in report
        and "Đã huấn luyện" in report
        and "30.0%" in report
        and "90.0%" in report
        and "Overfit" in report
    )
    metrics = json.loads((out / "chi_so.json").read_text(encoding="utf-8"))
    assert (
        metrics["lan_chay"]["Model gốc"]["muc_dung"]["dung_het"] == 0.3
        and metrics["huan_luyen"]["buoc_tot_nhat"] == 60
    )
    assert (out / "cau_sai_1.csv").read_text(encoding="utf-8-sig").count(
        "\n"
    ) == 8  # tiêu đề + 7 câu sai của model gốc


def test_runs_on_different_sets_of_messages_are_refused_because_the_comparison_would_be_unfair(
    tmp_path, monkeypatch
):
    a, b = tmp_path / "a.jsonl", tmp_path / "b.jsonl"
    write_predictions(a, sample_rows(3, 0))
    write_predictions(b, sample_rows(2, 0))
    monkeypatch.setattr(
        sys, "argv", ["slm_report", "--run", f"A={a}", "--run", f"B={b}", "--out", str(tmp_path / "o")]
    )

    with pytest.raises(SystemExit, match="không đo cùng một tập"):
        slm_report.main()


def test_a_run_argument_needs_a_name_and_a_path():
    with pytest.raises(Exception, match="Tên=đường dẫn"):
        slm_report.parse_run("khong_co_dau_bang")
    assert slm_report.parse_run("Model gốc=a/b.jsonl")[0] == "Model gốc"


# ---- tập đã đo: val để phát triển, test để báo cáo cuối ----
def test_the_split_is_read_from_the_rows_or_from_the_file_name(tmp_path):
    from pathlib import Path

    assert slm_report.split_of(Path("x_val.jsonl"), [{"id": "a"}]) == "val"
    assert slm_report.split_of(Path("x_test.jsonl"), [{"id": "a"}]) == "test"
    assert (
        slm_report.split_of(Path("ten_la.jsonl"), [{"id": "a", "split": "val"}]) == "val"
    )  # cột split ưu tiên hơn tên file
    assert slm_report.split_of(Path("ten_la.jsonl"), [{"id": "a"}]) == "không rõ"


def run_report(tmp_path, monkeypatch, base_name, tuned_name, base_split=None, tuned_split=None):
    def with_split(rows, split):
        return [{**r, "split": split} for r in rows] if split else rows

    base, tuned = tmp_path / base_name, tmp_path / tuned_name
    write_predictions(base, with_split(sample_rows(3, 7), base_split))
    write_predictions(tuned, with_split(sample_rows(9, 1), tuned_split))
    out = tmp_path / "bc"
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "slm_report",
            "--run",
            f"A={base}",
            "--run",
            f"B={tuned}",
            "--out",
            str(out),
            "--data",
            str(tmp_path / "khong_co.jsonl"),
        ],
    )
    slm_report.main()
    return (out / "BAO_CAO.md").read_text(encoding="utf-8")


def test_a_report_on_val_says_it_is_for_development_not_the_final_number(tmp_path, monkeypatch):
    report = run_report(tmp_path, monkeypatch, "goc_val.jsonl", "moi_val.jsonl")

    assert "tập val" in report and "KHÔNG phải số báo cáo cuối cùng" in report


def test_a_report_on_test_says_it_is_the_final_number(tmp_path, monkeypatch):
    report = run_report(tmp_path, monkeypatch, "goc.jsonl", "moi.jsonl", "test", "test")

    assert "tập test" in report and "số liệu báo cáo cuối cùng" in report


def test_runs_measured_on_different_splits_are_refused(tmp_path, monkeypatch):
    with pytest.raises(SystemExit, match="các tập khác nhau"):
        run_report(tmp_path, monkeypatch, "goc.jsonl", "moi.jsonl", "val", "test")
