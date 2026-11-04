"""Hỏi giải thích một trường trong chat, bước 1: khuôn dữ liệu và chuyển action.

.NET gửi danh sách trường của lần gợi ý gần nhất mỗi lượt (schools); skill trả action = "explain" kèm mã trường
(action_data) để .NET gọi phần tư vấn từng trường đã có sẵn.
"""

from app.schemas.chat import ChatMessage, ChatRequest, ChatResponse, ChatState, ResultSchool
from app.services.branches import NEED_RESULTS
from app.services.chat_flow import SkillOutput
from tests.fakes import flow_with, tin

# SLM đọc là tư vấn theo hồ sơ, có nêu tên trường: nhánh tư vấn gọi skill_explain
EXPLAIN = tin(("ho_so", {"truong": "Iowa"}))

SCHOOLS = [
    ResultSchool(code="iowa", name="University of Iowa", group="R"),
    ResultSchool(code="asu", name="Arizona State University"),
]


class SkillSeen:
    """Skill ghi lại state nó thấy, rồi trả action explain kèm mã trường."""

    def __init__(self) -> None:
        self.schools: list[ResultSchool] = []

    async def run(self, state, message):
        self.schools = state.schools
        return SkillOutput(
            "Mình xem trường này giúp bạn nhé.", action="explain", action_data={"code": "iowa"}
        )


def request(schools: list[ResultSchool] | None, state: ChatState | None = None) -> ChatRequest:
    state = state or ChatState(current_step="B4", has_results=True)
    return ChatRequest(
        messages=[ChatMessage(role="user", content="vì sao Iowa khó vào")], state=state, schools=schools
    )


def test_response_sends_action_and_data_as_camel_case():
    res = ChatResponse(answer="x", state=ChatState(), action="explain", action_data={"code": "iowa"})

    body = res.model_dump(by_alias=True)

    assert body["action"] == "explain"
    assert body["actionData"] == {"code": "iowa"}
    assert ChatResponse(answer="x", state=ChatState()).action_data == {}


def test_request_reads_the_school_list_in_camel_case():
    req = ChatRequest.model_validate(
        {
            "messages": [{"role": "user", "content": "hi"}],
            "schools": [{"code": "iowa", "name": "University of Iowa", "group": "R"}],
        }
    )

    assert req.schools == [ResultSchool(code="iowa", name="University of Iowa", group="R")]


async def test_chat_flow_gives_the_skill_the_school_list_and_returns_its_action():
    skill = SkillSeen()
    flow = flow_with({"skill_explain": skill}, EXPLAIN)

    res = await flow.reply(request(SCHOOLS))

    assert skill.schools == SCHOOLS
    assert res.action == "explain"
    assert res.action_data == {"code": "iowa"}


async def test_chat_flow_keeps_the_old_school_list_when_net_sends_none():
    skill = SkillSeen()
    flow = flow_with({"skill_explain": skill}, EXPLAIN)

    await flow.reply(request(None, ChatState(current_step="B4", has_results=True, schools=SCHOOLS)))

    assert skill.schools == SCHOOLS


# Học sinh gợi ý ở trang Gợi ý trường, chat chưa hề chạy gợi ý: .NET vẫn gửi danh sách đã lưu, nên không được hỏi "cần có danh sách trước"
WHAT_IF = tin(("kich_ban", {"che_do": "what_if", "thay_doi": [{"yeu_to": "sat", "gia_tri": "1500"}]}))


async def test_a_school_list_from_dotnet_means_the_student_already_has_results():
    flow = flow_with({}, WHAT_IF)

    res = await flow.reply(request(SCHOOLS, ChatState(current_step="B4")))  # chat chưa bật has_results

    assert res.action == "simulate"
    assert res.state.has_results is True


async def test_without_a_school_list_the_scenario_asks_for_one_first():
    flow = flow_with({}, WHAT_IF)

    for schools in (None, []):
        res = await flow.reply(request(schools, ChatState(current_step="B4")))

        assert res.action is None
        assert res.answer == NEED_RESULTS
        assert res.state.has_results is False


async def test_no_school_list_keeps_results_the_chat_already_knew_about():
    flow = flow_with({}, WHAT_IF)

    res = await flow.reply(request(None, ChatState(current_step="B4", has_results=True)))

    assert res.action == "simulate"
