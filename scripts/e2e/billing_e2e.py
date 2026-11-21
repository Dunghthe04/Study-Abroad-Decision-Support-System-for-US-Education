"""Kiểm thử đầu cuối gói dịch vụ (Free, Premium, Family) qua HTTP, chạy được nhiều lần.

Chuẩn bị (xem README.md): Postgres local có CSDL usas_e2e, smtp_sink.py đang chạy, rồi API chạy bằng
    E2E_JOB_SECONDS=2 E2E_ORDER_MINUTES=1 scripts/e2e/run-api.sh 5098
Chạy:  PYTHONIOENCODING=utf-8 python scripts/e2e/billing_e2e.py        (E2E_BASE=http://127.0.0.1:5098 mặc định)
Thư của API nằm ở thư mục smtp_sink.py đang ghi; nếu không phải scripts/e2e/.mail thì đặt SINK_DIR trỏ tới đó.

Mỗi lần chạy tự tạo người dùng mới (e2e-<run>-<vai trò>@e2e.test) qua register + OTP trong thư. CSDL chỉ dùng để
(1) nâng một tài khoản lên admin, (2) lùi ngày của kỳ gói (time travel), (3) thêm dòng ai_calls giả lập đã dùng token,
(4) đọc lại trạng thái để so sánh. Chỉ nối vào usas_e2e. Kết thúc in bảng kết quả, ghi scripts/e2e/last-run.md,
thoát mã khác 0 nếu có ca lỗi. Mã ca lấy từ docs/billing-plan-v2.md (P*, R*, F*, T*, G*, A*, D*).
"""

import hashlib
import hmac
import importlib.util
import json
import os
import random
import re
import string
import sys
import time
import traceback
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
from datetime import date, datetime, timedelta, timezone

import httpx
import psycopg
from psycopg.rows import dict_row

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
BASE = os.environ.get("E2E_BASE", "http://127.0.0.1:5098")
CHECKSUM = os.environ.get("E2E_CHECKSUM_KEY", "dev-checksum-key")   # run-api.sh đặt PayOs__ChecksumKey
JOB = float(os.environ.get("E2E_JOB_SECONDS", "2"))                 # chu kỳ job nền của API
ORDER_MIN = float(os.environ.get("E2E_ORDER_MINUTES", "1"))         # hạn đơn của API
GRACE_MIN = float(os.environ.get("E2E_GRACE_MINUTES", "2"))         # Billing:PendingGraceMinutes mặc định
RUN = time.strftime("%d%H%M%S") + "".join(random.choices(string.ascii_lowercase, k=3))
PASSWORD = "E2e-Pass-123!"
SEED_PLANS = {"free", "personal-1m", "personal-3m", "family-1m", "family-3m"}
DAY = timedelta(days=1)


def _load_mailbox():
    spec = importlib.util.spec_from_file_location("e2e_mailbox", os.path.join(HERE, "mailbox.py"))   # trùng tên thư viện chuẩn mailbox
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


mb = _load_mailbox()


# ---------------------------------------------------------------- CSDL (chỉ usas_e2e)

_conn = None


def db():
    global _conn
    if _conn is None or _conn.closed:
        settings = open(os.path.join(ROOT, "backend", "src", "StudyAbroad.Api", "appsettings.json"), encoding="utf-8").read()
        password = re.search(r'"Default":\s*"[^"]*Password=([^;"]*)', settings).group(1)   # không bao giờ in ra
        _conn = psycopg.connect(host="localhost", port=5432, dbname="usas_e2e", user="studyabroad", password=password,
                                autocommit=True, row_factory=dict_row, connect_timeout=10)
    return _conn


def sql(query, *params):
    cur = db().execute(query, params)
    return cur.fetchall() if cur.description else []


def one(query, *params):
    rows = sql(query, *params)
    return rows[0] if rows else None


def make_admin(u):
    sql("update app.users set role='admin' where id=%s", u.id)
    u.role, u.customer = "admin", False
    expect(u.login(), "đăng nhập lại sau khi lên admin")


def cycles(u, kind=None, status=None):
    return sql("select * from app.user_subscriptions where user_id=%s and (%s::text is null or kind=%s) and (%s::text is null or status=%s) order by created_at",
               u.id, kind, kind, status, status)


def active_cycle(u, kind="personal"):
    rows = cycles(u, kind, "active")
    expect(len(rows) == 1, f"{u.email}: cần đúng 1 kỳ {kind} active, có {len(rows)}")
    return rows[0]


def set_cycle_dates(cycle_id, starts_secs, ends_secs):
    """Time travel: đặt starts_at / ends_at lệch so với now() (giây, âm = quá khứ)."""
    sql("update app.user_subscriptions set starts_at=now()+make_interval(secs=>%s), ends_at=now()+make_interval(secs=>%s) where id=%s",
        starts_secs, ends_secs, cycle_id)


def add_usage(u, tokens, cycle_id=None, plan_code="free"):
    """Giả lập lượt chat Claude đã dùng `tokens` token (đóng dấu kỳ hoặc hạn mức Free như ChatService)."""
    sql("insert into app.ai_calls (id,user_id,kind,success,created_at,fallback_used,plan_code,user_subscription_id,prompt_tokens,completion_tokens,total_tokens)"
        " values (gen_random_uuid(),%s,'claude',true,now(),false,%s,%s,%s,%s,%s)",
        u.id, plan_code, cycle_id, tokens * 3 // 4, tokens - tokens * 3 // 4, tokens)


# ---------------------------------------------------------------- HTTP, quét token

TOKEN_LEAKS, SCANNED = [], [0]
CUSTOMER_PATHS = ("/api/v1/plans", "/api/v1/subscription", "/api/v1/orders", "/api/v1/family", "/api/v1/notifications",
                  "/api/v1/advisor", "/api/v1/recommendations")


def _walk(node, path, where=""):
    if isinstance(node, dict):
        for k, v in node.items():
            if "token" in k.lower() or k.lower() == "quota":
                TOKEN_LEAKS.append(f"{path} key '{where}{k}'")
            _walk(v, path, f"{where}{k}.")
    elif isinstance(node, list):
        for item in node:
            _walk(item, path, where)


def _scan(path, r):
    if not path.startswith(CUSTOMER_PATHS):
        return
    SCANNED[0] += 1
    try:
        for line in r.text.splitlines():   # JSON thường hoặc NDJSON của chat/stream
            if line.strip():
                _walk(json.loads(line), path)
    except ValueError:
        pass


class Http:
    customer = True
    id = None
    password = None
    email = None

    def __init__(self):
        octet = lambda: random.randint(1, 254)
        # Mỗi tài khoản giả lập một IP khách (X-Forwarded-For): bộ giới hạn 10 lần/phút của /auth tính theo IP, hàng chục tài khoản mới sẽ chạm trần
        self.http = httpx.Client(base_url=BASE, timeout=60, headers={"X-Forwarded-For": f"10.{octet()}.{octet()}.{octet()}"})

    def request(self, method, path, **kw):
        r = self.http.request(method, path, **kw)
        if r.status_code == 401 and self.id and self.password and not path.startswith("/api/v1/auth") and self.login():   # token 15 phút
            r = self.http.request(method, path, **kw)
        if self.customer:
            _scan(path, r)
        return r

    def get(self, path, **kw):
        return self.request("GET", path, **kw)

    def post(self, path, **kw):
        return self.request("POST", path, **kw)

    def put(self, path, **kw):
        return self.request("PUT", path, **kw)

    def delete(self, path, **kw):
        return self.request("DELETE", path, **kw)

    def login(self):
        return self.http.post("/api/v1/auth/login", json={"email": self.email, "password": self.password}).status_code == 200


class User(Http):
    def __init__(self, email, role, name):
        super().__init__()
        self.email, self.role, self.name, self.password = email, role, name, PASSWORD


ANON = Http()
USERS = {}


def dob_for_age(age):
    today = datetime.now(timezone(timedelta(hours=7))).date()
    try:
        born = today.replace(year=today.year - age)
    except ValueError:
        born = today.replace(year=today.year - age, day=28)
    return (born - timedelta(days=45)).isoformat()


def user(tag, role="student", age=20, parent_tag=None):
    """Tạo (một lần) tài khoản e2e-<run>-<tag>@e2e.test: register, OTP trong thư, verify-email (tự đăng nhập)."""
    if tag in USERS:
        return USERS[tag]
    email = f"e2e-{RUN}-{tag}@e2e.test"
    u = User(email, role, f"E2E {tag}")
    body = {"email": email, "password": PASSWORD, "fullName": u.name, "role": role, "dateOfBirth": dob_for_age(age)}
    if parent_tag:
        body["parentEmail"] = f"e2e-{RUN}-{parent_tag}@e2e.test"
    r = u.http.post("/api/v1/auth/register", json=body)
    if r.status_code == 429:
        time.sleep(61)
        r = u.http.post("/api/v1/auth/register", json=body)
    status(r, 201)
    otp = mb.otp(mb.wait_for(email, "Xác thực", timeout=30))
    v = u.http.post("/api/v1/auth/verify-email", json={"email": email, "otpCode": otp})
    u.id = status(v, 200)["user"]["id"]
    USERS[tag] = u
    return u


# ---------------------------------------------------------------- khung ca kiểm thử

RESULTS = []
_ids = {}


class Note:
    text = ""

    def __call__(self, text):
        self.text = text


@contextmanager
def case(cid, desc):
    note = Note()
    try:
        yield note
        RESULTS.append((cid, "PASS", f"{desc}" + (f" | {note.text}" if note.text else "")))
    except Exception as e:  # noqa: BLE001 - mọi lỗi của một ca chỉ làm ca đó rớt
        where = ""
        if not isinstance(e, AssertionError):
            frame = traceback.extract_tb(e.__traceback__)[-1]
            where = f" [{type(e).__name__} @ dòng {frame.lineno}]"
        RESULTS.append((cid, "FAIL", f"{desc} | {e}{where}"))
    last = RESULTS[-1]
    print(f"{last[1]:4} {last[0]:8} {last[2][:190]}", flush=True)


def expect(cond, msg=""):
    if not cond:
        raise AssertionError(msg or "điều kiện sai")


def short(text, n=260):
    text = re.sub(r"\s+", " ", text or "")
    return text if len(text) <= n else text[:n] + "..."


def status(r, code, err=None):
    """Kiểm mã HTTP (và `code` lỗi nghiệp vụ nếu có). Trả JSON của phản hồi (None nếu rỗng)."""
    codes = code if isinstance(code, (set, tuple, list)) else (code,)
    try:
        body = r.json() if r.content else None
    except ValueError:
        body = None
    ok = r.status_code in codes and (err is None or (isinstance(body, dict) and body.get("code") == err))
    expect(ok, f"{r.request.method} {r.request.url.path} -> {r.status_code} {short(r.text)}; cần {code}" + (f" code={err}" if err else ""))
    return body


def eventually(fn, timeout=20.0, every=0.5):
    """Gọi fn tới khi trả giá trị đúng (truthy); hết giờ trả None."""
    end = time.time() + timeout
    while True:
        value = fn()
        if value:
            return value
        if time.time() > end:
            return None
        time.sleep(every)


def settle():
    """Chờ vài chu kỳ job nền để chắc một thông báo 'đúng một lần' không bị gửi lặp."""
    time.sleep(JOB * 3 + 1)


def parse_dt(s):
    if s is None:
        return None
    s = re.sub(r"(\.\d{6})\d+", r"\1", s)
    d = datetime.fromisoformat(s)
    return d if d.tzinfo else d.replace(tzinfo=timezone.utc)


def close(a, b, secs=5):
    return a is not None and b is not None and abs((a - b).total_seconds()) <= secs


# ---------------------------------------------------------------- thư, thông báo, đơn, webhook

def mail_n(addr, subject):
    return len([m for m in mb.messages(addr) if subject.lower() in m["subject"].lower()])


def wait_mail(addr, subject, after=0, timeout=20):
    return mb.wait_for(addr, subject, after_count=after, timeout=timeout)


def notifs(u, kind=None):
    items = status(u.get("/api/v1/notifications", params={"pageSize": 50}), 200)["items"]
    return [n for n in items if kind is None or n["type"] == kind]


def wait_notif(u, kind, n=1, timeout=15):
    return eventually(lambda: len(notifs(u, kind)) >= n and notifs(u, kind), timeout)


def sub(u):
    return status(u.get("/api/v1/subscription"), 200)


def buy(u, plan):
    return status(u.post("/api/v1/orders", json={"planCode": plan}), 201)


def order(u, code):
    return status(u.get(f"/api/v1/orders/{code}"), 200)


def pay(code, amount=None):
    """Thanh toán giả qua /dev/payos: dựng webhook ký đúng rồi xử lý như webhook thật."""
    r = ANON.post(f"/api/v1/dev/payos/{code}/pay", json={} if amount is None else {"amount": amount})
    return status(r, 200)["handled"]


def new_premium(tag, plan="personal-1m", role="student", age=20):
    u = user(tag, role, age)
    o = buy(u, plan)
    expect(pay(o["orderCode"]), "webhook dev không được xử lý")
    expect(order(u, o["orderCode"])["status"] == "paid", f"đơn {o['orderCode']} chưa paid")
    return u


def _value_str(v):
    if v is None:
        return ""
    if isinstance(v, bool):
        return "true" if v else "false"
    return str(v) if isinstance(v, (int, str)) else json.dumps(v, ensure_ascii=False, separators=(",", ":"))


def sign_webhook(code, amount, key=CHECKSUM, tamper_amount=None, inner="00", outer="00"):
    """Body webhook payOS tự ký (HMAC-SHA256 trên data xếp theo tên, key=value nối &). tamper_amount đổi số tiền SAU khi ký."""
    data = {"orderCode": code, "amount": amount, "description": "E2E", "accountNumber": "12345678", "reference": f"E2E{code}",
            "transactionDateTime": "2026-11-21 10:00:00", "currency": "VND", "paymentLinkId": f"fake-{code}",
            "code": inner, "desc": "success", "counterAccountName": "NGUYEN VAN A"}
    string = "&".join(f"{k}={_value_str(data[k])}" for k in sorted(data))
    signature = hmac.new(key.encode(), string.encode(), hashlib.sha256).hexdigest()
    if tamper_amount is not None:
        data["amount"] = tamper_amount
    return json.dumps({"code": outer, "desc": "success", "success": True, "data": data, "signature": signature}, separators=(",", ":"))


def webhook(body):
    return ANON.post("/api/v1/payments/payos/webhook", content=body, headers={"Content-Type": "application/json"})


# ---------------------------------------------------------------- Family: lời mời, thành viên

def invite(owner, email, seat):
    return owner.post("/api/v1/family/invites", json={"email": email, "seatRole": seat})


def invite_token(addr, after=0):
    return mb.token_from_link(wait_mail(addr, "tham gia gói", after), "/family/invite")


def accept(u, token):
    return u.post("/api/v1/family/invites/accept", json={"token": token})


def family(u):
    return status(u.get("/api/v1/family"), 200)


def member_of(fam, name):
    found = [m for m in fam["members"] if m["name"] == name]
    expect(len(found) == 1, f"không thấy đúng 1 thành viên '{name}' trong {[m['name'] for m in fam['members']]}")
    return found[0]


def buy_family(tag, plan="family-1m", age=40):
    return new_premium(tag, plan, "parent", age)


def invite_row(email):
    return one("select * from app.family_members where invited_email=%s order by created_at desc limit 1", email)


# ================================================================ các nhóm ca kiểm thử

EXP = {}   # đơn của P11 tạo từ đầu (job cần ~3 phút để hết hạn), kiểm ở cuối


def phase_plans_and_setup():
    with case("T1a", "GET /plans ẩn danh: đúng 5 gói, trường đủ, Family có seats") as note:
        plans = status(ANON.get("/api/v1/plans"), 200)
        expect({p["code"] for p in plans} == SEED_PLANS and len(plans) == 5, f"mã gói: {sorted(p['code'] for p in plans)}")
        by = {p["code"]: p for p in plans}
        expect(by["free"]["priceVnd"] == 0 and by["free"]["durationDays"] is None, "gói free")
        expect(by["personal-1m"]["priceVnd"] == 79000 and by["personal-1m"]["durationDays"] == 30, "personal-1m")
        expect(by["family-3m"]["seats"] == {"students": 1, "parents": 2} and by["personal-1m"]["seats"] is None, "seats")
        expect(all({"code", "name", "kind", "priceVnd", "quotaPeriodDays", "quotaMultiplier"} <= set(p) for p in plans), "thiếu trường")
        expect(by["free"]["quotaMultiplier"] is None and by["personal-1m"]["quotaPeriodDays"] == 30 and by["personal-3m"]["quotaPeriodDays"] == 30, "quotaPeriodDays/quotaMultiplier")
        expect(by["personal-1m"]["quotaMultiplier"] and by["personal-1m"]["quotaMultiplier"] > 0, "quotaMultiplier")
        note(f"{len(plans)} gói")

    with case("P11-setup", "tạo đơn chờ cho P11 (job làm hết hạn, kiểm ở cuối)"):
        u = user("exp")
        o = buy(u, "personal-1m")
        EXP.update(user=u, code=o["orderCode"], expires=parse_dt(o["expiresAt"]))


def phase_purchase():
    u = None
    with case("P1★", "HS 17 tuổi mua personal-1m, dev pay -> paid, premium, thư biên nhận, thông báo") as note:
        u = user("u17", age=17)
        expect(sub(u)["tier"] == "free", "tài khoản mới phải là free")
        o = buy(u, "personal-1m")
        expect(o["status"] == "pending" and o["amountVnd"] == 79000 and o["planCode"] == "personal-1m", o)
        expect(o["checkoutUrl"] and o["qrCode"] and o["description"].startswith("USAS") and len(o["description"]) == 9, f"link/QR/mô tả: {o}")
        ttl = (parse_dt(o["expiresAt"]) - parse_dt(o["createdAt"])).total_seconds()
        expect(abs(ttl - ORDER_MIN * 60) <= 5, f"hạn đơn {ttl:.0f}s, cần ~{ORDER_MIN * 60:.0f}s")
        expect(sub(u)["pendingOrderCode"] == o["orderCode"], "summary phải có pendingOrderCode")
        EXP["u17_code"] = o["orderCode"]
        expect(pay(o["orderCode"]) is True, "handled")
        paid = order(u, o["orderCode"])
        cyc = paid["cycle"]
        expect(paid["status"] == "paid" and paid["paidAt"] and cyc and cyc["queued"] is False, paid)
        expect(close(parse_dt(cyc["startsAt"]), datetime.now(timezone.utc), 15), f"kỳ phải bắt đầu ngay: {cyc}")
        expect(parse_dt(cyc["endsAt"]) - parse_dt(cyc["startsAt"]) == 30 * DAY, f"kỳ phải dài 30 ngày: {cyc}")
        s = sub(u)
        expect(s["tier"] == "premium" and s["chargingPool"] == "personal" and s["pendingOrderCode"] is None, s)
        expect(s["personal"]["planCode"] == "personal-1m" and s["personal"]["usedPercent"] == 0 and s["personal"]["queued"] is None, s["personal"])
        wait_mail(u.email, "Biên nhận thanh toán")
        expect(wait_notif(u, "billing_payment_paid"), "thiếu thông báo billing_payment_paid")
        note(f"order {o['orderCode']}")

    with case("P-sanitize", "webhook ký tay (job không can thiệp): đơn paid, payload lưu có orderCode nhưng đã lược counterAccount*") as note:
        u = user("usan", age=27)
        code = buy(u, "personal-1m")["orderCode"]
        status(webhook(sign_webhook(code, 79000)), 200)
        expect(order(u, code)["status"] == "paid", "đơn phải paid")
        payload = one("select payload_json from app.payment_transactions where order_code=%s", code)["payload_json"] or ""
        expect('"orderCode"' in payload and "counterAccount" not in payload and "NGUYEN VAN A" not in payload, f"payload: {short(payload)}")

    with case("P14", "webhook lặp (dev pay + webhook ký tay): 1 kỳ, 1 thư biên nhận") as note:
        u = user("u17")
        code = EXP["u17_code"]
        expect(pay(code) is True, "dev pay lần 2")
        r = webhook(sign_webhook(code, 79000))
        status(r, 200)
        settle()
        expect(len(cycles(u, "personal")) == 1, f"{len(cycles(u, 'personal'))} kỳ, cần 1")
        expect(mail_n(u.email, "Biên nhận thanh toán") == 1, f"{mail_n(u.email, 'Biên nhận thanh toán')} thư biên nhận, cần 1")
        expect(len(notifs(u, "billing_payment_paid")) == 1, "thông báo billing_payment_paid phải đúng 1")

    with case("P14b", "webhook ký tay, dev pay và dev status PAID (job) đập cùng lúc: đúng 1 kỳ, 1 biên nhận") as note:
        u = user("urace2", age=26)
        code = buy(u, "personal-1m")["orderCode"]
        hits = [lambda: pay(code),
                lambda: webhook(sign_webhook(code, 79000)).status_code == 200,
                lambda: ANON.post(f"/api/v1/dev/payos/{code}/status", json={"status": "PAID", "amountPaid": 79000}).status_code == 200]
        with ThreadPoolExecutor(3) as ex:
            expect(all(f.result() for f in [ex.submit(h) for h in hits]), "một lời gọi lỗi")
        settle()
        expect(order(u, code)["status"] == "paid" and len(cycles(u, "personal")) == 1, f"{len(cycles(u, 'personal'))} kỳ")
        expect(mail_n(u.email, "Biên nhận thanh toán") == 1 and len(notifs(u, "billing_payment_paid")) == 1, "đúng 1 biên nhận và 1 thông báo")

    with case("P2", "HS 15 tuổi mua -> 403 age_restricted") as note:
        par = user("par15", "parent", 40)   # email phụ huynh khai lúc đăng ký (HS dưới 16 bắt buộc)
        u15 = user("u15", age=15, parent_tag="par15")
        r = u15.post("/api/v1/orders", json={"planCode": "personal-1m"})
        status(r, 403, "age_restricted")
        expect(len(sql("select 1 from app.payment_transactions where user_id=%s", u15.id)) == 0, "không được có đơn nào")

    with case("P4", "admin không mua được -> 403"):
        admin = ADMIN()
        r = admin.post("/api/v1/orders", json={"planCode": "personal-1m"})
        status(r, 403)

    with case("P5+P10", "đơn thứ hai -> 409 pending_order_exists kèm orderCode; hủy đơn -> cancelled, được tạo đơn mới") as note:
        u = user("ub", age=18)
        a = buy(u, "personal-1m")
        r = u.post("/api/v1/orders", json={"planCode": "personal-3m"})
        body = status(r, 409, "pending_order_exists")
        expect(body.get("orderCode") == a["orderCode"], f"orderCode {body.get('orderCode')} != {a['orderCode']}")
        c = status(u.post(f"/api/v1/orders/{a['orderCode']}/cancel"), 200)
        expect(c["status"] == "cancelled", c)
        status(u.post(f"/api/v1/orders/{a['orderCode']}/cancel"), 409, "not_pending")
        b = buy(u, "personal-1m")
        expect(b["orderCode"] != a["orderCode"] and b["status"] == "pending", b)
        EXP["ub_code"] = b["orderCode"]
        note("P5 và P10 đạt")

    with case("P-own", "đơn của người khác: xem/hủy -> 404 order_not_found, không thấy trong danh sách; admin không vào route khách") as note:
        other, code = user("u17"), EXP["ub_code"]
        status(other.get(f"/api/v1/orders/{code}"), 404, "order_not_found")
        status(other.post(f"/api/v1/orders/{code}/cancel"), 404, "order_not_found")
        lst = status(other.get("/api/v1/orders", params={"pageSize": 500}), 200)
        expect(code not in [o["orderCode"] for o in lst["items"]] and lst["pageSize"] == 50, f"pageSize={lst['pageSize']}")
        for path in ("/api/v1/orders", "/api/v1/subscription", "/api/v1/family"):
            status(ADMIN().get(path), 403)
        expect(order(user("ub"), code)["status"] == "pending", "đơn của ub không bị đụng")

    with case("P17", "webhook sai chữ ký / sửa số tiền sau khi ký -> 400, đơn không đổi"):
        code = EXP["ub_code"]
        status(webhook(sign_webhook(code, 79000, key="khoa-sai")), 400, "invalid_signature")
        status(webhook(sign_webhook(code, 1000, tamper_amount=79000)), 400, "invalid_signature")
        status(webhook("không phải json"), 400, "invalid_signature")
        expect(order(user("ub"), code)["status"] == "pending", "đơn phải vẫn pending")

    with case("P17c", "webhook ký đúng nhưng báo thất bại (mã trong data hoặc mã ngoài khác 00) -> 200, đơn không kích hoạt"):
        code = EXP["ub_code"]
        status(webhook(sign_webhook(code, 79000, inner="01")), 200)
        status(webhook(sign_webhook(code, 79000, outer="01")), 200)
        expect(order(user("ub"), code)["status"] == "pending" and len(cycles(user("ub"))) == 0, "đơn phải vẫn pending, không có kỳ")

    with case("P18★", "webhook ký đúng cho mã đơn lạ -> 200, không tạo gì") as note:
        code = 9_000_000_000_000 + random.randint(0, 99_999_999)
        r = webhook(sign_webhook(code, 79000))
        status(r, 200)
        expect(one("select count(*) n from app.payment_transactions where order_code=%s", code)["n"] == 0, "không được tạo đơn")

    with case("P-job", "dev status PAID (không webhook): job đối soát kích hoạt đơn trong vài giây") as note:
        u, code = user("ub"), EXP["ub_code"]
        status(ANON.post(f"/api/v1/dev/payos/{code}/status", json={"status": "PAID", "amountPaid": 79000}), 200)
        t0 = time.time()
        paid = eventually(lambda: (o := order(u, code))["status"] == "paid" and o, timeout=30)
        expect(paid, f"đơn vẫn {order(u, code)['status']} sau 30s")
        expect(paid["cycle"] and paid["cycle"]["queued"] is False and sub(u)["tier"] == "premium", paid)
        expect(mail_n(u.email, "Biên nhận thanh toán") == 1, "cần đúng 1 thư biên nhận")
        note(f"kích hoạt sau {time.time() - t0:.1f}s")

    with case("P15★", "trả sai số tiền -> mismatch, không có kỳ, báo người mua") as note:
        u = user("umm", age=19)
        o = buy(u, "personal-1m")
        expect(pay(o["orderCode"], 1000) is True, "handled")
        got = order(u, o["orderCode"])
        expect(got["status"] == "mismatch" and got["cycle"] is None and got["paidAt"] is None, got)
        expect(sub(u)["tier"] == "free" and len(cycles(u)) == 0, "không được có kỳ")
        expect(wait_notif(u, "billing_payment_mismatch"), "thiếu thông báo billing_payment_mismatch")
        expect(sub(u)["pendingOrderCode"] is None, "đơn mismatch không còn là đơn chờ")
        EXP["mm_code"] = o["orderCode"]
        note(f"order {o['orderCode']}")


def phase_renewal():
    with case("R1★", "gia hạn khi còn token: đơn thứ hai thành kỳ queued, summary có kỳ tiếp theo") as note:
        u = user("u17")
        o = buy(u, "personal-1m")
        expect(pay(o["orderCode"]), "handled")
        got = order(u, o["orderCode"])
        expect(got["status"] == "paid" and got["cycle"] and got["cycle"]["queued"] is True and got["cycle"]["startsAt"] is None, got)
        s = sub(u)
        expect(s["personal"]["queued"] and s["personal"]["queued"]["planName"], f"summary: {s['personal']}")
        expect(s["personal"]["expiringSoon"] is False, "có kỳ chờ thì không cảnh báo sắp hết hạn")
        rows = cycles(u, "personal")
        expect(sorted(r["status"] for r in rows) == ["active", "queued"], [r["status"] for r in rows])
        expect(next(r for r in rows if r["status"] == "queued")["starts_at"] is None, "kỳ queued không có ngày")

    with case("R6", "gia hạn lần hai khi đã có kỳ chờ -> 409 already_renewed"):
        u = user("u17")
        status(u.post("/api/v1/orders", json={"planCode": "personal-1m"}), 409, "already_renewed")
        status(u.post("/api/v1/orders", json={"planCode": "personal-3m"}), 409, "already_renewed")

    with case("R3★", "dùng hết token kỳ active: kỳ queued bắt đầu ngay, kỳ cũ ended exhausted_rollover") as note:
        u = user("u17")
        old = active_cycle(u)
        add_usage(u, old["token_quota"], old["id"], "personal-1m")
        s = eventually(lambda: (x := sub(u))["personal"]["queued"] is None and x, 20)
        expect(s, f"kỳ queued chưa bắt đầu: {sub(u)['personal']}")
        expect(s["personal"]["usedPercent"] == 0 and s["personal"]["exhausted"] is False, s["personal"])
        row_old = one("select * from app.user_subscriptions where id=%s", old["id"])
        expect(row_old["status"] == "ended" and row_old["ended_reason"] == "exhausted_rollover", f"{row_old['status']}/{row_old['ended_reason']}")
        new = active_cycle(u)
        expect(new["id"] != old["id"] and close(new["starts_at"], datetime.now(timezone.utc), 30), f"kỳ mới bắt đầu {new['starts_at']}")
        expect(new["ends_at"] - new["starts_at"] == 30 * DAY, "kỳ mới phải đủ 30 ngày")
        expect(close(row_old["ended_at"], new["starts_at"], 2), "kỳ cũ kết thúc đúng lúc kỳ mới bắt đầu")
        expect(wait_notif(u, "billing_cycle_started"), "thiếu thông báo billing_cycle_started")
        settle()
        expect(len(notifs(u, "billing_cycle_started")) == 1, f"{len(notifs(u, 'billing_cycle_started'))} thông báo billing_cycle_started, cần đúng 1 (job và đọc cùng chuyển kỳ)")

    with case("R2", "kỳ active hết hạn khi có kỳ queued: kỳ queued bắt đầu đúng lúc kỳ cũ hết hạn") as note:
        u = new_premium("r2")
        o = buy(u, "personal-1m")
        expect(pay(o["orderCode"]), "handled")
        old = active_cycle(u)
        set_cycle_dates(old["id"], -31 * 86400, -2 * 3600)
        old_end = one("select ends_at from app.user_subscriptions where id=%s", old["id"])["ends_at"]
        expect(eventually(lambda: sub(u)["personal"]["queued"] is None, 20), "kỳ queued chưa bắt đầu")
        row_old = one("select * from app.user_subscriptions where id=%s", old["id"])
        expect(row_old["status"] == "ended" and row_old["ended_reason"] == "expired", f"{row_old['status']}/{row_old['ended_reason']}")
        new = active_cycle(u)
        expect(close(new["starts_at"], old_end, 1), f"kỳ mới phải bắt đầu tại {old_end}, thực tế {new['starts_at']}")
        expect(new["ends_at"] - new["starts_at"] == 30 * DAY, "kỳ mới đủ 30 ngày từ lúc bắt đầu")
        expect(sub(u)["tier"] == "premium", "vẫn premium")
        expect(wait_notif(u, "billing_cycle_started"), "thiếu thông báo billing_cycle_started")
        settle()
        expect(len(notifs(u, "billing_cycle_started")) == 1, f"{len(notifs(u, 'billing_cycle_started'))} thông báo billing_cycle_started, cần đúng 1")

    with case("R7", "hết hạn không có kỳ chờ: về free, lịch sử còn, tính năng Premium khóa") as note:
        u = new_premium("r7")
        c = active_cycle(u)
        set_cycle_dates(c["id"], -31 * 86400, -3600)
        s = eventually(lambda: (x := sub(u))["tier"] == "free" and x, 20)
        expect(s and s["personal"] is None and s["chargingPool"] == "free", f"summary: {sub(u)}")
        row = one("select status, ended_reason from app.user_subscriptions where id=%s", c["id"])
        expect(row["status"] == "ended" and row["ended_reason"] == "expired", f"{row}")
        history = status(u.get("/api/v1/orders"), 200)
        expect(history["totalCount"] == 1 and history["items"][0]["status"] == "paid", "lịch sử đơn phải còn")
        expect(wait_notif(u, "billing_expired"), "thiếu thông báo billing_expired")
        settle()
        expect(len(notifs(u, "billing_expired")) == 1, f"{len(notifs(u, 'billing_expired'))} thông báo billing_expired, cần đúng 1")
        r = u.post("/api/v1/recommendations", json={})
        status(r, 403, "premium_required")


def phase_usage_notifications():
    with case("T7", "dùng 80%: đúng một thông báo billing_usage_80 dù job chạy nhiều lần") as note:
        u = new_premium("ut", age=21)
        c = active_cycle(u)
        add_usage(u, c["token_quota"] * 80 // 100, c["id"], "personal-1m")
        expect(wait_notif(u, "billing_usage_80"), "chưa có billing_usage_80")
        settle()
        expect(len(notifs(u, "billing_usage_80")) == 1, f"{len(notifs(u, 'billing_usage_80'))} thông báo 80%")
        expect(len(notifs(u, "billing_usage_100")) == 0, "chưa được có 100%")
        expect(sub(u)["personal"]["usedPercent"] == 80 and sub(u)["personal"]["lowQuota"] is True and sub(u)["lowQuota"] is True, "summary 80%")

    with case("T8", "dùng 100%: đúng một thông báo billing_usage_100, 80% vẫn một") as note:
        u = user("ut")
        c = active_cycle(u)
        add_usage(u, c["token_quota"] - c["token_quota"] * 80 // 100, c["id"], "personal-1m")
        expect(wait_notif(u, "billing_usage_100"), "chưa có billing_usage_100")
        settle()
        expect(len(notifs(u, "billing_usage_100")) == 1 and len(notifs(u, "billing_usage_80")) == 1, "mỗi loại đúng một lần")
        s = sub(u)
        expect(s["personal"]["usedPercent"] == 100 and s["personal"]["exhausted"] is True and s["tier"] == "premium", s["personal"])
        row = one("select warned_80_at, exhausted_notified_at from app.user_subscriptions where id=%s", c["id"])
        expect(row["warned_80_at"] and row["exhausted_notified_at"], f"dấu thời gian: {row}")

    with case("R8", "còn 3 ngày (nhắc trước 7 ngày): đúng một thư và một thông báo billing_expiring") as note:
        u = user("ut")
        c = active_cycle(u)
        set_cycle_dates(c["id"], -27 * 86400, 3 * 86400)
        wait_mail(u.email, "sắp hết hạn", timeout=20)
        expect(wait_notif(u, "billing_expiring"), "chưa có billing_expiring")
        settle()
        expect(mail_n(u.email, "sắp hết hạn") == 1 and len(notifs(u, "billing_expiring")) == 1, "chỉ được nhắc một lần")
        expect(one("select reminder_sent_at from app.user_subscriptions where id=%s", c["id"])["reminder_sent_at"], "reminder_sent_at")
        expect(sub(u)["personal"]["expiringSoon"] is True and sub(u)["expiringSoon"] is False, "expiringSoon theo pool đang trừ")


def phase_gating():
    with case("G6★", "Free: POST recommendations / repick / simulate -> 403 premium_required; GET latest không bị chặn") as note:
        u = user("free", age=20)
        for path, body in [("/api/v1/recommendations", {}), ("/api/v1/recommendations/repick", {"pref": [2, 2, 2]}), ("/api/v1/recommendations/simulate", {})]:
            r = u.post(path, json=body)
            b = status(r, 403, "premium_required")
            expect(b.get("feature") == "recommendations", f"{path}: feature={b.get('feature')}")
        r = u.get("/api/v1/recommendations/latest")
        expect(r.status_code in (200, 404), f"GET latest -> {r.status_code}")
        scen = u.post("/api/v1/scenarios", json={})
        expect(scen.status_code == 403 and scen.json().get("code") == "premium_required", f"POST scenarios -> {scen.status_code} {short(scen.text)}")

    with case("G6b", "Premium: POST recommendations không bị chặn premium_required") as note:
        u = user("u17")
        r = u.post("/api/v1/recommendations", json={})
        code = r.json().get("code") if r.content and r.headers.get("content-type", "").startswith("application/") else None
        expect(not (r.status_code == 403 and code == "premium_required"), f"Premium vẫn bị chặn: {short(r.text)}")
        note(f"status {r.status_code}: {short(r.text, 90)}")

    chat_body = lambda action: {"messages": [{"role": "user", "content": "Giải thích giúp em"}], "action": action}
    with case("G4", "Free bấm explain / goal_seek: thẻ locked premium_required, không gọi advisor") as note:
        u = user("free")
        for action in ({"type": "explain", "schoolCode": "MIT"}, {"type": "explain"}, {"type": "goal_seek", "schoolCode": "MIT", "target": "match"}):
            r = u.post("/api/v1/advisor/chat", json=chat_body(action))
            b = status(r, 200)
            expect(b.get("locked") and b["locked"]["reason"] == "premium_required" and b["locked"].get("resetsAt") is None, f"{action['type']}: {short(r.text)}")
            expect(b["answer"] and b.get("action") is None and b.get("aiUsage") is None, f"rò thông tin nội bộ: {short(r.text)}")
            expect(any(q.get("href") == "/plans" for q in (b.get("quickReplies") or [])), "cần nút Xem gói href=/plans")

    with case("G13", "Free: chat/stream trả sự kiện final có response.locked") as note:
        u = user("free")
        r = u.post("/api/v1/advisor/chat/stream", json=chat_body({"type": "explain", "schoolCode": "MIT"}))
        expect(r.status_code == 200, f"stream -> {r.status_code} {short(r.text)}")
        events = [json.loads(line) for line in r.text.splitlines() if line.strip()]
        final = [e for e in events if e.get("type") == "final"]
        expect(final and final[-1]["response"].get("locked", {}).get("reason") == "premium_required", f"events: {short(r.text)}")

    with case("G4b", "Premium bấm explain: không bị khóa") as note:
        u = user("u17")
        r = u.post("/api/v1/advisor/chat", json=chat_body({"type": "explain"}))
        locked = r.json().get("locked") if r.status_code == 200 else None
        expect(locked is None, f"Premium bị khóa: {short(r.text)}")
        note(f"status {r.status_code}")

    with case("G9", "Premium hết token mọi pool: nút explain bị khóa premium_required, recommendations vẫn mở (O2)") as note:
        u = user("ut")   # T8 đã dùng hết kỳ Personal, kỳ còn hạn
        s = sub(u)
        expect(s["tier"] == "premium" and s["personal"]["exhausted"] is True and s["chargingPool"] == "free", s)
        b = status(u.post("/api/v1/advisor/chat", json=chat_body({"type": "explain", "schoolCode": "MIT"})), 200)
        expect(b.get("locked", {}).get("reason") == "premium_required", f"locked={b.get('locked')}")
        r = u.post("/api/v1/recommendations", json={})
        expect(not (r.status_code == 403 and r.json().get("code") == "premium_required"), f"recommendations bị khóa: {short(r.text)}")
        note(f"recommendations status {r.status_code}")

    with case("G8", "Free hết hạn mức tháng (>=120000): chat trả quota_exhausted, resetsAt +07:00 mùng 1, không gọi advisor") as note:
        u = user("g8", age=22)
        add_usage(u, 120_000, None, "free")
        expect(sub(u)["free"]["usedPercent"] == 100, f"free: {sub(u)['free']}")
        r = u.post("/api/v1/advisor/chat", json={"messages": [{"role": "user", "content": "Học phí Harvard bao nhiêu?"}]})
        b = status(r, 200)
        lock = b.get("locked") or {}
        expect(lock.get("reason") == "quota_exhausted", f"locked={lock}")
        resets = lock.get("resetsAt") or ""
        expect(resets.endswith("+07:00"), f"resetsAt={resets}")
        d = parse_dt(resets)
        expect(d.day == 1 and (d.hour, d.minute, d.second) == (0, 0, 0) and d > datetime.now(timezone.utc), f"resetsAt không phải 00:00 mùng 1 tới: {resets}")
        expect(resets == sub(u)["free"]["resetsAt"], f"khác summary: {sub(u)['free']['resetsAt']}")
        r2 = u.post("/api/v1/advisor/chat", json=chat_body({"type": "explain", "schoolCode": "MIT"}))
        expect(status(r2, 200)["locked"]["reason"] == "quota_exhausted", "nút bấm cũng phải quota_exhausted")

    with case("G12", "admin: chat và recommendations không bị khóa premium") as note:
        admin = ADMIN()
        r = admin.post("/api/v1/advisor/chat", json=chat_body({"type": "explain"}))
        locked = r.json().get("locked") if r.status_code == 200 else None
        expect(locked is None, f"admin chat bị khóa: {short(r.text)}")
        r2 = admin.post("/api/v1/recommendations", json={})
        code = r2.json().get("code") if r2.content and "json" in r2.headers.get("content-type", "") else None
        expect(code != "premium_required", f"admin bị premium_required: {short(r2.text)}")
        note(f"chat {r.status_code}, recommendations {r2.status_code} (vai trò, không phải premium)")


def phase_admin():
    admin = ADMIN()
    plans = lambda: status(admin.get("/api/v1/admin/plans"), 200)
    pick = lambda code: next(p for p in plans() if p["code"] == code)
    body = lambda p, **over: {**{k: p[k] for k in ("code", "kind", "name", "description", "priceVnd", "durationDays", "quotaPeriodDays", "tokenQuota",
                                                    "isActive", "displayOrder")}, **over}

    with case("A-list", "GET admin/plans: đủ 5 gói, có tokenQuota (admin được xem)"):
        ps = plans()
        expect(SEED_PLANS <= {p["code"] for p in ps}, [p["code"] for p in ps])
        expect(pick("personal-1m")["tokenQuota"] == 500000 and "activeCycles" in pick("personal-1m") and "orderCount" in pick("personal-1m"), "trường admin")

    with case("A1", "tạo gói trùng mã -> 409 code_taken"):
        p = pick("personal-1m")
        status(admin.post("/api/v1/admin/plans", json=body(p, name="Trùng mã", isActive=False)), 409, "code_taken")

    new_code = f"e2e-{RUN}"
    plan_id = {}
    with case("A5", "tạo, sửa, xóa gói động (14 ngày / chu kỳ 7 ngày): kiểm tra validation và audit plan.create / plan.update / plan.delete") as note:
        draft = {"code": new_code, "kind": "personal", "name": "E2E 2k", "description": "gói thử", "priceVnd": 2000, "durationDays": 14,
                 "quotaPeriodDays": 7, "tokenQuota": 140000, "isActive": False, "displayOrder": 90}
        status(admin.post("/api/v1/admin/plans", json={**draft, "durationDays": 0}), 400, "invalid_duration")
        status(admin.post("/api/v1/admin/plans", json={**draft, "quotaPeriodDays": 15}), 400, "invalid_quota_period")
        created = status(admin.post("/api/v1/admin/plans", json=draft), 201)
        plan_id["id"] = created["id"]
        expect(created["priceVnd"] == 2000 and created["durationDays"] == 14 and created["quotaPeriodDays"] == 7 and created["tokenQuota"] == 140000 and created["quotaMultiplier"] == 5.0 and created["activeCycles"] == 0 and created["orderCount"] == 0, created)
        upd = status(admin.put(f"/api/v1/admin/plans/{created['id']}", json={**draft, "priceVnd": 2500, "code": "bị-bỏ-qua"}), 200)
        expect(upd["priceVnd"] == 2500 and upd["code"] == new_code, f"code không được đổi: {upd}")
        status(admin.delete(f"/api/v1/admin/plans/{created['id']}"), 204)
        expect(new_code not in {p["code"] for p in plans()}, "gói chưa bị xóa")
        rows = sql("select action, detail_json from app.audit_logs where entity_type='subscription_plan' and entity_id=%s order by created_at", created["id"])
        actions = [r["action"] for r in rows]
        expect(actions == ["plan.create", "plan.update", "plan.delete"], f"audit: {actions}")
        upd_detail = rows[1]["detail_json"]
        expect("before" in upd_detail and "after" in upd_detail, f"plan.update thiếu before/after: {short(str(upd_detail))}")
        note(f"audit {actions}")

    with case("A2", "đổi kind của gói -> 409 kind_immutable"):
        p = pick("personal-3m")
        status(admin.put(f"/api/v1/admin/plans/{p['id']}", json=body(p, kind="family")), 409, "kind_immutable")
        expect(pick("personal-3m")["kind"] == "personal", "kind không được đổi")

    with case("A3", "gói Free: đổi giá / ngừng bán / xóa -> 409 free_plan_locked; body thường của frontend vẫn lưu được"):
        f = pick("free")
        status(admin.put(f"/api/v1/admin/plans/{f['id']}", json=body(f, priceVnd=1)), 409, "free_plan_locked")
        status(admin.put(f"/api/v1/admin/plans/{f['id']}", json=body(f, isActive=False)), 409, "free_plan_locked")
        status(admin.delete(f"/api/v1/admin/plans/{f['id']}"), 409, "free_plan_locked")
        ok = status(admin.put(f"/api/v1/admin/plans/{f['id']}", json=body(f, durationDays=None, priceVnd=0, isActive=True)), 200)
        expect(ok["priceVnd"] == 0 and ok["isActive"] is True and ok["durationDays"] is None, ok)

    with case("A4", "xóa gói đã có đơn và kỳ -> 409 plan_in_use"):
        p = pick("personal-1m")
        expect(p["orderCount"] > 0 and p["activeCycles"] > 0, f"gói phải đang được dùng: {p['orderCount']}/{p['activeCycles']}")
        status(admin.delete(f"/api/v1/admin/plans/{p['id']}"), 409, "plan_in_use")

    with case("A6", "admin orders: lọc status=mismatch, tìm theo email và mã đơn, phân trang") as note:
        code = EXP["mm_code"]
        page = status(admin.get("/api/v1/admin/orders", params={"status": "mismatch", "pageSize": 50}), 200)
        expect({"items", "page", "pageSize", "totalCount", "totalPages"} <= set(page), "thiếu trường phân trang")
        expect(page["items"] and all(o["status"] == "mismatch" for o in page["items"]), "chỉ được có đơn mismatch")
        mine = next((o for o in page["items"] if o["orderCode"] == code), None)
        expect(mine and mine["buyerEmail"] == user("umm").email and mine["buyerName"] == "E2E umm", f"đơn {code}: {mine}")
        by_mail = status(admin.get("/api/v1/admin/orders", params={"q": user("umm").email.upper()}), 200)
        expect([o["orderCode"] for o in by_mail["items"]] == [code], f"q=email: {[o['orderCode'] for o in by_mail['items']]}")
        by_code = status(admin.get("/api/v1/admin/orders", params={"q": str(code)}), 200)
        expect([o["orderCode"] for o in by_code["items"]] == [code], "q=mã đơn")
        paged = status(admin.get("/api/v1/admin/orders", params={"pageSize": 2, "page": 1}), 200)
        expect(len(paged["items"]) <= 2 and paged["pageSize"] == 2 and paged["totalCount"] >= len(paged["items"]), "pageSize")
        detail = status(admin.get(f"/api/v1/admin/orders/{code}"), 200)
        expect("payloadJson" in detail and "counterAccount" not in (detail["payloadJson"] or ""), "chi tiết đơn")

    with case("A7★", "mismatch: kích hoạt có ghi chú -> paid + kỳ; không ghi chú -> 400; đơn khác bị từ chối -> cancelled") as note:
        code, buyer = EXP["mm_code"], user("umm")
        status(admin.post(f"/api/v1/admin/orders/{code}/activate", json={"note": "  "}), 400, "note_required")
        done = status(admin.post(f"/api/v1/admin/orders/{code}/activate", json={"note": "Đã đối chiếu sao kê ngân hàng"}), 200)
        expect(done["status"] == "paid" and done["cycle"] and done["adminNote"] == "Đã đối chiếu sao kê ngân hàng", done)
        expect(done["handledBy"] == admin.name, f"handledBy={done['handledBy']}")
        expect(sub(buyer)["tier"] == "premium" and len(cycles(buyer, "personal")) == 1, "người mua phải premium với đúng 1 kỳ")
        expect(wait_notif(buyer, "billing_payment_paid"), "thiếu thông báo billing_payment_paid")
        rej = user("urj", age=23)
        o = buy(rej, "personal-1m")
        pay(o["orderCode"], 5000)
        out = status(admin.post(f"/api/v1/admin/orders/{o['orderCode']}/reject", json={"note": "Chuyển nhầm số tiền"}), 200)
        expect(out["status"] == "cancelled" and out["adminNote"] == "Chuyển nhầm số tiền" and out["cycle"] is None, out)
        expect(sub(rej)["tier"] == "free" and wait_notif(rej, "billing_payment_mismatch", 2), "người mua phải được báo")
        status(admin.post(f"/api/v1/admin/orders/{o['orderCode']}/reject", json={"note": "lần hai"}), 409, "not_mismatch")
        audits = [r["action"] for r in sql("select action from app.audit_logs where entity_type='payment_transaction' and entity_id in (select id::text from app.payment_transactions where order_code in (%s,%s))", code, o["orderCode"])]
        expect("order.activate" in audits and "order.reject" in audits, f"audit đơn: {audits}")

    with case("A8", "recheck: mismatch + payOS báo đủ tiền -> paid; recheck đơn đã trả -> 409") as note:
        u = user("urc", age=24)
        o = buy(u, "personal-1m")
        pay(o["orderCode"], 1000)
        expect(order(u, o["orderCode"])["status"] == "mismatch", "phải mismatch")
        status(ANON.post(f"/api/v1/dev/payos/{o['orderCode']}/status", json={"status": "PAID", "amountPaid": 79000}), 200)
        got = status(admin.post(f"/api/v1/admin/orders/{o['orderCode']}/recheck"), 200)
        expect(got["status"] == "paid" and got["cycle"], got)
        expect(sub(u)["tier"] == "premium", "premium sau recheck")
        status(admin.post(f"/api/v1/admin/orders/{o['orderCode']}/recheck"), 409, "already_paid")
        expect("order.recheck" in [r["action"] for r in sql("select action from app.audit_logs where entity_type='payment_transaction' and entity_id=(select id::text from app.payment_transactions where order_code=%s)", o["orderCode"])], "audit order.recheck")
        EXP["paid_code"] = o["orderCode"]

    with case("A9", "kích hoạt tay đơn đã trả -> 409 already_paid, không thêm kỳ"):
        code = EXP["paid_code"]
        status(admin.post(f"/api/v1/admin/orders/{code}/activate", json={"note": "thử lại"}), 409, "already_paid")
        expect(len(cycles(user("urc"), "personal")) == 1, "chỉ 1 kỳ")

    with case("A10", "hai admin kích hoạt cùng lúc một đơn mismatch: một thắng, kỳ chỉ một") as note:
        u = user("urace", age=25)
        o = buy(u, "personal-1m")
        pay(o["orderCode"], 1000)
        with ThreadPoolExecutor(2) as ex:
            rs = list(ex.map(lambda i: admin.post(f"/api/v1/admin/orders/{o['orderCode']}/activate", json={"note": f"admin {i}"}), range(2)))
        codes = sorted(r.status_code for r in rs)
        expect(codes == [200, 409], f"mã: {codes} {[short(r.text, 80) for r in rs]}")
        expect(len(cycles(u, "personal")) == 1, f"{len(cycles(u, 'personal'))} kỳ")

    with case("P22★", "làn đầy (kỳ active + kỳ chờ) mà khách vẫn trả đơn khác: mismatch ghi chú lane_full, không thêm kỳ; admin kích hoạt -> 409 lane_full, có audit") as note:
        u = new_premium("p22", "personal-1m", age=22)           # kỳ active
        o = buy(u, "personal-1m")                                 # đơn thứ hai còn pending: có kỳ chờ rồi thì POST /orders bị R6 chặn, nên tạo đơn trước
        code = o["orderCode"]
        # Kỳ chờ dựng bằng SQL từ chính kỳ active (kỳ queued không có ngày), thay cho đơn gia hạn mà R6 không cho tạo thêm
        sql("insert into app.user_subscriptions (id,user_id,plan_id,kind,family_group_id,status,starts_at,ends_at,token_quota,plan_name,price_vnd,duration_days,created_at)"
            " select gen_random_uuid(),user_id,plan_id,kind,null,'queued',null,null,token_quota,plan_name,price_vnd,duration_days,now()"
            " from app.user_subscriptions where id=%s", active_cycle(u)["id"])
        before = sorted(r["status"] for r in cycles(u, "personal"))
        expect(before == ["active", "queued"], f"làn phải đầy: {before}")
        expect(pay(code) is True, "webhook dev không được xử lý")
        got = order(u, code)
        expect(got["status"] == "mismatch" and got["cycle"] is None and got["paidAt"] is None, got)
        detail = status(admin.get(f"/api/v1/admin/orders/{code}"), 200)
        expect("lane_full" in (detail["adminNote"] or ""), f"adminNote: {detail['adminNote']}")
        expect(sorted(r["status"] for r in cycles(u, "personal")) == before, "không được thêm kỳ khi làn đầy")
        expect(wait_notif(u, "billing_payment_mismatch"), "người mua phải được báo")
        err = status(admin.post(f"/api/v1/admin/orders/{code}/activate", json={"note": "Khách trả khi làn đầy"}), 409, "lane_full")
        expect(sorted(r["status"] for r in cycles(u, "personal")) == before, "kích hoạt tay khi làn đầy cũng không thêm kỳ")
        expect(order(u, code)["status"] == "mismatch", "đơn vẫn mismatch để admin xử lý sau")
        audit = one("select detail_json from app.audit_logs where action='order.activate' and entity_type='payment_transaction'"
                    " and entity_id=(select id::text from app.payment_transactions where order_code=%s)", code)
        expect(audit and "lane_full" in str(audit["detail_json"]), f"thiếu audit order.activate (lane_full): {audit}")
        note(f"order {code}: {err['code']}")

    with case("A11", "học sinh gọi route admin -> 403; chưa đăng nhập -> 401") as note:
        u = user("u17")
        status(u.get("/api/v1/admin/plans"), 403)
        status(u.get("/api/v1/admin/orders"), 403)
        status(u.post(f"/api/v1/admin/orders/{EXP['mm_code']}/activate", json={"note": "x"}), 403)
        status(u.post("/api/v1/admin/plans", json={}), 403)
        status(ANON.get("/api/v1/admin/orders"), 401)


def phase_family():
    S = {}
    fo1 = user("fo1", "parent", 40)
    fs1, fpa, fpb, fx = user("fs1", age=17), user("fpa", "parent", 35), user("fpb", "parent", 36), user("fx", age=18)

    with case("F1★", "phụ huynh mua family-1m: nhóm được tạo, chủ premium, ghế trống") as note:
        o = buy(fo1, "family-1m")
        expect(o["planKind"] == "family" and o["amountVnd"] == 99000, o)
        expect(pay(o["orderCode"]), "handled")
        f = family(fo1)
        expect(f["isOwner"] is True and f["ownerName"] == "E2E fo1" and f["invites"] == [], f)
        expect(f["cycle"] and f["cycle"]["planName"] == "Gia đình 1 tháng" and f["cycle"]["usedPercent"] == 0 and f["cycle"]["endsAt"], f["cycle"])
        expect(f["seats"] == {"students": {"used": 0, "max": 1}, "parents": {"used": 0, "max": 2}}, f["seats"])
        expect([(m["role"], m["isMe"]) for m in f["members"]] == [("owner", True)], f["members"])
        s = sub(fo1)
        expect(s["tier"] == "premium" and s["family"]["role"] == "owner" and s["personal"] is None, s)
        expect(len(sql("select 1 from app.family_groups where owner_user_id=%s and status='active'", fo1.id)) == 1, "đúng 1 nhóm active")
        S["cycle"] = active_cycle(fo1, "family")
        expect(S["cycle"]["family_group_id"], "kỳ phải gắn nhóm")

    with case("F2★", "mời HS qua email: 202, thư có token, thông báo trong app, trang tra cứu thấy pending") as note:
        n0 = mail_n(fs1.email, "tham gia gói")
        r = invite(fo1, fs1.email, "student")
        expect(status(r, 202)["message"] == "Đã gửi lời mời", r.text)
        S["tok_fs1"] = invite_token(fs1.email, n0)
        n = wait_notif(fs1, "family_invite")
        expect(n and "/family/invite?token=" in n[0]["linkUrl"], f"thông báo: {n}")
        look = status(ANON.post("/api/v1/family/invites/lookup", json={"token": S["tok_fs1"]}), 200)
        expect(look["status"] == "pending" and look["seatRole"] == "student" and look["ownerName"] == "E2E fo1", look)
        f = family(fo1)
        expect(len(f["invites"]) == 1 and f["invites"][0]["email"] == fs1.email and f["invites"][0]["canResendAt"], f["invites"])
        expect(f["seats"]["students"]["used"] == 1, "ghế chờ vẫn được tính")
        S["fs1_invite"] = f["invites"][0]["inviteId"]

    with case("F4", "mời email của chính mình -> 400 invalid_email"):
        status(invite(fo1, fo1.email, "student"), 400, "invalid_email")
        status(invite(fo1, "khong-phai-email", "student"), 400, "invalid_email")

    with case("F6", "mời lại email đang chờ -> 409 invite_exists"):
        status(invite(fo1, fs1.email, "student"), 409, "invite_exists")

    with case("F7", "gửi lại ngay sau khi mời -> 429 resend_too_soon"):
        status(fo1.post(f"/api/v1/family/invites/{S['fs1_invite']}/resend"), 429, "resend_too_soon")

    with case("F5", "ghế đầy (tính cả lời mời chờ): HS thứ hai và phụ huynh thứ ba -> 409 seat_full") as note:
        status(invite(fo1, f"e2e-{RUN}-nobody@e2e.test", "student"), 409, "seat_full")
        for who in (fpa, fx):   # fx là tài khoản học sinh được mời vào ghế phụ huynh (dùng cho F10)
            status(invite(fo1, who.email, "parent"), 202)
        S["tok_fx"] = invite_token(fx.email)
        S["tok_fpa"] = invite_token(fpa.email)
        status(invite(fo1, fpb.email, "parent"), 409, "seat_full")
        f = family(fo1)
        expect(f["seats"]["parents"] == {"used": 2, "max": 2} and len(f["invites"]) == 3, f["seats"])

    with case("F10", "role không khớp ghế (HS nhận ghế phụ huynh) -> 409 role_mismatch"):
        status(accept(fx, S["tok_fx"]), 409, "role_mismatch")

    with case("F8", "chủ hủy lời mời: link chết (404 invite_invalid), ghế được trả, hủy lần hai 409") as note:
        invite_id = next(i["inviteId"] for i in family(fo1)["invites"] if i["email"] == fx.email)
        status(fo1.delete(f"/api/v1/family/invites/{invite_id}"), 204)
        status(ANON.post("/api/v1/family/invites/lookup", json={"token": S["tok_fx"]}), 404, "invite_invalid")
        status(accept(fx, S["tok_fx"]), 404, "invite_invalid")
        status(fo1.delete(f"/api/v1/family/invites/{invite_id}"), 409, "not_pending")
        expect(family(fo1)["seats"]["parents"]["used"] == 1, "ghế phụ huynh phải được trả")
        expect(invite_row(fx.email)["status"] == "cancelled", "dòng phải cancelled")

    with case("F9", "đăng nhập tài khoản khác mở link mời -> 403 wrong_account"):
        status(accept(fpb, S["tok_fs1"]), 403, "wrong_account")
        status(fpb.post("/api/v1/family/invites/decline", json={"token": S["tok_fs1"]}), 403, "wrong_account")

    with case("F2b", "HS chấp nhận lời mời: vào nhóm, thấy tên chủ, chủ được báo") as note:
        n0 = len(notifs(fo1, "family_invite_accepted"))
        f = status(accept(fs1, S["tok_fs1"]), 200)
        expect(f["isOwner"] is False and f["ownerName"] == "E2E fo1" and [m["name"] for m in f["members"]] == ["E2E fo1", "E2E fs1"], f)
        expect(f["members"][1]["isMe"] is True and f["members"][1]["role"] == "student", f["members"])
        expect(wait_notif(fo1, "family_invite_accepted", n0 + 1), "chủ chưa được báo")
        expect(sub(fs1)["tier"] == "premium" and sub(fs1)["family"]["role"] == "member", sub(fs1))
        look = status(ANON.post("/api/v1/family/invites/lookup", json={"token": S["tok_fs1"]}), 200)
        expect(look["status"] == "accepted", f"lookup sau accept: {look}")
        status(accept(fs1, S["tok_fs1"]), 409, "not_pending")

    with case("F31", "thành viên xem nhóm: không có lời mời, không có email") as note:
        f = family(fs1)
        raw = json.dumps(f)
        expect(f["invites"] == [], f"thành viên thấy lời mời: {f['invites']}")
        expect("@" not in raw and "email" not in raw.lower(), f"lộ email: {short(raw)}")
        expect(f["seats"]["students"]["used"] == 1, f["seats"])

    with case("F17★", "thành viên chat: trừ vào pool Family, sharePercent tăng, chủ thấy") as note:
        c = S["cycle"]
        add_usage(fs1, c["token_quota"] // 5, c["id"], "family-1m")   # 20%
        for viewer in (fo1, fs1):
            f = family(viewer)
            expect(member_of(f, "E2E fs1")["sharePercent"] == 20, f"{viewer.name}: {f['members']}")
            expect(member_of(f, "E2E fo1")["sharePercent"] == 0 and f["cycle"]["usedPercent"] == 20, f"{viewer.name}: {f['cycle']}")
        s = sub(fs1)
        expect(s["chargingPool"] == "family" and s["family"]["usedPercent"] == 20 and s["free"]["usedPercent"] == 0, s)

    with case("F22★", "chủ gỡ thành viên: về Free, được báo, token đã dùng vẫn tính") as note:
        mid = member_of(family(fo1), "E2E fs1")["memberId"]
        status(fo1.delete(f"/api/v1/family/members/{mid}"), 204)
        s = sub(fs1)
        expect(s["tier"] == "free" and s["family"] is None and s["chargingPool"] == "free", s)
        status(fs1.get("/api/v1/family"), 404, "not_in_family")
        expect(wait_notif(fs1, "family_member_removed"), "thành viên chưa được báo")
        f = family(fo1)
        expect(f["cycle"]["usedPercent"] == 20 and f["seats"]["students"]["used"] == 0, f"pool phải giữ 20%: {f['cycle']}")
        status(fo1.delete(f"/api/v1/family/members/{mid}"), 404, "member_not_found")
        look = status(ANON.post("/api/v1/family/invites/lookup", json={"token": S["tok_fs1"]}), 404, "invite_invalid")

    with case("F24", "mời lại người đã bị gỡ rồi nhận: pool không reset, share giữ nguyên") as note:
        n0 = mail_n(fs1.email, "tham gia gói")
        status(invite(fo1, fs1.email, "student"), 202)
        status(accept(fs1, invite_token(fs1.email, n0)), 200)
        for viewer in (fo1, fs1):
            f = family(viewer)
            expect(member_of(f, "E2E fs1")["sharePercent"] == 20 and f["cycle"]["usedPercent"] == 20, f"{viewer.name}: {f['members']} {f['cycle']}")

    with case("F34★", "người được mời đang có Personal: vào nhóm vẫn premium, Personal trừ trước, token Personal không tính vào share Family") as note:
        o = buy(fpa, "personal-1m")
        expect(pay(o["orderCode"]), "handled")
        before = {m["name"]: m["sharePercent"] for m in family(fo1)["members"]}
        f = status(accept(fpa, S["tok_fpa"]), 200)
        expect(f["isOwner"] is False and [m["role"] for m in f["members"]] == ["owner", "student", "parent"], f["members"])
        s = sub(fpa)
        expect(s["tier"] == "premium" and s["chargingPool"] == "personal" and s["personal"] and s["family"]["role"] == "member", s)
        pc = active_cycle(fpa, "personal")
        add_usage(fpa, 100_000, pc["id"], "personal-1m")   # 20% của Personal
        f = family(fo1)
        expect(member_of(f, "E2E fpa")["sharePercent"] == 0 and f["cycle"]["usedPercent"] == 20, f"chủ chỉ thấy token trên pool Family: {f['members']}")
        s = sub(fpa)
        expect(s["chargingPool"] == "personal" and s["personal"]["usedPercent"] == 20, s)
        add_usage(fpa, pc["token_quota"], pc["id"], "personal-1m")   # F40: Personal dùng hết -> Family
        s = sub(fpa)
        expect(s["personal"]["exhausted"] is True and s["chargingPool"] == "family" and s["tier"] == "premium", f"F40: {s}")

    with case("F37", "người được mời có đơn gia đình chưa trả -> 409 pending_family_order; hủy đơn rồi nhận được") as note:
        n0 = mail_n(fpb.email, "tham gia gói")
        status(invite(fo1, fpb.email, "parent"), 202)
        tok = invite_token(fpb.email, n0)
        o = buy(fpb, "family-1m")
        status(accept(fpb, tok), 409, "pending_family_order")
        status(fpb.post(f"/api/v1/orders/{o['orderCode']}/cancel"), 200)
        f = status(accept(fpb, tok), 200)
        expect(len(f["members"]) == 4 and f["seats"]["parents"]["used"] == 2, f["seats"])

    with case("F25", "chủ không rời / gỡ chính mình được -> 409 owner_cannot_leave / cannot_remove_owner"):
        status(fo1.post("/api/v1/family/leave"), 409, "owner_cannot_leave")
        status(fo1.delete(f"/api/v1/family/members/{fo1.id}"), 409, "cannot_remove_owner")
        status(fs1.delete(f"/api/v1/family/members/{fo1.id}"), 403, "not_owner")
        status(fs1.post("/api/v1/family/invites", json={"email": "x@e2e.test", "seatRole": "student"}), 403, "not_owner")

    with case("F23", "thành viên rời nhóm: 204, chủ được báo, về Free, ghế trống") as note:
        n0 = len(notifs(fo1, "family_member_left"))
        status(fs1.post("/api/v1/family/leave"), 204)
        expect(sub(fs1)["tier"] == "free", sub(fs1))
        expect(wait_notif(fo1, "family_member_left", n0 + 1), "chủ chưa được báo")
        expect(family(fo1)["seats"]["students"]["used"] == 0, "ghế HS phải trống")
        status(fs1.post("/api/v1/family/leave"), 404, "not_in_family")

    with case("F13", "lời mời quá hạn 7 ngày: job đánh dấu expired, ghế được trả") as note:
        stu = user("fsexp", age=19)
        status(invite(fo1, stu.email, "student"), 202)
        expect(family(fo1)["seats"]["students"]["used"] == 1, "ghế chờ")
        sql("update app.family_members set expires_at=now()-interval '1 hour' where invited_email=%s and status='pending'", stu.email)
        expect(eventually(lambda: invite_row(stu.email)["status"] == "expired", 20), f"trạng thái: {invite_row(stu.email)['status']}")
        expect(family(fo1)["seats"]["students"]["used"] == 0 and family(fo1)["invites"] == [], "ghế phải trống, không còn lời mời")

    with case("F7b", "gửi lại sau 60s: 202, token mới (link cũ chết), mail mới; lần thứ 6 trong 24h -> 429") as note:
        stu = user("fsre", age=20)
        n0 = mail_n(stu.email, "tham gia gói")
        status(invite(fo1, stu.email, "student"), 202)
        old_tok = invite_token(stu.email, n0)
        inv = invite_row(stu.email)
        status(fo1.post(f"/api/v1/family/invites/{inv['id']}/resend"), 429, "resend_too_soon")
        sql("update app.family_members set last_invite_sent_at=now()-interval '2 minutes' where id=%s", inv["id"])
        status(fo1.post(f"/api/v1/family/invites/{inv['id']}/resend"), 202)
        new_tok = invite_token(stu.email, n0 + 1)
        expect(new_tok != old_tok, "token phải đổi")
        status(ANON.post("/api/v1/family/invites/lookup", json={"token": old_tok}), 404, "invite_invalid")
        expect(status(ANON.post("/api/v1/family/invites/lookup", json={"token": new_tok}), 200)["status"] == "pending", "token mới dùng được")
        sql("update app.family_members set invite_sent_count=5, last_invite_sent_at=now()-interval '2 minutes' where id=%s", inv["id"])
        status(fo1.post(f"/api/v1/family/invites/{inv['id']}/resend"), 429, "resend_too_soon")
        status(fo1.delete(f"/api/v1/family/invites/{inv['id']}"), 204)

    with case("F38", "đơn Family bị hủy rồi trả muộn khi đã là thành viên (C6, C15): gói riêng chạy, rời nhóm cũ, cả hai được báo") as note:
        fs4 = user("fs4", age=19)
        o = buy(fs4, "family-1m")
        status(fs4.post(f"/api/v1/orders/{o['orderCode']}/cancel"), 200)
        n0 = mail_n(fs4.email, "tham gia gói")
        status(invite(fo1, fs4.email, "student"), 202)
        status(accept(fs4, invite_token(fs4.email, n0)), 200)
        owner_left = len(notifs(fo1, "family_member_left"))
        expect(pay(o["orderCode"]), "webhook muộn không được xử lý")
        f = family(fs4)
        expect(f["isOwner"] is True and [m["name"] for m in f["members"]] == ["E2E fs4"], f"fs4 phải là chủ nhóm riêng: {f['members']}")
        expect("E2E fs4" not in [m["name"] for m in family(fo1)["members"]] and family(fo1)["seats"]["students"]["used"] == 0, "fs4 phải rời nhóm cũ")
        row = one("select status, ended_reason from app.family_members where user_id=%s order by created_at limit 1", fs4.id)
        expect(row["status"] == "ended" and row["ended_reason"] == "joined_own_family", f"{row}")
        expect(wait_notif(fo1, "family_member_left", owner_left + 1) and wait_notif(fs4, "family_member_left"), "cả hai phải được báo")
        expect(sub(fs4)["family"]["role"] == "owner", sub(fs4))

    # ---- gói Gia đình thứ hai: F11, F36, F27
    fo2, fs3 = user("fo2", "parent", 41), user("fs3", age=18)
    with case("F11", "người được mời đang ở nhóm khác còn hạn -> 409 already_in_family") as note:
        expect(pay(buy(fo2, "family-1m")["orderCode"]), "fo2 mua gói")
        n0 = mail_n(fpa.email, "tham gia gói")
        status(invite(fo2, fpa.email, "parent"), 202)
        status(accept(fpa, invite_token(fpa.email, n0)), 409, "already_in_family")

    with case("F36", "người được mời đang là chủ một gói Gia đình còn hạn -> 409 already_in_family") as note:
        n0 = mail_n(fo1.email, "tham gia gói")
        status(invite(fo2, fo1.email, "parent"), 202)
        status(accept(fo1, invite_token(fo1.email, n0)), 409, "already_in_family")
        S["fo2_pending"] = [fpa.email, fo1.email]

    with case("F27★", "gói Gia đình hết hạn không có kỳ chờ: nhóm và thành viên kết thúc, mọi người được báo, lời mời chờ kết thúc") as note:
        status(invite(fo2, fs3.email, "student"), 202)
        status(accept(fs3, invite_token(fs3.email)), 200)
        expect(sub(fs3)["tier"] == "premium", "fs3 phải premium trước khi hết hạn")
        c = active_cycle(fo2, "family")
        set_cycle_dates(c["id"], -31 * 86400, -3600)
        expect(eventually(lambda: sub(fs3)["tier"] == "free", 20), f"fs3 vẫn {sub(fs3)['tier']}")
        status(fs3.get("/api/v1/family"), 404, "not_in_family")
        status(fo2.get("/api/v1/family"), 404, "not_in_family")
        expect(sub(fo2)["tier"] == "free", "chủ cũng về free")
        expect(wait_notif(fs3, "family_ended") and wait_notif(fo2, "family_ended"), "thiếu thông báo family_ended")
        settle()
        expect(len(notifs(fs3, "family_ended")) == 1 and len(notifs(fo2, "family_ended")) == 1, "mỗi người đúng một thông báo")
        g = one("select status, ended_reason from app.family_groups where owner_user_id=%s order by created_at desc limit 1", fo2.id)
        expect(g["status"] == "ended" and g["ended_reason"] == "family_lapsed", g)
        left = sql("select status from app.family_members where family_group_id=(select id from app.family_groups where owner_user_id=%s order by created_at desc limit 1)", fo2.id)
        expect(left and all(m["status"] == "ended" for m in left), f"thành viên/lời mời phải ended: {[m['status'] for m in left]}")
        expect(sorted(r["status"] for r in sql("select status from app.user_subscriptions where user_id=%s", fo2.id)) == ["ended"], "kỳ family đã ended")

    # ---- xóa tài khoản
    with case("D1", "thành viên tự xóa tài khoản: nhóm bỏ họ, ghế trống, chủ được báo") as note:
        n0 = len(notifs(fo1, "family_member_left"))
        expect(family(fo1)["seats"]["parents"]["used"] == 2, "trước khi xóa: 2 ghế phụ huynh")
        r = fpb.delete("/api/v1/auth/me")
        expect(r.status_code in (200, 204), f"DELETE /auth/me -> {r.status_code} {short(r.text)}")
        expect(wait_notif(fo1, "family_member_left", n0 + 1), "chủ chưa được báo")
        f = family(fo1)
        expect("E2E fpb" not in [m["name"] for m in f["members"]] and f["seats"]["parents"]["used"] == 1, f"{f['members']}")
        row = one("select status, ended_reason from app.family_members where user_id=%s order by created_at desc limit 1", fpb.id)
        expect(row["status"] == "ended" and row["ended_reason"] == "member_deleted", f"{row}")

    with case("D3", "chủ xóa tài khoản: gói vẫn chạy cho thành viên (O1)") as note:
        r = fo1.delete("/api/v1/auth/me")
        expect(r.status_code in (200, 204), f"DELETE /auth/me -> {r.status_code} {short(r.text)}")
        f = family(fpa)
        expect(f["cycle"] and f["isOwner"] is False and len(f["members"]) == 2, f)
        s = sub(fpa)
        expect(s["tier"] == "premium" and s["family"] and s["family"]["role"] == "member", s)
        g = one("select status from app.family_groups where owner_user_id=%s order by created_at desc limit 1", fo1.id)
        expect(g["status"] == "active" and active_cycle(fo1, "family"), "nhóm và kỳ phải còn active")
        settle()
        expect(sub(fpa)["tier"] == "premium", "sau vài chu kỳ job vẫn premium")


def phase_deferred():
    with case("P11★", "đơn không trả hết hạn (hạn + ân hạn) do job: status expired") as note:
        u, code = EXP["user"], EXP["code"]
        deadline = EXP["expires"] + timedelta(minutes=GRACE_MIN, seconds=90)
        wait = (deadline - datetime.now(timezone.utc)).total_seconds()
        if wait > 0:
            print(f"     ... chờ đơn {code} hết hạn (~{wait:.0f}s)", flush=True)
        done = eventually(lambda: order(u, code)["status"] == "expired", timeout=max(wait, 1), every=3)
        expect(done, f"đơn vẫn {order(u, code)['status']} sau {GRACE_MIN + 1.5} phút ân hạn")
        expect(sub(u)["pendingOrderCode"] is None, "đơn hết hạn không còn là đơn chờ")
        note(f"order {code}")

    with case("P16", "thanh toán đúng tiền sau khi đơn hết hạn vẫn kích hoạt (C6)") as note:
        u, code = EXP["user"], EXP["code"]
        expect(pay(code), "handled")
        got = order(u, code)
        expect(got["status"] == "paid" and got["cycle"] and sub(u)["tier"] == "premium", got)

    with case("T1", "không phản hồi khách nào chứa trường token / quota") as note:
        expect(SCANNED[0] > 100, f"chỉ quét {SCANNED[0]} phản hồi")
        expect(not TOKEN_LEAKS, f"{len(TOKEN_LEAKS)} chỗ lộ: {sorted(set(TOKEN_LEAKS))[:6]}")
        note(f"quét {SCANNED[0]} phản hồi của khách")


_admin = []


def ADMIN():
    if not _admin:
        u = user("admin", age=30)
        make_admin(u)
        _admin.append(u)
    return _admin[0]


def main():
    print(f"E2E gói dịch vụ  run={RUN}  api={BASE}  thư={mb.MAIL_DIR}", flush=True)
    health = httpx.get(f"{BASE}/api/v1/plans", timeout=10)
    if health.status_code != 200:
        sys.exit(f"API không trả lời ở {BASE}: {health.status_code}")
    started = time.time()
    for phase in (phase_plans_and_setup, phase_purchase, phase_renewal, phase_usage_notifications, phase_gating, phase_admin, phase_family,
                  phase_deferred):
        try:
            phase()
        except Exception as e:  # noqa: BLE001 - lỗi ngoài ca: ghi lại và chạy tiếp nhóm sau
            RESULTS.append((f"SETUP:{phase.__name__}", "FAIL", f"{type(e).__name__}: {e}"))
            print(f"FAIL SETUP:{phase.__name__} {e}", flush=True)

    failed = [r for r in RESULTS if r[1] != "PASS"]
    lines = [f"# Kết quả E2E gói dịch vụ", "", f"- Run `{RUN}` lúc {time.strftime('%Y-%m-%d %H:%M:%S')}, API {BASE}, {time.time() - started:.0f}s",
             f"- {len(RESULTS) - len(failed)} đạt, {len(failed)} lỗi, tổng {len(RESULTS)} ca", "", "| Mã | Kết quả | Chi tiết |", "|---|---|---|"]
    lines += [f"| {cid} | {res} | {detail.replace('|', '/')} |" for cid, res, detail in RESULTS]
    with open(os.path.join(HERE, "last-run.md"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print("\n" + "=" * 100)
    for cid, res, detail in RESULTS:
        print(f"{cid:10} {res:5} {detail[:150]}")
    print("=" * 100)
    print(f"{len(RESULTS) - len(failed)} đạt, {len(failed)} lỗi, tổng {len(RESULTS)} ca ({time.time() - started:.0f}s). Chi tiết: scripts/e2e/last-run.md")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
