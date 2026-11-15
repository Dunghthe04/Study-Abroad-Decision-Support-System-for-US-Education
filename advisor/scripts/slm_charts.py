"""Vẽ biểu đồ đánh giá SLM từ kết quả của scripts.slm_metrics. Mỗi hàm vẽ một biểu đồ ra một file PNG.

Màu cố định theo thứ tự các lần chạy: cam (model gốc), xanh dương (model đã huấn luyện), xanh lá (lần thứ ba nếu có).
Bộ màu Okabe-Ito, phân biệt được cả với người khó phân biệt màu.
"""

import statistics
import textwrap
from pathlib import Path
from typing import Any

import matplotlib

matplotlib.use("Agg")  # vẽ ra file, không mở cửa sổ
import matplotlib.pyplot as plt  # noqa: E402

from app.schemas.slm import INTENTS  # noqa: E402
from scripts.slm_metrics import ERRORS, LEVELS, NONE, percentile  # noqa: E402

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
    "sai_follow_up": "Sai follow_up",
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


# Chú thích dưới mỗi biểu đồ: cách đọc và ý nghĩa, để dán ảnh riêng lẻ vào báo cáo hay slide vẫn hiểu được
NOTES = {
    "compare": (
        "Cách đọc: mỗi nhóm cột là một mức chấm, từ lỏng (trái) đến chặt (phải), cột càng cao càng tốt. Mức sau gồm cả mức trước. "
        "'Đúng hoàn toàn' là tin được tách đúng loại ý, thứ tự, nhiệm vụ, thực thể và câu nối tiếp.\n"
        "Vạch đen là khoảng tin cậy 95%, số trong ngoặc vuông dưới mỗi cột là hai đầu của vạch (thấp-cao): hai cột có vạch không chồng "
        "nhau thì khác biệt là thật, không phải do may rủi."
    ),
    "f1_type": (
        "Cách đọc: mỗi nhóm cột là một loại ý, cột càng cao càng tốt. F1 (0 đến 100%) là điểm tổng hợp của precision (model đoán loại này "
        "thì đúng bao nhiêu) và recall (trong các ý thật thuộc loại này, model tìm ra bao nhiêu)."
    ),
    "precision_recall": (
        "Cách đọc: mỗi nhóm cột là một loại ý, cột càng cao càng tốt, số trên đầu cột là phần trăm. Precision (khung trái): trong các ý model "
        "gán cho loại này, bao nhiêu ý thật sự đúng loại. Recall (khung phải): trong các ý thật sự thuộc loại này, model tìm ra bao nhiêu.\n"
        "Precision thấp mà recall cao nghĩa là model gán loại đó quá tay (nhầm nhiều ý khác vào đây). Recall thấp mà precision cao nghĩa là "
        "model gán ít nhưng đúng, còn bỏ sót nhiều. F1 (biểu đồ 02) là điểm gộp của hai số này."
    ),
    "f1_label": (
        "Cách đọc: mỗi hàng là một nhãn 'loại/nhiệm vụ' (ví dụ knowledge/school_info), n là số ý thật có nhãn này trong tập đo. "
        "Thanh càng dài (gần 1.0) càng tốt; số ở đầu thanh là điểm F1 (0 đến 1), ghi 0.00 nghĩa là model không làm đúng lần nào.\n"
        "Nhãn có n nhỏ (khoảng 10 trở xuống) thì điểm dao động mạnh, chỉ nên xem như gợi ý."
    ),
    "confusion": (
        "Cách đọc: mỗi hàng là loại ý đúng, mỗi cột là loại model đoán, số trong ô là số ý. Ô trên đường chéo là đoán đúng; "
        "ô ngoài đường chéo là nhầm lẫn. Màu càng đậm là chiếm tỉ lệ càng lớn trong hàng đó.\n"
        "Cột (không có) là ý bị bỏ sót; dòng (không có) là ý model tự thêm thừa."
    ),
    "errors": (
        "Cách đọc: mỗi thanh là 100% số tin đo. Phần xanh lá (đúng hoàn toàn) càng dài càng tốt, các màu còn lại là lỗi. "
        "Mỗi tin chỉ vào một nhóm: nhóm lỗi đầu tiên gặp khi chấm từ mức lỏng đến mức chặt (không dùng được, thiếu ý, thừa ý, "
        "sai loại ý, sai thứ tự, sai nhiệm vụ, sai thực thể, sai câu nối tiếp). Đoạn từ 4% ghi số ngay trong đoạn, đoạn từ 1% đến dưới 4% "
        "ghi số phía trên thanh; mọi đoạn (kể cả dưới 1%) đều có phần trăm và số tin ở chú thích bên dưới."
    ),
    "groups": (
        "Cách đọc: mỗi cột là tỉ lệ tin đúng hoàn toàn trong một nhóm tin, càng cao càng tốt. Hai biểu đồ chia theo hai cách: số ý trong tin "
        "(1, 2, 3 trở lên) và tin có hay không có ngữ cảnh câu trước.\n"
        "Cột thấp cho biết nhóm tin model còn yếu."
    ),
    "latency": (
        "Cách đọc: mỗi cột là số giây model mất để trả lời một tin, càng thấp càng nhanh. Khung trái: thời gian trung bình, mốc mà một nửa "
        "số tin nhanh hơn (trung vị) và mốc mà 9 trong 10 tin nhanh hơn. Khung phải: thời gian trung bình theo số ý trong tin; tin nhiều ý "
        "phải viết JSON dài hơn nên lâu hơn.\n"
        "Dòng dưới tiêu đề ghi tin chậm nhất của mỗi model. Thời gian phụ thuộc phần cứng của máy chạy đo."
    ),
    "confidence": (
        "Cách đọc: trục ngang là độ tin cậy model tự báo (0 đến 1), trục dọc là số tin. Trong mỗi khoảng có hai cột đứng cạnh nhau: "
        "xanh là tin đúng hoàn toàn, cam là tin sai. Model có độ tin cậy dùng được thì cột cam nằm bên trái và cột xanh dồn bên phải.\n"
        "Số trên đầu mỗi cột là số tin. Khung trong góc cho biết trong các tin sai có bao nhiêu tin model vẫn báo chắc từ 0.9.\n"
        "Nếu cột cam cũng dồn ở gần 1.0, model sai mà vẫn rất tự tin, nên không dùng độ tin cậy để tự phát hiện lỗi."
    ),
    "dataset": (
        "Cách đọc: mỗi cột là một loại ý, chia màu theo tập train (để học), val (để theo dõi lúc huấn luyện) và test (chỉ để đo cuối). "
        "Số trong mỗi đoạn màu là số dòng của tập đó (đoạn thấp ghi bên phải cột), số in đậm trên đầu cột là tổng số dòng của loại đó.\n"
        "Đây là toàn bộ dữ liệu; khi xuất file huấn luyện có bỏ các câu trùng chữ với câu test nên số dòng train và val thực tế ít hơn."
    ),
    "training": (
        "Cách đọc: trục ngang là bước huấn luyện, trục dọc là loss (độ sai, càng thấp càng tốt). Đường xanh là train loss (sai trên dữ liệu "
        "đang học, ghi dày); đường cam là val loss (sai trên dữ liệu chưa học, đo thưa hơn). Cả hai cùng giảm là đang học tốt. Khung trái là "
        "toàn bộ quá trình, khung phải phóng to val loss. Bảng bên dưới ghi số của từng lần đo val; ô tô xanh là lần đo có val loss thấp nhất.\n"
        "Nếu val loss tăng lại trong khi train loss vẫn giảm là học thuộc (overfit)."
    ),
    "gap": (
        "Cách đọc: mỗi chấm là val loss trừ train loss ở một lần đo val. Chênh nhỏ và ổn định là model học tốt mà không học thuộc; "
        "chênh tăng đều qua các lần đo là dấu hiệu overfit.\n"
        "Lưu ý: train loss lấy ở lần ghi gần nhất nên tự dao động, đường lên xuống nhẹ chưa phải bằng chứng."
    ),
    "val_metrics": (
        "Cách đọc: mỗi đường là một chỉ số đo trên tập val sau mỗi lần đánh giá lúc huấn luyện (0 đến 1, càng cao càng tốt). "
        "Dùng để xem chất lượng thật có tăng theo bước không, bổ sung cho val loss."
    ),
}  # fmt: skip


def add_note(fig: Any, note: str) -> None:
    """Chữ chú thích dưới đáy biểu đồ (dưới cả legend), xuống dòng theo bề rộng ảnh."""
    fig.canvas.draw()
    box = fig.get_tightbbox(fig.canvas.get_renderer())  # inch, gốc ở góc dưới trái của figure
    width_in, height_in = fig.get_size_inches()
    chars = max(40, int(box.width * 14))  # khoảng 14 ký tự mỗi inch ở cỡ chữ 9
    text = "\n".join(textwrap.fill(part, width=chars) for part in note.split("\n"))
    fig.text(
        box.x0 / width_in,
        (box.y0 - 0.15) / height_in,
        text,
        ha="left",
        va="top",
        fontsize=9,
        color="#333333",
        linespacing=1.4,
    )


def save(fig: Any, path: Path, note: str = "") -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    if note:
        add_note(fig, note)
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
                text = f"{v:.0%}" if v <= 1 else f"{v:.2f}"
                if errors and name in errors:
                    lo, hi = errors[name][k]
                    if hi > lo:  # khoảng tin cậy có bề rộng thì ghi số, để không phải gióng xuống trục
                        text += f"\n[{lo * 100:.0f}-{hi * 100:.0f}%]"
                ax.text(
                    bar.get_x() + bar.get_width() / 2,
                    top + 0.012,
                    text,
                    ha="center",
                    va="bottom",
                    fontsize=7.5 if "\n" not in text else 6.8,
                    linespacing=1.15,
                )
    ax.set_xticks(range(len(groups)), groups)
    ax.set_ylim(0, 1.2 if errors else 1.12)  # có ghi khoảng tin cậy thì cần thêm chỗ phía trên
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
    return save(fig, path, NOTES["compare"])


def chart_f1_per_type(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    style()
    fig, ax = plt.subplots(figsize=(9, 4.5))
    series = {n: [s["theo_loai"].get(t, {}).get("f1") for t in INTENTS] for n, s in summaries.items()}
    grouped_bars(ax, list(INTENTS), series)
    ax.set_title("F1 theo từng loại ý")
    legend_below(ax, len(series))
    return save(fig, path, NOTES["f1_type"])


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
            "Precision: khi model nói “ý này thuộc loại X”\nthì đúng bao nhiêu phần trăm",
            "Recall: trong các ý thật sự thuộc loại X\nmodel tìm ra bao nhiêu phần trăm",
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
    fig.suptitle("Precision và recall theo từng loại ý", fontweight="bold", y=1.08)
    return save(fig, path, NOTES["precision_recall"])


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
        values = [s["theo_nhan"].get(k, {}).get("f1", 0) for k in labels]
        ax.barh(ys, values, height * 0.92, label=name, color=color(i))
        for y, v in zip(
            ys, values, strict=True
        ):  # ghi điểm F1 ở đầu thanh; thanh dài 0 vẫn ghi 0.00 để thấy là bằng 0
            ax.text(v + 0.01, y, f"{v:.2f}", va="center", fontsize=6.5)
    ax.set_yticks(range(len(labels)), [f"{k}  (n={support[k]})" for k in labels], fontsize=7.5)
    ax.invert_yaxis()
    ax.set_xlim(0, 1.1)  # chừa chỗ bên phải cho số
    ax.set_title("F1 theo nhãn loại/nhiệm vụ (n = số ý có trong nhãn đúng)")
    ax.legend(
        loc="upper center", bbox_to_anchor=(0.5, -0.06), ncol=len(summaries), frameon=False
    )  # dưới trục, không che thanh
    return save(fig, path, NOTES["f1_label"])


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
    return save(fig, path, NOTES["confusion"])


def chart_errors(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Thanh chồng 100% theo từng model. Mọi đoạn đều có số: đoạn từ 4% ghi trong đoạn, từ 1% đến dưới 4% ghi phía trên thanh
    (so le hai tầng để không chồng chữ), còn lại (kể cả đoạn dưới 1%) ghi cả phần trăm lẫn số tin trong chú thích."""
    style()
    fig, ax = plt.subplots(figsize=(10, 1.8 + 0.9 * len(summaries)))
    names = list(summaries)
    left = [0.0] * len(names)
    bar_h = 0.55
    callouts: list[list[tuple[float, str]]] = [[] for _ in names]  # theo từng thanh: (vị trí giữa đoạn, chữ)
    for k, key in enumerate(ERRORS):
        shares = [s["loi"][key] / s["so_tin"] for s in summaries.values()]
        counts = [s["loi"][key] for s in summaries.values()]
        # Chú thích gom số của mọi model cho nhóm lỗi này, nên đoạn nhỏ đến đâu vẫn đọc được số chính xác
        detail = " · ".join(
            f"{name} {share:.1%} ({count})" for name, share, count in zip(names, shares, counts, strict=True)
        )
        ax.barh(
            names,
            shares,
            left=left,
            color=ERROR_COLORS[k],
            label=f"{ERROR_NAMES[key]}: {detail}",
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
            elif v >= 0.01:
                callouts[i].append((lft + v / 2, f"{v:.1%}"))
        left = [a + b for a, b in zip(left, shares, strict=True)]
    for i, items in enumerate(callouts):
        for n, (x, text) in enumerate(items):
            y_text = i - bar_h / 2 - 0.1 - 0.22 * (n % 2)  # so le hai tầng; trục y đảo nên trừ là lên trên
            ax.annotate(
                text,
                xy=(x, i - bar_h / 2),
                xytext=(x, y_text),
                ha="center",
                va="bottom",
                fontsize=7.5,
                arrowprops={"arrowstyle": "-", "lw": 0.6, "color": "#555555"},
            )
    ax.set_xlim(0, 1)
    ax.set_ylim(len(names) - 0.5, -1.0)  # chừa chỗ phía trên thanh cho số của đoạn nhỏ (trục y đảo)
    ax.set_xticks([0, 0.25, 0.5, 0.75, 1], ["0%", "25%", "50%", "75%", "100%"])
    ax.set_title("Phân loại kết quả theo từng tin nhắn")
    ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.12), ncol=2, fontsize=8, frameon=False)
    return save(fig, path, NOTES["errors"])


def chart_groups(summaries: dict[str, dict[str, Any]], path: Path) -> Path:
    """Tỉ lệ đúng hoàn toàn theo: số ý trong tin, có hay không có ngữ cảnh."""
    style()
    fig, axes = plt.subplots(1, 2, figsize=(9, 4.4), gridspec_kw={"width_ratios": [2.2, 2]})
    for ax, key, title in zip(
        axes,
        ("theo_so_y", "theo_ngu_canh"),
        ("Theo số ý trong tin", "Theo ngữ cảnh"),
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
    return save(fig, path, NOTES["groups"])


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
            ax.bar_label(container, labels=[f"{v:.2f}s" for v in values], fontsize=9, padding=2)
        ax.set_xticks(range(len(labels)), labels, fontsize=8.5)
        ax.margins(y=0.16)
        ax.set_ylabel("Giây cho một tin")

    labels = [
        "Trung bình",
        "Một nửa số tin\nnhanh hơn mốc này\n(trung vị)",
        "9 trong 10 tin\nnhanh hơn mốc này",
    ]
    draw(axes[0][0], labels, overview)
    axes[0][0].set_title("Một tin mất bao lâu để có kết quả?", fontsize=10)
    axes[0][0].legend(loc="upper left")
    if keys:
        counts = {k: max((g[k]["so_tin"] for g in by_count.values() if k in g), default=0) for k in keys}
        names_y = {"1": "1 ý", "2": "2 ý", "3+": "3 ý trở lên"}
        draw(
            axes[0][1],
            [f"{names_y.get(k, k)}\n({counts[k]} tin)" for k in keys],
            {n: [by_count[n].get(k, {}).get("trung_binh", 0.0) for k in keys] for n in names},
        )
        axes[0][1].set_title("Tin càng nhiều ý càng lâu (thời gian trung bình)", fontsize=10)
        axes[0][1].set_ylabel("")
    slowest = "   |   ".join(f"{n}: {max(v):.2f}s" for n, v in data.items())
    fig.suptitle("Thời gian trả lời", fontweight="bold", y=1.02)
    fig.text(0.5, 0.955, f"Tin chậm nhất:   {slowest}", ha="center", fontsize=9, color="#444444")
    return save(fig, path, NOTES["latency"])


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
        # hai cột cạnh nhau trong mỗi khoảng (không chồng lên nhau, tránh ra màu nâu khó hiểu ở chỗ chồng)
        counts, _, containers = ax.hist(
            [s["tin_cay"]["khi_dung"], s["tin_cay"]["khi_sai"]],
            bins=bins,
            color=["#009E73", "#D55E00"],
            label=["khi đúng hoàn toàn", "khi sai"],
        )
        for container, series in zip(
            containers, counts, strict=True
        ):  # ghi số tin trên đầu mỗi cột, khỏi gióng xuống trục
            ax.bar_label(
                container, labels=[str(int(c)) if c > 0 else "" for c in series], fontsize=7, padding=1.5
            )
        wrong = s["tin_cay"]["khi_sai"]
        sure = sum(c >= 0.9 for c in wrong)
        if wrong:  # câu hỏi chính của biểu đồ: trong các tin sai, bao nhiêu tin model vẫn báo rất chắc
            ax.text(
                0.02,
                0.97,
                f"Tin sai: {len(wrong)}\nvẫn báo chắc từ 0.9: {sure} ({sure / len(wrong):.0%})",
                transform=ax.transAxes,
                va="top",
                fontsize=8,
                bbox={"facecolor": "white", "edgecolor": "#D55E00", "boxstyle": "round,pad=0.3"},
            )
        ax.margins(y=0.12)  # chừa chỗ cho số trên cột cao nhất
        ax.set_title(name, fontsize=10)
        ax.set_xlabel("Độ tin cậy")
        ax.set_ylabel("Số tin")
        ax.legend()
    fig.suptitle("Độ tin cậy của model khi đúng và khi sai", fontweight="bold")
    return save(fig, path, NOTES["confidence"])


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
                ax.text(
                    i + 0.43, base + count / 2, f"{split} {count}", ha="left", va="center", fontsize=7.5,
                    color="#222222",
                )  # fmt: skip
        bottom = [a + b for a, b in zip(bottom, counts, strict=True)]
    for i, total in enumerate(bottom):
        ax.text(i, total + max(bottom) * 0.01, str(total), ha="center", fontsize=8, fontweight="bold")
    ax.set_xlim(-0.6, len(kinds) - 0.4 + 0.5)  # chừa chỗ bên phải cho số của các đoạn thấp ở cột cuối
    ax.set_ylabel("Số dòng")
    ax.set_title(f"Dữ liệu theo loại và theo tập (tổng {len(records)} dòng)")
    ax.legend()
    return save(fig, path, NOTES["dataset"])


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
                label=f"model gốc ({baseline_val_loss:.3f})",
            )
        ax.set_xlabel("Bước huấn luyện")
        ax.set_ylabel("Loss (càng thấp càng tốt)")
        ax.legend(loc="upper right")

    # Khung trái: toàn bộ quá trình, chỉ ghi điểm đầu, train loss cuối và val loss thấp nhất
    if trains:
        ax_full.annotate(
            f"bước {trains[0]['step']:.0f}: {trains[0]['train_loss']:.4f}", (trains[0]["step"], trains[0]["train_loss"]),
            xytext=(10, -4), textcoords="offset points", fontsize=9,
        )  # fmt: skip
        ax_full.text(
            0.97, 0.5, f"Train loss cuối: {trains[-1]['train_loss']:.4f} (bước {trains[-1]['step']:.0f})",
            transform=ax_full.transAxes, ha="right", va="center", fontsize=9,
            bbox={"facecolor": "white", "edgecolor": "#0072B2", "boxstyle": "round,pad=0.3"},
        )  # fmt: skip
    if summary.get("co_val"):
        epoch = (
            f", epoch {summary['epoch_tot_nhat']:.2f}" if summary.get("epoch_tot_nhat") is not None else ""
        )
        ax_full.axvline(summary["buoc_tot_nhat"], color="#009E73", ls="--", lw=1.3)
        ax_full.text(
            0.97, 0.36, f"Val loss thấp nhất: {summary['val_loss_tot_nhat']:.4f} (bước {summary['buoc_tot_nhat']:.0f}{epoch})",
            transform=ax_full.transAxes, ha="right", va="center", fontsize=9,
            bbox={"facecolor": "white", "edgecolor": "#009E73", "boxstyle": "round,pad=0.3"},
        )  # fmt: skip
    ax_full.set_title("Loss giảm thế nào trong cả quá trình", fontsize=10)

    # Khung phải: phóng to val loss từ lần đo đầu tiên, mỗi chấm có số
    if evals:
        ax_zoom.set_xlim(val_x[0] - 30, val_x[-1] + 40)
        ax_zoom.set_ylim(0, max(val_y) * 1.4)
        prev = None
        for step, value in zip(val_x, val_y, strict=True):
            near = (
                prev is not None and step - prev < 60
            )  # hai lần đo sát nhau thì nhãn nâng cao hơn để khỏi chồng chữ
            ax_zoom.annotate(
                f"{value:.4f}", (step, value), xytext=(0, 32 if near else 17), textcoords="offset points",
                ha="center", fontsize=9, color="#D55E00", fontweight="bold",
                bbox={"facecolor": "white", "alpha": 0.85, "edgecolor": "none", "pad": 0.6},
            )  # fmt: skip
            prev = step
    ax_zoom.set_ylabel("")
    ax_zoom.set_title("Phóng to val loss từ lần đo đầu tiên", fontsize=10)

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
            rowLabels=["Bước huấn luyện", "Epoch", "Train loss (lần ghi gần nhất)", "Val loss", "Val trừ train"],
            loc="center", cellLoc="center",
        )  # fmt: skip
        table.auto_set_font_size(False)
        table.set_fontsize(9.5)
        table.scale(1, 1.55)
        best = min(range(len(evals)), key=lambda i: val_y[i])
        for row in range(len(rows)):
            table[(row, best)].set_facecolor("#E3F4EC")  # tô nhạt lần đo có val loss thấp nhất
    fig.suptitle("Đường cong huấn luyện", fontweight="bold", y=0.97)
    return save(fig, path, NOTES["training"])


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
    for step, gap in pts:  # ghi số chênh ở từng chấm, khỏi gióng xuống trục
        ax.annotate(
            f"{gap:.4f}", (step, gap), xytext=(0, 8), textcoords="offset points", ha="center", fontsize=8.5
        )
    ax.margins(y=0.15)
    ax.axhline(0, color="#555555", lw=0.8)
    ax.set_xlabel("Bước huấn luyện")
    ax.set_ylabel("val loss trừ train loss")
    ax.set_title("Khoảng cách val và train (tăng dần = overfit)")
    return save(fig, path, NOTES["gap"])


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
    return save(fig, path, NOTES["val_metrics"])
