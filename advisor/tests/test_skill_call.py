"""Phần dùng chung khi gọi AI: đọc skill, ghép message, gọi lại 1 lần khi sai."""

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


def test_load_skill_reads_version(tmp_path):
    text, version = load_skill(skill_file(tmp_path))
    assert version == "1.0" and "Nội dung skill thử" in text


def test_load_skill_without_version_is_unknown(tmp_path):
    f = tmp_path / "x.md"
    f.write_text("không có phiên bản", encoding="utf-8")
    assert load_skill(f) == ("không có phiên bản", "unknown")
