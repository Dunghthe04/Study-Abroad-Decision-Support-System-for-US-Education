"""Bộ mẫu đo SLM đọc ngoại khóa: nhãn nhất quán với luật của skill_ec.md, và script đo tính đúng."""

from app.services.ec_reader import SKILL_PATH
from app.services.skill_call import SkillCall
from scripts.build_ec_dataset import ROLES, records
from scripts.eval_ec import model_input, predict, score
from tests.fakes import FakeLLM


def test_there_are_enough_samples_and_every_group_is_covered():
    rows = records()

    assert len(rows) >= 60
    assert {r["group"] for r in rows} == {
        "vai_tro_ro",
        "ha_pham_vi",
        "giu_pham_vi_cao",
        "khong_noi_vai_tro",
        "ha_vai_tro",
        "tieng_anh",
        "ca_kho",
    }
    assert any(r["hard"] for r in rows)


def test_ids_are_unique_and_texts_are_not_repeated():
    rows = records()

    assert len({r["id"] for r in rows}) == len(rows)
    assert len({r["text"] for r in rows}) == len(rows)


def test_every_label_follows_the_skill_rules():
    for r in records():
        assert r["expected_role"] in ROLES, r["id"]
        assert 1 <= r["scope_chosen"] <= 5 and 1 <= r["expected_scope"] <= 5, r["id"]
        assert r["expected_scope"] <= r["scope_chosen"], f"{r['id']}: đáp án không được nâng phạm vi"
        assert r["role_chosen"] in ROLES, r["id"]
        assert r["expected_role"] <= r["role_chosen"], f"{r['id']}: đáp án không được nâng vai trò"
        assert 0 <= r["months"] <= 120, r["id"]
        assert 1 <= len(r["text"]) <= 500, r["id"]


def test_the_set_covers_every_role_and_both_keep_and_lower_cases():
    rows = records()

    assert {r["expected_role"] for r in rows} == set(ROLES)
    assert any(r["expected_scope"] < r["scope_chosen"] for r in rows)  # hạ phạm vi
    assert any(r["expected_role"] < r["role_chosen"] for r in rows)  # hạ vai trò
    assert any(r["expected_role"] == r["role_chosen"] for r in rows)  # giữ vai trò
    assert {r["role_chosen"] for r in rows} == set(ROLES)  # học sinh chọn đủ 4 vai trò
    assert any(r["expected_scope"] == r["scope_chosen"] for r in rows)  # giữ phạm vi
    assert {r["expected_scope"] for r in rows} == {1, 2, 3, 4, 5}


def test_model_input_carries_the_role_and_scope_the_student_chose():
    row = records()[0]

    assert model_input(row) == {
        "act": [
            {
                "text": row["text"],
                "role": row["role_chosen"],
                "scope": row["scope_chosen"],
                "months": row["months"],
            }
        ]
    }


# ---------------------------------------------------------------- script đo


def perfect(rows):
    return [[(r["expected_role"], r["expected_scope"])] for r in rows]


def test_score_of_perfect_predictions_is_one():
    rows = records()

    result = score(rows, perfect(rows))

    assert result["dung_ca_hai"] == 1.0
    assert result["dung_vai_tro"] == 1.0 and result["dung_pham_vi"] == 1.0
    assert result["nang_pham_vi_trai_luat"] == 0 and result["nang_vai_tro_trai_luat"] == 0
    assert result["on_dinh_giua_cac_lan"] == 1.0
    assert result["tra_sai_khuon"] == 0
    assert result["ha_pham_vi_dung"] == 1.0 and result["ha_vai_tro_dung"] == 1.0
    assert result["giu_nguyen_dung"] == 1.0


def test_score_counts_a_raised_scope_and_a_wrong_role():
    rows = records()
    runs = perfect(rows)
    runs[0] = [(0.3, rows[0]["scope_chosen"])]  # sai vai trò
    runs[1] = [(rows[1]["expected_role"], min(5, rows[1]["scope_chosen"] + 1))]  # nâng phạm vi

    result = score(rows, runs)

    assert result["dung_vai_tro"] == round((len(rows) - 1) / len(rows), 3)
    assert result["nang_pham_vi_trai_luat"] == (1 if rows[1]["scope_chosen"] < 5 else 0)
    assert result["dung_ca_hai"] < 1.0


def test_score_counts_a_raised_role():
    rows = records()
    runs = perfect(rows)
    i = next(k for k, r in enumerate(rows) if r["role_chosen"] < 1.0)
    runs[i] = [(1.0, rows[i]["expected_scope"])]  # nâng vai trò cao hơn lựa chọn

    result = score(rows, runs)

    assert result["nang_vai_tro_trai_luat"] == 1


def test_score_treats_an_invalid_output_as_wrong_and_counts_it():
    rows = records()
    runs = perfect(rows)
    runs[3] = [None]

    result = score(rows, runs)

    assert result["tra_sai_khuon"] == 1
    assert result["dung_ca_hai"] == round((len(rows) - 1) / len(rows), 3)


def test_score_reports_instability_between_runs():
    rows = records()
    runs = [[p[0], p[0]] for p in perfect(rows)]
    runs[0] = [(0.3, 1), (0.8, 1)]  # hai lần cho hai kết quả khác nhau

    result = score(rows, runs)

    assert result["on_dinh_giua_cac_lan"] == round((len(rows) - 1) / len(rows), 3)


def test_score_reports_hard_cases_separately():
    rows = records()
    runs = perfect(rows)
    for i, r in enumerate(rows):
        if r["hard"]:
            runs[i] = [(0.3, r["scope_chosen"])] if r["expected_role"] != 0.3 else [(1.0, r["scope_chosen"])]

    result = score(rows, runs)

    assert result["dung_ca_hai_mau_ro"] == 1.0
    assert result["dung_ca_hai_mau_kho"] < 1.0


async def test_predict_calls_the_model_once_per_sample_and_repeat_with_temperature_zero():
    rows = records()[:3]
    answers = [
        {"act": [{"role": r["expected_role"], "scope": r["expected_scope"]}]} for r in rows for _ in range(2)
    ]
    llm = FakeLLM(*answers)

    runs = await predict(SkillCall(llm, "fake", SKILL_PATH), rows, repeat=2)

    assert runs == [[(r["expected_role"], r["expected_scope"])] * 2 for r in rows]
    assert len(llm.calls) == 6
    assert all(c["temperature"] == 0.0 for c in llm.calls)
    assert llm.calls[0]["schema"]["properties"]["act"]["minItems"] == 1


async def test_predict_marks_a_sample_invalid_when_the_model_answers_badly_twice():
    rows = records()[:1]
    llm = FakeLLM({"act": []}, {"act": []})  # sai số phần tử cả hai lần

    runs = await predict(SkillCall(llm, "fake", SKILL_PATH), rows, repeat=1)

    assert runs == [[None]]
