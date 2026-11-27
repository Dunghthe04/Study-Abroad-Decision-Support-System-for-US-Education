"""Hàm chấm của bộ kịch bản (slm/eval/chay_kich_ban.py) phải chấm đúng, nếu không báo cáo sẽ báo lỗi oan hoặc bỏ sót."""

from datetime import date

import pytest

from slm.eval.chay_kich_ban import HUA_DO, cham_nhan, cham_noi_dung, kiem_chung, lap_cau, moc_cu_khong_bao
from slm.eval.kich_ban import KHONG, KICH_BAN

TODAY = date(2026, 11, 27)


def test_past_deadline_must_be_called_passed():
    assert moc_cu_khong_bao("- 01/11/2026: Hạn nộp MIT", TODAY)
    assert moc_cu_khong_bao("Đạt SAT 1550 (hạn 09/2026).", TODAY)
    assert not moc_cu_khong_bao("- 01/11/2026: Hạn nộp MIT (đã qua)", TODAY)
    assert not moc_cu_khong_bao("Đạt SAT 1550 (hạn 09/2026, đã quá hạn)", TODAY)
    assert not moc_cu_khong_bao("- 01/05/2027: Nộp visa\n(hạn 12/2026)", TODAY)
    assert not moc_cu_khong_bao("Số liệu thu thập ngày 01/09/2026.", TODAY)


@pytest.mark.parametrize("text", ["Bạn chắc chắn đỗ trường này.", "Trung tâm đảm bảo đậu cho bạn."])
def test_admission_promise_is_flagged(text):
    assert HUA_DO.search(text)


@pytest.mark.parametrize(
    "text", ["Không thể đảm bảo đỗ.", "Mình không chắc chắn đậu hay rớt.", "Chưa chắc chắn được nhận."]
)
def test_denying_a_promise_is_not_flagged(text):
    assert not HUA_DO.search(text)


def test_internal_text_and_bare_yes_are_flagged():
    fails = kiem_chung('Có, {"tra_loi": "x"}', ["Mã: RAW-12"], TODAY)
    assert len(fails) == 3
    assert kiem_chung("Bắt đầu từ bây giờ không sớm đâu.", ["EducationUSA"], TODAY) == []


def test_label_grading():
    assert cham_nhan(["knowledge"], ["knowledge"], False) == (None, None)
    # Cùng loại, khác chi tiết của knowledge chỉ cảnh báo
    error, warning = cham_nhan(["knowledge"], ["knowledge:search"], False)
    assert error is None and warning
    # Khác loại con của ngoài phạm vi là lỗi
    assert cham_nhan(["out_of_scope:exception"], ["out_of_scope:greeting"], False)[0]
    assert cham_nhan(["knowledge"], ["profile_review:assessment"], False)[0]
    assert cham_nhan(["knowledge"], [], True)[0]


def test_waiting_answer_is_not_graded_on_content():
    turn = {"action": "simulate", "co": [r"1450"]}
    assert cham_noi_dung(turn, "Để mình thử.", "simulate", [], {}, {}) == []
    assert cham_noi_dung(turn, "Mình chưa hiểu.", None, [], {}, {}) == [
        "cần giao backend 'simulate', thực tế None",
        "thiếu ý /1450/",
    ]


def test_profile_checks():
    turn = {"action": KHONG, "luu": {"intake": "Spring 2027"}, "giu": ["gpa"]}
    assert cham_noi_dung(turn, "ok", None, [], {"intake": "Spring 2027", "gpa": 8}, {"gpa": 8}) == []
    assert len(cham_noi_dung(turn, "ok", "recommend", [], {"gpa": 9}, {"gpa": 8})) == 3


def test_repeat_ignores_waiting_and_fixed_refusals():
    before = {"noi": "a", "tra_loi": "X"}
    same = {"noi": "b", "action": None, "nhan": ["profile_review:roadmap"], "tra_loi": "X"}
    assert lap_cau(same, before)
    assert not lap_cau({**same, "action": "school_info"}, before)
    assert not lap_cau({**same, "nhan": ["out_of_scope:exception"]}, before)
    assert not lap_cau({**same, "noi": "a"}, before)
    assert not lap_cau(same, None)


def test_scenario_data_is_well_formed():
    allowed = {"noi", "nhan", "action", "co", "khong", "toi_da", "nguon", "luu", "giu", "dien_ho_so"}
    names = [s["ten"] for s in KICH_BAN]
    assert len(names) == len(set(names))
    for scenario in KICH_BAN:
        assert scenario["luot"], scenario["ten"]
        for turn in scenario["luot"]:
            assert set(turn) <= allowed, (scenario["ten"], turn)
            assert "noi" in turn or "dien_ho_so" in turn
