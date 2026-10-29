"""Gợi ý trường trong chat, bước 1: advisor báo cho .NET biết cần chạy gợi ý trường (action = "recommend").

Advisor không tự chạy gợi ý: pipeline (lọc, chấm, chọn, lưu) nằm ở .NET RecommendationService.
"""

from dataclasses import dataclass

from app.schemas.chat import ChatMessage, ChatRequest, ChatResponse, ChatState
from app.services.chat_flow import ChatFlow, SkillOutput
from app.services.skills.skill_lo_trinh import RECOMMEND_ACTION, Route, action_for


@dataclass
class FakePrediction:
    skills: list[str]
    probs: dict[str, float]


class PickerOf:
    def __init__(self, *skills: str) -> None:
        self._skills = list(skills)

    async def predict(self, step, last_question, message, missing):
        return FakePrediction(self._skills, {s: 0.9 for s in self._skills})


class SkillWith:
    def __init__(self, answer: str, action: str | None = None) -> None:
        self._output = SkillOutput(answer, action=action)

    async def run(self, state, message):
        return self._output


def request(message: str = "có") -> ChatRequest:
    return ChatRequest(messages=[ChatMessage(role="user", content=message)], state=ChatState(current_step="B4"))


def route(name: str) -> Route:
    return Route(route=name, alt_routes=[], codes=[], english_status="ok", months_to_deadline=None)


def test_only_the_direct_route_asks_for_a_school_list():
    assert action_for(route("direct")) == RECOMMEND_ACTION == "recommend"
    # dự bị, 2+2, dời kỳ: hệ thống chưa có danh sách trường cho các lộ trình này
    for name in ("pathway", "cc_2plus2", "postpone"):
        assert action_for(route(name)) is None


def test_response_has_no_action_by_default_and_sends_it_as_camel_case():
    assert ChatResponse(answer="x", state=ChatState()).action is None

    body = ChatResponse(answer="x", state=ChatState(), action="recommend").model_dump(by_alias=True)

    assert body["action"] == "recommend"


async def test_chat_flow_passes_the_action_of_a_skill_to_the_response():
    # skill_lo_trinh cần đủ hồ sơ mới chạy; ở đây chỉ kiểm tra ChatFlow chuyển action nên dùng skill không cần hồ sơ
    flow = ChatFlow(PickerOf("skill_tra_loi"), {"skill_tra_loi": SkillWith("...", RECOMMEND_ACTION)})

    res = await flow.reply(request())

    assert res.action == "recommend"


async def test_chat_flow_has_no_action_when_no_skill_asks_for_one():
    flow = ChatFlow(PickerOf("skill_tra_loi"), {"skill_tra_loi": SkillWith("Phí SEVIS là ...")})

    res = await flow.reply(request("phí sevis bao nhiêu"))

    assert res.action is None
