"""Lượt ① bậc ngoại khóa: gửi đúng dữ liệu, kiểm tra JSON, gọi lại khi sai, endpoint /recommend/ec."""

import json

import pytest
from fastapi.testclient import TestClient

from app.api.deps import get_ec_reader
from app.main import create_app
from app.schemas.ec import EcRequest, ec_schema
from app.services.ec_reader import EcError, EcReader, tier_of
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

# Hồ sơ An trong tài liệu
AN = {
    "act": [
        {"text": "Đội trưởng CLB Robotics của trường, dẫn đội 2 năm", "scope": 1, "months": 24},
        {"text": "Thành viên nhóm tình nguyện dạy học ở Hà Nội", "scope": 3, "months": 12},
    ],
    "awards": [{"text": "Giải Nhì Olympic Tin học", "level": 3}],
}
# Đầu ra đúng trong tài liệu
AN_RESULT = {
    "act": [{"role": 0.8, "scope": 1, "q": 0.61}, {"role": 0.3, "scope": 3, "q": 0.47}],
    "tong_hd": 1.08,
    "bonus": 0.15,
    "m": 0.31,
    "tier": 3,
}


def request(data=AN):
    return EcRequest.model_validate(data)


async def test_returns_tier_and_sends_skill_as_system():
    llm = FakeLLM(AN_RESULT)
    res = await EcReader(llm, "qwen3:8b").read(request())

    assert res.tier == 3 and res.m == 0.31
    assert res.skill_version == "1.1" and res.model == "qwen3:8b"
    messages = llm.calls[0]["messages"]
    assert "SKILL NGOẠI KHÓA" in messages[0]["content"]
    sent = json.loads(messages[1]["content"])
    assert sent["act"][0]["scope"] == 1 and sent["awards"][0]["level"] == 3
    assert llm.calls[0]["temperature"] == 0


async def test_no_activity_and_no_award_is_tier_1_without_calling_ai():
    llm = FakeLLM()
    res = await EcReader(llm, "m").read(request({"act": [], "awards": []}))
    assert (res.tier, res.m) == (1, 0)
    assert llm.calls == []


async def test_ai_raising_scope_is_rejected_and_retried():
    raised = {**AN_RESULT, "act": [{"role": 0.8, "scope": 4, "q": 0.61}, AN_RESULT["act"][1]]}  # 1 → 4
    llm = FakeLLM(raised, AN_RESULT)
    res = await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2
    assert res.act[0].scope == 1


async def test_ai_lowering_scope_is_allowed():
    lowered = {**AN_RESULT, "act": [AN_RESULT["act"][0], {"role": 0.3, "scope": 1, "q": 0.31}]}  # 3 → 1
    res = await EcReader(FakeLLM(lowered), "m").read(request())
    assert res.act[1].scope == 1


async def test_tier_not_matching_m_is_rejected():
    llm = FakeLLM({**AN_RESULT, "tier": 4}, AN_RESULT)  # m 0.31 phải là bậc 3
    res = await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2 and res.tier == 3


async def test_wrong_activity_count_is_rejected():
    llm = FakeLLM({**AN_RESULT, "act": AN_RESULT["act"][:1]}, AN_RESULT)
    await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2


async def test_bonus_over_half_is_rejected():
    llm = FakeLLM({**AN_RESULT, "bonus": 0.75}, AN_RESULT)
    await EcReader(llm, "m").read(request())
    assert len(llm.calls) == 2


async def test_failing_twice_raises():
    llm = FakeLLM(OllamaError("timeout"), {"act": []})
    with pytest.raises(EcError):
        await EcReader(llm, "m").read(request())


@pytest.mark.parametrize(
    ("m", "tier"), [(0, 1), (0.09, 1), (0.10, 2), (0.31, 3), (0.45, 4), (0.64, 4), (0.65, 5), (1, 5)]
)
def test_tier_cutoffs(m, tier):
    assert tier_of(m) == tier


def test_schema_fixes_activity_count_and_roles():
    schema = ec_schema(2)
    act = schema["properties"]["act"]
    assert act["minItems"] == act["maxItems"] == 2
    assert act["items"]["properties"]["role"]["enum"] == [1.0, 0.8, 0.6, 0.3]
    assert list(schema["properties"]) == ["act", "tong_hd", "bonus", "m", "tier"]


def test_endpoint_returns_tier():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(FakeLLM(AN_RESULT), "m")
    resp = TestClient(app).post("/recommend/ec", json=AN)
    assert resp.status_code == 200
    assert resp.json()["tier"] == 3


def test_endpoint_returns_502_when_ai_fails():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(
        FakeLLM(OllamaError("x"), OllamaError("x")), "m"
    )
    assert TestClient(app).post("/recommend/ec", json=AN).status_code == 502


def test_endpoint_rejects_scope_out_of_range():
    app = create_app()
    app.dependency_overrides[get_ec_reader] = lambda: EcReader(FakeLLM(), "m")
    bad = {"act": [{"text": "CLB", "scope": 9, "months": 6}], "awards": []}
    assert TestClient(app).post("/recommend/ec", json=bad).status_code == 422
