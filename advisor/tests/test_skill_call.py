"""Phần dùng chung của 3 lượt: đọc skill, ghép message, gọi lại 1 lần khi sai."""

from app.services.ollama import OllamaError
from app.services.skill_call import SKILLS_DIR, SkillCall, load_skill
from tests.fakes import FakeLLM

SKILL = SKILLS_DIR / "skill_ec.md"


def check_ok(raw):
    if raw.get("ok") is not True:
        raise ValueError("sai")
    return "đã kiểm tra"


async def test_sends_skill_and_compact_json_then_returns_checked_result():
    llm = FakeLLM({"ok": True})
    call = SkillCall(llm, "qwen3:8b", SKILL)
    result = await call.run({"a": 1, "b": "Hà Nội"}, {"type": "object"}, check_ok)

    assert result == "đã kiểm tra"
    system, user = llm.calls[0]["messages"]
    assert system == {"role": "system", "content": call.skill}
    assert user["content"] == '{"a":1,"b":"Hà Nội"}'               # liền, giữ tiếng Việt
    assert llm.calls[0]["schema"] == {"type": "object"}
    assert llm.calls[0]["temperature"] == 0


async def test_retries_once_after_check_fails():
    llm = FakeLLM({"ok": False}, {"ok": True})
    assert await SkillCall(llm, "m", SKILL).run({}, {}, check_ok) == "đã kiểm tra"
    assert len(llm.calls) == 2


async def test_retries_once_after_ollama_error():
    llm = FakeLLM(OllamaError("timeout"), {"ok": True})
    assert await SkillCall(llm, "m", SKILL).run({}, {}, check_ok) == "đã kiểm tra"


async def test_returns_none_after_two_failures_and_stops():
    llm = FakeLLM({"ok": False}, OllamaError("x"), {"ok": True})
    assert await SkillCall(llm, "m", SKILL).run({}, {}, check_ok) is None
    assert len(llm.calls) == 2                                       # không gọi lần thứ 3


def test_load_skill_reads_version_of_all_three_skills():
    for name in ("skill_ec.md", "skill_score.md", "skill_pick.md"):
        text, version = load_skill(SKILLS_DIR / name)
        assert version == "1.1" and len(text) > 1000


def test_load_skill_without_version_is_unknown(tmp_path):
    f = tmp_path / "x.md"
    f.write_text("không có phiên bản", encoding="utf-8")
    assert load_skill(f) == ("không có phiên bản", "unknown")


def test_schema_required_keys_match_properties():
    # Ollama ép theo "required": gõ nhầm 1 dấu cách ("tong_hd ") thì AI bỏ key thật và lượt đó luôn lỗi
    from app.schemas.ec import ec_schema
    from app.schemas.pick import pick_schema
    from app.schemas.score import score_schema

    ec = ec_schema(1)
    assert set(ec["required"]) == set(ec["properties"])
    assert set(ec["properties"]["act"]["items"]["required"]) == set(ec["properties"]["act"]["items"]["properties"])
    item = score_schema(["A"])["properties"]["items"]["items"]
    assert set(item["required"]) == set(item["properties"])
    pick = pick_schema(["A"])
    assert set(pick["required"]) == set(pick["properties"])
