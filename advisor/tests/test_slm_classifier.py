"""Bộ phân loại SLM qua Ollama: gửi đúng đầu vào, đọc đúng đầu ra, tính độ tin cậy, sai thì thử lại rồi lùi về mơ hồ."""

import json
import math

import httpx
import pytest

from app.schemas.chat import ChatState, ProfileField
from app.schemas.slm import INTENTS, SLM_SCHEMA
from app.services.ollama import OllamaClient, OllamaError
from app.services.slm_classifier import OllamaSlm, label_confidence, output_format

GOOD = {"type": "trung_tam", "nhan_phu": None, "thuc_the": {"dich_vu": "SUA_BAI_LUAN", "thanh_pho": "HN"}}


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


async def test_a_good_answer_is_read_into_the_contract():
    slm = FakeSlm(GOOD)

    result = await OllamaSlm(slm).read("ai sửa bài luận ở HN", None, "")

    assert (result.type, result.nhan_phu, result.thuc_the) == ("trung_tam", None, GOOD["thuc_the"])
    assert result.confidence == 1.0  # không có logprobs thì không đo được
    assert slm.calls[0][1] is SLM_SCHEMA


async def test_the_model_gets_the_prompt_and_the_same_input_columns_as_the_dataset():
    slm = FakeSlm(GOOD)

    await OllamaSlm(slm).read("8.5 ạ", {"gpa": "8.0"}, "Điểm GPA của bạn?")

    system, _ = slm.calls[0][0]
    assert system["role"] == "system" and "slm_phan_loai" in system["content"]
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


async def test_an_answer_that_breaks_the_rules_is_retried_once():
    wrong_order = {"type": "trung_tam", "nhan_phu": "kien_thuc", "thuc_the": {}}
    slm = FakeSlm(wrong_order, GOOD)

    result = await OllamaSlm(slm).read("x", None, "")

    assert result.type == "trung_tam" and len(slm.calls) == 2


async def test_two_failures_fall_back_to_an_ambiguous_message_with_zero_confidence():
    slm = FakeSlm(OllamaError("down"), {"type": "nhu_cau"})

    result = await OllamaSlm(slm).read("x", None, "")

    assert (result.type, result.thuc_the, result.confidence) == ("ngoai_pham_vi", {"loai_con": "mo_ho"}, 0.0)


async def test_values_not_written_in_the_message_are_dropped():
    answer = {
        "type": "ho_so",
        "nhan_phu": None,
        "thuc_the": {"gpa": "8.0", "truong": "null", "major": "computer science"},
    }
    slm = FakeSlm(answer)

    result = await OllamaSlm(slm).read("em học Computer Science nên chọn trường nào", {"gpa": "8.0"}, "")

    # gpa chép từ hồ sơ, truong là "null" tự điền: bỏ. major có trong tin (không phân biệt hoa thường): giữ
    assert result.thuc_the == {"major": "computer science"}


def test_confidence_is_the_probability_of_the_tokens_that_spell_the_type():
    content = '{"type":"kien_thuc","nhan_phu":null}'
    tokens = [
        {"token": '{"type":"', "logprob": -5.0},  # ngoài giá trị type: không tính
        {"token": "kien", "logprob": -0.1},
        {"token": "_thuc", "logprob": -0.2},
        {"token": '","nhan_phu":null}', "logprob": -3.0},  # chữ " đầu nằm ngoài giá trị: không tính
    ]

    assert label_confidence(content, tokens) == pytest.approx(math.exp(-0.3))
    assert label_confidence(content, []) == 1.0


def test_the_prompt_names_every_type_and_the_priority_rule():
    prompt = OllamaSlm(FakeSlm()).prompt

    assert all(intent in prompt for intent in INTENTS)
    assert "ho_so > kich_ban > kien_thuc > trung_tam" in prompt


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


# ---- script đo ----
def test_the_eval_score_counts_labels_and_entities():
    from scripts.eval_slm import score

    records = [
        {"output": {"type": "kien_thuc", "nhan_phu": "trung_tam", "thuc_the": {"thanh_pho": "HN"}}},
        {"output": {"type": "mac_dinh", "nhan_phu": None, "thuc_the": {"gpa": "8.5"}}},
    ]
    preds = [
        {"type": "kien_thuc", "nhan_phu": None, "thuc_the": {"thanh_pho": "Hà Nội"}},
        {"type": "kien_thuc", "nhan_phu": None, "thuc_the": {"gpa": "8.5"}},
    ]

    result = score(records, preds)

    assert (result["nhan_chinh"], result["nhan_phu"], result["nhan_phu_tin_hai_y"]) == (0.5, 0.5, 0.0)
    assert (result["dung_khoa_thuc_the"], result["dung_het_thuc_the"]) == (1.0, 0.5)
    assert result["theo_loai"] == {"mac_dinh": 0.0, "kien_thuc": 1.0}
    assert result["nham_nhieu_nhat"] == ["mac_dinh -> kien_thuc: 1"]


# ---- định dạng đầu ra (settings.slm_format) ----
def test_output_format_turns_the_config_into_what_ollama_gets():
    assert output_format("json") == "json"
    assert output_format("schema") is SLM_SCHEMA
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
