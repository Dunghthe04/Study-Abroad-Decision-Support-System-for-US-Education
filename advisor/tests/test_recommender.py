import pytest

from app.schemas.recommendation import AiPick, AiRankRequest, AiSchoolInput, AiStudentInput
from app.services.recommender import (
    TEMPERATURE,
    Recommender,
    RecommenderError,
    build_output_schema,
    build_user_prompt,
    clean_reason,
    group_notes,
    keep_valid_picks,
    school_data,
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


# ---------- dữ liệu từng trường gửi cho LLM ----------


def test_school_data_puts_computed_comparison_next_to_numbers():
    data = school_data(STUDENT, school(), notes=[])

    assert data["sat"] == {
        "so_sanh": "nằm trong khoảng 25%-75%",
        "cua_ban": 1380,
        "moc_25": 1250,
        "moc_75": 1480,
    }
    assert data["gpa"]["so_sanh"] == "thấp hơn"
    assert data["chi_phi"] == {"moi_nam": "57,168 USD", "so_sanh": "trong ngân sách"}


def test_school_data_says_cost_is_missing_instead_of_skipping_it():
    data = school_data(STUDENT, school(total_cost_usd=None), notes=[])

    assert data["chi_phi"] == "chưa có dữ liệu, cần hỏi trường"


def test_group_notes_mark_cheapest_and_priciest_in_each_group():
    schools = [
        school(code="A", total_cost_usd=30000),
        school(code="B", total_cost_usd=50000),
        school(code="C", total_cost_usd=40000),
        school(code="S", category="safety", total_cost_usd=20000),  # nhóm chỉ có 1 trường: không so
    ]

    notes = group_notes(AiRankRequest(student=STUDENT, schools=schools))

    assert notes == {
        "A": ["chi phí thấp nhất trong nhóm match"],
        "B": ["chi phí cao nhất trong nhóm match"],
        "C": [],
        "S": [],
    }


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


async def test_rank_drops_reason_that_contradicts_the_numbers():
    asu = school(code="ASU", name="Arizona State University", sat25=1120, sat75=1360)
    wrong = "SAT 1380 thấp hơn mốc 25% (1250), nên đây là trường thử sức."  # sai: 1380 nằm trong khoảng
    right = "Với SAT 1380, bạn đã vượt mốc 75% (1360) của trường."
    llm = FakeLLM({"items": [{"code": "UW", "reason": wrong}, {"code": "ASU", "reason": right}]})

    response = await Recommender(llm).rank(AiRankRequest(student=STUDENT, schools=[school(), asu]))

    assert [p.code for p in response.items] == ["ASU"]  # UW bị bỏ, .NET sẽ dùng lý do soạn sẵn
    assert llm.calls[0]["temperature"] == TEMPERATURE


@pytest.mark.parametrize(
    ("raw", "expected"),
    [
        ("Câu một. Câu hai.", "Câu một. Câu hai."),
        ("Câu một. Câu hai.\nCâu một viết lại. Câu hai viết lại.", "Câu một. Câu hai."),  # LLM viết lặp
        ("Câu một. Câu hai bị cắt d", "Câu một."),  # chạm giới hạn độ dài
        ("GPA 3.6 thấp hơn 3.9", "GPA 3.6 thấp hơn 3.9"),  # chỉ 1 câu bị cắt thì giữ nguyên
    ],
)
def test_clean_reason(raw, expected):
    assert clean_reason(raw) == expected
