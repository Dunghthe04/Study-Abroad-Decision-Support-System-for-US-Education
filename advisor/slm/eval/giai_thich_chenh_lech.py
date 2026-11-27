"""Tìm các nhãn mà model B hơn model A và giải thích vì sao, từ chính các câu đã đo.

Chạy (trong thư mục advisor, đã kích hoạt .venv):
  python -m slm.eval.giai_thich_chenh_lech --a "Qwen=slm/results/ket_qua/qwen3_4b-instruct_shots8_test.jsonl" \
      --b "Gemma=slm/results/ket_qua/gemma3_4b_shots8_test.jsonl" --out slm/results/bao_cao/giai_thich_qwen_gemma.md

Ba bước cho mỗi nhãn B hơn A (theo F1):
  1. Chênh có thật không: kiểm định McNemar trên các tin có nhãn đó (p < 0,05 mới coi là khác biệt thật).
  2. A mất điểm ở đâu: thiếu nhãn (recall) hay gán nhãn này cho tin khác (precision).
  3. Ở các tin B đúng mà A sai: A trả gì thay thế, vì lý do gì (sai khuôn JSON hay hiểu sai ý), kèm câu ví dụ.
Chỉ đọc file kết quả, không gọi model.
"""

import argparse
from collections import Counter
from pathlib import Path

from slm.eval import slm_metrics as M

MIN_GAP = 0.01  # F1 của B hơn A ít nhất chừng này mới liệt kê
MIN_SUPPORT = 20  # nhãn quá ít tin thì không đủ để kết luận


def named(text: str) -> tuple[str, str]:
    name, _, path = text.partition("=")
    return name, path


def labels(intents: list[dict]) -> set[str]:
    return set(M.names_of(intents, "task"))


def gold_of(row: dict) -> set[str]:
    return labels(row["nhan"]["intents"])


def pred_of(row: dict) -> set[str]:
    # Tin lùi về mơ hồ (model trả sai luật) không được coi là đoán đúng nhãn nào
    return set() if M.is_fallback(row) else labels(M.predicted_intents(row))


def how_it_failed(row: dict, label: str) -> str:
    if M.is_fallback(row):
        return f"lùi về mơ hồ ({row.get('loi_ly_do') or 'không rõ lý do'})"
    other = sorted(pred_of(row) - gold_of(row))
    return "hiểu thành " + ", ".join(other) if other else "bỏ sót ý này (không đoán thêm nhãn nào khác)"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--a", required=True, type=named, help='"Tên=file.jsonl" của model được chọn')
    parser.add_argument("--b", required=True, type=named, help='"Tên=file.jsonl" của model so sánh')
    parser.add_argument("--out", required=True)
    parser.add_argument("--examples", type=int, default=5)
    args = parser.parse_args()

    (name_a, path_a), (name_b, path_b) = args.a, args.b
    rows_a = {r["id"]: r for r in M.load_rows(path_a)}
    rows_b = {r["id"]: r for r in M.load_rows(path_b)}
    if set(rows_a) != set(rows_b):
        raise SystemExit("Hai file không cùng danh sách câu, không so được.")
    ids = sorted(rows_a)

    def f1_table(rows: dict) -> dict[str, dict[str, float]]:
        out = {}
        for label in sorted({x for i in ids for x in gold_of(rows[i])}):
            tp = sum(label in gold_of(rows[i]) and label in pred_of(rows[i]) for i in ids)
            fn = sum(label in gold_of(rows[i]) and label not in pred_of(rows[i]) for i in ids)
            fp = sum(label not in gold_of(rows[i]) and label in pred_of(rows[i]) for i in ids)
            out[label] = M.prf(tp, fp, fn) | {"n": tp + fn, "fp": fp}
        return out

    ta, tb = f1_table(rows_a), f1_table(rows_b)
    ahead = sum(ta[k]["f1"] > tb[k]["f1"] + MIN_GAP for k in ta)
    behind = [k for k in ta if tb[k]["f1"] > ta[k]["f1"] + MIN_GAP and ta[k]["n"] >= MIN_SUPPORT]
    behind.sort(key=lambda k: tb[k]["f1"] - ta[k]["f1"], reverse=True)

    lines = [
        f"# Các nhãn {name_b} hơn {name_a} và vì sao",
        "",
        f"Tập đo: {len(ids)} tin, {len(ta)} nhãn (loại/nhiệm vụ). {name_a} hơn ở {ahead} nhãn; "
        f"{name_b} hơn từ {MIN_GAP:.2f} F1 trở lên ở {len(behind)} nhãn (chỉ tính nhãn có từ {MIN_SUPPORT} tin).",
        "",
        f"| Nhãn | Số tin | F1 {name_a} | F1 {name_b} | Chỉ {name_a} đúng | Chỉ {name_b} đúng | p (McNemar) | Chênh thật? |",
        "|---|---|---|---|---|---|---|---|",
    ]
    detail = []
    for label in behind:
        having = [i for i in ids if label in gold_of(rows_a[i])]
        ok_a = [label in pred_of(rows_a[i]) for i in having]
        ok_b = [label in pred_of(rows_b[i]) for i in having]
        test = M.mcnemar_exact(ok_a, ok_b)
        if test["p"] >= 0.05:
            verdict = "Không (trong khoảng ngẫu nhiên)"
        elif test["only_b"] > test["only_a"]:
            verdict = f"Có, {name_b} tìm được nhiều hơn"
        else:
            verdict = f"Recall {name_a} vẫn hơn; {name_b} hơn F1 nhờ precision"
        lines.append(
            f"| {label} | {len(having)} | {ta[label]['f1']:.2f} | {tb[label]['f1']:.2f} | {test['only_a']} | "
            f"{test['only_b']} | {test['p']:.3g} | {verdict} |"
        )

        lost = [i for i, a, b in zip(having, ok_a, ok_b, strict=True) if b and not a]
        reasons = Counter(how_it_failed(rows_a[i], label) for i in lost)
        detail += [
            "",
            f"## {label}",
            "",
            f"| | Recall (tìm được bao nhiêu) | Precision (đoán nhãn này thì đúng bao nhiêu) | Gán nhầm nhãn này |",
            "|---|---|---|---|",
            f"| {name_a} | {ta[label]['recall']:.2f} | {ta[label]['precision']:.2f} | {ta[label]['fp']} tin |",
            f"| {name_b} | {tb[label]['recall']:.2f} | {tb[label]['precision']:.2f} | {tb[label]['fp']} tin |",
            "",
            f"**{len(lost)} tin {name_b} đúng mà {name_a} sai.** {name_a} sai kiểu gì:",
        ]
        detail += [f"- {count} tin: {reason}" for reason, count in reasons.most_common(5)]
        if lost:
            detail += ["", "Câu ví dụ (lấy theo kiểu sai nhiều nhất):", ""]
            top = reasons.most_common(1)[0][0]
            for i in [i for i in lost if how_it_failed(rows_a[i], label) == top][: args.examples]:
                detail.append(f"- “{rows_a[i]['question']}”")

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines + detail) + "\n", encoding="utf-8")
    print("\n".join(lines + detail))
    print(f"\nĐã ghi: {out}")


if __name__ == "__main__":
    main()
