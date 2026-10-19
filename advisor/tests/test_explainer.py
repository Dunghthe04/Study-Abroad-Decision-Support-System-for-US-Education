from app.schemas.explain import ExplainRequest
from app.services.explainer import Explainer, build_facts
from tests.fakes import FakeLLM

# Hồ sơ mẫu: An và Colgate. why là các mã lý do mà cloud đã chọn.
COLGATE = {
    "student": {"gpa": 3.6, "sat": 1350, "budget": 50000, "tier": 3, "major": "Computer and information sciences"},
    "school": {
        "name": "Colgate University", "state": "NY", "control": "private",
        "g2": "RF", "blk": "AID",
        "why": ["GPA_LOW", "SAT_BELOW", "NEED_AID", "INTL_LOW"],
        "acc": 0.175, "sat25": 1450, "sat75": 1510, "pg": 17, "ps": 0, "cost": 95610, "gap": 45610,
    },
}

# Câu trả lời đúng khuôn mà Qwen giả sẽ trả
GOOD = {
    "summary": "Colgate là trường rất khó vào với hồ sơ hiện tại của bạn.",
    "strengths": [],
    "improvements": ["Nâng SAT lên khoảng 1450."],
}


def test_facts_come_from_why_codes_with_saved_numbers():
    facts = build_facts(ExplainRequest.model_validate(COLGATE))

    assert len(facts) == 4  # 4 mã why → 4 câu
    assert "17%" in facts[0]  # GPA_LOW lấy pg = 17
    assert "thiếu khoảng 100 điểm" in facts[1]  # SAT_BELOW: 1450 - 1350
    assert "45.610" in facts[2]  # NEED_AID lấy gap, có dấu chấm hàng nghìn


async def test_qwen_receives_facts_and_returns_ok():
    llm = FakeLLM(GOOD)
    res = await Explainer(llm, "qwen3:8b").explain(ExplainRequest.model_validate(COLGATE))

    assert res.status == "OK"
    assert len(llm.calls) == 1  # trả đúng khuôn thì chỉ gọi 1 lần
    sent = llm.calls[0]["messages"][1]["content"]  # message "user" gửi cho Qwen
    assert "Khó vào xa" in sent  # g2 = RF đã đổi sang nhãn tiếng Việt
    assert "thấp hơn mốc 25%" in sent  # facts đã được gửi cho Qwen


async def test_failed_when_qwen_breaks_shape_twice():
    llm = FakeLLM({"summary": 1}, {"summary": 1})  # cả 2 lần đều sai khuôn
    res = await Explainer(llm, "qwen3:8b").explain(ExplainRequest.model_validate(COLGATE))

    assert res.status == "FAILED"
    assert res.summary == ""
    assert len(llm.calls) == 2  # gọi lại đúng 1 lần
