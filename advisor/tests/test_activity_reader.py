from app.schemas.extracurricular import ActivityInput, ExtracurricularRequest
from app.services.activity_reader import ActivityReader
from app.services.ollama import OllamaError
from tests.fakes import FakeLLM

ROBOTICS = ActivityInput(
    id="a1",
    name="CLB Robotics",
    role="Chủ nhiệm, người sáng lập",
    organization="THPT Chu Văn An",
    description="Lập CLB 30 thành viên, đạt giải Nhì cấp tỉnh",
    impact_level=3,
    months=24,
)
UNICEF = ActivityInput(
    id="a2", name="Tình nguyện mùa hè", role="TNV", organization="UNICEF Việt Nam", impact_level=2, months=2
)


def request(*activities: ActivityInput) -> ExtracurricularRequest:
    return ExtracurricularRequest(activities=list(activities))


async def test_uses_attributes_read_by_llm():
    llm = FakeLLM(
        {
            "activities": [
                {"id": "a1", "role": "founder", "reputable_org": False, "impact_level": 3},
                {"id": "a2", "role": "member", "reputable_org": True, "impact_level": 2},
            ]
        }
    )

    result = await ActivityReader(llm).score(request(ROBOTICS, UNICEF))

    assert result.ai_used is True
    assert [a.role for a in result.activities] == ["founder", "member"]
    assert result.activities[1].reputable_org is True
    assert result.score == 1.61  # 0.92 (sáng lập, tỉnh, 24 tháng) + 0.69 (TNV UNICEF, uy tín, 2 tháng)
    assert llm.calls[0]["schema"]["properties"]["activities"]["items"]["properties"]["id"]["enum"] == [
        "a1",
        "a2",
    ]


async def test_llm_cannot_raise_impact_level_above_student_claim():
    llm = FakeLLM({"activities": [{"id": "a2", "role": "member", "reputable_org": True, "impact_level": 5}]})

    result = await ActivityReader(llm).score(request(UNICEF))

    assert result.activities[0].impact_level == 2  # học sinh khai 2, LLM nói 5 → giữ 2


async def test_llm_can_lower_impact_level():
    llm = FakeLLM(
        {"activities": [{"id": "a1", "role": "founder", "reputable_org": False, "impact_level": 1}]}
    )

    result = await ActivityReader(llm).score(request(ROBOTICS))

    assert result.activities[0].impact_level == 1


async def test_activity_missed_by_llm_falls_back_to_keywords():
    llm = FakeLLM({"activities": [{"id": "a2", "role": "member", "reputable_org": True, "impact_level": 2}]})

    result = await ActivityReader(llm).score(request(ROBOTICS, UNICEF))

    assert result.activities[0].role == "founder"  # đọc từ khóa "người sáng lập"
    assert result.activities[0].reputable_org is False


async def test_llm_error_still_returns_score_from_keywords():
    llm = FakeLLM(OllamaError("connection refused"))

    result = await ActivityReader(llm).score(request(ROBOTICS, UNICEF))

    assert result.ai_used is False
    assert [a.role for a in result.activities] == ["founder", "member"]
    assert result.score > 0


async def test_no_activities_scores_zero_without_calling_llm():
    llm = FakeLLM()

    result = await ActivityReader(llm).score(request())

    assert result.score == 0
    assert llm.calls == []
