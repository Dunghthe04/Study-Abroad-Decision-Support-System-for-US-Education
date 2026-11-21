"""Phần dùng chung khi gọi AI: đọc skill, ghép message, gọi lại 1 lần khi sai."""

from pydantic import BaseModel

from app.services.ollama import OllamaError
from app.services.skill_call import SkillCall, load_skill
from tests.fakes import FakeLLM

SKILL_TEXT = "skill_version 1.0\nNội dung skill thử."


def skill_file(tmp_path):
    path = tmp_path / "skill.md"
    path.write_text(SKILL_TEXT, encoding="utf-8")
    return path


def check_ok(raw):
    if raw.get("ok") is not True:
        raise ValueError("sai")
    return "đã kiểm tra"


async def test_sends_skill_and_compact_json_then_returns_checked_result(tmp_path):
    llm = FakeLLM({"ok": True})
    call = SkillCall(llm, "qwen3:8b", skill_file(tmp_path))
    result = await call.run({"a": 1, "b": "Hà Nội"}, {"type": "object"}, check_ok)

    assert result == "đã kiểm tra"
    system, user = llm.calls[0]["messages"]
    assert system == {"role": "system", "content": call.skill}
    assert user["content"] == '{"a":1,"b":"Hà Nội"}'  # liền, giữ tiếng Việt
    assert llm.calls[0]["schema"] == {"type": "object"}
    assert llm.calls[0]["temperature"] == 0


async def test_retries_once_after_check_fails(tmp_path):
    llm = FakeLLM({"ok": False}, {"ok": True})
    assert await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check_ok) == "đã kiểm tra"
    assert len(llm.calls) == 2


async def test_retries_once_after_ollama_error(tmp_path):
    llm = FakeLLM(OllamaError("timeout"), {"ok": True})
    assert await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check_ok) == "đã kiểm tra"


async def test_returns_none_after_two_failures_and_stops(tmp_path):
    llm = FakeLLM({"ok": False}, OllamaError("x"), {"ok": True})
    assert await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check_ok) is None
    assert len(llm.calls) == 2  # không gọi lần thứ 3


def check_with_reason(raw):
    raise ValueError("thiếu tên trường")


async def test_the_retry_names_the_defect_and_shows_the_previous_result(tmp_path):
    llm = FakeLLM({"ok": False, "x": 1}, {"ok": True})

    def check(raw):
        if raw.get("ok") is not True:
            raise ValueError("thiếu tên trường")
        return "đã kiểm tra"

    assert await SkillCall(llm, "m", skill_file(tmp_path)).run({"a": 1}, {}, check) == "đã kiểm tra"

    first, second = llm.calls[0]["messages"], llm.calls[1]["messages"]
    assert second[:2] == first and len(second) == 3  # yêu cầu cũ giữ nguyên, thêm một tin nêu lỗi
    note = second[2]
    assert note["role"] == "user"  # Claude chỉ đọc tin system và user
    assert "thiếu tên trường" in note["content"]
    assert '{"ok":false,"x":1}' in note["content"]  # kết quả trước, để model biết sửa chỗ nào
    assert len(first) == 2  # lần đầu không bị thêm gì


async def test_after_a_connection_error_the_retry_is_sent_unchanged(tmp_path):
    llm = FakeLLM(OllamaError("timeout"), {"ok": True})

    await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check_ok)

    assert llm.calls[1]["messages"] == llm.calls[0]["messages"]  # không có lỗi nội dung nào để nêu


async def test_a_schema_error_is_summarised_without_the_pydantic_link(tmp_path):
    class Out(BaseModel):
        n: int

    llm = FakeLLM({"n": "abc"}, {"n": 1})

    result = await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, Out.model_validate)

    assert result == Out(n=1)
    note = llm.calls[1]["messages"][-1]["content"]
    assert "n:" in note and "errors.pydantic.dev" not in note


async def test_a_long_defect_and_previous_result_are_cut(tmp_path):
    llm = FakeLLM({"a": "x" * 5000}, {"a": "x" * 5000})

    def check(raw):
        raise ValueError("@" * 1000)

    assert await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check) is None

    note = llm.calls[1]["messages"][-1]["content"]
    assert len(note) < 2500 and note.count("@") == 400  # lý do cắt ở 400 chữ, kết quả trước ở 1500


async def test_an_error_without_a_message_still_gets_a_reason(tmp_path):
    llm = FakeLLM({"ok": False}, {"ok": True})

    def check(raw):
        if raw.get("ok") is not True:
            raise ValueError()
        return "đã kiểm tra"

    await SkillCall(llm, "m", skill_file(tmp_path)).run({}, {}, check)

    assert "kết quả không hợp lệ" in llm.calls[1]["messages"][-1]["content"]


def test_load_skill_reads_version(tmp_path):
    text, version = load_skill(skill_file(tmp_path))
    assert version == "1.0" and "Nội dung skill thử" in text


def test_load_skill_without_version_is_unknown(tmp_path):
    f = tmp_path / "x.md"
    f.write_text("không có phiên bản", encoding="utf-8")
    assert load_skill(f) == ("không có phiên bản", "unknown")
