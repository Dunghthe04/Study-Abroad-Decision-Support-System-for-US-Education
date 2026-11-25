"""Nhập bộ dữ liệu trường (workbook usdss_dataset_*.xlsx, cấu trúc dataset v3) vào Postgres hoặc xuất SQL migration.

Đọc các sheet trong workbook usdss_dataset_*.xlsx:
  - Roster                     : danh sách trường được chọn (included = YES)
  - Facts                      : giá trị từng trường/cột (chỉ lấy is_primary = Y, status verified/derived/human_verified;
                                 tính lại công thức derived; xóa cột có unverified_reason audit_fail/sanity_fail)
  - Sources                    : metadata nguồn (URL, ngày truy cập, bậc tin cậy T1–T9)
  - Majors                     : danh mục nhóm ngành CIP 2020
  - University_Offering_Majors : liên kết ngành của bậc đại học (CDS_J1 verified + SCORECARD_PCIP)
  - Scholarships               : học bổng verified gắn với trường, loại trừ external và bậc không hỗ trợ undergraduate
  - State_Living_Cost          : chỉ số giá sinh hoạt theo bang BEA SARPP (UC-28)
  - Reference_Fees             : phí visa F-1 MRV và SEVIS I-901 (UC-26)
  - Media                      : ảnh trường + ghi công tác giả/giấy phép/trang nguồn Commons (UC-21)

Quy tắc bắt buộc:
  - UC-103: KHÔNG ghi đè is_active và KHÔNG ghi đè dòng đã có Moderator chỉnh sửa (last_edited_by_user_id IS NOT NULL).
  - #14: dọn học bổng vi phạm đặc tả (university_id IS NULL, kind = 'external', study_level <> 'undergraduate')
         và dòng tự nạp cũ bị bản mới thay thế; giữ nguyên dòng hợp lệ do Moderator sửa/tạo.
  - #16: nạp field_evidence sạch (source_url, source_section, extracted_quote JSON kèm retrieved_at/period_year) cho UC-21.
  - Idempotent: chạy nhiều lần cho kết quả giống nhau.

Ví dụ:
    python scripts\import_school_dataset.py "usdss_dataset_1536_241126.xlsx"
    python scripts\import_school_dataset.py "usdss_dataset_1536_241126.xlsx" --emit-sql out.sql --tag 241126
    python scripts\import_school_dataset.py "usdss_dataset_1536_241126.xlsx" --apply
"""

import argparse
import ast
import datetime
import json
import os
import re
import sys
import uuid
from collections import Counter

import openpyxl

DEFAULT_DB = "postgresql://studyabroad:studyabroad@localhost:5432/studyabroad"
UUID_NS = uuid.uuid5(uuid.NAMESPACE_URL, "https://usdss.studyabroad.local/dataset")
USABLE_STATUSES = ("verified", "derived", "human_verified")
CLEAR_REASONS = ("audit_fail", "sanity_fail")
ADMISSION_CYCLE = "2027"

RELIABILITY_BY_TIER = {
    "T1": "official",
    "T2": "official",
    "T3": "official",
    "T4": "aggregated",
    "T9": "self_reported",
}

COVERAGE_MAP = {
    "full_cost": "full",
    "full": "full",
    "tuition_fees": "tuition",
    "tuition": "tuition",
    "partial": "partial",
    "stipend": "stipend",
}

VALID_SCHOLARSHIP_KINDS = {"merit", "need", "merit_and_need", "athletic", "program", "other"}
VALID_AMOUNT_TYPES = {"fixed", "range", "percent_tuition", "full_tuition", "full_ride", "varies"}
VALID_APP_METHODS = {"automatic", "separate_application", "nomination", "unknown"}
VALID_GPA_SCALES = {"weighted", "unweighted", "both"}

# (col_name, type_spec) — exact PostgreSQL column types from AppDbContextModelSnapshot
UNI_COL_TYPES = [
    ("name", "varchar(256)"),
    ("city", "varchar(128)"),
    ("state", "varchar(2)"),
    ("website", "varchar(512)"),
    ("control", "varchar(16)"),
    ("acceptance_rate", "numeric(5,4)"),
    ("international_student_count", "integer"),
    ("is_ivy", "boolean"),
]

OFF_FACT_COL_TYPES = [
    ("tuition_usd", "numeric(12,2)"),
    ("fees_usd", "numeric(12,2)"),
    ("living_usd", "numeric(12,2)"),
    ("other_expenses_usd", "numeric(12,2)"),
    ("health_insurance_usd", "numeric(12,2)"),
    ("application_fee_usd", "numeric(12,2)"),
    ("act_policy", "varchar(16)"),
    ("act_submit_pct", "numeric(5,4)"),
    ("tuition_in_state_usd", "numeric(12,2)"),
    ("min_gpa4", "numeric(3,2)"),
    ("min_ielts", "numeric(3,1)"),
    ("min_toefl", "numeric(5,1)"),
    ("min_duolingo", "numeric(5,1)"),
    ("avg_gpa4", "numeric(3,2)"),
    ("avg_gpa_weighted", "numeric(3,2)"),
    ("gpa_scale_type", "varchar(16)"),
    ("sat25", "integer"),
    ("sat75", "integer"),
    ("act25", "integer"),
    ("act75", "integer"),
    ("sat_policy", "varchar(16)"),
    ("sat_submit_pct", "numeric(5,4)"),
    ("gpa_submit_pct", "numeric(5,4)"),
    ("gpa_pct_4_0", "numeric(5,4)"),
    ("gpa_pct_375_399", "numeric(5,4)"),
    ("gpa_pct_350_374", "numeric(5,4)"),
    ("gpa_pct_325_349", "numeric(5,4)"),
    ("gpa_pct_300_324", "numeric(5,4)"),
    ("gpa_pct_250_299", "numeric(5,4)"),
    ("gpa_pct_below_250", "numeric(5,4)"),
    ("c7_gpa_importance", "varchar(16)"),
    ("c7_test_importance", "varchar(16)"),
    ("c7_ec_importance", "varchar(16)"),
    ("applicants_total", "integer"),
    ("admitted_total", "integer"),
    ("intl_applicants", "integer"),
    ("intl_admitted", "integer"),
    ("intl_acceptance_rate", "numeric(5,4)"),
    ("intl_aid_policy", "varchar(16)"),
    ("intl_aid_avg_usd", "numeric(12,2)"),
    ("intl_aid_recipients", "integer"),
    ("retention_rate_1yr", "numeric(5,4)"),
    ("grad_rate_6yr", "numeric(5,4)"),
    ("fy_on_campus_pct", "numeric(5,4)"),
    ("ug_on_campus_pct", "numeric(5,4)"),
    ("housing_types", "varchar(1000)"),
    ("intl_housing", "boolean"),
    ("housing_only_usd", "numeric(12,2)"),
    ("housing_deposit_usd", "numeric(12,2)"),
    ("student_faculty_ratio", "integer"),
    ("activities", "varchar(2000)"),
    ("campus_setting", "varchar(64)"),
]

OFF_ALL_COLS = (
    ["academic_year"]
    + [c for c, _ in OFF_FACT_COL_TYPES]
    + ["deadlines_json", "source_url", "retrieved_at", "source_reliability"]
)

RATE_COLS = {
    "acceptance_rate",
    "intl_acceptance_rate",
    "sat_submit_pct",
    "act_submit_pct",
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
    "fy_on_campus_pct",
    "ug_on_campus_pct",
}

EVIDENCE_ONLY_FIELDS = [
    "coa_total_usd",
    "vn_aid_scope",
    "deadline_ea",
    "deadline_ed",
    "deadline_rd",
    "majors",
    "stem_majors",
]

SCHOLARSHIP_COLS = [
    "name",
    "provider",
    "study_level",
    "amount_usd",
    "coverage_type",
    "min_gpa4",
    "min_ielts",
    "deadline",
    "eligibility_notes",
    "source_url",
    "retrieved_at",
    "source_reliability",
    "kind",
    "amount_type",
    "amount_min_usd",
    "amount_max_usd",
    "duration_years",
    "renewable",
    "renewal_criteria",
    "intl_eligible",
    "vn_eligible",
    "need_based",
    "merit_based",
    "min_sat",
    "min_act",
    "min_toefl",
    "min_duolingo",
    "major_restriction",
    "application_method",
    "apply_url",
]


def safe_eval_arith(expr):
    """Evaluate simple arithmetic / ROUND / SUM formulas via ast without eval()."""
    try:
        node = ast.parse(expr, mode="eval").body
    except SyntaxError:
        return None
    ops = {
        ast.Add: lambda a, b: a + b,
        ast.Sub: lambda a, b: a - b,
        ast.Mult: lambda a, b: a * b,
        ast.Div: lambda a, b: a / b,
    }
    def _walk(n):
        if isinstance(n, ast.Constant) and isinstance(n.value, (int, float)):
            return n.value
        if isinstance(n, ast.UnaryOp) and isinstance(n.op, (ast.UAdd, ast.USub)):
            v = _walk(n.operand)
            return v if isinstance(n.op, ast.UAdd) else -v
        if isinstance(n, ast.BinOp) and type(n.op) in ops:
            return ops[type(n.op)](_walk(n.left), _walk(n.right))
        if isinstance(n, ast.Call) and isinstance(n.func, ast.Name):
            args = [_walk(a) for a in n.args]
            if n.func.id == "ROUND" and len(args) in (1, 2):
                return round(args[0], int(args[1])) if len(args) == 2 else round(args[0])
            if n.func.id == "SUM":
                return sum(args)
        raise ValueError("unsupported")
    try:
        return _walk(node)
    except Exception:
        return None


def det_uuid(kind, key):
    return str(uuid.uuid5(UUID_NS, f"{kind}:{key}"))


def to_text(value):
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def to_num(value):
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, (int, float)):
        return float(value)
    s = str(value).replace(",", "").replace("$", "").strip()
    if not s:
        return None
    try:
        return float(s)
    except ValueError:
        return None


def to_bool(value):
    if value is None:
        return None
    if isinstance(value, bool):
        return value
    s = str(value).strip().upper()
    if s in ("TRUE", "YES", "Y", "1"):
        return True
    if s in ("FALSE", "NO", "N", "0"):
        return False
    return None


def to_date_str(value):
    if value is None:
        return None
    if isinstance(value, datetime.datetime):
        return value.date().isoformat()
    if isinstance(value, datetime.date):
        return value.isoformat()
    s = str(value).strip()[:10]
    return s if re.fullmatch(r"\d{4}-\d{2}-\d{2}", s) else None


def short_academic_year(value):
    text = to_text(value)
    if not text:
        return None
    m = re.match(r"^(\d{4})\s*[-–/]\s*(\d{2,4})$", text)
    if not m:
        return None
    return f"{m.group(1)}-{m.group(2)[-2:]}"


def typed(who, col, type_spec, value, warnings, truncate=False):
    if value is None or value == "":
        return None
    m = re.match(r"numeric\((\d+),(\d+)\)", type_spec)
    if m or type_spec in ("integer", "smallint"):
        x = to_num(value)
        if x is None:
            warnings.append(f"{who}.{col}: not a number ({value!r}) -> skip")
            return None
        if type_spec in ("integer", "smallint"):
            return int(round(x))
        p, s = map(int, m.groups())
        if abs(x) >= 10 ** (p - s):
            warnings.append(f"{who}.{col}: {x} out of {type_spec} -> skip")
            return None
        return round(x, s)
    if type_spec == "boolean":
        return to_bool(value)
    if type_spec == "date":
        d = to_date_str(value)
        if d is None:
            warnings.append(f"{who}.{col}: invalid date ({value!r}) -> skip")
        return d
    text = to_text(value)
    if text is None:
        return None
    m = re.match(r"varchar\((\d+)\)", type_spec)
    if m:
        limit = int(m.group(1))
        if len(text) > limit:
            if truncate:
                return text[:limit]
            warnings.append(f"{who}.{col}: len {len(text)} > {limit} -> skip")
            return None
    return text


def sql_lit(value, cast=None):
    if value is None:
        return "NULL"
    if isinstance(value, bool):
        lit = "TRUE" if value else "FALSE"
    elif isinstance(value, (int, float)):
        lit = repr(value)
    else:
        lit = "'" + str(value).replace("'", "''") + "'"
    return f"{lit}::{cast}" if cast else lit


def read_sheet_dicts(wb, name):
    if name not in wb.sheetnames:
        return []
    ws = wb[name]
    rows = ws.iter_rows(values_only=True)
    header = next(rows, None)
    if not header:
        return []
    headers = [str(h) if h is not None else "" for h in header]
    result = []
    for raw in rows:
        if any(v is not None for v in raw):
            result.append(dict(zip(headers, raw)))
    return result


def read_sources(wb):
    return {
        to_text(r.get("source_id")): r
        for r in read_sheet_dicts(wb, "Sources")
        if to_text(r.get("source_id"))
    }


def read_roster(wb):
    roster = {}
    for r in read_sheet_dicts(wb, "Roster"):
        code = to_text(r.get("school_code"))
        if code and to_bool(r.get("included")):
            roster[code] = to_text(r.get("name")) or code
    return roster


def read_facts(wb_formulas):
    """Read Facts sheet (with formulas intact) and evaluate all primary usable values."""
    ws = wb_formulas["Facts"]
    it = ws.iter_rows(values_only=True)
    hdr = [str(h) if h is not None else "" for h in next(it)]
    idx = {h: i for i, h in enumerate(hdr)}

    row_raw = {}  # excel row_no -> dict
    primary_usable = {}  # (code, field_key) -> row_no
    cleared_keys = set()  # (code, field_key) whose primary row failed audit/sanity

    for rn, r in enumerate(it, start=2):
        if not any(v is not None for v in r):
            continue
        code = to_text(r[idx["school_code"]])
        fk = to_text(r[idx["field_key"]])
        if not code or not fk:
            continue
        status = to_text(r[idx["status"]]) or ""
        is_primary = (to_text(r[idx["is_primary"]]) or "").upper() == "Y"
        reason = to_text(r[idx["unverified_reason"]]) or ""
        rec = {
            "row_no": rn,
            "fact_id": to_text(r[idx["fact_id"]]),
            "school_code": code,
            "field_key": fk,
            "source_id": to_text(r[idx["source_id"]]),
            "value": r[idx["value"]],
            "locator": to_text(r[idx["locator"]]),
            "quote": to_text(r[idx["quote (nguyên văn)"]]),
            "period_year": to_text(r[idx["period_year"]]),
            "second_read_by": to_text(r[idx["second_read_by"]]),
            "checked_by": to_text(r[idx["checked_by"]]),
            "status": status,
            "is_primary": is_primary,
        }
        row_raw[rn] = rec
        if is_primary:
            if status in USABLE_STATUSES:
                primary_usable[(code, fk)] = rn
            elif reason in CLEAR_REASONS:
                cleared_keys.add((code, fk))

    cache = {}

    def eval_row(rn, depth=0):
        if rn in cache:
            return cache[rn]
        if depth > 8 or rn not in row_raw:
            return None
        rec = row_raw[rn]
        v = rec["value"]
        if v is None or v == "":
            return None
        if not isinstance(v, str):
            res = str(v)
            cache[rn] = res
            return res
        s = v.strip()
        if s.startswith("'"):
            s = s[1:]
        if not s.startswith("="):
            cache[rn] = s
            return s
        if s.startswith("=IF({intl_aid_policy}"):
            labels = dict(re.findall(r'\{intl_aid_policy\}="(\w+)","([^"]+)"', s))
            m_def = re.search(r',"([^"]+)"\)+$', s)
            trn = primary_usable.get((rec["school_code"], "intl_aid_policy"))
            pol = eval_row(trn, depth + 1) if trn else ""
            res = labels.get(pol or "", m_def.group(1) if m_def else None)
            cache[rn] = res
            return res
        expr = s[1:]

        def repl_cell(m):
            val = eval_row(int(m.group(1)), depth + 1)
            x = to_num(val)
            return repr(x) if x is not None else "BAD"

        def repl_key(m):
            trn = primary_usable.get((rec["school_code"], m.group(1)))
            val = eval_row(trn, depth + 1) if trn else None
            x = to_num(val)
            return repr(x) if x is not None else "BAD"

        expr = re.sub(r"\bF(\d+)\b", repl_cell, expr)
        expr = re.sub(r"\{(\w+)\}", repl_key, expr)
        if "BAD" in expr:
            return None
        val = safe_eval_arith(expr)
        if val is None:
            return None
        res = str(val)
        cache[rn] = res
        return res

    usable = {}  # (code, field_key) -> (evaluated_str, rec)
    for key, rn in primary_usable.items():
        val = eval_row(rn)
        if val is not None and val != "":
            usable[key] = (val, row_raw[rn])
    return usable, cleared_keys


def load_dataset(path, codes_filter=None, tag="241126"):
    wb_val = openpyxl.load_workbook(path, read_only=True, data_only=True)
    wb_f = openpyxl.load_workbook(path, read_only=True, data_only=False)

    roster = read_roster(wb_val)
    sources = read_sources(wb_val)
    usable_facts, cleared_keys = read_facts(wb_f)

    all_codes = sorted(roster.keys())
    if codes_filter:
        all_codes = [c for c in all_codes if c in codes_filter]
    code_set = set(all_codes)

    warnings = []
    schools = []  # (code, insert_uni_dict, overlay_uni_dict, overlay_off_dict)
    evidences = []  # list of dicts

    # Pre-index fallback source per school
    school_fallback_src = {}
    for sid, srow in sources.items():
        sc = to_text(srow.get("scope"))
        if sc in code_set and sc not in school_fallback_src and to_text(srow.get("url")):
            school_fallback_src[sc] = srow

    for code in all_codes:
        u_overlay = {}
        for col, tspec in UNI_COL_TYPES:
            if (code, col) in usable_facts:
                raw_val, _ = usable_facts[(code, col)]
                val = typed(code, col, tspec, raw_val, warnings)
                if col in RATE_COLS and val is not None and not 0 <= val <= 1:
                    warnings.append(f"{code}.{col}={val} outside 0..1 -> skip")
                    val = None
                if val is not None:
                    u_overlay[col] = val
            elif (code, col) in cleared_keys and col not in ("name", "is_ivy"):
                u_overlay[col] = None

        u_insert = {
            "id": det_uuid("university", code),
            "code": code,
            "name": u_overlay.get("name") or roster[code],
            "city": u_overlay.get("city"),
            "state": u_overlay.get("state"),
            "website": u_overlay.get("website"),
            "control": u_overlay.get("control"),
            "acceptance_rate": u_overlay.get("acceptance_rate"),
            "international_student_count": u_overlay.get("international_student_count"),
            "is_ivy": bool(u_overlay.get("is_ivy", False)),
            "is_active": True,
        }

        o_overlay = {}
        if (code, "cost_year") in usable_facts:
            ay = short_academic_year(usable_facts[(code, "cost_year")][0])
            if ay:
                o_overlay["academic_year"] = ay

        for col, tspec in OFF_FACT_COL_TYPES:
            if (code, col) in usable_facts:
                raw_val, _ = usable_facts[(code, col)]
                val = typed(code, col, tspec, raw_val, warnings)
                if col in RATE_COLS and val is not None and not 0 <= val <= 1:
                    warnings.append(f"{code}.{col}={val} outside 0..1 -> skip")
                    val = None
                if col in ("avg_gpa4", "min_gpa4") and val is not None and not 0 <= val <= 4:
                    warnings.append(f"{code}.{col}={val} outside 0..4 -> skip")
                    val = None
                if col == "avg_gpa_weighted" and val is not None and val < 0:
                    warnings.append(f"{code}.{col}={val} < 0 -> skip")
                    val = None
                if col == "gpa_scale_type" and val not in VALID_GPA_SCALES:
                    warnings.append(f"{code}.{col}={val!r} invalid -> skip")
                    val = None
                if val is not None:
                    o_overlay[col] = val
            elif (code, col) in cleared_keys:
                o_overlay[col] = None

        # Check constraints on SAT & ACT ranges
        for lo, hi, a, b in ((400, 1600, "sat25", "sat75"), (1, 36, "act25", "act75")):
            va, vb = o_overlay.get(a), o_overlay.get(b)
            if va is not None and not lo <= va <= hi:
                warnings.append(f"{code}.{a}={va} outside {lo}..{hi} -> skip")
                o_overlay.pop(a, None)
                va = None
            if vb is not None and not lo <= vb <= hi:
                warnings.append(f"{code}.{b}={vb} outside {lo}..{hi} -> skip")
                o_overlay.pop(b, None)
                vb = None
            if va is not None and vb is not None and va > vb:
                warnings.append(f"{code}: {a} ({va}) > {b} ({vb}) -> skip both")
                o_overlay.pop(a, None)
                o_overlay.pop(b, None)

        deadlines = {}
        for fk, dtag in (("deadline_ea", "EA"), ("deadline_ed", "ED"), ("deadline_rd", "RD")):
            if (code, fk) in usable_facts:
                dval = to_date_str(usable_facts[(code, fk)][0])
                if dval:
                    key = "RollingFinal" if dtag == "RD" and dval[5:] > "06-30" and dval[:4] == ADMISSION_CYCLE else dtag
                    deadlines[key] = dval
        if deadlines:
            o_overlay["deadlines_json"] = deadlines

        cds = sources.get(f"S{code}-CDS") or school_fallback_src.get(code) or {}
        src_url = typed(code, "source_url", "varchar(512)", cds.get("url"), warnings)
        src_date = to_date_str(cds.get("accessed_at"))
        src_rel = RELIABILITY_BY_TIER.get(to_text(cds.get("tier")) or "")
        if src_url:
            o_overlay["source_url"] = src_url
        if src_date:
            o_overlay["retrieved_at"] = src_date
        if src_rel:
            o_overlay["source_reliability"] = src_rel

        schools.append((code, u_insert, u_overlay, o_overlay))

        # Field-level provenance (UC-21 / #16)
        ev_fields = (
            [(col, col, u_overlay.get(col)) for col, _ in UNI_COL_TYPES]
            + [("academic_year", "cost_year", o_overlay.get("academic_year"))]
            + [(col, col, o_overlay.get(col)) for col, _ in OFF_FACT_COL_TYPES]
            + [(col, col, usable_facts[(code, col)][0] if (code, col) in usable_facts else None) for col in EVIDENCE_ONLY_FIELDS]
        )
        for field_name, fact_key, field_val in ev_fields:
            if field_val is None or (code, fact_key) not in usable_facts:
                continue
            _, frec = usable_facts[(code, fact_key)]
            srow = sources.get(frec["source_id"]) or {}
            ev_url = typed(frec["fact_id"] or code, "source_url", "varchar(512)", srow.get("url"), warnings)
            meta = {
                "quote": frec["quote"],
                "retrieved_at": to_date_str(srow.get("accessed_at")),
                "period_year": frec["period_year"],
                "applied_to_snapshot": True,
            }
            if frec["checked_by"] and frec["second_read_by"]:
                vby = f"{frec['checked_by']} + {frec['second_read_by']}"
            else:
                vby = frec["checked_by"] or f"usdss dataset {tag}"
            evidences.append(
                {
                    "id": det_uuid(f"evidence:{tag}", f"{code}:{field_name}"),
                    "code": code,
                    "field_name": field_name[:64],
                    "field_value": str(field_val),
                    "source_url": ev_url,
                    "source_section": (frec["locator"] or "")[:64] or None,
                    "extracted_quote": json.dumps(meta, ensure_ascii=False),
                    "verified_by": vby[:128],
                    "status": frec["status"][:32],
                }
            )

    # Majors & University_Offering_Majors
    majors = []
    for r in read_sheet_dicts(wb_val, "Majors"):
        name = to_text(r.get("name"))
        cip = to_text(r.get("cip_code"))
        if name:
            majors.append((det_uuid("major", name), name, cip))

    major_links = []
    seen_links = set()
    for r in read_sheet_dicts(wb_val, "University_Offering_Majors"):
        code = to_text(r.get("school_code (→ university_offering)"))
        level = (to_text(r.get("study_level")) or "undergraduate").lower()
        mname = to_text(r.get("major_name (→ majors.name)"))
        if not code or code not in code_set or level != "undergraduate" or not mname:
            continue
        if (code, mname) in seen_links:
            continue
        seen_links.add((code, mname))
        major_links.append((code, mname, bool(to_bool(r.get("is_stem"))), bool(to_bool(r.get("is_verified")))))

    # Scholarships (UC-103 + #14): verified, attached to a university, non-external, undergraduate-eligible
    scholarships = []
    skipped_sch = Counter()
    for r in read_sheet_dicts(wb_val, "Scholarships"):
        status = to_text(r.get("status")) or ""
        if status not in USABLE_STATUSES:
            skipped_sch["not_usable"] += 1
            continue
        code = to_text(r.get("school_code"))
        if not code or code not in code_set:
            skipped_sch["no_university"] += 1
            continue
        kind = (to_text(r.get("kind")) or "").lower() or None
        if kind == "external":
            skipped_sch["external"] += 1
            continue
        if kind is not None and kind not in VALID_SCHOLARSHIP_KINDS:
            warnings.append(f"scholarship {r.get('scholarship_id')}: invalid kind {kind!r} -> other")
            kind = "other"
        raw_level = (to_text(r.get("study_level")) or "").lower()
        if raw_level not in ("undergraduate", "both"):
            skipped_sch["non_undergraduate"] += 1
            continue

        sid = to_text(r.get("scholarship_id")) or f"{code}:{r.get('name')}"
        name = typed(sid, "name", "varchar(256)", r.get("name"), warnings, truncate=True)
        if not name:
            skipped_sch["no_name"] += 1
            continue
        src = sources.get(to_text(r.get("source_id"))) or {}

        amount_type = (to_text(r.get("amount_type")) or "").lower() or None
        if amount_type not in VALID_AMOUNT_TYPES:
            amount_type = None
        amount_min = typed(sid, "amount_min_usd", "numeric(12,2)", r.get("amount_min_usd"), warnings)
        amount_max = typed(sid, "amount_max_usd", "numeric(12,2)", r.get("amount_max_usd"), warnings)
        if amount_min is not None and amount_max is not None and amount_min > amount_max:
            warnings.append(f"{sid}: amount_min_usd > amount_max_usd -> swap/skip")
            amount_min = amount_max = None
        raw_amount = r.get("amount_usd")
        if raw_amount in (None, "") and amount_type == "range":
            raw_amount = amount_max
        amount_usd = typed(sid, "amount_usd", "numeric(12,2)", raw_amount, warnings)

        min_gpa4 = typed(sid, "min_gpa4", "numeric(3,2)", r.get("min_gpa4"), warnings)
        if min_gpa4 is not None and not 0 <= min_gpa4 <= 4:
            min_gpa4 = None
        min_ielts = typed(sid, "min_ielts", "numeric(3,1)", r.get("min_ielts"), warnings)
        if min_ielts is not None and not 0 <= min_ielts <= 9:
            min_ielts = None
        min_toefl = typed(sid, "min_toefl", "numeric(5,1)", r.get("min_toefl"), warnings)
        if min_toefl is not None and not 0 <= min_toefl <= 120:
            min_toefl = None
        min_duolingo = typed(sid, "min_duolingo", "numeric(5,1)", r.get("min_duolingo"), warnings)
        if min_duolingo is not None and not 10 <= min_duolingo <= 160:
            min_duolingo = None
        min_sat = typed(sid, "min_sat", "integer", r.get("min_sat"), warnings)
        if min_sat is not None and not 400 <= min_sat <= 1600:
            min_sat = None
        min_act = typed(sid, "min_act", "integer", r.get("min_act"), warnings)
        if min_act is not None and not 1 <= min_act <= 36:
            min_act = None
        duration_years = typed(sid, "duration_years", "integer", r.get("duration_years"), warnings)
        if duration_years is not None and not 1 <= duration_years <= 8:
            duration_years = None

        app_method = (to_text(r.get("application_method")) or "").lower() or None
        if app_method not in VALID_APP_METHODS:
            app_method = None

        scholarships.append(
            {
                "id": det_uuid("scholarship", sid),
                "code": code,
                "name": name,
                "provider": typed(sid, "provider", "varchar(256)", r.get("provider"), warnings, truncate=True),
                "study_level": "undergraduate",
                "amount_usd": amount_usd,
                "coverage_type": COVERAGE_MAP.get((to_text(r.get("coverage_type")) or "").lower()),
                "min_gpa4": min_gpa4,
                "min_ielts": min_ielts,
                "deadline": to_date_str(r.get("deadline")),
                "eligibility_notes": typed(sid, "eligibility_notes", "varchar(2000)", r.get("eligibility_text"), warnings, truncate=True),
                "source_url": typed(sid, "source_url", "varchar(512)", r.get("apply_url") or src.get("url"), warnings),
                "retrieved_at": to_date_str(src.get("accessed_at")) or to_date_str(r.get("checked_at")),
                "source_reliability": RELIABILITY_BY_TIER.get(to_text(src.get("tier")) or "", "official"),
                "is_active": to_bool(r.get("is_active")) is not False,
                "kind": kind,
                "amount_type": amount_type,
                "amount_min_usd": amount_min,
                "amount_max_usd": amount_max,
                "duration_years": duration_years,
                "renewable": to_bool(r.get("renewable")),
                "renewal_criteria": typed(sid, "renewal_criteria", "varchar(1000)", r.get("renewal_criteria"), warnings, truncate=True),
                "intl_eligible": to_bool(r.get("intl_eligible")),
                "vn_eligible": to_bool(r.get("vn_eligible")),
                "need_based": to_bool(r.get("need_based")),
                "merit_based": to_bool(r.get("merit_based")),
                "min_sat": min_sat,
                "min_act": min_act,
                "min_toefl": min_toefl,
                "min_duolingo": min_duolingo,
                "major_restriction": typed(sid, "major_restriction", "varchar(500)", r.get("major_restriction"), warnings, truncate=True),
                "application_method": app_method,
                "apply_url": typed(sid, "apply_url", "varchar(512)", r.get("apply_url"), warnings),
            }
        )

    # State living costs (BEA SARPP, UC-28)
    state_living_costs = []
    for r in read_sheet_dicts(wb_val, "State_Living_Cost"):
        sc = to_text(r.get("state_code"))
        yr = typed("state_living_cost", "year", "smallint", r.get("year"), warnings)
        all_items = typed(sc or "slc", "index_all_items", "numeric(6,2)", r.get("index_all_items"), warnings)
        src_url = typed(sc or "slc", "source_url", "varchar(512)", r.get("source_url"), warnings)
        ret_at = to_date_str(r.get("retrieved_at"))
        if sc and yr and all_items is not None and src_url and ret_at:
            state_living_costs.append(
                {
                    "id": det_uuid("state_living_cost", f"{sc}:{yr}"),
                    "state_code": sc,
                    "year": yr,
                    "index_all_items": all_items,
                    "index_housing_rents": typed(sc, "index_housing_rents", "numeric(6,2)", r.get("index_housing_rents"), warnings),
                    "index_goods": typed(sc, "index_goods", "numeric(6,2)", r.get("index_goods"), warnings),
                    "index_other_services": typed(sc, "index_other_services", "numeric(6,2)", r.get("index_other_services"), warnings),
                    "source_url": src_url,
                    "retrieved_at": ret_at,
                }
            )

    # Reference fees (F-1 MRV + SEVIS I-901, UC-26)
    ref_fees_rows = {to_text(r.get("fee_key")): r for r in read_sheet_dicts(wb_val, "Reference_Fees")}
    ref_fees = None
    visa_row = ref_fees_rows.get("visa_mrv_f1")
    sevis_row = ref_fees_rows.get("sevis_i901_f1")
    if visa_row and sevis_row:
        visa_amt = to_num(visa_row.get("amount_usd"))
        sevis_amt = to_num(sevis_row.get("amount_usd"))
        if visa_amt is not None and sevis_amt is not None:
            ret_dates = [d for d in (to_date_str(visa_row.get("retrieved_at")), to_date_str(sevis_row.get("retrieved_at"))) if d]
            ref_fees = {
                "id": det_uuid("app_setting", "cost.reference_fees"),
                "key": "cost.reference_fees",
                "value_json": json.dumps(
                    {
                        "visaFeeUsd": int(round(visa_amt)),
                        "visaSourceUrl": to_text(visa_row.get("source_url")),
                        "sevisFeeUsd": int(round(sevis_amt)),
                        "sevisSourceUrl": to_text(sevis_row.get("source_url")),
                        "retrievedAt": max(ret_dates) if ret_dates else None,
                    },
                    ensure_ascii=False,
                ),
                "description": f"Phí visa F-1 (MRV) và I-901 SEVIS từ travel.state.gov & ice.gov (đối chiếu {max(ret_dates) if ret_dates else tag}) cho UC-26.",
            }

    # Media attribution (UC-21)
    media_rows = []
    seen_media = set()
    for r in read_sheet_dicts(wb_val, "Media"):
        code = to_text(r.get("school_code"))
        mtype = (to_text(r.get("Loại ảnh")) or "").lower()
        url = typed(code or "media", "url", "varchar(1000)", r.get("URL Cloudinary"), warnings)
        if not code or code not in code_set or not mtype or not url:
            continue
        if (code, url) in seen_media:
            continue
        seen_media.add((code, url))
        media_rows.append(
            {
                "id": det_uuid("media", f"{code}:{url}"),
                "code": code,
                "type": mtype[:32],
                "url": url,
                "display_order": typed(code, "display_order", "integer", r.get("Thứ tự"), warnings) or 0,
                "license": typed(code, "license", "varchar(100)", r.get("Giấy phép bản quyền"), warnings, truncate=True),
                "author": typed(code, "author", "varchar(300)", r.get("Tác giả"), warnings, truncate=True),
                "source_page_url": typed(code, "source_page_url", "varchar(1000)", r.get("Trang nguồn Commons"), warnings, truncate=True),
            }
        )

    return {
        "tag": tag,
        "schools": schools,
        "majors": majors,
        "major_links": major_links,
        "scholarships": scholarships,
        "skipped_sch": skipped_sch,
        "state_living_costs": state_living_costs,
        "ref_fees": ref_fees,
        "media": media_rows,
        "evidences": evidences,
        "warnings": warnings,
    }


def render_sql(data):
    tag = re.sub(r"[^a-zA-Z0-9_]", "", data["tag"])
    schools = data["schools"]
    majors = data["majors"]
    major_links = data["major_links"]
    scholarships = data["scholarships"]
    slc = data["state_living_costs"]
    ref_fees = data["ref_fees"]
    media_rows = data["media"]
    evidences = data["evidences"]

    t_school = f"_refresh_school_{tag}"
    t_majors = f"_refresh_majors_{tag}"
    t_sch = f"_refresh_scholarships_{tag}"
    t_media = f"_refresh_media_{tag}"
    t_ev = f"_refresh_evidence_{tag}"

    lines = [
        f"-- Refresh school dataset {tag} (schools: {len(schools)}, majors: {len(majors)}, major_links: {len(major_links)}, verified scholarships: {len(scholarships)}, BEA state living costs: {len(slc)}, reference fees: {bool(ref_fees)}, media: {len(media_rows)}, field_evidence: {len(evidences)}).",
        "-- UC-103: never overwrites is_active or rows edited by a Moderator (last_edited_by_user_id IS NOT NULL).",
        "-- #14: removes invalid scholarships (university_id IS NULL / kind = 'external' / study_level <> 'undergraduate') and stale auto-imported rows superseded by this dataset.",
        "SET LOCAL standard_conforming_strings = on;",
        "SET LOCAL lock_timeout = '10s';",
        "",
        f"-- 1. Ensure all {len(schools)} schools exist (new rows default to is_active = TRUE; existing rows keep is_active & Moderator edits)",
        "INSERT INTO app.universities (id, code, name, city, state, website, control, acceptance_rate, international_student_count, is_ivy, is_active, created_at) VALUES",
    ]
    uni_ins_rows = []
    for _, u, _, _ in schools:
        uni_ins_rows.append(
            "("
            + ", ".join(
                [
                    sql_lit(u["id"]),
                    sql_lit(u["code"]),
                    sql_lit(u["name"]),
                    sql_lit(u["city"]),
                    sql_lit(u["state"]),
                    sql_lit(u["website"]),
                    sql_lit(u["control"]),
                    sql_lit(u["acceptance_rate"]),
                    sql_lit(u["international_student_count"]),
                    sql_lit(u["is_ivy"]),
                    "TRUE",
                    "CURRENT_TIMESTAMP",
                ]
            )
            + ")"
        )
    lines.append(",\n".join(uni_ins_rows))
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    # Section 2: Universities & undergraduate offerings overlay (skip last_edited_by_user_id IS NOT NULL; never touch is_active)
    lines.append(f"-- 2. Overlay verified {tag} values onto non-Moderator-edited universities and undergraduate university_offerings")
    lines.append(f"CREATE TEMP TABLE {t_school} (code text PRIMARY KEY, university_data jsonb NOT NULL, offering_data jsonb NOT NULL) ON COMMIT DROP;")
    lines.append(f"INSERT INTO {t_school} VALUES")
    st_rows = []
    for code, _, u_ov, o_ov in schools:
        u_json = json.dumps(u_ov, ensure_ascii=False, separators=(",", ":"))
        o_json = json.dumps(o_ov, ensure_ascii=False, separators=(",", ":"))
        st_rows.append(f"({sql_lit(code)}, {sql_lit(u_json, 'jsonb')}, {sql_lit(o_json, 'jsonb')})")
    lines.append(",\n".join(st_rows) + ";")
    lines.append("")

    u_upd_cols = [c for c, _ in UNI_COL_TYPES]
    u_set = ",\n    ".join(f"{c} = (incoming.row_data).{c}" for c in u_upd_cols)
    u_t_row = ", ".join(f"target.{c}" for c in u_upd_cols)
    u_i_row = ", ".join(f"(incoming.row_data).{c}" for c in u_upd_cols)
    lines.append(
        f"""WITH incoming AS (
    SELECT t.id, jsonb_populate_record(NULL::app.universities, to_jsonb(t) || s.university_data) AS row_data
    FROM {t_school} s
    JOIN app.universities t ON t.code = s.code
    WHERE t.last_edited_by_user_id IS NULL
)
UPDATE app.universities target
SET {u_set},
    updated_at = CURRENT_TIMESTAMP
FROM incoming
WHERE target.id = incoming.id
  AND target.last_edited_by_user_id IS NULL
  AND ROW({u_t_row}) IS DISTINCT FROM ROW({u_i_row});"""
    )
    lines.append("")

    lines.append(
        f"""INSERT INTO app.university_offerings (id, university_id, study_level, created_at)
SELECT gen_random_uuid(), u.id, 'undergraduate', CURRENT_TIMESTAMP
FROM {t_school} s
JOIN app.universities u ON u.code = s.code
ON CONFLICT (university_id, study_level) DO NOTHING;"""
    )
    lines.append("")

    o_set = ",\n    ".join(f"{c} = (incoming.row_data).{c}" for c in OFF_ALL_COLS)
    o_t_row = ", ".join(f"target.{c}" for c in OFF_ALL_COLS)
    o_i_row = ", ".join(f"(incoming.row_data).{c}" for c in OFF_ALL_COLS)
    lines.append(
        f"""WITH incoming AS (
    SELECT t.id, jsonb_populate_record(NULL::app.university_offerings, to_jsonb(t) || s.offering_data || CASE WHEN s.offering_data ? 'deadlines_json' THEN jsonb_build_object('deadlines_json', COALESCE(NULLIF(t.deadlines_json, 'null'::jsonb), '{{}}'::jsonb) || (s.offering_data->'deadlines_json')) ELSE '{{}}'::jsonb END) AS row_data
    FROM {t_school} s
    JOIN app.universities u ON u.code = s.code
    JOIN app.university_offerings t ON t.university_id = u.id AND t.study_level = 'undergraduate'
    WHERE t.last_edited_by_user_id IS NULL
)
UPDATE app.university_offerings target
SET {o_set},
    updated_at = CURRENT_TIMESTAMP
FROM incoming
WHERE target.id = incoming.id
  AND target.last_edited_by_user_id IS NULL
  AND ROW({o_t_row}) IS DISTINCT FROM ROW({o_i_row});"""
    )
    lines.append("")

    # Section 3: Majors & offering-major links (sync for non-Moderator-edited offerings)
    lines.append("-- 3. Upsert CIP 2020 majors (keep curated name_vi/aliases/is_popular) & sync offering-major links for non-Moderator-edited offerings")
    lines.append("INSERT INTO app.majors (id, name, cip_code, aliases, is_popular, created_at) VALUES")
    lines.append(
        ",\n".join(
            f"({sql_lit(mid)}, {sql_lit(name)}, {sql_lit(cip)}, '{{}}'::text[], FALSE, CURRENT_TIMESTAMP)"
            for mid, name, cip in majors
        )
    )
    lines.append("ON CONFLICT (name) DO UPDATE SET cip_code = EXCLUDED.cip_code, updated_at = CURRENT_TIMESTAMP WHERE app.majors.cip_code IS DISTINCT FROM EXCLUDED.cip_code;")
    lines.append("")

    lines.append(f"CREATE TEMP TABLE {t_majors} (code text NOT NULL, major_name text NOT NULL, is_stem boolean NOT NULL, is_verified boolean NOT NULL) ON COMMIT DROP;")
    if major_links:
        lines.append(f"INSERT INTO {t_majors} VALUES")
        lines.append(
            ",\n".join(
                f"({sql_lit(c)}, {sql_lit(mn)}, {sql_lit(st)}, {sql_lit(ver)})"
                for c, mn, st, ver in major_links
            )
            + ";"
        )
    lines.append(
        f"""DELETE FROM app.university_offering_majors uom
USING app.university_offerings o, app.universities u
WHERE uom.university_offering_id = o.id
  AND o.university_id = u.id
  AND o.study_level = 'undergraduate'
  AND o.last_edited_by_user_id IS NULL
  AND EXISTS (SELECT 1 FROM {t_school} s WHERE s.code = u.code)
  AND NOT EXISTS (
      SELECT 1
      FROM {t_majors} r
      JOIN app.majors m ON m.name = r.major_name
      WHERE r.code = u.code AND m.id = uom.major_id
  );"""
    )
    lines.append(
        f"""INSERT INTO app.university_offering_majors (university_offering_id, major_id, is_stem, is_verified)
SELECT o.id, m.id, r.is_stem, r.is_verified
FROM {t_majors} r
JOIN app.universities u ON u.code = r.code
JOIN app.university_offerings o ON o.university_id = u.id AND o.study_level = 'undergraduate'
JOIN app.majors m ON m.name = r.major_name
WHERE o.last_edited_by_user_id IS NULL
ON CONFLICT (university_offering_id, major_id) DO UPDATE
SET is_stem = EXCLUDED.is_stem, is_verified = EXCLUDED.is_verified
WHERE app.university_offering_majors.is_stem IS DISTINCT FROM EXCLUDED.is_stem
   OR app.university_offering_majors.is_verified IS DISTINCT FROM EXCLUDED.is_verified;"""
    )
    lines.append("")

    # Section 4: Scholarships cleanup (#14) + UC-103-safe upsert (never overwrite is_active or last_edited_by_user_id IS NOT NULL)
    lines.append(f"-- 4. Clean invalid/stale scholarships (#14) & upsert {len(scholarships)} verified undergraduate scholarships (UC-103: preserve is_active & Moderator edits)")
    lines.append(
        f"""CREATE TEMP TABLE {t_sch} (
    id uuid NOT NULL,
    code text NOT NULL,
    name varchar(256) NOT NULL,
    provider varchar(256),
    study_level varchar(32) NOT NULL,
    amount_usd numeric(12,2),
    coverage_type varchar(32),
    min_gpa4 numeric(3,2),
    min_ielts numeric(3,1),
    deadline date,
    eligibility_notes varchar(2000),
    source_url varchar(512),
    retrieved_at date,
    source_reliability varchar(16),
    is_active boolean NOT NULL,
    kind varchar(32),
    amount_type varchar(32),
    amount_min_usd numeric(12,2),
    amount_max_usd numeric(12,2),
    duration_years integer,
    renewable boolean,
    renewal_criteria varchar(1000),
    intl_eligible boolean,
    vn_eligible boolean,
    need_based boolean,
    merit_based boolean,
    min_sat integer,
    min_act integer,
    min_toefl numeric(5,1),
    min_duolingo numeric(5,1),
    major_restriction varchar(500),
    application_method varchar(32),
    apply_url varchar(512),
    PRIMARY KEY (code, name)
) ON COMMIT DROP;"""
    )
    if scholarships:
        lines.append(f"INSERT INTO {t_sch} VALUES")
        sch_rows = []
        for s in scholarships:
            sch_rows.append(
                "("
                + ", ".join(
                    [
                        sql_lit(s["id"]),
                        sql_lit(s["code"]),
                        sql_lit(s["name"]),
                        sql_lit(s["provider"]),
                        sql_lit(s["study_level"]),
                        sql_lit(s["amount_usd"]),
                        sql_lit(s["coverage_type"]),
                        sql_lit(s["min_gpa4"]),
                        sql_lit(s["min_ielts"]),
                        sql_lit(s["deadline"], "date"),
                        sql_lit(s["eligibility_notes"]),
                        sql_lit(s["source_url"]),
                        sql_lit(s["retrieved_at"], "date"),
                        sql_lit(s["source_reliability"]),
                        sql_lit(s["is_active"]),
                        sql_lit(s["kind"]),
                        sql_lit(s["amount_type"]),
                        sql_lit(s["amount_min_usd"]),
                        sql_lit(s["amount_max_usd"]),
                        sql_lit(s["duration_years"]),
                        sql_lit(s["renewable"]),
                        sql_lit(s["renewal_criteria"]),
                        sql_lit(s["intl_eligible"]),
                        sql_lit(s["vn_eligible"]),
                        sql_lit(s["need_based"]),
                        sql_lit(s["merit_based"]),
                        sql_lit(s["min_sat"]),
                        sql_lit(s["min_act"]),
                        sql_lit(s["min_toefl"]),
                        sql_lit(s["min_duolingo"]),
                        sql_lit(s["major_restriction"]),
                        sql_lit(s["application_method"]),
                        sql_lit(s["apply_url"]),
                    ]
                )
                + ")"
            )
        lines.append(",\n".join(sch_rows) + ";")
    lines.append("")

    # 4a: Remove spec-violating scholarships (no FK references app.scholarships)
    lines.append(
        """DELETE FROM app.scholarships
WHERE university_id IS NULL
   OR kind = 'external'
   OR study_level IS DISTINCT FROM 'undergraduate';"""
    )
    # 4b: Remove stale auto-imported scholarships superseded by the new dataset (preserve Moderator-edited/created rows)
    lines.append(
        f"""DELETE FROM app.scholarships target
WHERE target.last_edited_by_user_id IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM {t_sch} s
      JOIN app.universities u ON u.code = s.code
      WHERE u.id = target.university_id AND s.name = target.name
  );"""
    )
    # 4c: Update existing non-Moderator-edited scholarships in place (NEVER overwrite is_active)
    sch_upd_cols = [c for c in SCHOLARSHIP_COLS if c != "name"]
    sch_set = ",\n    ".join(f"{c} = s.{c}" for c in sch_upd_cols)
    sch_t_row = ", ".join(f"target.{c}" for c in sch_upd_cols)
    sch_s_row = ", ".join(f"s.{c}" for c in sch_upd_cols)
    lines.append(
        f"""UPDATE app.scholarships target
SET {sch_set},
    updated_at = CURRENT_TIMESTAMP
FROM {t_sch} s
JOIN app.universities u ON u.code = s.code
WHERE target.university_id = u.id
  AND target.name = s.name
  AND target.last_edited_by_user_id IS NULL
  AND ROW({sch_t_row}) IS DISTINCT FROM ROW({sch_s_row});"""
    )
    # 4d: Insert brand-new scholarships
    sch_ins_cols = ", ".join(SCHOLARSHIP_COLS)
    sch_sel_cols = ", ".join(f"s.{c}" for c in SCHOLARSHIP_COLS)
    lines.append(
        f"""INSERT INTO app.scholarships (id, university_id, is_active, {sch_ins_cols}, created_at)
SELECT s.id, u.id, s.is_active, {sch_sel_cols}, CURRENT_TIMESTAMP
FROM {t_sch} s
JOIN app.universities u ON u.code = s.code
WHERE NOT EXISTS (
    SELECT 1 FROM app.scholarships existing
    WHERE existing.university_id = u.id AND existing.name = s.name
)
ON CONFLICT (id) DO NOTHING;"""
    )
    lines.append("")

    # Section 5: BEA SARPP state living costs (UC-28)
    if slc:
        lines.append(f"-- 5. Upsert {len(slc)} BEA SARPP state living cost indices (UC-28)")
        lines.append("INSERT INTO app.state_living_costs (id, state_code, year, index_all_items, index_housing_rents, index_goods, index_other_services, source_url, retrieved_at, created_at) VALUES")
        lines.append(
            ",\n".join(
                "("
                + ", ".join(
                    [
                        sql_lit(r["id"]),
                        sql_lit(r["state_code"]),
                        sql_lit(r["year"]),
                        sql_lit(r["index_all_items"]),
                        sql_lit(r["index_housing_rents"]),
                        sql_lit(r["index_goods"]),
                        sql_lit(r["index_other_services"]),
                        sql_lit(r["source_url"]),
                        sql_lit(r["retrieved_at"], "date"),
                        "CURRENT_TIMESTAMP",
                    ]
                )
                + ")"
                for r in slc
            )
        )
        lines.append(
            """ON CONFLICT (state_code, year) DO UPDATE
SET index_all_items = EXCLUDED.index_all_items,
    index_housing_rents = EXCLUDED.index_housing_rents,
    index_goods = EXCLUDED.index_goods,
    index_other_services = EXCLUDED.index_other_services,
    source_url = EXCLUDED.source_url,
    retrieved_at = EXCLUDED.retrieved_at,
    updated_at = CURRENT_TIMESTAMP
WHERE ROW(app.state_living_costs.index_all_items, app.state_living_costs.index_housing_rents, app.state_living_costs.index_goods, app.state_living_costs.index_other_services, app.state_living_costs.source_url, app.state_living_costs.retrieved_at)
   IS DISTINCT FROM ROW(EXCLUDED.index_all_items, EXCLUDED.index_housing_rents, EXCLUDED.index_goods, EXCLUDED.index_other_services, EXCLUDED.source_url, EXCLUDED.retrieved_at);"""
        )
        lines.append("")

    # Section 6: Reference fees (UC-26)
    if ref_fees:
        lines.append("-- 6. Upsert F-1 visa MRV and SEVIS I-901 reference fees (UC-26)")
        lines.append(
            f"INSERT INTO app.app_settings (id, key, value_json, description, created_at) VALUES ({sql_lit(ref_fees['id'])}, {sql_lit(ref_fees['key'])}, {sql_lit(ref_fees['value_json'])}, {sql_lit(ref_fees['description'])}, CURRENT_TIMESTAMP)"
        )
        lines.append(
            """ON CONFLICT (key) DO UPDATE
SET value_json = EXCLUDED.value_json,
    description = EXCLUDED.description,
    updated_at = CURRENT_TIMESTAMP
WHERE app.app_settings.value_json IS DISTINCT FROM EXCLUDED.value_json
   OR app.app_settings.description IS DISTINCT FROM EXCLUDED.description;"""
        )
        lines.append("")

    # Section 7: Media attribution backfill (UC-21)
    if media_rows:
        lines.append(f"-- 7. Sync {len(media_rows)} university media rows & Commons attribution (license/author/source_page_url)")
        lines.append(f"CREATE TEMP TABLE {t_media} (id uuid NOT NULL, code text NOT NULL, type varchar(32) NOT NULL, url varchar(1000) NOT NULL, display_order integer NOT NULL, license varchar(100), author varchar(300), source_page_url varchar(1000), PRIMARY KEY (code, url)) ON COMMIT DROP;")
        lines.append(f"INSERT INTO {t_media} VALUES")
        lines.append(
            ",\n".join(
                "("
                + ", ".join(
                    [
                        sql_lit(m["id"]),
                        sql_lit(m["code"]),
                        sql_lit(m["type"]),
                        sql_lit(m["url"]),
                        sql_lit(m["display_order"]),
                        sql_lit(m["license"]),
                        sql_lit(m["author"]),
                        sql_lit(m["source_page_url"]),
                    ]
                )
                + ")"
                for m in media_rows
            )
            + ";"
        )
        lines.append(
            f"""UPDATE app.media target
SET license = COALESCE(target.license, m.license),
    author = COALESCE(target.author, m.author),
    source_page_url = COALESCE(target.source_page_url, m.source_page_url),
    updated_at = CURRENT_TIMESTAMP
FROM {t_media} m
JOIN app.universities u ON u.code = m.code
WHERE target.entity_type = 'University'
  AND target.entity_id = u.id
  AND target.url = m.url
  AND ROW(target.license, target.author, target.source_page_url)
      IS DISTINCT FROM ROW(COALESCE(target.license, m.license), COALESCE(target.author, m.author), COALESCE(target.source_page_url, m.source_page_url));"""
        )
        lines.append(
            f"""INSERT INTO app.media (id, entity_type, entity_id, type, url, display_order, license, author, source_page_url, created_at)
SELECT m.id, 'University', u.id, m.type, m.url, m.display_order, m.license, m.author, m.source_page_url, CURRENT_TIMESTAMP
FROM {t_media} m
JOIN app.universities u ON u.code = m.code
WHERE NOT EXISTS (
    SELECT 1 FROM app.media existing
    WHERE existing.entity_type = 'University' AND existing.entity_id = u.id AND existing.url = m.url
)
ON CONFLICT (id) DO NOTHING;"""
        )
        lines.append("")

    # Section 8: Field-level provenance (#14 cleanup + #16 UC-21)
    lines.append(f"-- 8. Replace dataset field_evidence with {len(evidences)} clean verified/derived rows (UC-21 group provenance & #14 stale cleanup)")
    lines.append(f"CREATE TEMP TABLE {t_ev} (id uuid PRIMARY KEY, code text NOT NULL, field_name varchar(64) NOT NULL, field_value text, source_url varchar(512), source_section varchar(64), extracted_quote text NOT NULL, verified_by varchar(128) NOT NULL, status varchar(32) NOT NULL) ON COMMIT DROP;")
    if evidences:
        lines.append(f"INSERT INTO {t_ev} VALUES")
        lines.append(
            ",\n".join(
                "("
                + ", ".join(
                    [
                        sql_lit(e["id"]),
                        sql_lit(e["code"]),
                        sql_lit(e["field_name"]),
                        sql_lit(e["field_value"]),
                        sql_lit(e["source_url"]),
                        sql_lit(e["source_section"]),
                        sql_lit(e["extracted_quote"]),
                        sql_lit(e["verified_by"]),
                        sql_lit(e["status"]),
                    ]
                )
                + ")"
                for e in evidences
            )
            + ";"
        )
    lines.append(
        f"""DELETE FROM app.field_evidence fe
USING app.universities u
WHERE fe.university_id = u.id
  AND u.last_edited_by_user_id IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM app.university_offerings o
    WHERE o.university_id = u.id AND o.study_level = 'undergraduate' AND o.last_edited_by_user_id IS NOT NULL
  )
  AND EXISTS (SELECT 1 FROM {t_school} s WHERE s.code = u.code)
  AND NOT EXISTS (SELECT 1 FROM {t_ev} e WHERE e.id = fe.id);"""
    )
    lines.append(
        f"""INSERT INTO app.field_evidence (id, university_id, field_name, field_value, source_url, source_section, extracted_quote, verified_by, status, created_at)
SELECT e.id, u.id, e.field_name, e.field_value, e.source_url, e.source_section, e.extracted_quote, e.verified_by, e.status, CURRENT_TIMESTAMP
FROM {t_ev} e
JOIN app.universities u ON u.code = e.code
WHERE u.last_edited_by_user_id IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM app.university_offerings o
    WHERE o.university_id = u.id AND o.study_level = 'undergraduate' AND o.last_edited_by_user_id IS NOT NULL
  )
ON CONFLICT (id) DO UPDATE SET
  field_value = EXCLUDED.field_value,
  source_url = EXCLUDED.source_url,
  source_section = EXCLUDED.source_section,
  extracted_quote = EXCLUDED.extracted_quote,
  verified_by = EXCLUDED.verified_by,
  status = EXCLUDED.status
WHERE (app.field_evidence.field_value, app.field_evidence.source_url, app.field_evidence.source_section,
       app.field_evidence.extracted_quote, app.field_evidence.verified_by, app.field_evidence.status)
  IS DISTINCT FROM (EXCLUDED.field_value, EXCLUDED.source_url, EXCLUDED.source_section,
       EXCLUDED.extracted_quote, EXCLUDED.verified_by, EXCLUDED.status);"""
    )
    lines.append("")

    lines.append(
        """-- Tỷ lệ ngoài [0, 1] là dữ liệu sai (UC-21): xóa giá trị cũ, trừ trường Moderator đã sửa tay.
UPDATE app.universities SET acceptance_rate = NULL
WHERE acceptance_rate IS NOT NULL AND (acceptance_rate < 0 OR acceptance_rate > 1)
  AND last_edited_by_user_id IS NULL;
"""
    )

    # Explicit cleanup of temp tables (also ON COMMIT DROP, safe inside or outside explicit txn)
    drop_tables = [t_ev, t_sch, t_majors, t_school]
    if media_rows:
        drop_tables.insert(1, t_media)
    for tbl in drop_tables:
        lines.append(f"DROP TABLE {tbl};")
    lines.append("")
    return "\n".join(lines)


def run(path, db_url, apply, emit_sql, tag, codes=None):
    data = load_dataset(path, codes_filter=codes, tag=tag)
    print(
        f"Đọc {os.path.basename(path)}: {len(data['schools'])} trường, {len(data['majors'])} ngành, "
        f"{len(data['major_links'])} liên kết ngành, {len(data['scholarships'])} học bổng hợp lệ, "
        f"{len(data['state_living_costs'])} bang BEA, {len(data['media'])} ảnh, {len(data['evidences'])} field_evidence"
    )
    if data["skipped_sch"]:
        print(f"  học bổng bỏ qua: {dict(data['skipped_sch'])}")
    if data["warnings"]:
        print(f"  {len(data['warnings'])} cảnh báo:")
        for w in data["warnings"][:30]:
            print(f"    - {w}")
        if len(data["warnings"]) > 30:
            print(f"    ... (+{len(data['warnings']) - 30} cảnh báo khác)")

    if not emit_sql and not apply:
        print("Chạy thử: chưa ghi gì. Thêm --emit-sql <file> hoặc --apply.")
        return

    sql = render_sql(data)
    if emit_sql:
        os.makedirs(os.path.dirname(os.path.abspath(emit_sql)), exist_ok=True)
        with open(emit_sql, "w", encoding="utf-8", newline="\n") as f:
            f.write(sql)
        print(f"Đã xuất SQL: {emit_sql} ({len(sql):,} ký tự)")

    if apply:
        import psycopg

        with psycopg.connect(db_url) as conn:
            with conn.cursor() as cur:
                cur.execute(sql)
            conn.commit()
        print("Đã ghi vào DB.")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("xlsx", help="workbook usdss_dataset_*.xlsx")
    parser.add_argument("--apply", action="store_true", help="ghi trực tiếp vào DB (mặc định chỉ chạy thử)")
    parser.add_argument("--emit-sql", default="", help="xuất file SQL migration (idempotent)")
    parser.add_argument("--tag", default="241126", help="nhãn phiên bản cho tên temp table và UUID evidence")
    parser.add_argument("--codes", default="", help="chỉ nhập các mã trường này, cách nhau bằng dấu phẩy")
    args = parser.parse_args()
    codes = {c.strip() for c in args.codes.split(",") if c.strip()}
    run(args.xlsx, os.environ.get("DATABASE_URL", DEFAULT_DB), args.apply, args.emit_sql, args.tag, codes or None)
