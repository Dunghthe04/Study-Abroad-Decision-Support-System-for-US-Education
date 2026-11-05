"""Nhập bộ dữ liệu trường (workbook usdss_dataset_*.xlsx, cấu trúc dataset_v2) vào Postgres.

Đọc các sheet:
  - Import SQL                  : thông tin trường và offering (mỗi dòng 1 trường)
  - Facts                       : các cột Import SQL chưa có (ACT, C7, phân phối GPA, aid...);
                                  chỉ lấy fact status verified/derived và is_primary = Y
  - Majors                      : danh mục ngành (tên + mã CIP)
  - University_Offering_Majors  : ngành của từng trường, kèm cờ STEM

Khớp trường theo mã IPEDS (universities.code), offering theo (trường, bậc học):
có rồi thì cập nhật, chưa có thì thêm mới. Ngành của offering được thay toàn bộ theo file.
Chạy lại nhiều lần được.

Mặc định chỉ chạy thử: đọc và kiểm tra file, in thống kê, không kết nối DB.
Thêm --apply để ghi vào DB (một transaction, lỗi thì không ghi gì).

Cần psycopg và openpyxl:
    python scripts\\import_school_dataset.py "usdss_dataset_2257_311026.xlsx"
    python scripts\\import_school_dataset.py "usdss_dataset_2257_311026.xlsx" --apply
Chỉ nhập vài trường để thử: thêm --codes 190099,166027
DB mặc định là Postgres trong docker-compose (localhost:5432); đổi bằng biến môi trường DATABASE_URL.
"""

import argparse
import datetime
import os
import sys
from collections import Counter, defaultdict

import openpyxl

DEFAULT_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"
FACT_STATUSES_USED = ("verified", "derived")

# Cột của bảng universities lấy từ sheet Import SQL (cùng tên cột)
UNIVERSITY_COLS = [
    "name",
    "city",
    "state",
    "website",
    "control",
    "acceptance_rate",
    "international_student_count",
]

# Cột của bảng university_offerings lấy từ sheet Import SQL (cùng tên cột)
OFFERING_COLS_FROM_IMPORT = [
    "tuition_usd",
    "living_usd",
    "fees_usd",
    "min_gpa4",
    "min_ielts",
    "min_toefl",
    "min_duolingo",
    "avg_gpa4",
    "sat25",
    "sat75",
    "sat_policy",
    "deadlines_json",
    "notes",
    "source_url",
    "retrieved_at",
]

# Cột của bảng university_offerings lấy từ sheet Facts (field_key trùng tên cột)
OFFERING_COLS_FROM_FACTS = [
    "act25",
    "act75",
    "applicants_total",
    "admitted_total",
    "intl_applicants",
    "intl_admitted",
    "intl_acceptance_rate",
    "c7_gpa_importance",
    "c7_test_importance",
    "c7_ec_importance",
    "sat_submit_pct",
    "gpa_submit_pct",
    "gpa_pct_4_0",
    "gpa_pct_375_399",
    "gpa_pct_350_374",
    "gpa_pct_325_349",
    "gpa_pct_300_324",
    "gpa_pct_250_299",
    "gpa_pct_below_250",
    "intl_aid_policy",
    "intl_aid_avg_usd",
    "intl_aid_recipients",
    "retention_rate_1yr",
    "grad_rate_6yr",
    "other_expenses_usd",
]

# Các cột tỉ lệ, phải nằm trong 0–1
RATE_COLS = [
    "intl_acceptance_rate",
    "sat_submit_pct",
    "gpa_submit_pct",
    "gpa_pct_4_0",
    "gpa_pct_375_399",
    "gpa_pct_350_374",
    "gpa_pct_325_349",
    "gpa_pct_300_324",
    "gpa_pct_250_299",
    "gpa_pct_below_250",
    "retention_rate_1yr",
    "grad_rate_6yr",
]

ALL_OFFERING_COLS = [
    "academic_year",
    *OFFERING_COLS_FROM_IMPORT,
    *OFFERING_COLS_FROM_FACTS,
]


def read_sheet(wb, sheet_name):
    """Đọc 1 sheet thành list dict theo dòng tiêu đề, bỏ các dòng trống."""
    rows = list(wb[sheet_name].iter_rows(values_only=True))
    header = [str(h).strip() if h is not None else "" for h in rows[0]]
    result = []
    for row in rows[1:]:
        if not row or row[0] in (None, ""):
            continue
        result.append(dict(zip(header, row, strict=False)))
    return result


def to_text(value):
    if value is None:
        return None
    text = str(value).strip()
    if text == "":
        return None
    return text


def to_bool(value):
    text = str(value).strip().lower()
    return text in ("true", "yes", "1", "y")


def to_date(value):
    if value is None or value == "":
        return None
    if isinstance(value, datetime.datetime):
        return value.date()
    if isinstance(value, datetime.date):
        return value
    return datetime.date.fromisoformat(str(value)[:10])


def short_academic_year(cost_year):
    """'2026-2027' -> '2026-27' cho giống định dạng academic_year đang có trong DB."""
    text = to_text(cost_year)
    if text is None:
        return None
    parts = text.split("-")
    if len(parts) == 2 and len(parts[0]) == 4 and len(parts[1]) == 4:
        return f"{parts[0]}-{parts[1][2:]}"
    return text


def read_facts(wb):
    """Trả về dict (mã trường, field_key) -> giá trị, chỉ lấy fact dùng được."""
    facts = {}
    for row in read_sheet(wb, "Facts"):
        if row.get("status") not in FACT_STATUSES_USED:
            continue
        if str(row.get("is_primary")).strip().upper() != "Y":
            continue
        value = row.get("value")
        if value is None or value == "":
            continue
        code = str(row["school_code"]).strip()
        facts[(code, row["field_key"])] = value
    return facts


def read_majors(wb):
    """Sheet Majors -> list (tên ngành, mã CIP)."""
    majors = []
    for row in read_sheet(wb, "Majors"):
        name = to_text(row.get("name"))
        cip_code = to_text(row.get("cip_code"))
        if name:
            majors.append((name, cip_code))
    return majors


def read_offering_majors(wb):
    """Sheet University_Offering_Majors -> dict (mã trường, bậc học) -> list (tên ngành, is_stem)."""
    links = defaultdict(list)
    for row in read_sheet(wb, "University_Offering_Majors"):
        code = str(row["school_code (→ university_offering)"]).strip()
        level = to_text(row.get("study_level"))
        major_name = to_text(row.get("major_name (→ majors.name)"))
        is_stem = to_bool(row.get("is_stem"))
        is_verified = to_bool(row.get("is_verified"))
        links[(code, level)].append((major_name, is_stem, is_verified))
    return links


def build_school(row, facts, warnings):
    """Ghép 1 dòng Import SQL với Facts thành 3 phần: universities, university_offerings và mã/bậc học."""
    code = str(row["code"]).strip()
    level = to_text(row.get("study_level")) or "undergraduate"

    uni = {}
    for col in UNIVERSITY_COLS:
        uni[col] = row.get(col)
    uni["is_ivy"] = to_bool(facts.get((code, "is_ivy")))
    uni["is_active"] = to_bool(row.get("is_active"))

    off = {}
    for col in OFFERING_COLS_FROM_IMPORT:
        off[col] = row.get(col)
    for col in OFFERING_COLS_FROM_FACTS:
        off[col] = facts.get((code, col))
    off["retrieved_at"] = to_date(off["retrieved_at"])
    off["deadlines_json"] = to_text(off["deadlines_json"])

    academic_year = short_academic_year(facts.get((code, "cost_year")))
    if academic_year is None:
        academic_year = to_text(row.get("academic_year"))
    off["academic_year"] = academic_year

    # SAT phải đủ cả 2 mốc và đúng khoảng (công thức gợi ý cần cả 2), nếu không thì bỏ cả cặp
    sat25 = off["sat25"]
    sat75 = off["sat75"]
    sat_ok = sat25 is not None and sat75 is not None and 400 <= sat25 <= sat75 <= 1600
    if not sat_ok and (sat25 is not None or sat75 is not None):
        warnings.append(f"{code}: SAT không đủ/không hợp lệ ({sat25}, {sat75}) -> bỏ")
        off["sat25"] = None
        off["sat75"] = None

    # ACT được thiếu 1 mốc (fact mốc còn lại có thể chưa xác minh), chỉ bỏ khi sai khoảng
    act25 = off["act25"]
    act75 = off["act75"]
    act_out_of_range = False
    for act in (act25, act75):
        if act is not None and not 1 <= act <= 36:
            act_out_of_range = True
    if act25 is not None and act75 is not None and act25 > act75:
        act_out_of_range = True
    if act_out_of_range:
        warnings.append(f"{code}: ACT không hợp lệ ({act25}, {act75}) -> bỏ")
        off["act25"] = None
        off["act75"] = None

    for col in ("avg_gpa4", "min_gpa4"):
        gpa = off[col]
        if gpa is not None and not 0 <= gpa <= 4:
            warnings.append(f"{code}: {col} = {gpa} ngoài thang 4 -> bỏ")
            off[col] = None

    for col in RATE_COLS:
        rate = off[col]
        if rate is not None and not 0 <= rate <= 1:
            warnings.append(f"{code}: {col} = {rate} ngoài khoảng 0–1 -> bỏ")
            off[col] = None

    return code, level, uni, off


def check_major_links(major_names, offering_majors, school_keys, warnings):
    """Ngành của trường phải có trong sheet Majors và trường phải có trong Import SQL."""
    for key, links in offering_majors.items():
        if key not in school_keys:
            warnings.append(f"{key[0]} ({key[1]}): có ngành nhưng không có trong Import SQL -> bỏ")
        for major_name, _, _ in links:
            if major_name not in major_names:
                raise ValueError(f"{key[0]}: ngành '{major_name}' không có trong sheet Majors")


def write_to_db(conn, majors, schools, offering_majors, stats):
    with conn.cursor() as cur:
        major_ids = {}
        for name, cip_code in majors:
            cur.execute(
                "INSERT INTO app.majors (id, name, cip_code, created_at) "
                "VALUES (gen_random_uuid(), %s, %s, now()) "
                "ON CONFLICT (name) DO UPDATE SET cip_code = EXCLUDED.cip_code, updated_at = now() "
                "RETURNING id",
                (name, cip_code),
            )
            major_ids[name] = cur.fetchone()[0]
        stats["ngành"] = len(major_ids)

        uni_cols = ["code", *UNIVERSITY_COLS, "is_ivy", "is_active"]
        uni_updates = ", ".join(f"{c} = EXCLUDED.{c}" for c in uni_cols if c != "code")
        off_updates = ", ".join(f"{c} = EXCLUDED.{c}" for c in ALL_OFFERING_COLS)
        off_placeholders = []
        for col in ALL_OFFERING_COLS:
            if col == "deadlines_json":
                off_placeholders.append("%s::jsonb")
            else:
                off_placeholders.append("%s")

        for code, level, uni, off in schools:
            cur.execute(
                f"INSERT INTO app.universities (id, {', '.join(uni_cols)}, created_at) "
                f"VALUES (gen_random_uuid(), {', '.join(['%s'] * len(uni_cols))}, now()) "
                f"ON CONFLICT (code) DO UPDATE SET {uni_updates}, updated_at = now() "
                "RETURNING id, (xmax = 0)",
                [code, *[uni[c] for c in UNIVERSITY_COLS], uni["is_ivy"], uni["is_active"]],
            )
            uni_id, uni_inserted = cur.fetchone()
            stats["trường: thêm mới" if uni_inserted else "trường: cập nhật"] += 1

            cur.execute(
                f"INSERT INTO app.university_offerings (id, university_id, study_level, {', '.join(ALL_OFFERING_COLS)}, created_at) "
                f"VALUES (gen_random_uuid(), %s, %s, {', '.join(off_placeholders)}, now()) "
                f"ON CONFLICT (university_id, study_level) DO UPDATE SET {off_updates}, updated_at = now() "
                "RETURNING id",
                [uni_id, level, *[off[c] for c in ALL_OFFERING_COLS]],
            )
            offering_id = cur.fetchone()[0]

            cur.execute(
                "DELETE FROM app.university_offering_majors WHERE university_offering_id = %s",
                (offering_id,),
            )
            for major_name, is_stem, is_verified in offering_majors.get((code, level), []):
                cur.execute(
                    "INSERT INTO app.university_offering_majors (university_offering_id, major_id, is_stem, is_verified) "
                    "VALUES (%s, %s, %s, %s)",
                    (offering_id, major_ids[major_name], is_stem, is_verified),
                )
                stats["liên kết ngành"] += 1


def run(path, db_url, apply, codes=None):
    wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
    facts = read_facts(wb)
    majors = read_majors(wb)
    offering_majors = read_offering_majors(wb)

    warnings = []
    schools = []
    for row in read_sheet(wb, "Import SQL"):
        code = str(row["code"]).strip()
        if codes and code not in codes:
            continue
        schools.append(build_school(row, facts, warnings))

    school_keys = set()
    for code, level, _, _ in schools:
        school_keys.add((code, level))
    if not codes:
        major_names = set()
        for name, _ in majors:
            major_names.add(name)
        check_major_links(major_names, offering_majors, school_keys, warnings)

    print(f"Đọc {os.path.basename(path)}: {len(schools)} trường, {len(majors)} ngành, {len(facts)} fact dùng được")
    filled = Counter()
    for _, level, _, off in schools:
        for col in ("act25", "c7_gpa_importance", "gpa_pct_4_0", "intl_aid_policy", "retention_rate_1yr"):
            if off[col] is not None:
                filled[col] += 1
    without_majors = 0
    for key in school_keys:
        if not offering_majors.get(key):
            without_majors += 1
    print(f"  có ACT: {filled['act25']}, có C7: {filled['c7_gpa_importance']}, "
          f"có phân phối GPA: {filled['gpa_pct_4_0']}, có chính sách aid: {filled['intl_aid_policy']}, "
          f"có retention: {filled['retention_rate_1yr']}, chưa có ngành: {without_majors}")
    if warnings:
        print(f"  {len(warnings)} cảnh báo:")
        for warning in warnings:
            print(f"    - {warning}")

    if not apply:
        print("Chạy thử: chưa ghi gì. Thêm --apply để ghi.")
        return

    import psycopg

    stats = Counter()
    with psycopg.connect(db_url) as conn:
        write_to_db(conn, majors, schools, offering_majors, stats)
        conn.commit()
    for key, n in sorted(stats.items()):
        print(f"  {key}: {n}")
    print("Đã ghi vào DB.")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("xlsx", help="workbook usdss_dataset_*.xlsx")
    parser.add_argument(
        "--apply", action="store_true", help="ghi vào DB (mặc định chỉ chạy thử)"
    )
    parser.add_argument(
        "--codes",
        default="",
        help="chỉ nhập các mã trường này, cách nhau bằng dấu phẩy",
    )
    args = parser.parse_args()
    codes = {c.strip() for c in args.codes.split(",") if c.strip()}
    run(args.xlsx, os.environ.get("DATABASE_URL", DEFAULT_DB), args.apply, codes)
    sys.exit(0)
