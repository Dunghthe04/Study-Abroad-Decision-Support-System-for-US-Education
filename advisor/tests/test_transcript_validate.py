"""Bộ kiểm tra JSON Claude trả ở bước B: tên môn tiếng Việt chuẩn, điểm nguyên văn, đủ số, kỳ hiểu được."""

from app.services.transcript_validate import drop_bad_cells, retry_message, validate_parsed

SOURCE = """| Môn | Toán | Ngữ văn | Vật lý | Công nghệ |
| Lớp 10 HK1 | 8,0 | 7,5 | 6,8 | 9,1 |
| Lớp 10 HK2 | 8,4 | 7,9 | 7,2 | 9,0 |"""


def _data(subjects1=None, subjects2=None, term1="HK1", **extra) -> dict:
    s1 = (
        subjects1
        if subjects1 is not None
        else [
            {"name": "Toán", "score": "8,0"},
            {"name": "Ngữ văn", "score": "7,5"},
            {"name": "Vật lý", "score": "6,8"},
            {"name": "Công nghệ", "score": "9,1"},
        ]
    )
    s2 = (
        subjects2
        if subjects2 is not None
        else [
            {"name": "Toán", "score": "8,4"},
            {"name": "Ngữ văn", "score": "7,9"},
            {"name": "Vật lý", "score": "7,2"},
            {"name": "Công nghệ", "score": "9,0"},
        ]
    )
    return {
        "scale": "10",
        "level": "thpt",
        "language": "vi",
        "terms": [
            {"grade": "Lớp 10", "term": term1, "subjects": s1},
            {"grade": "Lớp 10", "term": "HK2", "subjects": s2},
        ],
        **extra,
    }


def _rules(issues) -> list[str]:
    return [i.rule for i in issues]


def test_ket_qua_dung_khong_co_loi():
    assert validate_parsed(_data(), SOURCE) == []


def test_r1_thieu_terms_la_loi_schema():
    issues = validate_parsed({"scale": "10", "level": "thpt"}, SOURCE)
    assert _rules(issues) == ["R1"]


def test_r1_bo_qua_khi_khong_phai_bang_diem_hoac_thang_khac():
    assert validate_parsed({"scale": "other", "level": "unknown", "terms": []}, "Hóa đơn tiền điện") == []
    assert validate_parsed({"scale": "4", "level": "dai_hoc", "terms": []}, "GPA 3.5") == []


def test_r2_sai_dau_ten_mon_goi_y_ten_dung():
    data = _data()
    data["terms"][0]["subjects"][1]["name"] = "Ngũ văn"
    data["terms"][0]["subjects"][3]["name"] = "Công nghề"
    issues = validate_parsed(data, SOURCE)
    assert _rules(issues) == ["R2", "R2"]
    assert "Ngữ văn" in issues[0].message and "Công nghệ" in issues[1].message
    assert (issues[0].term_index, issues[0].subject_index) == (0, 1)


def test_r2_chep_sai_chu_gan_dung_cung_bat():
    data = _data()
    data["terms"][1]["subjects"][0]["name"] = "Tiénd Anh"
    issues = validate_parsed(data, SOURCE)
    assert _rules(issues) == ["R2"] and "Tiếng Anh" in issues[0].message


def test_r3_mojibake_va_hai_dau_thanh_mot_am_tiet():
    data = _data()
    data["terms"][0]["subjects"][1]["name"] = "Ngá»¯ vÄƒn"
    data["terms"][0]["subjects"][2]["name"] = "Vật lýý́"
    assert _rules(validate_parsed(data, SOURCE)) == ["R3", "R3"]


def test_r4_ten_tieng_anh_chua_dich():
    data = _data()
    data["terms"][0]["subjects"][0]["name"] = "Mathematics"
    data["terms"][0]["subjects"][2]["name"] = "Data Structures"
    assert _rules(validate_parsed(data, SOURCE)) == ["R4", "R4"]


def test_r4_nguon_tieng_anh_phai_co_ten_goc_co_trong_van_ban():
    source = "Fall 2023 | Mathematics 8.5 | Data Structures 9.0 | Physics 7.0"
    ok = {
        "scale": "10",
        "level": "dai_hoc",
        "language": "en",
        "terms": [
            {
                "grade": "",
                "term": "Fall 2023",
                "subjects": [
                    {"name": "Toán", "score": "8.5", "name_original": "Mathematics"},
                    {"name": "Cấu trúc dữ liệu", "score": "9.0", "name_original": "Data Structures"},
                    {"name": "Vật lý", "score": "7.0", "name_original": "Physics"},
                ],
            }
        ],
    }
    assert validate_parsed(ok, source) == []

    missing = {
        **ok,
        "terms": [
            {
                **ok["terms"][0],
                "subjects": [
                    {"name": "Toán", "score": "8.5"},
                    {"name": "Cấu trúc dữ liệu", "score": "9.0", "name_original": "Algorithms"},
                    {"name": "Vật lý", "score": "7.0", "name_original": "Physics"},
                ],
            }
        ],
    }
    assert _rules(validate_parsed(missing, source)) == ["R4", "R4"]


def test_r4b_dich_sai_nghia_mon_co_san():
    source = "Fall 2023 | Chemistry 8.5"
    data = {
        "scale": "10",
        "level": "dai_hoc",
        "language": "en",
        "terms": [
            {
                "grade": "",
                "term": "Fall 2023",
                "subjects": [
                    {"name": "Sinh học", "score": "8.5", "name_original": "Chemistry"},
                ],
            }
        ],
    }
    issues = validate_parsed(data, source)
    assert _rules(issues) == ["R4b"] and "Hóa học" in issues[0].message


def test_r5_diem_bia_hoac_ngoai_thang():
    data = _data()
    data["terms"][0]["subjects"][0]["score"] = "8,25"
    data["terms"][1]["subjects"][0]["score"] = "84"
    assert _rules(validate_parsed(data, SOURCE)) == ["R5", "R5"]


def test_r5_diem_chu_dat_khong_tinh_la_loi():
    data = _data()
    data["terms"][0]["subjects"].append({"name": "Giáo dục thể chất", "score": "Đ"})
    assert validate_parsed(data, SOURCE) == []


def test_r6_bo_sot_qua_nhieu_diem():
    data = _data(subjects2=[])
    data["terms"][0]["subjects"] = data["terms"][0]["subjects"][:1]
    issues = validate_parsed(data, SOURCE)
    assert _rules(issues) == ["R6"] and "8" in issues[0].message

    data["terms"][0]["subjects"] = _data()["terms"][0]["subjects"][:2]  # 2/8 = 25%: trên ngưỡng, không bắt
    assert validate_parsed(data, SOURCE) == []


def test_r7_ten_ky_khong_hieu():
    issues = validate_parsed(_data(term1="HKý I"), SOURCE)
    assert _rules(issues) == ["R7"] and issues[0].term_index == 0


def test_bo_o_loi_giu_o_dung():
    data = _data(term1="HKý I")
    data["terms"][1]["subjects"][1]["name"] = "Ngũ văn"
    issues = validate_parsed(data, SOURCE)
    kept = drop_bad_cells(data, issues)
    assert len(kept["terms"]) == 1
    assert [s["name"] for s in kept["terms"][0]["subjects"]] == ["Toán", "Vật lý", "Công nghệ"]
    assert len(data["terms"]) == 2  # không sửa dữ liệu gốc


def test_loi_nhan_lai_cho_claude_liet_ke_tung_loi():
    data = _data()
    data["terms"][0]["subjects"][1]["name"] = "Ngũ văn"
    msg = retry_message(validate_parsed(data, SOURCE), data, SOURCE)
    assert "Ngũ văn" in msg and "Ngữ văn" in msg and SOURCE in msg and '"terms"' in msg


# --- Vòng thử lại ở bước B: Claude giả trả lần lượt từng kết quả ---

import asyncio  # noqa: E402

from app.core.config import Settings  # noqa: E402
from app.services.ollama import OllamaError  # noqa: E402
from app.services.transcript_reader import TranscriptReader  # noqa: E402


class SequenceParser:
    def __init__(self, *answers) -> None:
        self.answers, self.calls = list(answers), []

    async def chat_json(self, messages, schema, temperature=0.0):
        self.calls.append(messages)
        answer = self.answers.pop(0)
        if isinstance(answer, Exception):
            raise answer
        return answer


def _run(parser: SequenceParser):
    reader = TranscriptReader(None, Settings(_env_file=None), parser, "claude-x")  # type: ignore[arg-type,call-arg]
    return asyncio.run(reader._parse_with_parser(SOURCE, "hoc_ba.png", "glm-ocr"))


def _wrong_name() -> dict:
    data = _data()
    data["terms"][0]["subjects"][1]["name"] = "Ngũ văn"
    return data


def _scores(result) -> dict[int, dict[str, float]]:
    return {t.semester: t.scores for t in result.terms}


def test_dat_ngay_lan_dau_chi_goi_mot_lan():
    parser = SequenceParser(_data())
    result = _run(parser)
    assert len(parser.calls) == 1 and result.attempts == 1 and result.validation_errors == []
    assert _scores(result)[1]["Ngữ văn"] == 7.5


def test_lan_dau_sai_lan_hai_dung_dung_ket_qua_lan_hai():
    parser = SequenceParser(_wrong_name(), _data())
    result = _run(parser)
    assert len(parser.calls) == 2 and result.attempts == 2 and result.validation_errors == []
    retry_text = parser.calls[1][0]["content"]
    assert "Ngũ văn" in retry_text and "Ngữ văn" in retry_text and SOURCE in retry_text
    assert not any("chưa chắc chắn" in w for w in result.warnings)


def test_hai_lan_deu_sai_giu_o_dung_va_bao_o_loi():
    parser = SequenceParser(_wrong_name(), _wrong_name())
    result = _run(parser)
    assert result.attempts == 2 and len(result.validation_errors) == 1
    hk1 = _scores(result)[1]
    assert "Ngữ văn" not in hk1 and hk1["Toán"] == 8.0 and _scores(result)[2]["Ngữ văn"] == 7.9
    assert any("1 ô điểm" in w for w in result.warnings)


def test_thu_lai_bi_loi_mang_giu_lan_mot_bo_o_loi():
    parser = SequenceParser(_wrong_name(), OllamaError("quá tải"))
    result = _run(parser)
    assert result.attempts == 1 and "Ngữ văn" not in _scores(result)[1]
    assert any("1 ô điểm" in w for w in result.warnings)
