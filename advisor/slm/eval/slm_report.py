"""Báo cáo đo SLM: so sánh các lần chạy (model gốc và model đã huấn luyện) trên cùng một tập, kèm bảng CSV và biểu đồ.

Bước 1: chạy slm.eval.eval_slm cho từng model, cùng một tập (test), ra slm/results/ket_qua/<model>[_shotsN]_<tập>.jsonl:
  python -m slm.eval.eval_slm --model qwen3:4b-instruct --prompt slm_intents --shots 8 --split test
  python -m slm.eval.eval_slm --model usas-slm-v4 --prompt slm_intents --split test
Bước 2: dựng báo cáo. Lần chạy ĐẦU TIÊN là model gốc (baseline), các lần sau là model đã huấn luyện:
Công cụ dùng được cho cả hai tập: --split val để phát triển (chọn prompt, chọn epoch), --split test MỘT LẦN để lấy số báo cáo cuối.
Báo cáo tự ghi đúng tập đã đo. Ví dụ dựng báo cáo trên test:
  python -m slm.eval.slm_report --run "Model gốc=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" \\
      --run "Đã huấn luyện=slm/results/ket_qua/usas-slm-v4_test.jsonl" --out slm/results/bao_cao/2026-11-10_v4_test_ban_thu \\
      --log training_log.csv --config "Model gốc: ..." --config "Đã huấn luyện: ..."
Thêm đường cong huấn luyện (log từ notebook Colab: training_log.csv hoặc trainer_state.json) và val loss của model gốc:
  ... --log training_log.csv --baseline-val-loss 2.31
Thêm mốc tham chiếu (tạo bằng slm.eval.baseline_tfidf) và bảng tiêu chí chọn model (slm/eval/model_info.json, mặc định tự dùng):
  ... --baseline "TF-IDF + LR=slm/results/ket_qua/baseline_tfidf_test.jsonl" --models-info slm/eval/model_info.json

Mỗi lần dựng báo cáo ra MỘT thư mục tự chứa đủ để xem lại và kiểm tra, đặt tên --out theo dạng
  slm/results/bao_cao/<YYYY-MM-DD>_<model>_<tập>_<ban_thu|chinh_thuc>
  REPORT.md          báo cáo tổng hợp (đọc trước)
  manifest.json       lần chạy này dùng gì: ngày, commit git (và có sửa chưa commit không), cấu hình, lệnh, mã băm sha256 của đáp án thô, log và dữ liệu
  metrics.json         toàn bộ chỉ số dạng máy đọc
  *.csv, *.png        bảng (mở bằng Excel) và biểu đồ có chú thích
  raw_predictions/        bản sao đáp án thô của từng model đã dùng để dựng báo cáo
  <log huấn luyện>    bản sao log (nếu có --log)
Bản thử không commit (thư mục bao_cao và ket_qua nằm trong .gitignore); số chính thức thì commit bằng git add -f.
"""

import argparse
import hashlib
import json
import re
import shlex
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path
from typing import Any

from slm.eval import slm_audit
from slm.eval import slm_charts as charts
from slm.eval import slm_metrics as m
from slm.prepare.build_slm_jsonl import DEFAULT_OUT

MODEL_INFO = Path(__file__).with_name("model_info.json")

LEVEL_TEXT = {
    "hop_le": "Valid (usable) response",
    "dung_tap_loai": "Correct category (order ignored)",
    "dung_thu_tu": "Correct category and order",
    "dung_nhiem_vu": "Correct task per intent",
    "dung_thuc_the": "Correct entities",
    "dung_het": "Exact match",
}


SPLIT_NOTES = {
    "test": "test set (held out, never used for training or epoch selection): final reported numbers",
    "val": "validation set (used to monitor training, pick the epoch and tune prompts): development numbers, NOT the final reported numbers",
    "train": "train set (the model has seen it, so scores do not reflect generalization)",
}


# Tập đã đo của một lần chạy: cột split của file dự đoán, không có thì lấy từ đuôi tên file (<model>_val.jsonl)
def split_of(path: Path, rows: list[dict[str, Any]]) -> str:
    named = {r["split"] for r in rows if r.get("split")}
    if len(named) == 1:
        return named.pop()
    for name in SPLIT_NOTES:
        if path.stem.endswith(f"_{name}"):
            return name
    return "unknown"


def parse_run(text: str) -> tuple[str, Path]:
    name, sep, path = text.partition("=")
    if not sep or not name.strip() or not path.strip():
        raise argparse.ArgumentTypeError(
            '--run phải có dạng "Tên=đường dẫn file", ví dụ "Model gốc=slm/results/ket_qua/x_test.jsonl"'
        )
    return name.strip(), Path(path.strip())


def percent(v: float | None) -> str:
    return "-" if v is None else f"{v:.1%}"


def ci_text(value: float, lo: float, hi: float, digits: int = 1) -> str:
    return f"{value:.{digits}%} ({lo:.{digits}%} - {hi:.{digits}%})"


def comparison_table(summaries: dict[str, dict[str, Any]]) -> tuple[list[str], list[list[Any]]]:
    """Bảng so sánh trên TẤT CẢ tin: tin model không trả được kết quả dùng được tính là sai ở mọi mức."""
    names = list(summaries)
    rows: list[list[Any]] = []
    for key in m.LEVELS:
        row: list[Any] = [LEVEL_TEXT[key]]
        for s in summaries.values():
            lo, hi = s["khoang_tin_cay_95"][key]
            row.append(ci_text(s["muc_dung"][key], lo, hi))
        rows.append(row)
    for label, key in (("Macro-F1 per category", "macro_f1"), ("Micro-F1 per category", "micro_f1")):
        rows.append(
            [label]
            + [
                f"{s['f1_loai'][key]:.3f} ({s['khoang_tin_cay_f1'][key][0]:.3f} - {s['khoang_tin_cay_f1'][key][1]:.3f})"
                for s in summaries.values()
            ]
        )
    rows.append(
        ["Macro-F1 per category/task label"] + [f"{s['f1_nhan']['macro_f1']:.3f}" for s in summaries.values()]
    )
    rows.append(
        ["follow_up: accuracy"]
        + [percent(s["follow_up"]["accuracy"]) if s["follow_up"] else "-" for s in summaries.values()]
    )
    rows.append(
        ["follow_up: F1 of follow-up messages"]
        + [f"{s['follow_up']['f1']:.3f}" if s["follow_up"] else "-" for s in summaries.values()]
    )
    for key in m.ERRORS[1:]:
        rows.append(
            [f"Error: {charts.ERROR_NAMES[key]} (messages)"]
            + [str(s["loi"][key]) for s in summaries.values()]
        )
    for label, key in (
        ("Mean latency (s/message)", "trung_binh"),
        ("P90 latency (s/message)", "p90"),
        ("P95 latency (s/message)", "p95"),
        ("Slowest message (s)", "max"),
    ):
        rows.append(
            [label] + [f"{s['thoi_gian'][key]:.2f}" if s["thoi_gian"] else "-" for s in summaries.values()]
        )
    rows.insert(
        m.LEVELS.index("dung_het") + 1,
        ["Exact match, case-sensitive (stricter)"]
        + [percent(s["muc_dung"]["dung_het_chat"]) for s in summaries.values()],
    )
    return ["Metric"] + names, rows


VALID_LEVELS = ("dung_tap_loai", "dung_nhiem_vu", "dung_thuc_the", "dung_het")


def valid_table(valid: dict[str, dict[str, Any]]) -> tuple[list[str], list[list[Any]]]:
    """Bảng chỉ tính các tin model trả được kết quả dùng được: đo khả năng HIỂU Ý, tách khỏi khả năng tuân thủ định dạng."""
    rows: list[list[Any]] = [["Messages with a usable response"] + [str(s["so_tin"]) for s in valid.values()]]
    for key in VALID_LEVELS:
        row: list[Any] = [LEVEL_TEXT[key]]
        for s in valid.values():
            lo, hi = s["khoang_tin_cay_95"][key]
            row.append(ci_text(s["muc_dung"][key], lo, hi))
        rows.append(row)
    for label, key in (("Macro-F1 per category", "macro_f1"), ("Micro-F1 per category", "micro_f1")):
        rows.append(
            [label]
            + [
                f"{s['f1_loai'][key]:.3f} ({s['khoang_tin_cay_f1'][key][0]:.3f} - {s['khoang_tin_cay_f1'][key][1]:.3f})"
                for s in valid.values()
            ]
        )
    return ["Metric (usable responses only)"] + list(valid), rows


def paired_table(comparisons: dict[str, dict[str, Any]], base: str) -> tuple[list[str], list[list[Any]]]:
    """Bảng so cặp: model đầu tiên với từng model khác trên cùng các tin. p của McNemar chính xác (hai phía) cho tỉ lệ."""
    header = [
        "Compared with",
        "Metric",
        f"{base}",
        "Other",
        "Difference (pp)",
        "95% paired CI (pp)",
        f"Only {base} right",
        "Only other right",
        "McNemar p",
    ]
    rows: list[list[Any]] = []
    for other, c in comparisons.items():
        for key, label in (
            ("valid", "Valid response"),
            ("category", "Correct category"),
            ("exact", "Exact match"),
        ):
            r = c[key]
            lo, hi = r["khoang_tin_cay_95"]
            p_text = "< 0.001" if r["p"] < 0.001 else f"{r['p']:.3f}"
            rows.append(
                [other, label, f"{r['a']:.1%}", f"{r['b']:.1%}", f"{r['hieu'] * 100:+.1f}",
                 f"{lo * 100:+.1f} to {hi * 100:+.1f}", r["only_a"], r["only_b"], p_text]
            )  # fmt: skip
        r = c["macro_f1"]
        lo, hi = r["khoang_tin_cay_95"]
        rows.append(
            [other, "Macro-F1", f"{r['a']:.3f}", f"{r['b']:.3f}", f"{r['hieu'] * 100:+.1f}",
             f"{lo * 100:+.1f} to {hi * 100:+.1f}", "-", "-", "-"]
        )  # fmt: skip
    return header, rows


def wrong_cases(rows: list[dict[str, Any]]) -> list[list[Any]]:
    out = []
    for r in rows:
        j = m.judge(r)
        if j["dung_het"]:
            continue
        out.append(
            [
                r["id"],
                j["loi"],
                r["question"],
                json.dumps(r["nhan"]["intents"], ensure_ascii=False),
                json.dumps(r["du_doan"]["intents"], ensure_ascii=False),
            ]
        )
    return out


def shots_of(path: Path, rows: list[dict[str, Any]]) -> str:
    recorded = (rows[0].get("cai_dat") or {}).get("shots") if rows else None
    if recorded is not None:
        return str(recorded)
    found = re.search(r"_shots(\d+)", path.stem)
    return found.group(1) if found else "0"


def setup_lines(
    run_files: list[tuple[str, Path]], runs: dict[str, list[dict[str, Any]]]
) -> tuple[list[str], list[list[Any]], str]:
    """Bảng thiết lập thí nghiệm lấy từ chính file dự đoán. Điều kiện chạy chỉ có khi file tạo bằng eval_slm bản mới (có cai_dat);
    file cũ không lưu nên ghi rõ điều đó và nêu mặc định hiện tại của mã để người đọc đối chiếu."""
    header = ["Run", "Model tag", "Prompt", "Examples (shots)", "Messages", "Runtime settings"]
    rows: list[list[Any]] = []
    recorded_all = True
    for name, path in run_files:
        first = runs[name][0]
        settings = first.get("cai_dat")
        recorded_all = recorded_all and bool(settings)
        text = (
            ", ".join(f"{k}={v}" for k, v in settings.items() if k not in ("shots", "prompt_version"))
            if settings
            else "not recorded in this file"
        )
        prompt = f"{first.get('prompt', '?')}" + (f" {settings['prompt_version']}" if settings else "")
        rows.append(
            [name, first.get("model", "?"), prompt, shots_of(path, runs[name]), len(runs[name]), text]
        )
    note = (
        "Runtime settings were recorded by the evaluation script for every run."
        if recorded_all
        else "Runtime settings are stored only by the newer evaluation script. For files without them, the code defaults are: "
        "temperature 0.0, thinking disabled, JSON-formatted output, token log-probabilities on. "
        "Hardware, quantization and inference-engine version are not stored in any file: add them with --config."
    )
    return header, rows, note


def models_table(
    summaries: dict[str, dict[str, Any]], runs: dict[str, list[dict[str, Any]]], info: dict[str, Any]
) -> tuple[list[str], list[list[Any]]]:
    """Bảng tiêu chí chọn model: thông số công bố (model_info.json) cạnh kết quả đo. null/thiếu hiện n/a."""

    def cell(value: Any) -> str:
        return "n/a" if value in (None, "") else str(value)

    header = [
        "Model", "Parameters", "Context (tokens)", "License", "File size (GB)", "VRAM (GB)",
        "Tokens per Vietnamese word", "Valid response", "Macro-F1", "Mean latency (s)",
    ]  # fmt: skip
    rows: list[list[Any]] = []
    for name, s in summaries.items():
        data = info.get(runs[name][0].get("model", ""), {})
        rows.append(
            [data.get("display", name), cell(data.get("params")), cell(data.get("context")), cell(data.get("license")),
             cell(data.get("size_gb")), cell(data.get("vram_gb")), cell(data.get("tokens_per_vi_word")),
             f"{s['muc_dung']['hop_le']:.1%}", f"{s['f1_loai']['macro_f1']:.3f}",
             f"{s['thoi_gian']['trung_binh']:.2f}" if s["thoi_gian"] else "n/a"]
        )  # fmt: skip
    return header, rows


def table_md(header: list[str], rows: list[list[Any]]) -> list[str]:
    return [
        "| " + " | ".join(header) + " |",
        "|" + "---|" * len(header),
        *["| " + " | ".join(map(str, r)) + " |" for r in rows],
    ]


def markdown(
    summaries: dict[str, dict[str, Any]],
    split_note: str,
    training: dict[str, Any] | None,
    made: list[str],
    config: list[str] | None = None,
    extras: dict[str, Any] | None = None,
) -> str:
    extras = extras or {}
    header, rows = comparison_table(summaries)
    lines = [
        "# SLM Intent Extraction Evaluation Report",
        "",
        f"Evaluation set: {split_note}. Messages: {', '.join(f'{n} = {s["so_tin"]}' for n, s in summaries.items())}.",
        "The first run is the reference model; later runs are compared against it. Same set, same scoring.",
        f"Report date: {date.today().isoformat()}.",
        "",
    ]
    if extras.get("setup"):
        sh, sr, note = extras["setup"]
        lines += ["## Experiment setup", "", *table_md(sh, sr), "", note, ""]
    if config:
        lines += ["## Experiment configuration", "", *[f"- {c}" for c in config], ""]
    lines += [
        "## Comparison (all messages)",
        "",
        "A message whose response could not be used counts as wrong at every level, so this table mixes format compliance with intent understanding. The next table separates them.",
        "",
        *table_md(header, rows),
        "",
        "Values in parentheses are 95% confidence intervals (bootstrap over messages, 1000 resamples). If two intervals do not overlap, the difference is significant.",
        "Message-level rows count messages; F1 rows count intents (one message can hold several). "
        '"Correct category and order" equals "Correct category" unless a message has two or more intents (89% of messages have one).',
    ]
    if extras.get("valid"):
        vh, vr = valid_table(extras["valid"])
        lines += [
            "",
            "## Understanding intent, each model's own usable responses",
            "",
            "Only the messages for which each model returned a usable response. The message sets differ between models "
            "(a model that often fails may keep only the easy messages), so these columns are not directly comparable. "
            "Use the next table for a fair comparison.",
            "",
            *table_md(vh, vr),
        ]
    if extras.get("common"):
        ch, cr = valid_table(extras["common"])
        lines += [
            "",
            "## Understanding intent on the same messages (every model usable)",
            "",
            "Only the messages for which ALL models returned a usable response, so every model is scored on exactly the same messages. "
            "This is the fair comparison of how well the models understand the intent, apart from output-format failures.",
            "",
            *table_md(ch, cr),
        ]
    if extras.get("paired"):
        ph, pr = paired_table(extras["paired"], extras["base"])
        lines += [
            "",
            f"## Paired comparison with {extras['base']}",
            "",
            "Same messages, so the comparison is paired: the McNemar exact test uses only the messages where the two models differ, "
            "and the confidence interval of the difference comes from a paired bootstrap. A p-value below 0.05 and an interval that "
            "does not contain 0 both indicate a real difference.",
            "",
            *table_md(ph, pr),
        ]
    lines += ["", "## Confidence calibration", ""]
    ece_common = extras.get("ece_common", {})
    lines += [
        "Confidence is the probability the model gave to its predicted intent categories (lowest intent in the message). "
        "It is compared with the share of messages whose category was actually correct. ECE (expected calibration error) is 0 for a perfectly "
        "calibrated score. Invalid responses are excluded because their confidence 0 is a system default. "
        "ECE on the common messages uses the same messages for every model.",
        "",
        *table_md(
            ["Model", "ECE (own usable responses)", "Usable responses", "ECE (common messages)"],
            [
                [
                    n,
                    f"{s['hieu_chuan']['ece']:.3f}" if s["hieu_chuan"] else "n/a",
                    s["so_tin_hop_le"],
                    f"{ece_common[n]:.3f}" if ece_common.get(n) is not None else "n/a",
                ]
                for n, s in summaries.items()
            ],
        ),
    ]
    reasons = {n: s["ly_do_loi"] for n, s in summaries.items() if s.get("ly_do_loi")}
    lines += ["", "## Why responses were invalid", ""]
    if reasons:
        keys = sorted({k for d in reasons.values() for k in d})
        lines += table_md(["Model", *keys], [[n, *[d.get(k, 0) for k in keys]] for n, d in reasons.items()])
    else:
        lines.append(
            "The prediction files do not record why a response failed. Re-run slm.eval.eval_slm (current version) to record the cause "
            "(timeout, invalid JSON, schema violation, ...) for every invalid response."
        )
    zero = {n: z for n, z in extras.get("zero_f1", {}).items() if z}
    if zero:
        lines += [
            "",
            "## Labels with F1 = 0",
            "",
            "Labels with at least 10 intents and F1 = 0. If most or all of their intents sit in invalid responses, the model did not "
            "predict the wrong label: it returned no usable answer for those messages.",
            "",
            *table_md(
                ["Model", "Label", "Intents", "In invalid responses"],
                [[n, z["label"], z["so_y"], z["trong_tin_hong"]] for n, items in zero.items() for z in items],
            ),
        ]
    slowest = {n: v for n, v in extras.get("slowest", {}).items() if v}
    if slowest:
        lines += [
            "",
            "## Latency notes",
            "",
            "Latency depends on the hardware and on the length of the output; neither the hardware nor the output token count is stored "
            "in these prediction files. The slowest message of each model:",
            "",
            *table_md(
                ["Model", "Seconds", "Usable response", "Message"],
                [
                    [n, f"{v['giay']:.1f}", "yes" if v["hop_le"] else "no", v["cau"].replace("|", "/")[:70]]
                    for n, v in slowest.items()
                ],
            ),
        ]
    if extras.get("baselines"):
        bh, br = extras["baselines"]
        lines += [
            "",
            "## Reference baselines (category level only)",
            "",
            "The baselines predict only the intent category, never tasks or entities, so only category-level numbers are comparable. "
            "Majority always answers the most frequent category of the training set (a floor); TF-IDF + logistic regression is a simple trained classifier.",
            "",
            *table_md(bh, br),
        ]
    if extras.get("models"):
        mh, mr = extras["models"]
        lines += [
            "",
            "## Model selection criteria",
            "",
            *table_md(mh, mr),
            "",
            "Parameters, context length and license come from the published model cards; n/a means not yet measured "
            "(measure file size with `ollama list` and VRAM with `nvidia-smi` on the machine that ran the evaluation).",
        ]
    if extras.get("audit"):
        a = extras["audit"]
        rows_audit = [
            [name, st["messages"], st["intents"], st["multi_intent"]]
            for name, st in sorted(a["splits"].items())
        ]
        lines += [
            "",
            "## Data audit",
            "",
            *table_md(["Split", "Messages", "Intents", "Messages with 2+ intents"], rows_audit),
            "",
            f"Questions of the test set that also appear (case- and spacing-insensitive) in train: {a['test_cung_chu_voi_train']}, "
            f"in validation: {a['test_cung_chu_voi_val']}; validation questions also in train: {a['val_cung_chu_voi_train']}; "
            f"repeated questions inside the test set: {a['trung_trong_test']}.",
            "These are counts in the raw labelled data. Repeats inside the test set are mostly short replies that occur in different contexts.",
            *(
                [
                    "The files actually used for fine-tuning drop every question that also appears in the test set: "
                    + ", ".join(f"{k}.jsonl contains {v}" for k, v in a["file_huan_luyen_trung_test"].items())
                    + " test questions. Few-shot examples are also drawn from train excluding test questions."
                ]
                if a.get("file_huan_luyen_trung_test")
                else []
            ),
            "Chart 09 counts messages by the type of their first intent; the F1 charts count intents, so a category can have more intents than messages.",
        ]
    if extras.get("examples"):
        examples = extras["examples"]
        names = list(examples[0]["du_doan"]) if examples else []
        lines += [
            "",
            "## Representative errors",
            "",
            f"Up to two messages for each error type of the first run ({names[0] if names else ''}), chosen by a fixed hash of the message id (not by hand). "
            "Full text of every wrong message is in wrong_messages_*.csv.",
            "",
            *table_md(
                ["Error", "Message", "Correct label", *[f"{n}" for n in names]],
                [
                    [
                        e["loi_cua_model_dau"],
                        e["cau"].replace("|", "/")[:80],
                        e["nhan_dung"].replace("|", "/"),
                        *[
                            f"{e['du_doan'][n]['loi']}: {e['du_doan'][n]['du_doan']}".replace("|", "/")
                            if n in e["du_doan"]
                            else "-"
                            for n in names
                        ],
                    ]
                    for e in examples
                ],
            ),
        ]
    if training:
        lines += ["", "## Training", ""]
        if not training.get("co_val"):
            lines.append("The log has no val loss.")
        else:
            lines += [
                f"- Lowest val loss: {training['val_loss_tot_nhat']:.4f} at step {training['buoc_tot_nhat']:.0f}"
                + (
                    f" (epoch {training['epoch_tot_nhat']:.2f})"
                    if training.get("epoch_tot_nhat") is not None
                    else ""
                ),
                f"- Val loss at the last evaluation: {training['val_loss_cuoi']:.4f} (number of val evaluations: {training['so_lan_do_val']})",
                *(
                    [
                        f"- Val loss minus train loss: {training['chenh_val_train_tai_tot_nhat']:.4f} at the lowest point"
                        + (
                            f", {training['chenh_val_train_cuoi']:.4f} at the last evaluation"
                            if training.get("chenh_val_train_cuoi") is not None
                            else ""
                        )
                        + " (a steady increase over evaluations signals overfitting)"
                    ]
                    if training.get("chenh_val_train_tai_tot_nhat") is not None
                    else []
                ),
                f"- Overfitting (val loss rises more than 5% after its lowest point): {'yes' if training['overfit'] else 'not observed'}",
                f"- Converged (the lowest point is not the last evaluation): {'not yet, val loss was still falling at the end' if training['chua_hoi_tu'] else 'yes'}",
            ]
    lines += [
        "",
        "## Method",
        "",
        "- The unit of measurement is one message. Accuracy levels run from loose to strict, each including the previous one: "
        "usable response, correct category, correct order, correct task, correct entities, exact match (including follow_up). "
        "The span text is not compared; only category, task (tasks, mode, subtype), entities and follow_up are.",
        "- Precision, recall and F1 are computed per intent (one message can contain several intents), per category and per "
        "category/task label (a profile_review intent with several tasks counts once per task). A response that fell back to "
        '"ambiguous" because the model failed is not a prediction: its true intents count as missed and it adds no extra intent. '
        "An intent without tasks counts toward the category's default task (collect_profile/answer, knowledge/general, center_referral/find).",
        "- The confusion matrix uses the same counting as precision and recall, with an extra column (invalid) for failed responses.",
        "- Entity strings are compared ignoring letter case and extra spaces, because school, city and center names are matched "
        "case-insensitively downstream (the stricter case-sensitive exact match is reported as a separate row).",
        "- Macro-F1 and Micro-F1 intervals resample whole messages (1000 resamples, fixed seed). Group rates use Wilson intervals.",
        "- The test set is never used for training; the best checkpoint and early stopping (if any) are chosen on val, not on test.",
        "",
        "## Files",
        "",
        *[f"- {name}" for name in made],
    ]
    return "\n".join(lines) + "\n"


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git_output(*args: str) -> str | None:
    """Kết quả một lệnh git trong repo; None nếu không chạy trong repo git hoặc không có git."""
    try:
        done = subprocess.run(  # noqa: S603
            ["git", *args],  # noqa: S607
            capture_output=True,
            text=True,
            cwd=Path(__file__).resolve().parent,
            timeout=10,
            check=True,
        )
    except (OSError, subprocess.SubprocessError):
        return None
    return done.stdout.strip()


def git_state() -> dict[str, Any]:
    """Commit đang đứng và mã nguồn đã sửa chưa commit hay chưa (file mới chưa theo dõi không tính)."""
    commit = git_output("rev-parse", "--short", "HEAD") or None
    changes = git_output("status", "--porcelain", "--untracked-files=no")
    return {"git_commit": commit, "co_sua_chua_commit": None if changes is None else bool(changes)}


def archive_inputs(
    out: Path, run_files: list[tuple[str, Path]], log: Path | None, data: Path
) -> tuple[dict[str, Any], list[str]]:
    """Chép đáp án thô và log huấn luyện vào thư mục báo cáo, trả về (phần manifest, tên các file đã chép).
    Báo cáo nhờ vậy tự chứa đủ đầu vào, kèm mã băm để biết sau này file có bị đổi không."""
    names = [path.name for _, path in run_files]
    if len(set(names)) != len(names):
        raise SystemExit(
            f"Hai lần chạy trùng tên file {names}: không chép được vào raw_predictions. Đổi tên một file."
        )
    raw_dir = out / "raw_predictions"
    raw_dir.mkdir(parents=True, exist_ok=True)
    for _, path in run_files:
        shutil.copy2(path, raw_dir / path.name)
    info: dict[str, Any] = {
        "dap_an_tho": [
            {"ten": name, "file": f"raw_predictions/{path.name}", "sha256": sha256_of(path)}
            for name, path in run_files
        ],
        "log_huan_luyen": None,
        "du_lieu": {"file": data.name, "sha256": sha256_of(data)} if data.is_file() else None,
    }
    made = [f"raw_predictions/ (copies of the raw predictions: {', '.join(names)})"]
    if log:
        target = out / log.name
        if log.resolve() != target.resolve():
            shutil.copy2(log, target)
        info["log_huan_luyen"] = {"file": log.name, "sha256": sha256_of(log)}
        made.append(f"{log.name}: training log (copy)")
    return info, made


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--run",
        action="append",
        type=parse_run,
        required=True,
        help='"Tên=file dự đoán"; lần đầu là model gốc',
    )
    parser.add_argument(
        "--out",
        type=Path,
        required=True,
        help="thư mục báo cáo, đặt tên dạng slm/results/bao_cao/<YYYY-MM-DD>_<model>_<tập>_<ban_thu|chinh_thuc>",
    )
    parser.add_argument(
        "--log", type=Path, default=None, help="log huấn luyện: training_log.csv hoặc trainer_state.json"
    )
    parser.add_argument(
        "--baseline-val-loss",
        type=float,
        default=None,
        help="val loss của model gốc, vẽ làm đường mức xuất phát",
    )
    parser.add_argument(
        "--data",
        type=Path,
        default=DEFAULT_OUT,
        help="du_lieu_slm.jsonl, để vẽ phân bố dữ liệu và kiểm tra trùng câu",
    )
    parser.add_argument(
        "--config",
        action="append",
        default=[],
        help='một dòng cấu hình thí nghiệm ghi vào báo cáo, dùng nhiều lần. Ví dụ: --config "Phần cứng: RTX 4060 8GB, Ollama 0.40"',
    )
    parser.add_argument(
        "--split-note",
        default=None,
        help="ghi chú về tập đo; mặc định tự suy ra từ tập đã đo (val hoặc test)",
    )
    parser.add_argument(
        "--baseline",
        action="append",
        type=parse_run,
        default=[],
        help='mốc tham chiếu chỉ có loại ý, "Tên=file" tạo bằng slm.eval.baseline_tfidf; dùng nhiều lần',
    )
    parser.add_argument(
        "--models-info",
        type=Path,
        default=MODEL_INFO,
        help="model_info.json: thông số công bố của các model, để dựng bảng tiêu chí chọn model",
    )
    args = parser.parse_args()

    runs = {name: m.load_rows(path) for name, path in args.run}
    splits = {name: split_of(path, runs[name]) for name, path in args.run}
    if len(set(splits.values())) > 1:
        raise SystemExit(
            f"Các lần chạy đo trên các tập khác nhau {splits}: so sánh sẽ không công bằng. Chạy lại cùng --split."
        )
    split = next(iter(splits.values()))
    ids = [frozenset(r["id"] for r in rows) for rows in runs.values()]
    if len(set(ids)) > 1:
        raise SystemExit(
            "Các lần chạy không đo cùng một tập tin nhắn (khác id): so sánh sẽ không công bằng. Chạy lại cùng --split và --limit."
        )
    summaries = {name: m.summarize(rows) for name, rows in runs.items()}
    names = list(runs)
    # Chỉ các tin hợp lệ: tách "hiểu sai ý" khỏi "không tuân thủ định dạng". Tên kèm số tin vì mỗi model còn số tin khác nhau
    valid_runs = {n: m.valid_rows(rows) for n, rows in runs.items()}
    valid = {f"{n} (n={len(rows)})": m.summarize(rows) for n, rows in valid_runs.items() if rows}
    common_rows = m.common_valid(runs)
    common = (
        {f"{n} (n={len(rows)})": m.summarize(rows) for n, rows in common_rows.items()}
        if next(iter(common_rows.values()))
        else {}
    )
    ece_common = {
        n: (m.calibration(rows) or {}).get("ece") if rows else None for n, rows in common_rows.items()
    }
    comparisons = {n: m.paired_compare(runs[names[0]], runs[n]) for n in names[1:]}
    baselines: dict[str, dict[str, Any]] = {}
    for name, path in args.baseline:
        rows = m.load_rows(path)
        if frozenset(r["id"] for r in rows) != ids[0]:
            raise SystemExit(
                f"Mốc tham chiếu {name} không đo cùng tập tin nhắn với các lần chạy: không so sánh được."
            )
        baselines[name] = m.summarize(rows)
    out: Path = args.out
    out.mkdir(parents=True, exist_ok=True)
    made: list[str] = []

    def note(path: Path | None) -> None:
        if path:
            made.append(path.name)

    # bảng
    header, rows = comparison_table(summaries)
    m.write_csv(out / "table_comparison.csv", header, rows)
    note(out / "table_comparison.csv")
    if valid:
        vh, vr = valid_table(valid)
        m.write_csv(out / "table_own_valid.csv", vh, vr)
        note(out / "table_own_valid.csv")
    if common:
        ch, cr = valid_table(common)
        m.write_csv(out / "table_same_messages.csv", ch, cr)
        note(out / "table_same_messages.csv")
    if comparisons:
        ph, pr = paired_table(comparisons, names[0])
        m.write_csv(out / "table_paired.csv", ph, pr)
        note(out / "table_paired.csv")
    for table, key in (("table_per_category.csv", "theo_loai"), ("table_per_label.csv", "theo_nhan")):
        labels = sorted({k for s in summaries.values() for k in s[key]})
        head = ["Label"] + [
            f"{n} {col}" for n in summaries for col in ("precision", "recall", "f1", "support")
        ]
        body = [
            [k]
            + [
                v
                for s in summaries.values()
                for v in (
                    round(s[key].get(k, {}).get("precision", 0), 4),
                    round(s[key].get(k, {}).get("recall", 0), 4),
                    round(s[key].get(k, {}).get("f1", 0), 4),
                    s[key].get(k, {}).get("ho_tro", 0),
                )
            ]
            for k in labels
        ]
        m.write_csv(out / table, head, body)
        note(out / table)
    for i, (name, s) in enumerate(summaries.items(), start=1):
        c = s["nham_lan"]
        m.write_csv(
            out / f"confusion_{i}.csv",
            ["True \\ predicted"] + c["col_labels"],
            [[lab] + row for lab, row in zip(c["labels"], c["matrix"], strict=True)],
        )
        m.write_csv(
            out / f"wrong_messages_{i}.csv",
            ["id", "error type", "message", "true label", "model prediction"],
            wrong_cases(runs[name]),
        )
        made += [
            f"confusion_{i}.csv ({name})",
            f"wrong_messages_{i}.csv ({name}): every wrong message, for review",
        ]
    examples = slm_audit.error_examples(runs)
    m.write_csv(
        out / "error_examples.csv",
        ["error of the first run", "id", "message", "correct label", *[f"{n}" for n in names]],
        [
            [e["loi_cua_model_dau"], e["id"], e["cau"], e["nhan_dung"],
             *[f"{e['du_doan'][n]['loi']}: {e['du_doan'][n]['du_doan']}" if n in e["du_doan"] else "" for n in names]]
            for e in examples
        ],
    )  # fmt: skip
    note(out / "error_examples.csv")

    # biểu đồ
    note(charts.chart_compare(summaries, out / "01_model_comparison.png", split))
    if common:
        note(charts.chart_compare(common, out / "01b_same_messages.png", split, valid_only=True))
    if comparisons:
        note(charts.chart_paired(comparisons, names[0], out / "01c_paired_difference.png"))
    note(charts.chart_f1_per_type(summaries, out / "02_f1_per_category.png"))
    note(charts.chart_precision_recall(summaries, out / "02b_precision_recall.png"))
    note(charts.chart_f1_per_label(summaries, out / "03_f1_per_label.png"))
    note(charts.chart_confusion(summaries, out / "04_confusion_matrix.png"))
    note(charts.chart_errors(summaries, out / "05_outcome_per_message.png"))
    note(charts.chart_invalid_causes(summaries, out / "05b_invalid_causes.png"))
    note(charts.chart_groups(summaries, out / "06_message_groups.png"))
    note(charts.chart_latency(summaries, out / "07_latency.png"))
    note(charts.chart_confidence(summaries, out / "08_confidence_reliability.png"))
    note(charts.chart_roc(summaries, out / "08b_confidence_roc_auc.png"))
    audit = None
    if args.data.is_file():
        records = [
            json.loads(line) for line in args.data.read_text(encoding="utf-8").splitlines() if line.strip()
        ]
        note(charts.chart_dataset(records, out / "09_dataset.png"))
        audit = slm_audit.data_audit(records)
        audit["file_huan_luyen_trung_test"] = slm_audit.finetune_overlap(
            args.data.parent / "finetune", slm_audit.test_questions(records)
        )
    training = None
    if args.log:
        log = m.load_training_log(args.log)
        training = m.training_summary(log)
        note(charts.chart_training(log, training, out / "10_training_curves.png", args.baseline_val_loss))
        note(charts.chart_gap(log, out / "11_val_train_gap.png"))
        note(charts.chart_val_metrics(log, out / "12_validation_metrics.png"))

    slim = {
        n: {k: v for k, v in s.items() if k not in ("tin_cay",)}
        | {"thoi_gian": {k: v for k, v in (s["thoi_gian"] or {}).items() if k != "mau"}}
        for n, s in summaries.items()
    }
    (out / "metrics.json").write_text(
        json.dumps(
            {
                "lan_chay": slim,
                "cung_tin_hop_le": {
                    n: {k: v for k, v in s.items() if k != "tin_cay"} for n, s in common.items()
                },
                "chi_tin_hop_le": {
                    n: {k: v for k, v in s.items() if k != "tin_cay"} for n, s in valid.items()
                },
                "so_cap": comparisons,
                "moc_tham_chieu": {
                    n: {k: v for k, v in s.items() if k != "tin_cay"} for n, s in baselines.items()
                },
                "kiem_tra_du_lieu": audit,
                "huan_luyen": training,
            },
            ensure_ascii=False,
            indent=2,
            default=float,
        ),
        encoding="utf-8",
    )
    made.append("metrics.json: all metrics, machine-readable")
    archived, archived_names = archive_inputs(out, args.run + args.baseline, args.log, args.data)
    made += archived_names
    manifest = {
        "ngay_dung_bao_cao": date.today().isoformat(),
        "tap_do": split,
        **git_state(),
        "cau_hinh": args.config,
        "lan_chay": [{"ten": name, "so_tin": len(runs[name])} for name, _ in args.run],
        "lenh_dung_lai": f"python -m slm.eval.slm_report {shlex.join(sys.argv[1:])}",
        **archived,
    }
    (out / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    made.append("manifest.json: date, git commit, configuration, command and input hashes")
    info = json.loads(args.models_info.read_text(encoding="utf-8")) if args.models_info.is_file() else {}
    extras: dict[str, Any] = {
        "setup": setup_lines(args.run, runs),
        "valid": valid,
        "common": common,
        "ece_common": ece_common,
        "zero_f1": {n: slm_audit.zero_f1_labels(runs[n], summaries[n]["theo_nhan"]) for n in names},
        "slowest": {n: slm_audit.slowest_message(runs[n]) for n in names},
        "paired": comparisons,
        "base": names[0],
        "audit": audit,
        "examples": examples,
        "models": models_table(summaries, runs, info) if info else None,
    }
    if baselines:

        def category_row(label: str, s: dict[str, Any]) -> list[Any]:
            lo, hi = s["khoang_tin_cay_95"]["dung_tap_loai"]
            return [
                label,
                ci_text(s["muc_dung"]["dung_tap_loai"], lo, hi),
                f"{s['f1_loai']['macro_f1']:.3f}",
                f"{s['f1_loai']['micro_f1']:.3f}",
            ]

        extras["baselines"] = (
            ["Model", "Correct category", "Macro-F1", "Micro-F1"],
            [category_row(n, s) for n, s in baselines.items()]
            + [category_row(n, s) for n, s in summaries.items()],
        )
    (out / "REPORT.md").write_text(
        markdown(
            summaries, args.split_note or SPLIT_NOTES.get(split, split), training, made, args.config, extras
        ),
        encoding="utf-8",
    )
    print(f"Đã ghi báo cáo vào {out} ({len(made)} file). Mở REPORT.md trước.")


if __name__ == "__main__":
    main()
