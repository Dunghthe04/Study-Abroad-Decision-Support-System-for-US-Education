"""Chỗ đăng ký skill cũ: build_handlers tạo skill để các nhánh (branches.py) dùng bên trong."""

import inspect

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.schemas.slm import SlmResult
from app.services.chat_flow import SkillOutput
from app.services.skill_rules import ORDER
from app.services.skills import build_handlers
from tests.fakes import flow_with


def test_build_handlers_returns_a_dict_and_does_not_need_a_real_llm():
    handlers = build_handlers(llm=object(), model="qwen3:8b")

    assert isinstance(handlers, dict)


def test_registered_names_are_known_skill_names():
    for name in build_handlers(llm=object(), model="qwen3:8b"):
        assert name in ORDER, f"{name} không phải tên skill đã biết"


def test_every_registered_handler_has_an_async_run():
    for name, handler in build_handlers(llm=object(), model="qwen3:8b").items():
        assert inspect.iscoroutinefunction(handler.run), f"{name}.run phải là async def"


def test_each_call_builds_a_fresh_registry():
    first = build_handlers(llm=object(), model="m")
    replaced = object()
    first["skill_ho_so"] = replaced  # sửa bản này không được ảnh hưởng lần gọi sau

    assert build_handlers(llm=object(), model="m").get("skill_ho_so") is not replaced


async def test_a_skill_in_the_registry_is_run_by_its_branch_in_chat_flow():
    class FakeSkill:
        async def run(self, state, message):
            return SkillOutput("skill mới đã chạy")

    flow = flow_with({**build_handlers(llm=object(), model="m"), "skill_ho_so": FakeSkill()})
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="em được 1420")], state=ChatState(current_step="B2")
    )

    res = await flow.reply(req)

    # SLM đọc là trả lời hồ sơ, đang ở B2 nên nhánh hồ sơ gọi skill_ho_so
    assert res.answer == "skill mới đã chạy"


async def test_out_of_scope_works_without_any_registered_skill():
    flow = flow_with({}, SlmResult(type="ngoai_pham_vi", thuc_the={"loai_con": "tam_biet"}))
    req = ChatRequest(messages=[ChatMessage(role="user", content="em cảm ơn ạ")], state=ChatState())

    res = await flow.reply(req)

    assert res.skills == ["ngoai_pham_vi"] and res.answer


async def test_knowledge_skill_embeds_with_the_embedder_and_writes_with_the_llm():
    from app.services.knowledge import KnowledgeHit
    from app.services.skills.skill_tra_loi import TraLoiHandler

    class Embedder:
        async def embed(self, texts):
            return [[0.1, 0.2]]

    class Llm:  # giống Gemini: chỉ có chat_json, không có embed
        async def chat_json(self, messages, schema, temperature=0.0):
            return {"enough": True, "answer": "I-20 là giấy xác nhận trường nhận bạn để xin visa du học."}

    class Retriever:
        async def search(self, vector, study_level):
            assert vector == [0.1, 0.2]
            return [
                KnowledgeHit(
                    "K1",
                    "I-20 là gì",
                    "I-20 là giấy xác nhận trường nhận bạn để xin visa.",
                    None,
                    None,
                    None,
                    None,
                    0.9,
                )
            ]

    handler = TraLoiHandler(Llm(), "gemini", Retriever(), 0.72, Embedder())

    res = await handler.run(ChatState(), "I-20 là gì")

    assert "I-20" in res.answer and res.citations


async def test_a_question_below_the_threshold_is_logged_as_a_knowledge_miss(caplog):
    import logging

    from app.services.knowledge import KnowledgeHit
    from app.services.skills.skill_tra_loi import NO_BASIS, TraLoiHandler

    class Embedder:
        async def embed(self, texts):
            return [[0.1]]

    class Retriever:
        async def search(self, vector, study_level):
            return [KnowledgeHit("K9", "câu khác", "nội dung khác", None, None, None, None, 0.40)]

    handler = TraLoiHandler(object(), "gemini", Retriever(), 0.72, Embedder())

    with caplog.at_level(logging.INFO, logger="advisor.knowledge"):
        res = await handler.run(ChatState(), "học bổng ở Mars")

    assert res.answer == NO_BASIS
    assert "knowledge_miss" in caplog.text and "nearest=K9" in caplog.text and "0.400" in caplog.text
