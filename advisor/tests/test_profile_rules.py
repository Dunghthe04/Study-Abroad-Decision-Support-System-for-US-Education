"""So luật của advisor với file luật chung shared/profile_rules.json (.NET cũng có test so với file này)."""

import json
from pathlib import Path

import pytest

from app.services.skills import validators as v
from app.services.skills.commit_rules import STUDY_LEVELS
from app.services.skills.skill_bac_hoc import CURRENT_LEVELS, MAX_MAJOR_LEN

RULES_PATH = Path(__file__).resolve().parents[2] / "shared" / "profile_rules.json"
RULES = json.loads(RULES_PATH.read_text(encoding="utf-8"))


def test_study_levels():
    assert STUDY_LEVELS == set(RULES["studyLevels"])


def test_current_education_levels():
    assert list(CURRENT_LEVELS) == RULES["currentEducationLevels"]


def test_gpa_scales():
    assert list(v.GPA_SCALES) == RULES["gpaScales"]


def test_cert_limits():
    expected = {cert: (r["min"], r["max"], r["step"]) for cert, r in RULES["certLimits"].items()}
    assert v.CERT_LIMITS == expected


def test_english_certs():
    assert list(v.ENGLISH_CERTS) == RULES["englishCerts"]


def test_budget_tiers():
    # File chung chỉ ghi trần; sàn của mỗi mức là trần của mức trước (mức đầu từ 0)
    tiers = RULES["budgetTiers"]
    floors = [0, *(t["maxUsd"] for t in tiers[:-1])]
    assert v.BUDGET_RANGES == {t["id"]: (floor, t["maxUsd"]) for t, floor in zip(tiers, floors, strict=True)}
    assert v.MAX_BUDGET_USD == RULES["maxAnnualBudgetUsd"]


def test_enums():
    assert list(v.SCHOLARSHIP_LEVELS) == RULES["scholarshipNeeds"]
    assert list(v.ACTIVITY_ROLES) == RULES["activityRoles"]
    assert list(v.IMPACT_LEVELS) == RULES["impactLevels"]


def test_max_lengths():
    lengths = RULES["maxLengths"]
    assert MAX_MAJOR_LEN == lengths["major"]
    assert v.MAX_NAME_LEN == lengths["activityName"]
    assert v.MAX_STATE_LEN == lengths["state"]


# Giá trị biên: cùng các số này được test ở .NET (ProfileRulesTests)
@pytest.mark.parametrize("cert", sorted(RULES["certLimits"]))
def test_cert_boundaries(cert):
    r = RULES["certLimits"][cert]
    assert v.check_score(cert, r["min"]) == r["min"]
    assert v.check_score(cert, r["max"]) == r["max"]
    with pytest.raises(v.AskAgain):
        v.check_score(cert, r["min"] - r["step"])
    with pytest.raises(v.AskAgain):
        v.check_score(cert, r["max"] + r["step"])


def test_budget_tier_boundaries():
    tiers = RULES["budgetTiers"]
    for tier, upper in zip(tiers, tiers[1:], strict=False):
        # Giống form: usd < maxUsd
        assert v.budget_tier(tier["maxUsd"] - 1) == tier["id"]
        assert v.budget_tier(tier["maxUsd"]) == upper["id"]
