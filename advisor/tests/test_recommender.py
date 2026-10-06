import pytest

from app.schemas.recommendation import AiPick, AiRankRequest, AiSchoolInput, AiStudentInput
from app.services.recommender import (
    Recommender,
    RecommenderError,
    build_output_schema,
    build_user_prompt,
    keep_valid_picks,
    school_facts,
)
from tests.fakes import FakeLLM

STUDENT = AiStudentInput(study_level="undergraduate", gpa4=3.6, sat=1380, annual_budget_usd=60000, ielts=7.0)


def school(**changes) -> AiSchoolInput:
    """Trường mẫu (UW). Truyền tham số để đổi vài trường dữ liệu, giống `with { ... }` của record C#."""
    data = {
        "code": "UW",
        "name": "University of Washington",
        "state": "WA",
        "category": "match",
        "avg_gpa4": 3.7,
        "sat25": 1250,
        "sat75": 1480,
        "total_cost_usd": 57168,
        "english": "met",
    }
    data.update(changes)
    return AiSchoolInput(**data)


# ---------- school_facts: phép so sánh do code làm, phải luôn đúng ----------


@pytest.mark.parametrize(
    ("sat", "expected"),
    [
        (1500, "SAT 1500 cao hơn mốc 75% (1480)"),
        (1200, "SAT 1200 thấp hơn mốc 25% (1250)"),
        (1380, "SAT 1380 trong khoảng mốc 25%-75% (1250-1480)"),
        (1480, "SAT 1480 trong khoảng mốc 25%-75% (1250-1480)"),  # bằng đúng mốc vẫn là "trong khoảng"
    ],
)
def test_facts_compare_sat_with_school_range(sat, expected):
    student = STUDENT.model_copy(update={"sat": sat})

    assert expected in school_facts(student, school())


@pytest.mark.parametrize(
    ("gpa", "expected"),
    [
        (3.8, "GPA 3.8 cao hơn GPA trung bình 3.7"),
        (3.5, "GPA 3.5 thấp hơn GPA trung bình 3.7"),
        (3.7, "GPA 3.7 bằng GPA trung bình 3.7"),
    ],
)
def test_facts_compare_gpa_with_school_average(gpa, expected):
    student = STUDENT.model_copy(update={"gpa4": gpa})

    assert expected in school_facts(student, school())


@pytest.mark.parametrize(
    ("total_cost", "expected"),
    [
        (57168, "chi phí 57,168 USD/năm, trong ngân sách 60,000 USD"),
        (65000, "chi phí 65,000 USD/năm, vượt ngân sách 60,000 USD"),
    ],
)
def test_facts_compare_cost_with_budget(total_cost, expected):
    assert expected in school_facts(STUDENT, school(total_cost_usd=total_cost))


def test_facts_skip_missing_school_data():
    bare = school(avg_gpa4=None, sat25=None, sat75=None, total_cost_usd=None, english="unknown")

    assert school_facts(STUDENT, bare) == ["trường không công bố yêu cầu tiếng Anh"]


# ---------- prompt và schema gửi cho LLM ----------


def test_user_prompt_writes_numbers_the_way_dotnet_guard_reads_them():
    prompt = build_user_prompt(AiRankRequest(student=STUDENT, schools=[school()]))

    assert "IELTS 7," in prompt  # 7.0 -> 7
    assert "60,000 USD" in prompt
    assert "57,168 USD" in prompt
    assert "None" not in prompt  # dữ liệu thiếu thì bỏ, không in "None"


def test_output_schema_only_allows_input_codes():
    items = build_output_schema(["UW", "ASU"])["properties"]["items"]

    assert items["items"]["properties"]["code"]["enum"] == ["UW", "ASU"]
    assert items["minItems"] == items["maxItems"] == 2


# ---------- lọc kết quả LLM ----------


def test_keep_valid_picks_drops_unknown_and_duplicate_codes():
    items = [
        AiPick(code="UW", reason="lần 1"),
        AiPick(code="XYZ", reason="mã bịa"),
        AiPick(code="UW", reason="lần 2"),
        AiPick(code="ASU", reason="ok"),
    ]

    picks = keep_valid_picks(items, ["UW", "ASU"])

    assert [(p.code, p.reason) for p in picks] == [("UW", "lần 1"), ("ASU", "ok")]


async def test_rank_keeps_partial_answer_for_dotnet_to_fill_in():
    asu = school(code="ASU", name="Arizona State University")
    llm = FakeLLM({"items": [{"code": "UW", "reason": "Lý do UW"}]})

    response = await Recommender(llm).rank(AiRankRequest(student=STUDENT, schools=[school(), asu]))

    assert [p.code for p in response.items] == ["UW"]  # thiếu ASU vẫn trả, không báo lỗi
    sent = llm.calls[0]
    assert sent["messages"][0]["role"] == "system"
    assert sent["schema"]["properties"]["items"]["items"]["properties"]["code"]["enum"] == ["UW", "ASU"]


async def test_rank_raises_when_no_code_is_valid():
    llm = FakeLLM({"items": [{"code": "XYZ", "reason": "mã bịa"}]})

    with pytest.raises(RecommenderError):
        await Recommender(llm).rank(AiRankRequest(student=STUDENT, schools=[school()]))
