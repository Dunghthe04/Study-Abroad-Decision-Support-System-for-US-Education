"""Phản hồi chat báo thêm SLM đã hiểu tin ra sao (khóa slm): loại các ý, loại con của ngoại phạm vi, độ tin cậy, có dùng kết quả dự phòng không.
.NET dùng khối này để đánh dấu câu chưa trả lời được. Khối chỉ chứa mã cố định, không có chữ của học sinh."""

from app.schemas.chat import ChatMessage, ChatRequest
from app.services.slm_classifier import FALLBACK, OllamaSlm
from tests.fakes import flow_with, tin


def ask(text: str) -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=text)])


async def test_summary_lists_the_intents_and_confidence_of_the_turn():
    flow = flow_with({}, tin(("out_of_scope", {"subtype": "farewell"}, "tạm biệt"), confidence=0.93))

    res = await flow.reply(ask("tạm biệt"))

    assert res.slm is not None
    assert res.slm.intents == ["out_of_scope"]
    assert res.slm.out_of_scope == "farewell"
    assert res.slm.confidence == 0.93
    assert res.slm.fallback is False
    # Nhãn model gán, đủ loại, đoạn chữ và thực thể, để admin đối chiếu và sửa
    assert [(y.type, y.span, y.entities) for y in res.slm.model_intents] == [
        ("out_of_scope", "tạm biệt", {"subtype": "farewell"})
    ]


async def test_model_intents_keep_the_raw_model_label_even_when_a_code_rule_rewrites_the_turn():
    # SLM gán nhầm câu hỏi số liệu vào "mơ hồ"; luật code cứu thành câu kiến thức. Admin phải thấy nhãn GỐC của model
    flow = flow_with(
        {"skill_tra_loi": _Say("trả lời")},
        tin(("out_of_scope", {"subtype": "ambiguous"}, "gpa cua truong x")),
    )

    res = await flow.reply(ask("gpa cua truong x"))

    assert res.slm.intents == ["knowledge"]  # cái hệ thống đã chạy
    assert [y.type for y in res.slm.model_intents] == ["out_of_scope"]  # cái model đã nói
    assert res.slm.model_intents[0].entities == {"subtype": "ambiguous"}


async def test_an_ambiguous_turn_is_reported_with_its_subtype():
    flow = flow_with({}, tin(("out_of_scope", {"subtype": "ambiguous"}, "asdfgh"), confidence=0.2))

    res = await flow.reply(ask("asdfgh"))

    assert (res.slm.out_of_scope, res.slm.fallback) == ("ambiguous", False)  # mơ hồ thật, không phải SLM hỏng


async def test_a_turn_without_out_of_scope_has_no_subtype():
    flow = flow_with({"skill_tra_loi": _Say("trả lời")}, tin("knowledge"))

    res = await flow.reply(ask("I-20 là gì"))

    assert res.slm.intents == ["knowledge"]
    assert res.slm.out_of_scope is None


async def test_the_fallback_result_of_the_classifier_is_flagged():
    flow = flow_with({}, FALLBACK.model_copy(deep=True))

    res = await flow.reply(ask("asdfgh"))

    assert res.slm.fallback is True
    assert res.slm.out_of_scope == "ambiguous"
    assert res.slm.confidence == 0.0
    assert (
        res.slm.model_intents == []
    )  # model hỏng: không có đầu ra thật để lưu, câu "tin không rõ" là do code dựng


async def test_the_real_classifier_marks_fallback_after_two_bad_answers():
    class BadSlm:
        async def chat_json_scored(self, messages, output):
            raise ValueError("model trả sai khuôn")

    result = await OllamaSlm(BadSlm(), prompt="slm_intents").read("asdfgh", None, "")

    assert result.fallback is True
    assert result.types() == ["out_of_scope"]


async def test_a_normal_classifier_result_is_not_marked_fallback():
    assert tin("knowledge").fallback is False


async def test_summary_is_serialized_in_camel_case():
    flow = flow_with({}, tin(("out_of_scope", {"subtype": "exception"}, "thời tiết"), confidence=0.8))

    body = (await flow.reply(ask("thời tiết"))).model_dump(by_alias=True)

    assert body["slm"] == {
        "modelIntents": [{"type": "out_of_scope", "span": "thời tiết", "entities": {"subtype": "exception"}}],
        "intents": ["out_of_scope"],
        "outOfScope": "exception",
        "confidence": 0.8,
        "fallback": False,
    }


class _Say:
    def __init__(self, text: str) -> None:
        self._text = text

    async def run(self, state, message):
        from app.services.chat_flow import SkillOutput

        return SkillOutput(self._text)
