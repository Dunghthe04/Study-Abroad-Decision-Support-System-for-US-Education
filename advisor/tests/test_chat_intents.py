"""Các hàm phụ của ChatFlow khi xử lý tin nhiều ý: chọn ý cần chạy và phần kết quả giao cho từng nhánh."""

from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import PROFILE_ONLY, intents_to_run, view_of


def tin(*intents, confidence=1.0) -> SlmTinResult:
    """Mỗi ý là tên loại, hoặc (loại, entities), hoặc (loại, entities, span). collect_profile phải đứng đầu."""

    def make(item) -> SlmY:
        if isinstance(item, str):
            return SlmY(type=item, span="x")
        kind, entities, *span = item
        return SlmY(type=kind, span=span[0] if span else "x", entities=entities)

    return SlmTinResult(intents=[make(i) for i in intents], confidence=confidence)


def kinds(slm: SlmTinResult) -> list[str]:
    return [y.type for y in intents_to_run(slm)]


# ---- ý cần chạy ----
def test_intents_run_in_the_order_given():
    assert kinds(tin("knowledge")) == ["knowledge"]
    assert kinds(tin("knowledge", "center_referral")) == ["knowledge", "center_referral"]
    assert kinds(tin("knowledge", "knowledge")) == ["knowledge", "knowledge"]  # mỗi câu hỏi một lần chạy


def test_every_intent_runs_however_many_there_are():
    assert kinds(tin(*["knowledge"] * 12)) == ["knowledge"] * 12


def test_a_profile_intent_stays_first():
    assert kinds(tin(("collect_profile", {"gpa": "8.5 ạ"}), "knowledge")) == ["collect_profile", "knowledge"]
    assert kinds(tin(("collect_profile", {"gpa": "8.5"}))) == ["collect_profile"]


def test_profile_values_inside_another_intent_add_a_profile_intent_in_front():
    slm = tin(("profile_review", {"gpa": "GPA 3.5"}), "knowledge")

    assert intents_to_run(slm) == [PROFILE_ONLY, *slm.intents]


def test_a_message_with_no_profile_value_gets_no_profile_intent():
    assert PROFILE_ONLY not in intents_to_run(tin("profile_review", "knowledge"))


# ---- phần kết quả giao cho nhánh ----
def test_view_of_gives_the_profile_branch_the_whole_result():
    slm = tin(("collect_profile", {"gpa": "GPA 3.5"}), "knowledge", "center_referral")

    assert view_of(slm, slm.intents[0]) is slm


def test_view_of_gives_any_other_branch_only_its_own_intent():
    slm = tin(("collect_profile", {"gpa": "GPA 3.5"}), "knowledge", "center_referral", confidence=0.8)

    view = view_of(slm, slm.intents[1])

    assert [y.type for y in view.intents] == ["knowledge"]
    assert view.confidence == 0.8
    assert len(slm.intents) == 3  # kết quả gốc không bị đổi
