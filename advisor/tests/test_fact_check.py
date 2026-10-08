import pytest

from app.schemas.recommendation import AiSchoolInput, AiStudentInput
from app.services.fact_check import contradictions, cost_ranks, relations

STUDENT = AiStudentInput(study_level="undergraduate", gpa4=3.6, sat=1380, annual_budget_usd=60000)
# Giống University of Florida trong lần chạy thật: SAT nằm trong khoảng, GPA thấp hơn, chi phí trong ngân sách
UF_TRUTH = {"category": "reach", "sat": "within", "gpa": "below", "budget": "within", "english": "unknown"}


def school(**changes) -> AiSchoolInput:
    data = {
        "code": "UW",
        "name": "University of Washington",
        "category": "match",
        "avg_gpa4": 3.7,
        "sat25": 1250,
        "sat75": 1480,
        "total_cost_usd": 57168,
        "english": "met",
    }
    data.update(changes)
    return AiSchoolInput(**data)


# ---------- relations: phép so sánh đúng ----------


@pytest.mark.parametrize(
    ("sat", "expected"),
    [(1500, "above"), (1200, "below"), (1380, "within"), (1480, "within"), (1250, "within")],
)
def test_relations_sat(sat, expected):
    assert relations(STUDENT.model_copy(update={"sat": sat}), school())["sat"] == expected


@pytest.mark.parametrize(("gpa", "expected"), [(3.8, "above"), (3.5, "below"), (3.7, "equal")])
def test_relations_gpa(gpa, expected):
    assert relations(STUDENT.model_copy(update={"gpa4": gpa}), school())["gpa"] == expected


@pytest.mark.parametrize(("cost", "expected"), [(57168, "within"), (60000, "within"), (65000, "over")])
def test_relations_budget(cost, expected):
    assert relations(STUDENT, school(total_cost_usd=cost))["budget"] == expected


def test_relations_skip_missing_data():
    rel = relations(STUDENT, school(avg_gpa4=None, sat25=None, sat75=None, total_cost_usd=None))

    assert rel == {"category": "match", "english": "met"}


# ---------- contradictions: bắt lý do nói ngược số liệu ----------


@pytest.mark.parametrize(
    "reason",
    [
        "SAT 1380 thấp hơn mốc 25% (1320-1480), GPA 3.6 thấp hơn GPA trung bình 3.9",  # lỗi thật đã gặp
        "Với SAT 1380, bạn đã vượt mốc 75% (1480) của trường.",
        "GPA 3.6 của bạn cao hơn mức trung bình 3.9.",  # số thập phân không được cắt ngang câu
        "Chi phí 43,849 USD/năm vượt ngân sách của bạn.",
        "Bạn đạt yêu cầu tiếng Anh của trường.",  # thật ra trường không công bố
        "SAT 1380 nằm trong khoảng 25%-75% nên đây là lựa chọn an toàn.",  # trường thuộc nhóm thử sức (reach)
        "Bạn chắc chắn sẽ được nhận.",  # hứa hẹn quá mức
    ],
)
def test_contradictions_catch_wrong_claims(reason):
    assert contradictions(reason, UF_TRUTH)


@pytest.mark.parametrize(
    "reason",
    [
        "Điểm SAT 1380 của bạn nằm trong khoảng 25%-75%, nhưng GPA 3.6 thấp hơn mức trung bình 3.9.",
        "SAT 1380 của bạn cao hơn mốc 25% (1320) nên bạn có cơ sở để thử sức.",  # đúng khi nằm trong khoảng
        "SAT 1380 vẫn thấp hơn mốc 75% (1480) của trường.",  # cũng đúng khi nằm trong khoảng
        "Chi phí 43,849 USD/năm nằm gọn trong ngân sách, trường không công bố yêu cầu tiếng Anh.",
        "Với SAT 1380, bạn nên kiểm tra thêm trên website của trường.",  # không khẳng định gì
        "GPA 3.6 thấp hơn mức trung bình 3.9 nên đây là trường để bạn thử sức.",
    ],
)
def test_contradictions_accept_correct_or_neutral_claims(reason):
    assert contradictions(reason, UF_TRUTH) == []


def test_contradictions_accept_advice_for_school_without_enough_data():
    truth = {"category": "insufficient_data", "budget": "within", "english": "unknown"}
    reason = "Trường chưa đủ dữ liệu để xếp nhóm, nhưng chi phí 12,269 USD/năm trong ngân sách, bạn có thể thử sức."

    assert contradictions(reason, truth) == []


def test_contradictions_catch_met_english_when_school_publishes_nothing():
    reason = "Bạn đã đạt yêu cầu tiếng Anh, hãy tập trung vào bài luận."

    assert contradictions(reason, {"english": "unknown"})
    assert contradictions("Trường không công bố yêu cầu tiếng Anh.", {"english": "unknown"}) == []


@pytest.mark.parametrize(
    ("score", "reason", "is_wrong"),
    [
        (3.0, "Hồ sơ ngoại khóa còn mỏng, bạn nên bổ sung một dự án.", True),  # lỗi thật: chê hồ sơ mạnh
        (3.0, "Điểm ngoại khóa 3/4 là lợi thế khi viết bài luận.", False),
        (0.5, "Điểm ngoại khóa là lợi thế của bạn.", True),
        (2.0, "Hồ sơ ngoại khóa cần bổ sung thêm.", False),  # mức trung bình: khen hay chê đều chấp nhận
    ],
)
def test_contradictions_check_extracurricular_level(score, reason, is_wrong):
    student = STUDENT.model_copy(update={"extracurricular_score": score})
    truth = relations(student, school())

    assert bool(contradictions(reason, truth)) == is_wrong


# ---------- trường rẻ nhất / đắt nhất trong nhóm ----------


def test_cost_ranks_mark_cheapest_and_priciest_per_group():
    schools = [
        school(code="A", total_cost_usd=30000),
        school(code="B", total_cost_usd=50000),
        school(code="C", total_cost_usd=40000),
        school(code="D", total_cost_usd=None),  # thiếu chi phí: không xếp
        school(code="S", category="safety", total_cost_usd=20000),  # nhóm chỉ 1 trường: không so
    ]

    assert cost_ranks(schools) == {"A": "cheapest", "B": "priciest", "C": "none", "D": "none", "S": "none"}


@pytest.mark.parametrize(
    ("reason", "cost_rank", "is_wrong"),
    [
        ("Trường có chi phí cao nhất trong nhóm reach.", "none", True),  # lỗi thật: gán nhầm cho UNC
        ("Trường có chi phí cao nhất trong nhóm reach.", "cheapest", True),
        ("Trường có chi phí cao nhất trong nhóm reach.", "priciest", False),
        ("Đây là trường rẻ nhất nhóm, rất đáng thử.", "cheapest", False),
        ("Chi phí vừa phải, phù hợp với ngân sách.", "none", False),  # không khẳng định gì
    ],
)
def test_contradictions_check_cost_rank_in_group(reason, cost_rank, is_wrong):
    assert bool(contradictions(reason, {"cost_rank": cost_rank})) == is_wrong
