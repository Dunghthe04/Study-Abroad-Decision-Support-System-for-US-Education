from app.services.ollama import OllamaError
from scripts.eval_skill import eval_records, evaluate, score
from scripts.export_slm_chat import skill_rows
from tests.test_skill_generator import REQ, FakeSlm, good_skill


def record(id_: str, split: str = "val", skill_md: str = "skill đáp án", type_: str = "ho_so") -> dict:
    """Một dòng dữ liệu gốc mang đúng câu hỏi, hồ sơ, dòng tính sẵn của REQ trong test_skill_generator."""
    return {
        "id": id_,
        "type": type_,
        "split": split,
        "skill_md": skill_md,
        "input": {"cau_hoi": REQ.cau_hoi, "ho_so": REQ.ho_so},
        "tinh_san": REQ.tinh_san,
    }


def test_eval_rows_are_exactly_the_skill_rows_exported_as_val():
    records = [record(f"ho_so_{i:03}", split="train") for i in range(17)]
    records += [record("ho_so_100"), record("ho_so_101", skill_md=""), record("kb_001", type_="kich_ban")]

    exported = [row["messages"][-1]["content"] for row in skill_rows(records, "P", "val")]
    chosen = eval_records(records)

    assert [r["id"] for r in chosen] == ["ho_so_100", "ho_so_000", "ho_so_008", "ho_so_016"]
    assert len(exported) == len(chosen)


async def test_each_row_reports_first_try_retry_or_failure_with_the_reason():
    slm = FakeSlm(
        good_skill(),  # dòng 1: qua ngay
        good_skill().replace("8.5", "x"),  # dòng 2: lần 1 bỏ mất số...
        good_skill(),  # ...thử lại thì qua
        "## 1. Nhiệm vụ\nthiếu mục",  # dòng 3: sai khuôn
        OllamaError("hết giờ"),  # ...rồi gọi model lỗi
    )

    rows = await evaluate(slm, [record("a"), record("b"), record("c")])

    assert [row["ket_qua"] for row in rows] == ["lan_1", "sau_thu_lai", "hong"]
    assert rows[0]["loi_tung_lan"] == [None] and rows[0]["skill"] == good_skill().strip()
    assert (
        rows[1]["loi_tung_lan"][0].startswith("skill bỏ mất số của hồ sơ")
        and rows[1]["loi_tung_lan"][1] is None
    )
    assert rows[2]["loi_tung_lan"][1] == "gọi model: hết giờ" and rows[2]["skill"] is None
    assert all(len(row["giay_tung_lan"]) == len(row["loi_tung_lan"]) for row in rows)


def test_score_counts_outcomes_errors_and_times():
    rows = [
        {"ket_qua": "lan_1", "loi_tung_lan": [None], "giay_tung_lan": [10.0]},
        {"ket_qua": "sau_thu_lai", "loi_tung_lan": ["skill bỏ mất số của hồ sơ: ['12']", None],
         "giay_tung_lan": [20.0, 30.0]},
        {"ket_qua": "hong", "loi_tung_lan": ["skill bỏ mất số của hồ sơ: ['08']", "gọi model: hết giờ"],
         "giay_tung_lan": [40.0, 120.0]},
    ]  # fmt: skip

    result = score(rows)

    assert (result["qua_lan_1"], result["qua_sau_thu_lai"], result["hong_ca_2_lan"]) == (1, 1, 1)
    assert result["ti_le_qua"] == 0.667
    assert result["loi_hay_gap"] == ["skill bỏ mất số của hồ sơ: 2", "gọi model: 1"]
    assert result["giay_moi_lan_goi"] == {"trung_binh": 44.0, "p90": 40.0, "lau_nhat": 120.0}
    assert result["giay_moi_cau_lau_nhat"] == 160.0
