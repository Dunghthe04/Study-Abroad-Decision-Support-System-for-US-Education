"""Trả lời câu hỏi tỉnh / thành: chỉ nhận đúng tên tỉnh (backend kiểm, kể cả tên gọi khác); câu khác để model đọc;
không rõ thì hỏi lại kèm ô chọn lấy từ API tỉnh / thành.

Lỗi thật 28/11/2026: mọi câu tối đa 6 chữ, không có dấu hỏi đều bị coi là tên tỉnh ("3.6 nhé", "gợi ý trường cho em"),
model không được đọc nên câu hỏi của học sinh bị mất.
"""

import httpx
import pytest

from app.schemas.chat import ChatMessage, ChatRequest, ChatState, ProfileField
from app.services.chat_flow import SkillOutput
from app.services.replies import BACK_TO_QUESTION
from app.services.skills import build_handlers, province_catalog
from app.services.skills.skill_tinh import (
    PROVINCE_ID,
    PROVINCE_QUESTION,
    PROVINCE_UNCLEAR,
    SET_PROVINCE,
    ask_province,
    with_province_question,
)
from tests.fakes import FakeLLM, flow_with, tin

NAMES = ["Hà Nội", "Huế", "Đà Nẵng", "Hải Phòng", "TP. Hồ Chí Minh"]
ALIASES = {"sài gòn": "TP. Hồ Chí Minh", "tp. hcm": "TP. Hồ Chí Minh"}


def backend(request: httpx.Request) -> httpx.Response:
    if request.url.path.endswith("/resolve"):
        return httpx.Response(200, json={"province": ALIASES.get(request.url.params["name"].lower())})
    return httpx.Response(200, json=NAMES)


def down(request: httpx.Request) -> httpx.Response:
    raise httpx.ConnectError("backend tắt")


def use(handler) -> None:
    province_catalog.configure(
        httpx.AsyncClient(transport=httpx.MockTransport(handler), base_url="http://backend")
    )


@pytest.fixture(autouse=True)
async def catalog():
    use(backend)
    await province_catalog.names()
    yield
    province_catalog.configure(None)


def asked_state() -> ChatState:
    profile = {"gpa": ProfileField(value={"value": 8.5, "scale": 10}, status="confirmed")}
    state = ChatState(
        current_step="B3", profile=profile, roadmap={"mode": "average", "provinceMissing": True}
    )
    ask_province(state)
    return state


class CountingSlm:
    def __init__(self, result) -> None:
        self.result, self.calls = result, 0

    async def classify(self, state, message):
        self.calls += 1
        return self.result


class Knowledge:
    async def run(self, state, message, entities=None):
        return SkillOutput("câu của kho")


async def reply(state: ChatState, text: str, slm=None, llm=None):
    flow = flow_with(
        {**build_handlers(llm or FakeLLM(), "m"), "skill_tra_loi": Knowledge()}, slm or tin("knowledge")
    )
    flow._slm = CountingSlm(slm or tin("knowledge"))
    response = await flow.reply(ChatRequest(messages=[ChatMessage(role="user", content=text)], state=state))
    return response, flow._slm.calls


def options_of(widget) -> list[str]:
    assert widget is not None and widget.type == "select"
    return [o["value"] for o in widget.data["fields"][0]["options"]]


# ---- hỏi kèm ô chọn
def test_the_question_comes_with_the_list_from_the_api():
    output = ask_province(ChatState())

    assert options_of(output.widget) == NAMES
    assert output.widget.data["format"] == "{province}"


def test_the_question_added_after_the_roadmap_answer_keeps_the_list():
    state = ChatState(roadmap={"mode": "average", "provinceMissing": True})

    output = with_province_question(state, SkillOutput("lời tư vấn"))

    assert options_of(output.widget) == NAMES


# ---- đúng tên tỉnh: lưu thẳng, không cần model
@pytest.mark.parametrize(
    ("text", "name"),
    [
        ("Hà Nội", "Hà Nội"),
        ("em ở Đà Nẵng", "Đà Nẵng"),
        ("dạ em ở sài gòn ạ", "TP. Hồ Chí Minh"),
        ("mình sống tại TP. HCM nhé", "TP. Hồ Chí Minh"),
        ("ha noi", "Hà Nội"),
    ],
)
async def test_a_province_name_is_saved_without_the_model(text, name):
    response, calls = await reply(asked_state(), text)

    assert (response.action, response.action_data) == (SET_PROVINCE, {"province": name})
    assert calls == 0 and response.state.question_id is None


# ---- câu khác: model đọc, rồi hỏi lại kèm ô chọn
@pytest.mark.parametrize(
    "text", ["gợi ý trường cho em", "học phí MIT", "thôi em hỏi visa", "nhà em gần Hà Nội"]
)
async def test_another_question_is_read_and_the_province_is_asked_again_with_the_list(text):
    response, calls = await reply(asked_state(), text)

    assert calls == 1 and response.action != SET_PROVINCE
    assert response.answer == "câu của kho\n\n" + BACK_TO_QUESTION + PROVINCE_QUESTION
    assert options_of(response.widget) == NAMES


# Model coi là câu khai hồ sơ nhưng không có giá trị hồ sơ nào: là câu trả lời tỉnh không rõ
async def test_an_unclear_reply_is_asked_again_with_the_list():
    response, calls = await reply(asked_state(), "nhà em ở quê ấy", tin("collect_profile"))

    assert calls == 1 and response.action != SET_PROVINCE
    assert response.answer == PROVINCE_UNCLEAR
    assert options_of(response.widget) == NAMES
    assert response.state.question_id == PROVINCE_ID


async def test_it_stops_asking_after_two_more_tries():
    state = asked_state()
    for _ in range(2):
        response, _calls = await reply(state, "chỗ em ở ấy", tin("collect_profile"))
        state = response.state

    response, _calls = await reply(state, "chỗ em ở ấy", tin("collect_profile"))

    assert response.state.question_id is None and response.widget is None
    assert response.answer != PROVINCE_UNCLEAR


# Câu có giá trị hồ sơ thì xử lý như khai hồ sơ, không bị coi là tên tỉnh
async def test_a_profile_value_is_not_taken_as_a_province():
    llm = FakeLLM({"values": {"sat": 1500}, "none_fields": []})

    response, _calls = await reply(
        asked_state(), "SAT em được 1500", tin(("collect_profile", {"sat": "1500"})), llm
    )

    assert response.action != SET_PROVINCE
    assert response.state.profile["sat"].value == 1500


# ---- backend lỗi: vẫn hỏi bằng chữ, không đoán bừa là tên tỉnh
async def test_without_the_api_nothing_is_guessed():
    use(down)

    question = ask_province(ChatState())
    response, calls = await reply(asked_state(), "3.6 nhé")

    assert question.widget is None and question.answer == PROVINCE_QUESTION
    assert calls == 1 and response.action != SET_PROVINCE


# Lỗi thấy khi chạy model thật: câu nói về nơi ở bị gán relatives_state, đọc ra không có gì để ghi
async def test_a_reply_that_gives_nothing_new_is_asked_again():
    llm = FakeLLM({"values": {}, "none_fields": []})
    slm = tin(("collect_profile", {"relatives_state": "chỗ em ở quê ấy"}))

    response, _calls = await reply(asked_state(), "chỗ em ở quê ấy", slm, llm)

    assert response.answer == PROVINCE_UNCLEAR
    assert options_of(response.widget) == NAMES
    assert response.state.question_id == PROVINCE_ID
    assert [
        k for k in response.state.asked if k != PROVINCE_ID
    ] == []  # câu hồ sơ kế tiếp chưa bị tính là đã hỏi
