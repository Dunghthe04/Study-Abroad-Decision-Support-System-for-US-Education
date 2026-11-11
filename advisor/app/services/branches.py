"""Các nhánh xử lý tin nhắn theo loại câu hỏi SLM (kiến trúc mới: không còn chuỗi bước B0 đến B6).

SLM tách mỗi tin thành các ý, mỗi ý thuộc một trong 6 loại, mỗi loại là một nhánh. ChatFlow gọi nhánh của
từng ý. Nhánh nào chưa làm xong thì tạm dùng skill cũ bên trong (lớp tên Legacy...);
làm xong thì thay lớp đó trong build_branches, không phải sửa ChatFlow.
"""

import datetime
from typing import Any

from app.schemas.chat import ChatState, QuickReply, Widget
from app.schemas.slm import INFO_FIELDS, SlmTinResult, SlmY, single_school, task_of
from app.services import search_filters
from app.services.advice_writer import AdviceWriter
from app.services.chat_flow import (
    NEED_PROFILE,
    NOT_READY,
    OPENING_QUESTIONS,
    Branch,
    SkillHandler,
    SkillOutput,
    ask_major,
    combine,
    start_intake,
)
from app.services.goal_target import plain, target_code
from app.services.priority import detect_priorities
from app.services.priority import from_model as priority_from_model
from app.services.priority import label as priority_label
from app.services.scenario import FILTER_FACTORS, LABELS, SUPPORTED_TEXT, WhatIf, parse_what_if
from app.services.skill_input import skill_request_of
from app.services.skill_rules import REQUIRED_B3, REQUIRED_B4, REQUIRED_BY_SKILL, missing_fields
from app.services.skill_template import TEMPLATE_TASKS, build_skill, tasks_of
from app.services.skills.profile_handler import ChatProfileHandler
from app.services.skills.skill_danh_gia import gpa_table_of
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
LOOKING_UP = "Mình đang tra số liệu của trường này nhé."


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
        if task == "search" and filters:
            plan = await search_filters.normalize(filters, y.entities.get("sort"))
            return SkillOutput(
                LOOKING_UP,
                action="school_search",
                action_data={
                    "question": y.span,
                    "filters": plan.filters,
                    "sort": plan.sort,
                    "unresolved": plan.unresolved,
                    "model_filters": filters,  # dạng model trả, để câu tiếp nối thêm điều kiện vào
                    "model_sort": y.entities.get("sort"),
                    "kb_answer": LOOKING_UP,
                },
            )
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
        return SkillOutput(NOT_READY)


# Chưa có danh sách gợi ý thì không có gì để thử kịch bản
NEED_RESULTS = "Bạn cần có danh sách trường gợi ý trước thì mình mới thử kịch bản được. Bạn muốn mình gợi ý trường trước không?"
NOTHING_TO_TRY = f"Mình chưa thử được thay đổi này. Hiện mình thử được: {SUPPORTED_TEXT}."
ASK_SCHOOL = "Bạn muốn vào trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé."
# Mục lọc trường: không thử tạm được, sửa hồ sơ rồi gợi ý lại (.NET không tự chạy lại gợi ý khi hồ sơ đổi)
FILTER_TEXT = (
    "{names} là điều kiện lọc trường nên mình không thử tạm được. Bạn sửa {names} trong hồ sơ rồi nhờ mình "
    "gợi ý trường lại thì danh sách sẽ theo {names} mới. Sau này muốn đổi lại lúc nào cũng được."
)
# Mục phần gợi ý trường không dùng
NO_EFFECT_TEXT = "{names} không làm thay đổi danh sách trường gợi ý."
TRY_INSTEAD = f"Bạn thử đổi được: {SUPPORTED_TEXT}."


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
def cannot_try(what_if: WhatIf) -> SkillOutput:
    sentences = group_sentences(what_if)
    if not sentences:
        return SkillOutput(NOTHING_TO_TRY)
    if not what_if.filters:
        sentences.append(TRY_INSTEAD)
    sections = dict.fromkeys(FILTER_FACTORS[key] for key in what_if.filters)
    replies = [QuickReply(label=f"Sửa {name}", value=f"Em muốn sửa {name}") for name in sections]
    return SkillOutput(" ".join(sentences), quick_replies=replies)


# Có thay đổi thử được kèm mục không thử được: nói rõ từng loại sau câu "Mình thử kịch bản"
def cannot_try_note(what_if: WhatIf) -> str:
    grouped = {LABELS[key] for key in what_if.filters + what_if.no_effect}
    others = [label for label in what_if.unsupported if label not in grouped]
    sentences = ([f"Mình chưa thử được: {', '.join(others)}."] if others else []) + group_sentences(what_if)
    return "".join(f" {sentence}" for sentence in sentences)


# Kịch bản: what_if thì SLM đã trích và chuẩn hóa giá trị, đây kiểm tra rồi giao .NET chạy simulate (action "simulate");
# goal_seeking thì giao .NET tìm trường và chạy goal-seek (action "goal_seek")
class ScenarioBranch:
    # writer: Claude viết lời khi không có gì thử được; None thì dùng câu cố định
    def __init__(self, writer: WhatIfWriter | None = None) -> None:
        self._writer = writer

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        entities = slm.intents[0].entities
        if entities.get("mode") == "goal_seeking":
            return self._goal_seek(entities)
        if entities.get("mode") != "what_if":
            return SkillOutput(NOT_READY)
        if not state.has_results:
            return SkillOutput(NEED_RESULTS)
        try:
            what_if = parse_what_if(entities.get("changes", []))
        except AskAgain as ask:
            return SkillOutput(ask.question)
        # Ưu tiên: model trả (priority_*) thì tin model, quy tắc từ khóa bổ sung cho yếu tố model chưa nêu
        priorities = {**detect_priorities(message), **priority_from_model(entities.get("changes", []))}
        if not what_if.changes and not priorities:
            return await self._cannot_try(what_if, entities.get("changes", []))
        note = cannot_try_note(what_if)
        tried = ", ".join(
            part for part in (what_if.label(), priority_label(priorities) if priorities else "") if part
        )
        data: dict = {"changes": what_if.changes, "unsupported": what_if.unsupported}
        if priorities:
            data["priorities"] = priorities
        return SkillOutput(f"Mình thử kịch bản: {tried}.{note}", action="simulate", action_data=data)

    # Không có gì thử được mà có mục lọc trường hoặc mục không ảnh hưởng: Claude viết lời, nút sửa vẫn do code gắn.
    # Không có writer hoặc Claude sai cả hai lần thì dùng câu cố định (cannot_try)
    async def _cannot_try(self, what_if: WhatIf, changes: list[dict]) -> SkillOutput:
        fallback = cannot_try(what_if)
        if self._writer is None or not (what_if.filters or what_if.no_effect):
            return fallback
        answer = await self._writer.write(what_if, changes)
        return SkillOutput(answer, quick_replies=fallback.quick_replies) if answer else fallback

    # "Cần gì để vào trường X": tên trường còn là chữ, .NET khớp với 249 trường rồi chạy goal-seek.
    # Không cần danh sách gợi ý có sẵn: goal-seek tính từ hồ sơ và dữ liệu trường
    @staticmethod
    def _goal_seek(entities: dict) -> SkillOutput:
        school = str(entities.get("school") or "").strip()
        target = target_code(entities.get("target_group"))
        if not school:
            # Chưa nêu trường: hiện ô chọn trường có gợi ý, giữ nhóm đích để câu hỏi gửi lại đúng ý
            return SkillOutput(ASK_SCHOOL, widget=Widget(type="school", data={"target": target}))
        return SkillOutput(
            f"Mình xem giúp bạn cần gì để vào {school} nhé.",
            action="goal_seek",
            action_data={"school": school, "target": target},
        )


# Câu trả lời cố định theo subtype, không gọi model.
# farewell gồm cả chào, cảm ơn, tạm biệt nên dùng một câu hợp với cả ba
OUT_OF_SCOPE = {
    "exception": "Mình chỉ hỗ trợ tư vấn du học Mỹ thôi nhé.",
    "sensitive": "Mình không hỗ trợ việc này. Hồ sơ du học cần trung thực, mình giúp bạn chuẩn bị đúng cách nhé.",
    "farewell": "Rất vui được trò chuyện với bạn! Bạn cần mình giúp gì về du học Mỹ không?",
    "ambiguous": "Mình chưa rõ ý bạn lắm. Bạn nói cụ thể hơn giúp mình nhé.",
}


# Ngoài phạm vi: trả câu cố định. Câu hồ sơ đang chờ (nếu có) do ChatFlow nhắc lại như mọi câu hỏi xen ngang
class OutOfScopeBranch:
    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        kind = slm.intents[0].entities.get("subtype", "ambiguous")
        return SkillOutput(OUT_OF_SCOPE.get(kind, OUT_OF_SCOPE["ambiguous"]))


# TẠM, phần của bạn: tư vấn theo hồ sơ bằng skill cũ (đánh giá, lộ trình, giải thích một trường).
# Làm xong nhánh mới (SLM sinh skill cá nhân hóa, Claude trả lời) thì thay lớp này
class LegacyAdviceBranch:
    def __init__(self, skills: dict[str, SkillHandler]) -> None:
        self._skills = skills

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        if not state.has_results:
            name = "skill_danh_gia"
        elif single_school(slm.intents[0].entities):
            name = "skill_explain"
        else:
            name = "skill_lo_trinh"
        if name in REQUIRED_BY_SKILL:
            asked = ask_missing_profile(state, slm.intents[0], REQUIRED_BY_SKILL[name])
            if asked is not None:
                return asked
        return await self._skills[name].run(state, message)


# Thiếu hồ sơ: hỏi tiếp hồ sơ (thiếu ngành thì hiện ô chọn ngành) thay vì tư vấn khi chưa đủ thông tin.
# Đang hỏi hồ sơ dở (có câu đang chờ) thì chỉ báo cần thêm hồ sơ, ChatFlow nhắc lại câu đang chờ.
# Đủ hồ sơ thì trả None để nhánh tư vấn làm tiếp
def ask_missing_profile(state: ChatState, y: SlmY, required: list[str]) -> SkillOutput | None:
    missing = missing_fields(state.profile, required)
    if not missing:
        return None
    # Nhớ câu tư vấn để trả lời ngay khi học sinh xác nhận hồ sơ (ProfileBranch). Chỉ giữ câu đầu tiên
    if state.pending_advice is None:
        state.pending_advice = y
    if state.question_id and state.question_id not in OPENING_QUESTIONS:
        return SkillOutput(NEED_PROFILE)
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
RECOMMENDING = "Mình lọc danh sách trường hợp với hồ sơ và ngành của bạn nhé."
# Hồ sơ vừa đủ mà học sinh chưa hỏi câu tư vấn nào (tin là "Đúng rồi"): đánh giá hồ sơ như trước đây
DEFAULT_QUESTION = "Đánh giá hồ sơ của em giúp em."
# Câu chỉ hỏi giải thích trường mà chưa nêu trường nào
ASK_WHICH_SCHOOLS = (
    "Bạn muốn hỏi về trường nào? Bạn nhắn tên trường giúp mình nhé, "
    'hoặc nhắn "gợi ý trường cho em" để mình lọc danh sách trường hợp với hồ sơ của bạn.'
)
CANNOT_ANSWER = "Mình chưa viết được câu trả lời cho câu này. Bạn bấm hỏi lại giúp mình nhé."
ASK_AGAIN_LABEL = "Hỏi lại"


def asks_for_school_list(text: str) -> bool:
    folded = plain(text)
    return any(word in folded for word in SCHOOL_LIST_WORDS)


# Câu profile_review chỉ có so sánh hoặc giải thích trường (không có việc của khuôn, không chọn trường):
# - compare (schema bắt có từ hai trường): giao .NET so số liệu như câu so sánh kiến thức, kể cả khi câu có
#   chữ "trường nào" ("Iowa và Purdue trường nào hợp với em hơn")
# - explain_school chưa nêu trường và không xin danh sách trường: nhờ nêu tên trường
# - còn lại (có tên trường mà chưa có kết quả gợi ý, hoặc xin danh sách): trả None để chạy gợi ý trường
def others_only_output(y: SlmY, tasks: list[str], school_list_words: bool) -> SkillOutput | None:
    schools = [t.strip() for t in y.entities.get("schools") or []]
    if "compare" in tasks and len(schools) >= 2:
        return SkillOutput(
            LOOKING_UP,
            action="school_compare",
            action_data={
                "question": y.span,
                "schools": schools,
                "fields": info_fields(y.entities),
                "kb_answer": LOOKING_UP,
            },
        )
    if not school_list_words and not single_school(y.entities):
        return SkillOutput(ASK_WHICH_SCHOOLS)
    return None


# Tư vấn theo hồ sơ (phần của Việt). Câu về trường giao code có sẵn: hỏi một trường đã gợi ý thì giải thích
# trường đó (skill_explain → .NET), xin danh sách trường thì chạy gợi ý (.NET). Câu còn lại (đánh giá hồ sơ,
# lộ trình, hoặc cả hai: SLM tách sẵn trong tasks) thì code dựng skill từ khuôn (skill_ho_so_khuon),
# Claude viết câu trả lời. Claude sai thì báo chưa trả lời được kèm nút hỏi lại, không dùng skill tĩnh
class GeneratedAdviceBranch:
    def __init__(self, skills: dict[str, SkillHandler], writer: AdviceWriter) -> None:
        self._skills = skills
        self._writer = writer

    async def run(self, state: ChatState, message: str, slm: SlmTinResult) -> SkillOutput:
        y = slm.intents[0]
        question = y.span if y.type == "profile_review" else DEFAULT_QUESTION
        if y.type == "profile_review" and single_school(y.entities) and state.has_results:
            return await self._skills["skill_explain"].run(state, question)

        # Việc bộ phân loại tách ra. Khuôn chỉ làm assessment, roadmap; chọn trường, so sánh, giải thích trường là
        # phần của Dũng. Câu chỉ có việc của Dũng thì giao code có sẵn (xem others_only_output)
        tasks = (y.entities.get("tasks") or None) if y.type == "profile_review" else None
        others_only = tasks is not None and not any(t in TEMPLATE_TASKS for t in tasks)
        school_list = y.type == "profile_review" and (asks_for_school_list(question) or others_only)
        if others_only and "school_list" not in tasks:
            out = others_only_output(y, tasks, asks_for_school_list(question))
            if out is not None:
                return out
        # Gợi ý trường cần ngành để lọc trường; tư vấn hồ sơ chỉ cần các mục cơ bản
        asked = ask_missing_profile(state, y, REQUIRED_B4 if school_list else REQUIRED_B3)
        if asked is not None:
            return asked
        if school_list:
            return SkillOutput(RECOMMENDING, action="recommend")

        # Code dựng skill từ khuôn (không gọi SLM, không hỏng); writer chỉ nhận việc của khuôn
        today = datetime.date.today()
        req = skill_request_of(state, question, today)
        skill = build_skill(req, tasks, state.profile, today, gpa_table_of(state.grade_scale))
        answer = await self._writer.write(skill, req, state.schools, tasks_of(tasks))
        if answer is None:
            return SkillOutput(
                CANNOT_ANSWER, quick_replies=[QuickReply(label=ASK_AGAIN_LABEL, value=question)]
            )
        return SkillOutput(answer)


# Trả lời câu hồ sơ đang chờ hoặc cập nhật hồ sơ. Hồ sơ vừa đủ trong lượt này (học sinh bấm "Đúng rồi" ở X1)
# thì tư vấn luôn, không bắt học sinh nhắn thêm một lần nữa mới thấy kết quả.
# Có câu tư vấn bị hoãn (pending_advice) thì trả lời đúng câu đó, không phải tin "Đúng rồi" của lượt này.
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
            return combine(output, await self._advice.run(state, message, slm))
        return output


# Bảng nhánh theo loại câu hỏi SLM. skills: các skill cũ do skills.build_handlers tạo.
# main.py luôn truyền writer nên chạy thật dùng nhánh tư vấn mới. Chỉ test cũ (chưa viết lại) không
# truyền, khi đó tạm dùng LegacyAdviceBranch
def build_branches(
    skills: dict[str, SkillHandler],
    writer: AdviceWriter | None = None,
    what_if_writer: WhatIfWriter | None = None,
) -> dict[str, Branch]:
    advice: Branch = (
        GeneratedAdviceBranch(skills, writer) if writer is not None else LegacyAdviceBranch(skills)
    )
    return {
        "collect_profile": ProfileBranch(ChatProfileHandler(skills), advice),
        "profile_review": advice,
        "scenario": ScenarioBranch(what_if_writer),
        "knowledge": KnowledgeBranch(skills["skill_tra_loi"])
        if "skill_tra_loi" in skills
        else NotReadyBranch(),
        "center_referral": (
            CenterBranch(skills["skill_chuyen_trung_tam"])
            if "skill_chuyen_trung_tam" in skills
            else NotReadyBranch()
        ),
        "out_of_scope": OutOfScopeBranch(),
    }
