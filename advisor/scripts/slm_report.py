"""Báo cáo đo SLM: so sánh các lần chạy (model gốc và model đã huấn luyện) trên cùng một tập, kèm bảng CSV và biểu đồ.

Bước 1: chạy scripts.eval_slm cho từng model, cùng một tập (test), ra data/slm/ket_qua/<model>[_shotsN]_<tập>.jsonl:
  python -m scripts.eval_slm --model qwen3:4b-instruct --prompt slm_intents --shots 8 --split test
  python -m scripts.eval_slm --model usas-slm-v4 --prompt slm_intents --split test
Bước 2: dựng báo cáo. Lần chạy ĐẦU TIÊN là model gốc (baseline), các lần sau là model đã huấn luyện:
Công cụ dùng được cho cả hai tập: --split val để phát triển (chọn prompt, chọn epoch), --split test MỘT LẦN để lấy số báo cáo cuối.
Báo cáo tự ghi đúng tập đã đo. Ví dụ dựng báo cáo trên test:
  python -m scripts.slm_report --run "Model gốc=data/slm/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" \\
      --run "Đã huấn luyện=data/slm/ket_qua/usas-slm-v4_test.jsonl" --out data/slm/bao_cao/2026-11-10_v4_test_ban_thu \\
      --log training_log.csv --config "Model gốc: ..." --config "Đã huấn luyện: ..."
Thêm đường cong huấn luyện (log từ notebook Colab: training_log.csv hoặc trainer_state.json) và val loss của model gốc:
  ... --log training_log.csv --baseline-val-loss 2.31

Mỗi lần dựng báo cáo ra MỘT thư mục tự chứa đủ để xem lại và kiểm tra, đặt tên --out theo dạng
  data/slm/bao_cao/<YYYY-MM-DD>_<model>_<tập>_<ban_thu|chinh_thuc>
  BAO_CAO.md          báo cáo tổng hợp (đọc trước)
  manifest.json       lần chạy này dùng gì: ngày, commit git (và có sửa chưa commit không), cấu hình, lệnh, mã băm sha256 của đáp án thô, log và dữ liệu
  chi_so.json         toàn bộ chỉ số dạng máy đọc
  *.csv, *.png        bảng (mở bằng Excel) và biểu đồ có chú thích
  du_doan_tho/        bản sao đáp án thô của từng model đã dùng để dựng báo cáo
  <log huấn luyện>    bản sao log (nếu có --log)
Bản thử không commit (thư mục bao_cao và ket_qua nằm trong .gitignore); số chính thức thì commit bằng git add -f.
"""

import argparse
import hashlib
import json
import shlex
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path
from typing import Any

from scripts import slm_charts as charts
from scripts import slm_metrics as m
from scripts.build_slm_jsonl import DEFAULT_OUT

RESULT_DIR = DEFAULT_OUT.parent
LEVEL_TEXT = {
    "hop_le": "Trả được kết quả dùng được",
    "dung_tap_loai": "Đúng loại ý (không xét thứ tự)",
    "dung_thu_tu": "Đúng loại ý và thứ tự",
    "dung_nhiem_vu": "Đúng nhiệm vụ của từng ý",
    "dung_thuc_the": "Đúng cả thực thể",
    "dung_het": "Đúng hoàn toàn",
}


SPLIT_NOTES = {
    "test": "tập test (giữ nguyên, không dùng để huấn luyện hay chọn epoch): đây là số liệu báo cáo cuối cùng",
    "val": "tập val (dùng để theo dõi và chọn epoch, chỉnh prompt): đây là số để phát triển, KHÔNG phải số báo cáo cuối cùng",
    "train": "tập train (model đã học tập này nên điểm không phản ánh khả năng tổng quát hóa)",
}


# Tập đã đo của một lần chạy: cột split của file dự đoán, không có thì lấy từ đuôi tên file (<model>_val.jsonl)
def split_of(path: Path, rows: list[dict[str, Any]]) -> str:
    named = {r["split"] for r in rows if r.get("split")}
    if len(named) == 1:
        return named.pop()
    for name in SPLIT_NOTES:
        if path.stem.endswith(f"_{name}"):
            return name
    return "không rõ"


def parse_run(text: str) -> tuple[str, Path]:
    name, sep, path = text.partition("=")
    if not sep or not name.strip() or not path.strip():
        raise argparse.ArgumentTypeError(
            '--run phải có dạng "Tên=đường dẫn file", ví dụ "Model gốc=data/slm/ket_qua/x_test.jsonl"'
        )
    return name.strip(), Path(path.strip())


def percent(v: float | None) -> str:
    return "-" if v is None else f"{v:.1%}"


def comparison_table(summaries: dict[str, dict[str, Any]]) -> tuple[list[str], list[list[Any]]]:
    names = list(summaries)
    rows: list[list[Any]] = []
    for key in m.LEVELS:
        row: list[Any] = [LEVEL_TEXT[key]]
        for s in summaries.values():
            lo, hi = s["khoang_tin_cay_95"][key]
            row.append(f"{s['muc_dung'][key]:.1%} ({lo:.1%} - {hi:.1%})")
        rows.append(row)
    rows.append(
        ["F1 trung bình theo loại ý (macro)"]
        + [f"{s['f1_loai']['macro_f1']:.3f}" for s in summaries.values()]
    )
    rows.append(
        ["F1 gộp theo loại ý (micro)"] + [f"{s['f1_loai']['micro_f1']:.3f}" for s in summaries.values()]
    )
    rows.append(
        ["F1 trung bình theo nhãn loại/nhiệm vụ"]
        + [f"{s['f1_nhan']['macro_f1']:.3f}" for s in summaries.values()]
    )
    rows.append(
        ["tiep_noi: độ chính xác"]
        + [percent(s["tiep_noi"]["accuracy"]) if s["tiep_noi"] else "-" for s in summaries.values()]
    )
    rows.append(
        ["tiep_noi: F1 của câu nối tiếp"]
        + [f"{s['tiep_noi']['f1']:.3f}" if s["tiep_noi"] else "-" for s in summaries.values()]
    )
    for key in m.ERRORS[1:]:
        rows.append(
            [f"Lỗi: {charts.ERROR_NAMES[key]}"] + [f"{s['loi'][key]} tin" for s in summaries.values()]
        )
    rows.append(
        ["Thời gian trung bình (giây/tin)"]
        + [f"{s['thoi_gian']['trung_binh']:.2f}" if s["thoi_gian"] else "-" for s in summaries.values()]
    )
    rows.append(
        ["Thời gian p90 (giây/tin)"]
        + [f"{s['thoi_gian']['p90']:.2f}" if s["thoi_gian"] else "-" for s in summaries.values()]
    )
    return ["Chỉ số"] + names, rows


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
                r["cau_hoi"],
                json.dumps(r["nhan"]["cac_y"], ensure_ascii=False),
                json.dumps(r["du_doan"]["cac_y"], ensure_ascii=False),
            ]
        )
    return out


def markdown(
    summaries: dict[str, dict[str, Any]],
    split_note: str,
    training: dict[str, Any] | None,
    made: list[str],
    config: list[str] | None = None,
) -> str:
    header, rows = comparison_table(summaries)
    lines = [
        "# Báo cáo đo SLM tách ý",
        "",
        f"Tập đo: {split_note}. Số tin: {', '.join(f'{n} = {s["so_tin"]}' for n, s in summaries.items())}.",
        "Lần chạy đầu tiên là model gốc (chưa huấn luyện); các lần sau là model đã huấn luyện. Cùng một tập, cùng cách chấm.",
        f"Ngày dựng báo cáo: {date.today().isoformat()}.",
        "",
    ]
    if config:
        lines += ["## Cấu hình thí nghiệm", "", *[f"- {c}" for c in config], ""]
    lines += [
        "## So sánh",
        "",
        "| " + " | ".join(header) + " |",
        "|" + "---|" * len(header),
        *["| " + " | ".join(map(str, r)) + " |" for r in rows],
        "",
        "Trong ngoặc là khoảng tin cậy 95% (bootstrap 1000 lần lấy mẫu lại). Hai khoảng không chồng nhau thì khác biệt có ý nghĩa.",
    ]
    if training:
        lines += ["", "## Huấn luyện", ""]
        if not training.get("co_val"):
            lines.append("Log không có val loss.")
        else:
            lines += [
                f"- Val loss thấp nhất: {training['val_loss_tot_nhat']:.4f} tại bước {training['buoc_tot_nhat']:.0f}"
                + (
                    f" (epoch {training['epoch_tot_nhat']:.2f})"
                    if training.get("epoch_tot_nhat") is not None
                    else ""
                ),
                f"- Val loss ở lần đo cuối: {training['val_loss_cuoi']:.4f} (số lần đo val: {training['so_lan_do_val']})",
                *(
                    [
                        f"- Chênh val loss trừ train loss: {training['chenh_val_train_tai_tot_nhat']:.4f} tại điểm thấp nhất"
                        + (
                            f", {training['chenh_val_train_cuoi']:.4f} ở lần đo cuối"
                            if training.get("chenh_val_train_cuoi") is not None
                            else ""
                        )
                        + " (tăng dần qua các lần đo là dấu hiệu học thuộc)"
                    ]
                    if training.get("chenh_val_train_tai_tot_nhat") is not None
                    else []
                ),
                f"- Overfit (val loss tăng trên 5% sau điểm thấp nhất): {'có' if training['overfit'] else 'không thấy'}",
                f"- Đã hội tụ (điểm thấp nhất không nằm ở lần đo cuối): {'chưa, val loss còn giảm ở cuối' if training['chua_hoi_tu'] else 'rồi'}",
            ]
    lines += [
        "",
        "## Cách đo",
        "",
        "- Đơn vị đo là một tin nhắn. Các mức đúng xếp từ lỏng đến chặt, mức sau gồm mức trước: dùng được, đúng loại ý, đúng thứ tự, đúng nhiệm vụ, "
        "đúng thực thể, đúng hoàn toàn (kể cả tiep_noi, tức follow_up). Phép đo chưa so chữ của span (đoạn trích của từng ý), "
        "chỉ so loại ý, nhiệm vụ, thực thể và tiep_noi.",
        "- Precision, recall, F1 tính trên từng ý (một tin có thể có nhiều ý), theo loại ý và theo nhãn loại/nhiệm vụ.",
        "- Tên loại ý trong báo cáo là tên nội bộ: mac_dinh = collect_profile, ho_so = profile_review, kich_ban = scenario, "
        "kien_thuc = knowledge, trung_tam = center_referral, ngoai_pham_vi = out_of_scope.",
        "- Tập test không dùng để huấn luyện; chọn điểm tốt nhất và dừng sớm (nếu có) dựa trên val, không dựa trên test.",
        "",
        "## Các file",
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
            f"Hai lần chạy trùng tên file {names}: không chép được vào du_doan_tho. Đổi tên một file."
        )
    raw_dir = out / "du_doan_tho"
    raw_dir.mkdir(parents=True, exist_ok=True)
    for _, path in run_files:
        shutil.copy2(path, raw_dir / path.name)
    info: dict[str, Any] = {
        "dap_an_tho": [
            {"ten": name, "file": f"du_doan_tho/{path.name}", "sha256": sha256_of(path)}
            for name, path in run_files
        ],
        "log_huan_luyen": None,
        "du_lieu": {"file": data.name, "sha256": sha256_of(data)} if data.is_file() else None,
    }
    made = [f"du_doan_tho/ (bản sao đáp án thô: {', '.join(names)})"]
    if log:
        target = out / log.name
        if log.resolve() != target.resolve():
            shutil.copy2(log, target)
        info["log_huan_luyen"] = {"file": log.name, "sha256": sha256_of(log)}
        made.append(f"{log.name}: log huấn luyện (bản sao)")
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
        help="thư mục báo cáo, đặt tên dạng data/slm/bao_cao/<YYYY-MM-DD>_<model>_<tập>_<ban_thu|chinh_thuc>",
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
        "--data", type=Path, default=DEFAULT_OUT, help="du_lieu_slm.jsonl, để vẽ phân bố dữ liệu"
    )
    parser.add_argument(
        "--config",
        action="append",
        default=[],
        help='một dòng cấu hình thí nghiệm ghi vào báo cáo, dùng nhiều lần. Ví dụ: --config "Model gốc: qwen3:4b-instruct, 8 ví dụ từ train, prompt slm_intents v3.0"',
    )
    parser.add_argument(
        "--split-note",
        default=None,
        help="ghi chú về tập đo; mặc định tự suy ra từ tập đã đo (val hoặc test)",
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
    out: Path = args.out
    out.mkdir(parents=True, exist_ok=True)
    made: list[str] = []

    def note(path: Path | None) -> None:
        if path:
            made.append(path.name)

    # bảng
    header, rows = comparison_table(summaries)
    m.write_csv(out / "bang_so_sanh.csv", header, rows)
    note(out / "bang_so_sanh.csv")
    for table, key in (("bang_theo_loai.csv", "theo_loai"), ("bang_theo_nhan.csv", "theo_nhan")):
        labels = sorted({k for s in summaries.values() for k in s[key]})
        head = ["Nhãn"] + [f"{n} {col}" for n in summaries for col in ("precision", "recall", "f1", "số ý")]
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
            out / f"nham_lan_{i}.csv",
            ["Nhãn đúng \\ dự đoán"] + c["labels"],
            [[lab] + row for lab, row in zip(c["labels"], c["matrix"], strict=True)],
        )
        m.write_csv(
            out / f"cau_sai_{i}.csv",
            ["id", "loại lỗi", "câu hỏi", "nhãn đúng", "model dự đoán"],
            wrong_cases(runs[name]),
        )
        made += [f"nham_lan_{i}.csv ({name})", f"cau_sai_{i}.csv ({name}): từng câu sai để xem lại"]

    # biểu đồ
    note(charts.chart_compare(summaries, out / "01_so_sanh.png", split))
    note(charts.chart_f1_per_type(summaries, out / "02_f1_theo_loai.png"))
    note(charts.chart_f1_per_label(summaries, out / "03_f1_theo_nhan.png"))
    note(charts.chart_confusion(summaries, out / "04_nham_lan.png"))
    note(charts.chart_errors(summaries, out / "05_phan_loai_ket_qua.png"))
    note(charts.chart_groups(summaries, out / "06_theo_nhom_tin.png"))
    note(charts.chart_latency(summaries, out / "07_thoi_gian.png"))
    note(charts.chart_confidence(summaries, out / "08_do_tin_cay.png"))
    if args.data.is_file():
        records = [
            json.loads(line) for line in args.data.read_text(encoding="utf-8").splitlines() if line.strip()
        ]
        note(charts.chart_dataset(records, out / "09_du_lieu.png"))
    training = None
    if args.log:
        log = m.load_training_log(args.log)
        training = m.training_summary(log)
        note(charts.chart_training(log, training, out / "10_huan_luyen.png", args.baseline_val_loss))
        note(charts.chart_gap(log, out / "11_chenh_val_train.png"))
        note(charts.chart_val_metrics(log, out / "12_chi_so_val_theo_buoc.png"))

    slim = {
        n: {k: v for k, v in s.items() if k not in ("tin_cay",)}
        | {"thoi_gian": {k: v for k, v in (s["thoi_gian"] or {}).items() if k != "mau"}}
        for n, s in summaries.items()
    }
    (out / "chi_so.json").write_text(
        json.dumps({"lan_chay": slim, "huan_luyen": training}, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    made.append("chi_so.json: toàn bộ chỉ số dạng máy đọc")
    archived, archived_names = archive_inputs(out, args.run, args.log, args.data)
    made += archived_names
    manifest = {
        "ngay_dung_bao_cao": date.today().isoformat(),
        "tap_do": split,
        **git_state(),
        "cau_hinh": args.config,
        "lan_chay": [{"ten": name, "so_tin": len(runs[name])} for name, _ in args.run],
        "lenh_dung_lai": f"python -m scripts.slm_report {shlex.join(sys.argv[1:])}",
        **archived,
    }
    (out / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    made.append("manifest.json: ngày, commit git, cấu hình, lệnh và mã băm của đầu vào")
    (out / "BAO_CAO.md").write_text(
        markdown(summaries, args.split_note or SPLIT_NOTES.get(split, split), training, made, args.config),
        encoding="utf-8",
    )
    print(f"Đã ghi báo cáo vào {out} ({len(made)} file). Mở BAO_CAO.md trước.")


if __name__ == "__main__":
    main()
