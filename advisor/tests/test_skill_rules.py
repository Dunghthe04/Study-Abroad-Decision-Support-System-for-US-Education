"""Bước 2.9: luật code chọn skill chạy thật. Hàm thuần nên không cần AI, DB hay Ollama."""

from app.schemas.chat import ChatState, ProfileField
from app.services.skill_rules import Blocked, missing_fields, plan_skills


def state(step: str = "B2", **fields: str) -> ChatState:
    """Dựng trạng thái: fields là {tên thông tin: trạng thái}, ví dụ gpa="confirmed"."""
    return ChatState(
        current_step=step, profile={k: ProfileField(value="x", status=v) for k, v in fields.items()}
    )


FULL_PROFILE = dict(
    education_system="confirmed",
    gpa="confirmed",
    english="confirmed",
    budget="confirmed",
    scholarship_need="confirmed",
    activities="confirmed",
)


def test_unsure_model_falls_back_to_current_step_skill():
    plan = plan_skills(state("B2"), [])

    assert plan.skills == ["skill_ho_so"]
    assert plan.source == "fallback_step"


def test_unknown_step_falls_back_to_ngoai_le():
    assert plan_skills(state("END"), []).skills == ["ngoai_le"]


def test_at_most_two_skills_keeping_highest_probability():
    # model xếp xác suất giảm dần: tra_loi > ho_so > ket_thuc; chỉ giữ 2 nhãn đầu
    plan = plan_skills(state("B2"), ["skill_tra_loi", "skill_ho_so", "ket_thuc"])

    assert set(plan.skills) == {"skill_tra_loi", "skill_ho_so"}
    assert plan.source == "model"


def test_two_skills_run_in_fixed_order():
    plan = plan_skills(state("B2"), ["skill_tra_loi", "skill_ho_so"])  # model gợi ý tra_loi trước

    assert plan.skills == [
        "skill_ho_so",
        "skill_tra_loi",
    ]  # nhưng chạy skill của bước trước, rồi mới trả lời kiến thức


def test_service_runs_after_knowledge_and_end_runs_last():
    assert plan_skills(state("B4"), ["skill_chuyen_trung_tam", "skill_tra_loi"]).skills == [
        "skill_tra_loi",
        "skill_chuyen_trung_tam",
    ]
    assert plan_skills(state("B4"), ["ket_thuc", "skill_chuyen_trung_tam"]).skills == [
        "skill_chuyen_trung_tam",
        "ket_thuc",
    ]


def test_ngoai_le_is_dropped_when_with_another_label_but_kept_alone():
    assert plan_skills(state("B2"), ["ngoai_le", "skill_ho_so"]).skills == ["skill_ho_so"]
    assert plan_skills(state("B2"), ["ngoai_le"]).skills == ["ngoai_le"]


def test_unknown_labels_are_ignored():
    plan = plan_skills(state("B3", **FULL_PROFILE), ["skill_la"])

    assert plan.source == "fallback_step"
    assert plan.skills == ["skill_danh_gia"]  # nhãn lạ bị bỏ, dùng skill của bước B3


def test_assessment_is_blocked_until_profile_is_complete():
    plan = plan_skills(state("B3", education_system="confirmed", gpa="confirmed"), ["skill_danh_gia"])

    assert plan.skills == []
    assert plan.blocked == [Blocked("skill_danh_gia", "thieu_ho_so")]
    assert plan.missing == ["english", "budget", "scholarship_need", "activities"]  # đúng thứ tự hỏi


def test_assessment_runs_when_profile_is_complete():
    plan = plan_skills(state("B3", **FULL_PROFILE), ["skill_danh_gia"])

    assert plan.skills == ["skill_danh_gia"]
    assert not plan.blocked
    assert plan.missing == []


def test_none_and_unknown_count_as_answered_but_not_for_gpa_and_budget():
    profile = dict(FULL_PROFILE, english="none", activities="unknown")
    assert missing_fields(state(**profile).profile) == []

    profile = dict(FULL_PROFILE, gpa="unknown")  # GPA không được bỏ qua
    assert missing_fields(state(**profile).profile) == ["gpa"]

    profile = dict(FULL_PROFILE, budget="said_in_B0")  # tự nói chưa xác nhận
    assert missing_fields(state(**profile).profile) == ["budget"]


def test_explain_and_what_if_need_a_school_list():
    blocked = plan_skills(state("B4", **FULL_PROFILE), ["skill_kich_ban"], has_results=False)
    allowed = plan_skills(state("B4", **FULL_PROFILE), ["skill_kich_ban"], has_results=True)

    assert blocked.skills == []
    assert blocked.blocked == [Blocked("skill_kich_ban", "chua_co_danh_sach")]
    assert blocked.missing == []  # chặn vì chưa có danh sách, không phải thiếu hồ sơ
    assert allowed.skills == ["skill_kich_ban"]


def test_only_blocked_skill_is_removed_the_other_still_runs():
    plan = plan_skills(state("B3", gpa="confirmed"), ["skill_danh_gia", "skill_tra_loi"])

    assert plan.skills == ["skill_tra_loi"]  # chưa đủ hồ sơ thì chưa chấm, nhưng vẫn trả lời kiến thức
    assert [b.skill for b in plan.blocked] == ["skill_danh_gia"]
