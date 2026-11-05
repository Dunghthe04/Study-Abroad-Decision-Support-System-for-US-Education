"""Bước 7b: điền cột ho_so và ho_so_tinh_san của 35 dòng có skill trong sheet ho_so (du_lieu_slm.xlsx) từ ho_so_goc.json.

Dựng hai cột bằng đúng code chat thật dùng (skill_request_of: profile_to_skill_input và calc_profile), nên đầu vào sinh
skill lúc train giống hệt lúc chạy. Ngày hôm nay cố định TODAY để chạy lại ra cùng kết quả. Sheet chưa có cột
ho_so_tinh_san thì chèn ngay sau cột ho_so (thứ tự build_slm_jsonl.columns_of đòi). Dòng không có trong ho_so_goc.json
(dòng ho_so không có skill) giữ nguyên.

id của dòng tính như build_slm_jsonl (đếm dòng không trống trong sheet), và phải khớp câu hỏi của id đó trong
du_lieu_slm.jsonl (nguồn làm ra ho_so_goc.json), lệch thì dừng để không ghi nhầm dòng.

Sau bước này skill_md cũ chưa có số của tinh_san nên build_slm_jsonl báo "skill_md sai" ở các dòng này cho tới khi viết
lại skill (bước 7d).

  python -m scripts.fill_ho_so_excel
"""

import datetime
import json
from copy import copy
from pathlib import Path

from openpyxl import load_workbook
from openpyxl.worksheet.worksheet import Worksheet

from app.schemas.chat import ChatState
from app.services.skill_input import skill_request_of
from scripts.build_slm_jsonl import CALC_COLUMN, DEFAULT_EXCEL, DEFAULT_OUT, as_text
from scripts.make_ho_so_goc import OUT as SOURCE

TODAY = datetime.date(2026, 11, 5)
SHEET = "ho_so"
PROFILE_COLUMN = "ho_so"
CALC_WIDTH = 45


def to_json(value: dict) -> str:
    return json.dumps(value, ensure_ascii=False)


# Chèn cột tính sẵn ngay sau cột hồ sơ, chép kiểu ô tiêu đề; đã có thì thôi. Trả về số cột (1-based) của hai cột
def ensure_calc_column(ws: Worksheet) -> tuple[int, int]:
    header = [as_text(cell.value) for cell in ws[1]]
    profile_col = header.index(PROFILE_COLUMN) + 1
    if CALC_COLUMN not in header:
        widths = {
            i: ws.column_dimensions[ws.cell(1, i).column_letter].width for i in range(1, len(header) + 1)
        }
        ws.insert_cols(profile_col + 1)
        # insert_cols không dời độ rộng cột: đặt lại cho các cột sau chỗ chèn
        for i, width in widths.items():
            target = i + 1 if i > profile_col else i
            ws.column_dimensions[ws.cell(1, target).column_letter].width = width
        source, cell = ws.cell(1, profile_col), ws.cell(1, profile_col + 1)
        cell.value = CALC_COLUMN
        cell.font, cell.fill, cell.alignment, cell.border = (
            copy(source.font),
            copy(source.fill),
            copy(source.alignment),
            copy(source.border),
        )
        ws.column_dimensions[cell.column_letter].width = CALC_WIDTH
    return profile_col, profile_col + 1


def questions_of(jsonl: Path) -> dict[str, str]:
    records = (json.loads(line) for line in jsonl.read_text(encoding="utf-8").splitlines())
    return {r["id"]: r["input"]["cau_hoi"] for r in records if r["type"] == SHEET}


def fill(ws: Worksheet, source: dict, questions: dict[str, str]) -> int:
    profile_col, calc_col = ensure_calc_column(ws)
    question_col = [as_text(cell.value) for cell in ws[1]].index("cau_hoi") + 1
    count = filled = 0
    for row in ws.iter_rows(min_row=2):
        if not any(as_text(cell.value) for cell in row):
            continue  # dòng trống không tính id, như build_slm_jsonl
        count += 1
        record_id = f"{SHEET}_{count:03d}"
        if record_id not in source:
            continue
        question = as_text(ws.cell(row[0].row, question_col).value)
        if question != questions.get(record_id):
            raise ValueError(
                f"Dòng {row[0].row} ({record_id}): câu hỏi không khớp du_lieu_slm.jsonl, dừng để không ghi nhầm"
            )
        item = source[record_id]
        state = ChatState(
            profile=item["profile"], transcript=item["transcript"], benchmarks=item["benchmarks"]
        )
        request = skill_request_of(state, question, TODAY)
        alignment = copy(ws.cell(row[0].row, profile_col).alignment)
        ws.cell(row[0].row, profile_col).value = to_json(request.ho_so)
        calc = ws.cell(row[0].row, calc_col)
        calc.value = to_json(request.tinh_san)
        calc.alignment = alignment
        filled += 1
    if filled != len(source):
        raise ValueError(f"Chỉ điền được {filled}/{len(source)} hồ sơ: thiếu id trong sheet {SHEET}")
    return filled


def main(excel: Path = DEFAULT_EXCEL, source: Path = SOURCE, jsonl: Path = DEFAULT_OUT) -> None:
    workbook = load_workbook(excel)
    filled = fill(workbook[SHEET], json.loads(source.read_text(encoding="utf-8")), questions_of(jsonl))
    workbook.save(excel)
    print(f"Đã điền {filled} dòng ({PROFILE_COLUMN}, {CALC_COLUMN}) trong sheet {SHEET} → {excel}")


if __name__ == "__main__":
    main()
