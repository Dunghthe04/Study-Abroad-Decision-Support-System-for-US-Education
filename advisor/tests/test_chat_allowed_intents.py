"""allowed_intents (gói Free/Premium do .NET gửi): bỏ ý gói không cho, không còn ý nào thì trả câu cần Premium.
Nhánh giả (FakeBranch) là chỗ duy nhất gọi LLM, nên ran == [] nghĩa là không gọi Claude.
"""

from fastapi.testclient import TestClient

from app.api.deps import get_chat_flow
from app.main import create_app
from app.services.chat_flow import ChatFlow
from app.services.replies import CONTINUE_CHAT, MENU, PREMIUM_REQUIRED
from app.services.skill_rules import REQUIRED_B3
from app.services.slm_contract import FixedSlm
from tests.fakes import tin
from tests.test_chat_api import Echo, read_lines
from tests.test_chat_flow import confirmed, make_flow, request, waiting_advice

FREE = ["knowledge", "out_of_scope"]


def gated(message: str, allowed: list[str] | None, pending: str | None = None, **state_fields):
    req = request(message, pending, **state_fields)
    req.allowed_intents = allowed
    return req


def assert_locked(res, ran, locked):
    assert ran == []  # không nhánh nào chạy: không gọi LLM
    assert res.action == "premium_required" and res.answer == PREMIUM_REQUIRED
    assert res.locked_intents == locked and res.skills == []


async def test_none_runs_every_intent_like_before():
    ran: list[str] = []
    flow, _ = make_flow(tin("knowledge", "profile_review"), ran)

    res = await flow.reply(gated("x", None))

    assert sorted(ran) == ["knowledge", "profile_review"]
    assert res.locked_intents == [] and res.action is None


async def test_free_knowledge_question_is_answered():
    ran: list[str] = []
    flow, _ = make_flow(tin("knowledge"), ran)

    res = await flow.reply(gated("I-20 là gì", FREE))

    assert ran == ["knowledge"] and res.answer == "trả lời của knowledge"
    assert res.locked_intents == [] and res.action is None


async def test_free_profile_review_is_locked_without_running_any_branch():
    ran: list[str] = []
    flow, counting = make_flow(tin("profile_review"), ran)

    res = await flow.reply(gated("hồ sơ em vào được trường nào", FREE))

    assert_locked(res, ran, ["profile_review"])
    assert counting.calls == ["hồ sơ em vào được trường nào"]  # SLM vẫn đọc tin
    assert res.slm.intents == ["profile_review"]  # tóm tắt SLM giữ như cũ


async def test_mixed_message_runs_the_allowed_part_and_lists_the_locked_types():
    ran: list[str] = []
    flow, _ = make_flow(tin("knowledge", "profile_review", ("scenario", {"mode": "goal_seeking"})), ran)

    res = await flow.reply(gated("x", FREE))

    assert ran == ["knowledge"] and res.skills == ["knowledge"]
    assert res.locked_intents == ["profile_review", "scenario"] and res.action is None


async def test_pending_question_button_is_locked_without_the_slm():
    ran: list[str] = []
    flow, counting = make_flow(tin("knowledge"), ran)

    res = await flow.reply(gated("Bỏ qua", FREE, pending="F2"))

    assert counting.calls == []
    assert_locked(res, ran, ["collect_profile"])


async def test_try_menu_shortcut_is_locked():
    ran: list[str] = []
    flow, counting = make_flow(tin("knowledge"), ran)

    res = await flow.reply(gated(MENU["scenario"].value, FREE, has_results=True))

    assert counting.calls == []
    assert_locked(res, ran, ["scenario"])


async def test_back_from_form_keeps_the_waiting_advice_when_locked():
    ran: list[str] = []
    flow, _ = make_flow(tin("knowledge"), ran)
    req = gated(
        CONTINUE_CHAT,
        FREE,
        current_step="B3",
        profile=confirmed(*REQUIRED_B3),
        pending_advice=waiting_advice(),
    )

    res = await flow.reply(req)

    assert_locked(res, ran, ["profile_review"])
    assert res.state.pending_advice is not None  # nâng cấp xong câu tư vấn vẫn còn


def test_wire_names_are_camel_case_on_both_endpoints():
    app = create_app()
    flow = ChatFlow(FixedSlm(tin("knowledge", "profile_review")), {"knowledge": Echo()})
    app.dependency_overrides[get_chat_flow] = lambda: flow
    client = TestClient(app)
    body = {"messages": [{"role": "user", "content": "x"}], "allowedIntents": FREE}

    plain = client.post("/api/v1/chat", json=body).json()
    final = read_lines(client.post("/api/v1/chat/stream", json=body))[-1]

    assert plain["lockedIntents"] == ["profile_review"] and plain["skills"] == ["knowledge"]
    assert final["type"] == "final" and final["response"] == plain

    body["allowedIntents"] = ["out_of_scope"]
    locked = client.post("/api/v1/chat", json=body).json()
    assert locked["action"] == "premium_required" and locked["answer"] == PREMIUM_REQUIRED
    assert read_lines(client.post("/api/v1/chat/stream", json=body))[-1]["response"] == locked
