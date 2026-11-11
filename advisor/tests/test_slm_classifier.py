"""Bộ phân loại SLM qua Ollama: gửi đúng đầu vào, đọc danh sách ý, tính độ tin cậy, sai thì thử lại rồi lùi về mơ hồ."""

import json
import math

import httpx
import pytest

from app.schemas.chat import ChatState, ProfileField
from app.schemas.slm import INTENTS, SLM_TIN_SCHEMA
from app.services.ollama import OllamaClient, OllamaError
from app.services.slm_classifier import OllamaSlm, keep_verbatim, label_confidence, output_format, words

CENTER = {
    "type": "center_referral",
    "span": "ai sửa bài luận ở HN",
    "entities": {"service": "ESSAY_EDITING", "city": "HN"},
}
GOOD = {"intents": [CENTER]}


def y(kind, span, **entities):
    return {"type": kind, "span": span, "entities": entities}


class FakeSlm:
    """SLM giả: lần lượt trả các câu trả lời đã soạn sẵn (hoặc ném lỗi), ghi lại message nhận được."""

    def __init__(self, *answers):
        self._answers = list(answers)
        self.calls = []

    async def chat_json_scored(self, messages, schema):
        self.calls.append((messages, schema))
        answer = self._answers.pop(0)
        if isinstance(answer, Exception):
            raise answer
        return answer, json.dumps(answer, ensure_ascii=False), []


def user_input(slm, call=0):
    return json.loads(slm.calls[call][0][1]["content"])


# ---- đọc kết quả ----
async def test_a_good_answer_is_read_into_the_contract():
    slm = FakeSlm(GOOD)

    result = await OllamaSlm(slm).read("ai sửa bài luận ở HN", None, "")

    assert result.types() == ["center_referral"]
    assert (result.intents[0].span, result.intents[0].entities) == (CENTER["span"], CENTER["entities"])
    assert result.confidence == 1.0  # không có logprobs thì không đo được
    assert slm.calls[0][1] is SLM_TIN_SCHEMA


async def test_the_model_gets_the_prompt_and_the_same_input_columns_as_the_dataset():
    slm = FakeSlm(GOOD)

    await OllamaSlm(slm).read("8.5 ạ", {"gpa": "8.0"}, "Điểm GPA của bạn?")

    system, _ = slm.calls[0][0]
    assert system["role"] == "system" and "slm_intents" in system["content"]
    assert user_input(slm) == {
        "question": "8.5 ạ",
        "profile": {"gpa": "8.0"},
        "pending_question": "Điểm GPA của bạn?",
    }


async def test_classify_takes_the_profile_and_pending_question_from_the_state():
    state = ChatState(
        last_question="Hiện bạn đang học lớp mấy, hoặc đang làm gì?",
        profile={
            "gpa": ProfileField(value="8.5", status="confirmed"),
            "awards": ProfileField(value=["HSG tỉnh"], status="confirmed"),
            "sat": ProfileField(value=None, status="unknown"),
        },
    )
    slm = FakeSlm(GOOD)

    await OllamaSlm(slm).classify(state, "em lớp 12")

    sent = user_input(slm)
    assert sent["profile"] == {"gpa": "8.5", "awards": '["HSG tỉnh"]'}  # trường chưa có giá trị thì bỏ
    assert sent["pending_question"] == "Hiện bạn đang học lớp mấy, hoặc đang làm gì?"


# ---- tin nhiều ý ----
async def test_several_intents_come_back_in_the_order_the_code_wants_them():
    answer = {
        "intents": [
            y("knowledge", "I-20 là gì"),
            y("collect_profile", "em GPA 3.5", gpa="em GPA 3.5"),
            y("profile_review", "tư vấn trường"),
            y("profile_review", "em nên nộp trường nào"),
        ]
    }

    result = await OllamaSlm(FakeSlm(answer)).read(
        "I-20 là gì, em GPA 3.5, tư vấn trường, em nên nộp trường nào", None, ""
    )

    assert result.types() == [
        "collect_profile",
        "knowledge",
        "profile_review",
    ]  # collect_profile lên đầu, hai profile_review gộp một
    assert result.intents[2].span == "tư vấn trường, em nên nộp trường nào"
    assert result.profile_values() == {"gpa": "em GPA 3.5"}


async def test_every_question_is_kept_however_many_the_model_found():
    asked = [f"câu {i}" for i in range(1, 9)]
    answer = {"intents": [y("knowledge", span) for span in asked]}

    result = await OllamaSlm(FakeSlm(answer)).read(", ".join(asked), None, "")

    assert [i.span for i in result.intents] == asked


async def test_a_greeting_next_to_a_real_question_is_dropped():
    answer = {"intents": [y("out_of_scope", "chào", subtype="farewell"), y("knowledge", "I-20 là gì")]}

    result = await OllamaSlm(FakeSlm(answer)).read("chào, I-20 là gì", None, "")

    assert result.types() == ["knowledge"]


# ---- sai thì thử lại, rồi lùi về mơ hồ ----
async def test_an_answer_that_breaks_the_rules_is_retried_once():
    no_mode = {"intents": [y("scenario", "nếu em lên SAT 1500 thì sao")]}  # scenario thiếu mode
    slm = FakeSlm(no_mode, GOOD)

    result = await OllamaSlm(slm).read("x", None, "")

    assert result.types() == ["center_referral"] and len(slm.calls) == 2


@pytest.mark.parametrize(
    "bad",
    [
        {"type": "knowledge", "span": "x", "entities": {}},  # khuôn một ý, không có intents
        {"cac_y": [y("knowledge", "x")]},  # tên khóa cũ
        {"intents": []},
        {"intents": "không phải danh sách"},
        {"intents": ["không phải object"]},
        {"intents": [y("loai_la", "x")]},
    ],
)
async def test_an_answer_in_the_wrong_shape_is_retried_not_crashed(bad):
    slm = FakeSlm(bad, GOOD)

    result = await OllamaSlm(slm).read("ai sửa bài luận ở HN", None, "")

    assert result.types() == ["center_referral"] and len(slm.calls) == 2


async def test_two_failures_fall_back_to_an_ambiguous_message_with_zero_confidence():
    slm = FakeSlm(OllamaError("down"), {"intents": "hỏng"})

    result = await OllamaSlm(slm).read("x", None, "")

    assert result.types() == ["out_of_scope"]
    assert result.intents[0].entities == {"subtype": "ambiguous"} and result.confidence == 0.0


# ---- bỏ giá trị và đoạn không có trong tin ----
async def test_values_not_written_in_the_message_are_dropped_per_intent():
    answer = {
        "intents": [
            y("collect_profile", "em học Computer Science", gpa="8.0", major="computer science"),
            y("profile_review", "chọn trường nào", schools=["null"]),
        ]
    }

    result = await OllamaSlm(FakeSlm(answer)).read(
        "em học Computer Science nên chọn trường nào", {"gpa": "8.0"}, ""
    )

    # gpa chép từ hồ sơ, schools là ["null"] tự điền (hết tên thì bỏ khóa): bỏ. major có trong tin (không phân biệt hoa thường): giữ
    assert result.intents[0].entities == {"major": "computer science"}
    assert result.intents[1].entities == {}


def test_keep_verbatim_keeps_a_span_made_of_words_of_the_message_even_when_rewritten():
    raw = {"intents": [y("knowledge", "I-20 là gì"), y("knowledge", "SAT là gì")]}

    cleaned = keep_verbatim(raw, "I-20 và SAT là gì?")

    assert [i["span"] for i in cleaned["intents"]] == ["I-20 là gì", "SAT là gì"]


def test_keep_verbatim_replaces_a_span_with_words_the_message_does_not_have():
    cleaned = keep_verbatim({"intents": [y("knowledge", "visa F-1 là gì")]}, "I-20 là gì")
    assert cleaned["intents"][0]["span"] == "I-20 là gì"

    empty = keep_verbatim({"intents": [{"type": "knowledge", "span": "  ", "entities": {}}]}, "I-20 là gì")
    assert empty["intents"][0]["span"] == "I-20 là gì"

    missing = keep_verbatim({"intents": [{"type": "knowledge", "entities": {}}]}, "I-20 là gì")
    assert missing["intents"][0]["span"] == "I-20 là gì"


def test_keep_verbatim_leaves_a_broken_shape_for_the_contract_to_reject():
    assert keep_verbatim({"intents": "x"}, "tin") == {"intents": "x"}
    assert keep_verbatim({"intents": ["x"]}, "tin") == {"intents": ["x"]}
    assert keep_verbatim({}, "tin") == {}


def test_words_keeps_codes_and_numbers_whole_and_ignores_case():
    assert words("I-20 là gì? TP.HCM 8.5") == {"i-20", "là", "gì", "tp.hcm", "8.5"}


# ---- độ tin cậy ----
def test_confidence_is_the_probability_of_the_tokens_that_spell_the_type():
    content = '{"intents":[{"type":"knowledge","span":"a"}]}'
    tokens = [
        {"token": '{"intents":[{"type":"', "logprob": -5.0},  # ngoài giá trị type: không tính
        {"token": "know", "logprob": -0.1},
        {"token": "ledge", "logprob": -0.2},
        {"token": '","span":"a"}]}', "logprob": -3.0},  # chữ " đầu nằm ngoài giá trị: không tính
    ]
    assert "".join(t["token"] for t in tokens) == content

    assert label_confidence(content, tokens) == pytest.approx(math.exp(-0.3))
    assert label_confidence(content, []) == 1.0


def test_confidence_of_a_message_is_the_weakest_intent():
    content = '{"intents":[{"type":"knowledge","span":"a"},{"type":"profile_review","span":"b"}]}'
    tokens = [
        {"token": '{"intents":[{"type":"', "logprob": -9.0},
        {"token": "know", "logprob": -0.1},
        {"token": "ledge", "logprob": -0.2},
        {"token": '","span":"a"},{"type":"', "logprob": -9.0},
        {"token": "profile", "logprob": -1.0},
        {"token": "_review", "logprob": -0.5},
        {"token": '","span":"b"}]}', "logprob": -9.0},
    ]
    assert "".join(t["token"] for t in tokens) == content

    assert label_confidence(content, tokens) == pytest.approx(
        math.exp(-1.5)
    )  # ý profile_review thấp hơn ý knowledge


def test_confidence_is_one_when_the_content_has_no_type():
    assert label_confidence("không phải json", [{"token": "x", "logprob": -1.0}]) == 1.0


def test_the_prompt_names_every_type_and_the_list_of_intents():
    prompt = OllamaSlm(FakeSlm()).prompt

    assert all(intent in prompt for intent in INTENTS)
    assert '"intents"' in prompt and "luôn đứng đầu" in prompt and "cac_y" not in prompt


# ---- OllamaClient.chat_json_scored ----
async def test_chat_json_scored_asks_for_logprobs_and_returns_them_with_the_raw_content():
    sent = {}
    logprobs = [{"token": "{", "logprob": -0.01}]

    def handler(request: httpx.Request) -> httpx.Response:
        sent["body"] = json.loads(request.content)
        return httpx.Response(200, json={"message": {"content": '{"ok": true}'}, "logprobs": logprobs})

    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama.test")
    result = await OllamaClient(http, model="qwen3:4b").chat_json_scored(
        [{"role": "user", "content": "hi"}], {}
    )

    assert result == ({"ok": True}, '{"ok": true}', logprobs)
    assert sent["body"]["logprobs"] is True and sent["body"]["model"] == "qwen3:4b"
    assert sent["body"]["think"] is False and sent["body"]["options"]["temperature"] == 0.0


async def test_chat_json_scored_raises_when_content_is_not_json():
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"message": {"content": "xin chào"}})

    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama.test")
    with pytest.raises(OllamaError):
        await OllamaClient(http, model="qwen3:4b").chat_json_scored([], {})


# ---- định dạng đầu ra (settings.slm_format) ----
def test_output_format_turns_the_config_into_what_ollama_gets():
    assert output_format("json") == "json"
    assert output_format("schema") is SLM_TIN_SCHEMA
    with pytest.raises(ValueError, match="slm_format"):
        output_format("jsn")


async def test_the_chosen_output_format_is_sent_to_ollama():
    slm = FakeSlm(GOOD)

    await OllamaSlm(slm, "json").read("ai sửa bài luận ở HN", None, "")

    assert slm.calls[0][1] == "json"


async def test_chat_json_scored_sends_plain_json_mode_when_asked():
    sent = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent["body"] = json.loads(request.content)
        return httpx.Response(200, json={"message": {"content": '{"ok": true}'}})

    http = httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://ollama.test")
    await OllamaClient(http, model="usas-slm").chat_json_scored([{"role": "user", "content": "hi"}], "json")

    assert sent["body"]["format"] == "json"
