"""Đọc bảng điểm (học bạ THPT / bảng điểm ĐH, nhiều ảnh hoặc PDF) qua Ollama.

Quy trình 2 bước, vì model nhỏ vừa đọc ảnh vừa suy luận ra JSON thì rất dễ sai khi đổi bố cục:
- Bước A "chép": model đọc ảnh (vision_model) chép nguyên văn ảnh thành Markdown. PDF có chữ thì bỏ qua bước này.
- Bước B "hiểu": LLM chữ (transcript_parse_model, để trống thì dùng llm_model) đọc Markdown, trả JSON theo schema (structured output).
Sau đó code hậu kiểm: điểm phải có thật trong văn bản bước A và nằm trong 0–10, rồi chuẩn hoá tên môn / tên kỳ.
"""

from __future__ import annotations

import base64
import io
import json
import logging
import math
import re
import unicodedata
from dataclasses import dataclass, field
from typing import Any

import httpx
import pypdf
from PIL import Image, ImageOps

from app.core.config import Settings
from app.schemas.transcript_ocr import (
    TranscriptFilePayload,
    TranscriptOcrRequest,
    TranscriptOcrResponse,
    TranscriptSubjectScore,
    TranscriptTerm,
)
from app.services.transcript_text import read_term_tables, tidy_transcription

logger = logging.getLogger(__name__)

# Model đọc ảnh dự phòng khi chưa tải đúng vision_model, ưu tiên từ trái sang phải
VISION_MODEL_PRIORITY = ("glm-ocr", "qwen3-vl", "qwen2.5vl", "minicpm-v", "llava")

# glm-ocr chỉ nhận các lệnh cố định ("Text Recognition:", "Table Recognition:"...)
GLM_OCR_PROMPT = "Text Recognition:"
TRANSCRIBE_PROMPT = (
    "Transcribe everything in this image into Markdown. "
    "Render every table as a Markdown table and keep every row, every column header and every section heading "
    '(for example "Lớp 10", "HK1", "Cả năm", "Fall2023"). '
    "Copy every number exactly as written, including decimal commas. "
    "Do not summarize, compute, translate or explain anything."
)

PARSE_PROMPT = """Bạn nhận văn bản được chép lại từ ảnh hoặc PDF bảng điểm (học bạ THPT hoặc bảng điểm đại học ở Việt Nam).
Hãy trích điểm tổng kết từng môn theo từng học kỳ.

Quy tắc:
1. "score" chép NGUYÊN VĂN ô điểm trong văn bản (giữ dấu phẩy nếu có, VD "8,2"). Không tự tính, không đoán, không làm tròn. Ô trống thì bỏ môn đó.
2. Mỗi học kỳ là một phần tử của "terms":
   - "grade": khối lớp hoặc năm học như ghi trong văn bản, VD "Lớp 10", "Năm 2", "2023-2024". Không có thì để "".
   - "term": tên kỳ nguyên văn, VD "HK1", "Học kỳ II", "Fall2023", "Summer2024".
3. Học bạ THPT thường chia khối "Lớp 10", "Lớp 11", "Lớp 12"; mỗi khối có dòng HK1, HK2, Cả năm và môn học nằm ở các cột.
   Mỗi dòng HK1 / HK2 là một kỳ riêng, "grade" là lớp của khối chứa dòng đó.
4. Bảng điểm đại học thường mỗi dòng một môn và có cột hoặc tiêu đề kỳ học. Gom các môn cùng kỳ vào một phần tử.
   Có cả điểm thang 10, thang 4 và điểm chữ thì lấy điểm thang 10.
5. BỎ QUA: dòng "Cả năm" / tổng kết năm, cột TB / ĐTB / TBM / GPA, học lực, hạnh kiểm, xếp loại, số tín chỉ, mã môn,
   môn chấm Đ / CĐ (đạt / chưa đạt), điểm thành phần (chuyên cần, giữa kỳ) khi đã có điểm tổng kết môn.
6. "scale": "10" nếu điểm theo thang 10, "4" nếu chỉ có thang 4, "letter" nếu chỉ có điểm chữ (A, B+...), "other" nếu khác.
7. "level": "thpt" nếu là học bạ / kết quả học tập phổ thông (trường THPT, Sở GD&ĐT, Kỳ 1 / Kỳ 2 kèm các môn Toán, Văn, Lý...),
   "dai_hoc" nếu là bảng điểm đại học / cao đẳng, "unknown" nếu không rõ.
8. Văn bản không phải bảng điểm thì trả {"scale": "other", "level": "unknown", "terms": []}.

Văn bản:
"""

PARSE_SCHEMA: dict[str, Any] = {
    "type": "object",
    "properties": {
        "scale": {"type": "string", "enum": ["10", "4", "letter", "other"]},
        "level": {"type": "string", "enum": ["thpt", "dai_hoc", "unknown"]},
        "terms": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "grade": {"type": "string"},
                    "term": {"type": "string"},
                    "subjects": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {"name": {"type": "string"}, "score": {"type": "string"}},
                            "required": ["name", "score"],
                        },
                    },
                },
                "required": ["grade", "term", "subjects"],
            },
        },
    },
    "required": ["scale", "level", "terms"],
}

# Văn bản chép từ PDF / ảnh dài quá thì cắt, tránh vượt ngữ cảnh của model bước B
MAX_SOURCE_CHARS = 12000
MAX_PDF_PAGES = 10
MIN_IMAGE_SIDE = 1200

FRIENDLY_AI_BUSY = (
    "AI chưa đọc được (máy chủ AI đang bận hoặc thiếu bộ nhớ). Bạn thử lại sau ít phút hoặc nhập điểm tay."
)

SUBJECT_MAP: dict[str, str] = {
    "toán": "Toán",
    "toan": "Toán",
    "toán học": "Toán",
    "đại số": "Toán",
    "hình học": "Toán",
    "văn": "Ngữ văn",
    "van": "Ngữ văn",
    "ngữ văn": "Ngữ văn",
    "ngu van": "Ngữ văn",
    "tiếng việt": "Ngữ văn",
    "lý": "Vật lý",
    "ly": "Vật lý",
    "lí": "Vật lý",
    "vật lý": "Vật lý",
    "vat ly": "Vật lý",
    "vật lí": "Vật lý",
    "vat li": "Vật lý",
    "hóa": "Hóa học",
    "hoá": "Hóa học",
    "hoa": "Hóa học",
    "hóa học": "Hóa học",
    "hoá học": "Hóa học",
    "hoa hoc": "Hóa học",
    "sinh": "Sinh học",
    "sinh học": "Sinh học",
    "sinh hoc": "Sinh học",
    "sử": "Lịch sử",
    "su": "Lịch sử",
    "lịch sử": "Lịch sử",
    "lich su": "Lịch sử",
    "địa": "Địa lý",
    "dia": "Địa lý",
    "địa lý": "Địa lý",
    "dia ly": "Địa lý",
    "địa lí": "Địa lý",
    "dia li": "Địa lý",
    "ngoại ngữ": "Tiếng Anh",
    "ngoai ngu": "Tiếng Anh",
    "tiếng anh": "Tiếng Anh",
    "tieng anh": "Tiếng Anh",
    "anh": "Tiếng Anh",
    "anh văn": "Tiếng Anh",
    "anh van": "Tiếng Anh",
    "tin": "Tin học",
    "tin học": "Tin học",
    "tin hoc": "Tin học",
    "công nghệ": "Công nghệ",
    "cong nghe": "Công nghệ",
    "gdcd": "GDCD",
    "giáo dục công dân": "GDCD",
    "giao duc cong dan": "GDCD",
    "gdqp": "GDQP",
    "gdqp-an": "GDQP",
    "giáo dục quốc phòng": "GDQP",
    "giao duc quoc phong": "GDQP",
    "quốc phòng": "GDQP",
    "quoc phong": "GDQP",
    "gd cd": "GDCD",
    "gd qp": "GDQP",
    # OCR hay đọc nhầm chữ "Đ" có gạch thành "Dj"
    "dja": "Địa lý",
    "dja ly": "Địa lý",
    "dja li": "Địa lý",
    # Ánh xạ tên tiếng Anh (bảng điểm song ngữ hoặc model trả về tên tiếng Anh)
    "math": "Toán",
    "maths": "Toán",
    "mathematics": "Toán",
    "physics": "Vật lý",
    "chemistry": "Hóa học",
    "biology": "Sinh học",
    "literature": "Ngữ văn",
    "vietnamese literature": "Ngữ văn",
    "history": "Lịch sử",
    "geography": "Địa lý",
    "english": "Tiếng Anh",
    "foreign language": "Tiếng Anh",
    "technology": "Công nghệ",
    "computer": "Tin học",
    "informatics": "Tin học",
    "information technology": "Tin học",
    "civic education": "GDCD",
    "defense education": "GDQP",
    "national defense": "GDQP",
}

IGNORED_SUBJECTS: set[str] = {
    "thể dục",
    "the duc",
    "td",
    "pe",
    "physical education",
    "tb môn",
    "tb mon",
    "đtb",
    "dtb",
    "điểm tb",
    "diem tb",
    "tb",
    "tbm",
    "gpa",
    "average",
    "học lực",
    "hoc luc",
    "hl",
    "hạnh kiểm",
    "hanh kiem",
    "hk",
    "conduct",
    "xếp loại",
    "xep loai",
    "rank",
    "danh hiệu",
    "danh hieu",
}

_NUMBER_RE = re.compile(r"\d+(?:[.,]\d+)?")
_ACADEMIC_YEAR_RE = re.compile(r"(20\d\d)\s*[-–/]\s*(20\d\d)")
_YEAR_RE = re.compile(r"(?<!\d)(20\d\d)(?!\d)")
_SEMESTER_RE = re.compile(r"\b(?:hk|hoc ky|ky|semester|sem|term)\s*([1-9]|iii|ii|i)\b")
# "Lớp 11", "Lớp: 11A1", "khối 10"
_SCHOOL_GRADE_RE = re.compile(r"\b(?:lop|grade|class|khoi)[\s:.]*(6|7|8|9|10|11|12)(?!\d)")
_UNI_YEAR_RE = re.compile(r"\b(?:nam|year)\s*(?:thu\s*)?([1-6])\b")
_ROMAN = {"i": 1, "ii": 2, "iii": 3}


class OllamaUnavailableError(Exception):
    """Gọi Ollama thất bại. Chi tiết kỹ thuật chỉ ghi log, người dùng nhận câu tiếng Việt dễ hiểu."""


@dataclass
class _RawTerm:
    """Một kỳ đọc được từ 1 tệp, chưa gán "Năm n" (kỳ ĐH ghi theo năm học cần biết năm học sớm nhất của mọi tệp)."""

    label: str
    scores: dict[str, float]
    grade_name: str | None = None
    semester: int | None = None
    academic_year: int | None = None
    high_school: bool = False


@dataclass
class _FileResult:
    terms: list[_RawTerm] = field(default_factory=list)
    model: str | None = None
    warnings: list[str] = field(default_factory=list)


class TranscriptReader:
    def __init__(self, http: httpx.AsyncClient, settings: Settings) -> None:
        self._http = http
        self._settings = settings

    async def read_transcript(self, request: TranscriptOcrRequest) -> TranscriptOcrResponse:
        """Đọc và bóc tách bảng điểm từ danh sách nhiều ảnh hoặc nhiều tệp PDF."""
        files_to_process: list[TranscriptFilePayload] = list(request.files or [])
        if not files_to_process and request.image_base64:
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

        raw_terms: list[_RawTerm] = []
        warnings: list[str] = []
        models_used: list[str] = []
        processed_files_count = 0

        for idx, file_item in enumerate(files_to_process, 1):
            file_name = file_item.file_name or f"Tệp {idx}"
            result = await self._read_file(file_item, file_name)
            warnings.extend(result.warnings)
            if result.model and result.model not in models_used:
                models_used.append(result.model)
            if result.terms:
                processed_files_count += 1
                raw_terms.extend(result.terms)
            elif not result.warnings:
                warnings.append(
                    f"Tệp '{file_name}': không thấy học kỳ hoặc điểm môn nào. Ảnh có thể bị mờ, bị cắt hoặc không phải bảng điểm."
                )

        if any(t.high_school and t.grade_name is None and t.academic_year is not None for t in raw_terms):
            warnings.append(
                "Học bạ không ghi rõ lớp nên hệ thống tạm xếp năm học sớm nhất là Lớp 10. Bạn kiểm tra lại tên kỳ trong bảng giúp nhé."
            )
        collected = self._merge_terms(raw_terms)
        model_used = " + ".join(models_used) or None

        if not collected:
            return TranscriptOcrResponse(
                success=False,
                model_used=model_used,
                message="Chưa đọc được điểm từ các tệp đã tải lên. Bạn kiểm tra lại ảnh chụp hoặc nhập điểm tay vào bảng.",
                warnings=warnings,
            )

        final_terms: list[TranscriptTerm] = []
        all_scores: list[float] = []
        for order, term_name in enumerate(sorted(collected, key=self._term_sort_key), 1):
            score_items = [
                TranscriptSubjectScore(subject=subj, score=round(sc, 2), raw_score=str(sc), credits=1.0)
                for subj, sc in collected[term_name].items()
            ]
            all_scores.extend(collected[term_name].values())
            final_terms.append(TranscriptTerm(term_order=order, term_name=term_name, scores=score_items))

        # Quy đổi GPA: trung bình tất cả các điểm môn / 10 * 4
        avg_score_10 = round(sum(all_scores) / len(all_scores), 2)
        gpa4 = round((avg_score_10 / 10.0) * 4.0, 2)

        return TranscriptOcrResponse(
            success=True,
            model_used=model_used,
            terms=final_terms,
            average_score_scale10=avg_score_10,
            calculated_gpa4=gpa4,
            message=(
                f"Đã đọc {len(final_terms)} học kỳ với {len(all_scores)} điểm môn "
                f"từ {processed_files_count}/{len(files_to_process)} tệp. Bạn kiểm tra lại bảng trước khi đi tiếp."
            ),
            warnings=warnings,
        )

    # ---------- Đọc từng tệp ----------

    async def _read_file(self, file_item: TranscriptFilePayload, file_name: str) -> _FileResult:
        raw_base64 = file_item.image_base64.strip()
        if "," in raw_base64:
            raw_base64 = raw_base64.split(",", 1)[1].strip()
        try:
            file_bytes = base64.b64decode(raw_base64)
        except Exception:
            return _FileResult(warnings=[f"Tệp '{file_name}': dữ liệu tệp bị hỏng, bạn tải lại giúp nhé."])

        # Nhận diện định dạng qua magic bytes (tránh ảnh bị đặt đuôi .pdf và ngược lại)
        if file_bytes.startswith(b"%PDF"):
            return await self._read_pdf(file_bytes, file_name)
        if not self._looks_like_image(file_bytes) and self._declared_pdf(file_item):
            return await self._read_pdf(file_bytes, file_name)
        return await self._read_images([file_bytes], file_name)

    @staticmethod
    def _looks_like_image(data: bytes) -> bool:
        return (
            data.startswith(b"\xff\xd8")  # JPEG
            or data.startswith(b"\x89PNG")
            or data.startswith(b"GIF8")
            or data.startswith(b"BM")
            or (data.startswith(b"RIFF") and b"WEBP" in data[:12])
        )

    @staticmethod
    def _declared_pdf(file_item: TranscriptFilePayload) -> bool:
        return bool(
            (file_item.content_type and "pdf" in file_item.content_type.lower())
            or (file_item.file_name and file_item.file_name.lower().endswith(".pdf"))
        )

    async def _read_pdf(self, pdf_bytes: bytes, file_name: str) -> _FileResult:
        """PDF có chữ: đưa thẳng chữ sang bước B. PDF scan: render từng trang thành ảnh rồi đi bước A."""
        try:
            reader = pypdf.PdfReader(io.BytesIO(pdf_bytes))
            text = "\n".join(page.extract_text() or "" for page in reader.pages[:MAX_PDF_PAGES])
        except Exception as ex:
            logger.warning("Không đọc được chữ trong PDF %s: %s", file_name, ex)
            text = ""

        if len(text.strip()) > 50:
            return await self._parse_source(text, file_name, model_used=None)

        try:
            page_images = self._render_pdf_pages(pdf_bytes)
        except Exception as ex:
            logger.warning("Không render được PDF %s: %s", file_name, ex)
            return _FileResult(
                warnings=[
                    f"Tệp '{file_name}': không mở được tệp PDF. Bạn thử xuất lại PDF hoặc chụp ảnh bảng điểm."
                ]
            )
        if not page_images:
            return _FileResult(warnings=[f"Tệp '{file_name}': tệp PDF không có trang nào."])
        return await self._read_images(page_images, file_name)

    def _render_pdf_pages(self, pdf_bytes: bytes) -> list[bytes]:
        import pypdfium2 as pdfium

        pdf = pdfium.PdfDocument(pdf_bytes)
        try:
            images: list[bytes] = []
            for i in range(min(len(pdf), MAX_PDF_PAGES)):
                page = pdf[i]
                width, height = page.get_size()
                # Trang A4 ~ 842pt; render sao cho cạnh dài ~ image_max_side
                scale = self._settings.image_max_side / max(width, height, 1)
                pil = page.render(scale=max(scale, 1.0)).to_pil()
                buf = io.BytesIO()
                pil.save(buf, format="PNG")
                images.append(buf.getvalue())
            return images
        finally:
            pdf.close()

    async def _read_images(self, images: list[bytes], file_name: str) -> _FileResult:
        """Bước A cho từng ảnh (hoặc từng trang PDF scan), gộp văn bản rồi đi bước B một lần."""
        vision_model = await self._pick_vision_model()
        if not vision_model:
            return _FileResult(
                warnings=[
                    f"Tệp '{file_name}': máy chủ AI chưa cài model đọc ảnh. Bạn nhập điểm tay vào bảng giúp nhé."
                ]
            )

        pages: list[str] = []
        for page_no, image in enumerate(images, 1):
            try:
                prepared = self._prepare_image(image)
                markdown = tidy_transcription(await self._transcribe(vision_model, prepared))
            except OllamaUnavailableError as ex:
                logger.warning("Bước A lỗi (%s, %s trang %d): %s", vision_model, file_name, page_no, ex)
                return _FileResult(model=vision_model, warnings=[f"Tệp '{file_name}': {FRIENDLY_AI_BUSY}"])
            if markdown.strip():
                pages.append(markdown if len(images) == 1 else f"--- Trang {page_no} ---\n{markdown}")

        if not pages:
            return _FileResult(
                model=vision_model,
                warnings=[
                    f"Tệp '{file_name}': AI không đọc được chữ nào trong ảnh. Bạn chụp rõ và thẳng hơn giúp nhé."
                ],
            )
        return await self._parse_source("\n\n".join(pages), file_name, model_used=vision_model)

    def _prepare_image(self, image: bytes) -> str:
        """Xoay theo EXIF, đưa về cỡ vừa đủ cho model, chuyển xám và tăng tương phản. Trả về base64."""
        try:
            img = Image.open(io.BytesIO(image))
            img = ImageOps.exif_transpose(img)
            img = img.convert("L")
            longest = max(img.size)
            max_side = self._settings.image_max_side
            target = (
                max_side if longest > max_side else MIN_IMAGE_SIDE if longest < MIN_IMAGE_SIDE else longest
            )
            if target != longest:
                ratio = target / longest
                img = img.resize(
                    (round(img.width * ratio), round(img.height * ratio)), Image.Resampling.LANCZOS
                )
            img = ImageOps.autocontrast(img, cutoff=1)
            buf = io.BytesIO()
            img.save(buf, format="PNG", optimize=True)
            return base64.b64encode(buf.getvalue()).decode("ascii")
        except Exception as ex:
            # Định dạng Pillow không mở được (HEIC...): gửi nguyên ảnh, để model tự thử
            logger.info("Bỏ qua tiền xử lý ảnh: %s", ex)
            return base64.b64encode(image).decode("ascii")

    # ---------- Gọi Ollama ----------

    async def _pick_vision_model(self) -> str | None:
        available = await self._get_available_ollama_models()
        wanted = self._settings.vision_model
        for name in available:
            if name == wanted or name == f"{wanted}:latest":
                return name
        for prefix in VISION_MODEL_PRIORITY:
            for name in available:
                if name.startswith(prefix):
                    return name
        return None

    async def _transcribe(self, model: str, image_b64: str) -> str:
        """Bước A: chép nguyên văn ảnh thành Markdown."""
        prompt = GLM_OCR_PROMPT if model.startswith("glm-ocr") else TRANSCRIBE_PROMPT
        payload: dict[str, Any] = {
            "model": model,
            "messages": [{"role": "user", "content": prompt, "images": [image_b64]}],
            "stream": True,
            # Nhả GPU ngay sau khi chép xong để model bước B có chỗ chạy
            "keep_alive": self._settings.vision_keep_alive,
            "options": {"temperature": 0.0, "num_predict": 4096, "num_ctx": 8192},
        }
        if model.startswith("qwen3"):
            # qwen3-vl mặc định "suy nghĩ" trước, tiêu hết num_predict nên content rỗng
            payload["think"] = False
        content = await self._chat(payload, self._settings.vision_timeout_seconds)
        return re.sub(r"<think>.*?</think>", "", content, flags=re.DOTALL).strip()

    async def _parse_source(self, source: str, file_name: str, model_used: str | None) -> _FileResult:
        """Bước B: LLM chữ đọc văn bản, trả JSON theo schema; sau đó hậu kiểm bằng code."""
        source = source[:MAX_SOURCE_CHARS]
        # Bảng học bạ đã dựng thẳng cột thì code đọc luôn: nhanh và không lệch cột như LLM nhỏ
        if table_terms := read_term_tables(source):
            level = "thpt" if any(t["grade"] for t in table_terms) else "unknown"
            direct = self._check_parsed(
                {"scale": "10", "level": level, "terms": table_terms}, source, file_name
            )
            if direct.terms:
                direct.model = model_used
                return direct
        parse_model = self._settings.transcript_parse_model or self._settings.llm_model
        payload: dict[str, Any] = {
            "model": parse_model,
            "messages": [{"role": "user", "content": PARSE_PROMPT + source}],
            "format": PARSE_SCHEMA,
            "think": False,
            "stream": False,
            "options": {"temperature": 0.0, "num_ctx": 8192},
        }
        used = " + ".join(m for m in (model_used, parse_model) if m)
        try:
            content = await self._chat(payload, self._settings.transcript_parse_timeout_seconds)
            data = json.loads(content)
        except OllamaUnavailableError as ex:
            logger.warning("Bước B lỗi (%s, %s): %s", parse_model, file_name, ex)
            return _FileResult(model=used, warnings=[f"Tệp '{file_name}': {FRIENDLY_AI_BUSY}"])
        except ValueError as ex:
            logger.warning("Bước B trả JSON hỏng (%s): %s", file_name, ex)
            return _FileResult(
                model=used,
                warnings=[f"Tệp '{file_name}': AI trả kết quả không hợp lệ, bạn thử lại giúp nhé."],
            )

        result = self._check_parsed(data, source, file_name)
        result.model = used
        return result

    async def _chat(self, payload: dict[str, Any], timeout: float) -> str:
        """POST /api/chat. Lỗi 5xx (thường do thiếu VRAM) thì thử lại 1 lần chạy hoàn toàn trên CPU."""
        try:
            return await self._post_chat(payload, timeout)
        except httpx.HTTPStatusError as ex:
            # Model lặp chữ (repeat limit) là lỗi của model, chạy lại trên CPU cũng lặp y như vậy
            if (
                ex.response.status_code < 500
                or not self._settings.vision_cpu_fallback
                or "repeat limit" in ex.response.text
            ):
                raise OllamaUnavailableError(f"{ex.response.status_code}: {ex.response.text[:300]}") from ex
            logger.warning(
                "Ollama %s lỗi %s, thử lại trên CPU: %s",
                payload["model"],
                ex.response.status_code,
                ex.response.text[:300],
            )
        except httpx.HTTPError as ex:
            raise OllamaUnavailableError(repr(ex)) from ex

        cpu_payload = {**payload, "options": {**payload.get("options", {}), "num_gpu": 0}}
        try:
            return await self._post_chat(cpu_payload, timeout)
        except httpx.HTTPStatusError as ex:
            raise OllamaUnavailableError(f"{ex.response.status_code}: {ex.response.text[:300]}") from ex
        except httpx.HTTPError as ex:
            raise OllamaUnavailableError(repr(ex)) from ex

    async def _post_chat(self, payload: dict[str, Any], timeout: float) -> str:
        if payload.get("stream"):
            return await self._post_chat_stream(payload, timeout)
        resp = await self._http.post("/api/chat", json=payload, timeout=timeout)
        resp.raise_for_status()
        return resp.json().get("message", {}).get("content", "") or ""

    async def _post_chat_stream(self, payload: dict[str, Any], timeout: float) -> str:
        """Nhận chữ dần dần. Model đọc ảnh hay chép xong bảng rồi mới lặp chữ ở phần chữ ký/con dấu,
        Ollama dừng giữa chừng với lỗi "repeat limit": giữ lại phần đã chép thay vì bỏ hết."""
        parts: list[str] = []
        async with self._http.stream("POST", "/api/chat", json=payload, timeout=timeout) as resp:
            if resp.status_code >= 400:
                await resp.aread()
                resp.raise_for_status()
            async for line in resp.aiter_lines():
                if not line.strip():
                    continue
                chunk = json.loads(line)
                if error := chunk.get("error"):
                    if parts and "repeat limit" in error:
                        logger.info("%s lặp chữ sau %d đoạn, giữ phần đã chép", payload["model"], len(parts))
                        break
                    raise OllamaUnavailableError(error)
                parts.append(chunk.get("message", {}).get("content", ""))
        return "".join(parts)

    async def _get_available_ollama_models(self) -> list[str]:
        """Lấy danh sách các mô hình hiện có trong Ollama."""
        try:
            resp = await self._http.get("/api/tags", timeout=5.0)
            if resp.status_code == 200:
                return [m.get("name", "") for m in resp.json().get("models", [])]
        except Exception as ex:
            logger.warning("Không lấy được danh sách model Ollama: %s", ex)
        return []

    # ---------- Hậu kiểm ----------

    def _check_parsed(self, data: Any, source: str, file_name: str) -> _FileResult:
        result = _FileResult()
        if not isinstance(data, dict):
            return result

        scale = str(data.get("scale", "10"))
        if scale in ("4", "letter"):
            result.warnings.append(
                f"Tệp '{file_name}': bảng điểm có vẻ dùng {'thang 4' if scale == '4' else 'điểm chữ'}, "
                "hệ thống chỉ nhận thang 10 nên chưa tự điền. Bạn quy đổi và nhập tay giúp nhé."
            )
            return result

        source_numbers = {round(float(n.replace(",", ".")), 2) for n in _NUMBER_RE.findall(source)}
        high_school = data.get("level") == "thpt"
        dropped: list[str] = []

        for raw in data.get("terms") or []:
            if not isinstance(raw, dict):
                continue
            grade = str(raw.get("grade") or "").strip()
            term = str(raw.get("term") or "").strip()
            resolved = self._resolve_term(grade, term)
            if resolved is None:
                continue

            scores: dict[str, float] = {}
            for item in raw.get("subjects") or []:
                if not isinstance(item, dict):
                    continue
                subject = self._normalize_subject_name(str(item.get("name") or ""))
                if not subject:
                    continue
                value = self._parse_score(item.get("score"))
                if value is None:
                    continue
                if not 0.0 <= value <= 10.0 or round(value, 2) not in source_numbers:
                    dropped.append(f"{subject} {item.get('score')}")
                    continue
                scores[subject] = value
            if scores:
                resolved.scores = scores
                resolved.high_school = high_school
                self._drop_invented_grade(resolved, source)
                result.terms.append(resolved)

        if dropped:
            sample = ", ".join(dropped[:4]) + ("…" if len(dropped) > 4 else "")
            result.warnings.append(
                f"Tệp '{file_name}': bỏ {len(dropped)} điểm không khớp với ảnh ({sample}). Bạn kiểm tra và điền lại các ô còn trống."
            )
        return result

    @classmethod
    def _drop_invented_grade(cls, term: _RawTerm, source: str) -> None:
        """LLM hay tự điền "Lớp 10" khi văn bản không ghi lớp: lớp không có trong văn bản thì bỏ, dùng năm học thay thế."""
        if not term.grade_name or not term.grade_name.startswith("Lớp "):
            return
        plain_source = cls._plain(source)
        grades_in_source = {m.group(1) for m in _SCHOOL_GRADE_RE.finditer(plain_source)}
        if term.grade_name.removeprefix("Lớp ") in grades_in_source:
            return
        academic = _ACADEMIC_YEAR_RE.search(plain_source)
        if academic:
            term.grade_name = None
            term.academic_year = int(academic.group(1))
            term.high_school = True

    @staticmethod
    def _parse_score(raw: Any) -> float | None:
        if raw is None or isinstance(raw, bool):
            return None
        if isinstance(raw, int | float):
            return float(raw)
        text = str(raw).strip().replace(",", ".")
        try:
            value = float(text)
        except ValueError:
            return None  # Đ, CĐ, A, B+...
        return value if math.isfinite(value) else None

    @staticmethod
    def _plain(text: str) -> str:
        """Bỏ dấu tiếng Việt, viết thường, gộp khoảng trắng: 'Học kỳ II' -> 'hoc ky ii'."""
        text = text.replace("đ", "d").replace("Đ", "D")
        text = unicodedata.normalize("NFD", text)
        text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
        return re.sub(r"\s+", " ", text).strip().lower()

    @classmethod
    def _resolve_term(cls, grade: str, term: str) -> _RawTerm | None:
        """Hiểu tên kỳ: trả None nếu là dòng cả năm. Kỳ ĐH ghi theo năm học thì để academic_year, gán "Năm n" sau."""
        label = f"{grade} {term}".strip()
        both = cls._plain(label)
        term_plain = cls._plain(term)
        if re.search(r"\b(ca nam|tong ket|whole year|full year|annual)\b", term_plain or both):
            return None

        raw = _RawTerm(label=label, scores={})

        # Kỳ theo mùa (portal ĐH): Fall = HK1, Spring = HK2, Summer gộp vào HK2 của cùng năm học
        season_match = re.search(r"\b(fall|autumn|spring|summer|ky he)(?=\b|\s*20\d\d)", both)
        season = season_match.group(1) if season_match else None
        semester_match = _SEMESTER_RE.search(both)
        if semester_match:
            token = semester_match.group(1)
            raw.semester = _ROMAN.get(token) or int(token)
        elif season:
            raw.semester = 1 if season in ("fall", "autumn") else 2

        school = _SCHOOL_GRADE_RE.search(both) or re.search(r"^(10|11|12)$", cls._plain(grade))
        uni = _UNI_YEAR_RE.search(both)
        academic = _ACADEMIC_YEAR_RE.search(both)

        if school:
            raw.grade_name = f"Lớp {school.group(1)}"
        elif uni:
            raw.grade_name = f"Năm {uni.group(1)}"
        elif academic:
            raw.academic_year = int(academic.group(1))
        elif season:
            year = _YEAR_RE.search(both)
            if year:
                y = int(year.group(1))
                raw.academic_year = y if season in ("fall", "autumn") else y - 1

        # "Học kỳ 5" không kèm năm: kỳ thứ n tính từ đầu khoá -> Năm ⌈n/2⌉
        if raw.semester and raw.semester > 2:
            if raw.grade_name is None and raw.academic_year is None:
                raw.grade_name = f"Năm {math.ceil(raw.semester / 2)}"
            raw.semester = 1 if raw.semester % 2 == 1 else 2
        return raw

    def _merge_terms(self, raw_terms: list[_RawTerm]) -> dict[str, dict[str, float]]:
        """Kỳ chỉ ghi năm học: ĐH gán "Năm n", THPT gán "Lớp 10 + n" tính từ năm học sớm nhất. Rồi gộp điểm theo tên kỳ chuẩn."""
        first_year: dict[bool, int] = {}
        for t in raw_terms:
            if t.academic_year is not None:
                first_year[t.high_school] = min(
                    first_year.get(t.high_school, t.academic_year), t.academic_year
                )
        merged: dict[str, dict[str, float]] = {}
        for t in raw_terms:
            grade = t.grade_name
            if grade is None and t.academic_year is not None:
                offset = t.academic_year - first_year[t.high_school]
                grade = f"Lớp {min(10 + offset, 12)}" if t.high_school else f"Năm {offset + 1}"
            if grade and t.semester in (1, 2):
                name = f"{grade} HK{t.semester}"
            else:
                name = self._normalize_term_name(t.label)
            merged.setdefault(name, {}).update(t.scores)
        return merged

    # ---------- Chuẩn hoá tên ----------

    @staticmethod
    def _normalize_subject_name(raw_subject: str) -> str | None:
        """Chuẩn hóa tên môn học hoặc loại bỏ môn không tính điểm số."""
        s = re.sub(r"\s+", " ", raw_subject).strip(" *:-")
        lower = s.lower()
        if not lower or lower in IGNORED_SUBJECTS:
            return None
        if "tb môn" in lower or "học lực" in lower or "hạnh kiểm" in lower or "xếp loại" in lower:
            return None
        return SUBJECT_MAP.get(lower) or _PLAIN_SUBJECT_MAP.get(TranscriptReader._plain(s), s)

    @classmethod
    def _normalize_term_name(cls, term_name: str) -> str:
        """Tên kỳ chưa hiểu được: chuẩn hoá nhẹ ('lop 10 hk1' -> 'Lớp 10 HK1'), còn lại giữ nguyên."""
        t = term_name.strip()
        resolved = cls._resolve_term("", t)
        if resolved and resolved.grade_name and resolved.semester in (1, 2):
            return f"{resolved.grade_name} HK{resolved.semester}"
        return t

    @classmethod
    def _term_sort_key(cls, term_name: str) -> tuple[int, int, str]:
        """Sắp xếp kỳ theo thời gian: Lớp 6..12 trước, rồi Năm 1..6, tên lạ xếp cuối."""
        plain = cls._plain(term_name)
        grade_order = 99
        if m := re.match(r"lop (\d+)", plain):
            grade_order = int(m.group(1))
        elif m := re.match(r"nam (\d+)", plain):
            grade_order = 20 + int(m.group(1))
        semester = _SEMESTER_RE.search(plain)
        term_order = int(semester.group(1)) if semester and semester.group(1).isdigit() else 3
        return (grade_order, term_order, plain)


# Tra tên môn theo dạng bỏ dấu: không lệ thuộc cách gõ dấu (hóa / hoá, Unicode tổ hợp hay dựng sẵn)
_PLAIN_SUBJECT_MAP: dict[str, str] = {TranscriptReader._plain(k): v for k, v in SUBJECT_MAP.items()}
