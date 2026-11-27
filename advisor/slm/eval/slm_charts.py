"""Vẽ biểu đồ đánh giá SLM từ kết quả của slm.eval.slm_metrics. Mỗi hàm vẽ một biểu đồ ra một file PNG.

Màu cố định theo thứ tự các lần chạy: cam (model gốc), xanh dương (model đã huấn luyện), xanh lá (lần thứ ba nếu có).
Bộ màu Okabe-Ito, phân biệt được cả với người khó phân biệt màu.
"""

import statistics
from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")  # vẽ ra file, không mở cửa sổ
import matplotlib.pyplot as plt  # noqa: E402

from app.schemas.slm import INTENTS  # noqa: E402
from slm.eval.slm_metrics import ERRORS, INVALID, LEVELS, NONE, percentile  # noqa: E402

PALETTE = ["#E69F00", "#0072B2", "#009E73", "#CC79A7", "#56B4E9"]
LEVEL_NAMES = {
    "hop_le": "Valid\nresponse",
    "dung_tap_loai": "Correct\ncategory",
    "dung_thu_tu": "Correct\norder",
    "dung_nhiem_vu": "Correct\ntask",
    "dung_thuc_the": "Correct\nentity",
    "dung_het": "Exact\nmatch",
}
ERROR_NAMES = {
    "dung_het": "Exact match", "json_loi": "Invalid response", "thieu_y": "Missing intent", "thua_y": "Extra intent",
    "sai_loai": "Wrong category", "sai_thu_tu": "Wrong order", "sai_nhiem_vu": "Wrong task", "sai_thuc_the": "Wrong entity",
    "sai_follow_up": "Wrong follow_up",
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


def spread(xs: list[float], gap: float, lo: float, hi: float) -> list[float]:
    """Dàn vị trí chữ chú thích theo chiều ngang để không đè nhau: giữ gần chỗ gốc nhất, cách nhau ít nhất gap, nằm trong [lo, hi].
    xs phải tăng dần."""
    out: list[float] = []
    for x in xs:
        out.append(max(x, out[-1] + gap if out else lo))
    for i in range(len(out) - 1, -1, -1):
        out[i] = min(out[i], hi - (len(out) - 1 - i) * gap)
    return out


def leader_labels(ax: Any, items: list[tuple[float, str]], y_from: float, y_text: float, gap: float, lo: float, hi: float) -> None:
    """Số của các đoạn quá hẹp: ghi phía trên thanh, có đường kẻ trỏ xuống đúng đoạn đó. items: (vị trí giữa đoạn, chữ)."""
    items = sorted(items)
    for (x, text), x_text in zip(items, spread([x for x, _ in items], gap, lo, hi), strict=True):
        ax.annotate(
            text, xy=(x, y_from), xytext=(x_text, y_text), ha="center", va="bottom", fontsize=7.5, annotation_clip=False,
            arrowprops={"arrowstyle": "-", "lw": 0.6, "color": "#555555", "shrinkA": 0, "shrinkB": 0},
        )  # fmt: skip


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
                text = f"{v:.0%}" if v <= 1 else f"{v:.2f}"
                ax.text(
                    bar.get_x() + bar.get_width() / 2,
                    top + 0.025,
                    text,
                    ha="center",
                    va="bottom",
                    fontsize=8,
                    rotation=90
                    if len(series) >= 3
                    else 0,  # cột hẹp (3 model trở lên): xoay dọc để chữ không dính nhau
                )
    ax.set_xticks(range(len(groups)), groups)
    ax.set_ylim(0, 1.2 if len(series) >= 3 else 1.12)  # chừa chỗ cho nhãn xoay dọc
    ax.set_yticks([0, 0.25, 0.5, 0.75, 1.0], ["0%", "25%", "50%", "75%", "100%"])


def legend_below(ax: Any, ncol: int) -> None:
    """Chú thích đặt dưới trục để không che cột."""
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.2), ncol=ncol, frameon=False)


def chart_compare(
    summaries: dict[str, dict[str, Any]], path: Path, split: str = "test", valid_only: bool = False
) -> Path:
    """Hai khung: mức TIN NHẮN (các mức đúng) và mức Ý (macro, micro F1), mỗi cột có khoảng tin cậy 95% (vạch đen).
    Tách hai khung vì hai nhóm đo trên hai đơn vị khác nhau. Bỏ 'đúng thứ tự' vì nó trùng 'đúng loại ý' khi tin chỉ có một ý.
    valid_only: summaries tính chỉ trên tin hợp lệ nên bỏ cột 'Valid response' (luôn 100%)."""
    style()
    levels = [k for k in LEVELS if k != "dung_thu_tu" and not (valid_only and k == "hop_le")]
    fig, (ax_msg, ax_f1) = plt.subplots(
        1, 2, figsize=(11, 4.8), gridspec_kw={"width_ratios": [len(levels), 2.2]}
    )
    series = {n: [s["muc_dung"][k] for k in levels] for n, s in summaries.items()}
    errors = {n: [tuple(s["khoang_tin_cay_95"][k]) for k in levels] for n, s in summaries.items()}
    grouped_bars(ax_msg, [LEVEL_NAMES[k] for k in levels], series, errors)
    ax_msg.set_title("Message level (share of messages)", fontsize=10)
    f1_series = {n: [s["f1_loai"]["macro_f1"], s["f1_loai"]["micro_f1"]] for n, s in summaries.items()}
    f1_errors = {
        n: [tuple(s["khoang_tin_cay_f1"]["macro_f1"]), tuple(s["khoang_tin_cay_f1"]["micro_f1"])]
        for n, s in summaries.items()
    }
    grouped_bars(ax_f1, ["Macro-F1", "Micro-F1"], f1_series, f1_errors)
    ax_f1.set_title("Intent level (F1 over categories)", fontsize=10)
    handles, names = ax_msg.get_legend_handles_labels()
    fig.legend(
        handles, names, loc="lower center", bbox_to_anchor=(0.5, -0.04), ncol=len(names), frameon=False
    )
    scope = "same messages, usable for every model" if valid_only else "all messages"
    fig.suptitle(
        f"Accuracy on the {split} set, {scope} (black bars: 95% confidence interval)",
        fontweight="bold",
        y=1.0,
    )
    return save(fig, path)


def chart_f1_per_type(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    style()
    fig, ax = plt.subplots(figsize=(9, 4.5))
    series = {n: [s["theo_loai"].get(t, {}).get("f1") for t in INTENTS] for n, s in summaries.items()}
    support = {
        t: max(s["theo_loai"].get(t, {}).get("ho_tro", 0) for s in summaries.values()) for t in INTENTS
    }
    grouped_bars(ax, [f"{t}\n(n={support[t]})" for t in INTENTS], series)
    ax.set_title("F1 per category (n = intents in ground truth)")
    legend_below(ax, len(series))
    return save(fig, path)


def chart_precision_recall(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Precision và recall của từng loại ý, hai khung cạnh nhau, mỗi cột có số. F1 (biểu đồ 02) gộp hai số này nên che mất việc model sai
    theo hướng nào: gán loại này quá nhiều (precision thấp) hay bỏ sót nhiều (recall thấp)."""
    style()
    present = {k for s in summaries.values() for k in s["theo_loai"]}
    kinds = [k for k in INTENTS if k in present] + sorted(present - set(INTENTS))
    fig, axes = plt.subplots(1, 2, figsize=(14, 4.8))
    for ax, key, title in zip(
        axes,
        ("precision", "recall"),
        (
            "Precision: when the model says\n'this intent is category X',\nhow often it is right",
            "Recall: of all intents that truly\nbelong to category X,\nhow many the model finds",
        ),
        strict=True,
    ):
        series = {n: [s["theo_loai"].get(k, {}).get(key) for k in kinds] for n, s in summaries.items()}
        grouped_bars(ax, kinds, series)
        ax.set_title(title, fontsize=10)
        ax.tick_params(axis="x", labelsize=8.5)
        plt.setp(
            ax.get_xticklabels(), rotation=20, ha="right"
        )  # xoay nhẹ để tên loại dài không dính vào nhau
    handles, names = axes[0].get_legend_handles_labels()
    fig.legend(handles, names, loc="lower center", bbox_to_anchor=(0.5, -0.1), ncol=len(names), frameon=False)
    fig.suptitle("Precision and recall per category", fontweight="bold", y=1.08)
    return save(fig, path)


def chart_f1_per_label(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """F1 theo nhãn 'loại/nhiệm vụ' dạng bảng nhiệt: mỗi hàng một nhãn (xếp theo số ý), mỗi cột một model.
    Ô có viền đen là model tốt nhất của hàng đó; nhãn dưới 10 ý tô xám vì điểm dao động mạnh."""
    style()
    support: dict[str, int] = {}
    for s in summaries.values():
        for k, v in s["theo_nhan"].items():
            support[k] = max(support.get(k, 0), v["ho_tro"])
    labels = sorted((k for k, n in support.items() if n > 0), key=lambda k: (-support[k], k))
    names = list(summaries)
    matrix = [[s["theo_nhan"].get(k, {}).get("f1", 0.0) for s in summaries.values()] for k in labels]
    fig, ax = plt.subplots(figsize=(3.8 + 1.7 * len(names), 1.6 + 0.3 * len(labels)))
    im = ax.imshow(matrix, cmap="Blues", vmin=0, vmax=1, aspect="auto")
    for i, row in enumerate(matrix):
        best = max(row)
        for j, v in enumerate(row):
            ax.text(
                j,
                i,
                f"{v:.2f}",
                ha="center",
                va="center",
                fontsize=8,
                color="white" if v > 0.55 else "#222222",
            )
            if (
                v == best and best > 0 and support[labels[i]] >= 10
            ):  # viền đen quanh ô tốt nhất, chỉ ở nhãn đủ mẫu
                ax.add_patch(plt.Rectangle((j - 0.5, i - 0.5), 1, 1, fill=False, edgecolor="black", lw=1.6))
    ax.set_xticks(range(len(names)), names, fontsize=9)
    ax.xaxis.tick_top()
    ax.set_yticks(range(len(labels)), [f"{k}  (n={support[k]})" for k in labels], fontsize=8)
    for tick, k in zip(ax.get_yticklabels(), labels, strict=True):
        if support[k] < 10:
            tick.set_color("#999999")
    ax.grid(False)
    ax.tick_params(length=0)
    bar = fig.colorbar(im, ax=ax, fraction=0.05, pad=0.03, aspect=40)
    bar.set_label("F1 score (darker = higher, 1.00 = perfect)", fontsize=8.5)
    bar.ax.tick_params(labelsize=8)
    # Tiêu đề gắn vào trục (không dùng suptitle) để nằm sát bảng, không chừa khoảng trắng lớn
    ax.set_title(
        "F1 per category/task label\n(n = intents in ground truth; black box = best model"
        + ("; grey = fewer than 10 intents, unreliable)" if any(n < 10 for n in support.values()) else ")"),
        fontsize=10,
        pad=14,
    )
    return save(fig, path)


def heatmap(ax: Any, c: dict[str, Any], title: str) -> Any:
    rows, cols, matrix = c["labels"], c.get("col_labels", c["labels"]), c["matrix"]
    norm = [[(v / sum(row)) if sum(row) else 0 for v in row] for row in matrix]  # mỗi hàng chia cho tổng hàng
    im = ax.imshow(norm, cmap="Blues", vmin=0, vmax=1)
    ax.set_xticks(range(len(cols)), cols, rotation=40, ha="right", fontsize=8)
    ax.set_yticks(range(len(rows)), rows, fontsize=8)
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
    # đường kẻ tách phần loại ý với hai cột đặc biệt (none, invalid) và hàng (none)
    ax.axvline(len(INTENTS) - 0.5, color="#888888", lw=1)
    if INVALID in cols:
        ax.axvline(cols.index(INVALID) - 0.5, color="#888888", lw=1)
    ax.axhline(len(INTENTS) - 0.5, color="#888888", lw=1)
    ax.set_xlabel("Predicted")
    ax.set_ylabel("True label")
    ax.set_title(title, fontsize=10)
    ax.grid(False)
    return im


def chart_confusion(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Ma trận nhầm lẫn của từng model đặt cạnh nhau (ô = số ý, màu = tỉ lệ trên hàng).
    Cột (invalid) là các ý thuộc tin model không trả được kết quả dùng được: tách riêng để không lẫn với nhầm loại ý."""
    style()
    n = len(summaries)
    fig, axes = plt.subplots(1, n, figsize=(6.2 * n, 5.6), squeeze=False, gridspec_kw={"wspace": 0.8})
    images = [heatmap(ax, s["nham_lan"], name) for ax, (name, s) in zip(axes[0], summaries.items(), strict=True)]
    bar = fig.colorbar(images[-1], ax=axes[0].tolist(), fraction=0.025, pad=0.03, shrink=0.8)
    bar.set_label("Share of the true-label row (darker = higher)", fontsize=9)
    bar.ax.tick_params(labelsize=8)
    fig.suptitle(
        f"Confusion matrix of intent categories (cell = intents, shade = share of the row)\n"
        f"{NONE}: missing intent (column) or extra intent (row); {INVALID}: no usable response",
        fontweight="bold",
        fontsize=11,
        y=0.93,  # sát hình: hai ma trận vuông nên không cao hết khung
    )
    return save(fig, path)


def chart_errors(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Thanh chồng 100% theo từng model. Mọi đoạn khác 0 đều có số: đoạn từ 4% ghi trong đoạn, hẹp hơn thì ghi phía trên thanh và kẻ
    đường trỏ vào đúng đoạn (dưới 1% ghi thêm số tin)."""
    style()
    fig, ax = plt.subplots(figsize=(10, 1.8 + 0.9 * len(summaries)))
    names = list(summaries)
    left = [0.0] * len(names)
    bar_h = 0.55
    callouts: list[list[tuple[float, str]]] = [[] for _ in names]  # theo từng thanh: (vị trí giữa đoạn, chữ)
    for k, key in enumerate(ERRORS):
        if not any(s["loi"][key] for s in summaries.values()):
            continue  # loại lỗi không xảy ra ở model nào: không đưa vào biểu đồ và chú giải
        shares = [s["loi"][key] / s["so_tin"] for s in summaries.values()]
        ax.barh(
            names,
            shares,
            left=left,
            color=ERROR_COLORS[k],
            label=ERROR_NAMES[key],
            height=bar_h,
        )
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
            elif v > 0:
                # Đoạn hẹp (kể cả dưới 1%): ghi phía trên thanh và kẻ đường trỏ vào. Đoạn dưới 1% ghi thêm số tin
                count = list(summaries.values())[i]["loi"][key]
                callouts[i].append((lft + v / 2, f"{v:.1%}" if v >= 0.01 else f"{v:.1%}\n({count})"))
        left = [a + b for a, b in zip(left, shares, strict=True)]
    for i, items in enumerate(callouts):
        if items:
            leader_labels(ax, items, i - bar_h / 2, i - bar_h / 2 - 0.2, gap=0.055, lo=0.03, hi=1.0)
    ax.set_xlim(0, 1)
    ax.set_ylim(len(names) - 0.5, -1.0)  # chừa chỗ phía trên thanh cho số của đoạn nhỏ (trục y đảo)
    ax.set_xticks([0, 0.25, 0.5, 0.75, 1], ["0%", "25%", "50%", "75%", "100%"])
    ax.set_title("Outcome per message")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.12), ncol=3, fontsize=9, frameon=False)
    return save(fig, path)


def chart_groups(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Tỉ lệ đúng hoàn toàn theo: số ý trong tin, có hay không có ngữ cảnh."""
    style()
    fig, axes = plt.subplots(1, 2, figsize=(9, 4.4), gridspec_kw={"width_ratios": [2.2, 2]})
    for ax, key, title in zip(
        axes,
        ("theo_so_y", "theo_ngu_canh"),
        ("By number of intents per message", "By context"),
        strict=True,
    ):
        names = sorted(
            {g for s in summaries.values() for g in s[key]},
            key=lambda g: (INTENTS.index(g) if g in INTENTS else 0, g),
        )
        series = {n: [s[key].get(g, {}).get("dung_het") for g in names] for n, s in summaries.items()}
        labels = {
            "co_ngu_canh": "with context",
            "khong_ngu_canh": "no context",
            "1": "1 intent",
            "2": "2 intents",
            "3+": "3+ intents",
        }
        sizes = {g: max(s[key].get(g, {}).get("so_tin", 0) for s in summaries.values()) for g in names}
        errors = {
            n: [tuple(s[key].get(g, {}).get("khoang_tin_cay_dung_het", (0.0, 0.0))) for g in names]
            for n, s in summaries.items()
        }
        grouped_bars(ax, [f"{labels.get(g, g)}\n(n={sizes[g]})" for g in names], series, errors)
        ax.set_title(title)
        ax.tick_params(axis="x", labelsize=8)
    handles, names = axes[0].get_legend_handles_labels()
    fig.legend(
        handles, names, loc="lower center", bbox_to_anchor=(0.5, -0.06), ncol=len(names), frameon=False
    )
    fig.suptitle("Exact-match rate by message group (black bars: 95% Wilson interval)", fontweight="bold")
    return save(fig, path)


def chart_latency(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    """Thời gian trả lời, nói bằng chữ dễ hiểu: trung bình, mốc mà một nửa số tin và 9 trong 10 tin đã có kết quả, và tin nhiều ý thì lâu
    hơn bao nhiêu. Số ghi thẳng trên cột. Không dùng hộp và ria (box plot) vì người không quen biểu đồ thống kê rất khó đọc."""
    data = {n: s["thoi_gian"]["mau"] for n, s in summaries.items() if s.get("thoi_gian")}
    if not data:
        return None
    style()
    names = list(data)
    overview = {n: [statistics.mean(v), percentile(v, 0.5), percentile(v, 0.9)] for n, v in data.items()}
    by_count = {n: summaries[n]["thoi_gian"].get("theo_so_y") or {} for n in names}
    keys = sorted({k for g in by_count.values() for k in g})
    panels = 2 if keys else 1
    fig, axes = plt.subplots(
        1, panels, figsize=(6.2 * panels, 4.6), squeeze=False, gridspec_kw={"width_ratios": [1.2, 1][:panels]}
    )

    def draw(ax: Any, labels: list[str], series: dict[str, list[float]]) -> None:
        width = 0.8 / len(series)
        for i, (name, values) in enumerate(series.items()):
            xs = [g + (i - (len(series) - 1) / 2) * width for g in range(len(labels))]
            container = ax.bar(xs, values, width * 0.92, label=name, color=color(i))
            ax.bar_label(
                container,
                labels=[f"{v:.2f}s" for v in values],
                fontsize=9,
                padding=3,
                rotation=90 if len(series) >= 3 else 0,
            )
        ax.set_xticks(range(len(labels)), labels, fontsize=8.5)
        ax.margins(y=0.24 if len(series) >= 3 else 0.16)
        ax.set_ylabel("Seconds per message")

    labels = [
        "Mean",
        "Median\n(half of messages\nare faster)",
        "P90\n(9 in 10 messages\nare faster)",
    ]
    draw(axes[0][0], labels, overview)
    axes[0][0].set_title("How long does one message take?", fontsize=10)
    axes[0][0].legend(loc="upper left")
    if keys:
        counts = {k: max((g[k]["so_tin"] for g in by_count.values() if k in g), default=0) for k in keys}
        names_y = {"1": "1 intent", "2": "2 intents", "3+": "3+ intents"}
        draw(
            axes[0][1],
            [f"{names_y.get(k, k)}\n({counts[k]} messages)" for k in keys],
            {n: [by_count[n].get(k, {}).get("trung_binh", 0.0) for k in keys] for n in names},
        )
        axes[0][1].set_title("More intents per message take longer (mean latency)", fontsize=10)
        axes[0][1].set_ylabel("")
    slowest = "   |   ".join(f"{n}: {max(v):.2f}s" for n, v in data.items())
    fig.suptitle("Response time", fontweight="bold", y=1.02)
    fig.text(0.5, 0.955, f"Slowest message:   {slowest}", ha="center", fontsize=9, color="#444444")
    return save(fig, path)


def chart_confidence(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    """Biểu đồ hiệu chuẩn (reliability diagram): trục ngang là confidence, trục dọc là tỉ lệ đúng LOẠI Ý thực tế trong khoảng đó.
    Điểm nằm trên đường chéo là hiệu chuẩn tốt; nằm dưới là tự tin quá mức. Chỉ tin hợp lệ (tin lùi về mơ hồ có confidence 0
    là giá trị mặc định của hệ thống). Confidence là xác suất token của chữ type nên chỉ đo độ chắc về loại ý."""
    usable = {n: s for n, s in summaries.items() if s.get("hieu_chuan") and s["hieu_chuan"]["khoang"]}
    if not usable:
        return None
    style()
    fig, axes = plt.subplots(1, len(usable), figsize=(4.6 * len(usable), 4.6), squeeze=False)
    for i, (ax, (name, s)) in enumerate(zip(axes[0], usable.items(), strict=True)):
        cal = s["hieu_chuan"]
        ax.plot([0, 1], [0, 1], ls="--", color="#888888", lw=1, label="Perfect calibration")
        xs = [b["confidence"] for b in cal["khoang"]]
        ys = [b["accuracy"] for b in cal["khoang"]]
        sizes = [20 + 400 * b["so_tin"] / cal["so_tin"] for b in cal["khoang"]]
        ax.plot(xs, ys, color=color(i), lw=1.2, alpha=0.7)
        ax.scatter(
            xs,
            ys,
            s=sizes,
            color=color(i),
            edgecolor="white",
            zorder=3,
            label="Confidence bin (size = messages)",
        )
        for b, x, y in zip(cal["khoang"], xs, ys, strict=True):
            ax.annotate(
                str(b["so_tin"]), (x, y), xytext=(0, 9), textcoords="offset points", ha="center", fontsize=7
            )
        ax.set_xlim(0, 1.05)
        ax.set_ylim(0, 1.1)
        invalid = s["so_tin"] - s["so_tin_hop_le"]
        ax.set_title(
            f"{name}\nECE = {cal['ece']:.3f}  (n = {cal['so_tin']} valid, {invalid} invalid excluded)",
            fontsize=9,
        )
        ax.set_xlabel("Confidence (probability of the predicted category)")
        ax.set_ylabel("Share of messages with correct category")
        ax.legend(loc="upper left", fontsize=7.5, frameon=False)
    fig.suptitle(
        "Reliability of the confidence score (below the diagonal = overconfident)", fontweight="bold", y=1.08
    )
    return save(fig, path)


def chart_roc(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    """Đường ROC và AUC của độ tin cậy: confidence có phân biệt được tin đoán đúng loại ý với tin đoán sai không.
    Hai khung: mọi tin (tin lùi về mơ hồ có confidence 0 do hệ thống gán) và chỉ tin hợp lệ (công bằng hơn khi nói về độ tin cậy
    của chính model). Mỗi model một đường, AUC ghi trong chú thích."""
    panels = [("tat_ca", "All messages (invalid ones have confidence 0)"), ("hop_le", "Valid responses only")]
    if not any((s.get("roc") or {}).get(k) for s in summaries.values() for k, _ in panels):
        return None
    style()
    fig, axes = plt.subplots(1, 2, figsize=(11, 5.4))
    for ax, (key, title) in zip(axes, panels, strict=True):
        ax.plot([0, 1], [0, 1], ls="--", color="#888888", lw=1, label="Random guess (AUC = 0.50)")
        for i, (name, s) in enumerate(summaries.items()):
            curve = (s.get("roc") or {}).get(key)
            if not curve:
                continue
            ax.plot(
                curve["fpr"], curve["tpr"], color=color(i), lw=2,
                label=f"{name}: AUC = {curve['auc']:.3f}\n({curve['so_tin_dung']} correct, {curve['so_tin_sai']} wrong)",
            )  # fmt: skip
        ax.set_xlim(0, 1)
        ax.set_ylim(0, 1.02)
        ax.set_aspect("equal")
        ax.set_title(title, fontsize=10)
        ax.set_xlabel("False positive rate\n(share of wrong messages that pass the threshold)", fontsize=9)
        ax.set_ylabel("True positive rate\n(share of correct messages that pass the threshold)", fontsize=9)
        ax.legend(loc="lower right", fontsize=8, frameon=True)
    fig.suptitle("ROC curve: can the confidence score separate correct from wrong categories?", fontweight="bold")
    return save(fig, path)


def chart_dataset(records: list[dict[str, Any]], path: Path) -> Path:
    """Số dòng dữ liệu theo loại và theo tập train, val, test."""
    style()
    kinds = list(INTENTS)
    splits = [("train", "#0072B2"), ("val", "#E69F00"), ("test", "#009E73")]
    fig, ax = plt.subplots(figsize=(9, 4.4))
    bottom = [0] * len(kinds)
    totals = [sum(1 for r in records if r["type"] == k) for k in kinds]
    # Đoạn cao hơn mốc này thì ghi số ngay trong đoạn, thấp hơn thì ghi bên phải cột
    tall = 0.04 * max(totals)
    for split, col in splits:
        counts = [sum(1 for r in records if r["type"] == k and r["split"] == split) for k in kinds]
        ax.bar(kinds, counts, bottom=bottom, color=col, label=f"{split} ({sum(counts)})")
        for i, (count, base) in enumerate(zip(counts, bottom, strict=True)):
            if count == 0:
                continue
            if count >= tall:
                ax.text(
                    i, base + count / 2, str(count), ha="center", va="center", fontsize=8,
                    color="#222222" if split == "val" else "white",
                )  # fmt: skip
            else:
                ax.annotate(
                    f"{split} {count}", xy=(i + 0.4, base + count / 2), xytext=(i + 0.52, base + count / 2),
                    ha="left", va="center", fontsize=7.5, color="#222222",
                    arrowprops={"arrowstyle": "-", "lw": 0.6, "color": "#555555", "shrinkA": 0, "shrinkB": 0},
                )  # fmt: skip
        bottom = [a + b for a, b in zip(bottom, counts, strict=True)]
    for i, total in enumerate(bottom):
        ax.text(i, total + max(bottom) * 0.01, str(total), ha="center", fontsize=8, fontweight="bold")
    ax.set_xlim(-0.6, len(kinds) - 0.4 + 0.5)  # chừa chỗ bên phải cho số của các đoạn thấp ở cột cuối
    ax.set_ylabel("Rows")
    ax.set_title(f"Messages by primary category and split (total {len(records)})", pad=10)
    # Chú thích nằm ở góc trên bên phải, trên một hàng; trục y nới thêm để không đè lên cột nào
    ax.set_ylim(0, max(bottom) * 1.2)
    ax.legend(loc="upper right", ncol=len(splits), frameon=False, fontsize=9)
    return save(fig, path)


def chart_training(
    log: list[dict[str, float]], summary: dict[str, Any], path: Path, baseline_val_loss: float | None = None
) -> Path:
    """Đường train loss và val loss theo bước. Hai khung trên (toàn bộ quá trình và phóng to val loss) chỉ ghi vài số chính; mọi số của
    từng lần đo val nằm trong bảng bên dưới, nên không có chữ chồng lên đường và không phải gióng xuống trục."""
    style()
    trains = [p for p in log if "train_loss" in p]
    evals = [p for p in log if "eval_loss" in p]
    fig = plt.figure(figsize=(12, 7.4))
    grid = fig.add_gridspec(2, 2, height_ratios=[3.3, 1.5], hspace=0.42, wspace=0.16)
    ax_full = fig.add_subplot(grid[0, 0])
    ax_zoom = fig.add_subplot(grid[0, 1])
    ax_table = fig.add_subplot(grid[1, :])

    train_x, train_y = [p["step"] for p in trains], [p["train_loss"] for p in trains]
    val_x, val_y = [p["step"] for p in evals], [p["eval_loss"] for p in evals]
    for ax, train_width in ((ax_full, 1.5), (ax_zoom, 1.0)):
        ax.plot(
            train_x,
            train_y,
            color="#0072B2",
            lw=train_width,
            alpha=1.0 if ax is ax_full else 0.35,
            label="train loss",
        )
        if evals:
            ax.plot(val_x, val_y, color="#D55E00", lw=2, marker="o", ms=5, label="val loss")
        if baseline_val_loss is not None:
            ax.axhline(
                baseline_val_loss,
                color="#E69F00",
                ls=":",
                lw=1.5,
                label=f"base model ({baseline_val_loss:.3f})",
            )
        ax.set_xlabel("Training step")
        ax.set_ylabel("Loss (lower is better)")
        ax.legend(loc="upper right")

    # Khung trái: toàn bộ quá trình, chỉ ghi điểm đầu, train loss cuối và val loss thấp nhất
    if trains:
        ax_full.annotate(
            f"step {trains[0]['step']:.0f}: {trains[0]['train_loss']:.4f}", (trains[0]["step"], trains[0]["train_loss"]),
            xytext=(10, -4), textcoords="offset points", fontsize=9,
        )  # fmt: skip
        ax_full.text(
            0.97, 0.5, f"Final train loss: {trains[-1]['train_loss']:.4f} (step {trains[-1]['step']:.0f})",
            transform=ax_full.transAxes, ha="right", va="center", fontsize=9,
            bbox={"facecolor": "white", "edgecolor": "#0072B2", "boxstyle": "round,pad=0.3"},
        )  # fmt: skip
    if summary.get("co_val"):
        epoch = (
            f", epoch {summary['epoch_tot_nhat']:.2f}" if summary.get("epoch_tot_nhat") is not None else ""
        )
        ax_full.axvline(summary["buoc_tot_nhat"], color="#009E73", ls="--", lw=1.3)
        ax_full.text(
            0.97, 0.36, f"Lowest val loss: {summary['val_loss_tot_nhat']:.4f} (step {summary['buoc_tot_nhat']:.0f}{epoch})",
            transform=ax_full.transAxes, ha="right", va="center", fontsize=9,
            bbox={"facecolor": "white", "edgecolor": "#009E73", "boxstyle": "round,pad=0.3"},
        )  # fmt: skip
    ax_full.set_title("Loss over the whole run", fontsize=10)

    # Khung phải: phóng to val loss từ lần đo đầu tiên, mỗi chấm có số
    if evals:
        ax_zoom.set_xlim(val_x[0] - 30, val_x[-1] + 40)
        ax_zoom.set_ylim(0, max(val_y) * 1.4)
        # Chỉ ghi số ở điểm đầu, điểm thấp nhất và điểm cuối: ghi hết thì các số sát nhau dính vào nhau (bảng bên dưới đã có mọi lần đo)
        shown = {0, len(val_y) - 1, val_y.index(min(val_y))}
        for idx in sorted(shown):
            step, value = val_x[idx], val_y[idx]
            edge = (
                "left" if idx == 0 else "right" if idx == len(val_y) - 1 else "center"
            )  # nhãn ở mép thì đẩy vào trong, khỏi bị cắt
            ax_zoom.annotate(
                f"{value:.4f}", (step, value), textcoords="offset points",
                # Điểm cuối: nhãn nằm dưới chấm, để không bị đọc nhầm thành số của chấm đứng trước
                xytext=(8 if edge == "left" else 0, -18 if edge == "right" else 14),
                ha=edge, fontsize=9, color="#D55E00", fontweight="bold",
                bbox={"facecolor": "white", "alpha": 0.85, "edgecolor": "none", "pad": 0.6},
            )  # fmt: skip
    ax_zoom.set_ylabel("")
    ax_zoom.set_title("Val loss zoomed in from the first evaluation", fontsize=10)

    # Bảng số của từng lần đo val
    ax_table.axis("off")
    if evals and trains:
        before = [[t for t in trains if t["step"] <= e["step"]] for e in evals]
        train_at = [b[-1]["train_loss"] if b else float("nan") for b in before]
        rows = [
            [f"{e['step']:.0f}" for e in evals],
            [f"{e['epoch']:.2f}" if e.get("epoch") is not None else "-" for e in evals],
            [f"{t:.4f}" for t in train_at],
            [f"{e['eval_loss']:.4f}" for e in evals],
            [f"{e['eval_loss'] - t:.4f}" for e, t in zip(evals, train_at, strict=True)],
        ]
        table = ax_table.table(
            cellText=rows,
            rowLabels=["Training step", "Epoch", "Train loss (latest logged)", "Val loss", "Val minus train"],
            loc="center", cellLoc="center",
        )  # fmt: skip
        table.auto_set_font_size(False)
        table.set_fontsize(9.5)
        table.scale(1, 1.55)
        best = min(range(len(evals)), key=lambda i: val_y[i])
        for row in range(len(rows)):
            table[(row, best)].set_facecolor("#E3F4EC")  # tô nhạt lần đo có val loss thấp nhất
    fig.suptitle("Training curves", fontweight="bold", y=0.97)
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
    gaps = [g for _, g in pts]
    for i, (step, gap) in enumerate(pts):  # ghi số chênh ở từng chấm, khỏi gióng xuống trục
        neighbours = [gaps[j] for j in (i - 1, i + 1) if 0 <= j < len(gaps)]
        below = bool(neighbours) and gap < min(neighbours)  # đáy: nhãn nằm dưới để không đè lên đường kẻ
        ax.annotate(
            f"{gap:.4f}", (step, gap), xytext=(0, -14 if below else 9), textcoords="offset points",
            ha="center", fontsize=8.5,
            bbox={"facecolor": "white", "alpha": 0.85, "edgecolor": "none", "pad": 0.6},
        )  # fmt: skip
    ax.margins(y=0.15)
    ax.axhline(0, color="#555555", lw=0.8)
    ax.set_xlabel("Training step")
    ax.set_ylabel("val loss minus train loss")
    ax.set_title("Val-train gap (steadily rising = overfitting)")
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
    ax.set_xlabel("Training step")
    ax.set_title("Validation metrics over training steps")
    ax.legend()
    return save(fig, path)


def chart_invalid_causes(summaries: dict[str, dict[str, Any]], path: Path) -> Path | None:
    """Vì sao tin lùi về mơ hồ (không trả được kết quả dùng được), theo từng model. Chỉ vẽ khi file dự đoán có ghi lý do (loi_ly_do)
    nghĩa là được tạo bằng eval_slm bản mới; file cũ không có thì trả None."""
    have = {n: s["ly_do_loi"] for n, s in summaries.items() if s.get("ly_do_loi")}
    if not have:
        return None
    style()
    reasons = sorted({r for d in have.values() for r in d})
    fig, ax = plt.subplots(figsize=(9, 1.6 + 0.8 * len(have)))
    names = list(have)
    left = [0] * len(names)
    widest = max(sum(d.values()) for d in have.values()) or 1
    small: list[list[tuple[float, str]]] = [[] for _ in names]
    for k, reason in enumerate(reasons):
        counts = [have[n].get(reason, 0) for n in names]
        ax.barh(names, counts, left=left, color=color(k), label=reason.replace("_", " "), height=0.55)
        for i, (c, lft) in enumerate(zip(counts, left, strict=True)):
            if c >= 0.06 * widest:  # đoạn đủ rộng thì ghi trong đoạn
                ax.text(lft + c / 2, i, str(c), ha="center", va="center", fontsize=8, color="white")
            elif c:
                small[i].append((lft + c / 2, str(c)))
        left = [a + b for a, b in zip(left, counts, strict=True)]
    # Trục y đảo: giá trị nhỏ hơn nằm cao hơn, nên chữ đặt ở i - 0.45 là phía trên thanh
    for i, items in enumerate(small):
        if items:
            leader_labels(ax, items, i - 0.275, i - 0.45, gap=0.055 * widest, lo=0.02 * widest, hi=1.02 * widest)
    ax.set_xlim(0, widest * 1.06)
    ax.set_ylim(len(names) - 0.5, -0.85)
    ax.set_xlabel("Messages with no usable response")
    ax.set_title("Why responses were invalid (messages)")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.25), ncol=min(len(reasons), 4), frameon=False)
    return save(fig, path)


def chart_paired(comparisons: dict[str, dict[str, Any]], base: str, path: Path) -> Path | None:
    """Biểu đồ rừng (forest plot): hiệu của model gốc so (base) trừ từng model khác, kèm khoảng tin cậy 95% bootstrap theo cặp.
    Khoảng không chạm đường 0 nghĩa là base hơn hẳn (có ý nghĩa thống kê)."""
    if not comparisons:
        return None
    style()
    metrics = [
        ("valid", "Valid response"),
        ("category", "Correct category"),
        ("exact", "Exact match"),
        ("macro_f1", "Macro-F1"),
    ]
    fig, ax = plt.subplots(figsize=(9, 1.6 + 0.9 * len(metrics)))
    others = list(comparisons)
    step = 0.8 / max(len(others), 1)
    for g, (key, _label) in enumerate(metrics):
        for i, other in enumerate(others):
            c = comparisons[other][key]
            y = g + (i - (len(others) - 1) / 2) * step
            lo, hi = c["khoang_tin_cay_95"]
            ax.errorbar(
                c["hieu"] * 100, y, xerr=[[(c["hieu"] - lo) * 100], [(hi - c["hieu"]) * 100]],
                fmt="o", color=color(i + 1), capsize=3, label=f"vs {other}" if g == 0 else None,
            )  # fmt: skip
            ax.annotate(
                f"{c['hieu'] * 100:+.1f}",
                (hi * 100, y),
                xytext=(6, 0),
                textcoords="offset points",
                va="center",
                fontsize=8,
            )
    ax.axvline(0, color="#444444", lw=1)
    ax.set_yticks(range(len(metrics)), [m[1] for m in metrics])
    ax.invert_yaxis()
    ax.set_xlabel(f"{base} minus other model (percentage points, 95% paired bootstrap interval)")
    ax.set_title(f"How much better is {base}? (right of 0 = {base} is better)")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.22), ncol=len(others), frameon=False)
    return save(fig, path)
