"""Đo bước TÌM kiếm của kho kiến thức: câu học sinh hỏi có tìm đúng dòng trong app.knowledge_documents không, và ngưỡng nào hợp lý.
Chỉ đo bước tìm (embedding + so vector), CHƯA gồm SLM phân loại ý, Claude viết lại và kiểm số liệu. Chỉ đọc DB, không ghi gì.
Cần Ollama chạy với model embedding trong settings và kho đã có vector (python -m scripts.ingest_knowledge).

Mỗi câu có dòng đúng mong đợi (entry_no, tức cột id trong file Excel; nhiều dòng cùng đúng thì liệt kê hết). Câu ngoài phạm vi: expect trống,
kỳ vọng hệ thống từ chối. must_not: dòng sai chủ đề mà mình đã thấy tìm nhầm (ví dụ hỏi trượt visa F-1 mà ra dòng visa B-2).
Bộ câu giữ nguyên để so sánh trước và sau khi đổi dữ liệu hay ngưỡng. Nếu Phương Anh sửa id hoặc xóa dòng thì phải cập nhật expect ở đây.

Chạy trong thư mục advisor:
  python -m scripts.eval_knowledge_retrieval            # bảng theo ngưỡng + danh sách câu tìm nhầm
  python -m scripts.eval_knowledge_retrieval --all      # in cả các câu tìm đúng
  python -m scripts.eval_knowledge_retrieval --full     # toàn tuyến: thêm Claude viết lại và kiểm tra (gọi Claude thật, tốn token)
"""

import argparse
import asyncio
import sys
from collections import defaultdict
from dataclasses import dataclass, field

THRESHOLDS = [0.60, 0.65, 0.70, 0.72, 0.75, 0.78, 0.80, 0.82, 0.85, 0.90]
TOP_K = 5


@dataclass(frozen=True)
class Case:
    kind: str  # paraphrase | near_miss | typo | needs_media | out_of_scope
    question: str
    expect: frozenset[int] = field(default_factory=frozenset)  # trống = ngoài phạm vi, phải từ chối
    must_not: frozenset[int] = field(default_factory=frozenset)


def case(
    kind: str, question: str, expect: list[int] | None = None, must_not: list[int] | None = None
) -> Case:
    return Case(kind, question, frozenset(expect or []), frozenset(must_not or []))


CASES = [
    # Cách học sinh hỏi khác câu mẫu: phải tìm ra đúng chủ đề
    case("paraphrase", "Mình mới tìm hiểu, SEVIS là cái gì vậy?", [353, 96]),
    case(
        "paraphrase", "Cho em hỏi form I-20 dùng để làm gì", [351, 95, 853]
    ),  # 853: đã đọc tay, câu trả lời có nói I-20 dùng làm gì
    case("paraphrase", "DS-160 là gì", [358]),
    case("paraphrase", "Xin visa F-1 thì phải đóng phí SEVIS bao nhiêu", [245, 354, 855]),
    case("paraphrase", "Học bổng merit là gì", [395, 240]),
    case("paraphrase", "TOEFL và IELTS nên thi cái nào", [340, 284, 331]),
    case("paraphrase", "Em bị waitlist thì nên làm gì", [103, 257, 377]),
    case(
        "paraphrase", "Early decision khác early action ra sao", [102, 250, 59, 373]
    ),  # 373: đã đọc tay, có so sánh ED và EA
    case("paraphrase", "Common App nộp một lần apply được bao nhiêu trường", [101, 372]),
    case("paraphrase", "Trượt visa lần đầu có nên nộp lại ngay không", [114, 897]),
    case("paraphrase", "Bị từ chối visa thì có phải đóng lại phí visa và SEVIS không", [909]),
    case("paraphrase", "Phỏng vấn visa F-1 thường hỏi những gì", [115, 885]),
    case("paraphrase", "Đi phỏng vấn visa nên mặc đồ gì", [188]),
    case("paraphrase", "I-20 của em bị sai họ tên thì sửa sao", [854]),
    case("paraphrase", "F-1 làm thêm trong trường tối đa bao nhiêu giờ một tuần", [135, 369, 328]),
    case("paraphrase", "STEM OPT kéo dài thêm bao lâu", [126, 367, 868]),
    case("paraphrase", "CPT là gì vậy", [368, 125, 252]),
    case("paraphrase", "Điểm SAT 1360 có nên nộp cho trường test optional không", [75]),
    case("paraphrase", "Học cao đẳng cộng đồng có phải kém chất lượng không", [124, 247, 405]),
    case("paraphrase", "Học online từ Việt Nam lấy bằng Mỹ có xin được F-1 không", [330]),
    case("paraphrase", "Sang Mỹ có bắt buộc mua bảo hiểm y tế không", [117, 401]),
    case("paraphrase", "Chi phí du học Mỹ gồm những khoản nào", [440, 251]),
    case("paraphrase", "Nghỉ một năm sau THPT rồi mới đi Mỹ có ảnh hưởng visa không", [133, 268]),
    case("paraphrase", "Rolling admission nộp muộn có sao không", [119, 259, 376]),
    case("paraphrase", "Học bạ có cần dịch công chứng trước khi nộp không", [105, 739]),
    case("paraphrase", "Sinh viên Việt Nam có điền FAFSA được không", [260]),
    case("paraphrase", "Học bổng full ride là gì", [398]),
    case("paraphrase", "Em làm mất bản giấy I-20 trước ngày phỏng vấn thì làm sao", [202]),
    case("paraphrase", "I-94 là gì", [98, 361]),
    case("paraphrase", "Trường công và trường tư ở Mỹ khác nhau chỗ nào", [276, 402]),
    case("paraphrase", "GPA 3.5 là giỏi hay trung bình", [282]),
    case("paraphrase", "Có thể dùng Duolingo thay cho IELTS không", [90, 230]),
    case("paraphrase", "Ảnh nộp DS-160 cần theo chuẩn nào", [760]),
    case("paraphrase", "Em chưa biết học ngành gì thì nộp đại học Mỹ được không", [466]),
    case("paraphrase", "Điền DS-160 gồm những phần nào", [942]),
    case("paraphrase", "Trong DS-160 chữ Does Not Apply dùng khi nào", [945]),
    case("paraphrase", "Đang điền DS-160 dở mà bị thoát thì làm tiếp được không", [958]),
    case("paraphrase", "Số tiền chứng minh tài chính xin visa F-1 tính ra sao", [764, 14, 399]),
    case("paraphrase", "Visa J-1 là gì", [356, 99]),
    # Câu gần nghĩa nhưng KHÁC Ý: dễ bị trả nhầm dòng cùng chủ đề rộng
    case("near_miss", "Bị trượt visa F-1 lần 1 thì sao?", [114, 896, 897, 898], must_not=[899]),
    case(
        "near_miss", "Hạn nộp hồ sơ Early Action là khi nào?", [60, 7, 374]
    ),  # 374: đã đọc tay, có nêu hạn 01/11, 15/11
    case("near_miss", "Học phí đại học công lập ở Mỹ khoảng bao nhiêu?", [935, 510, 5], must_not=[921]),
    case(
        "near_miss", "Phí visa F-1 là bao nhiêu tiền?", [970, 979, 781, 856, 764]
    ),  # 764: đã đọc tay, có nêu 185 USD cho F-1
    case("near_miss", "Điểm IELTS tối thiểu để xin học bổng là bao nhiêu?", [38, 2]),
    # Chỉ gõ không dấu, viết tắt, ngắn
    case("typo", "opt la gi", [366, 235]),
    case("typo", "ielts bao nhieu diem la du", [2, 38]),
    case("typo", "visa f1 phong van co kho ko", [285, 115]),
    case("typo", "toefl 85 diem du chua", [283]),
    case("typo", "i20 la gi", [351, 95]),
    # Hỏi ảnh mẫu: kho chỉ có chữ nên chỉ kiểm tìm ra dòng về I-20; việc có gửi ảnh là việc của bước sau
    case("needs_media", "Cho mình xem mẫu form I-20", [853, 351, 95]),
    case("needs_media", "Form I-20 trông như thế nào", [853, 351, 95]),
    # Ngoài phạm vi: phải từ chối
    case("out_of_scope", "Thời tiết hôm nay thế nào?"),
    case("out_of_scope", "Em muốn du học Canada thì sao?"),
    case("out_of_scope", "Giá vé máy bay đi Mỹ bao nhiêu?"),
    case("out_of_scope", "Giải giúp em bài toán đạo hàm này"),
    case("out_of_scope", "Cách nấu phở bò ngon"),
    case("out_of_scope", "Học bổng du học Úc có những loại nào?"),
    case("out_of_scope", "Em bị đau đầu mất ngủ phải làm sao?"),
    case("out_of_scope", "Điểm chuẩn đại học Bách Khoa Hà Nội năm nay"),
    case("out_of_scope", "Ai là tổng thống Mỹ hiện nay?"),
    case("out_of_scope", "Viết hộ em bài luận Common App"),
]


@dataclass(frozen=True)
class Result:
    case: Case
    hits: list[tuple[int, float]]  # (entry_no, điểm) xếp từ gần nhất

    @property
    def top(self) -> tuple[int, float] | None:
        return self.hits[0] if self.hits else None


def outcome(result: Result, threshold: float) -> str:
    """dung | sai | bo_sot | tu_choi_dung. 'sai' gồm cả trả nhầm dòng và trả lời câu ngoài phạm vi."""
    top = result.top
    answered = top is not None and top[1] >= threshold
    if not result.case.expect:  # ngoài phạm vi
        return "sai" if answered else "tu_choi_dung"
    if not answered:
        return "bo_sot"
    assert top is not None
    return "dung" if top[0] in result.case.expect else "sai"


def summarize(results: list[Result], thresholds: list[float]) -> list[dict]:
    rows = []
    for t in thresholds:
        counts = defaultdict(int)
        for r in results:
            counts[outcome(r, t)] += 1
        answered = counts["dung"] + counts["sai"]
        rows.append(
            {
                "threshold": t,
                "dung": counts["dung"],
                "sai": counts["sai"],
                "bo_sot": counts["bo_sot"],
                "tu_choi_dung": counts["tu_choi_dung"],
                "precision": counts["dung"] / answered if answered else None,
            }
        )
    return rows


def top3_recall(results: list[Result]) -> dict[str, tuple[int, int]]:
    """Theo nhóm: số câu có dòng đúng trong 3 dòng gần nhất / tổng câu có dòng đúng (bỏ qua ngoài phạm vi)."""
    out: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for r in results:
        if not r.case.expect:
            continue
        out[r.case.kind][1] += 1
        if any(entry in r.case.expect for entry, _ in r.hits[:3]):
            out[r.case.kind][0] += 1
    return {k: (v[0], v[1]) for k, v in out.items()}


async def run_cases(cases: list[Case]) -> list[Result]:
    import httpx

    from app.core.config import get_settings
    from app.core.db import open_pool
    from app.services.knowledge import KnowledgeRetriever
    from app.services.ollama import OllamaClient

    settings = get_settings()
    pool = open_pool(settings.database_url)
    if pool is None:
        raise SystemExit("Không kết nối được database (kiểm tra DATABASE_URL trong advisor/.env)")
    results: list[Result] = []
    try:
        async with httpx.AsyncClient(base_url=settings.ollama_base_url, timeout=60) as http:
            llm = OllamaClient(http, settings.llm_model, embedding_model=settings.embedding_model)
            retriever = KnowledgeRetriever(pool, TOP_K)
            for c in cases:
                [vector] = await llm.embed([c.question])
                hits = await retriever.search(vector)
                results.append(Result(c, [(h.entry_no, h.score) for h in hits]))
    finally:
        pool.close()
    return results


def print_report(results: list[Result], show_all: bool, current: float) -> None:
    print(
        f"{len(results)} câu ({sum(1 for r in results if r.case.expect)} có dòng đúng, "
        f"{sum(1 for r in results if not r.case.expect)} ngoài phạm vi)\n"
    )
    print(
        "Ngưỡng | đúng | sai (trả nhầm) | bỏ sót (đáng ra trả lời) | từ chối đúng | độ chính xác khi trả lời"
    )
    for row in summarize(results, THRESHOLDS):
        mark = "  <- hiện tại" if abs(row["threshold"] - current) < 1e-9 else ""
        precision = "-" if row["precision"] is None else f"{row['precision']:.0%}"
        print(
            f"{row['threshold']:.2f}   | {row['dung']:4d} | {row['sai']:14d} | {row['bo_sot']:24d} | {row['tu_choi_dung']:12d} | {precision}{mark}"
        )

    print("\nDòng đúng nằm trong 3 dòng gần nhất (theo nhóm câu):")
    for kind, (ok, total) in sorted(top3_recall(results).items()):
        print(f"  {kind:12s} {ok}/{total}")

    print(f"\nCâu tìm dòng đầu tiên KHÔNG đúng mong đợi (ngưỡng hiện tại {current:.2f}):")
    for r in results:
        top = r.top
        verdict = outcome(r, current)
        if verdict in ("dung", "tu_choi_dung") and not show_all:
            continue
        got = "không có" if top is None else f"id {top[0]} điểm {top[1]:.2f}"
        want = "từ chối" if not r.case.expect else f"id {sorted(r.case.expect)}"
        bad = " [dòng sai chủ đề đã biết]" if top and top[0] in r.case.must_not else ""
        print(
            f"  [{verdict:12s}] ({r.case.kind}) {r.case.question}\n      nhận: {got}{bad}; mong đợi: {want}"
        )


@dataclass(frozen=True)
class FullResult:
    case: Case
    top: tuple[int, float] | None  # dòng gần nhất (entry_no, điểm), chưa chắc là dòng được dùng
    answered: bool  # hệ thống có trả câu trả lời (không phải "chưa có cơ sở" hay "gián đoạn")
    answer: str
    fallback: bool  # Claude sai định dạng hai lần nên hệ thống trả nguyên văn dòng đầu
    used: int | None = (
        None  # entry_no của dòng thật sự được dùng để trả lời (Claude có thể chọn dòng không phải dòng đầu)
    )


def full_outcome(result: FullResult, min_score: float) -> str:
    """dung | sai | tu_choi_dung | bo_sot_nguong (điểm dưới ngưỡng) | bo_sot_claude (Claude nói đoạn không đủ trả lời)."""
    if not result.case.expect:
        return "sai" if result.answered else "tu_choi_dung"
    if result.answered:
        used = result.used if result.used is not None else (result.top[0] if result.top else None)
        return "dung" if used in result.case.expect else "sai"
    below = result.top is None or result.top[1] < min_score
    return "bo_sot_nguong" if below else "bo_sot_claude"


def summarize_full(results: list[FullResult], min_score: float) -> dict[str, int]:
    counts: dict[str, int] = defaultdict(int)
    for r in results:
        counts[full_outcome(r, min_score)] += 1
    counts["fallback"] = sum(1 for r in results if r.fallback)
    return dict(counts)


class _FallbackCounter:
    """Đếm lần TraLoiHandler lùi về câu chuẩn vì Claude sai định dạng hai lần, và ghi dòng Claude chọn khác dòng đầu (qua log)."""

    def __init__(self) -> None:
        import logging
        import re

        # Script không qua setup_logging của app nên log INFO bị bỏ; knowledge_rerank là INFO, cần bật để biết Claude chọn dòng nào
        logging.getLogger("advisor.knowledge").setLevel(logging.INFO)
        self.count = 0
        self.last_chosen: int | None = None
        counter = self

        class Handler(logging.Handler):
            def emit(self, record: logging.LogRecord) -> None:
                message = record.getMessage()
                if "không trả được câu hợp lệ" in message:
                    counter.count += 1
                match = re.match(r"knowledge_answer entry=(\d+)", message)
                if match:
                    counter.last_chosen = int(match.group(1))

        logging.getLogger("advisor.knowledge").addHandler(Handler())


async def run_full(cases: list[Case]) -> list[FullResult]:
    """Đo toàn tuyến: đúng TraLoiHandler của chat (tìm kiếm, ngưỡng, Claude viết lại, kiểm tra), gọi Claude thật."""
    import httpx

    from app.core.config import get_settings
    from app.core.db import open_pool
    from app.schemas.chat import ChatState
    from app.services.accent_restorer import AccentRestorer
    from app.services.claude import ClaudeClient
    from app.services.knowledge import KnowledgeRetriever
    from app.services.ollama import OllamaClient
    from app.services.skills.skill_tra_loi import NO_BASIS, UNAVAILABLE, TraLoiHandler

    settings = get_settings()
    if not settings.anthropic_api_key:
        raise SystemExit("Chưa có ANTHROPIC_API_KEY trong advisor/.env")
    pool = open_pool(settings.database_url)
    if pool is None:
        raise SystemExit("Không kết nối được database (kiểm tra DATABASE_URL trong advisor/.env)")
    results: list[FullResult] = []
    counter = _FallbackCounter()
    try:
        async with (
            httpx.AsyncClient(base_url=settings.ollama_base_url, timeout=60) as ollama_http,
            httpx.AsyncClient(
                base_url=settings.anthropic_base_url, timeout=settings.anthropic_timeout_seconds
            ) as claude_http,
        ):
            ollama = OllamaClient(ollama_http, settings.llm_model, embedding_model=settings.embedding_model)
            claude = ClaudeClient(
                claude_http,
                settings.anthropic_model,
                settings.anthropic_api_key,
                max_retries=settings.anthropic_max_retries,
                fallback_model=settings.anthropic_fallback_model or None,
            )
            # Cùng tham số với chat thật: top_k và ngưỡng lấy từ settings
            retriever = KnowledgeRetriever(pool, settings.knowledge_top_k)
            handler = TraLoiHandler(
                claude,
                settings.anthropic_model,
                retriever,
                settings.knowledge_min_score,
                embedder=ollama,
                restorer=AccentRestorer(claude),
            )
            for c in cases:
                [vector] = await ollama.embed([c.question])
                hits = await retriever.search(vector)
                top = (hits[0].entry_no, hits[0].score) if hits else None
                before = counter.count
                counter.last_chosen = None
                out = await handler.run(ChatState(), c.question)
                answered = out.answer not in (NO_BASIS, UNAVAILABLE)
                used = (counter.last_chosen or (top[0] if top else None)) if answered else None
                results.append(FullResult(c, top, answered, out.answer, counter.count > before, used))
                print(f"  {len(results)}/{len(cases)} {c.question[:50]}", flush=True)
    finally:
        pool.close()
    return results


def print_full_report(results: list[FullResult], min_score: float) -> None:
    counts = summarize_full(results, min_score)
    in_scope = sum(1 for r in results if r.case.expect)
    print(
        f"\nToàn tuyến (có Claude), ngưỡng {min_score:.2f}: {len(results)} câu "
        f"({in_scope} có dòng đúng, {len(results) - in_scope} ngoài phạm vi)"
    )
    labels = [
        ("dung", "Trả lời đúng (dựa trên dòng đúng)"),
        ("sai", "TRẢ LỜI SAI (từ dòng sai, hoặc trả lời câu ngoài phạm vi)"),
        ("tu_choi_dung", "Từ chối đúng (câu ngoài phạm vi)"),
        ("bo_sot_nguong", "Từ chối nhầm vì điểm dưới ngưỡng"),
        ("bo_sot_claude", "Từ chối nhầm vì Claude nói đoạn không đủ trả lời"),
    ]
    for key, label in labels:
        print(f"  {counts.get(key, 0):3d}  {label}")
    print(f"  {counts['fallback']:3d}  (trong đó lùi về câu chuẩn vì Claude sai định dạng hai lần)")

    print("\nChi tiết các câu không phải 'đúng' hoặc 'từ chối đúng':")
    for r in results:
        verdict = full_outcome(r, min_score)
        if verdict in ("dung", "tu_choi_dung"):
            continue
        top = "không có" if r.top is None else f"id {r.top[0]} điểm {r.top[1]:.2f}"
        want = "từ chối" if not r.case.expect else f"id {sorted(r.case.expect)}"
        note = " [lùi về câu chuẩn]" if r.fallback else ""
        used = f"; dòng được dùng: id {r.used}" if r.used is not None else ""
        print(
            f"  [{verdict}] ({r.case.kind}) {r.case.question}{note}\n      dòng đầu: {top}{used}; mong đợi: {want}"
        )
        if r.answered:
            print(f"      trả lời: {r.answer[:600].replace(chr(10), ' ')}")


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="Đo tìm kiếm kho kiến thức")
    parser.add_argument("--all", action="store_true", help="in cả các câu tìm đúng")
    parser.add_argument(
        "--full", action="store_true", help="đo toàn tuyến qua TraLoiHandler, gọi Claude thật (tốn token)"
    )
    args = parser.parse_args()

    from app.core.config import get_settings

    settings = get_settings()
    if args.full:
        print_full_report(asyncio.run(run_full(CASES)), settings.knowledge_min_score)
        return
    results = asyncio.run(run_cases(CASES))
    print_report(results, args.all, settings.knowledge_min_score)


if __name__ == "__main__":
    main()
