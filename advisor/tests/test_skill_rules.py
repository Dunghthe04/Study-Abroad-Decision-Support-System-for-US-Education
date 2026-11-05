"""Bước 2.9: luật code chọn skill chạy thật. Hàm thuần nên không cần AI, DB hay Ollama."""

from app.schemas.chat import ChatState, ProfileField
from app.services.skill_rules import REQUIRED_B3, Blocked, missing_fields, plan_skills


def state(step: str = "B2", **fields: str) -> ChatState:
    """Dựng trạng thái: fields là {tên thông tin: trạng thái}, ví dụ gpa="confirmed"."""
    return ChatState(
        current_step=step, profile={k: ProfileField(value="x", status=v) for k, v in fields.items()}
    )


B3_PROFILE = dict(target_level="confirmed", gpa="confirmed", english="confirmed", budget="confirmed")
FULL_PROFILE = dict(B3_PROFILE, major="confirmed", scholarship="confirmed")  # đủ cho cả B4


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
    plan = plan_skills(state("B3", gpa="confirmed"), ["skill_danh_gia"])

    assert plan.skills == []
    assert plan.blocked == [Blocked("skill_danh_gia", "thieu_ho_so")]
    assert plan.missing == ["target_level", "english", "budget"]  # đúng thứ tự hỏi, chưa đòi ngành


def test_assessment_runs_without_major_or_scholarship():
    plan = plan_skills(state("B3", **B3_PROFILE), ["skill_danh_gia"])

    assert plan.skills == ["skill_danh_gia"]
    assert not plan.blocked
    assert plan.missing == []


def test_assessment_still_runs_when_major_is_unknown():
    profile = dict(B3_PROFILE, major="unknown")  # chưa rõ ngành vẫn đánh giá được hồ sơ
    plan = plan_skills(state("B3", **profile), ["skill_danh_gia"])

    assert plan.skills == ["skill_danh_gia"]


def test_roadmap_is_blocked_for_major_when_major_is_the_first_missing():
    profile = dict(FULL_PROFILE, major="unknown")  # chỉ ngành chưa rõ
    plan = plan_skills(state("B4", **profile), ["skill_lo_trinh"])

    assert plan.skills == []
    assert plan.blocked == [Blocked("skill_lo_trinh", "thieu_nganh")]
    assert plan.missing == ["major"]  # học bổng đã có, chỉ còn ngành


def test_roadmap_asks_profile_first_when_other_fields_are_missing_too():
    profile = dict(FULL_PROFILE, gpa="unknown", major="unknown")  # GPA đứng trước ngành trong thứ tự hỏi
    plan = plan_skills(state("B4", **profile), ["skill_lo_trinh"])

    assert plan.blocked == [Blocked("skill_lo_trinh", "thieu_ho_so")]
    assert plan.missing == ["gpa", "major"]


def test_roadmap_runs_when_profile_is_complete():
    plan = plan_skills(state("B4", **FULL_PROFILE), ["skill_lo_trinh"])

    assert plan.skills == ["skill_lo_trinh"]
    assert not plan.blocked


def test_none_and_unknown_count_as_answered_but_not_for_must_have_fields():
    profile = dict(FULL_PROFILE, english="none", scholarship="unknown")
    assert missing_fields(state(**profile).profile) == []

    profile = dict(FULL_PROFILE, gpa="unknown")  # GPA không được bỏ qua
    assert missing_fields(state(**profile).profile) == ["gpa"]

    profile = dict(FULL_PROFILE, budget="said_in_B0")  # tự nói chưa xác nhận
    assert missing_fields(state(**profile).profile) == ["budget"]

    profile = dict(FULL_PROFILE, target_level="unknown")  # bậc học không được bỏ qua
    assert missing_fields(state(**profile).profile) == ["target_level"]

    profile = dict(FULL_PROFILE, major="unknown")  # "chưa rõ" ngành: B4 vẫn thiếu
    assert missing_fields(state(**profile).profile) == ["major"]


def test_missing_fields_can_be_counted_for_the_b3_milestone_only():
    profile = dict(B3_PROFILE, major="unknown")

    assert missing_fields(state(**profile).profile, REQUIRED_B3) == []
    assert missing_fields(state(**profile).profile) == ["major", "scholarship"]  # mặc định tính theo B4


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
