"""Dịch vụ OCR đọc bảng điểm (học bạ THPT/ĐH đa tệp) qua Ollama Vision / LLM."""

from __future__ import annotations

import base64
import io
import json
import logging
from typing import Any

import httpx
import pypdf

from app.core.config import Settings
from app.schemas.transcript_ocr import (
    TranscriptFilePayload,
    TranscriptOcrRequest,
    TranscriptOcrResponse,
    TranscriptSubjectScore,
    TranscriptTerm,
)

logger = logging.getLogger(__name__)

OCR_MULTI_IMAGE_PROMPT = """Bạn là chuyên gia AI nhận diện và trích xuất bảng điểm học bạ (học sinh THPT hoặc sinh viên Đại học tại Việt Nam).
Nhiệm vụ của bạn là đọc hình ảnh/trang học bạ này và bóc tách ĐẦY ĐỦ:
1. Học kỳ và Khối lớp xuất hiện trong ảnh (vd: "Lớp 10 HK1", "Lớp 10 HK2", "Lớp 11 HK1", "Lớp 11 HK2", "Lớp 12 HK1", hoặc "Học kỳ 1 Năm 1"...).
2. TẤT CẢ các môn học và điểm số tương ứng có trong bảng.

Định dạng JSON yêu cầu:
{
  "terms": [
    {
      "termName": "Lớp 10 HK1",
      "scores": [
        { "subject": "Toán", "score": 8.5 },
        { "subject": "Ngữ văn", "score": 7.8 }
      ]
    }
  ]
}

Quy tắc bóc tách bắt buộc:
1. Xác định CHÍNH XÁC học kỳ và khối lớp từ tiêu đề, đầu trang hoặc cột của bảng điểm (ví dụ: "HỌC KỲ I - LỚP 10" -> "Lớp 10 HK1"; "HỌC KỲ II - LỚP 11" -> "Lớp 11 HK2"). Nếu ảnh có cả 2 học kỳ hoặc cả năm, hãy tách thành các phần tử riêng trong mảng "terms".
2. Đọc TẤT CẢ các môn học xuất hiện trong bảng (Toán, Văn, Ngoại ngữ, Vật lí, Hóa học, Sinh học, Lịch sử, Địa lí, Tin học, GDCD, Công nghệ, GDQP, Kinh tế & Pháp luật, hoặc các môn chuyên ngành Đại học). Giữ nguyên tên môn thực tế, KHÔNG tự ý bỏ sót bất kỳ môn nào.
3. Lấy điểm trung bình môn của kỳ (ĐTBmhk) hoặc điểm tổng kết môn. Điểm là số thực từ 0.0 đến 10.0.
4. Nếu ảnh quá mờ, không có bảng điểm, hoặc không nhận diện được chữ nào, trả về JSON với "terms": [].
5. Chỉ trả về chuỗi JSON thuần túy, tuyệt đối KHÔNG viết thêm lời giải thích nào bên ngoài.
"""


class TranscriptReader:
    def __init__(self, http: httpx.AsyncClient, settings: Settings) -> None:
        self._http = http
        self._settings = settings

    async def read_transcript(self, request: TranscriptOcrRequest) -> TranscriptOcrResponse:
        """Đọc và bóc tách bảng điểm từ danh sách nhiều ảnh hoặc nhiều tệp PDF."""
        # 1. Thu thập danh sách các tệp cần xử lý
        files_to_process: list[TranscriptFilePayload] = []
        if request.files and len(request.files) > 0:
            files_to_process.extend(request.files)
        elif request.image_base64:
            files_to_process.append(
                TranscriptFilePayload(
                    image_base64=request.image_base64,
                    file_name=request.file_name or "uploaded_file",
                    content_type=request.content_type,
                )
            )

        if not files_to_process:
            return TranscriptOcrResponse(
                success=False,
                message="Không có tệp bảng điểm nào được tải lên.",
                warnings=["Vui lòng chọn ít nhất một ảnh hoặc tệp PDF bảng điểm để xử lý."],
            )

        collected_terms_map: dict[str, dict[str, float]] = {}
        warnings: list[str] = []
        model_used: str | None = None
        processed_files_count = 0

        # 2. Lặp qua từng tệp để OCR bóc tách
        for idx, file_item in enumerate(files_to_process, 1):
            file_name = file_item.file_name or f"Tệp {idx}"
            raw_base64 = file_item.image_base64.strip()
            if "," in raw_base64:
                raw_base64 = raw_base64.split(",", 1)[1].strip()

            file_bytes = b""
            try:
                file_bytes = base64.b64decode(raw_base64)
            except Exception:
                warnings.append(f"Tệp '{file_name}': Định dạng base64 không hợp lệ.")
                continue

            # Kiểm tra nếu là file PDF
            is_pdf = (
                (file_item.content_type and "pdf" in file_item.content_type.lower())
                or (file_item.file_name and file_item.file_name.lower().endswith(".pdf"))
                or file_bytes.startswith(b"%PDF")
            )

            file_terms: list[TranscriptTerm] = []

            if is_pdf and file_bytes:
                # Xử lý PDF (thử đọc text trước, nếu không có text thì trích xuất ảnh từng trang)
                pdf_terms, pdf_model, pdf_warn = await self._process_pdf(file_bytes, file_name)
                if pdf_terms:
                    file_terms.extend(pdf_terms)
                    model_used = pdf_model or model_used
                if pdf_warn:
                    warnings.append(pdf_warn)
            else:
                # Xử lý ảnh trực tiếp qua Ollama Vision
                img_terms, img_model, img_warn = await self._process_single_image(raw_base64, file_name)
                if img_terms:
                    file_terms.extend(img_terms)
                    model_used = img_model or model_used
                if img_warn:
                    warnings.append(img_warn)

            if not file_terms:
                if not any(file_name in w for w in warnings):
                    warnings.append(
                        f"Tệp '{file_name}': Không nhận diện được học kỳ hoặc điểm môn học nào. Ảnh có thể bị mờ hoặc không phải học bạ."
                    )
            else:
                processed_files_count += 1
                # Gộp điểm vào terms map
                for t in file_terms:
                    norm_name = self._normalize_term_name(t.term_name)
                    if norm_name not in collected_terms_map:
                        collected_terms_map[norm_name] = {}
                    for s in t.scores:
                        if s.subject and s.score is not None:
                            collected_terms_map[norm_name][s.subject.strip()] = s.score

        # 3. Kiểm tra kết quả tổng hợp
        if not collected_terms_map:
            return TranscriptOcrResponse(
                success=False,
                model_used=model_used,
                terms=[],
                calculated_gpa4=None,
                average_score_scale10=None,
                message="Không thể nhận diện được điểm số từ các tệp đã tải lên. Vui lòng kiểm tra lại ảnh chụp hoặc tự nhập tay điểm vào bảng.",
                warnings=warnings,
            )

        # 4. Sắp xếp các học kỳ theo thứ tự chuẩn thời gian
        sorted_term_names = sorted(collected_terms_map.keys(), key=self._term_sort_key)
        final_terms: list[TranscriptTerm] = []
        all_subject_scores: list[float] = []

        for order, term_name in enumerate(sorted_term_names, 1):
            subjects_dict = collected_terms_map[term_name]
            score_items: list[TranscriptSubjectScore] = []
            for subj, sc in subjects_dict.items():
                score_items.append(
                    TranscriptSubjectScore(
                        subject=subj,
                        score=round(sc, 2),
                        raw_score=str(sc),
                        credits=1.0,
                    )
                )
                all_subject_scores.append(sc)

            final_terms.append(
                TranscriptTerm(
                    term_order=order,
                    term_name=term_name,
                    scores=score_items,
                )
            )

        # 5. Quy đổi chuẩn GPA: Tính điểm trung bình tất cả các môn / 10 * 4
        avg_score_10 = round(sum(all_subject_scores) / len(all_subject_scores), 2) if all_subject_scores else 0.0
        gpa4 = round((avg_score_10 / 10.0) * 4.0, 2)

        return TranscriptOcrResponse(
            success=True,
            model_used=model_used or "ollama_vision",
            terms=final_terms,
            average_score_scale10=avg_score_10,
            calculated_gpa4=gpa4,
            message=(
                f"Đã đọc và bóc tách thành công {len(final_terms)} học kỳ với {len(all_subject_scores)} lượt điểm môn học "
                f"từ {processed_files_count}/{len(files_to_process)} tệp."
            ),
            warnings=warnings,
        )

    async def _process_single_image(
        self, image_base64: str, file_name: str
    ) -> tuple[list[TranscriptTerm], str | None, str | None]:
        """Gửi 1 ảnh sang Ollama Vision."""
        available_models = await self._get_available_ollama_models()
        target_model = self._settings.vision_model

        matched_model = None
        for m in available_models:
            if target_model in m or "vision" in m or "llava" in m or "minicpm" in m:
                matched_model = m
                break

        if not matched_model:
            return [], None, f"Tệp '{file_name}': Mô hình Vision ({target_model}) chưa sẵn sàng trên Ollama."

        payload = {
            "model": matched_model,
            "messages": [
                {
                    "role": "user",
                    "content": OCR_MULTI_IMAGE_PROMPT,
                    "images": [image_base64],
                }
            ],
            "format": "json",
            "stream": False,
            "options": {"temperature": 0.0},
        }

        try:
            resp = await self._http.post(
                "/api/chat",
                json=payload,
                timeout=self._settings.vision_timeout_seconds,
            )
            resp.raise_for_status()
            data = resp.json()
            content = data.get("message", {}).get("content", "")
            terms = self._parse_json_to_terms(content)
            return terms, matched_model, None
        except Exception as ex:
            logger.warning("Lỗi gọi Vision model %s cho tệp %s: %s", matched_model, file_name, ex)
            return [], matched_model, f"Tệp '{file_name}': Lỗi phân tích ảnh ({ex})."

    async def _process_pdf(
        self, pdf_bytes: bytes, file_name: str
    ) -> tuple[list[TranscriptTerm], str | None, str | None]:
        """Xử lý tệp PDF: Đọc text hoặc trích xuất từng ảnh của các trang."""
        try:
            reader = pypdf.PdfReader(io.BytesIO(pdf_bytes))
            extracted_text = ""
            for page in reader.pages:
                t = page.extract_text()
                if t:
                    extracted_text += t + "\n"

            # Nếu PDF chứa văn bản điện tử
            if len(extracted_text.strip()) > 50:
                prompt = (
                    f"Dưới đây là văn bản trích xuất từ tệp học bạ PDF:\n\n{extracted_text[:6000]}\n\n"
                    f"{OCR_MULTI_IMAGE_PROMPT}"
                )
                payload = {
                    "model": self._settings.llm_model,
                    "messages": [{"role": "user", "content": prompt}],
                    "format": "json",
                    "stream": False,
                    "options": {"temperature": 0.0},
                }
                resp = await self._http.post("/api/chat", json=payload, timeout=self._settings.llm_timeout_seconds)
                resp.raise_for_status()
                content = resp.json().get("message", {}).get("content", "")
                terms = self._parse_json_to_terms(content)
                if terms:
                    return terms, self._settings.llm_model, None

            # Nếu PDF là dạng scan, thử lấy các ảnh nhúng bên trong
            collected_terms: list[TranscriptTerm] = []
            used_model: str | None = None
            for page_idx, page in enumerate(reader.pages, 1):
                if hasattr(page, "images") and len(page.images) > 0:
                    for img in page.images:
                        img_b64 = base64.b64encode(img.data).decode("utf-8")
                        t_list, m_name, _ = await self._process_single_image(img_b64, f"{file_name} - Trang {page_idx}")
                        if t_list:
                            collected_terms.extend(t_list)
                            used_model = m_name

            if collected_terms:
                return collected_terms, used_model, None

        except Exception as ex:
            logger.warning("Lỗi xử lý PDF %s: %s", file_name, ex)
            return [], None, f"Tệp '{file_name}': Không thể đọc định dạng PDF ({ex})."

        return [], None, None

    async def _get_available_ollama_models(self) -> list[str]:
        """Lấy danh sách các mô hình hiện có trong Ollama."""
        try:
            resp = await self._http.get("/api/tags", timeout=5.0)
            if resp.status_code == 200:
                data = resp.json()
                return [m.get("name", "") for m in data.get("models", [])]
        except Exception:
            pass
        return []

    def _parse_json_to_terms(self, json_str: str) -> list[TranscriptTerm]:
        """Parse chuỗi JSON trả về từ LLM/Vision thành danh sách TranscriptTerm."""
        try:
            data = json.loads(json_str)
            raw_terms = data.get("terms", [])
            if not isinstance(raw_terms, list):
                return []

            terms: list[TranscriptTerm] = []
            for t in raw_terms:
                term_name = str(t.get("termName", "")).strip()
                if not term_name:
                    continue
                raw_scores = t.get("scores", [])
                score_items: list[TranscriptSubjectScore] = []
                if isinstance(raw_scores, list):
                    for s in raw_scores:
                        subj = str(s.get("subject", "")).strip()
                        if not subj:
                            continue
                        try:
                            sc = float(s.get("score", 0.0))
                            sc = max(0.0, min(10.0, sc))
                            score_items.append(
                                TranscriptSubjectScore(
                                    subject=subj,
                                    score=round(sc, 2),
                                    raw_score=str(s.get("score", sc)),
                                    credits=1.0,
                                )
                            )
                        except (ValueError, TypeError):
                            continue

                if score_items:
                    terms.append(TranscriptTerm(term_order=len(terms) + 1, term_name=term_name, scores=score_items))

            return terms
        except Exception as ex:
            logger.warning("Không thể parse JSON: %s. Chuỗi: %s", ex, json_str[:200])
            return []

    @staticmethod
    def _normalize_term_name(term_name: str) -> str:
        """Chuẩn hóa tên học kỳ (ví dụ: 'lop 10 hk1' -> 'Lớp 10 HK1')."""
        t = term_name.strip()
        lower = t.lower()
        if "10" in lower:
            grade = "Lớp 10"
        elif "11" in lower:
            grade = "Lớp 11"
        elif "12" in lower:
            grade = "Lớp 12"
        elif "năm 1" in lower or "nam 1" in lower:
            grade = "Năm 1"
        elif "năm 2" in lower or "nam 2" in lower:
            grade = "Năm 2"
        else:
            grade = t

        if "hk1" in lower or "kỳ 1" in lower or "học kỳ 1" in lower or "hk 1" in lower:
            term = "HK1"
        elif "hk2" in lower or "kỳ 2" in lower or "học kỳ 2" in lower or "hk 2" in lower:
            term = "HK2"
        elif "cả năm" in lower or "ca nam" in lower or "tổng kết" in lower:
            term = "Cả năm"
        else:
            term = ""

        if grade != t and term:
            return f"{grade} {term}"
        return t

    @staticmethod
    def _term_sort_key(term_name: str) -> tuple[int, int]:
        """Sắp xếp học kỳ theo thứ tự thời gian (Lớp 10 HK1 trước, Lớp 12 sau)."""
        lower = term_name.lower()
        grade_order = 99
        if "10" in lower:
            grade_order = 10
        elif "11" in lower:
            grade_order = 11
        elif "12" in lower:
            grade_order = 12
        elif "năm 1" in lower:
            grade_order = 13
        elif "năm 2" in lower:
            grade_order = 14

        term_order = 3
        if "hk1" in lower or "kỳ 1" in lower or "học kỳ 1" in lower:
            term_order = 1
        elif "hk2" in lower or "kỳ 2" in lower or "học kỳ 2" in lower:
            term_order = 2
        elif "cả năm" in lower:
            term_order = 3

        return (grade_order, term_order)
