"""Đáp án lượt ② (chấm trường) theo GOI_Y_TRUONG_TONG_QUAN bản 2 và quy ước trong skill_score.md.

Nhận đúng JSON gọn mà AI nhận (key c, acc, iacc, sat, gpa, L...), trả đúng các key AI phải trả,
để so từng bước. Chỉ dùng cho bộ đánh giá (bước 4.1, 4.3). Không chạy trong hệ thống.
"""

# Cận các khoảng GPA (k1..k7) theo skill: k1 [0, 2.50) ... k6 [3.75, 4.00), k7 = 4.0
GPA_EDGES = [0.0, 2.5, 3.0, 3.25, 3.5, 3.75, 4.0]
# Bảng đổi z ra điểm vị trí (skill bước 2c), nội suy tuyến tính giữa 2 mốc
Z_TABLE = [(-2, 2), (-1.5, 7), (-1, 16), (-0.5, 31), (0, 50), (0.5, 69), (1, 84), (1.5, 93), (2, 98)]
GPA_SD = 0.3
LOW_SUBMIT = 0.5
INTL_RATIO = 0.5


def clamp(x, lo, hi):
    return max(lo, min(hi, x))


def z_to_position(z):
    if z <= Z_TABLE[0][0]:
        return Z_TABLE[0][1]
    if z >= Z_TABLE[-1][0]:
        return Z_TABLE[-1][1]
    for (z1, p1), (z2, p2) in zip(Z_TABLE, Z_TABLE[1:], strict=False):
        if z1 <= z <= z2:
            return p1 + (p2 - p1) * (z - z1) / (z2 - z1)
    raise AssertionError("không tới được")


def gpa_position(gpa, dist):
    """Bước 2b: dist = 7 tỉ lệ k1..k7 (0–1)."""
    if gpa >= 4.0:
        return 100 * sum(dist[:6]) + 50 * dist[6]
    for k in range(6):
        lo, hi = GPA_EDGES[k], GPA_EDGES[k + 1]
        if gpa < hi:
            return 100 * sum(dist[:k]) + 100 * max(0.0, (gpa - lo) / (hi - lo)) * dist[k]
    raise AssertionError("không tới được")


def score_school(student, s):
    """Tầng 2 + tầng 3 cho 1 trường, trả dict cùng key với ScoreItem."""
    gpa, sat, tier, budget = student.get("gpa"), student.get("sat"), student["tier"], student.get("budget")
    lg, ls, le = s["L"]

    # Bước 1. Trọng số
    if s.get("subm") is not None and s["subm"] < LOW_SUBMIT:
        ls = ls * 0.5
    if sat is None or s.get("satp") == "no" or s.get("sat") is None:
        ls = 0

    # Bước 2. Điểm vị trí
    ps = None
    if ls > 0:
        lo, hi = s["sat"]
        ps = clamp(25 + 50 * (sat - lo) / (hi - lo), 0, 100)
    pg, conf = None, "L"
    if gpa is not None and s.get("gpa") is not None:
        pg, conf = gpa_position(gpa, s["gpa"]), "H"
    elif gpa is not None and s.get("avg") is not None:
        pg = z_to_position((gpa - s["avg"]) / GPA_SD)

    if pg is None and ps is None:
        a = total = None
        g = "NA"
        w = [0.5, 0.5]
    else:
        if pg is None:
            w = [0.0, 1.0]
        elif ps is None:
            w = [1.0, 0.0]
        else:
            w = [lg / (lg + ls), ls / (lg + ls)] if lg + ls else [0.5, 0.5]
        # Bước 3, 5, 6a
        a = w[0] * (pg or 0) + w[1] * (ps or 0)
    adj = (tier - 3) * le
    if pg is not None or ps is not None:
        total = a + adj
        g = "R" if total < 25 else "S" if total > 75 else "M"
    if (s.get("acc") or 0) >= 0.99:
        g = "O"

    # Bước 6b. Du học sinh
    r, g2 = None, g
    if s.get("iacc") is not None and s.get("acc"):
        r = s["iacc"] / s["acc"]
        if r < INTL_RATIO and g in ("R", "M", "S"):
            g2 = {"S": "M", "M": "R", "R": "RF"}[g]

    # Tầng 3
    coa, aid = s.get("coa"), s.get("aid")
    f = clamp((budget - coa) / budget, 0, 1) if budget and coa else None
    h = min(1.0, aid / coa) if aid is not None and coa else None
    gap = coa - budget if budget and coa and coa > budget else None

    def rnd(x, n=2):
        return None if x is None else round(x, n)

    return {
        "c": s["c"], "w": [round(w[0], 2), round(w[1], 2)], "pg": rnd(pg), "conf": conf, "ps": rnd(ps),
        "A": rnd(a), "adj": adj, "S": rnd(total), "g": g, "r": rnd(r), "g2": g2,
        "F": rnd(f, 3), "H": rnd(h, 3), "Q": s.get("q"), "gap": rnd(gap, 0),
    }
