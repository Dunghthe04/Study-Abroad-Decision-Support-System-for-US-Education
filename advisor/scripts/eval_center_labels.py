"""Đo model SLM hiện tại trên nhiều cách nói về trung tâm (chưa có trong dữ liệu huấn luyện): bao nhiêu câu đúng, sai kiểu gì.
Dùng đúng lớp OllamaSlm của hệ thống (có kiểm hợp đồng, sai hai lần thì lùi về dự phòng) nên 'fallback' là câu học sinh sẽ nhận 'chưa rõ ý'.
Cần Ollama chạy với model trong settings.slm_model. Bộ câu giữ nguyên để so sánh trước và sau khi huấn luyện lại; không đưa câu nào của bộ này vào dữ liệu huấn luyện.

Chạy trong thư mục advisor:
  python -m scripts.eval_center_labels
"""

import asyncio
import json
from collections import defaultdict

import httpx

from app.core.config import get_settings
from app.services.ollama import OllamaClient
from app.services.skill_call import SKILLS_DIR, load_skill
from app.services.slm_classifier import OllamaSlm, build_messages, output_format

# (nhóm, câu, loại mong đợi, dịch vụ mong đợi, thành phố mong đợi). types: tập loại chấp nhận được
CASES = [
    # G1: tìm trung tâm chung chung
    *[("G1 chung chung", q, {"center_referral"}, None, None) for q in [
        "tôi muốn tìm 1 trung tâm để tư vấn", "mình muốn tìm một trung tâm tư vấn du học", "cho mình xin 1 trung tâm",
        "giới thiệu giúp em 1 trung tâm", "gợi ý trung tâm tư vấn du học Mỹ đi", "có trung tâm nào tư vấn du học không",
        "trung tâm nào tư vấn tốt vậy", "em muốn được tư vấn, có chỗ nào không", "nhờ bạn tìm giúp trung tâm tư vấn",
        "tôi cần 1 trung tâm hỗ trợ du học", "bạn biết trung tâm nào không", "có bên nào tư vấn du học Mỹ không",
        "mình cần một đơn vị tư vấn", "chỉ giúp em chỗ tư vấn du học", "tìm cho tôi 1 trung tâm", "tôi muốn đến trung tâm",
        "địa chỉ trung tâm tư vấn du học ở đâu", "cho em danh sách các trung tâm",
    ]],
    # G2: có dịch vụ
    ("G2 có dịch vụ", "tôi cần trung tâm kèm IELTS", {"center_referral"}, "TEST_PREP", None),
    ("G2 có dịch vụ", "tìm giúp em chỗ luyện SAT", {"center_referral"}, "TEST_PREP", None),
    ("G2 có dịch vụ", "nơi nào hướng dẫn viết essay vậy", {"center_referral"}, "ESSAY_EDITING", None),
    ("G2 có dịch vụ", "có ai chỉnh sửa bài luận cho em không", {"center_referral"}, "ESSAY_EDITING", None),
    ("G2 có dịch vụ", "trung tâm nào giúp em xin học bổng", {"center_referral"}, "SCHOLARSHIP_SEARCH", None),
    ("G2 có dịch vụ", "em cần người luyện phỏng vấn visa F-1", {"center_referral"}, "VISA_INTERVIEW_PREP", None),
    ("G2 có dịch vụ", "chỗ nào giúp chọn trường phù hợp", {"center_referral"}, "SCHOOL_SELECTION", None),
    ("G2 có dịch vụ", "tìm trung tâm hỗ trợ nộp hồ sơ", {"center_referral"}, "APPLICATION_SUBMISSION", None),
    ("G2 có dịch vụ", "ai giúp em ôn thi TOEFL", {"center_referral"}, "TEST_PREP", None),
    ("G2 có dịch vụ", "tôi muốn tìm chỗ học IELTS", {"center_referral"}, "TEST_PREP", None),
    # G3: có thành phố
    ("G3 có thành phố", "tôi muốn tìm 1 trung tâm ở Hà Nội", {"center_referral"}, None, "hà nội"),
    ("G3 có thành phố", "ở Đà Nẵng có trung tâm tư vấn nào không", {"center_referral"}, None, "đà nẵng"),
    ("G3 có thành phố", "trung tâm tư vấn ở Sài Gòn", {"center_referral"}, None, "sài gòn"),
    ("G3 có thành phố", "em ở Cần Thơ, cần tìm trung tâm", {"center_referral"}, None, "cần thơ"),
    ("G3 có thành phố", "tìm trung tâm ở HN", {"center_referral"}, None, "hn"),
    ("G3 có thành phố", "bên Hải Phòng có trung tâm nào không", {"center_referral"}, None, "hải phòng"),
    # G4: dịch vụ và thành phố
    ("G4 dịch vụ + thành phố", "tìm trung tâm luyện IELTS ở Hà Nội", {"center_referral"}, "TEST_PREP", "hà nội"),
    ("G4 dịch vụ + thành phố", "ai sửa essay ở Sài Gòn", {"center_referral"}, "ESSAY_EDITING", "sài gòn"),
    ("G4 dịch vụ + thành phố", "nơi săn học bổng ở TP.HCM", {"center_referral"}, "SCHOLARSHIP_SEARCH", "tp.hcm"),
    ("G4 dịch vụ + thành phố", "em ở Đà Nẵng cần luyện phỏng vấn visa", {"center_referral"}, "VISA_INTERVIEW_PREP", "đà nẵng"),
    # G5: muốn gặp người thật (chấp nhận center_referral hoặc out_of_scope/human_handoff)
    *[("G5 gặp người thật", q, {"center_referral", "out_of_scope"}, None, None) for q in [
        "tôi muốn gặp tư vấn viên", "tìm cho tôi một tư vấn viên", "cho tôi nói chuyện với người thật",
        "tôi cần được tư vấn trực tiếp", "kết nối em với tư vấn viên", "em muốn có người tư vấn riêng",
        "chuyển em sang tư vấn viên",
    ]],
    # G6: đặt lịch, so sánh trung tâm
    ("G6 đặt lịch / so sánh", "giúp tôi đặt lịch hẹn với một trung tâm", {"center_referral"}, None, None),
    ("G6 đặt lịch / so sánh", "so sánh hai trung tâm giúp tôi", {"center_referral"}, None, None),
    # G7: câu dễ nhầm sang trung tâm vì có chữ tìm nhưng KHÔNG phải trung tâm
    ("G7 không phải trung tâm", "tìm trường ở Texas", {"knowledge"}, None, None),
    ("G7 không phải trung tâm", "tìm trường có học bổng cho du học sinh", {"knowledge"}, None, None),
    ("G7 không phải trung tâm", "tìm giúp em trường phù hợp với hồ sơ", {"profile_review"}, None, None),
    ("G7 không phải trung tâm", "tôi muốn tìm hiểu về visa F-1", {"knowledge"}, None, None),
    ("G7 không phải trung tâm", "tìm hiểu học bổng Fulbright", {"knowledge"}, None, None),
    # G8: tìm "trung tâm" nhưng không phải dịch vụ du học Mỹ (ngôn ngữ khác, nước khác, việc khác) phải là ngoài phạm vi
    *[
        ("G8 ngoài dịch vụ du học Mỹ", q, {"out_of_scope"}, None, None)
        for q in [
            "mình muốn tìm nơi học tiếng Nhật",
            "tìm trung tâm tiếng Hàn giúp em",
            "có chỗ nào luyện thi JLPT N2 không",
            "trung tâm tư vấn du học Úc ở đâu",
            "em cần trung tâm du học Canada",
            "tìm trung tâm học nấu ăn",
            "em muốn học vẽ ở đâu",
            "tìm nơi học lái xe",
        ]
    ],
]  # fmt: skip

st = get_settings()
base = st.slm_base_url or st.ollama_base_url
prompt_text, _ = load_skill(SKILLS_DIR / f"{st.slm_prompt}.md")


def judge(result, kinds, service, city):
    """(đúng?, lý do sai). fallback = hệ thống sẽ trả 'chưa rõ ý'."""
    if result.fallback:
        return False, "fallback (model trả sai hợp đồng hai lần)"
    y = result.intents[0]
    if len(result.intents) != 1 or y.type not in kinds:
        return False, f"loại sai: {[i.type for i in result.intents]}"
    if y.type == "center_referral":
        got_service, got_city = y.entities.get("service"), (y.entities.get("city") or "").lower() or None
        if service != got_service:
            return False, f"dịch vụ sai: {got_service!r} (cần {service!r})"
        if city != got_city:
            return False, f"thành phố sai: {got_city!r} (cần {city!r})"
    return True, ""


async def main():
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
        fails = []
        for group, q, kinds, service, city in CASES:
            result = await slm.read(q, None, "")
            ok, why = judge(result, kinds, service, city)
            stats[group][0] += ok
            stats[group][1] += 1
            if not ok:
                raw = ""
                if result.fallback:
                    _, content, _ = await client.chat_json_scored(
                        build_messages(prompt_text, q, None, ""), output_format(st.slm_format)
                    )
                    raw = content.strip()[:170]
                fails.append(
                    (
                        group,
                        q,
                        why,
                        raw
                        or json.dumps(
                            [i.model_dump(exclude={"span"}) for i in result.intents], ensure_ascii=False
                        )[:170],
                    )
                )
        print("model:", st.slm_model)
        total_ok = sum(v[0] for v in stats.values())
        total = sum(v[1] for v in stats.values())
        for g, (ok, n) in stats.items():
            print(f"  {g}: {ok}/{n}")
        print(f"  TỔNG: {total_ok}/{total} ({100 * total_ok / total:.0f}%)")
        print("--- sai ---")
        for g, q, why, raw in fails:
            print(f"[{g}] {q!r}\n    {why}\n    model: {raw}")


asyncio.run(main())
