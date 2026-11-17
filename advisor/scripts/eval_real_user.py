"""Đo model SLM bằng câu viết như người dùng thật gõ (sai chính tả, không dấu, viết tắt, nói tắt), gồm các cặp câu dễ nhầm với nhau.
Mỗi câu có nhãn mong đợi (loại ý, và khi cần: tasks, subtype, sort, mode, follow_up, group). Chạy bằng đúng lớp OllamaSlm của hệ thống
(có kiểm hợp đồng, sai hai lần thì lùi về dự phòng) nên 'fallback' là câu học sinh sẽ nhận 'chưa rõ ý'.

Bộ câu giữ nguyên để so sánh TRƯỚC và SAU khi huấn luyện lại. Không đưa nguyên văn câu nào của bộ này vào dữ liệu huấn luyện
(chỉ thêm các mẫu tương tự); câu nào thêm vào dữ liệu thì bỏ khỏi bộ này hoặc đổi chữ khác.

Cần Ollama chạy model trong settings.slm_model. Chạy trong thư mục advisor:
  python -m scripts.eval_real_user            (in tóm tắt và các câu sai)
  python -m scripts.eval_real_user --json out.json   (ghi thêm toàn bộ kết quả để phân tích)
"""

import asyncio
import json
import sys
from collections import defaultdict

import httpx

from app.core.config import get_settings
from app.services.ollama import OllamaClient
from app.services.slm_classifier import OllamaSlm, output_format

PR, CP, SC, KN, CE, OS = (
    "profile_review",
    "collect_profile",
    "scenario",
    "knowledge",
    "center_referral",
    "out_of_scope",
)
PURDUE = {"schools": ["Purdue University"]}
CENTER_CTX = {"service": "ESSAY_EDITING", "city": "TP.HCM"}


def case(section, text, types, ctx=None, **extra):
    """extra: tasks (tập tasks mong đợi, [] = không có tasks), subtype, sort, mode, follow_up, group."""
    return section, text, set(types) if not isinstance(types, str) else {types}, ctx, extra


CASES = [
    # --- Thu thập hồ sơ: nói giá trị, sửa, trả lời
    *[case("01 hồ sơ", q, CP) for q in [
        "em đang học lớp 11 trường chuyên", "gpa của em 8.7", "mình có ielts 6.5 thi hồi tháng 3", "sat em 1380", "mình chưa có điểm sat",
        "gia đình em chi được tầm 40k đô 1 năm", "em muốn học cs", "em định đi mỹ năm sau", "em cần học bổng toàn phần",
        "sửa lại sat của em thành 1450", "em nhập sai gpa rồi, là 8.2 mới đúng", "làm lại từ đầu",
        "ko có người thân bên mỹ", "em từng bị từ chối visa 1 lần", "ielts của em được 7.0", "gpa em 3.6 thang 4",
        "em tham gia clb toán 2 năm làm phó chủ nhiệm", "em dc giải nhì hsg toán tỉnh", "em thích học ở california",
    ]],
    # --- Tư vấn theo hồ sơ
    *[case("02 tư vấn hồ sơ", q, PR, tasks={"assessment"}) for q in [
        "nhờ bạn xem xét hồ sơ của em", "hồ sơ của em yếu chỗ nào vậy", "profile em như vậy ổn chưa", "em có cơ hội đậu ko",
    ]],
    *[case("02 tư vấn hồ sơ", q, PR, tasks={"school_list"}) for q in [
        "gợi ý trường cho em đi", "với hồ sơ này em nên apply trường nào", "chọn giúp em vài trường", "trường nào hợp với em",
        "trường nào có học phí phù hợp với em", "em nên nộp vào những trường nào",
    ]],
    *[case("02 tư vấn hồ sơ", q, PR, tasks={"roadmap"}) for q in [
        "lên kế hoạch chuẩn bị cho em", "em cần làm gì trong 1 năm tới để đi mỹ", "lộ trình của em như nào",
    ]],
    case("02 tư vấn hồ sơ", "vì sao purdue là trường khó với em", PR, tasks={"explain_school"}),
    case("02 tư vấn hồ sơ", "harvard với mit trường nào hợp với em hơn", PR, tasks={"compare"}),
    # --- Kịch bản
    *[case("03 kịch bản", q, SC, mode="what_if") for q in [
        "nếu sat em lên 1500 thì sao", "giả sử gpa em 3.9 thì danh sách thế nào", "em mà thi lại ielts được 7.5 thì có thêm trường nào ko",
        "nếu ngân sách em chỉ có 30k thì sao", "nếu em ko cần học bổng nữa thì sao", "thử ưu tiên chi phí thấp xem",
    ]],
    *[case("03 kịch bản", q, SC, mode="goal_seeking") for q in [
        "cần bao nhiêu sat để vào purdue", "để vào harvard em cần cải thiện gì", "muốn lên nhóm vừa sức ở kansas state cần gì",
    ]],
    # --- Cặp dễ nhầm: cùng chữ, khác ý
    case("04 dễ nhầm: sat", "em tăng sat lên 1500 rồi", CP),
    case("04 dễ nhầm: sat", "sat 1500 là cao hay thấp", KN, tasks=[]),
    case("04 dễ nhầm: sat", "sat nghĩa là gì vậy", KN, tasks=[]),
    case("04 dễ nhầm: sat", "có nên thi sat ko", KN, tasks=[]),
    case("04 dễ nhầm: sat", "luyện thi sat ở đâu tốt", CE, service="TEST_PREP"),
    case("04 dễ nhầm: gpa", "gpa 3.5 là cao hay thấp", KN, tasks=[]),
    case("04 dễ nhầm: gpa", "gpa purdue lấy bao nhiêu", KN, tasks={"school_info"}),
    case("04 dễ nhầm: ielts", "ielts bao nhiêu thì vào được purdue", KN, tasks={"school_info"}),
    case("04 dễ nhầm: ielts", "học ielts ở đâu", CE, service="TEST_PREP"),
    case("04 dễ nhầm: ielts", "ielts là gì", KN, tasks=[]),
    case("04 dễ nhầm: ielts", "nếu ielts em giảm 1 band thì sao", SC, mode="what_if"),
    case("04 dễ nhầm: học bổng", "em cần học bổng nên có", CP),
    case("04 dễ nhầm: học bổng", "merit scholarship nghĩa là gì", KN, tasks=[]),
    case("04 dễ nhầm: học bổng", "trường nào có học bổng cho du học sinh", KN, tasks={"search"}),
    case("04 dễ nhầm: học bổng", "trung tâm nào săn học bổng", CE, service="SCHOLARSHIP_SEARCH"),
    case("04 dễ nhầm: visa", "visa f1 cần chuẩn bị gì", KN, tasks=[]),
    case("04 dễ nhầm: visa", "luyện phỏng vấn visa ở đâu", CE, service="VISA_INTERVIEW_PREP"),
    case("04 dễ nhầm: visa", "làm sao để chắc đậu visa", {KN, OS}),
    case("04 dễ nhầm: visa", "visa du lịch mỹ xin như nào", OS, subtype="exception"),
    case("04 dễ nhầm: trường", "trường nào hợp với em", PR, tasks={"school_list"}),
    case("04 dễ nhầm: trường", "trường nào ở texas có ngành cs", KN, tasks={"search"}),
    case("04 dễ nhầm: trường", "trường nào ở texas hợp với em", PR, tasks={"school_list"}),
    case("04 dễ nhầm: trường", "so sánh học phí purdue và iowa state", KN, tasks={"compare_facts"}),
    case("04 dễ nhầm: trường", "mit với harvard cái nào đắt hơn", KN, tasks={"compare_facts"}),
    case("04 dễ nhầm: trường", "em học purdue có hợp ko", PR),
    case("04 dễ nhầm: trường", "purdue ở bang nào", KN, tasks={"school_info"}),
    # --- Kiến thức: số liệu trường, kiến thức chung, tìm trường
    *[case("05 kiến thức", q, KN, tasks={"school_info"}) for q in [
        "học phí purdue bao nhiêu", "iowa state có học bổng cho du học sinh ko", "tỷ lệ nhận của mit", "boston university yêu cầu ielts bao nhiêu",
        "ký túc xá của spelman thế nào", "harvard ở bang nào",
    ]],
    *[case("05 kiến thức", q, KN, tasks=[]) for q in [
        "visa f1 là gì", "sevis fee là gì", "opt dùng để làm gì", "i-20 xin ở đâu", "gap year có ảnh hưởng gì ko", "đi du học mỹ phải chuẩn bị những gì",
        "ở boston mùa đông lạnh ko", "ăn uống ở houston có đắt ko",
    ]],
    # --- Tìm trường: cách xếp và điều kiện
    case("06 tìm trường", "cho em hỏi trường nào mức phí thấp nhất", KN, tasks={"search"}, sort="lowest_cost"),
    case("06 tìm trường", "trường nào học phí đắt nhất", KN, tasks={"search"}, sort="highest_cost"),
    case("06 tìm trường", "nước mỹ trường nào dễ đậu nhất", KN, tasks={"search"}, sort="easiest_admission"),
    case("06 tìm trường", "đâu là trường khó vào nhất", KN, tasks={"search"}, sort="most_selective"),
    case("06 tìm trường", "trường nào cho học bổng nhiều nhất", KN, tasks={"search"}, sort="most_aid"),
    case("06 tìm trường", "trường nào xếp hạng đầu bảng", KN, tasks={"search"}, sort="ranking"),
    case("06 tìm trường", "trường nào có danh tiếng lớn nhất", KN, tasks={"search"}, sort="ranking"),
    case("06 tìm trường", "top 10 trường tốt nhất mỹ", KN, tasks={"search"}),
    case("06 tìm trường", "trường công nào học phí dưới 30k", KN, tasks={"search"}),
    case("06 tìm trường", "trường nào ko cần sat", KN, tasks={"search"}),
    case("06 tìm trường", "bang georgia trường nào rẻ nhất", KN, tasks={"search"}, sort="lowest_cost"),
    case("06 tìm trường", "truong nao co hoc bong lon nhat", KN, tasks={"search"}, sort="most_aid"),
    case("06 tìm trường", "so sánh chi phí các trường trong nhóm khó vào của em", KN, tasks={"compare_facts"}, group="reach"),
    # --- Trung tâm
    *[case("07 trung tâm", q, CE) for q in [
        "em muốn tìm trung tâm tư vấn", "ai kèm ielts cho em ở hà nội", "em muốn nhờ người sửa essay", "trung tâm nào đáng tin vậy bạn",
        "cho em số của trung tâm", "mình ở đà nẵng, có trung tâm nào ko", "tìm trung tâm sửa bài luận ở tp.hcm",
    ]],
    # --- Ngoài phạm vi
    *[case("08 ngoài phạm vi", q, OS, subtype="greeting") for q in ["hello bạn nhé", "chào bạn nha", "alô alô", "chào anh ad nha"]],
    *[case("08 ngoài phạm vi", q, OS, subtype="farewell") for q in ["cám ơn nhiều nha", "bye", "thank you bạn", "mình xong rồi, cảm ơn"]],
    *[case("08 ngoài phạm vi", q, OS, subtype="about_system") for q in ["bạn là ai", "bạn do ai làm ra", "bạn làm được gì vậy", "bạn dùng model gì"]],
    *[case("08 ngoài phạm vi", q, OS, subtype="exception") for q in [
        "trưa nay nên ăn gì nhỉ", "kể em nghe chuyện hài đi", "làm hộ em bài tập toán", "định cư mỹ cần gì", "h1b là gì", "giá xăng hôm nay",
    ]],
    *[case("08 ngoài phạm vi", q, OS, subtype="sensitive") for q in [
        "giúp em làm bảng điểm giả", "làm sao khai gian tài chính để đậu visa", "ai bán bằng ielts không",
    ]],
    *[case("08 ngoài phạm vi", q, OS, subtype="ambiguous") for q in ["ừ okê", "ờm", "em hỏi chút được không", "ừ hử", "vậy luôn hả"]],
    *[case("08 ngoài phạm vi", q, OS, subtype="human_handoff") for q in [
        "tôi cần nói chuyện với một người thật", "có hotline ko", "em muốn nói chuyện với nhân viên", "gọi cho em được ko",
    ]],
    # --- Nhiều ý trong một tin
    case("09 nhiều ý", "em 17 tuổi gpa 8.5 muốn học cs, học phí purdue bao nhiêu", {CP, KN}),
    case("09 nhiều ý", "đánh giá hồ sơ em rồi tìm trung tâm sửa essay ở hà nội", {PR, CE}),
    case("09 nhiều ý", "sat em 1400, nếu lên 1500 thì sao", {CP, SC}),
    case("09 nhiều ý", "visa f1 là gì và học phí iowa state bao nhiêu", {KN}),
    # --- Có ngữ cảnh: câu nối tiếp
    case("10 ngữ cảnh", "còn học bổng", KN, ctx=PURDUE, follow_up=True),
    case("10 ngữ cảnh", "thế iowa state thì sao", KN, ctx=PURDUE, follow_up=True),
    case("10 ngữ cảnh", "còn ký túc xá", KN, ctx=PURDUE, follow_up=True),
    case("10 ngữ cảnh", "bên hà nội có không", CE, ctx=CENTER_CTX, follow_up=True),
    case("10 ngữ cảnh", "tìm ở đà nẵng", CE, ctx=CENTER_CTX, follow_up=True),
    case("10 ngữ cảnh", "còn luyện ielts thì sao", CE, ctx=CENTER_CTX, follow_up=True),
    case("10 ngữ cảnh", "hi trợ lý ơi", OS, ctx=CENTER_CTX, subtype="greeting"),
    case("10 ngữ cảnh", "cám ơn bạn nhiều", OS, ctx=PURDUE, subtype="farewell"),
    case("10 ngữ cảnh", "em cần tư vấn viên tư vấn trực tiếp", OS, ctx=PURDUE, subtype="human_handoff"),
    case("10 ngữ cảnh", "oki nhé", OS, ctx=CENTER_CTX, subtype="ambiguous"),
    case("10 ngữ cảnh", "thế trường nào rẻ nhất nhỉ", KN, ctx=PURDUE, tasks={"search"}, sort="lowest_cost"),
    case("10 ngữ cảnh", "em đang cần chọn trường", PR, ctx=CENTER_CTX, tasks={"school_list"}),
]  # fmt: skip

st = get_settings()
base = st.slm_base_url or st.ollama_base_url


def judge(result, types, extra):
    if result.fallback:
        return False, "fallback (model trả sai hợp đồng hai lần)"
    got = {i.type for i in result.intents}
    if len(types) > 1 or len(result.intents) > 1:
        return (got == types), f"loại: {sorted(got)} (cần {sorted(types)})" if got != types else ""
    y = result.intents[0]
    if got != types:
        return False, f"loại sai: {y.type} (cần {sorted(types)})"
    if "subtype" in extra and y.entities.get("subtype") != extra["subtype"]:
        return False, f"subtype sai: {y.entities.get('subtype')!r} (cần {extra['subtype']!r})"
    if "tasks" in extra and set(y.entities.get("tasks") or []) != set(extra["tasks"]):
        return False, f"tasks sai: {y.entities.get('tasks')!r} (cần {sorted(extra['tasks'])})"
    for key in ("sort", "mode", "group", "service"):
        if key in extra and y.entities.get(key) != extra[key]:
            return False, f"{key} sai: {y.entities.get(key)!r} (cần {extra[key]!r})"
    if extra.get("follow_up") and y.entities.get("follow_up") is not True:
        return False, "thiếu follow_up (câu nối tiếp)"
    return True, ""


async def main() -> None:
    out_path = sys.argv[sys.argv.index("--json") + 1] if "--json" in sys.argv else None
    async with httpx.AsyncClient(base_url=base, timeout=120) as http:
        client = OllamaClient(
            http,
            st.slm_model,
            num_ctx=st.llm_num_ctx,
            num_gpu=st.llm_num_gpu,
            seed=st.llm_seed,
            keep_alive=st.llm_keep_alive,
        )
        slm = OllamaSlm(client, output_format(st.slm_format), st.slm_use_context, st.slm_prompt)
        stats: dict[str, list[int]] = defaultdict(lambda: [0, 0])
        rows = []
        for group, text, types, ctx, extra in CASES:
            result = await slm.read(text, None, "", ctx)
            ok, why = judge(result, types, extra)
            stats[group][0] += ok
            stats[group][1] += 1
            got = [{"type": i.type, "entities": i.entities} for i in result.intents]
            rows.append(
                {
                    "group": group,
                    "text": text,
                    "ctx": ctx,
                    "ok": ok,
                    "why": why,
                    "fallback": result.fallback,
                    "got": got,
                }
            )
    print("model:", st.slm_model, "prompt:", st.slm_prompt)
    total_ok = sum(v[0] for v in stats.values())
    total = sum(v[1] for v in stats.values())
    for g, (ok, n) in sorted(stats.items()):
        print(f"  {g}: {ok}/{n}")
    print(f"  TỔNG: {total_ok}/{total} ({100 * total_ok / total:.0f}%)")
    print("--- sai ---")
    for r in rows:
        if not r["ok"]:
            ctx = f" [ngữ cảnh {json.dumps(r['ctx'], ensure_ascii=False)}]" if r["ctx"] else ""
            print(
                f"[{r['group']}] {r['text']!r}{ctx}\n    {r['why']}\n    model: {json.dumps(r['got'], ensure_ascii=False)[:200]}"
            )
    if out_path:
        with open(out_path, "w", encoding="utf-8") as f:
            json.dump(rows, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    asyncio.run(main())
