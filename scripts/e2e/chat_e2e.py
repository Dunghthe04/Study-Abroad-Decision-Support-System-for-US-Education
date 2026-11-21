"""Kiểm tra chat gõ chữ với advisor thật (SLM + Claude): Free hỏi kiến thức, Free hỏi tư vấn, Premium hỏi tư vấn, luồng stream.

Cần API (run-api.sh) và advisor chạy ở 127.0.0.1:8000 (Advisor__BaseUrl mặc định của run-api.sh), advisor dùng CSDL usas_e2e.
"""
import json, os, sys, time
import httpx

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ.setdefault("SINK_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mail"))
import mailbox  # noqa: E402

B = os.environ.get("E2E_BASE", "http://127.0.0.1:5095").rstrip("/") + "/api/v1"
PW = "E2e#Passw0rd!"
run = str(int(time.time()))[-6:]
results = []


def check(case, ok, detail):
    results.append((case, "PASS" if ok else "FAIL", detail))
    print(f"{case:<6} {'PASS' if ok else 'FAIL'}  {detail}", flush=True)


def user(tag, dob="2009-02-02"):
    email = f"chat-{run}-{tag}@e2e.test"
    c = httpx.Client(timeout=180, headers={"X-Forwarded-For": f"10.7.{run[-2:]}.{len(tag)}"})
    r = c.post(f"{B}/auth/register", json={"email": email, "password": PW, "fullName": f"Chat {tag}", "role": "student", "dateOfBirth": dob})
    assert r.status_code in (200, 201), r.text
    r = c.post(f"{B}/auth/verify-email", json={"email": email, "otpCode": mailbox.otp(mailbox.wait_for(email, "", 0, 30))})
    assert r.status_code == 200, r.text
    r = c.post(f"{B}/auth/login", json={"email": email, "password": PW})
    assert r.status_code == 200, r.text
    return c


def chat(c, text):
    r = c.post(f"{B}/advisor/chat", json={"messages": [{"role": "user", "content": text}], "studyLevel": "undergraduate"})
    return r.status_code, (r.json() if r.headers.get("content-type", "").startswith("application/json") else r.text)


free = user("free")
st, body = chat(free, "Kỳ thi SAT gồm những phần nào và thang điểm bao nhiêu?")
locked = body.get("locked") if isinstance(body, dict) else None
check("G1", st == 200 and not locked and len(body.get("answer", "")) > 20, f"Free hỏi kiến thức: {st}, locked={locked}, answer[:80]={str(body.get('answer',''))[:80]!r}")

st, body = chat(free, "Dựa vào hồ sơ của em, gợi ý cho em danh sách trường đại học ở Mỹ phù hợp nhé.")
locked = body.get("locked") if isinstance(body, dict) else None
check("G2", st == 200 and isinstance(locked, dict) and locked.get("reason") == "premium_required", f"Free hỏi tư vấn: {st}, locked={locked}, answer[:80]={str(body.get('answer',''))[:80]!r}")

sub = free.get(f"{B}/subscription").json()
check("G1b", sub["tier"] == "free" and sub["free"]["usedPercent"] >= 0 and "tokens" not in json.dumps(sub).lower(), f"Free allowance used {sub['free']['usedPercent']}% (Claude tokens of the knowledge answer counted)")

# Stream: Free advice question -> final event carries locked
lines = []
with free.stream("POST", f"{B}/advisor/chat/stream", json={"messages": [{"role": "user", "content": "So sánh giúp em cơ hội đỗ của em vào MIT và Stanford."}], "studyLevel": "undergraduate"}) as r:
    for line in r.iter_lines():
        if line.strip():
            lines.append(json.loads(line))
final = next((l for l in lines if l.get("type") == "final"), None)
lk = final and final["response"].get("locked")
check("G13", r.status_code == 200 and final is not None and isinstance(lk, dict), f"stream final locked={lk}, events={[l.get('type') for l in lines]}")

prem = user("prem")
o = prem.post(f"{B}/orders", json={"planCode": "personal-1m"}).json()
httpx.post(f"{B}/dev/payos/{o['orderCode']}/pay", json={})
time.sleep(2)
sub = prem.get(f"{B}/subscription").json()
check("P1", sub["tier"] == "premium", f"premium after pay: tier={sub['tier']}")
st, body = chat(prem, "Dựa vào hồ sơ của em, gợi ý cho em danh sách trường đại học ở Mỹ phù hợp nhé.")
locked = body.get("locked") if isinstance(body, dict) else None
check("G-prem", st == 200 and not locked, f"Premium hỏi tư vấn: {st}, locked={locked}, answer[:80]={str(body.get('answer',''))[:80]!r}")
sub2 = prem.get(f"{B}/subscription").json()
check("T-prem", sub2["chargingPool"] == "personal", f"Premium charged pool={sub2['chargingPool']}, personal used {sub2['personal']['usedPercent'] if sub2.get('personal') else None}%")

fails = [r for r in results if r[1] == "FAIL"]
print(f"\n{len(results) - len(fails)} pass, {len(fails)} fail")
sys.exit(1 if fails else 0)
