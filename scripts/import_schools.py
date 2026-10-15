"""Nhập dữ liệu trường từ file Excel của nhóm thu thập (sheet Schools) vào Postgres.

Chỉ cập nhật offering bậc đại học (undergraduate). Khớp trường theo mã (universities.code):
có rồi thì cập nhật, chưa có thì thêm mới. Chạy lại nhiều lần được.

Mặc định chỉ chạy thử (in ra sẽ thêm/sửa gì). Thêm --apply mới ghi vào DB.

Chạy (dùng venv của advisor, đã có psycopg và openpyxl):
    advisor\\.venv\\Scripts\\python scripts\\import_schools.py "usdss_research_duongnx_0857_141026.xlsx"
    advisor\\.venv\\Scripts\\python scripts\\import_schools.py "usdss_research_duongnx_0857_141026.xlsx" --apply
Chỉ nhập vài trường để thử: thêm --limit 20 (20 trường đầu) hoặc --codes 190099,166027
DB mặc định là Postgres trong docker-compose (localhost:5432); đổi bằng biến môi trường DATABASE_URL.
"""

import argparse
import os
import re
import sys
from collections import Counter

import openpyxl
import psycopg

DEFAULT_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"
LEVEL = "undergraduate"

# Cột của bảng universities lấy từ Excel (cùng tên khóa)
UNIVERSITY_COLS = ["name", "city", "state", "website", "control", "acceptance_rate", "international_student_count"]

# Cột của bảng university_offerings lấy từ Excel (cùng tên khóa), trừ majors/stem_majors/academic_year xử lý riêng
OFFERING_COLS = [
    "tuition_usd", "fees_usd", "living_usd", "other_expenses_usd",
    "min_gpa4", "min_ielts", "min_toefl", "min_duolingo",
    "avg_gpa4", "sat25", "sat75", "sat_policy", "sat_submit_pct",
    "gpa_pct_4_0", "gpa_pct_375_399", "gpa_pct_350_374", "gpa_pct_325_349",
    "gpa_pct_300_324", "gpa_pct_250_299", "gpa_pct_below_250",
    "c7_gpa_importance", "c7_test_importance", "c7_ec_importance",
    "intl_acceptance_rate", "intl_aid_policy", "intl_aid_avg_usd",
    "retention_rate_1yr", "grad_rate_6yr",
]
ALL_OFFERING_COLS = ["majors", "stem_majors", "academic_year", *OFFERING_COLS]

# 14 trường của file nghiên cứu nhóm A dùng mã tạm "GA-..."; DB đã có các trường này theo mã IPEDS.
# Đổi sang mã IPEDS để cập nhật đúng trường, không tạo trùng. Nhóm thu thập nên dùng luôn mã IPEDS.
GROUP_A_CODES = {
    "GA-BSU": "150136", "GA-FGCU": "433660", "GA-LSU": "159391", "GA-MSU": "180461",
    "GA-NDSU": "200332", "GA-TXST": "228459", "GA-UNF": "136172", "GA-UNT": "227216",
    "GA-USA": "102094", "GA-USD": "219471", "GA-UTSA": "229027", "GA-UWYO": "240727",
    "GA-VCU": "234030", "GA-WVU": "238032",
}


def read_schools(path):
    """Đọc sheet Schools. Tiêu đề dạng 'Nhãn tiếng Việt\\n[khóa]' nên lấy phần trong []."""
    wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    rows = list(wb["Schools"].iter_rows(values_only=True))
    keys = [re.search(r"\[(.+)\]", str(h)).group(1) if h and "[" in str(h) else h for h in rows[0]]
    out = []
    for row in rows[1:]:
        d = dict(zip(keys, row, strict=False))
        if d.get("code") and d.get("study_level") == LEVEL:
            code = str(d["code"]).strip()
            d["code"] = GROUP_A_CODES.get(code, code)
            out.append(d)
    return out


def split_list(value):
    return [x.strip() for x in str(value).split("|") if x.strip()] if value else []


def clean(d):
    """Chuẩn hóa 1 dòng Excel thành 2 dict: cho universities và cho university_offerings."""
    uni = {k: d.get(k) for k in UNIVERSITY_COLS}
    uni["is_active"] = d.get("is_active") is not False
    off = {k: d.get(k) for k in OFFERING_COLS}
    off["majors"] = split_list(d.get("majors"))
    off["stem_majors"] = split_list(d.get("stem_majors"))
    off["academic_year"] = str(d["cost_year"])[:16] if d.get("cost_year") else None
    # SAT không hợp lệ (vi phạm ràng buộc của bảng) thì bỏ, không làm hỏng cả lần nhập
    if not (off["sat25"] and off["sat75"] and 400 <= off["sat25"] <= off["sat75"] <= 1600):
        off["sat25"] = off["sat75"] = None
    return uni, off


def run(path, db_url, apply, limit=0, codes=None):
    schools = read_schools(path)
    if codes:
        schools = [d for d in schools if d["code"] in codes]
    if limit:
        schools = schools[:limit]
    print(f"Đọc {len(schools)} trường bậc đại học từ {os.path.basename(path)}")
    stats = Counter()
    with psycopg.connect(db_url) as conn, conn.cursor() as cur:
        for d in schools:
            uni, off = clean(d)
            cur.execute("SELECT id FROM app.universities WHERE code = %s", (d["code"],))
            row = cur.fetchone()
            if row:
                uni_id = row[0]
                sets = ", ".join(f"{k} = %s" for k in [*UNIVERSITY_COLS, "is_active"])
                cur.execute(f"UPDATE app.universities SET {sets}, updated_at = now() WHERE id = %s",
                            [*uni.values(), uni_id])
                stats["trường: cập nhật"] += 1
            else:
                cols = ["code", *UNIVERSITY_COLS, "is_active"]
                cur.execute(
                    f"INSERT INTO app.universities (id, {', '.join(cols)}, created_at) "
                    f"VALUES (gen_random_uuid(), {', '.join(['%s'] * len(cols))}, now()) RETURNING id",
                    [d["code"], *uni.values()])
                uni_id = cur.fetchone()[0]
                stats["trường: thêm mới"] += 1
                print(f"  + trường mới {d['code']} {uni['name']}")

            cur.execute("SELECT 1 FROM app.university_offerings WHERE university_id = %s AND study_level = %s",
                        (uni_id, LEVEL))
            stats["offering: cập nhật" if cur.fetchone() else "offering: thêm mới"] += 1
            values = [off[k] for k in ALL_OFFERING_COLS]
            updates = ", ".join(f"{k} = EXCLUDED.{k}" for k in ALL_OFFERING_COLS)
            cur.execute(
                f"INSERT INTO app.university_offerings (id, university_id, study_level, {', '.join(ALL_OFFERING_COLS)}, created_at) "
                f"VALUES (gen_random_uuid(), %s, %s, {', '.join(['%s'] * len(ALL_OFFERING_COLS))}, now()) "
                f"ON CONFLICT (university_id, study_level) DO UPDATE SET {updates}, updated_at = now()",
                [uni_id, LEVEL, *values])

        for key, n in sorted(stats.items()):
            print(f"  {key}: {n}")
        filled = Counter(k for d in schools for k in ("c7_gpa_importance", "gpa_pct_4_0", "intl_acceptance_rate")
                         if d.get(k) is not None)
        print(f"  có C7: {filled['c7_gpa_importance']}, có phân phối GPA: {filled['gpa_pct_4_0']}, "
              f"có tỉ lệ du học sinh: {filled['intl_acceptance_rate']}")
        if apply:
            conn.commit()
            print("Đã ghi vào DB.")
        else:
            conn.rollback()
            print("Chạy thử: chưa ghi gì. Thêm --apply để ghi.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("xlsx", help="file Excel của nhóm thu thập dữ liệu")
    parser.add_argument("--apply", action="store_true", help="ghi vào DB (mặc định chỉ chạy thử)")
    parser.add_argument("--limit", type=int, default=0, help="chỉ nhập N trường đầu (0 = tất cả)")
    parser.add_argument("--codes", default="", help="chỉ nhập các mã trường này, cách nhau bằng dấu phẩy")
    args = parser.parse_args()
    codes = {c.strip() for c in args.codes.split(",") if c.strip()}
    run(args.xlsx, os.environ.get("DATABASE_URL", DEFAULT_DB), args.apply, args.limit, codes)
    sys.exit(0)
