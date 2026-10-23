"""Gộp các lần đo trong runs/ thành một bảng để báo cáo.

Chạy:  python -m evals.recommendation.summarize_runs
Mỗi dòng là một lần chạy: số cặp (học sinh x trường), số lần gọi AI, độ chính xác, token, thời gian.
"""

import json
import sys
from pathlib import Path

RUNS = Path(__file__).parent / "runs"


def main():
    sys.stdout.reconfigure(encoding="utf-8")  # PowerShell cũ in tiếng Việt bị lỗi mã hóa nếu thiếu dòng này
    rows = []
    for path in sorted(RUNS.glob("*.json")):
        d = json.loads(path.read_text(encoding="utf-8"))
        s, t = d["summary"], d.get("tokens") or {}
        lat = d.get("latency_seconds") or {}
        rows.append(
            [
                path.name[:15],
                d.get("model", ""),
                d.get("skill_version", ""),
                s["pairs"],
                t.get("calls", ""),
                f"{s['g2_correct_pct']}%",
                f"{s['all_steps_correct_pct']}%",
                s["mean_s_error"],
                t.get("input", ""),
                t.get("output", ""),
                d.get("seconds", ""),
                lat.get("median", ""),
            ]
        )
    head = [
        "lần chạy",
        "model",
        "skill",
        "cặp",
        "gọi",
        "đúng nhóm",
        "đúng mọi bước",
        "sai số S",
        "token vào",
        "token ra",
        "tổng giây",
        "giây/lần gọi",
    ]
    widths = [max(len(str(r[i])) for r in [head] + rows) for i in range(len(head))]
    for r in [head] + rows:
        print(" | ".join(str(v).ljust(w) for v, w in zip(r, widths, strict=True)))


if __name__ == "__main__":
    main()
