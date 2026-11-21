"""Bảng chính sách shared/answer_policy.json: mỗi loại ý của SLM và mỗi hành động do AI hay do code viết chữ trả về học sinh.

Backend có test cùng file này (AnswerPolicyTests.cs). Thêm loại ý, nhánh hay hành động mà quên khai báo, hoặc bảng nói
khác code (skill không tồn tại, writer không có), thì test đỏ. Bảng cũng là nội dung dùng cho tài liệu thiết kế.
"""

import importlib
import json
import re
from pathlib import Path

import pytest

from app.schemas.slm import INTENTS
from app.services.branches import build_branches
from tests.fakes import FakeAdviceWriter
from tests.test_skill_format import READERS, WRITERS

POLICY = json.loads(
    (Path(__file__).resolve().parents[2] / "shared" / "answer_policy.json").read_text(encoding="utf-8")
)
INTENT_POLICY: dict[str, dict] = POLICY["intents"]
ACTION_POLICY: dict[str, dict] = POLICY["actions"]
ENTRIES = {
    **{f"intent:{k}": v for k, v in INTENT_POLICY.items()},
    **{f"action:{k}": v for k, v in ACTION_POLICY.items()},
}
WHO = {"code", "claude", "mixed"}
APP_DIR = Path(__file__).resolve().parents[1] / "app"


def skill_files(names: list[str]) -> set[str]:
    return {f"{name}.md" for name in names}


# ---- bảng phủ đủ loại ý và nhánh
def test_every_intent_type_has_a_policy_and_nothing_extra():
    assert set(INTENT_POLICY) == set(INTENTS)


def test_each_policy_names_the_branch_that_really_handles_the_intent():
    dummy = {
        name: object()
        for name in (
            "skill_bac_hoc",
            "skill_ho_so",
            "skill_explain",
            "skill_tra_loi",
            "skill_chuyen_trung_tam",
        )
    }

    branches = build_branches(dummy, FakeAdviceWriter())

    assert {intent: type(branch).__name__ for intent, branch in branches.items()} == {
        intent: policy["branch"] for intent, policy in INTENT_POLICY.items()
    }


# ---- mỗi dòng đủ thông tin theo ai viết
@pytest.mark.parametrize("name", ENTRIES)
def test_each_entry_says_who_writes_and_why(name):
    entry = ENTRIES[name]

    assert entry["who"] in WHO
    assert entry["why"].strip()


@pytest.mark.parametrize("name", [n for n, e in ENTRIES.items() if e["who"] != "code"])
def test_a_claude_entry_always_has_skills_a_writer_and_a_fallback(name):
    entry = ENTRIES[name]

    assert (
        entry["skills"] and entry["writer"] and entry["fallback"].strip()
    )  # Claude không bao giờ là đường duy nhất


@pytest.mark.parametrize("name", [n for n, e in ENTRIES.items() if e["who"] == "code"])
def test_a_code_entry_has_no_writer(name):
    entry = ENTRIES[name]

    assert not entry.get("skills") and not entry.get("writer")


# ---- bảng nói đúng sự thật về code
@pytest.mark.parametrize("name", [n for n, e in ENTRIES.items() if e.get("skills")])
def test_every_listed_skill_exists_and_is_a_writing_skill(name):
    files = skill_files(ENTRIES[name]["skills"])

    assert files <= {p.name for p in (APP_DIR / "skills").glob("*.md")}, files
    assert files <= WRITERS, f"{files - WRITERS} không phải skill viết lời"


def test_every_reading_skill_listed_exists():
    for entry in INTENT_POLICY.values():
        assert skill_files(entry.get("reads", [])) <= READERS


@pytest.mark.parametrize("name", [n for n, e in ENTRIES.items() if e.get("writer")])
def test_the_writer_exists_and_uses_the_skill_the_table_says(name):
    entry = ENTRIES[name]
    module_name, class_name = entry["writer"].rsplit(".", 1)

    module = importlib.import_module(module_name)

    assert hasattr(module, class_name), entry["writer"]
    for constant in ("SKILL_PATH", "PROMPT_PATH"):
        if hasattr(module, constant):
            assert getattr(module, constant).stem in entry["skills"], (
                f"{module_name}.{constant} không có trong bảng"
            )


# ---- hành động
def test_every_emitted_action_is_declared():
    emitted = set()
    for path in APP_DIR.rglob("*.py"):
        emitted |= set(re.findall(r'\baction="([a-z_]+)"', path.read_text(encoding="utf-8")))

    assert emitted <= set(ACTION_POLICY), emitted - set(ACTION_POLICY)


def test_every_action_a_branch_may_emit_is_declared():
    for intent, entry in INTENT_POLICY.items():
        assert set(entry.get("may_emit", [])) <= set(ACTION_POLICY), intent
