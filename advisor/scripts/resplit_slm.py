"""Chia lại train / val / test của file Excel dữ liệu SLM theo NHÓM, rồi sắp các dòng của mỗi sheet theo thứ tự train, val, test.

Vì sao chia theo nhóm: nếu cùng một hồ sơ (hoặc cùng một câu hỏi) nằm ở cả train và test thì model đã "thấy" câu đó, điểm đo bị cao ảo.
Cách làm:
  1. Gom nhóm xuyên các sheet: các dòng cùng hồ sơ thuộc một nhóm; dòng không hồ sơ và không ngữ cảnh thì cùng khuôn câu hỏi
     (đã che tên trường, thành phố, số) là một nhóm; dòng có hồ sơ hoặc ngữ cảnh chỉ nối theo cùng cả câu, hồ sơ và ngữ cảnh.
  2. Cả nhóm vào đúng một tập. Đi từ nhãn con hiếm nhất, ép đủ số dòng test và val cho nhãn đó (có trần, nhãn quá ít dòng thì không thể đủ,
     phải viết thêm dòng); phần còn lại chia cho đủ tỉ lệ 70 / 15 / 15 theo số dòng.
  3. Thứ tự chọn nhóm theo mã băm cố định (--seed) nên chạy lại ra đúng kết quả cũ.
Mặc định chỉ in bảng trước và sau (chạy thử). Thêm --apply thì mới ghi lại cột split và sắp lại thứ tự dòng trong file Excel.
Ghi file thì phải đóng Excel trước. Sau khi ghi: chạy build_slm_jsonl rồi check_slm_data.

Chạy trong thư mục advisor:
  python -m scripts.resplit_slm
  python -m scripts.resplit_slm --apply
"""

import argparse
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

from openpyxl import load_workbook

from scripts.build_slm_jsonl import DEFAULT_EXCEL, SPLITS
from scripts.check_slm_data import (
    MIN_PROFILE_KEY,
    MIN_TEST,
    MULTI_MIN_INTENTS,
    Item,
    check_label_has_enough_test,
    check_profile_not_in_two_splits,
    check_test_questions_not_in_train,
    label_counts,
    labels_of,
    norm,
    profile_key,
    read_items,
)

RATIOS = {"train": 0.70, "val": 0.15, "test": 0.15}
SEED = "usas-slm-v9"
# Khóa thực thể là tên trường, thành phố, trung tâm: che đi để các câu chỉ khác tên được coi là cùng khuôn
MASKED_KEYS = ("school", "schools", "city", "centers", "service")


def skeleton(item: Item) -> str:
    """Khuôn câu hỏi: chữ thường, che tên trường / thành phố / trung tâm theo thực thể của ý, che chữ số."""
    text = norm(item.record["input"]["question"])
    names: list[str] = []
    for y in item.record["output"]["intents"]:
        for key in MASKED_KEYS:
            value = y["entities"].get(key)
            names.extend(value if isinstance(value, list) else [value] if value else [])
    for name in sorted({norm(n) for n in names}, key=len, reverse=True):
        text = text.replace(name, "<x>")
    return "".join("0" if c.isdigit() else c for c in text)


def group_items(items: list[Item]) -> list[list[int]]:
    """Các dòng cùng hồ sơ hoặc cùng khuôn câu hỏi vào một nhóm (nối bắc cầu). Trả về danh sách chỉ số dòng của từng nhóm."""
    parent = list(range(len(items)))

    def find(a: int) -> int:
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    seen: dict[tuple, int] = {}
    for i, item in enumerate(items):
        profile = profile_key(item.record)
        context = json.dumps(item.record["input"].get("dang_noi_ve"), sort_keys=True, ensure_ascii=False)
        keys: list[tuple] = []
        if profile:
            keys.append(("hồ sơ", profile))
        if not profile and context == "null":
            keys.append(("câu", skeleton(item)))  # câu thuần chữ: cùng khuôn là cùng nhóm
        else:
            # Câu có hồ sơ hoặc ngữ cảnh là đầu vào khác nhau dù chữ giống (vd "so sánh hai trường đầu"): chỉ nối khi cùng cả hồ sơ, ngữ cảnh
            keys.append(("câu + đầu vào", skeleton(item), profile, context))
        for key in keys:
            if key in seen:
                parent[find(i)] = find(seen[key])
            else:
                seen[key] = i
    groups: dict[int, list[int]] = defaultdict(list)
    for i in range(len(items)):
        groups[find(i)].append(i)
    return list(groups.values())


def stable_order(key: str, seed: str) -> str:
    return hashlib.sha1(f"{seed}|{key}".encode()).hexdigest()


def assign_splits(items: list[Item], min_test: int = MIN_TEST, seed: str = SEED) -> list[str]:
    groups = group_items(items)
    row_labels = [
        Counter(label for y in it.record["output"]["intents"] for label in labels_of(y)) for it in items
    ]
    group_labels = [sum((row_labels[i] for i in g), Counter()) for g in groups]
    group_key = [
        min(norm(items[i].record["input"]["question"]) + str(items[i].row) + items[i].sheet for i in g)
        for g in groups
    ]
    total_label = Counter()
    for c in group_labels:
        total_label.update(c)

    assigned: list[str | None] = [None] * len(groups)
    got: dict[str, Counter] = {s: Counter() for s in SPLITS}  # số ý đã có của từng nhãn ở từng tập
    rows_in: Counter = Counter()

    def put(g: int, split: str) -> None:
        assigned[g] = split
        got[split].update(group_labels[g])
        rows_in[split] += len(groups[g])

    # Nhóm quá lớn (nối nhiều câu chung) cho vào train, để không chiếm hết test và val
    biggest = max(range(len(groups)), key=lambda g: len(groups[g]))
    if len(groups[biggest]) > 0.1 * len(items):
        put(biggest, "train")

    order = sorted(range(len(groups)), key=lambda g: stable_order(group_key[g], seed))
    for label in sorted(total_label, key=lambda lab: (total_label[lab], lab)):
        n = total_label[label]
        # Nhãn quá ít dòng thì trần theo tỉ lệ để train còn đủ để học; muốn đủ test phải viết thêm dòng
        want_test = min(max(min_test, math.ceil(RATIOS["test"] * n)), math.ceil(0.3 * n))
        want_val = min(max(min_test, math.ceil(RATIOS["val"] * n)), math.ceil(0.3 * n))
        for split, want in (("test", want_test), ("val", want_val)):
            for g in order:
                if got[split][label] >= want:
                    break
                if assigned[g] is None and group_labels[g][label]:
                    put(g, split)

    target = {s: RATIOS[s] * len(items) for s in SPLITS}
    for g in order:
        if assigned[g] is None:
            size = len(groups[g])
            best = max(SPLITS, key=lambda s: (target[s] - rows_in[s]) / target[s])
            if rows_in[best] + size > target[best] * 1.15 and best != "train":
                best = "train"
            put(g, best)

    result = [""] * len(items)
    for g, split in enumerate(assigned):
        for i in groups[g]:
            result[i] = split or "train"
    return result


def group_keys(items: list[Item], group: list[int]) -> Counter:
    """Những thứ một nhóm đóng góp cho tập chứa nó: nhãn con, mục thông tin trường, và dòng có câu hệ thống đang hỏi theo loại."""
    keys: Counter = Counter()
    for i in group:
        record = items[i].record
        for y in record["output"]["intents"]:
            for label in labels_of(y):
                keys[("nhãn", label)] += 1
            for field in y["entities"].get("fields") or []:
                keys[("mục", field)] += 1
            if y["type"] == "collect_profile":
                for key in y["entities"]:
                    keys[("khóa hồ sơ", key)] += 1
        if record["input"]["pending_question"]:
            keys[("câu đang hỏi", record["type"])] += 1
        if len(record["output"]["intents"]) >= MULTI_MIN_INTENTS:
            keys[("nhiều ý", f"từ {MULTI_MIN_INTENTS} ý")] += 1
    return keys


def minimum_of(key: tuple[str, str], min_test: int) -> int:
    """Mức tối thiểu của một khóa ở tập bị lấy bớt: khóa hồ sơ chỉ cần vài dòng, các loại khác cần min_test."""
    return MIN_PROFILE_KEY if key[0] == "khóa hồ sơ" else min_test


def rebalance(items: list[Item], splits: list[str], min_test: int = MIN_TEST, seed: str = SEED) -> list[str]:
    """Kéo tỉ lệ về gần 70 / 15 / 15 bằng cách chuyển NHÓM từ test và val sang train, miễn là mọi nhãn, mọi mục thông tin trường và mọi loại
    có câu đang hỏi vẫn còn đủ min_test dòng ở tập bị lấy bớt. Nhóm nào đã nằm trong một tập thì giữ nguyên cả nhóm."""
    groups = group_items(items)
    group_split = [splits[g[0]] for g in groups]
    assert all(len({splits[i] for i in g}) == 1 for g in groups), "một nhóm đang nằm ở nhiều tập"
    keys = [group_keys(items, g) for g in groups]
    have: dict[str, Counter] = {s: Counter() for s in SPLITS}
    rows: Counter = Counter()
    for g, split in enumerate(group_split):
        have[split].update(keys[g])
        rows[split] += len(groups[g])
    target = {s: RATIOS[s] * len(items) for s in SPLITS}
    group_key = [
        min(norm(items[i].record["input"]["question"]) + str(items[i].row) + items[i].sheet for i in g)
        for g in groups
    ]
    order = sorted(range(len(groups)), key=lambda g: stable_order(group_key[g], seed))
    for split in ("test", "val"):
        for g in order:
            if rows[split] <= target[split]:
                break
            if group_split[g] != split or rows[split] - len(groups[g]) < target[split] * 0.97:
                continue
            if any(have[split][k] - v < minimum_of(k, min_test) for k, v in keys[g].items()):
                continue  # chuyển đi thì có nhãn, mục hoặc loại tụt dưới mức tối thiểu
            have[split].subtract(keys[g])
            have["train"].update(keys[g])
            rows[split] -= len(groups[g])
            rows["train"] += len(groups[g])
            group_split[g] = "train"
    result = [""] * len(items)
    for g, split in enumerate(group_split):
        for i in groups[g]:
            result[i] = split
    return result


def summarize(items: list[Item], splits: list[str], min_test: int) -> list[str]:
    changed = [Item(it.sheet, it.row, {**it.record, "split": s}) for it, s in zip(items, splits, strict=True)]
    count = Counter(splits)
    lines = ["  số dòng: " + "  ".join(f"{s}={count[s]} ({count[s] / len(items):.0%})" for s in SPLITS)]
    lines.append(f"  hồ sơ nằm ở nhiều tập: {len(check_profile_not_in_two_splits(changed))}")
    lines.append(f"  đầu vào test trùng dòng ở tập khác: {len(check_test_questions_not_in_train(changed))}")
    thin = check_label_has_enough_test(changed, min_test)
    lines.append(f"  nhãn có dưới {min_test} dòng test: {len(thin)}")
    for issue in thin:
        lines.append(f"    - {issue.where}: {issue.detail}")
    return lines


def apply_to_excel(path: Path, items: list[Item], splits: list[str]) -> None:
    """Ghi cột split mới và sắp các dòng của mỗi sheet theo train, val, test (giữ thứ tự cũ trong từng tập)."""
    new_split = {(it.sheet, it.row): s for it, s in zip(items, splits, strict=True)}
    workbook = load_workbook(path)
    for sheet in workbook.worksheets:
        if sheet.title in ("README", "summary"):
            continue
        header = [c.value for c in sheet[1]]
        split_col = header.index("split")
        rows = []
        for number in range(2, sheet.max_row + 1):
            values = [c.value for c in sheet[number]]
            if (sheet.title, number) in new_split:
                values[split_col] = new_split[(sheet.title, number)]
                rows.append((SPLITS.index(values[split_col]), number, values))
        rows.sort(key=lambda r: (r[0], r[1]))
        for target, (_, _, values) in enumerate(rows, start=2):
            for col, value in enumerate(values, start=1):
                sheet.cell(
                    row=target, column=col
                ).value = value  # gán cả None: cell(value=None) sẽ giữ giá trị cũ
    workbook.save(path)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--excel", type=Path, default=DEFAULT_EXCEL)
    parser.add_argument("--min-test", type=int, default=MIN_TEST)
    parser.add_argument("--seed", default=SEED)
    parser.add_argument("--apply", action="store_true", help="ghi vào file Excel (mặc định chỉ in bảng)")
    parser.add_argument(
        "--rebalance",
        action="store_true",
        help="giữ split hiện tại, chỉ chuyển bớt nhóm từ test và val sang train cho gần 70 / 15 / 15 (không phá mức tối thiểu của nhãn)",
    )
    args = parser.parse_args()

    items, issues = read_items(args.excel)
    if issues:
        print(
            "File còn dòng sai chuẩn, sửa trước:",
            *[f"  {i.where}: {i.detail}" for i in issues[:10]],
            sep="\n",
        )
        return 1
    before = [it.record["split"] for it in items]
    after = (
        rebalance(items, before, args.min_test, args.seed)
        if args.rebalance
        else assign_splits(items, args.min_test, args.seed)
    )
    groups = group_items(items)
    print(f"{len(items)} dòng, {len(groups)} nhóm (nhóm lớn nhất {max(map(len, groups))} dòng)")
    print("\nTRƯỚC:", *summarize(items, before, args.min_test), sep="\n")
    print("\nSAU:", *summarize(items, after, args.min_test), sep="\n")
    labels = label_counts(
        [Item(it.sheet, it.row, {**it.record, "split": s}) for it, s in zip(items, after, strict=True)]
    )
    print(
        f"\n{len(labels)} nhãn con; ít dòng test nhất: "
        + ", ".join(f"{k}={v['test']}" for k, v in sorted(labels.items(), key=lambda kv: kv[1]["test"])[:5])
    )
    if args.apply:
        apply_to_excel(args.excel, items, after)
        print(
            f"\nĐã ghi {args.excel}. Chạy tiếp: python -m scripts.build_slm_jsonl && python -m scripts.check_slm_data"
        )
    else:
        print("\n(Chạy thử, chưa ghi file. Thêm --apply để ghi.)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
