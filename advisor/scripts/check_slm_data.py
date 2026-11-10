"""Kiểm dữ liệu SLM trong file Excel trước khi chia lại, thêm dòng hay train. Chỉ đọc, không sửa file.

Báo lỗi theo sheet và số dòng Excel. Có lỗi thì thoát mã 1, nên chạy được trong bước kiểm trước khi train.

Các kiểm tra:
  1. Đoạn chữ (doan) của mỗi ý phải nằm trong câu hỏi.
  2. Một dòng chỉ có tối đa 1 ý mac_dinh (thu thập hồ sơ): nhiều giá trị hồ sơ trong một câu thì gộp vào một ý.
  3. Dòng test không được có CÙNG đầu vào (câu hỏi, hồ sơ, ngữ cảnh, câu đang hỏi) với dòng ở train hoặc val.
     Câu chữ trùng nhưng hồ sơ hoặc ngữ cảnh khác thì chỉ đếm để xem (đó là đầu vào khác nhau).
  4. Một hồ sơ chỉ nằm ở một tập (train, val hoặc test), không được lẫn.
  5. Mỗi nhãn con có đủ số dòng test tối thiểu (mặc định 10), nếu không thì F1 theo nhãn không đáng tin.
  6. Không còn skill_md trong dữ liệu (skill do code điền khuôn, SLM chỉ học tách ý).
  7. Mỗi mục thông tin trường (muc: chi phí, học bổng, ký túc xá, khuôn viên…) có đủ dòng test.
  8. Câu từ 3 ý trở lên có đủ dòng test và val (kiểm việc tách nhiều ý).
  9. Mỗi khóa hồ sơ (gpa, visa_refused, standardized_plan…) có đủ dòng test và val.
Ngoài ra in số dòng có câu hệ thống đang hỏi (pending_question) theo loại, chỉ để xem.

Chạy trong thư mục advisor:
  python -m scripts.check_slm_data
  python -m scripts.check_slm_data --excel data/slm/du_lieu_slm.xlsx --min-test 10 --show 8
"""

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from openpyxl import load_workbook

from app.schemas.slm import INFO_FIELDS, PROFILE_KEYS
from scripts.build_slm_jsonl import (
    CONTEXT_COLUMN,
    DEFAULT_EXCEL,
    EN_COLUMNS,
    EN_SHEETS,
    SPLITS,
    TYPES,
    as_text,
    to_record,
)

MIN_TEST = 10  # số dòng test tối thiểu của một nhãn con
# Khóa trong thuc_the dùng làm nhãn con của từng loại ý (loại không có thì chỉ có nhãn chung "-")
LABEL_KEY = {
    "mac_dinh": "nhiem_vu",
    "ho_so": "cac_nhiem_vu",
    "kich_ban": "che_do",
    "kien_thuc": "nhiem_vu",
    "trung_tam": "nhiem_vu",
    "ngoai_pham_vi": "loai_con",
}


@dataclass(frozen=True)
class Item:
    sheet: str
    row: int  # số dòng trong Excel (dòng 1 là tiêu đề)
    record: dict[str, Any]

    @property
    def where(self) -> str:
        return f"{self.sheet}, dòng {self.row}"


@dataclass(frozen=True)
class Issue:
    check: str
    where: str
    detail: str


def norm(text: str) -> str:
    return re.sub(r"\s+", " ", str(text).strip().lower())


def read_items(path: Path) -> tuple[list[Item], list[Issue]]:
    """Đọc từng dòng Excel thành bản ghi (cùng cách với build_slm_jsonl) và nhớ số dòng. Dòng sai chuẩn thì báo, không dừng."""
    workbook = load_workbook(path, read_only=True, data_only=True)
    items: list[Item] = []
    issues: list[Issue] = []
    for sheet in workbook.worksheets:
        if sheet.title in ("README", "summary"):
            continue
        intent = EN_SHEETS.get(sheet.title, sheet.title)
        if intent not in TYPES:
            issues.append(Issue("sheet", sheet.title, f"không phải một loại trong {TYPES}"))
            continue
        rows = sheet.iter_rows(values_only=True)
        header = tuple(EN_COLUMNS.get(as_text(c), as_text(c)) for c in next(rows, ()) if as_text(c))
        count = 0
        for number, values in enumerate(rows, start=2):
            raw = dict(zip(header, values, strict=False))
            if not any(as_text(v) for v in raw.values()):
                continue
            count += 1
            where = f"{sheet.title}, dòng {number}"
            try:
                record = to_record(intent, raw, where, f"{intent}_{count:03d}")
            except ValueError as ex:
                issues.append(Issue("định dạng", where, str(ex)))
                continue
            items.append(Item(sheet.title, number, record))
    assert CONTEXT_COLUMN  # cột context nằm trong header, to_record đọc riêng
    return items, issues


def check_spans(items: list[Item]) -> list[Issue]:
    found = []
    for it in items:
        question = norm(it.record["input"]["cau_hoi"])
        for y in it.record["output"]["cac_y"]:
            if norm(y["doan"]) not in question:
                found.append(
                    Issue(
                        "doan không nằm trong câu",
                        it.where,
                        f"“{y['doan']}” ← “{it.record['input']['cau_hoi']}”",
                    )
                )
    return found


def check_one_default_per_row(items: list[Item]) -> list[Issue]:
    found = []
    for it in items:
        n = sum(1 for y in it.record["output"]["cac_y"] if y["type"] == "mac_dinh")
        if n > 1:
            found.append(Issue("nhiều ý mac_dinh", it.where, f"{n} ý mac_dinh, phải gộp thành 1"))
    return found


def input_key(record: dict[str, Any]) -> str:
    """Đầu vào của SLM: câu hỏi + hồ sơ + ngữ cảnh + câu hệ thống đang hỏi. Hai dòng cùng khóa này là cùng một đầu vào."""
    inp = record["input"]
    return json.dumps(
        [
            norm(inp["cau_hoi"]),
            inp.get("ho_so") or None,
            inp.get("dang_noi_ve"),
            inp["cau_he_thong_dang_hoi"],
        ],
        sort_keys=True,
        ensure_ascii=False,
    )


def check_test_questions_not_in_train(items: list[Item]) -> list[Issue]:
    """Dòng test có CÙNG đầu vào (câu, hồ sơ, ngữ cảnh, câu đang hỏi) với dòng ở train hoặc val: model đã thấy đúng dòng này."""
    splits_of: dict[str, set[str]] = defaultdict(set)
    for it in items:
        splits_of[input_key(it.record)].add(it.record["split"])
    found = []
    for it in items:
        if it.record["split"] != "test":
            continue
        other = splits_of[input_key(it.record)] - {"test"}
        if other:
            found.append(
                Issue(
                    "đầu vào test trùng dòng ở tập khác",
                    it.where,
                    f"“{it.record['input']['cau_hoi']}” cũng có ở {sorted(other)}",
                )
            )
    return found


def count_same_text_other_input(items: list[Item]) -> int:
    """Số dòng test có câu chữ trùng một dòng ở train hoặc val nhưng hồ sơ hoặc ngữ cảnh khác. Chỉ để xem, không tính là lỗi."""
    text_splits: dict[str, set[str]] = defaultdict(set)
    for it in items:
        text_splits[norm(it.record["input"]["cau_hoi"])].add(it.record["split"])
    same_input = {id(it) for it in items} and {it.where for it in check_test_questions_not_in_train(items)}
    return sum(
        1
        for it in items
        if it.record["split"] == "test"
        and text_splits[norm(it.record["input"]["cau_hoi"])] - {"test"}
        and it.where not in same_input
    )


def profile_key(record: dict[str, Any]) -> str | None:
    profile = record["input"].get("ho_so")
    return json.dumps(profile, sort_keys=True, ensure_ascii=False) if profile else None


def check_profile_not_in_two_splits(items: list[Item]) -> list[Issue]:
    where_of: dict[str, list[Item]] = defaultdict(list)
    for it in items:
        key = profile_key(it.record)
        if key:
            where_of[key].append(it)
    found = []
    for group in where_of.values():
        splits = {it.record["split"] for it in group}
        if len(splits) > 1:
            rows = ", ".join(f"{it.sheet}:{it.row}({it.record['split']})" for it in group[:4])
            found.append(
                Issue(
                    "hồ sơ nằm ở nhiều tập",
                    group[0].where,
                    f"{len(group)} dòng, tập {sorted(splits)}: {rows}…",
                )
            )
    return found


def labels_of(y: dict[str, Any]) -> list[str]:
    """Nhãn con của một ý: loại + nhiệm vụ. Ý có nhiều nhiệm vụ (ho_so) tính vào từng nhiệm vụ riêng, vì F1 đo theo từng nhiệm vụ."""
    value = y["thuc_the"].get(LABEL_KEY[y["type"]])
    values = value if isinstance(value, list) else [value]
    return [f"{y['type']}/{v or '-'}" for v in values] or [f"{y['type']}/-"]


def label_counts(items: list[Item]) -> dict[str, Counter]:
    counts: dict[str, Counter] = defaultdict(Counter)
    for it in items:
        for y in it.record["output"]["cac_y"]:
            for label in labels_of(y):
                counts[label][it.record["split"]] += 1
    return counts


def check_label_has_enough_test(items: list[Item], min_test: int) -> list[Issue]:
    found = []
    for label, c in sorted(label_counts(items).items()):
        if c["test"] < min_test:
            found.append(
                Issue(
                    "nhãn thiếu dòng test",
                    label,
                    f"test {c['test']} (cần ≥ {min_test}), val {c['val']}, train {c['train']}",
                )
            )
    return found


def field_counts(items: list[Item]) -> dict[str, Counter]:
    """Số ý kien_thuc nêu từng mục thông tin trường (muc) theo tập."""
    counts: dict[str, Counter] = {f: Counter() for f in INFO_FIELDS}
    for it in items:
        for y in it.record["output"]["cac_y"]:
            for field in y["thuc_the"].get("muc") or []:
                counts[field][it.record["split"]] += 1
    return counts


def check_every_info_field_has_enough_test(items: list[Item], min_test: int) -> list[Issue]:
    """Mỗi mục thông tin trường trong hợp đồng (chi phí, ký túc xá, khuôn viên…) phải có đủ dòng test, nếu không model chưa được kiểm mục đó."""
    found = []
    for field, c in field_counts(items).items():
        if c["test"] < min_test:
            found.append(
                Issue(
                    "mục thông tin thiếu dòng test",
                    field,
                    f"test {c['test']} (cần ≥ {min_test}), val {c['val']}, train {c['train']}",
                )
            )
    return found


MULTI_MIN_INTENTS = 3  # từ ngần này ý trở lên là câu nhiều ý
MIN_PROFILE_KEY = 3  # mỗi khóa hồ sơ cần ít nhất ngần này dòng test (và val)


def check_multi_intent_has_enough_test_and_val(items: list[Item], min_rows: int) -> list[Issue]:
    """Câu từ 3 ý trở lên phải có đủ dòng test và val, nếu không chưa kiểm được việc tách nhiều ý."""
    counts: Counter = Counter()
    for it in items:
        if len(it.record["output"]["cac_y"]) >= MULTI_MIN_INTENTS:
            counts[it.record["split"]] += 1
    return [
        Issue(
            "câu nhiều ý thiếu dòng",
            split,
            f"{counts[split]} dòng từ {MULTI_MIN_INTENTS} ý trở lên (cần ≥ {min_rows})",
        )
        for split in ("test", "val")
        if counts[split] < min_rows
    ]


def check_every_profile_key_has_test_and_val(
    items: list[Item], minimum: int = MIN_PROFILE_KEY
) -> list[Issue]:
    """Mỗi khóa hồ sơ (gpa, visa_refused, standardized_plan…) phải có dòng test và val trong ý khai hồ sơ."""
    counts: dict[str, Counter] = {k: Counter() for k in PROFILE_KEYS}
    for it in items:
        for y in it.record["output"]["cac_y"]:
            if y["type"] == "mac_dinh":
                for key in y["thuc_the"]:
                    if key in counts:
                        counts[key][it.record["split"]] += 1
    found = []
    for key, c in counts.items():
        for split in ("test", "val"):
            if c[split] < minimum:
                found.append(
                    Issue(
                        "khóa hồ sơ thiếu dòng",
                        key,
                        f"{split} {c[split]} (cần ≥ {minimum}), train {c['train']}",
                    )
                )
    return found


def check_no_skill(items: list[Item]) -> list[Issue]:
    return [
        Issue("còn skill_md", it.where, "dữ liệu train không còn skill")
        for it in items
        if it.record.get("skill_md")
    ]


def pending_summary(items: list[Item]) -> dict[str, int]:
    counts: Counter = Counter()
    for it in items:
        if it.record["input"]["cau_he_thong_dang_hoi"]:
            counts[it.record["type"]] += 1
    return dict(counts)


def run_checks(items: list[Item], min_test: int = MIN_TEST) -> dict[str, list[Issue]]:
    return {
        "1. doan nằm trong câu hỏi": check_spans(items),
        "2. mỗi dòng tối đa 1 ý mac_dinh": check_one_default_per_row(items),
        "3. đầu vào test không trùng dòng ở train/val": check_test_questions_not_in_train(items),
        "4. một hồ sơ chỉ ở một tập": check_profile_not_in_two_splits(items),
        f"5. mỗi nhãn có ≥ {min_test} dòng test": check_label_has_enough_test(items, min_test),
        "6. không còn skill_md": check_no_skill(items),
        f"7. mỗi mục thông tin trường có ≥ {min_test} dòng test": check_every_info_field_has_enough_test(
            items, min_test
        ),
        f"8. câu từ {MULTI_MIN_INTENTS} ý trở lên có ≥ {min_test} dòng test và val": check_multi_intent_has_enough_test_and_val(
            items, min_test
        ),
        f"9. mỗi khóa hồ sơ có ≥ {MIN_PROFILE_KEY} dòng test và val": check_every_profile_key_has_test_and_val(
            items
        ),
    }


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--excel", type=Path, default=DEFAULT_EXCEL)
    parser.add_argument("--min-test", type=int, default=MIN_TEST)
    parser.add_argument("--show", type=int, default=6, help="số lỗi in ra cho mỗi kiểm tra")
    args = parser.parse_args()

    items, read_issues = read_items(args.excel)
    by_split = Counter(it.record["split"] for it in items)
    print(f"Đọc {len(items)} dòng từ {args.excel.name}: " + "  ".join(f"{s}={by_split[s]}" for s in SPLITS))
    total = len(read_issues)
    for issue in read_issues:
        print(f"  [định dạng] {issue.where}: {issue.detail}")

    for name, issues in run_checks(items, args.min_test).items():
        total += len(issues)
        print(f"\n{'OK ' if not issues else 'LỖI'}  {name}: {len(issues)}")
        for issue in issues[: args.show]:
            print(f"   - {issue.where}: {issue.detail}")
        if len(issues) > args.show:
            print(f"   … và {len(issues) - args.show} lỗi nữa")

    same_text = count_same_text_other_input(items)
    print(
        f"\nDòng test có câu chữ trùng train/val nhưng hồ sơ hoặc ngữ cảnh khác (chỉ để xem, không tính lỗi): {same_text}"
    )
    print("Số dòng có câu hệ thống đang hỏi (pending_question) theo loại: " + str(pending_summary(items)))
    print(f"\nTổng số lỗi: {total}")
    return 1 if total else 0


if __name__ == "__main__":
    raise SystemExit(main())
