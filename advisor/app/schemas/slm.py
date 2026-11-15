"""Hợp đồng đầu ra của SLM (Qwen3 4B): tách tin thành danh sách ý, kèm đầu vào dựng skill hồ sơ (SkillRequest).

Đây là chỗ nối giữa hai phần: bộ phân loại (Dũng) tạo ra SlmTinResult; handler hồ sơ (Việt) và các nhánh còn lại đọc nó.
Tên loại, khóa và mã ở đây trùng từng chữ với dữ liệu huấn luyện (data/slm/du_lieu_slm.jsonl) và prompt app/skills/slm_intents.md:
một ý là {"type", "span", "entities"}, cả tin là {"intents": [...]}. Hệ thống đọc đúng thứ model trả, không đổi tên.
Sửa file này thì báo người kia trước, và sửa dữ liệu, prompt cho khớp (rồi huấn luyện lại).
"""

from typing import Any, Literal

from pydantic import BaseModel, Field, model_validator

# collect_profile: tin chỉ trả lời câu hồ sơ đang chờ (handler hồ sơ của Việt, không cần skill). Năm loại sau có nhánh riêng
INTENTS = ("collect_profile", "profile_review", "scenario", "knowledge", "center_referral", "out_of_scope")
Intent = Literal[
    "collect_profile", "profile_review", "scenario", "knowledge", "center_referral", "out_of_scope"
]

# Khóa hồ sơ, phải giống info_to_field.PROFILE_KEYS (có test kiểm).
# Không import vì sẽ kéo cả gói skills và chat_flow theo, chat_flow lại cần file này nên dễ nhập vòng
PROFILE_KEYS = (
    "current_level",
    "current_grade",
    "target_level",
    "major",
    "intake",
    "education_system",
    "gpa",
    "sat",
    "act",
    "english",
    "english_plan",
    "standardized_plan",
    "budget",
    "scholarship",
    "funding_source",
    "activities",
    "awards",
    "relatives_state",
    "preferred_states",
    "school_type",
    "visa_refused",
)
# Khóa entities riêng của từng loại. Loại nào cũng được kèm thêm khóa hồ sơ khi tin có giá trị hồ sơ
ENTITY_KEYS = {
    "collect_profile": {"tasks", "follow_up"},
    "profile_review": {"tasks", "schools", "follow_up"},
    "scenario": {"mode", "changes", "school", "factor", "target_group", "follow_up"},
    "knowledge": {"tasks", "schools", "fields", "filters", "sort", "follow_up"},
    "center_referral": {"service", "city", "tasks", "centers", "follow_up"},
    "out_of_scope": {"subtype"},
}
# Nhiệm vụ (tasks) luôn là danh sách. Vắng hoặc rỗng nghĩa là việc mặc định của loại đó
# collect_profile: vắng = khai giá trị, hoặc trả lời câu hệ thống đang hỏi. edit (sửa một mục đã nhập, kèm mục hồ sơ đó),
#   status (hỏi đã nhập gì, còn thiếu gì), restart (nhập lại từ đầu)
PROFILE_TASKS = ("edit", "status", "restart")
# profile_review: tư vấn theo hồ sơ của chính học sinh. Một câu có thể hỏi nhiều việc ("đánh giá hồ sơ và cho em lộ trình") nên đây là
# danh sách không lặp. Rỗng hoặc vắng = chưa rõ muốn gì, hệ thống hỏi lại bằng nút. compare cần schools từ hai trường;
# explain_school có schools (hoặc lấy từ ngữ cảnh)
REVIEW_TASKS = ("assessment", "school_list", "roadmap", "compare", "explain_school")
# knowledge: vắng = câu kiến thức chung. Câu hỏi số liệu trường dùng ba nhiệm vụ sau, hệ thống tra cơ sở dữ liệu theo đúng điều model trả,
# không đoán lại bằng từ khóa:
#   school_info: hỏi số liệu một trường ("học phí Harvard bao nhiêu"): schools (tên như học sinh gõ) và fields (các mục hỏi)
#   compare_facts: hỏi sự thật của từ hai trường trở lên ("học phí BU và NYU chênh nhau bao nhiêu"): schools (từ hai tên, theo thứ tự trong câu)
#     và fields. Khác tư vấn hồ sơ "BU hay NYU hợp với em hơn" (loại profile_review, dùng hồ sơ học sinh)
#   search: tìm trường theo điều kiện, không nêu trường nào ("trường nào ở California có học bổng"): filters (các điều kiện học sinh nói,
#     điều kiện không nói thì bỏ trống) và sort nếu học sinh nói muốn xếp theo gì ("rẻ nhất", "dễ vào nhất").
#     Không dùng hồ sơ học sinh: ai hỏi cùng câu cũng nhận cùng danh sách (khác "gợi ý trường cho em")
KNOWLEDGE_TASKS = ("school_info", "compare_facts", "search")
# center_referral: vắng = tìm trung tâm. compare cần centers (từ hai tên trở lên)
CENTER_TASKS = ("booking", "compare")
TASKS_BY_INTENT = {
    "collect_profile": PROFILE_TASKS,
    "profile_review": REVIEW_TASKS,
    "knowledge": KNOWLEDGE_TASKS,
    "center_referral": CENTER_TASKS,
}
# Mã mục (fields) khớp InfoField bên .NET (ChatSchoolInfo); "other" là điều hệ thống không lưu (việc làm...)
INFO_FIELDS = (
    "cost",
    "aid",
    "english_requirement",
    "test_policy",
    "gpa",
    "acceptance_rate",
    "deadline",
    "graduation_rate",
    "majors",
    "ranking",
    "location",
    "website",
    "housing",  # ký túc xá: loại nhà ở, nhà riêng cho du học sinh, tỷ lệ sinh viên ở ký túc, tiền phòng, tiền cọc
    "student_faculty_ratio",  # tỷ lệ sinh viên trên giảng viên, lớp đông hay nhỏ
    "student_activities",  # câu lạc bộ, đội nhóm, đời sống sinh viên
    "campus_setting",  # khuôn viên ở thành phố, ngoại ô, thị trấn hay nông thôn, quy mô
    "other",
)
# Điều kiện lọc trong filters. Giá trị dạng chữ là CHỮ HỌC SINH GÕ ("texas", "30 nghìn đô", "top 50"), code tự khớp và đổi đơn vị.
# Các điều kiện cùng có hiệu lực (trường phải thỏa tất cả). Mỗi khóa nói rõ trường phải thỏa gì:
#   state (một tên hoặc danh sách), city, major: trường ở bang đó (một trong các bang), ở thành phố đó, có đào tạo ngành đó
#   school_type: public | private
#   aid: yes (có học bổng cho du học sinh) | need_based | merit_based
#   test_policy: optional (test-optional hoặc không nhận SAT, ACT) | required
#   max_cost: tổng chi phí cả năm không vượt mức này
#   max_ielts, max_toefl, max_duolingo: mức tối thiểu trường yêu cầu không vượt mức này (học sinh có điểm đó vẫn nộp được)
#   min_acceptance_rate, max_acceptance_rate: tỷ lệ nhận từ mức này trở lên (dễ vào) hoặc không quá mức này (chọn lọc)
#   max_rank: có hạng trong các bảng xếp hạng chung từ hạng này trở lên ("top 50")
#   min_graduation_rate: tỷ lệ tốt nghiệp trong 6 năm từ mức này trở lên
#   ivy: yes (trường Ivy League)
FILTER_TEXT_KEYS = (
    "city",
    "major",
    "max_cost",
    "max_ielts",
    "max_toefl",
    "max_duolingo",
    "min_acceptance_rate",
    "max_acceptance_rate",
    "max_rank",
    "min_graduation_rate",
)
FILTER_ENUMS = {
    "school_type": ("public", "private"),
    "aid": ("yes", "need_based", "merit_based"),
    "test_policy": ("optional", "required"),
    "ivy": ("yes",),
}
FILTER_KEYS = ("state", *FILTER_TEXT_KEYS, *FILTER_ENUMS)
# Cách xếp danh sách học sinh muốn; không nói thì xếp theo xếp hạng (điểm chất lượng của trường)
SORTS = ("ranking", "lowest_cost", "easiest_admission", "most_selective")
# Chế độ trong kịch bản
SCENARIO_MODES = ("what_if", "goal_seeking")
# Nhóm đích của goal_seeking: khó vào, vừa sức, an toàn
TARGET_GROUPS = ("reach", "target", "safety")
# Giá trị của subtype trong ngoài phạm vi. greeting: lời chào mở đầu ("hello", "xin chào"); farewell: cảm ơn, tạm biệt, kết thúc;
# about_system: hỏi về chính hệ thống ("bạn là ai", "bạn làm được gì"); human_handoff: muốn nói chuyện với người thật
SUBTYPES = ("exception", "sensitive", "greeting", "farewell", "ambiguous", "about_system", "human_handoff")
# Mã dịch vụ của trung tâm tư vấn trong dữ liệu huấn luyện. Chưa nhánh nào dùng giá trị này
SERVICE_CODES = (
    "SCHOOL_SELECTION",
    "ESSAY_EDITING",
    "APPLICATION_SUBMISSION",
    "VISA_INTERVIEW_PREP",
    "TEST_PREP",
    "SCHOLARSHIP_SEARCH",
)
# city không có danh sách cố định: là đoạn chữ nguyên văn trong tin (vd "Sài Gòn", "HN").
# Danh sách thành phố đổi theo trung tâm trong DB, nhánh trung tâm khớp với CenterFinder.cities() lúc chạy
# Trần kỹ thuật cho số ý của một tin, để model sinh lặp vô hạn không làm hệ thống chạy mãi. Không phải giới hạn
# sản phẩm: tin có bao nhiêu câu hỏi thì trả lời bấy nhiêu. Vượt trần thì các ý sau bị cắt
MAX_RAW_Y = 20


def task_of(entities: dict[str, Any]) -> str | None:
    """Nhiệm vụ của loại chỉ có một nhiệm vụ mỗi ý (collect_profile, knowledge, center_referral); None nghĩa là việc mặc định."""
    tasks = entities.get("tasks")
    return tasks[0] if tasks else None


def single_school(entities: dict[str, Any]) -> str | None:
    """Tên trường khi ý nói về đúng một trường và không so sánh; None khi không nêu trường, từ hai trường, hoặc đang so sánh."""
    schools = entities.get("schools") or []
    comparing = bool({"compare", "compare_facts"} & set(entities.get("tasks") or []))
    return schools[0].strip() if len(schools) == 1 and not comparing else None


def _is_text_list(value: Any, minimum: int) -> bool:
    return (
        isinstance(value, list)
        and len(value) >= minimum
        and all(isinstance(v, str) and v.strip() for v in value)
    )


# Entities phải đúng khóa của loại (cộng khóa hồ sơ) và đúng giá trị hệ thống biết. Sai thì ném ValueError
def check_entities(intent: str, entities: dict[str, Any]) -> None:
    allowed = ENTITY_KEYS[intent] | set(PROFILE_KEYS)
    unknown = set(entities) - allowed
    if unknown:
        raise ValueError(f"khóa entities lạ với loại {intent}: {sorted(unknown)}")
    for key in (set(entities) & set(PROFILE_KEYS)) | ({"city"} & set(entities)):
        if not isinstance(entities[key], str) or not entities[key]:
            raise ValueError(f"giá trị '{key}' phải là đoạn chữ trong tin nhắn")
    # follow_up: câu thiếu thông tin và lấy phần thiếu từ ngữ cảnh (context trong đầu vào). false hoặc vắng = câu tự đứng được
    if "follow_up" in entities and not isinstance(entities["follow_up"], bool):
        raise ValueError("follow_up phải là true hoặc false")
    tasks = _check_tasks(intent, entities)
    schools = entities.get("schools")
    if schools is not None and not _is_text_list(schools, 1):
        raise ValueError("schools phải là danh sách tên trường")
    filters = entities.get("filters")
    if filters is not None:
        _check_filters(filters)
    if entities.get("sort") is not None and entities["sort"] not in SORTS:
        raise ValueError(f"sort phải là một trong {SORTS}")
    # Câu tiếp nối chỉ đổi cách xếp ("rẻ nhất đi") không có điều kiện mới: điều kiện lấy từ ngữ cảnh
    if (
        "search" in tasks
        and not filters
        and not (entities.get("follow_up") is True and entities.get("sort") is not None)
    ):
        raise ValueError("search cần filters có ít nhất một điều kiện")
    if (filters is not None or entities.get("sort") is not None) and "search" not in tasks:
        raise ValueError("filters và sort chỉ đi với tasks = search")
    if "compare_facts" in tasks and not _is_text_list(schools, 2):
        raise ValueError("compare_facts cần schools từ hai tên trường trở lên")
    fields = entities.get("fields")
    if fields is not None and not (
        isinstance(fields, list) and fields and all(f in INFO_FIELDS for f in fields)
    ):
        raise ValueError(f"fields phải là danh sách mã trong {INFO_FIELDS}")
    if entities.get("service") is not None and entities["service"] not in SERVICE_CODES:
        raise ValueError(f"mã dịch vụ lạ: {entities['service']}")
    if intent == "out_of_scope" and entities.get("subtype") not in SUBTYPES:
        raise ValueError(f"subtype phải là một trong {SUBTYPES}")
    if intent == "scenario":
        _check_scenario(entities)
    _check_task_entities(intent, entities, tasks, schools)


# Nhiệm vụ của loại đang xét: danh sách không lặp các mã của loại. Loại chỉ có một nhiệm vụ mỗi ý thì tối đa một mã.
# Trả về danh sách (rỗng khi vắng) để các luật sau dùng
def _check_tasks(intent: str, entities: dict[str, Any]) -> list[str]:
    tasks = entities.get("tasks")
    if tasks is None:
        return []
    codes = TASKS_BY_INTENT.get(intent, ())
    if not (
        isinstance(tasks, list) and len(set(map(str, tasks))) == len(tasks) and all(t in codes for t in tasks)
    ):
        raise ValueError(f"tasks của loại {intent} phải là danh sách không lặp các mã trong {codes}")
    if intent != "profile_review" and len(tasks) > 1:
        raise ValueError(f"loại {intent} chỉ có một nhiệm vụ mỗi ý, đang là {tasks}")
    return tasks


# Kịch bản: mode bắt buộc, what_if cần changes, mỗi change cần factor và value hoặc action
def _check_scenario(entities: dict[str, Any]) -> None:
    if entities.get("mode") not in SCENARIO_MODES:
        raise ValueError(f"mode phải là một trong {SCENARIO_MODES}")
    changes = entities.get("changes", [])
    if entities["mode"] == "what_if" and not changes:
        raise ValueError("what_if cần changes")
    for change in changes:
        if "factor" not in change or not ({"value", "action"} & set(change)):
            raise ValueError("mỗi change cần factor và value hoặc action")
    if entities.get("target_group") is not None and entities["target_group"] not in TARGET_GROUPS:
        raise ValueError(f"target_group phải là một trong {TARGET_GROUPS}")
    factor = entities.get("factor")
    if factor is not None and not _is_text_list(factor, 1):
        raise ValueError("factor phải là danh sách tên yếu tố")
    if entities.get("school") is not None and not (
        isinstance(entities["school"], str) and entities["school"].strip()
    ):
        raise ValueError("school phải là tên trường")


# Quy tắc nhiệm vụ của nhập hồ sơ, tư vấn hồ sơ và trung tâm. Sai thì ném ValueError
def _check_task_entities(intent: str, entities: dict[str, Any], tasks: list[str], schools: Any) -> None:
    if intent == "collect_profile":
        # edit không nói mục nào vẫn hợp lệ (vd bấm "Cần sửa"): skill hồ sơ hỏi lại sửa mục nào
        has_profile = bool(set(entities) & set(PROFILE_KEYS))
        if ({"status", "restart"} & set(tasks)) and has_profile:
            raise ValueError(f"{tasks[0]} không kèm giá trị hồ sơ")
    elif intent == "profile_review":
        if "compare" in tasks and not _is_text_list(schools, 2):
            raise ValueError("compare cần schools từ hai tên trường trở lên")
    elif intent == "center_referral":
        centers = entities.get("centers")
        if centers is not None and not _is_text_list(centers, 2):
            raise ValueError("centers phải là danh sách từ hai tên trung tâm trở lên")
        if "compare" in tasks and centers is None:
            raise ValueError("compare cần centers")
        if centers is not None and "compare" not in tasks:
            raise ValueError("centers chỉ đi với tasks = compare")


# filters: object có ít nhất một điều kiện, đúng khóa và đúng giá trị. Sai thì ném ValueError
def _check_filters(filters: Any) -> None:
    if not isinstance(filters, dict) or not filters:
        raise ValueError("filters phải là object có ít nhất một điều kiện")
    unknown = set(filters) - set(FILTER_KEYS)
    if unknown:
        raise ValueError(f"điều kiện lọc lạ: {sorted(unknown)}")
    state = filters.get("state")
    if state is not None and not (_is_text_list(state, 1) or (isinstance(state, str) and state.strip())):
        raise ValueError("state phải là tên bang hoặc danh sách tên bang")
    for key in FILTER_TEXT_KEYS:
        if key in filters and not (isinstance(filters[key], str) and filters[key].strip()):
            raise ValueError(f"{key} phải là đoạn chữ học sinh gõ")
    for key, allowed in FILTER_ENUMS.items():
        if key in filters and filters[key] not in allowed:
            raise ValueError(f"{key} phải là một trong {allowed}")


# Một ý trong tin nhắn. Tin có nhiều ý thì SLM trả một danh sách SlmY, mỗi ý có đoạn chữ và thông tin riêng.
# span: knowledge và center_referral được viết lại thành câu hỏi đầy đủ ("I-20 và SAT là gì" tách thành hai ý);
# collect_profile phải nguyên văn (kiểm ở bộ phân loại vì cần tin gốc)
class SlmY(BaseModel):
    type: Intent
    span: str = Field(min_length=1)
    entities: dict[str, Any] = Field(default_factory=dict)

    @model_validator(mode="after")
    def _check(self) -> "SlmY":
        check_entities(self.type, self.entities)
        return self

    def profile_values(self) -> dict[str, str]:
        return {key: value for key, value in self.entities.items() if key in PROFILE_KEYS}


# Ba loại này chỉ chạy một lần mỗi tin: SLM trả nhiều ý cùng loại thì gộp thành một
ONCE = ("collect_profile", "profile_review", "scenario")


# Gộp entities của hai ý cùng loại: danh sách thì nối thêm (tasks, schools, changes), còn lại giá trị sau thay giá trị trước
def _merge_entities(a: dict[str, Any], b: dict[str, Any]) -> dict[str, Any]:
    out = dict(a)
    for key, value in b.items():
        out[key] = (
            out[key] + [x for x in value if x not in out[key]]
            if isinstance(value, list) and isinstance(out.get(key), list)
            else value
        )
    return out


# Đưa danh sách ý SLM trả về về dạng hệ thống chạy được
def chuan_hoa_y(intents: list[SlmY]) -> list[SlmY]:
    # 1. Ngoài phạm vi: có ý khác thì bỏ hết ý ngoài phạm vi (chào kèm câu hỏi), không thì chỉ giữ ý đầu
    others = [y for y in intents if y.type != "out_of_scope"]
    intents = others or intents[:1]
    # 2. Gộp các ý cùng loại thuộc ONCE
    merged: list[SlmY] = []
    for y in intents:
        # Tìm xem trong merged có ý nào cùng loại y chưa
        before = next((m for m in merged if m.type == y.type and y.type in ONCE), None)
        if before is None:
            merged.append(y)
        else:
            merged[merged.index(before)] = SlmY(
                type=y.type,
                span=f"{before.span}, {y.span}",
                entities=_merge_entities(before.entities, y.entities),
            )
    # 3. Giữ thứ tự học sinh hỏi; chỉ collect_profile lên đầu vì ghi hồ sơ phải xong trước các ý khác
    merged = [y for y in merged if y.type == "collect_profile"] + [
        y for y in merged if y.type != "collect_profile"
    ]
    # 4. Không giới hạn số ý trả lời; chỉ cắt khi vượt trần kỹ thuật (collect_profile đứng đầu nên không bao giờ bị cắt)
    return merged[:MAX_RAW_Y]


# Cả tin nhắn: các ý cần trả lời (đã qua chuan_hoa_y). Luật dưới đây là lưới an toàn, sai thì ném ValueError
class SlmTin(BaseModel):
    intents: list[SlmY] = Field(min_length=1)

    @model_validator(mode="after")
    def _check(self) -> "SlmTin":
        types = [y.type for y in self.intents]
        if "out_of_scope" in types and len(types) > 1:
            raise ValueError("out_of_scope phải đứng một mình")
        for t in ONCE:
            if types.count(t) > 1:
                raise ValueError(f"loại '{t}' chỉ được một ý, hãy gộp lại")
        if "collect_profile" in types and types[0] != "collect_profile":
            raise ValueError("collect_profile phải đứng đầu")
        if len(types) > MAX_RAW_Y:
            raise ValueError(f"quá {MAX_RAW_Y} ý trong một tin")
        return self


# SlmTin cộng độ tin cậy (confidence). Độ tin cậy do code tính (từ xác suất của model), không để SLM tự khai.
# Đây là kết quả bộ phân loại trả cho ChatFlow và các nhánh
class SlmTinResult(SlmTin):
    confidence: float = Field(default=1.0, ge=0, le=1)
    # True khi SLM hai lần đều trả sai (hoặc Ollama tắt) và bộ phân loại phải dùng kết quả dự phòng. Báo cho admin biết SLM hỏng, không phải câu mơ hồ thật
    fallback: bool = False

    # Dựng từ JSON SLM trả ({"intents": [...]}): kiểm từng ý, dọn (gộp, sắp xếp, cắt ý quá trần kỹ thuật) rồi kiểm cả tin.
    # JSON sai thì ném ValueError (ValidationError của pydantic cũng là ValueError)
    @classmethod
    def from_raw(cls, raw: dict[str, Any], confidence: float = 1.0) -> "SlmTinResult":
        items = raw.get("intents")
        if not isinstance(items, list):
            raise ValueError("thiếu danh sách intents")
        intents = chuan_hoa_y([SlmY.model_validate(item) for item in items])
        return cls(intents=intents, confidence=confidence)

    # Các loại của những ý trả lời ngay, theo thứ tự (để ghi log)
    def types(self) -> list[str]:
        return [y.type for y in self.intents]

    # Giá trị hồ sơ trong cả tin, gom từ mọi ý (ý loại nào cũng có thể kèm giá trị hồ sơ). Giá trị sau thay giá trị trước.
    # Handler hồ sơ (Việt) tự chuẩn hóa và kiểm rồi mới lưu
    def profile_values(self) -> dict[str, str]:
        values: dict[str, str] = {}
        for y in self.intents:
            values.update(y.profile_values())
        return values

    # Tin có cần chuyển qua handler hồ sơ không: có ý collect_profile (trả lời câu đang chờ), hoặc có kèm giá trị hồ sơ
    def touches_profile(self) -> bool:
        return "collect_profile" in self.types() or bool(self.profile_values())


# ---- Đầu vào dựng skill hồ sơ ----
# Dòng code tính sẵn cho skill hồ sơ (profile_calc.calc_profile): mức GPA / tiếng Anh so với mốc dữ liệu trường,
# điểm TB từng năm, mốc thời gian... Tách khỏi ho_so để phân biệt dữ kiện học sinh khai với phần code so sánh;
# không nằm trong PROFILE_KEYS vì SLM không được tách ra các khóa này từ câu chat
CALC_KEYS = (
    "grade_by_year",
    "gpa_level",
    "english_level",
    "sat_level",
    "budget_level",
    "ec_level",
    "scholarship_chance",
    "timeline",
)


# Câu hỏi và hồ sơ đã đủ (giá trị đã được nhánh hồ sơ chuẩn hóa và kiểm): đầu vào để code dựng skill từ khuôn (skill_template),
# rồi Claude viết câu trả lời theo skill đó
class SkillRequest(BaseModel):
    loai: Literal["ho_so"]
    cau_hoi: str = Field(min_length=1)
    ho_so: dict[str, str]
    # Các dòng code tính sẵn (khóa trong CALC_KEYS)
    tinh_san: dict[str, str] = Field(default_factory=dict)

    @model_validator(mode="after")
    def _check(self) -> "SkillRequest":
        if not self.ho_so:
            raise ValueError("hồ sơ rỗng, chưa đủ để dựng skill")
        unknown = set(self.ho_so) - set(PROFILE_KEYS)
        if unknown:
            raise ValueError(f"khóa hồ sơ lạ: {sorted(unknown)}")
        unknown_calc = set(self.tinh_san) - set(CALC_KEYS)
        if unknown_calc:
            raise ValueError(f"khóa tính sẵn lạ: {sorted(unknown_calc)}")
        return self


# Chuỗi phải có ít nhất 1 ký tự: chặn model điền "" cho khóa không có thông tin
_TEXT = {"type": "string", "minLength": 1}
# Khuôn của từng khóa entities riêng. Khóa hồ sơ dùng chung _TEXT
_ENTITY_SCHEMA = {
    "school": _TEXT,
    "mode": {"enum": list(SCENARIO_MODES)},
    "changes": {
        "type": "array",
        "items": {
            "type": "object",
            "additionalProperties": False,
            "required": ["factor"],
            "properties": {"factor": _TEXT, "value": _TEXT, "unit": _TEXT, "action": _TEXT},
        },
    },
    "target_group": {"enum": list(TARGET_GROUPS)},
    "factor": {"type": "array", "items": _TEXT},
    "service": {"enum": list(SERVICE_CODES)},
    "city": _TEXT,
    "subtype": {"enum": list(SUBTYPES)},
    "schools": {"type": "array", "minItems": 1, "items": _TEXT},
    "filters": {
        "type": "object",
        "additionalProperties": False,
        "minProperties": 1,
        "properties": {
            "state": {"anyOf": [_TEXT, {"type": "array", "minItems": 1, "items": _TEXT}]},
            **dict.fromkeys(FILTER_TEXT_KEYS, _TEXT),
            **{key: {"enum": list(values)} for key, values in FILTER_ENUMS.items()},
        },
    },
    "sort": {"enum": list(SORTS)},
    "follow_up": {"type": "boolean"},
    "centers": {"type": "array", "minItems": 2, "items": _TEXT},
    "fields": {"type": "array", "minItems": 1, "items": {"enum": list(INFO_FIELDS)}},
}


# tasks là danh sách mã của loại: profile_review nhiều mã không lặp, các loại khác tối đa một mã
def _tasks_schema(intent: str) -> dict[str, Any]:
    schema: dict[str, Any] = {"type": "array", "items": {"enum": list(TASKS_BY_INTENT[intent])}}
    if intent == "profile_review":
        schema["uniqueItems"] = True
    else:
        schema["maxItems"] = 1
    return schema


def _entity_schema(intent: str, key: str) -> dict[str, Any]:
    return _tasks_schema(intent) if key == "tasks" else _ENTITY_SCHEMA[key]


# Khóa bắt buộc theo loại, khớp với check_entities
_REQUIRED_KEYS = {"scenario": ["mode"], "out_of_scope": ["subtype"]}


# Khuôn cho một ý của loại intent: entities chỉ có khóa của loại đó cộng khóa hồ sơ
def _y_branch(intent: str) -> dict[str, Any]:
    return {
        "type": "object",
        "additionalProperties": False,
        "required": ["type", "span", "entities"],
        "properties": {
            "type": {"const": intent},
            "span": _TEXT,
            "entities": {
                "type": "object",
                "additionalProperties": False,
                "required": _REQUIRED_KEYS.get(intent, []),
                "properties": {
                    **dict.fromkeys(PROFILE_KEYS, _TEXT),
                    **{key: _entity_schema(intent, key) for key in sorted(ENTITY_KEYS[intent])},
                },
            },
        },
    }


# Khuôn JSON ép SLM khi sinh một tin có nhiều ý: {"intents": [ý, ý, ...]}. Mỗi ý là một nhánh của anyOf theo type,
# model chọn type nào thì chỉ sinh được khóa của loại đó. Chỉ dùng khi slm_format = "schema" (model chưa fine-tune);
# model đã fine-tune dùng format "json" vì Ollama xếp khóa theo bảng chữ cái, lệch thứ tự lúc train
SLM_TIN_SCHEMA = {
    "type": "object",
    "additionalProperties": False,
    "required": ["intents"],
    "properties": {
        "intents": {
            "type": "array",
            "minItems": 1,
            "maxItems": MAX_RAW_Y,
            "items": {"anyOf": [_y_branch(intent) for intent in INTENTS]},
        }
    },
}
