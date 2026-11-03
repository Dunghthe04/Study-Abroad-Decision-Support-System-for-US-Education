"""Các hàm phụ của ChatFlow khi xử lý tin nhiều ý: chọn ý cần chạy và phần kết quả giao cho từng nhánh."""

from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import PROFILE_ONLY, intents_to_run, view_of


def tin(*intents, confidence=1.0) -> SlmTinResult:
    """Mỗi ý là tên loại, hoặc (loại, thuc_the), hoặc (loại, thuc_the, doan). mac_dinh phải đứng đầu."""

    def make(item) -> SlmY:
        if isinstance(item, str):
            return SlmY(type=item, doan="x")
        kind, thuc_the, *doan = item
        return SlmY(type=kind, doan=doan[0] if doan else "x", thuc_the=thuc_the)

    return SlmTinResult(cac_y=[make(i) for i in intents], confidence=confidence)


def kinds(slm: SlmTinResult) -> list[str]:
    return [y.type for y in intents_to_run(slm)]


# ---- ý cần chạy ----
def test_intents_run_in_the_order_given():
    assert kinds(tin("kien_thuc")) == ["kien_thuc"]
    assert kinds(tin("kien_thuc", "trung_tam")) == ["kien_thuc", "trung_tam"]
    assert kinds(tin("kien_thuc", "kien_thuc")) == ["kien_thuc", "kien_thuc"]  # mỗi câu hỏi một lần chạy


def test_every_intent_runs_however_many_there_are():
    assert kinds(tin(*["kien_thuc"] * 12)) == ["kien_thuc"] * 12


def test_a_profile_intent_stays_first():
    assert kinds(tin(("mac_dinh", {"gpa": "8.5 ạ"}), "kien_thuc")) == ["mac_dinh", "kien_thuc"]
    assert kinds(tin(("mac_dinh", {"gpa": "8.5"}))) == ["mac_dinh"]


def test_profile_values_inside_another_intent_add_a_profile_intent_in_front():
    slm = tin(("ho_so", {"gpa": "GPA 3.5"}), "kien_thuc")

    assert intents_to_run(slm) == [PROFILE_ONLY, *slm.cac_y]


def test_a_message_with_no_profile_value_gets_no_profile_intent():
    assert PROFILE_ONLY not in intents_to_run(tin("ho_so", "kien_thuc"))


# ---- phần kết quả giao cho nhánh ----
def test_view_of_gives_the_profile_branch_the_whole_result():
    slm = tin(("mac_dinh", {"gpa": "GPA 3.5"}), "kien_thuc", "trung_tam")

    assert view_of(slm, slm.cac_y[0]) is slm


def test_view_of_gives_any_other_branch_only_its_own_intent():
    slm = tin(("mac_dinh", {"gpa": "GPA 3.5"}), "kien_thuc", "trung_tam", confidence=0.8)

    view = view_of(slm, slm.cac_y[1])

    assert [y.type for y in view.cac_y] == ["kien_thuc"]
    assert view.confidence == 0.8
    assert len(slm.cac_y) == 3  # kết quả gốc không bị đổi
