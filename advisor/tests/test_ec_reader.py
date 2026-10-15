"""Bậc ngoại khóa: AI chỉ đọc vai trò, phạm vi; Python tính q, m, bậc; AI lỗi thì dùng vai trò ở form."""

import json

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_ec_reader
from app.main import create_app
from app.schemas.ec import EcRequest, ec_schema
from app.services.ec_reader import EcReader, activity_q, tier_of
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

# Hồ sơ An trong GOI_Y_TRUONG_TONG_QUAN
AN = {
    "act": [
        {
            "text": "Đội trưởng CLB Robotics của trường, dẫn đội 2 năm",
            "scope": 1,
            "months": 24,
            "role": "leader",
        },
        {"text": "Thành viên nhóm tình nguyện dạy học ở Hà Nội", "scope": 3, "months": 12, "role": "member"},
    ],
    "awards": [{"text": "Giải Nhì Olympic Tin học", "level": 3}],
}
AN_READ = {"act": [{"role": 0.8, "scope": 1}, {"role": 0.3, "scope": 3}]}  # AI chỉ trả phần đọc hiểu


def request(data=AN):
    return EcRequest.model_validate(data)


async def test_an_matches_document_q_m_and_tier():
    llm = FakeLLM(AN_READ)
    res = await EcReader(llm, "qwen3:8b").read(request())

    # Tài liệu: q = 0,61 và 0,47; thưởng giải tỉnh 0,15; m = (1,08 + 0,15)/4 = 0,31 → bậc 3
    assert [a.q for a in res.act] == [0.61, 0.47]
    assert (res.tong_hd, res.bonus, res.m, res.tier) == (1.08, 0.15, 0.31, 3)
    assert res.ai_used and res.skill_version == "2.0" and res.model == "qwen3:8b"


async def test_ai_gets_text_scope_months_but_not_form_role():
    llm = FakeLLM(AN_READ)
    await EcReader(llm, "m").read(request())
    system, user = llm.calls[0]["messages"]
    assert "SKILL ĐỌC HOẠT ĐỘNG" in system["content"]
    sent = json.loads(user["content"])
    assert sent == {
        "act": [
            {"text": AN["act"][0]["text"], "scope": 1, "months": 24},
            {"text": AN["act"][1]["text"], "scope": 3, "months": 12},
        ]
    }  # không có "role", "awards"


async def test_ai_lowering_scope_changes_q():
    lowered = {"act": [{"role": 0.8, "scope": 1}, {"role": 0.3, "scope": 1}]}  # 3 → 1: text không đủ cấp tỉnh
    res = await EcReader(FakeLLM(lowered), "m").read(request())
    assert res.act[1].scope == 1 and res.act[1].q == activity_q(0.3, 1, 12)


async def test_ai_raising_scope_is_rejected_and_retried():
    raised = {"act": [{"role": 0.8, "scope": 4}, {"role": 0.3, "scope": 3}]}  # 1 → 4: không được nâng
    llm = FakeLLM(raised, AN_READ)
    res = await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2 and res.act[0].scope == 1 and res.ai_used


async def test_wrong_activity_count_is_retried():
    llm = FakeLLM({"act": AN_READ["act"][:1]}, AN_READ)
    await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2


async def test_ai_failing_twice_uses_form_roles_instead_of_error():
    llm = FakeLLM(OllamaError("timeout"), {"act": []})
    res = await EcReader(llm, "m").read(request())
    # leader 0,8 và member 0,3 từ form, phạm vi như học sinh khai → cùng kết quả với tài liệu
    assert (res.tier, res.m, res.ai_used) == (3, 0.31, False)


async def test_form_role_missing_counts_as_member():
    data = {"act": [{"text": "CLB cờ vua", "scope": 1, "months": 24}], "awards": []}
    res = await EcReader(FakeLLM(OllamaError("x"), OllamaError("x")), "m").read(request(data))
    assert res.act[0].role == 0.3


async def test_no_activity_and_no_award_is_tier_1_without_calling_ai():
    llm = FakeLLM()
    res = await EcReader(llm, "m").read(request({"act": [], "awards": []}))
    assert (res.tier, res.m, res.ai_used) == (1, 0, False)
    assert llm.calls == []


async def test_awards_only_needs_no_ai():
    llm = FakeLLM()
    data = {"act": [], "awards": [{"text": f"Giải {i}", "level": 5} for i in range(4)]}
    res = await EcReader(llm, "m").read(request(data))
    assert res.bonus == 0.5 and llm.calls == []  # 3 giải quốc tế = 0,75 nhưng tối đa 0,5


def test_only_top_four_activities_count():
    from app.schemas.ec import EcActivityRead
    from app.services.ec_reader import score_ec

    req = request({"act": [{"text": "x", "scope": 5, "months": 24}] * 5, "awards": []})
    reads = [EcActivityRead(role=1.0, scope=5)] * 5  # q = 1,0 mỗi hoạt động
    _, tong_hd, _, m, tier = score_ec(reads, req)
    assert (tong_hd, m, tier) == (4.0, 1.0, 5)


@pytest.mark.parametrize(
    ("role", "scope", "months", "q"), [(0.8, 1, 24, 0.61), (0.3, 3, 12, 0.47), (1.0, 5, 36, 1.0)]
)
def test_activity_q_formula(role, scope, months, q):
    assert activity_q(role, scope, months) == q


@pytest.mark.parametrize(
    ("m", "tier"), [(0, 1), (0.09, 1), (0.10, 2), (0.31, 3), (0.45, 4), (0.64, 4), (0.65, 5), (1, 5)]
)
def test_tier_cutoffs(m, tier):
    assert tier_of(m) == tier


def test_schema_asks_only_role_and_scope():
    item = ec_schema(2)["properties"]["act"]
    assert item["minItems"] == item["maxItems"] == 2
    assert list(item["items"]["properties"]) == ["role", "scope"]


def test_endpoint_returns_tier():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(FakeLLM(AN_READ), "m")
    resp = TestClient(app).post("/recommend/ec", json=AN)
    assert resp.status_code == 200
    assert resp.json()["tier"] == 3


def test_endpoint_still_returns_200_when_ai_fails():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(
        FakeLLM(OllamaError("x"), OllamaError("x")), "m"
    )
    resp = TestClient(app).post("/recommend/ec", json=AN)
    assert resp.status_code == 200 and resp.json()["ai_used"] is False


def test_endpoint_rejects_scope_out_of_range():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(FakeLLM(), "m")
    bad = {"act": [{"text": "CLB", "scope": 9, "months": 6}], "awards": []}
    assert TestClient(app).post("/recommend/ec", json=bad).status_code == 422
