"""Các nhánh xử lý tin nhắn theo loại câu hỏi SLM (kiến trúc mới: không còn chuỗi bước B0 đến B6).

SLM tách mỗi tin thành các ý, mỗi ý thuộc một trong 6 loại, mỗi loại là một nhánh. ChatFlow gọi nhánh của
từng ý. Thêm hay đổi cách xử lý một loại ý thì sửa lớp nhánh và bảng trong build_branches, không phải sửa ChatFlow.
"""

import datetime
from typing import Any

from app.schemas.chat import ChatState, ProfileField, QuickReply, Widget
from app.schemas.slm import INFO_FIELDS, SlmTinResult, SlmY, single_school, task_of
from app.services import search_filters, small_talk
from app.services.advice_writer import AdviceWriter
from app.services.chat_flow import (
    OPENING_QUESTIONS,
    Branch,
    SkillHandler,
    SkillOutput,
    ask_major,
    awaiting_confirmation,
    combine,
    start_intake,
)
from app.services.fit_method_writer import FitMethodWriter
from app.services.goal_target import plain, target_code
from app.services.priority import from_model as priority_from_model
from app.services.priority import label as priority_label
from app.services.replies import (
    ABOUT_SYSTEM_PAGE_BUTTON,
    ASK_AGAIN_LABEL,
    ASK_SCHOOL,
    ASK_WHICH_SCHOOLS,
    CANNOT_ANSWER,
    CANNOT_TRY_OTHERS,
    CENTER_PAGE_BUTTON,
    DEFAULT_QUESTION,
    EDIT_SECTION_LABEL,
    EDIT_SECTION_VALUE,
    EXPLAIN_ASK_SCHOOL,
    FILTER_TEXT,
    FIT_METHOD,
    GOAL_SEEK_WORKING,
    GREETING_REPLY,
    GROUP_NEED_RESULTS,
    GROUP_TOO_SMALL,
    HANDOFF_FREE,
    HANDOFF_INTRO,
    LOOKING_UP,
    MENU,
    NEED_PROFILE,
    NEED_RESULTS,
    NEXT_AFTER_SAVE,
    NEXT_AFTER_SAVE_REPLIES,
    NO_EFFECT_TEXT,
    NOT_READY,
    NOTHING_TO_TRY,
    OUT_OF_SCOPE,
    RECOMMENDING,
    ROUND_KNOWLEDGE_BUTTON,
    TRIED_SCENARIO,
    TRY_INSTEAD,
    TRY_WHAT,
    menu_buttons,
)
from app.services.scenario import (
    FILTER_FACTORS,
    LABELS,
    SUPPORTED_TEXT,
    WhatIf,
    merge_follow_up,
    parse_what_if,
    try_buttons,
)
from app.services.skill_input import skill_request_of
from app.services.skill_rules import REQUIRED_B3, REQUIRED_B4, missing_fields
from app.services.skill_template import TEMPLATE_TASKS, build_skill, tasks_of
from app.services.skills import major_catalog, skill_ho_so, state_catalog
from app.services.skills.profile_handler import ChatProfileHandler
from app.services.skills.skill_danh_gia import gpa_table_of
from app.services.skills.skill_tinh import ROADMAP_BUTTON, with_province_question
from app.services.skills.validators import AskAgain
from app.services.what_if_writer import WhatIfWriter


# Trung tâm: dùng dịch vụ, thành phố, việc (tasks) model đã tách trong ý center_referral; skill chỉ đổi mã dịch vụ sang mã DB,
# chuẩn hóa thành phố rồi gọi backend, không đọc lại câu bằng AI
class CenterBranch:
    def __init__(self, skill: Any) -> None:
        self._skill = skill

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        return await self._skill.run(state, message, slm.intents[0].entities)


# Kiến thức: kho kiến thức trả lời như cũ, và gửi kèm câu hỏi cho .NET (action "school_info").
# .NET thử lấy số liệu thật của trường từ cơ sở dữ liệu trước ("học phí Harvard bao nhiêu"); không phải câu hỏi về một trường
# hoặc cơ sở dữ liệu không có thì giữ nguyên câu trả lời của kho, nên nhánh này không làm hỏng câu kiến thức chung.
# Bước hiểu mục hỏi và khớp tên trường làm ở .NET, bằng từ khóa và bộ khớp tên (SLM v2 sẽ trả sẵn trường và mục khi đã huấn luyện lại).
#
# Model đã đọc câu và trả đủ trường (schools) lẫn mục hỏi (fields) thì tin model: bỏ qua kho kiến thức (nhanh hơn, đỡ một lượt gọi),
# giao .NET tra thẳng cơ sở dữ liệu theo đúng điều model trả. Câu trả lời tạm LOOKING_UP chỉ hiện nếu .NET chưa kịp thay.
# Model chỉ trả một trong hai, hoặc mục "other" (điều hệ thống không lưu) thì vẫn chạy kho kiến thức làm dự phòng như trên.
# Câu tạm LOOKING_UP: xem replies.py


# Nhóm kết quả model chỉ tới (group) → mã nhóm trong danh sách gợi ý (ResultSchool.group), và số trường tối đa so sánh được (khớp ChatSchoolInfo.MaxCompare bên .NET)
RESULT_GROUP_CODES = {"reach": {"R"}, "target": {"M"}, "safety": {"S"}}
RESULT_GROUP_LABEL = {"reach": "Khó vào", "target": "Vừa sức", "safety": "An toàn hơn"}


class ChatSchoolLimit:
    MAX_COMPARE = 4


def info_fields(entities: dict) -> list[str]:
    """Các mã mục model trả (đã kiểm ở check_entities), theo thứ tự model trả, không trùng."""
    return list(dict.fromkeys(m for m in entities.get("fields") or [] if m in INFO_FIELDS))


class KnowledgeBranch:
    def __init__(self, skill: SkillHandler) -> None:
        self._skill = skill

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        y = slm.intents[0]
        school = single_school(y.entities)
        fields = info_fields(y.entities)
        task = task_of(y.entities)
        # Tìm trường theo điều kiện: model đã trả filters, đổi chữ học sinh gõ sang bộ lọc chuẩn (mã bang, USD...) rồi giao .NET lọc.
        # Không chạy kho kiến thức. Điều nào chưa đọc chắc chắn được thì ghi vào unresolved để hệ thống hỏi lại, không bỏ qua lặng lẽ
        filters = y.entities.get("filters")
        # Tìm trường có điều kiện lọc và/hoặc cách xếp ("trường nào rẻ nhất" chỉ có cách xếp, không có điều kiện lọc)
        if task == "search" and (filters or y.entities.get("sort")):
            plan = await search_filters.normalize(
                filters or {}, search_filters.resolve_cost_sort(y.entities.get("sort"), message)
            )
            return SkillOutput(
                LOOKING_UP,
                action="school_search",
                action_data={
                    "question": y.span,
                    "filters": plan.filters,
                    "sort": plan.sort,
                    "unresolved": plan.unresolved,
                    # Điều kiện số đã bỏ vì không đọc ra (câu đã có cách xếp): .NET ghi rõ trên thẻ kết quả và gợi ý mức để lọc lại
                    "ignored": [
                        {**item, "label": search_filters.LABELS.get(item["key"], item["key"])}
                        for item in plan.ignored
                    ],
                    "model_filters": filters,  # dạng model trả, để câu tiếp nối thêm điều kiện vào
                    "model_sort": y.entities.get("sort"),
                    "kb_answer": LOOKING_UP,
                },
            )
        # Model chỉ vào một nhóm kết quả của học sinh ("các trường nhóm Khó vào"): tên trường lấy từ danh sách gợi ý gần nhất (code chỉ tra, không đoán)
        if task == "compare_facts" and (group := y.entities.get("group")) is not None:
            if not state.has_results:
                return SkillOutput(GROUP_NEED_RESULTS, quick_replies=menu_buttons())
            codes = RESULT_GROUP_CODES[group]
            in_group = [s.name for s in state.schools if s.group in codes][: ChatSchoolLimit.MAX_COMPARE]
            if len(in_group) < 2:
                text = GROUP_TOO_SMALL.format(group=RESULT_GROUP_LABEL[group], count=len(in_group))
                return SkillOutput(text, quick_replies=menu_buttons())
            y = y.model_copy(update={"entities": {**y.entities, "schools": in_group}})
        # So sánh sự thật của từ hai trường: model đã trả các trường (schools), bỏ qua kho kiến thức, .NET lấy số liệu từng trường
        schools = [t.strip() for t in y.entities.get("schools") or []]
        if task == "compare_facts" and len(schools) >= 2:
            return SkillOutput(
                LOOKING_UP,
                action="school_compare",
                action_data={
                    "question": y.span,
                    "schools": schools,
                    "fields": fields,
                    "kb_answer": LOOKING_UP,
                },
            )
        trusted = task in (None, "school_info") and school is not None and any(f != "other" for f in fields)
        out = SkillOutput(LOOKING_UP) if trusted else await self._skill.run(state, message)
        if out.action is not None:
            return out
        out.action = "school_info"
        out.action_data = {
            "question": y.span,
            "school": school,
            "fields": fields,
            "task": task,
            "kb_answer": out.answer,
            "kb_found": bool(out.citations),
            "kb_skipped": trusted,
        }
        return out


# Nhánh chưa làm: báo đang hoàn thiện (kịch bản; kiến thức khi chưa có DB; trung tâm khi chưa có backend)
class NotReadyBranch:
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        return SkillOutput(NOT_READY, quick_replies=menu_buttons())


# Các câu cố định của kịch bản (NEED_RESULTS, FILTER_TEXT, NO_EFFECT_TEXT...) nằm ở replies.py


# Câu về mục lọc trường và mục không làm đổi danh sách trường (rỗng nếu không có). Tên mục nối bằng dấu phẩy và "và",
# viết hoa chữ đầu câu: "Ngành, loại trường và bậc học là..."
def group_sentences(what_if: WhatIf) -> list[str]:
    sentences = []
    for keys, text in ((what_if.filters, FILTER_TEXT), (what_if.no_effect, NO_EFFECT_TEXT)):
        if keys:
            names = [LABELS[key] for key in keys]
            joined = f"{', '.join(names[:-1])} và {names[-1]}" if len(names) > 1 else names[0]
            sentence = text.format(names=joined)
            sentences.append(sentence[:1].upper() + sentence[1:])
    return sentences


# Không có gì thử được: mục lọc trường thì kèm nút mở luồng sửa nhóm đó, mục không ảnh hưởng thì gợi ý mục thử được.
# Mục khác (vd hoạt động, chứng chỉ tiếng Anh khác) vẫn trả câu chung
def cannot_try(what_if: WhatIf, profile: dict[str, ProfileField] | None = None) -> SkillOutput:
    sentences = group_sentences(what_if)
    quick_try = try_buttons(profile or {})
    # Vòng nộp (ED, EA, RD) không có trong công thức: kết quả không đổi, kèm nút hỏi kiến thức về vòng nộp
    if "application_round" in what_if.no_effect:
        quick_try = [ROUND_KNOWLEDGE_BUTTON.model_copy(), *quick_try]
    if not sentences:
        return SkillOutput(
            NOTHING_TO_TRY.format(supported=SUPPORTED_TEXT), quick_replies=quick_try or menu_buttons()
        )
    if not what_if.filters:
        sentences.append(TRY_INSTEAD.format(supported=SUPPORTED_TEXT))
        return SkillOutput(" ".join(sentences), quick_replies=quick_try or menu_buttons("scenario"))
    sections = dict.fromkeys(FILTER_FACTORS[key] for key in what_if.filters)
    replies = [
        QuickReply(label=EDIT_SECTION_LABEL.format(name=name), value=EDIT_SECTION_VALUE.format(name=name))
        for name in sections
    ]
    return SkillOutput(" ".join(sentences), quick_replies=replies)


# Có thay đổi thử được kèm mục không thử được: nói rõ từng loại sau câu "Mình thử kịch bản"
def cannot_try_note(what_if: WhatIf) -> str:
    grouped = {LABELS[key] for key in what_if.filters + what_if.no_effect}
    others = [label for label in what_if.unsupported if label not in grouped]
    sentences = ([CANNOT_TRY_OTHERS.format(names=", ".join(others))] if others else []) + group_sentences(
        what_if
    )
    return "".join(f" {sentence}" for sentence in sentences)


# Kịch bản: what_if thì SLM đã trích và chuẩn hóa giá trị, đây kiểm tra rồi giao .NET chạy simulate (action "simulate");
# goal_seeking thì giao .NET tìm trường và chạy goal-seek (action "goal_seek")
class ScenarioBranch:
    # writer: Claude viết lời khi không có gì thử được; None thì dùng câu cố định
    def __init__(self, writer: WhatIfWriter | None = None, knowledge: SkillHandler | None = None) -> None:
        self._writer = writer
        self._knowledge = knowledge

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        # Câu nối tiếp chỉ nói phần đổi: gộp vào kịch bản lượt trước (UC-155 1a)
        entities = merge_follow_up(slm.intents[0].entities, state.context.scenario)
        if entities.get("mode") == "goal_seeking":
            return self._goal_seek(state, entities)
        if entities.get("mode") != "what_if":
            return SkillOutput(NOT_READY, quick_replies=menu_buttons())
        if not state.has_results:
            return SkillOutput(
                NEED_RESULTS, quick_replies=[MENU["schools"].model_copy(), MENU["assess"].model_copy()]
            )
        try:
            # Bang và ngành đổi sang mã và tên chuẩn của hệ thống (đọc DB một lần rồi giữ); lỗi đọc thì hai yếu tố này chưa thử được
            what_if = parse_what_if(
                entities.get("changes", []), await state_catalog.states(), await major_catalog.majors()
            )
        except AskAgain as ask:
            # Thiếu số hoặc số không hợp lệ: hỏi lại, kèm nút thử nhanh để bấm thay vì gõ
            return SkillOutput(ask.question, quick_replies=try_buttons(state.profile))
        # Ưu tiên: chỉ lấy điều model trả (priority_*), không có quy tắc từ khóa đọc thêm
        priorities = priority_from_model(entities.get("changes", []))
        if not what_if.changes and not priorities:
            return await self._cannot_try(what_if, entities.get("changes", []), state.profile)
        note = cannot_try_note(what_if)
        tried = ", ".join(
            part for part in (what_if.label(), priority_label(priorities) if priorities else "") if part
        )
        data: dict = {"changes": what_if.changes, "unsupported": what_if.unsupported}
        if priorities:
            data["priorities"] = priorities
        # Nhớ kịch bản đã gộp, đúng dạng model nhận: lượt sau chỉ nói phần đổi
        state.context.scenario = {"mode": "what_if", "changes": entities.get("changes", [])}
        return SkillOutput(TRIED_SCENARIO.format(tried=tried, note=note), action="simulate", action_data=data)

    # Học sinh muốn thử thay đổi nhưng chưa nói thay đổi gì (nút menu "Thử thay đổi hồ sơ"): hỏi thử gì, kèm nút thử nhanh
    @staticmethod
    def ask_what(state: ChatState) -> SkillOutput:
        if not state.has_results:
            return SkillOutput(
                NEED_RESULTS, quick_replies=[MENU["schools"].model_copy(), MENU["assess"].model_copy()]
            )
        return SkillOutput(
            TRY_WHAT.format(supported=SUPPORTED_TEXT),
            quick_replies=try_buttons(state.profile) or menu_buttons("scenario"),
        )

    # Không có gì thử được mà có mục lọc trường hoặc mục không ảnh hưởng: Claude viết lời, nút sửa vẫn do code gắn.
    # Không có writer hoặc Claude sai cả hai lần thì dùng câu cố định (cannot_try)
    async def _cannot_try(
        self, what_if: WhatIf, changes: list[dict], profile: dict[str, ProfileField] | None = None
    ) -> SkillOutput:
        fallback = cannot_try(what_if, profile)
        if self._writer is None or not (what_if.filters or what_if.no_effect):
            out = fallback
        else:
            answer = await self._writer.write(what_if, changes)
            out = SkillOutput(answer, quick_replies=fallback.quick_replies) if answer else fallback
        return await self._with_round_knowledge(what_if, out)

    # UC-155: hỏi giả định vòng nộp (ED, EA, RD) thì trả luôn kiến thức về vòng nộp từ Kho tri thức (UC-36) trong cùng lượt,
    # không chỉ đưa nút. Kho không trả lời được thì giữ nguyên câu cũ kèm nút
    async def _with_round_knowledge(self, what_if: WhatIf, out: SkillOutput) -> SkillOutput:
        if self._knowledge is None or "application_round" not in what_if.no_effect:
            return out
        try:
            kb = await self._knowledge.run(ChatState(), ROUND_KNOWLEDGE_BUTTON.value)
        except Exception:  # noqa: BLE001 - lỗi kho không được làm hỏng câu trả lời chính
            return out
        if not kb.answer or kb.answer == NOT_READY:
            return out
        return SkillOutput(
            out.answer + ROUND_KNOWLEDGE_JOIN + kb.answer,
            quick_replies=out.quick_replies,
            citations=kb.citations,
            disclaimer=kb.disclaimer,
        )

    # "Cần gì để vào trường X": tên trường còn là chữ, .NET khớp với 249 trường rồi chạy goal-seek.
    # Không cần danh sách gợi ý có sẵn: goal-seek tính từ hồ sơ và dữ liệu trường
    @staticmethod
    def _goal_seek(state: ChatState, entities: dict) -> SkillOutput:
        school = str(entities.get("school") or "").strip()
        target = target_code(entities.get("target_group"))
        if not school:
            # Chưa nêu trường: hiện ô chọn trường có gợi ý, giữ nhóm đích để câu hỏi gửi lại đúng ý
            return SkillOutput(ASK_SCHOOL, widget=Widget(type="school", data={"target": target}))
        # Yếu tố học sinh hỏi (UC-155): chỉ để .NET nói rõ phần tiếng Anh khi hỏi riêng tiếng Anh. Tìm mức cần đạt vẫn tính cho mọi yếu tố
        factors = [f for f in entities.get("factor") or [] if f in GOAL_FACTORS]
        data: dict = {"school": school, "target": target}
        if factors:
            data["factor"] = factors
        state.context.scenario = {
            "mode": "goal_seeking",
            "school": school,
            "factor": entities.get("factor"),
            "target_group": entities.get("target_group"),
        }
        return SkillOutput(
            GOAL_SEEK_WORKING.format(school=school),
            action="goal_seek",
            action_data=data,
        )


# Câu trả lời cố định theo subtype (replies.OUT_OF_SCOPE), không gọi model; greeting có câu riêng (GREETING_REPLY).
# Mọi câu đều kèm menu nút để học sinh không bị cụt đường


# Ngoài phạm vi: trả câu cố định. Câu hồ sơ đang chờ (nếu có) do ChatFlow nhắc lại như mọi câu hỏi xen ngang
class OutOfScopeBranch:
    # center: skill trung tâm (cùng skill của CenterBranch) để human_handoff đi vào luồng trung tâm; None thì trả câu cố định
    def __init__(self, center: Any | None = None) -> None:
        self._center = center

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        kind = slm.intents[0].entities.get("subtype", "ambiguous")
        if kind == "human_handoff" and self._center is not None:
            # follow_up: dịch vụ và thành phố đã nhớ ở lượt trước được dùng luôn; chưa có thì luồng trung tâm hỏi bằng nút
            output = await self._center.run(state, message, {"follow_up": True})
            output.answer = f"{HANDOFF_INTRO}\n\n{output.answer}"
            return output
        if kind == HANDOFF_FREE:
            # Gói không cho gợi ý trung tâm trong chat (Miễn phí): dẫn tới trang danh bạ Trung tâm, bỏ nút Tìm trung tâm vì nút đó cũng bị khóa
            return SkillOutput(
                OUT_OF_SCOPE[HANDOFF_FREE],
                quick_replies=[CENTER_PAGE_BUTTON.model_copy(), *menu_buttons("centers")],
            )
        if kind == "about_system":
            # Câu cố định kèm nút mở trang Căn cứ tham số (UC-83)
            return SkillOutput(
                OUT_OF_SCOPE["about_system"],
                quick_replies=[ABOUT_SYSTEM_PAGE_BUTTON.model_copy(), *menu_buttons()],
            )
        # Luật small_talk là lưới an toàn cho model chưa học nhãn greeting (bản cũ hay trả ambiguous hoặc farewell cho lời chào)
        if kind == "greeting" or (
            kind in ("ambiguous", "farewell") and small_talk.classify(message) == "greeting"
        ):
            return SkillOutput(GREETING_REPLY, quick_replies=menu_buttons())
        return SkillOutput(OUT_OF_SCOPE.get(kind, OUT_OF_SCOPE["ambiguous"]), quick_replies=menu_buttons())


# Thiếu hồ sơ: hỏi tiếp hồ sơ (thiếu ngành thì hiện ô chọn ngành) thay vì tư vấn khi chưa đủ thông tin.
# Đang hỏi hồ sơ dở (có câu đang chờ) thì chỉ báo cần thêm hồ sơ, ChatFlow nhắc lại câu đang chờ.
# Đủ hồ sơ thì trả None để nhánh tư vấn làm tiếp
def ask_missing_profile(state: ChatState, y: SlmY, required: list[str]) -> SkillOutput | None:
    missing = missing_fields(state.profile, required)
    # Đủ hồ sơ mà còn dự định thi quá hạn thì cũng hỏi trước khi tư vấn (dự định cũ làm lệch đánh giá)
    overdue = skill_ho_so.overdue_card(state) is not None
    if not missing and not overdue:
        return None
    # Nhớ câu tư vấn để trả lời ngay khi học sinh xác nhận hồ sơ (ProfileBranch). Chỉ giữ câu đầu tiên
    if state.pending_advice is None:
        state.pending_advice = y
    # Đang chờ ở bảng rà soát X1 mà hồ sơ thiếu (hồ sơ bị xóa sau khi rà soát) thì không nhắc lại bảng trống, hỏi lại từ đầu
    if state.question_id and state.question_id not in OPENING_QUESTIONS and state.question_id != "X1":
        return SkillOutput(NEED_PROFILE)
    if missing and (review := awaiting_confirmation(state, required)) is not None:
        return review
    if not missing:
        return skill_ho_so.ask_overdue_first(state)
    return ask_major(state) if missing[0] == "major" else start_intake(state, required)


# Câu xin danh sách trường (đã bỏ dấu). SLM xếp các câu này vào profile_review; code giao .NET chạy gợi ý trường
# (pipeline có sẵn), không sinh skill
SCHOOL_LIST_WORDS = (
    "truong nao",
    "chon truong",
    "nhung truong",
    "danh sach truong",
    "goi y truong",
    "de xuat truong",
    "nop truong",
    "apply truong",
    "which universit",
    "which school",
    "which college",
    "universities should",
    "schools should",
    "colleges should",
    "recommend universit",
    "recommend school",
    "recommend college",
)
# Hồ sơ vừa đủ mà học sinh chưa hỏi câu tư vấn nào (tin là "Đúng rồi"): đánh giá hồ sơ như trước đây
# Câu chỉ hỏi giải thích trường mà chưa nêu trường nào
# Số trường đầu danh sách gợi ý hiện sẵn trong ô chọn (cùng giá trị với ChatActions.MaxSuggested bên .NET)
MAX_SUGGESTED = 6


# Chỗ nối câu giả định vòng nộp với câu trả lời của Kho tri thức (UC-155)
ROUND_KNOWLEDGE_JOIN = "\n\n"

# Yếu tố tìm mức cần đạt mà model được trả (hợp đồng SLM): activities là bậc ngoại khóa
GOAL_FACTORS = ("gpa", "sat", "act", "english", "activities")

# Câu explain_fit hỏi cách tính điểm phù hợp ("fit score tính thế nào"), không hỏi về một trường (UC-153 1a)
FIT_METHOD_WORDS = (
    "tinh the nao",
    "tinh nhu the nao",
    "tinh nhu nao",
    "tinh ra sao",
    "tinh sao",
    "tinh kieu gi",
    "cach tinh",
    "cong thuc",
    "duoc tinh",
)


def asks_how_fit_is_computed(text: str) -> bool:
    folded = plain(text)
    return any(word in folded for word in FIT_METHOD_WORDS)


def asks_for_school_list(text: str) -> bool:
    folded = plain(text)
    return any(word in folded for word in SCHOOL_LIST_WORDS)


# Câu profile_review chỉ có so sánh hoặc giải thích trường (không có việc của khuôn, không chọn trường):
# - compare (schema bắt có từ hai trường): giao .NET so số liệu như câu so sánh kiến thức, kể cả khi câu có
#   chữ "trường nào" ("Iowa và Purdue trường nào hợp với em hơn")
# - explain_school chưa nêu trường và không xin danh sách trường: nhờ nêu tên trường
# - còn lại (có tên trường mà chưa có kết quả gợi ý, hoặc xin danh sách): trả None để chạy gợi ý trường
def others_only_output(
    y: SlmY, tasks: list[str], school_list_words: bool, state: ChatState | None = None
) -> SkillOutput | None:
    schools = [t.strip() for t in y.entities.get("schools") or []]
    if "compare" in tasks and len(schools) >= 2:
        # UC-154: câu so sánh theo hồ sơ ("BU hay NYU hợp với em hơn"): .NET tính nhóm và điểm phù hợp từng trường bằng công thức gợi ý.
        # Câu so sánh số liệu (học phí, hạn nộp) không hỏi độ phù hợp đã được đổi sang câu kiến thức ở with_comparison_intent (UC-156)
        return SkillOutput(
            LOOKING_UP,
            action="school_fit_compare",
            action_data={"question": y.span, "schools": schools, "kb_answer": LOOKING_UP},
        )
    # UC-153 1a: hỏi cách tính điểm phù hợp, không nêu trường: giải thích cách tính, dẫn tới trang tham số, không dùng số của trường nào
    if (
        "explain_fit" in tasks
        and not school_list_words
        and not single_school(y.entities)
        and asks_how_fit_is_computed(y.span)
    ):
        return SkillOutput(FIT_METHOD, quick_replies=[ABOUT_SYSTEM_PAGE_BUTTON.model_copy()])
    if not school_list_words and not single_school(y.entities):
        # Ô chọn trường (chọn xong .NET giải thích trường đó), hiện sẵn vài trường đầu danh sách gợi ý nếu có
        suggested = [s.code for s in (state.schools if state else None) or []][:MAX_SUGGESTED]
        return SkillOutput(
            ASK_WHICH_SCHOOLS,
            widget=Widget(
                type="school", data={"target": None, "kind": "explain", "suggested": suggested or None}
            ),
        )
    return None


# Tư vấn theo hồ sơ (phần của Việt). Câu về trường giao code có sẵn: hỏi một trường đã gợi ý thì giải thích
# trường đó (skill_explain → .NET), xin danh sách trường thì chạy gợi ý (.NET). Câu còn lại (đánh giá hồ sơ,
# lộ trình, hoặc cả hai: SLM tách sẵn trong tasks) thì code dựng skill từ khuôn (skill_ho_so_khuon),
# Claude viết câu trả lời. Claude sai thì báo chưa trả lời được kèm nút hỏi lại, không dùng skill tĩnh
class GeneratedAdviceBranch:
    def __init__(
        self,
        skills: dict[str, SkillHandler],
        writer: AdviceWriter,
        method_writer: FitMethodWriter | None = None,
    ) -> None:
        self._skills = skills
        self._writer = writer
        self._method_writer = method_writer

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        y = slm.intents[0]
        question = y.span if y.type == "profile_review" else DEFAULT_QUESTION
        school = single_school(y.entities) if y.type == "profile_review" else None
        if school and state.has_results:
            out = await self._skills["skill_explain"].run(state, question)
            # Trường học sinh nêu không nằm trong danh sách gợi ý (skill chỉ biết hỏi lại "trường nào trong danh sách"):
            # học sinh được quyền hỏi bất kỳ trường nào, nên giao .NET khớp tên với toàn bộ trường rồi chạy goal-seek
            # (nói rõ tài chính, nhóm hiện tại và cần gì để lên nhóm cao hơn), không bắt chọn lại trong danh sách
            if out.action is None and out.answer == EXPLAIN_ASK_SCHOOL:
                return SkillOutput(
                    GOAL_SEEK_WORKING.format(school=school),
                    action="goal_seek",
                    action_data={"school": school, "target": None},
                )
            return out

        # Việc bộ phân loại tách ra. Khuôn chỉ làm assessment, roadmap; chọn trường, so sánh, giải thích trường là
        # phần của Dũng. Câu chỉ có việc của Dũng thì giao code có sẵn (xem others_only_output)
        tasks = (y.entities.get("tasks") or None) if y.type == "profile_review" else None
        others_only = tasks is not None and not any(t in TEMPLATE_TASKS for t in tasks)
        # Xin trường: câu có chữ xin trường, hoặc SLM tách ra việc chọn trường (câu gõ sai, nói kiểu khác vẫn nhận)
        school_list = y.type == "profile_review" and (
            asks_for_school_list(question) or others_only or "school_list" in (tasks or [])
        )
        if others_only and "school_list" not in tasks:
            # UC-154: so sánh theo hồ sơ cần hồ sơ đủ như gợi ý trường (UC-22 E1): thiếu thì hỏi tiếp, hồ sơ xong thì trả lời lại đúng câu này
            if (
                "compare" in tasks
                and len([s for s in y.entities.get("schools") or [] if str(s).strip()]) >= 2
            ):
                asked = ask_missing_profile(state, y, REQUIRED_B4)
                if asked is not None:
                    return asked
            out = others_only_output(y, tasks, asks_for_school_list(question), state)
            if out is not None:
                # UC-153 1a: Claude viết lời từ tham số công bố; không viết được thì giữ câu cố định FIT_METHOD
                if out.answer == FIT_METHOD and self._method_writer is not None:
                    written = await self._method_writer.write()
                    if written is not None:
                        return SkillOutput(written, quick_replies=out.quick_replies)
                return out
        # Gợi ý trường cần ngành để lọc trường; tư vấn hồ sơ chỉ cần các mục cơ bản
        asked = ask_missing_profile(state, y, REQUIRED_B4 if school_list else REQUIRED_B3)
        if asked is not None:
            return asked
        # Chỉ xin trường thì chạy gợi ý luôn. Có cả việc của khuôn (đánh giá, lộ trình chung) thì khuôn viết trước,
        # .NET chạy gợi ý rồi nối danh sách trường vào sau câu trả lời
        template_work = any(t in TEMPLATE_TASKS for t in tasks or [])
        if school_list and not template_work:
            # note: chỉ xin danh sách nên chưa có lời nào do Claude viết, .NET nhờ Claude viết lời cho danh sách
            return SkillOutput(RECOMMENDING, action="recommend", action_data={"note": True})
        recommend = "recommend" if school_list else None

        # Code dựng skill từ khuôn (không gọi SLM, không hỏng); writer chỉ nhận việc của khuôn
        today = datetime.date.today()
        req = skill_request_of(state, question, today)
        skill = build_skill(req, tasks, state.profile, today, gpa_table_of(state.grade_scale), state.roadmap)
        answer = await self._writer.write(skill, req, state.schools, tasks_of(tasks))
        if answer is None:
            return SkillOutput(
                CANNOT_ANSWER,
                quick_replies=[QuickReply(label=ASK_AGAIN_LABEL, value=question)],
                action=recommend,
            )
        # Có lộ trình .NET và học sinh hỏi lộ trình: thêm nút mở lộ trình đầy đủ ở Tổng quan,
        # chưa có tỉnh thì hỏi luôn tỉnh (một lần mỗi phiên, lượt sau .NET lưu)
        if state.roadmap is None or "roadmap" not in tasks_of(tasks):
            return SkillOutput(answer, action=recommend)
        return with_province_question(
            state, SkillOutput(answer, quick_replies=[ROADMAP_BUTTON.model_copy()], action=recommend)
        )


# Hồ sơ vừa lưu mà không có câu tư vấn bị hoãn: hỏi học sinh muốn làm gì tiếp. Nút gửi câu như học sinh tự gõ,
# đi qua SLM nên chỉ chạy đúng việc được chọn


# Trả lời câu hồ sơ đang chờ hoặc cập nhật hồ sơ. Hồ sơ vừa đủ trong lượt này (học sinh bấm "Đúng rồi" ở X1):
# có câu tư vấn bị hoãn (pending_advice) thì trả lời luôn đúng câu đó, không bắt học sinh nhắn lại;
# không có thì chỉ lưu rồi hỏi muốn làm gì tiếp, không tự đánh giá hay lên lộ trình.
# Tin đã có ý profile_review thì thôi: ChatFlow sẽ chạy nhánh tư vấn cho ý đó ngay sau, tư vấn ở đây nữa là trả lời hai lần
class ProfileBranch:
    def __init__(self, handler: ChatProfileHandler, advice: Branch) -> None:
        self._handler = handler
        self._advice = advice

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        was_ready = self._handler.ready(state, REQUIRED_B3)
        output = await self._handler.collect(state, message, slm)
        if not was_ready and self._handler.ready(state, REQUIRED_B3) and "profile_review" not in slm.types():
            original = state.pending_advice
            if original is not None:
                state.pending_advice = None
                return combine(
                    output, await self._advice.run(state, original.span, SlmTinResult(intents=[original]))
                )
            return combine(output, SkillOutput(NEXT_AFTER_SAVE, quick_replies=list(NEXT_AFTER_SAVE_REPLIES)))
        return output


# Bảng nhánh theo loại câu hỏi SLM. skills: các skill do skills.build_handlers tạo.
# writer: Claude viết lời tư vấn hồ sơ. Không có writer (chưa cấu hình Claude) thì nhánh tư vấn báo chưa sẵn sàng
def build_branches(
    skills: dict[str, SkillHandler],
    writer: AdviceWriter | None = None,
    what_if_writer: WhatIfWriter | None = None,
    method_writer: FitMethodWriter | None = None,
) -> dict[str, Branch]:
    advice: Branch = (
        GeneratedAdviceBranch(skills, writer, method_writer) if writer is not None else NotReadyBranch()
    )
    return {
        "collect_profile": ProfileBranch(ChatProfileHandler(skills), advice),
        "profile_review": advice,
        "scenario": ScenarioBranch(what_if_writer, skills.get("skill_tra_loi")),
        "knowledge": KnowledgeBranch(skills["skill_tra_loi"])
        if "skill_tra_loi" in skills
        else NotReadyBranch(),
        "center_referral": (
            CenterBranch(skills["skill_chuyen_trung_tam"])
            if "skill_chuyen_trung_tam" in skills
            else NotReadyBranch()
        ),
        "out_of_scope": OutOfScopeBranch(skills.get("skill_chuyen_trung_tam")),
    }
