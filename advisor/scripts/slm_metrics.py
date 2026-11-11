"""Chỉ số đo SLM tách ý, tính từ file dự đoán của scripts.eval_slm (data/slm/ket_qua/<model>_<split>.jsonl). Không cần Ollama.

Mỗi dòng của file dự đoán: {"id", "question", "nhan": {"intents": [...]}, "du_doan": {"intents": [...]}, "confidence", "giay"},
và "context" (ngữ cảnh đưa vào), "lui_ve_mo_ho" (model trả sai luật hai lần nên hệ thống lùi về "mơ hồ"). nhan là đáp án đúng,
du_doan là điều model trả; mỗi ý là {"type", "span", "entities"} đúng như hợp đồng (app/schemas/slm.py).

Đơn vị đo là MỘT TIN NHẮN (một dòng). Các mức đúng xếp từ lỏng đến chặt, mức sau gồm mức trước:
  hop_le          model trả được kết quả dùng được (không phải lùi về mơ hồ)
  dung_tap_loai   đủ các loại ý, không xét thứ tự
  dung_thu_tu     đủ loại ý và đúng thứ tự
  dung_nhiem_vu   thêm: mỗi ý đúng nhiệm vụ (tasks, mode, subtype)
  dung_thuc_the   thêm: mỗi ý đúng toàn bộ entities (trường, mục, bộ lọc, hồ sơ...). Chưa xét follow_up
  dung_het        thêm: đúng cả follow_up (đúng hoàn toàn, trừ chữ span vì span được phép viết lại)
Ngoài ra: precision / recall / F1 theo từng loại ý và theo nhãn "loại/nhiệm vụ", ma trận nhầm lẫn, phân loại lỗi,
độ chính xác của follow_up, kết quả theo số ý và theo có hoặc không có ngữ cảnh, thời gian, khoảng tin cậy 95% (bootstrap).
"""

import csv
import difflib
import json
import random
import statistics
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

from app.schemas.slm import INTENTS

NONE = "(không có)"  # nhãn cho ý bị thiếu (thiếu trong dự đoán) hoặc thừa (không có trong nhãn) ở ma trận nhầm lẫn
LEVELS = ("hop_le", "dung_tap_loai", "dung_thu_tu", "dung_nhiem_vu", "dung_thuc_the", "dung_het")
# Thứ tự ưu tiên khi gán MỘT loại lỗi cho một tin
ERRORS = (
    "dung_het",
    "json_loi",
    "thieu_y",
    "thua_y",
    "sai_loai",
    "sai_thu_tu",
    "sai_nhiem_vu",
    "sai_thuc_the",
    "sai_follow_up",
)
# Việc mặc định của loại có tasks khi model không ghi tasks (xem app/schemas/slm.py)
DEFAULT_TASK = {"collect_profile": "answer", "knowledge": "general", "center_referral": "find"}


def load_rows(path: str | Path) -> list[dict[str, Any]]:
    rows = [json.loads(line) for line in Path(path).read_text(encoding="utf-8").splitlines() if line.strip()]
    for r in rows:
        if "intents" not in r.get("nhan", {}) or "intents" not in r.get("du_doan", {}):
            raise ValueError(
                f"{path}: dòng {r.get('id')} không có intents (file dự đoán của bản cũ, hãy chạy lại scripts.eval_slm)"
            )
    return rows


def types_of(intents: list[dict[str, Any]]) -> list[str]:
    return [y["type"] for y in intents]


# Nhiệm vụ của một ý, một chữ để so sánh: mỗi loại có khóa nhiệm vụ riêng
def task_of(y: dict[str, Any]) -> str:
    e = y.get("entities") or {}
    kind = y["type"]
    if kind == "scenario":
        return str(e.get("mode"))
    if kind == "out_of_scope":
        return str(e.get("subtype"))
    tasks = e.get("tasks") or []
    if kind == "profile_review":
        return ",".join(sorted(tasks)) or "unclear"
    return str(tasks[0]) if tasks else DEFAULT_TASK[kind]


def label_of(y: dict[str, Any]) -> str:
    return f"{y['type']}/{task_of(y)}"


# Các danh sách mà thứ tự không có nghĩa (nhiệm vụ, mục, yếu tố, bang, thay đổi): so sánh như tập hợp.
# Danh sách mà thứ tự có nghĩa (schools, centers: theo thứ tự học sinh nói) giữ nguyên
SET_KEYS = ("tasks", "fields", "factor")


def canonical(entities: dict[str, Any]) -> dict[str, Any]:
    out = {k: v for k, v in entities.items() if k != "follow_up"}
    for key in SET_KEYS:
        if isinstance(out.get(key), list):
            out[key] = sorted(out[key])
    if isinstance(out.get("filters"), dict) and isinstance(out["filters"].get("state"), list):
        out["filters"] = {**out["filters"], "state": sorted(out["filters"]["state"])}
    if isinstance(out.get("changes"), list):
        out["changes"] = sorted(
            out["changes"], key=lambda c: json.dumps(c, sort_keys=True, ensure_ascii=False)
        )
    return out


def entities_without_follow_up(y: dict[str, Any]) -> dict[str, Any]:
    return canonical(y.get("entities") or {})


def follow_up_of(y: dict[str, Any]) -> bool:
    return bool((y.get("entities") or {}).get("follow_up"))


def is_fallback(row: dict[str, Any]) -> bool:
    if "lui_ve_mo_ho" in row:
        return bool(row["lui_ve_mo_ho"])
    return row.get("confidence") == 0.0  # file cũ chưa ghi cờ: confidence 0 chính là lùi về mơ hồ


# Ghép ý của nhãn với ý của dự đoán theo thứ tự (khớp đoạn giống nhau trước, đoạn khác nhau ghép lần lượt), ý dư ghép với None
def align(gold: list[str], pred: list[str]) -> list[tuple[int | None, int | None]]:
    pairs: list[tuple[int | None, int | None]] = []
    for op, g0, g1, p0, p1 in difflib.SequenceMatcher(a=gold, b=pred, autojunk=False).get_opcodes():
        if op == "equal":
            pairs += [(g0 + k, p0 + k) for k in range(g1 - g0)]
        else:
            common = min(g1 - g0, p1 - p0)
            pairs += [(g0 + k, p0 + k) for k in range(common)]
            pairs += [(g0 + k, None) for k in range(common, g1 - g0)]
            pairs += [(None, p0 + k) for k in range(common, p1 - p0)]
    return pairs


def judge(row: dict[str, Any]) -> dict[str, Any]:
    """Mức đúng, loại lỗi và một số cờ của MỘT tin."""
    gold, pred = row["nhan"]["intents"], row["du_doan"]["intents"]
    gt, pt = types_of(gold), types_of(pred)
    valid = not is_fallback(row)
    same_set = sorted(gt) == sorted(pt)
    same_order = gt == pt
    same_task = same_order and all(task_of(g) == task_of(p) for g, p in zip(gold, pred, strict=True))
    same_entity = same_task and all(
        entities_without_follow_up(g) == entities_without_follow_up(p)
        for g, p in zip(gold, pred, strict=True)
    )
    same_follow = same_entity and all(
        follow_up_of(g) == follow_up_of(p) for g, p in zip(gold, pred, strict=True)
    )
    levels = {
        "hop_le": valid,
        "dung_tap_loai": valid and same_set,
        "dung_thu_tu": valid and same_order,
        "dung_nhiem_vu": valid and same_task,
        "dung_thuc_the": valid and same_entity,
        "dung_het": valid and same_follow,
    }
    if not valid:
        error = "json_loi"
    elif same_follow:
        error = "dung_het"
    elif same_order:
        error = "sai_nhiem_vu" if not same_task else "sai_thuc_the" if not same_entity else "sai_follow_up"
    elif same_set:
        error = "sai_thu_tu"
    elif len(pt) < len(gt) and not (Counter(pt) - Counter(gt)):
        error = "thieu_y"
    elif len(pt) > len(gt) and not (Counter(gt) - Counter(pt)):
        error = "thua_y"
    else:
        error = "sai_loai"
    return {**levels, "loi": error}


def prf(tp: int, fp: int, fn: int) -> dict[str, float]:
    p = tp / (tp + fp) if tp + fp else 0.0
    r = tp / (tp + fn) if tp + fn else 0.0
    return {
        "precision": p,
        "recall": r,
        "f1": 2 * p * r / (p + r) if p + r else 0.0,
        "ho_tro": tp + fn,
        "tp": tp,
        "fp": fp,
        "fn": fn,
    }


# Precision / recall / F1 theo nhãn, tính trên cả tập ý (một tin có thể có nhiều ý cùng loại nên đếm theo số lượng)
def per_label(rows: list[dict[str, Any]], kind: str = "type") -> dict[str, dict[str, float]]:
    name = (lambda y: y["type"]) if kind == "type" else label_of
    tp, fp, fn = Counter(), Counter(), Counter()
    for r in rows:
        gold, pred = Counter(map(name, r["nhan"]["intents"])), Counter(map(name, r["du_doan"]["intents"]))
        for label in gold | pred:
            hit = min(gold[label], pred[label])
            tp[label] += hit
            fn[label] += gold[label] - hit
            fp[label] += pred[label] - hit
    labels = sorted(set(tp) | set(fp) | set(fn))
    return {label: prf(tp[label], fp[label], fn[label]) for label in labels}


# macro: trung bình F1 của các nhãn có mặt; micro: gộp mọi ý rồi tính một F1
def macro_micro(table: dict[str, dict[str, float]]) -> dict[str, float]:
    macro = statistics.mean(v["f1"] for v in table.values()) if table else 0.0
    micro = prf(
        sum(v["tp"] for v in table.values()),
        sum(v["fp"] for v in table.values()),
        sum(v["fn"] for v in table.values()),
    )["f1"]
    return {"macro_f1": macro, "micro_f1": micro}


def confusion(rows: list[dict[str, Any]]) -> dict[str, Any]:
    labels = [*INTENTS, NONE]
    index = {name: i for i, name in enumerate(labels)}
    matrix = [[0] * len(labels) for _ in labels]  # hàng = nhãn đúng, cột = dự đoán
    for r in rows:
        gt, pt = types_of(r["nhan"]["intents"]), types_of(r["du_doan"]["intents"])
        for g, p in align(gt, pt):
            matrix[index[gt[g] if g is not None else NONE]][index[pt[p] if p is not None else NONE]] += 1
    return {"labels": labels, "matrix": matrix}


def follow_up_scores(rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    """Độ chính xác của follow_up trên các tin có ngữ cảnh, tính trên các ý ghép được theo thứ tự."""
    tp = fp = fn = tn = 0
    for r in rows:
        if not r.get("context"):
            continue
        gold, pred = r["nhan"]["intents"], r["du_doan"]["intents"]
        for g, p in align(types_of(gold), types_of(pred)):
            if g is None or p is None:
                continue
            a, b = follow_up_of(gold[g]), follow_up_of(pred[p])
            tp, fp, fn, tn = tp + (a and b), fp + (not a and b), fn + (a and not b), tn + (not a and not b)
    total = tp + fp + fn + tn
    if not total:
        return None
    return {"so_y": total, "accuracy": (tp + tn) / total, **prf(tp, fp, fn)}


def bootstrap_ci(values: list[float], n: int = 1000, seed: int = 0) -> tuple[float, float]:
    """Khoảng tin cậy 95% của trung bình bằng bootstrap (lấy mẫu lại có hoàn lại). Cố định seed nên chạy lại ra cùng số."""
    if not values:
        return (0.0, 0.0)
    rng = random.Random(seed)
    means = sorted(sum(rng.choices(values, k=len(values))) / len(values) for _ in range(n))
    return (means[int(0.025 * (n - 1))], means[int(0.975 * (n - 1))])


def rate(flags: list[bool]) -> float | None:
    return sum(flags) / len(flags) if flags else None


def percentile(values: list[float], p: float) -> float:
    return sorted(values)[int(p * (len(values) - 1))]


def summarize(rows: list[dict[str, Any]]) -> dict[str, Any]:
    """Toàn bộ chỉ số của MỘT lần đo (một model trên một tập)."""
    judged = [judge(r) for r in rows]
    levels = {k: rate([j[k] for j in judged]) for k in LEVELS}
    ci = {k: bootstrap_ci([float(j[k]) for j in judged]) for k in LEVELS}
    errors = Counter(j["loi"] for j in judged)
    by_type = per_label(rows, "type")
    by_task = per_label(rows, "task")

    def group(key) -> dict[str, dict[str, Any]]:
        out: dict[str, list[dict[str, Any]]] = defaultdict(list)
        for r, j in zip(rows, judged, strict=True):
            out[key(r)].append(j)
        return {
            name: {
                "so_tin": len(js),
                "dung_tap_loai": rate([j["dung_tap_loai"] for j in js]),
                "dung_het": rate([j["dung_het"] for j in js]),
            }
            for name, js in sorted(out.items())
        }

    seconds = [r["giay"] for r in rows if r.get("giay") is not None]
    return {
        "so_tin": len(rows),
        "muc_dung": levels,
        "khoang_tin_cay_95": {k: list(v) for k, v in ci.items()},
        "loi": {name: errors.get(name, 0) for name in ERRORS},
        "theo_loai": by_type,
        "theo_nhan": by_task,
        "f1_loai": macro_micro(by_type),
        "f1_nhan": macro_micro(by_task),
        "nham_lan": confusion(rows),
        "follow_up": follow_up_scores(rows),
        "theo_so_y": group(
            lambda r: str(min(len(r["nhan"]["intents"]), 3)) + ("+" if len(r["nhan"]["intents"]) >= 3 else "")
        ),
        "theo_ngu_canh": group(lambda r: "co_ngu_canh" if r.get("context") else "khong_ngu_canh"),
        "theo_loai_chinh": group(
            lambda r: next(
                (y["type"] for y in r["nhan"]["intents"] if y["type"] != "collect_profile"), "collect_profile"
            )
        ),
        "thoi_gian": {
            "trung_binh": statistics.mean(seconds),
            "p50": percentile(seconds, 0.5),
            "p90": percentile(seconds, 0.9),
            "p95": percentile(seconds, 0.95),
            "mau": seconds,
        }
        if seconds
        else None,
        "tin_cay": {
            "khi_dung": [
                r["confidence"]
                for r, j in zip(rows, judged, strict=True)
                if j["dung_het"] and "confidence" in r
            ],
            "khi_sai": [
                r["confidence"]
                for r, j in zip(rows, judged, strict=True)
                if not j["dung_het"] and "confidence" in r
            ],
        },
        "id_sai": [r["id"] for r, j in zip(rows, judged, strict=True) if not j["dung_het"]],
    }


# ---- bảng CSV (mở bằng Excel) ----
def write_csv(path: Path, header: list[str], rows: list[list[Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8-sig", newline="") as f:  # utf-8-sig: Excel đọc đúng tiếng Việt
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)


def load_training_log(path: str | Path) -> list[dict[str, float]]:
    """Log huấn luyện: file .csv (cột step, epoch, train_loss hoặc loss, eval_loss, tùy chọn val_*) hoặc trainer_state.json của HuggingFace.
    Trả danh sách điểm {step, epoch, train_loss?, eval_loss?, ...} theo thứ tự bước."""
    path = Path(path)
    points: dict[float, dict[str, float]] = {}

    def put(step: float, **values: Any) -> None:
        point = points.setdefault(step, {"step": step})
        point.update({k: float(v) for k, v in values.items() if v not in (None, "")})

    if path.suffix == ".json":
        for entry in json.loads(path.read_text(encoding="utf-8")).get("log_history", []):
            extra = {
                k: v
                for k, v in entry.items()
                if k.startswith("eval_")
                and k not in ("eval_runtime", "eval_samples_per_second", "eval_steps_per_second")
            }
            if "loss" in entry:
                put(entry["step"], epoch=entry.get("epoch"), train_loss=entry["loss"])
            if "eval_loss" in entry:
                put(
                    entry["step"],
                    epoch=entry.get("epoch"),
                    **{("eval_loss" if k == "eval_loss" else "val_" + k[5:]): v for k, v in extra.items()},
                )
    else:
        with open(path, encoding="utf-8-sig", newline="") as f:
            reader = csv.DictReader(f)
            # CSV xuất từ log_history của HuggingFace Trainer: train loss từng lần ghi nằm ở cột "loss";
            # cột "train_loss" chỉ có một giá trị trung bình ở dòng cuối nên không dùng. CSV tự soạn thì dùng thẳng "train_loss".
            from_trainer = "loss" in (reader.fieldnames or [])
            for row in reader:
                step = row.get("step")
                if step in (None, ""):
                    continue
                values = {k: v for k, v in row.items() if k != "step"}
                if from_trainer:
                    values.pop("train_loss", None)
                    values["train_loss"] = values.pop("loss")
                put(float(step), **values)
    return [points[s] for s in sorted(points)]


def training_summary(log: list[dict[str, float]]) -> dict[str, Any]:
    """Epoch hội tụ (val loss thấp nhất), khoảng cách val - train, và dấu hiệu overfit: val loss tăng lại sau điểm thấp nhất."""
    evals = [p for p in log if "eval_loss" in p]
    if not evals:
        return {"co_val": False}
    best = min(evals, key=lambda p: p["eval_loss"])
    last = evals[-1]
    after = [p for p in evals if p["step"] > best["step"]]
    trains = [p for p in log if "train_loss" in p]

    def train_near(step: float) -> float | None:
        before = [p for p in trains if p["step"] <= step]
        return before[-1]["train_loss"] if before else None

    gap_best, gap_last = None, None
    if train_near(best["step"]) is not None:
        gap_best = best["eval_loss"] - train_near(best["step"])
    if train_near(last["step"]) is not None:
        gap_last = last["eval_loss"] - train_near(last["step"])
    return {
        "co_val": True,
        "buoc_tot_nhat": best["step"],
        "epoch_tot_nhat": best.get("epoch"),
        "val_loss_tot_nhat": best["eval_loss"],
        "val_loss_cuoi": last["eval_loss"],
        "chenh_val_train_tai_tot_nhat": gap_best,
        "chenh_val_train_cuoi": gap_last,
        "so_lan_do_val": len(evals),
        # val loss tăng liên tiếp sau điểm tốt nhất hơn 5% thì coi là overfit; chưa giảm hết (điểm tốt nhất là điểm cuối) thì chưa hội tụ
        "overfit": bool(after) and after[-1]["eval_loss"] > best["eval_loss"] * 1.05,
        "chua_hoi_tu": best["step"] == last["step"],
    }
