"""Từ điển dữ liệu của SLM: mỗi loại ý có khóa entities nào, giá trị nào hợp lệ, ví dụ thật và số lần gặp trong dữ liệu.

Danh sách khóa và giá trị lấy thẳng từ app/schemas/slm.py (nguồn sự thật của hệ thống), ví dụ và số lần lấy từ du_lieu_slm.jsonl,
chỉ phần mô tả bằng lời viết tay ở dưới. Thêm khóa hay giá trị mới vào schema mà chưa mô tả ở đây thì build() báo lỗi,
nên tài liệu không thể lệch với mã. Chạy trong thư mục advisor:
  python -m slm.prepare.build_entities_reference        # ghi slm/data/ENTITIES_REFERENCE.md
"""

import json
import sys
from collections import Counter
from typing import Any

from app.schemas import slm as schema
from slm.prepare.build_slm_jsonl import DEFAULT_OUT, SLM_DIR

OUT = SLM_DIR / "data" / "ENTITIES_REFERENCE.md"

TYPE_WHEN = {
    "collect_profile": "Học sinh khai hoặc sửa thông tin về mình, hoặc trả lời câu hệ thống đang chờ",
    "profile_review": "Muốn được tư vấn dựa trên hồ sơ của chính mình (đánh giá hồ sơ, nên chọn trường nào)",
    "scenario": 'Thử "nếu như…" (what_if) hoặc hỏi cần gì để vào một trường (goal_seeking)',
    "knowledge": "Hỏi kiến thức chung hoặc số liệu trường, không cần hồ sơ của học sinh",
    "center_referral": "Tìm, so sánh hoặc đặt lịch với trung tâm tư vấn",
    "out_of_scope": "Ngoài phạm vi: lời chào, cảm ơn, không rõ ý, nội dung nhạy cảm, hỏi về trợ lý",
}
# Mô tả khóa entities (khóa nào cũng phải có mặt ở đây)
KEY_MEANING = {
    "tasks": "Việc học sinh muốn làm (bảng nhiệm vụ bên dưới). Vắng là việc mặc định của loại",
    "follow_up": "true nếu câu thiếu thông tin và lấy phần thiếu từ context; false hoặc vắng nếu câu tự đứng được",
    "schools": "Tên trường, nguyên văn như học sinh gõ (danh sách)",
    "fields": "Mục thông tin trường học sinh hỏi (danh sách mã). Học sinh không nêu mục thì bỏ khóa",
    "filters": "Điều kiện tìm trường (object, bảng điều kiện lọc bên dưới)",
    "sort": "Cách xếp danh sách học sinh muốn. Không nói thì bỏ khóa",
    "group": "Nhóm kết quả của học sinh, thay cho tên trường (đi với compare_facts)",
    "mode": 'Kiểu kịch bản: thử "nếu như" hay hỏi cần gì để vào trường',
    "changes": "Các thay đổi của what_if: danh sách {factor, value, unit}, hoặc {factor, action: skip} để bỏ một yếu tố",
    "school": "Trường mục tiêu của goal_seeking, nguyên văn",
    "factor": "Yếu tố cần thay đổi của goal_seeking (danh sách, ví dụ sat, gpa, activities)",
    "target_group": "Nhóm đích của goal_seeking",
    "service": "Dịch vụ cần tìm (bảng dịch vụ bên dưới)",
    "city": 'Thành phố, nguyên văn như học sinh viết ("Sài Gòn", "ha noi")',
    "centers": "Tên các trung tâm cần so sánh (từ hai tên), nguyên văn",
    "subtype": "Loại ngoài phạm vi (bắt buộc, bảng bên dưới)",
}
TASK_MEANING = {
    "collect_profile": {
        "edit": "Sửa một mục đã nhập (kèm mục hồ sơ đó)",
        "status": "Hỏi đã nhập gì, còn thiếu gì",
        "restart": "Nhập lại từ đầu",
    },
    "profile_review": {
        "assessment": "Đánh giá hồ sơ",
        "school_list": "Gợi ý danh sách trường theo hồ sơ",
        "roadmap": "Lộ trình chuẩn bị",
        "compare": "So sánh trường theo hồ sơ (cần schools từ hai tên)",
        "explain_school": "Trường này có hợp với hồ sơ em không, có nên nộp không",
        "explain_fit": "Vì sao điểm phù hợp hoặc nhóm Reach, Match, Safety của một trường ra như vậy",
    },
    "knowledge": {
        "school_info": "Số liệu của một trường (schools, fields)",
        "compare_facts": "So sánh sự thật của từ hai trường (schools từ hai tên, hoặc group), kèm fields",
        "search": "Tìm trường theo điều kiện (filters và/hoặc sort), không nêu trường nào",
    },
    "center_referral": {
        "booking": "Đặt lịch với trung tâm",
        "compare": "So sánh trung tâm (centers)",
    },
}
DEFAULT_TASK_MEANING = {
    "collect_profile": "Khai giá trị hồ sơ hoặc trả lời câu đang chờ",
    "profile_review": "Chưa rõ muốn gì (tasks rỗng): hệ thống hỏi lại bằng nút",
    "knowledge": "Kiến thức chung (entities rỗng)",
    "center_referral": "Tìm trung tâm",
}
FIELD_MEANING = {
    "cost": "Tổng học phí, chi phí sinh hoạt",
    "aid": "Học bổng cho du học sinh",
    "english_requirement": "Yêu cầu tiếng Anh",
    "test_policy": "Chính sách SAT/ACT",
    "gpa": "GPA trúng tuyển, thang điểm",
    "acceptance_rate": "Tỷ lệ nhận",
    "deadline": "Hạn nộp hồ sơ",
    "graduation_rate": "Tỷ lệ tốt nghiệp",
    "majors": "Ngành đào tạo",
    "ranking": "Xếp hạng",
    "location": "Bang, thành phố, địa chỉ",
    "website": "Trang web của trường",
    "housing": "Ký túc xá: loại nhà ở, tiền phòng, tiền cọc",
    "student_faculty_ratio": "Tỷ lệ sinh viên trên giảng viên, sĩ số lớp",
    "student_activities": "Câu lạc bộ, đội nhóm, đời sống sinh viên",
    "campus_setting": "Khuôn viên ở thành phố, ngoại ô, thị trấn hay nông thôn",
    "tuition_in_state": "Học phí dành cho người trong bang (chỉ trường công)",
    "tuition_out_of_state": "Học phí ngoài bang, mức du học sinh thường phải trả",
    "living_cost": "Chi phí ăn ở, sinh hoạt mỗi năm",
    "insurance": "Bảo hiểm y tế bắt buộc cho du học sinh",
    "application_fee": "Lệ phí nộp hồ sơ (một lần)",
    "act_range": "Khoảng điểm ACT 25%-75% của sinh viên trúng tuyển",
    "sat_range": "Khoảng điểm SAT 25%-75% của sinh viên trúng tuyển",
    "sat_submit_rate": "Tỷ lệ sinh viên trúng tuyển có nộp SAT",
    "international_acceptance_rate": "Tỷ lệ nhận riêng của sinh viên quốc tế",
    "scholarships": "Liệt kê các học bổng có tên của trường",
    "other": "Điều hệ thống không lưu (việc làm...)",
}
FILTER_MEANING = {
    "state": "Bang (một tên hoặc danh sách)",
    "city": "Thành phố",
    "major": "Ngành đào tạo",
    "school_type": "Loại trường",
    "aid": "Học bổng cho du học sinh: có, theo nhu cầu, theo thành tích",
    "test_policy": "Chính sách thi: test-optional hoặc bắt buộc",
    "max_cost": "Tổng chi phí cả năm không vượt mức này",
    "max_ielts": "Mức IELTS tối thiểu trường yêu cầu không vượt mức này",
    "max_toefl": "Mức TOEFL tối thiểu trường yêu cầu không vượt mức này",
    "max_duolingo": "Mức Duolingo tối thiểu trường yêu cầu không vượt mức này",
    "min_acceptance_rate": "Tỷ lệ nhận từ mức này trở lên (dễ vào)",
    "max_acceptance_rate": "Tỷ lệ nhận không quá mức này (chọn lọc)",
    "max_rank": "Có hạng trong bảng xếp hạng chung từ hạng này trở lên (top N)",
    "min_graduation_rate": "Tỷ lệ tốt nghiệp 6 năm từ mức này trở lên",
    "ivy": "Trường Ivy League",
}
SORT_MEANING = {
    "ranking": "Xếp hạng cao nhất (mặc định khi không nói)",
    "lowest_cost": "Rẻ nhất (hệ thống hiểu là học phí hay tổng chi phí tùy chữ học sinh dùng)",
    "highest_cost": "Đắt nhất (như trên)",
    "easiest_admission": "Dễ vào nhất (tỷ lệ nhận cao nhất)",
    "most_selective": "Khó vào nhất (tỷ lệ nhận thấp nhất)",
    "most_aid": "Học bổng trung bình lớn nhất",
}
SERVICE_MEANING = {
    "SCHOOL_SELECTION": "Chọn trường",
    "ESSAY_EDITING": "Sửa bài luận",
    "APPLICATION_SUBMISSION": "Nộp hồ sơ",
    "VISA_INTERVIEW_PREP": "Luyện phỏng vấn visa",
    "TEST_PREP": "Luyện thi",
    "SCHOLARSHIP_SEARCH": "Săn học bổng",
}
SUBTYPE_MEANING = {
    "exception": "Không liên quan du học",
    "sensitive": "Gian lận, làm giả, nội dung xấu, cố đổi hướng dẫn của hệ thống",
    "greeting": "Lời chào mở đầu (hello, xin chào)",
    "farewell": "Cảm ơn, tạm biệt, kết thúc",
    "ambiguous": "Không rõ muốn gì và không có câu đang chờ",
    "about_system": 'Hỏi về chính trợ lý ("bạn là ai", "bạn làm được gì")',
    "human_handoff": "Muốn gặp người thật",
}
MODE_MEANING = {
    "what_if": 'Thử "nếu như…" (kèm changes; chưa nói đổi gì thì không có changes)',
    "goal_seeking": "Cần gì để vào một trường (kèm school)",
}
GROUP_MEANING = {"reach": "Khó vào", "target": "Vừa sức", "safety": "An toàn"}
# Khóa hồ sơ: (ý nghĩa, mã câu hệ thống hỏi trong chat_flow.QUESTIONS)
PROFILE_MEANING = {
    "current_level": ("Đang học hay làm gì hiện tại", "B1.1"),
    "current_grade": ("Lớp đang học", "B1.1"),
    "major": ("Ngành định học", "B1.3"),
    "intake": ("Kỳ nhập học dự kiến", "B1.4"),
    "education_system": ("Chương trình học (THPT Việt Nam, IB, A-level, AP)", "D1"),
    "gpa": ("Điểm trung bình", "D2"),
    "sat": ("Điểm SAT đã thi", "D3"),
    "act": ("Điểm ACT đã thi", "D3"),
    "english": ("Chứng chỉ tiếng Anh đã có, điểm và ngày thi", "E1"),
    "english_plan": ("Kế hoạch thi tiếng Anh", "E2"),
    "standardized_plan": ("Kế hoạch thi SAT, ACT", "E3"),
    "budget": ("Ngân sách mỗi năm (học phí và sinh hoạt)", "F1"),
    "scholarship": ("Mức cần học bổng: không cần, nên có, bắt buộc", "F2"),
    "funding_source": ("Ai chi trả chi phí du học", "F3"),
    "activities": ("Hoạt động ngoại khóa", "D4"),
    "awards": ("Giải thưởng", "D5"),
    "relatives_state": ("Người thân ở Mỹ (bang)", "N1"),
    "preferred_states": ("Bang đã tìm hiểu hoặc muốn học", "N2"),
    "school_type": ("Thích trường công hay trường tư", "N3"),
    "visa_refused": ("Từng bị từ chối visa chưa", "N4"),
}


def missing_descriptions() -> list[str]:
    """Khóa hoặc giá trị có trong schema mà chưa có mô tả ở đây. Rỗng nghĩa là tài liệu đủ."""
    problems = []
    for intent, keys in schema.ENTITY_KEYS.items():
        problems += [f"khóa {intent}.{k}" for k in sorted(keys) if k not in KEY_MEANING]
        if intent not in TYPE_WHEN:
            problems.append(f"loại {intent}")
    problems += [f"khóa hồ sơ {k}" for k in schema.PROFILE_KEYS if k not in PROFILE_MEANING]
    for intent, codes in schema.TASKS_BY_INTENT.items():
        problems += [f"nhiệm vụ {intent}.{c}" for c in codes if c not in TASK_MEANING.get(intent, {})]
    for label, values, meaning in (
        ("mục thông tin", schema.INFO_FIELDS, FIELD_MEANING),
        ("điều kiện lọc", schema.FILTER_KEYS, FILTER_MEANING),
        ("cách xếp", schema.SORTS, SORT_MEANING),
        ("mã dịch vụ", schema.SERVICE_CODES, SERVICE_MEANING),
        ("subtype", schema.SUBTYPES, SUBTYPE_MEANING),
        ("mode", schema.SCENARIO_MODES, MODE_MEANING),
        ("nhóm", schema.TARGET_GROUPS, GROUP_MEANING),
    ):
        problems += [f"{label} {v}" for v in values if v not in meaning]
    return problems


def cell(text: str) -> str:
    return text.replace("|", "/").replace("\n", " ")


def values_of(intent: str, key: str) -> str:
    """Giá trị hợp lệ của một khóa, lấy từ schema."""
    if key == "tasks":
        codes = ", ".join(f"`{c}`" for c in schema.TASKS_BY_INTENT.get(intent, ()))
        return f"danh sách, mã trong: {codes}"
    if key == "follow_up":
        return "`true` hoặc `false`"
    if key == "sort":
        return "một trong: " + ", ".join(f"`{v}`" for v in schema.SORTS)
    if key in ("group", "target_group"):
        return "một trong: " + ", ".join(f"`{v}`" for v in schema.TARGET_GROUPS)
    if key == "mode":
        return "một trong: " + ", ".join(f"`{v}`" for v in schema.SCENARIO_MODES)
    if key == "service":
        return "một trong: " + ", ".join(f"`{v}`" for v in schema.SERVICE_CODES)
    if key == "subtype":
        return "một trong: " + ", ".join(f"`{v}`" for v in schema.SUBTYPES)
    if key == "fields":
        return "danh sách mã (bảng mục thông tin)"
    if key == "filters":
        return "object (bảng điều kiện lọc)"
    if key == "changes":
        return "danh sách object"
    if key in ("schools", "centers", "factor"):
        return "danh sách chữ"
    return "chữ nguyên văn"


def stats(records: list[dict[str, Any]]) -> dict[str, Any]:
    """Số ý theo loại, số lần mỗi khóa xuất hiện và một ví dụ ngắn nhất cho mỗi (loại, khóa), cùng các giá trị gặp ở nơi không có danh sách cố định."""
    by_type: Counter = Counter()
    key_count: Counter = Counter()
    example: dict[tuple[str, str], tuple[int, str, Any]] = {}
    seen: dict[str, Counter] = {
        "unit": Counter(),
        "factor": Counter(),
        "filters": Counter(),
        "action": Counter(),
    }
    for r in records:
        question = (r.get("input") or {}).get("question", "")
        for y in (r.get("output") or {}).get("intents", []):
            kind, entities = y["type"], y.get("entities") or {}
            by_type[kind] += 1
            for key, value in entities.items():
                key_count[(kind, key)] += 1
                length = len(question)
                if (kind, key) not in example or length < example[(kind, key)][0]:
                    example[(kind, key)] = (length, question, value)
            for change in entities.get("changes") or []:
                seen["unit"][change.get("unit", "(không có)")] += 1
                seen["factor"][change.get("factor", "?")] += 1
                seen["action"][change.get("action", "(không có)")] += 1
            for fkey in entities.get("filters") or {}:
                seen["filters"][fkey] += 1
    return {"by_type": by_type, "key_count": key_count, "example": example, "seen": seen}


def example_text(data: dict[str, Any], intent: str, key: str) -> str:
    found = data["example"].get((intent, key))
    if not found:
        return "-"
    _, question, value = found
    shown = json.dumps(value, ensure_ascii=False)
    if len(shown) > 50:
        shown = shown[:49] + "…"
    question = question if len(question) <= 45 else question[:44] + "…"
    return cell(f"«{question}» → `{shown}`")


def table(header: list[str], rows: list[list[str]]) -> list[str]:
    return [
        "| " + " | ".join(header) + " |",
        "|" + "---|" * len(header),
        *["| " + " | ".join(map(cell, r)) + " |" for r in rows],
        "",
    ]


def build(records: list[dict[str, Any]]) -> str:
    problems = missing_descriptions()
    if problems:
        raise ValueError(
            "schema có khóa hoặc giá trị chưa mô tả trong build_entities_reference.py: " + ", ".join(problems)
        )
    data = stats(records)
    total_intents = sum(data["by_type"].values())
    out = [
        "# Từ điển dữ liệu SLM: loại ý và khóa entities",
        "",
        f"Sinh tự động từ `app/schemas/slm.py` (khóa và giá trị hợp lệ) và `du_lieu_slm.jsonl` ({len(records)} tin, {total_intents} ý: ví dụ và số lần gặp). "
        "Sửa schema thì chạy lại `python -m slm.prepare.build_entities_reference`. Luật gán nhãn: `HUONG_DAN_GAN_NHAN.md`.",
        "",
        "## 1. Một ý",
        "",
        "Mỗi tin tách thành danh sách ý. Mỗi ý có đúng ba khóa:",
        "",
        "| Khóa | Nội dung |",
        "|---|---|",
        "| `type` | Một trong sáu loại ở mục 2 |",
        "| `span` | Đoạn chữ của ý, có nguyên văn trong tin |",
        "| `entities` | Thông tin tách ra (mục 3 đến 5), không có thì `{}` |",
        "",
        "## 2. Sáu loại ý",
        "",
        *table(
            ["Loại", "Khi nào dùng", "Số ý trong dữ liệu"],
            [
                [f"`{t}`", TYPE_WHEN[t], f"{data['by_type'][t]} ({data['by_type'][t] / total_intents:.0%})"]
                for t in schema.INTENTS
            ],
        ),
        "## 3. Khóa entities của từng loại",
        "",
        'Cột "Số ý" là số ý có khóa đó. Khóa nào cũng có thể vắng; vắng nghĩa là học sinh không nhắc tới.',
        "",
    ]
    for intent in schema.INTENTS:
        keys = sorted(schema.ENTITY_KEYS[intent], key=lambda k: (k == "follow_up", k))
        rows = [
            [
                f"`{k}`",
                values_of(intent, k),
                KEY_MEANING[k],
                example_text(data, intent, k),
                str(data["key_count"][(intent, k)]),
            ]
            for k in keys
        ]
        out += [
            f"### `{intent}`",
            "",
            *table(["Khóa", "Giá trị hợp lệ", "Ý nghĩa", "Ví dụ thật", "Số ý"], rows),
        ]
        if intent == "collect_profile":
            out += ["Ngoài ra ý `collect_profile` mang các khóa hồ sơ ở mục 5.", ""]
    out += ["## 4. Giá trị hợp lệ", "", "### Nhiệm vụ (`tasks`)", ""]
    task_rows = []
    for intent, codes in schema.TASKS_BY_INTENT.items():
        task_rows.append([f"`{intent}`", "(vắng)", DEFAULT_TASK_MEANING[intent]])
        task_rows += [[f"`{intent}`", f"`{c}`", TASK_MEANING[intent][c]] for c in codes]
    out += table(["Loại", "Nhiệm vụ", "Nghĩa"], task_rows)
    out += [
        "### Mục thông tin trường (`fields`)",
        "",
        *table(["Mã", "Nghĩa"], [[f"`{c}`", FIELD_MEANING[c]] for c in schema.INFO_FIELDS]),
    ]
    filter_rows = []
    for k in schema.FILTER_KEYS:
        shown = (
            ", ".join(f"`{v}`" for v in schema.FILTER_ENUMS[k])
            if k in schema.FILTER_ENUMS
            else "chữ học sinh gõ"
        )
        filter_rows.append([f"`{k}`", shown, FILTER_MEANING[k], str(data["seen"]["filters"][k])])
    out += [
        "### Điều kiện lọc (`filters`)",
        "",
        'Giá trị dạng chữ là chữ học sinh gõ ("texas", "30 nghìn đô", "top 50"), code tự khớp và đổi đơn vị. Các điều kiện cùng có hiệu lực.',
        "",
    ]
    out += table(["Khóa", "Giá trị", "Nghĩa", "Số lần"], filter_rows)
    out += [
        "### Cách xếp (`sort`)",
        "",
        *table(["Mã", "Nghĩa"], [[f"`{c}`", SORT_MEANING[c]] for c in schema.SORTS]),
    ]
    out += ["### Kịch bản (`scenario`)", ""]
    out += table(
        ["Khóa hoặc giá trị", "Nghĩa"],
        [[f"`mode = {c}`", MODE_MEANING[c]] for c in schema.SCENARIO_MODES]
        + [[f"`target_group = {c}`", f"Nhóm {GROUP_MEANING[c]}"] for c in schema.TARGET_GROUPS],
    )
    units = ", ".join(f"`{u}` ({n})" for u, n in data["seen"]["unit"].most_common())
    factors = ", ".join(f"`{u}` ({n})" for u, n in data["seen"]["factor"].most_common())
    out += [
        "Trong `changes` (số lần gặp trong dữ liệu):",
        "",
        f"- `unit`: {units or '-'}",
        f"- `factor`: {factors or '-'}",
        "- `value`: số đã chuẩn hóa (1k5 thành 1500, 40k thành 40000, 800 triệu thành 800000000); riêng `scholarship` là `optional`, `preferred`, `mandatory`; `action: skip` thay `value` để bỏ một yếu tố.",
        "",
    ]
    out += [
        "### Dịch vụ trung tâm (`service`)",
        "",
        *table(["Mã", "Nghĩa"], [[f"`{c}`", SERVICE_MEANING[c]] for c in schema.SERVICE_CODES]),
    ]
    out += [
        "### Loại ngoài phạm vi (`subtype`)",
        "",
        *table(["Giá trị", "Nghĩa"], [[f"`{c}`", SUBTYPE_MEANING[c]] for c in schema.SUBTYPES]),
    ]
    out += ["## 5. Khóa hồ sơ (chỉ ở ý `collect_profile`)", ""]
    out += table(
        ["Khóa", "Nghĩa", "Câu hệ thống hỏi", "Số ý"],
        [
            [
                f"`{k}`",
                PROFILE_MEANING[k][0],
                PROFILE_MEANING[k][1],
                str(data["key_count"][("collect_profile", k)]),
            ]
            for k in schema.PROFILE_KEYS
        ],
    )
    out += [
        "Giá trị hồ sơ luôn là chữ nguyên văn trong tin. Ý khác ngoài `collect_profile` không có khóa hồ sơ.",
        "",
        "## 6. Ràng buộc (kiểm trong `app/schemas/slm.py` và luật gán nhãn)",
        "",
        "- `collect_profile`, `profile_review`, `scenario`: một ý mỗi tin (gộp lại). `knowledge`, `center_referral`: mỗi câu hỏi một ý.",
        "- `tasks` là danh sách không lặp. Chỉ `profile_review` được nhiều nhiệm vụ; các loại khác một nhiệm vụ mỗi ý.",
        "- `status` và `restart` không kèm giá trị hồ sơ. `compare` (profile_review) và `compare_facts` cần `schools` từ hai tên; `group` đi với `compare_facts` và không kèm `schools`.",
        "- `search` cần `filters` hoặc `sort`; `filters` và `sort` chỉ đi với `search`.",
        "- Mỗi change trong `changes` có `factor` và `value` hoặc `action`. `what_if` không có `changes` nghĩa là muốn thử mà chưa nói đổi gì. `centers` từ hai tên và chỉ đi với `tasks = compare`.",
        "- `out_of_scope` bắt buộc có `subtype` và đứng một mình (cả tin không có ý nào khác).",
        "",
    ]
    return "\n".join(out)


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    records = [
        json.loads(line) for line in DEFAULT_OUT.read_text(encoding="utf-8").splitlines() if line.strip()
    ]
    OUT.write_text(build(records), encoding="utf-8")
    print(f"Đã ghi {OUT}")


if __name__ == "__main__":
    main()
