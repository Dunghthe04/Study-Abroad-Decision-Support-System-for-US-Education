"""Việc 2.1: chỗ đăng ký skill. Chưa skill nào được đăng ký, nhưng khung phải đúng để người làm skill chỉ thêm một dòng."""

import inspect

from app.schemas.chat import ChatMessage, ChatRequest, ChatState
from app.services.chat_flow import ChatFlow, SkillOutput
from app.services.skill_rules import ORDER
from app.services.skills import build_handlers

# Hai skill tạm đã có sẵn trong ChatFlow nên không được đăng ký lại ở build_handlers
BUILT_IN = {"ngoai_le", "ket_thuc"}


def test_build_handlers_returns_a_dict_and_does_not_need_a_real_llm():
    handlers = build_handlers(llm=object(), model="qwen3:8b")

    assert isinstance(handlers, dict)


def test_registered_names_are_real_skill_labels_not_the_built_in_ones():
    handlers = build_handlers(llm=object(), model="qwen3:8b")

    for name in handlers:
        assert name in ORDER, f"{name} không phải nhãn skill của model chọn skill"
        assert name not in BUILT_IN, f"{name} đã có sẵn trong ChatFlow"


def test_every_registered_handler_has_an_async_run():
    handlers = build_handlers(llm=object(), model="qwen3:8b")

    for name, handler in handlers.items():
        assert inspect.iscoroutinefunction(handler.run), f"{name}.run phải là async def"


def test_each_call_builds_a_fresh_registry():
    first = build_handlers(llm=object(), model="m")
    first["skill_ho_so"] = object()  # sửa bản này không được ảnh hưởng lần gọi sau

    assert "skill_ho_so" not in build_handlers(llm=object(), model="m")


async def test_registry_keeps_the_built_in_skills_working_in_chat_flow():
    class Picker:
        async def predict(self, step, last_question, message, missing):
            class Result:
                skills = ["ket_thuc"]
                probs = {"ket_thuc": 0.9}

            return Result()

    flow = ChatFlow(Picker(), build_handlers(llm=object(), model="m"))
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="em cảm ơn ạ")], state=ChatState(current_step="B2")
    )

    res = await flow.reply(req)

    assert res.state.current_step == "END"  # ket_thuc vẫn chạy dù đã nạp danh sách đăng ký


async def test_a_skill_added_to_the_registry_is_run_by_chat_flow():
    class FakeSkill:
        async def run(self, state, message):
            return SkillOutput("skill mới đã chạy")

    flow = ChatFlow(handlers={**build_handlers(llm=object(), model="m"), "skill_ho_so": FakeSkill()})
    req = ChatRequest(
        messages=[ChatMessage(role="user", content="em được 1420")], state=ChatState(current_step="B2")
    )

    res = await flow.reply(req)

    assert res.answer == "skill mới đã chạy"  # chưa có model nên dùng skill của bước B2 là skill_ho_so
