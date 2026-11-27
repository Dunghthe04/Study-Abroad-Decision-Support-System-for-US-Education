"""Chỉ số đo SLM tách ý, tính từ file dự đoán của slm.eval.eval_slm (slm/results/ket_qua/<model>_<split>.jsonl). Không cần Ollama.

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
import math
import random
import statistics
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

import numpy as np

from app.schemas.slm import INTENTS

NONE = (
    "(none)"  # nhãn cho ý bị thiếu (thiếu trong dự đoán) hoặc thừa (không có trong nhãn) ở ma trận nhầm lẫn
)
INVALID = (
    "(invalid)"  # cột của ma trận nhầm lẫn: model không trả được kết quả dùng được nên không có dự đoán nào
)
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
                f"{path}: dòng {r.get('id')} không có intents (file dự đoán của bản cũ, hãy chạy lại slm.eval.eval_slm)"
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


def fold_text(value: Any) -> Any:
    """Chữ thường và gộp khoảng trắng cho mọi chuỗi (kể cả trong danh sách, từ điển). Tên trường, thành phố, trung tâm viết hoa khác nhau
    vẫn là một đáp án vì phía sau (SchoolNameMatcher, danh mục bang và ngành) cũng so không phân biệt hoa thường."""
    if isinstance(value, str):
        return " ".join(unicodedata.normalize("NFC", value).lower().split())
    if isinstance(value, list):
        return [fold_text(v) for v in value]
    if isinstance(value, dict):
        return {k: fold_text(v) for k, v in value.items()}
    return value


def canonical(entities: dict[str, Any], fold: bool = True) -> dict[str, Any]:
    out = {k: v for k, v in entities.items() if k != "follow_up"}
    if fold:
        out = fold_text(out)
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


def entities_without_follow_up(y: dict[str, Any], fold: bool = True) -> dict[str, Any]:
    return canonical(y.get("entities") or {}, fold)


def follow_up_of(y: dict[str, Any]) -> bool:
    return bool((y.get("entities") or {}).get("follow_up"))


def is_fallback(row: dict[str, Any]) -> bool:
    if "lui_ve_mo_ho" in row:
        return bool(row["lui_ve_mo_ho"])
    return row.get("confidence") == 0.0  # file cũ chưa ghi cờ: confidence 0 chính là lùi về mơ hồ


def predicted_intents(row: dict[str, Any]) -> list[dict[str, Any]]:
    """Các ý do MODEL dự đoán. Tin lùi về mơ hồ là hệ thống tự điền out_of_scope/ambiguous (model không trả được kết quả dùng được),
    không phải một dự đoán của model: không tính vào precision/recall/F1 và ma trận nhầm lẫn (ý đúng của tin đó tính là bị bỏ sót)."""
    return [] if is_fallback(row) else row["du_doan"]["intents"]


def common_valid(runs: dict[str, list[dict[str, Any]]]) -> dict[str, list[dict[str, Any]]]:
    """Các tin mà MỌI model đều trả được kết quả dùng được, cùng thứ tự id cho mọi model. Mỗi model chỉ tính trên tin hợp lệ của
    riêng nó thì tập tin khác nhau (model hay hỏng có thể chỉ còn các câu dễ), nên không so thẳng được; tập giao mới so công bằng."""
    by_id = {name: {r["id"]: r for r in rows} for name, rows in runs.items()}
    ids = sorted(set.intersection(*(set(d) for d in by_id.values())))
    keep = [i for i in ids if all(not is_fallback(by_id[name][i]) for name in runs)]
    return {name: [by_id[name][i] for i in keep] for name in runs}


def valid_rows(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Chỉ các tin model trả được kết quả dùng được. Dùng để tách 'hiểu sai ý' khỏi 'không tuân thủ định dạng'."""
    return [r for r in rows if not is_fallback(r)]


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
    # Bản chặt hơn: phân biệt hoa thường ở chuỗi thực thể (để cho thấy hoa thường làm đổi bao nhiêu)
    same_follow_strict = (
        same_task
        and all(
            entities_without_follow_up(g, fold=False) == entities_without_follow_up(p, fold=False)
            for g, p in zip(gold, pred, strict=True)
        )
        and all(follow_up_of(g) == follow_up_of(p) for g, p in zip(gold, pred, strict=True))
    )
    levels = {
        "hop_le": valid,
        "dung_tap_loai": valid and same_set,
        "dung_thu_tu": valid and same_order,
        "dung_nhiem_vu": valid and same_task,
        "dung_thuc_the": valid and same_entity,
        "dung_het": valid and same_follow,
        "dung_het_chat": valid and same_follow_strict,
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


# Một ý có thể mang nhiều nhiệm vụ (profile_review/assessment,school_list): tách thành từng nhãn riêng để không sinh nhãn ghép hiếm
def labels_of(y: dict[str, Any]) -> list[str]:
    if y["type"] == "profile_review":
        tasks = sorted((y.get("entities") or {}).get("tasks") or []) or ["unclear"]
        return [f"profile_review/{t}" for t in tasks]
    return [label_of(y)]


def names_of(intents: list[dict[str, Any]], kind: str) -> list[str]:
    return [y["type"] for y in intents] if kind == "type" else [x for y in intents for x in labels_of(y)]


# Precision / recall / F1 theo nhãn, tính trên cả tập ý (một tin có thể có nhiều ý cùng loại nên đếm theo số lượng).
# Tin lùi về mơ hồ không có dự đoán (predicted_intents): mọi ý đúng của nó là bị bỏ sót, không có ý thừa
def per_label(rows: list[dict[str, Any]], kind: str = "type") -> dict[str, dict[str, float]]:
    tp, fp, fn = Counter(), Counter(), Counter()
    for r in rows:
        gold = Counter(names_of(r["nhan"]["intents"], kind))
        pred = Counter(names_of(predicted_intents(r), kind))
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
    """Ma trận nhầm lẫn theo loại ý, nhất quán với precision/recall: đường chéo = số ý đúng loại (cùng cách đếm với per_label).
    Hàng = nhãn đúng (thêm hàng NONE cho ý thừa), cột = dự đoán (thêm cột NONE cho ý bị bỏ sót và cột INVALID cho tin model
    không trả được kết quả dùng được). Các ý còn lại sau khi trừ ý đúng loại được ghép lần lượt theo thứ tự."""
    row_labels = [*INTENTS, NONE]
    col_labels = [*INTENTS, NONE, INVALID]
    r_index = {name: i for i, name in enumerate(row_labels)}
    c_index = {name: i for i, name in enumerate(col_labels)}
    matrix = [[0] * len(col_labels) for _ in row_labels]
    for r in rows:
        gt = types_of(r["nhan"]["intents"])
        if is_fallback(r):
            for g in gt:
                matrix[r_index[g]][c_index[INVALID]] += 1
            continue
        pt = types_of(r["du_doan"]["intents"])
        hit = Counter(gt) & Counter(pt)
        for kind, count in hit.items():
            matrix[r_index[kind]][c_index[kind]] += count
        left_g, left_p = list((Counter(gt) - hit).elements()), list((Counter(pt) - hit).elements())
        left_g.sort(key=gt.index)  # giữ thứ tự xuất hiện trong tin
        left_p.sort(key=pt.index)
        for k in range(max(len(left_g), len(left_p))):
            g = left_g[k] if k < len(left_g) else NONE
            p = left_p[k] if k < len(left_p) else NONE
            matrix[r_index[g]][c_index[p]] += 1
    return {"labels": row_labels, "col_labels": col_labels, "matrix": matrix}


def follow_up_scores(rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    """Độ chính xác của follow_up trên các tin có ngữ cảnh, tính trên các ý ghép được theo thứ tự."""
    tp = fp = fn = tn = 0
    for r in rows:
        if not r.get("context") or is_fallback(r):
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


def wilson(k: int, n: int, z: float = 1.96) -> tuple[float, float]:
    """Khoảng tin cậy 95% của một tỉ lệ (Wilson). Dùng cho nhóm nhỏ, nơi bootstrap và công thức thường kém chính xác."""
    if n == 0:
        return (0.0, 0.0)
    p = k / n
    d = 1 + z * z / n
    c = p + z * z / (2 * n)
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return (max(0.0, (c - h) / d), min(1.0, (c + h) / d))


def mcnemar_exact(a_ok: list[bool], b_ok: list[bool]) -> dict[str, Any]:
    """Kiểm định McNemar chính xác (hai phía) cho hai model chấm trên CÙNG các tin. Chỉ các tin hai model chấm khác nhau mới có thông tin:
    only_a = A đúng B sai, only_b = A sai B đúng. Giả thuyết không: hai model đúng như nhau, tức only_a và only_b ngang nhau."""
    only_a = sum(x and not y for x, y in zip(a_ok, b_ok, strict=True))
    only_b = sum(y and not x for x, y in zip(a_ok, b_ok, strict=True))
    n = only_a + only_b
    if n == 0:
        return {"only_a": 0, "only_b": 0, "p": 1.0}
    k = min(only_a, only_b)
    tail = sum(math.comb(n, i) for i in range(k + 1)) / 2**n
    return {"only_a": only_a, "only_b": only_b, "p": min(1.0, 2 * tail)}


def count_array(rows: list[dict[str, Any]], labels: list[str], kind: str = "type") -> Any:
    """Mảng (số tin, số nhãn, 3) chứa tp, fp, fn của TỪNG tin theo từng nhãn (cùng cách đếm với per_label), để lấy mẫu lại theo tin."""
    index = {name: i for i, name in enumerate(labels)}
    arr = np.zeros((len(rows), len(labels), 3), dtype=np.int64)
    for k, r in enumerate(rows):
        gold = Counter(names_of(r["nhan"]["intents"], kind))
        pred = Counter(names_of(predicted_intents(r), kind))
        for label in gold | pred:
            hit = min(gold[label], pred[label])
            arr[k, index[label]] = (hit, pred[label] - hit, gold[label] - hit)
    return arr


def f1_scores(sums: Any) -> tuple[float, float]:
    """(macro-F1, micro-F1) từ tổng tp, fp, fn theo nhãn (mảng (số nhãn, 3)). Macro: trung bình F1 các nhãn; micro: gộp mọi ý."""
    tp, fp, fn = sums[:, 0].astype(float), sums[:, 1].astype(float), sums[:, 2].astype(float)

    def f1(a: Any, b: Any, c: Any) -> Any:
        p = np.divide(a, a + b, out=np.zeros_like(a), where=(a + b) > 0)
        r = np.divide(a, a + c, out=np.zeros_like(a), where=(a + c) > 0)
        return np.divide(2 * p * r, p + r, out=np.zeros_like(p), where=(p + r) > 0)

    macro = float(f1(tp, fp, fn).mean()) if len(tp) else 0.0
    micro = float(f1(np.array([tp.sum()]), np.array([fp.sum()]), np.array([fn.sum()]))[0])
    return macro, micro


def bootstrap_weights(n_rows: int, n: int = 1000, seed: int = 0) -> Any:
    """Mỗi hàng là số lần mỗi tin được chọn trong một lần lấy mẫu lại có hoàn lại. Hai model dùng chung để so cặp."""
    rng = np.random.default_rng(seed)
    return np.stack([np.bincount(rng.integers(0, n_rows, n_rows), minlength=n_rows) for _ in range(n)])


def percentile_ci(values: Any) -> tuple[float, float]:
    lo, hi = np.percentile(values, [2.5, 97.5])
    return (float(lo), float(hi))


def f1_ci(
    rows: list[dict[str, Any]], kind: str = "type", n: int = 1000, seed: int = 0
) -> dict[str, tuple[float, float]]:
    """Khoảng tin cậy 95% của macro-F1 và micro-F1, lấy mẫu lại THEO TIN (một tin có nhiều ý thì các ý đi cùng nhau)."""
    labels = sorted(per_label(rows, kind))
    arr = count_array(rows, labels, kind)
    macro, micro = [], []
    for w in bootstrap_weights(len(rows), n, seed):
        ma, mi = f1_scores(np.tensordot(w, arr, axes=(0, 0)))
        macro.append(ma)
        micro.append(mi)
    return {"macro_f1": percentile_ci(macro), "micro_f1": percentile_ci(micro)}


def paired_compare(
    rows_a: list[dict[str, Any]], rows_b: list[dict[str, Any]], n: int = 1000, seed: int = 0
) -> dict[str, Any]:
    """So hai model trên CÙNG tập tin: hiệu của A trừ B kèm khoảng tin cậy 95% (bootstrap theo cặp) và p của McNemar.
    Khoảng tin cậy không chứa 0 thì khác biệt có ý nghĩa thống kê."""
    by_a, by_b = {r["id"]: r for r in rows_a}, {r["id"]: r for r in rows_b}
    if set(by_a) != set(by_b):
        raise ValueError("Hai lần chạy không đo cùng một tập tin nhắn (khác id), không so cặp được")
    ids = sorted(by_a)
    ra, rb = [by_a[i] for i in ids], [by_b[i] for i in ids]
    ja, jb = [judge(r) for r in ra], [judge(r) for r in rb]
    weights = bootstrap_weights(len(ids), n, seed)
    out: dict[str, Any] = {"so_tin": len(ids)}
    for key, name in (("hop_le", "valid"), ("dung_tap_loai", "category"), ("dung_het", "exact")):
        a_ok, b_ok = [j[key] for j in ja], [j[key] for j in jb]
        diff = np.array(a_ok, dtype=float) - np.array(b_ok, dtype=float)
        boot = weights @ diff / len(ids)
        out[name] = {
            "a": sum(a_ok) / len(ids),
            "b": sum(b_ok) / len(ids),
            "hieu": float(diff.mean()),
            "khoang_tin_cay_95": percentile_ci(boot),
            **mcnemar_exact(a_ok, b_ok),
        }
    labels = sorted(set(per_label(ra)) | set(per_label(rb)))
    arr_a, arr_b = count_array(ra, labels), count_array(rb, labels)
    ones = np.ones(len(ids))
    macro_a = f1_scores(np.tensordot(ones, arr_a, axes=(0, 0)))[0]
    macro_b = f1_scores(np.tensordot(ones, arr_b, axes=(0, 0)))[0]
    boot = [
        f1_scores(np.tensordot(w, arr_a, axes=(0, 0)))[0] - f1_scores(np.tensordot(w, arr_b, axes=(0, 0)))[0]
        for w in weights
    ]
    out["macro_f1"] = {
        "a": macro_a,
        "b": macro_b,
        "hieu": macro_a - macro_b,
        "khoang_tin_cay_95": percentile_ci(boot),
    }
    return out


def calibration(rows: list[dict[str, Any]], bins: int = 10) -> dict[str, Any] | None:
    """Độ hiệu chuẩn của confidence trên các tin hợp lệ: trong mỗi khoảng, confidence trung bình so với tỉ lệ đúng LOẠI Ý thực tế.
    Confidence của hệ thống là xác suất token của chữ "type" (ý thấp nhất trong tin), nên chỉ đo độ chắc về loại ý, không đo thực thể:
    phải so với 'đúng loại ý' (dung_tap_loai) chứ không phải 'đúng hoàn toàn'. ECE = trung bình có trọng số của chênh lệch đó
    (0 là hiệu chuẩn hoàn hảo). Tin lùi về mơ hồ bị loại vì confidence 0 của chúng là giá trị mặc định của hệ thống."""
    pts = [
        (r["confidence"], judge(r)["dung_tap_loai"]) for r in rows if not is_fallback(r) and "confidence" in r
    ]
    if not pts:
        return None
    out, ece = [], 0.0
    for b in range(bins):
        lo, hi = b / bins, (b + 1) / bins
        sel = [(c, ok) for c, ok in pts if lo <= c < hi or (b == bins - 1 and c == 1.0)]
        if not sel:
            continue
        conf, acc = statistics.mean(c for c, _ in sel), statistics.mean(float(ok) for _, ok in sel)
        out.append({"lo": lo, "hi": hi, "so_tin": len(sel), "confidence": conf, "accuracy": acc})
        ece += len(sel) / len(pts) * abs(acc - conf)
    return {"so_tin": len(pts), "ece": ece, "khoang": out}


def roc(rows: list[dict[str, Any]], valid_only: bool = False) -> dict[str, Any] | None:
    """Đường ROC của confidence khi dùng nó để phân biệt tin đoán ĐÚNG loại ý (dương) với tin đoán SAI (âm), và diện tích AUC.
    AUC = xác suất một tin đúng lấy ngẫu nhiên có confidence cao hơn một tin sai lấy ngẫu nhiên (0,5 = không phân biệt được).
    Các tin cùng confidence được gộp thành một điểm nên kết quả không phụ thuộc thứ tự dòng.
    valid_only=True bỏ tin lùi về mơ hồ: confidence 0 của chúng do hệ thống gán, tính vào thì AUC cao lên một cách dễ dãi.
    Trả None khi không có đủ cả tin đúng lẫn tin sai."""
    pts = [
        (r["confidence"], bool(judge(r)["dung_tap_loai"]))
        for r in rows
        if "confidence" in r and not (valid_only and is_fallback(r))
    ]
    positive = sum(ok for _, ok in pts)
    negative = len(pts) - positive
    if not positive or not negative:
        return None
    groups: dict[float, list[int]] = {}
    for confidence, ok in pts:
        groups.setdefault(confidence, [0, 0])[0 if ok else 1] += 1
    fpr, tpr, tp, fp, area = [0.0], [0.0], 0, 0, 0.0
    for confidence in sorted(groups, reverse=True):
        tp += groups[confidence][0]
        fp += groups[confidence][1]
        x, y = fp / negative, tp / positive
        area += (x - fpr[-1]) * (y + tpr[-1]) / 2
        fpr.append(x)
        tpr.append(y)
    return {"auc": area, "fpr": fpr, "tpr": tpr, "so_tin_dung": positive, "so_tin_sai": negative}


def rate(flags: list[bool]) -> float | None:
    return sum(flags) / len(flags) if flags else None


def percentile(values: list[float], p: float) -> float:
    return sorted(values)[int(p * (len(values) - 1))]


def summarize(rows: list[dict[str, Any]]) -> dict[str, Any]:
    """Toàn bộ chỉ số của MỘT lần đo (một model trên một tập)."""
    judged = [judge(r) for r in rows]
    levels = {k: rate([j[k] for j in judged]) for k in (*LEVELS, "dung_het_chat")}
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
                "khoang_tin_cay_dung_het": list(wilson(sum(j["dung_het"] for j in js), len(js))),
            }
            for name, js in sorted(out.items())
        }

    seconds = [r["giay"] for r in rows if r.get("giay") is not None]
    # Thời gian theo số ý trong tin (1, 2, 3+): tin nhiều ý thì JSON trả về dài hơn nên lâu hơn
    times_by_count: dict[str, list[float]] = {}
    for r in rows:
        if r.get("giay") is not None:
            n_intents = len(r["nhan"]["intents"])
            times_by_count.setdefault(str(min(n_intents, 3)) + ("+" if n_intents >= 3 else ""), []).append(
                r["giay"]
            )
    reasons = Counter(r.get("loi_ly_do") or "unknown" for r in rows if is_fallback(r))
    return {
        "so_tin": len(rows),
        "so_tin_hop_le": sum(j["hop_le"] for j in judged),
        "ly_do_loi": dict(reasons) if any("loi_ly_do" in r for r in rows if is_fallback(r)) else None,
        "khoang_tin_cay_f1": {k: list(v) for k, v in f1_ci(rows).items()} if rows else None,
        "hieu_chuan": calibration(rows),
        "roc": {"tat_ca": roc(rows), "hop_le": roc(rows, valid_only=True)},
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
        "thoi_gian": {
            "trung_binh": statistics.mean(seconds),
            "p50": percentile(seconds, 0.5),
            "p90": percentile(seconds, 0.9),
            "p95": percentile(seconds, 0.95),
            "max": max(seconds),
            "mau": seconds,
            "theo_so_y": {
                k: {"so_tin": len(v), "trung_binh": statistics.mean(v)}
                for k, v in sorted(times_by_count.items())
            },
        }
        if seconds
        else None,
        # Chỉ tin hợp lệ (confidence 0 của tin lùi về mơ hồ là giá trị mặc định của hệ thống) và theo 'đúng loại ý'
        # (confidence là xác suất của chữ type nên chỉ đo độ chắc về loại ý)
        "tin_cay": {
            "khi_dung": [
                r["confidence"]
                for r, j in zip(rows, judged, strict=True)
                if j["dung_tap_loai"] and "confidence" in r
            ],
            "khi_sai": [
                r["confidence"]
                for r, j in zip(rows, judged, strict=True)
                if j["hop_le"] and not j["dung_tap_loai"] and "confidence" in r
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
