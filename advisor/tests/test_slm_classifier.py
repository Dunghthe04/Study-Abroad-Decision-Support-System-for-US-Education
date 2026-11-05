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
    "type": "trung_tam",
    "doan": "ai sửa bài luận ở HN",
    "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "HN"},
}
GOOD = {"cac_y": [CENTER]}


def y(kind, doan, **thuc_the):
    return {"type": kind, "doan": doan, "thuc_the": thuc_the}


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

    assert result.types() == ["trung_tam"]
    assert (result.cac_y[0].doan, result.cac_y[0].thuc_the) == (CENTER["doan"], CENTER["thuc_the"])
    assert result.confidence == 1.0  # không có logprobs thì không đo được
    assert slm.calls[0][1] is SLM_TIN_SCHEMA


async def test_the_model_gets_the_prompt_and_the_same_input_columns_as_the_dataset():
    slm = FakeSlm(GOOD)

    await OllamaSlm(slm).read("8.5 ạ", {"gpa": "8.0"}, "Điểm GPA của bạn?")

    system, _ = slm.calls[0][0]
    assert system["role"] == "system" and "slm_phan_loai_cac_y" in system["content"]
    assert user_input(slm) == {
        "cau_hoi": "8.5 ạ",
        "ho_so": {"gpa": "8.0"},
        "cau_he_thong_dang_hoi": "Điểm GPA của bạn?",
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
    assert sent["ho_so"] == {"gpa": "8.5", "awards": '["HSG tỉnh"]'}  # trường chưa có giá trị thì bỏ
    assert sent["cau_he_thong_dang_hoi"] == "Hiện bạn đang học lớp mấy, hoặc đang làm gì?"


# ---- tin nhiều ý ----
async def test_several_intents_come_back_in_the_order_the_code_wants_them():
    answer = {
        "cac_y": [
            y("kien_thuc", "I-20 là gì"),
            y("mac_dinh", "em GPA 3.5", gpa="em GPA 3.5"),
            y("ho_so", "tư vấn trường"),
            y("ho_so", "em nên nộp trường nào"),
        ]
    }

    result = await OllamaSlm(FakeSlm(answer)).read(
        "I-20 là gì, em GPA 3.5, tư vấn trường, em nên nộp trường nào", None, ""
    )

    assert result.types() == ["mac_dinh", "kien_thuc", "ho_so"]  # mac_dinh lên đầu, hai ho_so gộp một
    assert result.cac_y[2].doan == "tư vấn trường, em nên nộp trường nào"
    assert result.profile_values() == {"gpa": "em GPA 3.5"}


async def test_every_question_is_kept_however_many_the_model_found():
    asked = [f"câu {i}" for i in range(1, 9)]
    answer = {"cac_y": [y("kien_thuc", doan) for doan in asked]}

    result = await OllamaSlm(FakeSlm(answer)).read(", ".join(asked), None, "")

    assert [i.doan for i in result.cac_y] == asked


async def test_a_greeting_next_to_a_real_question_is_dropped():
    answer = {"cac_y": [y("ngoai_pham_vi", "chào", loai_con="tam_biet"), y("kien_thuc", "I-20 là gì")]}

    result = await OllamaSlm(FakeSlm(answer)).read("chào, I-20 là gì", None, "")

    assert result.types() == ["kien_thuc"]


# ---- sai thì thử lại, rồi lùi về mơ hồ ----
async def test_an_answer_that_breaks_the_rules_is_retried_once():
    no_mode = {"cac_y": [y("kich_ban", "nếu em lên SAT 1500 thì sao")]}  # kich_ban thiếu che_do
    slm = FakeSlm(no_mode, GOOD)

    result = await OllamaSlm(slm).read("x", None, "")

    assert result.types() == ["trung_tam"] and len(slm.calls) == 2


@pytest.mark.parametrize(
    "bad",
    [
        {"type": "kien_thuc", "nhan_phu": None, "thuc_the": {}},  # khuôn cũ, không có cac_y
        {"cac_y": []},
        {"cac_y": "không phải danh sách"},
        {"cac_y": ["không phải object"]},
        {"cac_y": [y("loai_la", "x")]},
    ],
)
async def test_an_answer_in_the_wrong_shape_is_retried_not_crashed(bad):
    slm = FakeSlm(bad, GOOD)

    result = await OllamaSlm(slm).read("ai sửa bài luận ở HN", None, "")

    assert result.types() == ["trung_tam"] and len(slm.calls) == 2


async def test_two_failures_fall_back_to_an_ambiguous_message_with_zero_confidence():
    slm = FakeSlm(OllamaError("down"), {"cac_y": "hỏng"})

    result = await OllamaSlm(slm).read("x", None, "")

    assert result.types() == ["ngoai_pham_vi"]
    assert result.cac_y[0].thuc_the == {"loai_con": "mo_ho"} and result.confidence == 0.0


# ---- bỏ giá trị và đoạn không có trong tin ----
async def test_values_not_written_in_the_message_are_dropped_per_intent():
    answer = {
        "cac_y": [
            y("mac_dinh", "em học Computer Science", gpa="8.0", major="computer science"),
            y("ho_so", "chọn trường nào", truong="null"),
        ]
    }

    result = await OllamaSlm(FakeSlm(answer)).read(
        "em học Computer Science nên chọn trường nào", {"gpa": "8.0"}, ""
    )

    # gpa chép từ hồ sơ, truong là "null" tự điền: bỏ. major có trong tin (không phân biệt hoa thường): giữ
    assert result.cac_y[0].thuc_the == {"major": "computer science"}
    assert result.cac_y[1].thuc_the == {}


def test_keep_verbatim_keeps_a_doan_made_of_words_of_the_message_even_when_rewritten():
    raw = {"cac_y": [y("kien_thuc", "I-20 là gì"), y("kien_thuc", "SAT là gì")]}

    cleaned = keep_verbatim(raw, "I-20 và SAT là gì?")

    assert [i["doan"] for i in cleaned["cac_y"]] == ["I-20 là gì", "SAT là gì"]


def test_keep_verbatim_replaces_a_doan_with_words_the_message_does_not_have():
    cleaned = keep_verbatim({"cac_y": [y("kien_thuc", "visa F-1 là gì")]}, "I-20 là gì")
    assert cleaned["cac_y"][0]["doan"] == "I-20 là gì"

    empty = keep_verbatim({"cac_y": [{"type": "kien_thuc", "doan": "  ", "thuc_the": {}}]}, "I-20 là gì")
    assert empty["cac_y"][0]["doan"] == "I-20 là gì"

    missing = keep_verbatim({"cac_y": [{"type": "kien_thuc", "thuc_the": {}}]}, "I-20 là gì")
    assert missing["cac_y"][0]["doan"] == "I-20 là gì"


def test_keep_verbatim_leaves_a_broken_shape_for_the_contract_to_reject():
    assert keep_verbatim({"cac_y": "x"}, "tin") == {"cac_y": "x"}
    assert keep_verbatim({"cac_y": ["x"]}, "tin") == {"cac_y": ["x"]}
    assert keep_verbatim({}, "tin") == {}


def test_words_keeps_codes_and_numbers_whole_and_ignores_case():
    assert words("I-20 là gì? TP.HCM 8.5") == {"i-20", "là", "gì", "tp.hcm", "8.5"}


# ---- độ tin cậy ----
def test_confidence_is_the_probability_of_the_tokens_that_spell_the_type():
    content = '{"cac_y":[{"type":"kien_thuc","doan":"a"}]}'
    tokens = [
        {"token": '{"cac_y":[{"type":"', "logprob": -5.0},  # ngoài giá trị type: không tính
        {"token": "kien", "logprob": -0.1},
        {"token": "_thuc", "logprob": -0.2},
        {"token": '","doan":"a"}]}', "logprob": -3.0},  # chữ " đầu nằm ngoài giá trị: không tính
    ]
    assert "".join(t["token"] for t in tokens) == content

    assert label_confidence(content, tokens) == pytest.approx(math.exp(-0.3))
    assert label_confidence(content, []) == 1.0


def test_confidence_of_a_message_is_the_weakest_intent():
    content = '{"cac_y":[{"type":"kien_thuc","doan":"a"},{"type":"ho_so","doan":"b"}]}'
    tokens = [
        {"token": '{"cac_y":[{"type":"', "logprob": -9.0},
        {"token": "kien", "logprob": -0.1},
        {"token": "_thuc", "logprob": -0.2},
        {"token": '","doan":"a"},{"type":"', "logprob": -9.0},
        {"token": "ho", "logprob": -1.0},
        {"token": "_so", "logprob": -0.5},
        {"token": '","doan":"b"}]}', "logprob": -9.0},
    ]
    assert "".join(t["token"] for t in tokens) == content

    assert label_confidence(content, tokens) == pytest.approx(math.exp(-1.5))  # ý ho_so thấp hơn ý kien_thuc


def test_confidence_is_one_when_the_content_has_no_type():
    assert label_confidence("không phải json", [{"token": "x", "logprob": -1.0}]) == 1.0


def test_the_prompt_names_every_type_and_the_list_of_intents():
    prompt = OllamaSlm(FakeSlm()).prompt

    assert all(intent in prompt for intent in INTENTS)
    assert '"cac_y"' in prompt and "luôn đứng đầu" in prompt and "nhan_phu" not in prompt


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
