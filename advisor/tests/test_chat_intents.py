"""Các hàm phụ của ChatFlow khi xử lý tin nhiều ý: chọn ý cần chạy, phần kết quả giao cho từng nhánh, nút cho ý hoãn lại."""

from app.schemas.chat import QuickReply, Widget
from app.schemas.slm import SlmTinResult, SlmY
from app.services.chat_flow import (
    MORE_QUESTIONS,
    MORE_QUESTIONS_TEXT,
    PROFILE_ONLY,
    SkillOutput,
    intents_to_run,
    postponed,
    short,
    view_of,
)


def tin(*intents, con_lai=(), confidence=1.0) -> SlmTinResult:
    """Mỗi ý là tên loại, hoặc (loại, thuc_the), hoặc (loại, thuc_the, doan). mac_dinh phải đứng đầu."""

    def make(item) -> SlmY:
        if isinstance(item, str):
            return SlmY(type=item, doan="x")
        kind, thuc_the, *doan = item
        return SlmY(type=kind, doan=doan[0] if doan else "x", thuc_the=thuc_the)

    return SlmTinResult(
        cac_y=[make(i) for i in intents], con_lai=[make(i) for i in con_lai], confidence=confidence
    )


def kinds(slm: SlmTinResult) -> list[str]:
    return [y.type for y in intents_to_run(slm)]


# ---- ý cần chạy ----
def test_intents_run_in_the_order_given():
    assert kinds(tin("kien_thuc")) == ["kien_thuc"]
    assert kinds(tin("kien_thuc", "trung_tam")) == ["kien_thuc", "trung_tam"]
    assert kinds(tin("kien_thuc", "kien_thuc")) == ["kien_thuc", "kien_thuc"]  # mỗi câu hỏi một lần chạy


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
    slm = tin(
        ("mac_dinh", {"gpa": "GPA 3.5"}), "kien_thuc", "trung_tam", con_lai=("kien_thuc",), confidence=0.8
    )

    view = view_of(slm, slm.cac_y[1])

    assert [y.type for y in view.cac_y] == ["kien_thuc"] and view.con_lai == []
    assert view.confidence == 0.8
    assert len(slm.cac_y) == 3 and len(slm.con_lai) == 1  # kết quả gốc không bị đổi


# ---- nút cho ý hoãn lại ----
def left(*doans: str) -> list[SlmY]:
    return [SlmY(type="kien_thuc", doan=d) for d in doans]


def test_postponed_intents_become_buttons_with_their_own_text():
    out = postponed(SkillOutput("đã trả lời"), left("OPT là gì", "CPT là gì"), as_text=False)

    assert out.answer == "đã trả lời\n\n" + MORE_QUESTIONS
    assert out.quick_replies == [
        QuickReply(label="OPT là gì", value="OPT là gì"),
        QuickReply(label="CPT là gì", value="CPT là gì"),
    ]


def test_postponed_intents_are_only_listed_when_buttons_would_clash():
    out = postponed(
        SkillOutput("hỏi ngành", [QuickReply(label="A", value="a")], Widget(type="major")),
        left("OPT là gì"),
        as_text=True,
    )

    assert out.answer == "hỏi ngành\n\n" + MORE_QUESTIONS_TEXT + "\n- OPT là gì"
    assert out.quick_replies == [QuickReply(label="A", value="a")] and out.widget == Widget(type="major")


def test_nothing_is_added_when_no_intent_was_postponed():
    output = SkillOutput("đã trả lời")

    assert postponed(output, [], as_text=False) is output


def test_a_long_question_gets_a_short_label_but_keeps_its_full_value():
    long = "học bổng toàn phần dành cho du học sinh Việt Nam xin vào lúc nào và cần những giấy tờ gì"

    button = postponed(SkillOutput("ok"), left(long), as_text=False).quick_replies[0]

    assert button.value == long and len(button.label) <= 60 and button.label.endswith("…")
    assert short("ngắn") == "ngắn"
