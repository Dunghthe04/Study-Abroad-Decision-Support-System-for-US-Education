"""Hỏi kho kiến thức bằng câu kiểu học sinh, xem tìm ra dòng nào và điểm bao nhiêu. Chỉ ĐỌC database, không gọi Claude.

Chạy trong thư mục advisor (cần Postgres và Ollama):  .venv/Scripts/python.exe -m slm.eval.thu_kho_kien_thuc
Điểm đạt ngưỡng chưa có nghĩa là dòng đó trả lời đúng ý: phải đọc câu trả lời. Chat thật còn có bước Claude chọn dòng.
"""

import asyncio
import json
from pathlib import Path

import httpx

from app.core.config import get_settings
from app.core.db import open_pool
from app.services.knowledge import KnowledgeRetriever
from app.services.ollama import OllamaClient

OUT = Path("slm/results/thu_tu_nhien")

CAU_HOI: list[tuple[str, str]] = [
    ("tổng quan", "muốn du học mỹ thì cần những gì ạ"),
    ("tổng quan", "quy trình du học Mỹ gồm những bước nào"),
    ("tổng quan", "nên chuẩn bị du học từ lớp mấy"),
    ("chi phí", "du học mỹ tốn khoảng bao nhiêu tiền 1 năm"),
    ("chi phí", "ngoài học phí ra còn phải trả những khoản gì nữa"),
    ("chi phí", "tiền ăn ở bên mỹ một tháng khoảng bao nhiêu"),
    ("chi phí", "làm sao để du học mỹ cho tiết kiệm"),
    ("học bổng", "học bổng toàn phần có thật không hay chỉ quảng cáo"),
    ("học bổng", "xin học bổng du học mỹ như thế nào"),
    ("học bổng", "gpa bao nhiêu thì xin được học bổng"),
    ("học bổng", "need-based aid là gì"),
    ("học bổng", "CSS Profile là cái gì"),
    ("visa", "visa f1 phỏng vấn người ta hay hỏi gì"),
    ("visa", "xin visa du học mỹ cần giấy tờ gì"),
    ("visa", "làm visa mất bao lâu"),
    ("visa", "bị rớt visa thì làm sao"),
    ("visa", "chứng minh tài chính cần bao nhiêu"),
    ("visa", "nhà không có sổ tiết kiệm lớn thì có đi được không"),
    ("làm thêm", "sang đó có được đi làm thêm không ạ"),
    ("làm thêm", "học xong có được ở lại mỹ làm việc không"),
    ("làm thêm", "OPT là gì"),
    ("bậc học", "community college là sao, học xong chuyển lên đại học được hả"),
    ("bậc học", "chương trình 2+2 là gì"),
    ("bậc học", "pathway khác gì học thẳng đại học"),
    ("thi cử", "sat với act thi cái nào dễ hơn"),
    ("thi cử", "ielts bao nhiêu thì đi du học mỹ được"),
    ("thi cử", "không có ielts thì nộp được không"),
    ("thi cử", "duolingo có được trường mỹ chấp nhận không"),
    ("thi cử", "một năm thi sat mấy đợt"),
    ("thi cử", "test optional nghĩa là gì"),
    ("hồ sơ", "hồ sơ nộp đại học mỹ gồm những gì"),
    ("hồ sơ", "bài luận nên viết về cái gì cho hay"),
    ("hồ sơ", "thư giới thiệu nên xin ai"),
    ("hồ sơ", "không có hoạt động ngoại khóa thì sao"),
    ("hồ sơ", "gpa việt nam đổi sang thang 4 kiểu gì"),
    ("hạn nộp", "ED với EA khác nhau chỗ nào"),
    ("hạn nộp", "hạn nộp hồ sơ kỳ fall thường là khi nào"),
    ("hạn nộp", "kỳ spring khác kỳ fall chỗ nào"),
    ("chọn trường", "nên chọn trường như thế nào cho phù hợp"),
    ("chọn trường", "nên nộp bao nhiêu trường"),
    ("chọn trường", "trường reach match safety là gì"),
    ("chọn ngành", "chưa biết học ngành gì thì làm sao"),
    ("chọn ngành", "ngành STEM là gì"),
    ("sau trúng tuyển", "đậu rồi thì làm gì tiếp theo"),
    ("sau trúng tuyển", "I-20 là gì"),
    ("đời sống", "ở ký túc xá hay thuê nhà ngoài thì tốt hơn"),
    ("đời sống", "bên mỹ có an toàn không ạ"),
    ("đời sống", "dưới 18 tuổi đi du học cần người giám hộ không"),
    ("đời sống", "qua mỹ cần mang theo những gì"),
    ("phụ huynh", "phụ huynh cần chuẩn bị gì cho con đi du học"),
    ("trung tâm", "có nên làm hồ sơ qua trung tâm không hay tự làm"),
]


async def main() -> None:
    s = get_settings()
    pool = open_pool(s.database_url)
    if pool is None:
        raise SystemExit("Không kết nối được database (kiểm tra DATABASE_URL trong advisor/.env)")
    try:
        async with httpx.AsyncClient(
            base_url=s.embedding_base_url or s.ollama_base_url, timeout=s.llm_timeout_seconds
        ) as http:
            embedder = OllamaClient(http, s.slm_model, embedding_model=s.embedding_model)
            retriever = KnowledgeRetriever(pool, s.knowledge_top_k)
            rows = []
            for group, question in CAU_HOI:
                [vector] = await embedder.embed([question])
                hits = await retriever.search(vector)
                rows.append({
                    "nhom": group, "cau": question,
                    "hits": [{"id": h.entry_no, "diem": round(h.score, 3), "cau_mau": h.question, "tra_loi": h.answer}
                             for h in hits],
                })
    finally:
        pool.close()

    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "kho_kien_thuc.jsonl").write_text(
        "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8"
    )
    below = sum(r["hits"][0]["diem"] < s.knowledge_min_score for r in rows)
    print(f"{len(rows)} câu, {below} câu dòng đầu dưới ngưỡng {s.knowledge_min_score}")
    for r in rows:
        flag = "" if r["hits"][0]["diem"] >= s.knowledge_min_score else "  << DƯỚI NGƯỠNG"
        print(f"\n## {r['cau']}{flag}")
        for h in r["hits"][:3]:
            print(f"  [{h['diem']}] #{h['id']} {h['cau_mau']}\n      {h['tra_loi'][:230]!r}")


if __name__ == "__main__":
    asyncio.run(main())
