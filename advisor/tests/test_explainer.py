from app.schemas.explain import ExplainRequest
from app.services.explainer import Explainer, build_facts, build_user
from tests.fakes import FakeLLM

# Hồ sơ mẫu: An và Colgate. why là các mã lý do mà cloud đã chọn.
COLGATE = {
    "student": {
        "gpa": 3.6,
        "sat": 1350,
        "budget": 50000,
        "tier": 3,
        "major": "Computer and information sciences",
    },
    "school": {
        "name": "Colgate University",
        "state": "NY",
        "control": "private",
        "g2": "RF",
        "blk": "AID",
        "why": ["GPA_LOW", "SAT_BELOW", "NEED_AID", "INTL_LOW"],
        "acc": 0.175,
        "sat25": 1450,
        "sat75": 1510,
        "pg": 17,
        "ps": 0,
        "cost": 95610,
        "gap": 45610,
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


# ---- UC-153: AI diễn giải điểm phù hợp, giới hạn theo độ chọn lọc

FIT = {
    "total": 0.61,
    "priorityAdjusted": True,
    "parts": [
        {"key": "position", "score": 0.4, "weight": 0.3},
        {"key": "budget", "score": 0.9, "weight": 0.25},
        {"key": "quality", "score": None, "weight": 0.2},
        {"key": "aid", "score": 0.4, "weight": 0.15},
        {"key": "english", "score": 1, "weight": 0.1},
    ],
}


def with_school(**more):
    return ExplainRequest.model_validate({**COLGATE, "school": {**COLGATE["school"], **more}})


def test_the_school_may_carry_the_selectivity_codes_and_the_fit_the_backend_sends():
    # Trước đây advisor từ chối LOW_RATE, RATE_CAP, WEAK_CAP nên cả lượt giải thích của trường rất chọn lọc hỏng
    req = with_school(why=["LOW_RATE", "RATE_CAP", "WEAK_CAP", "GPA_LOW"], fit=FIT)

    assert req.school.fit.priority_adjusted is True
    facts = build_facts(req)
    assert len(facts) == 4
    assert "dưới 15%" in facts[0] and "không đổi nhóm" in facts[0]
    assert "dưới 50%" in facts[1]
    assert "thấp hơn mốc 25%" in facts[2]


def test_the_fit_goes_to_the_ai_on_a_100_scale_with_the_lowest_parts_found_by_code():
    sent = build_user(with_school(fit=FIT))["fit"]

    assert sent["total"] == 61
    assert [(p["name"], p["score"], p["weight"]) for p in sent["parts"]][:2] == [
        ("vừa tầm học thuật", 40, 0.3),
        ("ngân sách", 90, 0.25),
    ]
    # hai phần cùng thấp nhất (40); phần thiếu số không tính
    assert sent["lowest"] == ["vừa tầm học thuật", "học bổng"]
    assert sent["missing"] == ["chất lượng trường"]
    assert sent["priorityAdjusted"] is True


def test_without_a_fit_the_ai_is_not_given_one():
    assert "fit" not in build_user(ExplainRequest.model_validate(COLGATE))


def test_a_fit_with_no_scored_part_has_no_lowest():
    sent = build_user(
        with_school(fit={"total": None, "parts": [{"key": "aid", "score": None, "weight": 0.15}]})
    )["fit"]

    assert sent["total"] is None and "lowest" not in sent
    assert sent["missing"] == ["học bổng"]
