"""Vẽ biểu đồ đánh giá SLM từ kết quả của scripts.slm_metrics. Mỗi hàm vẽ một biểu đồ ra một file PNG.

Màu cố định theo thứ tự các lần chạy: cam (model gốc), xanh dương (model đã huấn luyện), xanh lá (lần thứ ba nếu có).
Bộ màu Okabe-Ito, phân biệt được cả với người khó phân biệt màu.
"""

from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")  # vẽ ra file, không mở cửa sổ
import matplotlib.pyplot as plt  # noqa: E402

from app.schemas.slm import INTENTS  # noqa: E402
from scripts.slm_metrics import ERRORS, LEVELS, NONE  # noqa: E402

PALETTE = ["#E69F00", "#0072B2", "#009E73", "#CC79A7", "#56B4E9"]
LEVEL_NAMES = {
    "hop_le": "Trả được\nkết quả",
    "dung_tap_loai": "Đúng loại ý",
    "dung_thu_tu": "Đúng\nthứ tự",
    "dung_nhiem_vu": "Đúng\nnhiệm vụ",
    "dung_thuc_the": "Đúng\nthực thể",
    "dung_het": "Đúng\nhoàn toàn",
}
ERROR_NAMES = {
    "dung_het": "Đúng hoàn toàn", "json_loi": "Không dùng được (lùi về mơ hồ)", "thieu_y": "Thiếu ý", "thua_y": "Thừa ý",
    "sai_loai": "Sai loại ý", "sai_thu_tu": "Sai thứ tự", "sai_nhiem_vu": "Sai nhiệm vụ", "sai_thuc_the": "Sai thực thể",
    "sai_tiep_noi": "Sai tiep_noi",
}  # fmt: skip
ERROR_COLORS = [
    "#009E73",
    "#444444",
    "#D55E00",
    "#E69F00",
    "#CC79A7",
    "#56B4E9",
    "#F0E442",
    "#0072B2",
    "#999999",
]


def style() -> None:
    plt.rcParams.update(
        {
            "figure.facecolor": "white", "axes.facecolor": "white", "axes.spines.top": False, "axes.spines.right": False,
            "axes.grid": True, "grid.alpha": 0.25, "axes.axisbelow": True, "font.size": 10, "axes.titlesize": 12,
            "axes.titleweight": "bold",
        }
    )  # fmt: skip


def save(fig: Any, path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    return path


def color(i: int) -> str:
    return PALETTE[i % len(PALETTE)]


def grouped_bars(
    ax: Any,
    groups: list[str],
    series: dict[str, list[float | None]],
    errors: dict[str, list[tuple[float, float]]] | None = None,
) -> None:
    width = 0.8 / max(len(series), 1)
    for i, (name, values) in enumerate(series.items()):
        xs = [g + (i - (len(series) - 1) / 2) * width for g in range(len(groups))]
        vals = [v if v is not None else 0 for v in values]
        err = None
        if errors and name in errors:
            err = [
                [max(v - lo, 0) for v, (lo, _) in zip(vals, errors[name], strict=True)],
                [max(hi - v, 0) for v, (_, hi) in zip(vals, errors[name], strict=True)],
            ]
        bars = ax.bar(
            xs,
            vals,
            width * 0.92,
            label=name,
            color=color(i),
            yerr=err,
            capsize=3,
            error_kw={"elinewidth": 1, "ecolor": "#333333"},
        )
        for k, (bar, v) in enumerate(zip(bars, values, strict=True)):
            if v is not None:
                top = (
                    max(bar.get_height(), errors[name][k][1])
                    if errors and name in errors
                    else bar.get_height()
                )
                ax.text(
                    bar.get_x() + bar.get_width() / 2,
                    top + 0.012,
                    f"{v:.0%}" if v <= 1 else f"{v:.2f}",
                    ha="center",
                    va="bottom",
                    fontsize=7.5,
                )
    ax.set_xticks(range(len(groups)), groups)
    ax.set_ylim(0, 1.12)
    ax.set_yticks([0, 0.25, 0.5, 0.75, 1.0], ["0%", "25%", "50%", "75%", "100%"])


def legend_below(ax: Any, ncol: int) -> None:
    """Chú thích đặt dưới trục để không che cột."""
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.2), ncol=ncol, frameon=False)


def chart_compare(summaries: dict[str, dict[str, Any]], path: Path, split: str = "test") -> Path:
    """Cột các mức đúng của từng model, kèm khoảng tin cậy 95% (vạch đen)."""
    style()
    fig, ax = plt.subplots(figsize=(10, 4.8))
    groups = [LEVEL_NAMES[k] for k in LEVELS] + ["F1 trung bình\ntheo loại ý"]
    series = {
        n: [s["muc_dung"][k] for k in LEVELS] + [s["f1_loai"]["macro_f1"]] for n, s in summaries.items()
    }
    errors = {
        n: [tuple(s["khoang_tin_cay_95"][k]) for k in LEVELS] + [(series[n][-1], series[n][-1])]
        for n, s in summaries.items()
    }
    grouped_bars(ax, groups, series, errors)
    ax.set_title(f"So sánh các mức đúng trên tập {split} (vạch đen: khoảng tin cậy 95%)")
    legend_below(ax, len(series))
    return save(fig, path)


def chart_f1_per_type(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    style()
    fig, ax = plt.subplots(figsize=(9, 4.5))
    series = {n: [s["theo_loai"].get(t, {}).get("f1") for t in INTENTS] for n, s in summaries.items()}
    grouped_bars(ax, list(INTENTS), series)
    ax.set_title("F1 theo từng loại ý")
    legend_below(ax, len(series))
    return save(fig, path)


def chart_f1_per_label(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """F1 theo nhãn 'loại/nhiệm vụ' (chi tiết hơn), xếp theo số ý trong nhãn."""
    style()
    support: dict[str, int] = {}
    for s in summaries.values():
        for k, v in s["theo_nhan"].items():
            support[k] = max(support.get(k, 0), v["ho_tro"])
    labels = sorted((k for k, n in support.items() if n > 0), key=lambda k: (-support[k], k))
    fig, ax = plt.subplots(figsize=(9, max(4, 0.17 * len(labels) * len(summaries) + 1.2)))
    height = 0.8 / len(summaries)
    for i, (name, s) in enumerate(summaries.items()):
        ys = [j + (i - (len(summaries) - 1) / 2) * height for j in range(len(labels))]
        ax.barh(
            ys,
            [s["theo_nhan"].get(k, {}).get("f1", 0) for k in labels],
            height * 0.92,
            label=name,
            color=color(i),
        )
    ax.set_yticks(range(len(labels)), [f"{k}  (n={support[k]})" for k in labels], fontsize=7.5)
    ax.invert_yaxis()
    ax.set_xlim(0, 1.02)
    ax.set_title("F1 theo nhãn loại/nhiệm vụ (n = số ý có trong nhãn đúng)")
    ax.legend(loc="lower right")
    return save(fig, path)


def heatmap(ax: Any, c: dict[str, Any], title: str) -> Any:
    labels, matrix = c["labels"], c["matrix"]
    norm = [[(v / sum(row)) if sum(row) else 0 for v in row] for row in matrix]  # mỗi hàng chia cho tổng hàng
    im = ax.imshow(norm, cmap="Blues", vmin=0, vmax=1)
    ax.set_xticks(range(len(labels)), labels, rotation=40, ha="right", fontsize=8)
    ax.set_yticks(range(len(labels)), labels, fontsize=8)
    for i, row in enumerate(matrix):
        for j, v in enumerate(row):
            if v:
                ax.text(
                    j,
                    i,
                    str(v),
                    ha="center",
                    va="center",
                    fontsize=8,
                    color="white" if norm[i][j] > 0.55 else "#222222",
                )
    ax.set_xlabel("Model dự đoán")
    ax.set_ylabel("Nhãn đúng")
    ax.set_title(title, fontsize=10)
    ax.grid(False)
    return im


def chart_confusion(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Ma trận nhầm lẫn của từng model đặt cạnh nhau (ô = số ý, màu = tỉ lệ trên hàng)."""
    style()
    n = len(summaries)
    fig, axes = plt.subplots(1, n, figsize=(5.8 * n, 5.4), squeeze=False, gridspec_kw={"wspace": 0.75})
    for ax, (name, s) in zip(axes[0], summaries.items(), strict=True):
        heatmap(ax, s["nham_lan"], name)
    fig.suptitle(
        f"Ma trận nhầm lẫn giữa các loại ý. Dòng và cột {NONE} là ý bị thiếu hoặc ý thừa", fontweight="bold"
    )
    return save(fig, path)


def chart_errors(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    style()
    fig, ax = plt.subplots(figsize=(10, 1.4 + 0.9 * len(summaries)))
    names = list(summaries)
    left = [0.0] * len(names)
    for k, key in enumerate(ERRORS):
        shares = [s["loi"][key] / s["so_tin"] for s in summaries.values()]
        ax.barh(names, shares, left=left, color=ERROR_COLORS[k], label=ERROR_NAMES[key], height=0.55)
        for i, (v, lft) in enumerate(zip(shares, left, strict=True)):
            if v >= 0.04:
                ax.text(
                    lft + v / 2,
                    i,
                    f"{v:.0%}",
                    ha="center",
                    va="center",
                    fontsize=8,
                    color="white" if k in (0, 1, 7) else "#222222",
                )
        left = [a + b for a, b in zip(left, shares, strict=True)]
    ax.set_xlim(0, 1)
    ax.invert_yaxis()
    ax.set_xticks([0, 0.25, 0.5, 0.75, 1], ["0%", "25%", "50%", "75%", "100%"])
    ax.set_title("Phân loại kết quả theo từng tin nhắn")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.12), ncol=3, fontsize=8, frameon=False)
    return save(fig, path)


def chart_groups(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Tỉ lệ đúng hoàn toàn theo: số ý trong tin, có hay không có ngữ cảnh, loại ý chính."""
    style()
    fig, axes = plt.subplots(1, 3, figsize=(15, 4.4), gridspec_kw={"width_ratios": [2.2, 2, 4.2]})
    for ax, key, title in zip(
        axes,
        ("theo_so_y", "theo_ngu_canh", "theo_loai_chinh"),
        ("Theo số ý trong tin", "Theo ngữ cảnh", "Theo loại ý chính"),
        strict=True,
    ):
        names = sorted(
            {g for s in summaries.values() for g in s[key]},
            key=lambda g: (INTENTS.index(g) if g in INTENTS else 0, g),
        )
        series = {n: [s[key].get(g, {}).get("dung_het") for g in names] for n, s in summaries.items()}
        labels = {"co_ngu_canh": "có ngữ cảnh", "khong_ngu_canh": "không ngữ cảnh"}
        grouped_bars(ax, [labels.get(g, g) for g in names], series)
        ax.set_title(title)
        ax.tick_params(axis="x", labelsize=8)
    handles, names = axes[0].get_legend_handles_labels()
    fig.legend(
        handles, names, loc="lower center", bbox_to_anchor=(0.5, -0.06), ncol=len(names), frameon=False
    )
    fig.suptitle("Tỉ lệ đúng hoàn toàn theo nhóm tin", fontweight="bold")
    return save(fig, path)


def chart_latency(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    data = {n: s["thoi_gian"]["mau"] for n, s in summaries.items() if s.get("thoi_gian")}
    if not data:
        return None
    style()
    fig, ax = plt.subplots(figsize=(7, 3 + 0.4 * len(data)))
    box = ax.boxplot(
        list(data.values()),
        orientation="horizontal",
        tick_labels=list(data),
        patch_artist=True,
        showfliers=False,
    )
    for i, patch in enumerate(box["boxes"]):
        patch.set_facecolor(color(i))
        patch.set_alpha(0.7)
    for i, v in enumerate(data.values(), start=1):
        ax.text(max(v) * 1.01, i, f"tb {sum(v) / len(v):.2f}s", va="center", fontsize=8)
    ax.invert_yaxis()
    ax.set_xlabel("Giây cho một tin (không vẽ điểm ngoại lai)")
    ax.set_title("Thời gian trả lời")
    return save(fig, path)


def chart_confidence(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    """Độ tin cậy khi đúng và khi sai. Chỉ vẽ khi model có trả độ tin cậy thay đổi (không phải toàn 1.0)."""
    usable = {
        n: s for n, s in summaries.items() if len(set(s["tin_cay"]["khi_dung"] + s["tin_cay"]["khi_sai"])) > 1
    }
    if not usable:
        return None
    style()
    fig, axes = plt.subplots(1, len(usable), figsize=(5.5 * len(usable), 3.6), squeeze=False)
    for ax, (name, s) in zip(axes[0], usable.items(), strict=True):
        bins = [i / 10 for i in range(11)]
        ax.hist(s["tin_cay"]["khi_dung"], bins=bins, alpha=0.7, color="#009E73", label="khi đúng hoàn toàn")
        ax.hist(s["tin_cay"]["khi_sai"], bins=bins, alpha=0.7, color="#D55E00", label="khi sai")
        ax.set_title(name, fontsize=10)
        ax.set_xlabel("Độ tin cậy")
        ax.set_ylabel("Số tin")
        ax.legend()
    fig.suptitle("Độ tin cậy của model khi đúng và khi sai", fontweight="bold")
    return save(fig, path)


def chart_dataset(records: list[dict[str, Any]], path: Path) -> Path:
    """Số dòng dữ liệu theo loại và theo tập train, val, test."""
    style()
    kinds = list(INTENTS)
    splits = [("train", "#0072B2"), ("val", "#E69F00"), ("test", "#009E73")]
    fig, ax = plt.subplots(figsize=(9, 4.4))
    bottom = [0] * len(kinds)
    for split, col in splits:
        counts = [sum(1 for r in records if r["type"] == k and r["split"] == split) for k in kinds]
        ax.bar(kinds, counts, bottom=bottom, color=col, label=f"{split} ({sum(counts)})")
        bottom = [a + b for a, b in zip(bottom, counts, strict=True)]
    for i, total in enumerate(bottom):
        ax.text(i, total + max(bottom) * 0.01, str(total), ha="center", fontsize=8)
    ax.set_ylabel("Số dòng")
    ax.set_title(f"Dữ liệu theo loại và theo tập (tổng {len(records)} dòng)")
    ax.legend()
    return save(fig, path)


def chart_training(
    log: list[dict[str, float]], summary: dict[str, Any], path: Path, baseline_val_loss: float | None = None
) -> Path:
    """Đường train loss và val loss theo bước. Đánh dấu điểm val loss thấp nhất (epoch hội tụ) và đường mức xuất phát của model gốc."""
    style()
    fig, ax = plt.subplots(figsize=(9, 4.8))
    trains = [p for p in log if "train_loss" in p]
    evals = [p for p in log if "eval_loss" in p]
    ax.plot(
        [p["step"] for p in trains],
        [p["train_loss"] for p in trains],
        color="#0072B2",
        lw=1.4,
        label="train loss",
    )
    if evals:
        ax.plot(
            [p["step"] for p in evals],
            [p["eval_loss"] for p in evals],
            color="#D55E00",
            lw=1.8,
            marker="o",
            ms=4,
            label="val loss",
        )
    if summary.get("co_val"):
        ax.axvline(summary["buoc_tot_nhat"], color="#009E73", ls="--", lw=1.3)
        ax.annotate(
            f"val loss thấp nhất {summary['val_loss_tot_nhat']:.3f}\nbước {summary['buoc_tot_nhat']:.0f}"
            + (f" (epoch {summary['epoch_tot_nhat']:.2f})" if summary.get("epoch_tot_nhat") is not None else ""),
            (summary["buoc_tot_nhat"], summary["val_loss_tot_nhat"]), xytext=(12, 24), textcoords="offset points", fontsize=8.5,
            arrowprops={"arrowstyle": "->", "color": "#009E73"},
        )  # fmt: skip
    if baseline_val_loss is not None:
        ax.axhline(
            baseline_val_loss,
            color="#E69F00",
            ls=":",
            lw=1.5,
            label=f"model gốc (val loss {baseline_val_loss:.3f})",
        )
    ax.set_xlabel("Bước huấn luyện")
    ax.set_ylabel("Loss")
    ax.set_title("Đường cong huấn luyện")
    ax.legend()
    return save(fig, path)


def chart_gap(log: list[dict[str, float]], path: Path) -> Path | None:
    """Khoảng cách val loss trừ train loss theo bước: lớn dần là dấu hiệu overfit."""
    trains = [p for p in log if "train_loss" in p]
    evals = [p for p in log if "eval_loss" in p]
    if not trains or not evals:
        return None
    pts = []
    for e in evals:
        before = [t for t in trains if t["step"] <= e["step"]]
        if before:
            pts.append((e["step"], e["eval_loss"] - before[-1]["train_loss"]))
    if not pts:
        return None
    style()
    fig, ax = plt.subplots(figsize=(8, 3.6))
    ax.plot([p[0] for p in pts], [p[1] for p in pts], color="#CC79A7", marker="o", ms=4)
    ax.axhline(0, color="#555555", lw=0.8)
    ax.set_xlabel("Bước huấn luyện")
    ax.set_ylabel("val loss trừ train loss")
    ax.set_title("Khoảng cách val và train (tăng dần = overfit)")
    return save(fig, path)


def chart_val_metrics(log: list[dict[str, float]], path: Path) -> Path | None:
    """Các chỉ số đo trên val sau mỗi lần đánh giá (cột val_* trong log), nếu notebook có ghi."""
    keys = sorted({k for p in log for k in p if k.startswith("val_")})
    if not keys:
        return None
    style()
    fig, ax = plt.subplots(figsize=(8, 4))
    for i, key in enumerate(keys):
        pts = [(p["step"], p[key]) for p in log if key in p]
        ax.plot([a for a, _ in pts], [b for _, b in pts], marker="o", ms=4, color=color(i), label=key[4:])
    ax.set_ylim(0, 1.02)
    ax.set_xlabel("Bước huấn luyện")
    ax.set_title("Chỉ số trên val theo bước huấn luyện")
    ax.legend()
    return save(fig, path)
