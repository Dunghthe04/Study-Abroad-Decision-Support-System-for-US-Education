"""B3. Phần thuần của scripts/eval_skill_khuon: đổi ngược hồ sơ chữ, việc học sinh hỏi, số ngoài dữ liệu, đếm kết quả."""

import json
from datetime import date

import pytest

from app.schemas.chat import ProfileField
from app.schemas.slm import SkillRequest
from app.services.advice_writer import AdviceWriter
from app.services.claude import ClaudeError
from app.services.skill_input import profile_to_skill_input
from app.services.skills.skill_lo_trinh import choose_route
from scripts.eval_skill_khuon import (
    allowed_for,
    evaluate,
    numbers_outside,
    outcome,
    profile_of,
    read_rows,
    score,
    tasks_of_record,
    today_of,
    with_budget_tier,
)
from tests.fakes import FakeLLM
from tests.test_skill_template import FULL, TODAY, fields

# Khóa đổi ngược được về đúng giá trị form; hoạt động, giải thưởng giữ dạng chữ
PARSED = [key for key in FULL if key not in ("activities", "awards")]
TINH_SAN = {
    "gpa_level": "3.28 thang 4, thấp (TB các trường 3.73)",
    "budget_level": "36.000 – 44.000 USD mỗi năm, mức sát",
    "timeline": "hôm nay 11/2026, nhập học kỳ Fall 08/2027, còn 9 tháng",
}
HO_SO = {
    "target_level": "đại học",
    "major": "Tâm lý học",
    "intake": "Fall 2027",
    "gpa": "8.2 thang 10",
    "english": "IELTS 6.5",
    "budget": "khoảng 36.000 đến 44.000 USD mỗi năm",
}
GOOD = "**Đánh giá hồ sơ**\nGPA 8.2 thang 10, 3.28 thang 4.\n\n**Lộ trình chuẩn bị**\n- Nộp hồ sơ."


def record(id_: str = "ho_so_001", tasks: list[str] | None = None, skill_md: str = "# skill đáp án") -> dict:
    cac_y = [{"type": "ho_so", "doan": "x", "thuc_the": {"cac_nhiem_vu": tasks}}] if tasks else []
    return {
        "id": id_,
        "type": "ho_so",
        "split": "val",
        "input": {
            "cau_hoi": "Đánh giá hồ sơ và cho em lộ trình",
            "ho_so": HO_SO,
            "cau_he_thong_dang_hoi": "",
        },
        "output": {"cac_y": cac_y},
        "skill_md": skill_md,
        "tinh_san": TINH_SAN,
    }


def test_text_profile_turns_back_into_form_values():
    text = profile_to_skill_input(fields(FULL))

    profile = profile_of(text)

    assert {key: profile[key].value for key in PARSED} == {key: FULL[key] for key in PARSED}
    assert choose_route(profile, TODAY)[0] == choose_route(fields(FULL), TODAY)[0]


def test_none_and_unknown_keep_their_status():
    profile = profile_of({"english": "không có", "sat": "chưa rõ", "gpa": "điểm lạ"})

    assert profile["english"] == ProfileField(status="none")
    assert profile["sat"] == ProfileField(status="unknown")
    assert profile["gpa"].value == "điểm lạ"  # không đọc được thì giữ chữ, không vỡ


@pytest.mark.parametrize(
    ("budget", "level", "tier"),
    [
        ("khoảng 36.000 đến 44.000 USD mỗi năm", "36.000 – 44.000 USD mỗi năm, mức thiếu", "tier_under_20k"),
        ("khoảng 36.000 đến 44.000 USD mỗi năm", "36.000 – 44.000 USD mỗi năm, mức đủ", "tier_50k_70k"),
        ("khoảng 36.000 đến 44.000 USD mỗi năm", None, "khoảng 36.000 đến 44.000 USD mỗi năm"),
        ("khoảng 35.000 đến 50.000 USD mỗi năm", "35.000 – 50.000 USD mỗi năm, mức thiếu", "tier_35k_50k"),
    ],
)
def test_free_budget_text_takes_its_tier_from_the_calculated_level(budget, level, tier):
    profile = profile_of({"budget": budget})

    result = with_budget_tier(profile, {"budget_level": level} if level else {})

    assert result["budget"].value == tier


def test_a_short_budget_leads_the_template_to_community_college():
    profile = profile_of(HO_SO)
    short = with_budget_tier(profile, TINH_SAN | {"budget_level": "x, mức thiếu"})

    assert choose_route(short, TODAY)[0].route == "cc_2plus2"


def test_tasks_come_from_the_ho_so_intents_in_order_without_duplicates():
    row = record()
    row["output"]["cac_y"] = [
        {"type": "ho_so", "thuc_the": {"cac_nhiem_vu": ["lo_trinh", "danh_gia"]}},
        {"type": "truong", "thuc_the": {"cac_nhiem_vu": ["so_sanh"]}},
        {"type": "ho_so", "thuc_the": {"cac_nhiem_vu": ["danh_gia", "chon_truong"]}},
    ]

    assert tasks_of_record(row) == ["lo_trinh", "danh_gia", "chon_truong"]
    assert tasks_of_record(record()) is None


def test_today_comes_from_the_timeline_line():
    assert today_of(record(), TODAY) == date(2026, 11, 1)
    assert today_of(record() | {"tinh_san": {}}, TODAY) == TODAY


def test_numbers_from_the_data_are_allowed_and_invented_ones_are_caught():
    req = SkillRequest(loai="ho_so", cau_hoi="x", ho_so=HO_SO, tinh_san=TINH_SAN)
    allowed = allowed_for(req, profile_of(HO_SO), date(2026, 11, 1))

    text = (
        "GPA 8.2 thang 10 (3.28), IELTS 6.5, ngân sách 36.000 – 44.000, nhập học 08/2027. Học phí 61.500 USD."
    )

    assert numbers_outside(text, allowed) == ["61.500"]


@pytest.mark.parametrize(
    ("text", "attempts", "expected"),
    [
        (None, 2, "hong"),
        ("**Đánh giá hồ sơ** thôi", 1, "thieu_phan"),
        (GOOD, 1, "lan_1"),
        (GOOD, 2, "sau_viet_lai"),
    ],
)
def test_outcome(text, attempts, expected):
    assert outcome(text, attempts, ["danh_gia", "lo_trinh"]) == expected


async def test_each_row_writes_from_both_skills_and_keeps_no_text_by_default():
    llm = FakeLLM(
        {"tra_loi": GOOD},  # dòng 1, khuôn: qua ngay
        {"tra_loi": "Chắc chắn đỗ!"},  # dòng 1, đáp án: hứa đỗ...
        {"tra_loi": GOOD.replace("8.2", "9.9")},  # ...viết lại thì qua nhưng có số bịa
        {"tra_loi": GOOD},  # dòng 2, khuôn
    )
    records = [record("a", ["danh_gia", "lo_trinh"]), record("b", skill_md="")]

    rows = await evaluate(AdviceWriter(llm), records)

    first, second = rows
    assert first["khuon"]["ket_qua"] == "lan_1" and first["khuon"]["so_ngoai"] == []
    assert first["slm"] | {"giay_skill": 0, "giay_viet": 0} == {
        "ket_qua": "sau_viet_lai",
        "so_lan": 2,
        "loi": ["hua_do"],
        "giay_skill": 0,
        "giay_viet": 0,
        "token_vao": 0,
        "token_ra": 0,
        "do_dai": len(GOOD),
        "so_ngoai": ["9.9"],
    }
    assert second["slm"]["ket_qua"] == "khong_co_skill"  # dòng không có skill đáp án: không gọi Claude
    assert len(llm.calls) == 4
    assert all("tra_loi" not in row[s] and "skill" not in row[s] for row in rows for s in ("khuon", "slm"))

    khuon, slm = score(rows, "khuon"), score(rows, "slm")
    assert (khuon["lan_1"], khuon["ti_le_qua"], khuon["dong_co_so_ngoai_du_lieu"]) == (2, 1.0, 0)
    assert (slm["sau_viet_lai"], slm["khong_co_skill"], slm["ti_le_qua"]) == (1, 1, 0.5)
    assert slm["loi_hay_gap"] == ["hua_do: 1"] and slm["so_ngoai_hay_gap"] == ["9.9: 1"]


async def test_template_side_sends_only_its_own_tasks_and_the_other_side_keeps_them_all():
    llm = FakeLLM({"tra_loi": GOOD}, {"tra_loi": GOOD})

    await evaluate(AdviceWriter(llm), [record(tasks=["danh_gia", "chon_truong", "lo_trinh"])])

    khuon, slm = (json.loads(call["messages"][2]["content"])["nhiem_vu"] for call in llm.calls)
    assert khuon == ["đánh giá hồ sơ", "lộ trình chuẩn bị"]  # chọn trường là phần của Dũng
    assert slm == ["đánh giá hồ sơ", "chọn trường", "lộ trình chuẩn bị"]


async def test_each_row_is_saved_as_soon_as_it_is_done_and_rerun_rows_replace_old_ones(tmp_path):
    llm = FakeLLM(*[{"tra_loi": GOOD}] * 6)
    out = tmp_path / "ket_qua.jsonl"
    saved = []

    def save(row: dict) -> None:
        saved.append(row["id"])
        with out.open("a", encoding="utf-8") as f:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")

    await evaluate(AdviceWriter(llm), [record("a"), record("b")], on_row=save)
    await evaluate(AdviceWriter(llm), [record("b")], on_row=save, first=2)  # chạy tiếp từ dòng 2

    assert saved == ["a", "b", "b"]
    assert [row["id"] for row in read_rows(out)] == ["a", "b"]
    assert read_rows(tmp_path / "chua_co.jsonl") == []


async def test_the_template_skill_goes_to_claude_and_slm_skill_comes_from_the_generator():
    llm = FakeLLM({"tra_loi": GOOD}, ClaudeError("HTTP 529"), ClaudeError("HTTP 529"))
    asked = []

    async def slm_generate(req: SkillRequest) -> str | None:
        asked.append(req.cau_hoi)
        return "# skill SLM"

    [row] = await evaluate(AdviceWriter(llm), [record()], slm_generate, keep_text=True)

    assert asked == ["Đánh giá hồ sơ và cho em lộ trình"]
    assert "skill_ho_so_khuon" in llm.calls[0]["messages"][1]["content"]
    assert llm.calls[1]["messages"][1]["content"] == "# skill SLM"
    assert row["slm"]["ket_qua"] == "hong" and row["slm"]["loi"] == ["goi_model", "goi_model"]
    assert row["khuon"]["tra_loi"] == GOOD and "## 8. VÍ DỤ" in row["khuon"]["skill"]
