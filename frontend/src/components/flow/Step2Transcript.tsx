"use client";

import { useId, useState } from "react";
import {
  AlertTriangleIcon,
  ArrowLeftIcon,
  ArrowRightIcon,
  CheckCircle2Icon,
  FileTextIcon,
  FileUpIcon,
  Loader2Icon,
  PlusIcon,
  RefreshCwIcon,
  Trash2Icon,
  UploadCloudIcon,
  Edit3Icon,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { calculateLinearGpa, parseTermName, toGpa4 } from "@/lib/profile-flow-options";
import type {
  AcademicYearDraft,
  CurrentEducationLevel,
  DraftTranscriptFile,
  ExtractedTerm,
  GpaMode,
  TranscriptDraft,
} from "@/types/profile-flow";

interface Step2TranscriptProps {
  data: TranscriptDraft;
  currentEducationLevel?: CurrentEducationLevel | null;
  currentGrade?: string | null;
  /** File bảng điểm nháp — BE lưu riêng ngoài draft */
  draftTranscript?: DraftTranscriptFile | null;
  onTranscriptUploaded?: (file: DraftTranscriptFile | null) => void;
  onUploadingChange?: (uploading: boolean) => void;
  onUpdate: (updated: Partial<TranscriptDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

// Khớp với BE (ProfileFileStorageService): chỉ PDF/PNG/JPG, tối đa 10MB
export const TRANSCRIPT_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg"];
export const TRANSCRIPT_ACCEPT = TRANSCRIPT_EXTENSIONS.join(",");
export const TRANSCRIPT_MAX_BYTES = 10 * 1024 * 1024;

const GPA_MODE_OPTIONS: { value: GpaMode; label: string; description: string }[] = [
  { value: "overall", label: "Nhập GPA tổng", description: "Đã biết điểm trung bình chung, chỉ cần gõ một số." },
  {
    value: "transcript",
    label: "Tải học bạ hoặc nhập từng môn",
    description: "Hệ thống tự tính GPA từ điểm các môn.",
  },
];

type GpaScale = "10" | "4";

const DEFAULT_SUBJECTS = [
  "Toán",
  "Ngữ văn",
  "Tiếng Anh",
  "Vật lý",
  "Hóa học",
  "Sinh học",
  "Tin học",
  "Lịch sử",
  "Địa lý",
];

// Sinh danh sách 3 năm học gần nhất tính đến lớp/năm hiện tại
export function getInitialAcademicYears(level?: CurrentEducationLevel | null, grade?: string | null): AcademicYearDraft[] {
  let gradeNames: string[] = ["Lớp 10", "Lớp 11", "Lớp 12"];

  if (level === "middle_school") {
    if (grade === "Lớp 9") {
      gradeNames = ["Lớp 7", "Lớp 8", "Lớp 9"];
    } else if (grade === "Lớp 8") {
      gradeNames = ["Lớp 6", "Lớp 7", "Lớp 8"];
    } else if (grade === "Lớp 7") {
      gradeNames = ["Lớp 6", "Lớp 7"];
    } else if (grade === "Lớp 6") {
      gradeNames = ["Lớp 6"];
    } else {
      gradeNames = ["Lớp 7", "Lớp 8", "Lớp 9"];
    }
  } else if (level === "high_school") {
    if (grade === "Lớp 12") {
      gradeNames = ["Lớp 10", "Lớp 11", "Lớp 12"];
    } else if (grade === "Lớp 11") {
      gradeNames = ["Lớp 9", "Lớp 10", "Lớp 11"];
    } else if (grade === "Lớp 10") {
      gradeNames = ["Lớp 8", "Lớp 9", "Lớp 10"];
    } else {
      gradeNames = ["Lớp 10", "Lớp 11", "Lớp 12"];
    }
  } else if (level === "college_university") {
    if (grade === "Năm 4" || grade === "Năm cuối" || grade === "Đã tốt nghiệp") {
      gradeNames = ["Năm 2", "Năm 3", "Năm 4"];
    } else if (grade === "Năm 3") {
      gradeNames = ["Năm 1", "Năm 2", "Năm 3"];
    } else if (grade === "Năm 2") {
      gradeNames = ["Năm 1", "Năm 2"];
    } else if (grade === "Năm 1") {
      gradeNames = ["Năm 1"];
    } else {
      gradeNames = ["Năm 1", "Năm 2", "Năm 3"];
    }
  } else if (level === "master") {
    if (grade === "Năm 2" || grade === "Đã tốt nghiệp") {
      gradeNames = ["Năm 1", "Năm 2"];
    } else {
      gradeNames = ["Năm 1"];
    }
  }

  return gradeNames.map((g) => ({
    gradeName: g,
    subjects: DEFAULT_SUBJECTS.map((s) => ({
      subjectName: s,
      semester1Score: null,
      semester2Score: null,
    })),
  }));
}

const normalizeGrade = (g: string) =>
  g.normalize("NFD").replace(/\p{M}/gu, "").replace(/đ/gi, "d").replace(/\s+/g, " ").trim().toLowerCase();

// Tên gọi tắt / đầy đủ của cùng một môn → khóa chung, để "Văn" nhập tay và "Ngữ văn" từ OCR không thành 2 dòng
const SUBJECT_ALIASES: Record<string, string> = {
  van: "ngu van",
  "toan hoc": "toan",
  ly: "vat ly",
  li: "vat ly",
  "vat li": "vat ly",
  hoa: "hoa hoc",
  sinh: "sinh hoc",
  su: "lich su",
  dia: "dia ly",
  "dia li": "dia ly",
  anh: "tieng anh",
  "anh van": "tieng anh",
  "ngoai ngu": "tieng anh",
  tin: "tin hoc",
  "gd cd": "gdcd",
  "giao duc cong dan": "gdcd",
  "gd qp": "gdqp",
  "gdqp-an": "gdqp",
};
const subjectKey = (name: string) => {
  const n = normalizeGrade(name);
  return SUBJECT_ALIASES[n] ?? n;
};

const gradeOrder = (gradeName: string) => {
  const m = /^(lop|nam)\s*(\d+)$/.exec(normalizeGrade(gradeName));
  return m ? (m[1] === "nam" ? 100 : 0) + Number(m[2]) : Number.MAX_SAFE_INTEGER;
};

type TermTarget = { gradeName: string | null; semester: 1 | 2 | "all" };

// "Lớp 12 HK1" → { Lớp 12, 1 }; "Lớp 12 - Cả năm" → { Lớp 12, all }; tên lạ → { null, 1 } (gán vào năm đang mở)
function resolveTerm(termName: string): TermTarget {
  const parsed = parseTermName(termName);
  if (parsed) return parsed;
  const allYear = /^(.*?)\s*[-–]?\s*(cả năm|ca nam)\s*$/i.exec(termName.trim());
  if (allYear) return { gradeName: allYear[1].trim() || null, semester: "all" };
  return { gradeName: termName.trim() || null, semester: 1 };
}

// [UC-12] Lý do một ô điểm được tô cam, hiện ở tooltip
const REVIEW_LABELS: Record<string, string> = {
  source_image: "Điểm đọc từ ảnh / bản scan, hãy đối chiếu với bảng điểm gốc.",
  outlier: "Điểm chênh lệch nhiều so với các môn khác, hãy kiểm tra lại.",
  overwrote: "Điểm cũ đã bị thay bằng điểm từ tệp vừa tải.",
  duplicate: "Môn này xuất hiện nhiều lần trong cùng kỳ, đã lấy giá trị sau cùng.",
};

const REVIEW_FIELD = { semester1Score: "semester1Review", semester2Score: "semester2Review" } as const;

function countReviewCells(years: AcademicYearDraft[]): number {
  return years.reduce(
    (n, y) => n + y.subjects.reduce((m, s) => m + (s.semester1Review ? 1 : 0) + (s.semester2Review ? 1 : 0), 0),
    0
  );
}

// Gộp điểm OCR vào các năm học (không sửa trực tiếp object cũ)
// Ô bị trùng môn trong cùng kỳ, ghi đè điểm khác đã có, hoặc advisor đánh dấu → gắn lý do cần kiểm tra
export function mergeExtractedTerms(
  years: AcademicYearDraft[],
  terms: ExtractedTerm[],
  fallbackIdx: number
): AcademicYearDraft[] {
  let result = years.map((y) => ({ ...y, subjects: y.subjects.map((s) => ({ ...s })) }));

  for (const term of terms) {
    const target = resolveTerm(term.termName || "");
    let idx = target.gradeName
      ? result.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(target.gradeName!))
      : -1;

    // Bảng điểm có khối chưa nằm trong danh sách (VD đang ở Lớp 11 nhưng tải cả Lớp 9) → thêm năm mới
    if (idx === -1 && target.gradeName && /^(lop|nam)\s*\d+$/.test(normalizeGrade(target.gradeName))) {
      result = [...result, { gradeName: target.gradeName, subjects: [] }];
      idx = result.length - 1;
    }
    if (idx === -1) idx = Math.min(fallbackIdx, result.length - 1);
    if (idx < 0) continue;

    const subjects = result[idx].subjects;
    const seenInTerm = new Set<string>();
    for (const sc of term.scores) {
      const name = sc.subject?.trim();
      if (!name || typeof sc.score !== "number") continue;
      const key = subjectKey(name);
      const duplicate = seenInTerm.has(key);
      seenInTerm.add(key);
      let sub = subjects.find((x) => subjectKey(x.subjectName) === key);
      if (!sub) {
        sub = { subjectName: name, semester1Score: null, semester2Score: null };
        subjects.push(sub);
      }
      const cell = sub;
      const setCell = (field: "semester1Score" | "semester2Score") => {
        const prev = cell[field];
        const overwrote = typeof prev === "number" && prev !== sc.score;
        cell[field] = sc.score;
        cell[REVIEW_FIELD[field]] = duplicate
          ? "duplicate"
          : overwrote
          ? "overwrote"
          : sc.needsReview
          ? sc.reviewReason || "source_image"
          : null;
      };
      if (target.semester === 2) setCell("semester2Score");
      else if (target.semester === 1) setCell("semester1Score");
      else {
        if (cell.semester1Score == null) setCell("semester1Score");
        if (cell.semester2Score == null) setCell("semester2Score");
      }
    }
  }
  // Năm mới thêm vào cuối → xếp lại Lớp 10, 11, 12 cho đúng thứ tự (sort ổn định, tên lạ giữ ở cuối)
  return [...result].sort((a, b) => gradeOrder(a.gradeName) - gradeOrder(b.gradeName));
}

export function Step2Transcript({
  data,
  currentEducationLevel,
  currentGrade,
  draftTranscript,
  onTranscriptUploaded,
  onUploadingChange,
  onUpdate,
  onPrev,
  onNext,
}: Step2TranscriptProps) {
  // Khởi tạo các năm học dựa theo bậc học & lớp hiện tại của người dùng
  const defaultYears = getInitialAcademicYears(currentEducationLevel, currentGrade);
  const years = data.academicYears && data.academicYears.length > 0 ? data.academicYears : defaultYears;

  const [activeYearIndex, setActiveYearIndex] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [ocrFailedNotice, setOcrFailedNotice] = useState<string | null>(null);
  const [ocrSuccessNotice, setOcrSuccessNotice] = useState<string | null>(null);
  const [ocrWarnings, setOcrWarnings] = useState<string[]>([]);
  const [showManualTable, setShowManualTable] = useState(
    Boolean(
      data.academicYears &&
        data.academicYears.length > 0 &&
        data.academicYears.some((y) => y.subjects.some((s) => s.semester1Score !== null || s.semester2Score !== null))
    )
  );
  const [dragOver, setDragOver] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [scalePick, setScalePick] = useState<GpaScale>("10");
  const fileInputId = useId();
  const overallInputId = useId();

  const { gpa10, gpa4 } = calculateLinearGpa(years);
  const reviewCount = countReviewCells(years);

  // Nháp cũ chưa có gpaMode: chỉ có GPA tổng (không có điểm môn) thì là nhập GPA tổng
  const hasOverallGpa = data.manualGpa10Scale != null || data.calculatedGpa4Scale != null;
  const mode: GpaMode = data.gpaMode ?? (gpa10 === null && hasOverallGpa ? "overall" : "transcript");
  // Thang của GPA tổng suy từ số đã lưu; chưa có số thì theo lựa chọn của người dùng
  const overallScale: GpaScale =
    data.manualGpa10Scale != null ? "10" : data.calculatedGpa4Scale != null ? "4" : scalePick;
  const overallValue = overallScale === "10" ? data.manualGpa10Scale : data.calculatedGpa4Scale;

  const handleModeChange = (next: GpaMode) => {
    if (next === mode) return;
    setValidationError(null);
    if (next === "transcript") {
      onUpdate({ gpaMode: "transcript" });
      return;
    }
    // Chuyển sang GPA tổng thì bỏ điểm môn, để BE và chat không tính nhầm từ điểm cũ
    if (gpa10 !== null) {
      if (!window.confirm("Chuyển sang nhập GPA tổng sẽ xóa điểm các môn đã nhập. Bạn có chắc không?")) return;
      onUpdate({ gpaMode: "overall", academicYears: [], manualGpa10Scale: null, calculatedGpa4Scale: null });
      setShowManualTable(false);
      return;
    }
    onUpdate({ gpaMode: "overall" });
  };

  const handleOverallScaleChange = (scale: GpaScale) => {
    if (scale === overallScale) return;
    setScalePick(scale);
    onUpdate({ gpaMode: "overall", manualGpa10Scale: null, calculatedGpa4Scale: null });
  };

  const handleOverallChange = (valStr: string) => {
    const parsed = valStr.trim() === "" ? null : Number(valStr);
    const val = parsed === null || Number.isNaN(parsed) ? null : parsed;
    setValidationError(null);
    if (overallScale === "10") {
      onUpdate({ gpaMode: "overall", manualGpa10Scale: val, calculatedGpa4Scale: val === null ? null : toGpa4(val) });
    } else {
      onUpdate({ gpaMode: "overall", manualGpa10Scale: null, calculatedGpa4Scale: val });
    }
  };

  // [UC-12] Người dùng xác nhận đã rà toàn bộ bảng điểm → bỏ mọi cờ (cờ lưu trong nháp, BE không cho commit khi còn cờ)
  const handleConfirmReviewed = () => {
    onUpdate({
      academicYears: years.map((y) => ({
        ...y,
        subjects: y.subjects.map((s) => ({ ...s, semester1Review: null, semester2Review: null })),
      })),
    });
    setValidationError(null);
  };

  const handleScoreChange = (
    yearIdx: number,
    subIdx: number,
    field: "semester1Score" | "semester2Score",
    valStr: string
  ) => {
    const val = valStr.trim() === "" ? null : Number(valStr);
    const updatedYears = years.map((y, yIdx) => {
      if (yIdx !== yearIdx) return y;
      const newSubjects = y.subjects.map((s, sIdx) => {
        if (sIdx !== subIdx) return s;
        // Người dùng tự sửa ô → coi như đã kiểm tra ô đó
        return { ...s, [field]: val, [REVIEW_FIELD[field]]: null };
      });
      return { ...y, subjects: newSubjects };
    });

    const newGpas = calculateLinearGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
  };

  const handleAddSubject = (yearIdx: number) => {
    const name = window.prompt("Nhập tên môn học mới:", "Môn tự chọn");
    if (!name || !name.trim()) return;

    const updatedYears = years.map((y, yIdx) => {
      if (yIdx !== yearIdx) return y;
      return {
        ...y,
        subjects: [...y.subjects, { subjectName: name.trim(), semester1Score: null, semester2Score: null }],
      };
    });

    onUpdate({ academicYears: updatedYears });
  };

  const handleRemoveSubject = (yearIdx: number, subIdx: number) => {
    const updatedYears = years.map((y, yIdx) => {
      if (yIdx !== yearIdx) return y;
      return {
        ...y,
        subjects: y.subjects.filter((_, sIdx) => sIdx !== subIdx),
      };
    });

    const newGpas = calculateLinearGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
  };

  const triggerFileInput = () => {
    const el = document.getElementById(fileInputId) as HTMLInputElement | null;
    if (el) {
      el.value = "";
      el.click();
    }
  };

  const handleFileUpload = async (files: FileList | File[]) => {
    const fileArray = Array.from(files);
    if (fileArray.length === 0) return;

    // Báo lỗi ngay ở trình duyệt, khỏi chờ gửi lên BE (BE vẫn kiểm tra lại cả nội dung tệp)
    const file = fileArray[0];
    const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!TRANSCRIPT_EXTENSIONS.includes(ext)) {
      setUploadError("Chỉ nhận tệp PDF, PNG, JPG hoặc JPEG.");
      return;
    }
    if (file.size > TRANSCRIPT_MAX_BYTES) {
      setUploadError("Tệp quá lớn. Bảng điểm tối đa 10MB.");
      return;
    }

    setIsUploading(true);
    onUploadingChange?.(true);
    setUploadError(null);
    setOcrFailedNotice(null);
    setOcrSuccessNotice(null);
    setOcrWarnings([]);

    try {
      // BE chỉ nhận 1 file mỗi lần tải
      const res = await profileFlowApi.uploadDraftTranscript(fileArray.slice(0, 1));
      onTranscriptUploaded?.(res.draftTranscript ?? null);

      if (res.warnings && res.warnings.length > 0) {
        setOcrWarnings(res.warnings);
      }

      const extractedTerms = (res.extractedTerms ?? []).filter((t) => t.scores?.length > 0);

      if (extractedTerms.length > 0) {
        const updatedYears = mergeExtractedTerms(years, extractedTerms, activeYearIndex);
        const newGpas = calculateLinearGpa(updatedYears);
        onUpdate({
          gpaMode: "transcript",
          academicYears: updatedYears,
          manualGpa10Scale: newGpas.gpa10,
          calculatedGpa4Scale: newGpas.gpa4,
        });

        setShowManualTable(true);

        const filledYears = updatedYears.filter((y) =>
          y.subjects.some((s) => s.semester1Score != null || s.semester2Score != null)
        );

        if (filledYears.length < updatedYears.length) {
          const missingYears = updatedYears
            .filter((y) => y.subjects.every((s) => s.semester1Score == null && s.semester2Score == null))
            .map((y) => y.gradeName);

          setOcrSuccessNotice(
            `Đã nhận diện thành công điểm của ${filledYears.map((y) => y.gradeName).join(", ")}. Lưu ý: Hồ sơ du học cần đủ điểm các năm (${updatedYears.map((y) => y.gradeName).join(", ")}). Bạn đang còn thiếu ${missingYears.join(", ")}. Vui lòng tải thêm ảnh bảng điểm hoặc tự nhập các môn còn thiếu vào bảng bên dưới.`
          );
        } else {
          setOcrSuccessNotice(
            `Đã nhận diện thành công đầy đủ điểm các năm (${filledYears.map((y) => y.gradeName).join(", ")}) từ bảng điểm. Vui lòng kiểm tra lại điểm trong bảng bên dưới.`
          );
        }
      } else {
        // Tải file lên được nhưng AI không đọc được điểm (ảnh mờ, scan lệch...)
        setOcrFailedNotice(
          res.ocrMessage ||
            "Hệ thống đã nhận tệp nhưng không thể trích xuất được bảng điểm (ảnh có thể bị mờ, chụp lệch, độ tương phản thấp hoặc không chứa bảng điểm học tập). Vui lòng gửi lại ảnh rõ nét hơn hoặc nhập điểm trực tiếp bằng tay."
        );
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Đã có lỗi khi xử lý tệp bảng điểm";
      setUploadError(msg);
      setOcrFailedNotice(
        "Không thể đọc bảng điểm từ tệp này. Bạn có thể thử chụp lại ảnh rõ nét hơn hoặc chuyển sang tự nhập điểm bằng tay."
      );
    } finally {
      setIsUploading(false);
      onUploadingChange?.(false);
    }
  };

  const handleNextClick = () => {
    if (mode === "overall") {
      if (overallValue == null) {
        setValidationError("Vui lòng nhập GPA tổng trước khi tiếp tục.");
        return;
      }
      if (overallValue < 0 || overallValue > Number(overallScale)) {
        setValidationError(`GPA thang ${overallScale} phải từ 0 đến ${overallScale}.`);
        return;
      }
      setValidationError(null);
      onNext();
      return;
    }

    if (reviewCount > 0) {
      setValidationError(
        `Bảng điểm còn ${reviewCount} ô tô cam cần kiểm tra. Hãy đối chiếu với bảng điểm gốc rồi bấm "Tôi đã kiểm tra bảng điểm".`
      );
      setShowManualTable(true);
      const firstFlaggedIdx = years.findIndex((y) => y.subjects.some((s) => s.semester1Review || s.semester2Review));
      if (firstFlaggedIdx !== -1) setActiveYearIndex(firstFlaggedIdx);
      return;
    }

    if (gpa10 === null || gpa4 === null) {
      setValidationError(
        "Vui lòng tải lên ảnh bảng điểm hoặc tự điền điểm các môn học để hệ thống tính toán GPA trước khi tiếp tục."
      );
      setShowManualTable(true);
      return;
    }

    // Kiểm tra từng năm học yêu cầu trong danh sách năm
    const emptyYears = years.filter((y) =>
      y.subjects.every(
        (s) =>
          (s.semester1Score === null || s.semester1Score === undefined) &&
          (s.semester2Score === null || s.semester2Score === undefined)
      )
    );

    if (emptyYears.length > 0) {
      setValidationError(
        `Hồ sơ cần đầy đủ thông tin điểm của 3 năm học (${years.map((y) => y.gradeName).join(", ")}) để tính toán cơ hội trúng tuyển chuẩn xác nhất. Hiện bạn đang thiếu điểm của ${emptyYears.map((y) => y.gradeName).join(", ")}. Vui lòng tải thêm ảnh bảng điểm hoặc điền tay các năm còn thiếu.`
      );
      setShowManualTable(true);
      const firstMissingIdx = years.findIndex((y) => y.gradeName === emptyYears[0].gradeName);
      if (firstMissingIdx !== -1) setActiveYearIndex(firstMissingIdx);
      return;
    }

    // Kiểm tra nếu tổng số học kỳ có điểm quá ít (chỉ có 1 học kỳ trên tổng số các năm)
    let totalTermsWithScores = 0;
    for (const y of years) {
      if (y.subjects.some((s) => s.semester1Score !== null && s.semester1Score !== undefined)) {
        totalTermsWithScores++;
      }
      if (y.subjects.some((s) => s.semester2Score !== null && s.semester2Score !== undefined)) {
        totalTermsWithScores++;
      }
    }

    if (years.length >= 2 && totalTermsWithScores < 2) {
      setValidationError(
        "Dữ liệu hiện tại mới chỉ ghi nhận 1 học kỳ. Vui lòng tải thêm ảnh hoặc điền thêm điểm các học kỳ khác để hồ sơ có đủ điểm đánh giá."
      );
      setShowManualTable(true);
      return;
    }

    setValidationError(null);
    onNext();
  };

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 2/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">
          Bảng điểm của bạn
        </h1>
        <p className="text-body text-muted-foreground">
          Đã biết GPA thì gõ thẳng. Chưa biết thì tải bảng điểm lên để hệ thống tự tính.
        </p>
      </div>

      {/* Hai cách nhập GPA: GPA tổng hoặc học bạ */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3" role="radiogroup" aria-label="Cách nhập GPA">
        {GPA_MODE_OPTIONS.map((opt) => (
          <button
            key={opt.value}
            type="button"
            role="radio"
            aria-checked={mode === opt.value}
            onClick={() => handleModeChange(opt.value)}
            className={cn(
              "rounded-xl border p-4 text-left transition-colors cursor-pointer",
              mode === opt.value
                ? "border-brand bg-brand-wash/40 ring-1 ring-brand"
                : "border-line bg-surface hover:border-brand/50"
            )}
          >
            <div className="font-semibold text-sm text-foreground">{opt.label}</div>
            <div className="text-xs text-muted-foreground mt-0.5">{opt.description}</div>
          </button>
        ))}
      </div>

      {/* Hidden file input */}
      <input
        id={fileInputId}
        type="file"
        accept={TRANSCRIPT_ACCEPT}
        className="hidden"
        onChange={(e) => e.target.files && handleFileUpload(e.target.files)}
      />

      {mode === "overall" ? (
        <div className="rounded-xl border border-line bg-surface p-5 space-y-4">
          <div>
            <div className="font-semibold text-sm text-foreground">GPA tổng của bạn</div>
            <div className="text-xs text-muted-foreground mt-0.5">
              Điểm trung bình chung trên học bạ hoặc bảng điểm. Nhớ chọn đúng thang điểm.
            </div>
          </div>
          <div className="flex flex-col sm:flex-row sm:items-end gap-4">
            <div className="space-y-1.5">
              <Label className="text-xs font-semibold text-foreground">Thang điểm</Label>
              <div className="flex gap-2">
                {(["10", "4"] as const).map((scale) => (
                  <Button
                    key={scale}
                    type="button"
                    size="sm"
                    variant={overallScale === scale ? "default" : "outline"}
                    onClick={() => handleOverallScaleChange(scale)}
                    className="cursor-pointer"
                  >
                    Thang {scale}
                  </Button>
                ))}
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor={overallInputId} className="text-xs font-semibold text-foreground">
                GPA tổng (0–{overallScale})
              </Label>
              <Input
                id={overallInputId}
                type="number"
                inputMode="decimal"
                min={0}
                max={Number(overallScale)}
                step={0.01}
                placeholder={overallScale === "10" ? "VD: 8.5" : "VD: 3.4"}
                value={overallValue ?? ""}
                onChange={(e) => handleOverallChange(e.target.value)}
                className="w-40 bg-surface"
              />
            </div>
            {overallScale === "10" && (
              <div className="text-xs text-muted-foreground sm:pb-2.5">
                Quy đổi thang 4:{" "}
                <strong className="text-brand">
                  {data.manualGpa10Scale != null ? toGpa4(data.manualGpa10Scale).toFixed(2) : "—"}
                </strong>
              </div>
            )}
          </div>
        </div>
      ) : (
        <>
      {/* OCR Warnings if any */}
      {ocrWarnings.length > 0 && (
        <Alert className="border-warn/40 bg-warn-wash/40">
          <AlertTriangleIcon className="size-4 text-warn" />
          <AlertTitle className="text-sm font-semibold text-warn">Lưu ý khi đọc bảng điểm</AlertTitle>
          <AlertDescription className="text-xs text-muted-foreground space-y-1">
            {ocrWarnings.map((w, idx) => (
              <div key={idx}>• {w}</div>
            ))}
          </AlertDescription>
        </Alert>
      )}

      {/* OCR Success notice */}
      {ocrSuccessNotice && (
        <Alert className="border-brand/40 bg-brand-wash/40">
          <CheckCircle2Icon className="size-4 text-brand" />
          <AlertTitle className="text-sm font-semibold text-brand">Đã trích xuất điểm thành công</AlertTitle>
          <AlertDescription className="text-xs text-brand-ink">{ocrSuccessNotice}</AlertDescription>
        </Alert>
      )}

      {/* OCR Failure & Re-upload or Manual Entry Card */}
      {ocrFailedNotice && (
        <div className="rounded-2xl border-2 border-warn/60 bg-warn-wash/30 p-5 space-y-3 animate-in fade-in-50">
          <div className="flex items-start gap-3">
            <div className="p-2 rounded-xl bg-warn-wash text-warn shrink-0">
              <AlertTriangleIcon className="size-5" />
            </div>
            <div className="space-y-1">
              <div className="font-semibold text-sm text-foreground">
                Chưa nhận diện được dữ liệu từ bảng điểm
              </div>
              <div className="text-xs text-muted-foreground leading-relaxed">
                {ocrFailedNotice}
              </div>
            </div>
          </div>

          <div className="flex flex-wrap gap-2 pt-2 border-t border-warn/20">
            <Button
              type="button"
              size="sm"
              onClick={triggerFileInput}
              className="gap-1.5 cursor-pointer bg-brand hover:bg-brand-ink text-xs font-semibold text-white"
            >
              <RefreshCwIcon className="size-3.5" />
              Chọn ảnh / tệp khác rõ nét hơn
            </Button>
            <Button
              type="button"
              size="sm"
              variant="outline"
              onClick={() => {
                setShowManualTable(true);
                setOcrFailedNotice(null);
              }}
              className="gap-1.5 cursor-pointer text-xs font-semibold"
            >
              <Edit3Icon className="size-3.5" />
              Chuyển sang điền điểm bằng tay
            </Button>
          </div>
        </div>
      )}

      {/* 1. Vùng Upload bảng điểm (Luôn nằm ngay trên bảng điểm) */}
      <div className="space-y-3">
        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            if (e.dataTransfer.files) handleFileUpload(e.dataTransfer.files);
          }}
          className={cn(
            "flex flex-col items-center justify-center rounded-2xl border-2 border-dashed p-6 sm:p-7 text-center transition-all",
            dragOver
              ? "border-brand bg-brand-wash/30 scale-[1.01]"
              : "border-line bg-surface hover:border-brand/50 hover:bg-muted/10",
            isUploading && "border-brand bg-brand-wash/20"
          )}
        >
          {isUploading ? (
            <div className="py-2 space-y-3 flex flex-col items-center">
              <Loader2Icon className="size-9 animate-spin text-brand" />
              <div className="space-y-1">
                <div className="font-semibold text-sm text-foreground">
                  Đang phân tích bảng điểm bằng Ollama AI...
                </div>
                <div className="text-xs text-muted-foreground max-w-md">
                  Mô hình thị giác đang đọc các cột môn học và bóc tách điểm số. Vui lòng đợi trong giây lát...
                </div>
              </div>
            </div>
          ) : (
            <div className="flex flex-col sm:flex-row items-center justify-between gap-4 w-full">
              <div className="flex items-center gap-3 text-left">
                <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-brand-wash text-brand">
                  <UploadCloudIcon className="size-6" />
                </div>
                <div>
                  <div className="font-semibold text-sm text-foreground">
                    {draftTranscript ? "Đã đọc xong bảng điểm của bạn" : "Tải học bạ lên để tự điền bảng dưới"}
                  </div>
                  <div className="text-xs text-muted-foreground mt-0.5">
                    {draftTranscript
                      ? "Kiểm tra lại điểm ở bảng dưới, sửa trực tiếp nếu cần."
                      : "Tệp PDF hoặc ảnh chụp (PNG, JPG). Tải tiếp tệp khác để bổ sung các kỳ còn thiếu."}
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 shrink-0">
                {draftTranscript && (
                  <Badge variant="secondary" className="text-[11px] gap-1 px-2.5 py-1">
                    <CheckCircle2Icon className="size-3 text-ok" />
                    Đã tải file
                  </Badge>
                )}
                <Button
                  type="button"
                  size="sm"
                  onClick={triggerFileInput}
                  className="gap-2 cursor-pointer shadow-xs font-semibold text-xs bg-brand hover:bg-brand-ink text-white"
                >
                  <FileUpIcon className="size-3.5" />
                  {draftTranscript ? "Tải tệp khác" : "Chọn tệp từ máy"}
                </Button>
              </div>
            </div>
          )}
        </div>

        {uploadError && (
          <div className="rounded-xl bg-destructive/10 p-3 text-xs text-destructive font-medium border border-destructive/20">
            {uploadError}
          </div>
        )}
      </div>

      {/* 2. Bảng điểm các năm học & thanh tóm tắt GPA (Nằm ngay phía dưới vùng upload) */}
      <div className="space-y-6">
        {/* File attached banner if any */}
        {draftTranscript && (
          <div className="flex items-center justify-between rounded-xl border border-line bg-surface p-3 text-xs">
            <div className="flex items-center gap-2 text-foreground font-medium">
              <FileTextIcon className="size-4 text-brand" />
              <span>{draftTranscript.originalFileName}</span>
              <Badge variant="secondary" className="text-[10px]">Đã quét AI</Badge>
            </div>
            <button
              type="button"
              onClick={triggerFileInput}
              className="text-xs font-semibold text-brand hover:underline cursor-pointer"
            >
              Tải tệp khác
            </button>
          </div>
        )}

          {/* Academic Years Selector */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <div>
                <Label className="text-sm font-semibold text-foreground">
                  Điểm học bạ 3 năm gần nhất đến hiện tại
                </Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Đang hiển thị các năm học tính đến {currentGrade || "lớp hiện tại"}.
                </p>
              </div>
              <button
                type="button"
                onClick={triggerFileInput}
                className="text-xs text-brand font-medium hover:underline cursor-pointer flex items-center gap-1"
              >
                <FileUpIcon className="size-3.5" />
                Tải ảnh khác
              </button>
            </div>

            <div className="flex flex-wrap gap-2 border-b border-line pb-2">
              {years.map((y, idx) => (
                <button
                  key={y.gradeName}
                  type="button"
                  onClick={() => setActiveYearIndex(idx)}
                  className={cn(
                    "rounded-lg px-4 py-2 text-xs font-semibold transition-all cursor-pointer shadow-2xs",
                    activeYearIndex === idx
                      ? "bg-brand text-white shadow-xs"
                      : "bg-surface border border-line text-muted-foreground hover:border-brand/40 hover:text-foreground"
                  )}
                >
                  {y.gradeName}
                  {idx === years.length - 1 && " (hiện tại)"}
                </button>
              ))}
            </div>
          </div>

          {/* [UC-12] Nhắc kiểm tra các ô tô cam */}
          {reviewCount > 0 && (
            <Alert className="border-warn/40 bg-warn-wash/40">
              <AlertTriangleIcon className="size-4 text-warn" />
              <AlertTitle className="text-sm font-semibold text-warn">
                Bảng điểm có {reviewCount} ô cần kiểm tra
              </AlertTitle>
              <AlertDescription className="text-xs leading-relaxed space-y-2">
                <p>
                  Các ô tô cam là điểm đọc từ ảnh, điểm chênh lệch bất thường hoặc bị ghi đè / trùng môn (
                  {years
                    .filter((y) => y.subjects.some((s) => s.semester1Review || s.semester2Review))
                    .map((y) => y.gradeName)
                    .join(", ")}
                  ). Rê chuột vào ô để xem lý do. Sửa ô nào thì ô đó hết tô cam.
                </p>
                <Button type="button" size="sm" variant="outline" onClick={handleConfirmReviewed} className="gap-1.5 cursor-pointer">
                  <CheckCircle2Icon className="size-3.5" />
                  Tôi đã kiểm tra bảng điểm
                </Button>
              </AlertDescription>
            </Alert>
          )}

          {/* Subjects Table */}
          {years[activeYearIndex] && (
            <div className="space-y-4">
              <div className="overflow-hidden rounded-xl border border-line bg-surface shadow-2xs">
                <table className="w-full text-left text-xs">
                  <thead className="border-b border-line bg-muted/30 text-muted-foreground uppercase font-semibold text-[10px]">
                    <tr>
                      <th className="py-2.5 px-3">Môn học</th>
                      <th className="py-2.5 px-3 w-28 text-center">Học kỳ 1</th>
                      <th className="py-2.5 px-3 w-28 text-center">Học kỳ 2</th>
                      <th className="py-2.5 px-3 w-24 text-center">Trung bình</th>
                      <th className="py-2.5 px-2 w-12 text-center">Xóa</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line/60">
                    {years[activeYearIndex].subjects.map((sub, sIdx, subjects) => {
                      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
                      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
                      const avg =
                        s1 !== null && s2 !== null
                          ? Math.round(((s1 + s2) / 2) * 10) / 10
                          : s1 !== null
                          ? s1
                          : s2;

                      // Sau khi AI điền điểm, ô còn trống trong năm đã có điểm được tô vàng để nhắc kiểm tra
                      const flagEmpty =
                        ocrSuccessNotice !== null &&
                        subjects.some((x) => x.semester1Score != null || x.semester2Score != null);
                      const outOfRange = (v: number | null | undefined) => typeof v === "number" && (v < 0 || v > 10);
                      const cellClass = (empty: boolean, invalid: boolean, review?: string | null) =>
                        cn(
                          "h-8 text-center text-xs w-20 mx-auto",
                          flagEmpty && empty && "border-warn bg-warn-wash/40",
                          review && "border-warn bg-warn-wash ring-1 ring-warn/60",
                          invalid && "border-destructive ring-1 ring-destructive"
                        );
                      const reviewTitle = (review?: string | null) =>
                        review ? REVIEW_LABELS[review] ?? "Ô này cần kiểm tra lại." : undefined;

                      return (
                        <tr key={sIdx} className="hover:bg-muted/10 transition-colors">
                          <td className="py-2 px-3 font-medium text-foreground">
                            {sub.subjectName}
                          </td>
                          <td className="py-1 px-2 text-center">
                            <Input
                              type="number"
                              min={0}
                              max={10}
                              step={0.1}
                              placeholder="0–10"
                              value={sub.semester1Score ?? ""}
                              onChange={(e) =>
                                handleScoreChange(activeYearIndex, sIdx, "semester1Score", e.target.value)
                              }
                              className={cellClass(s1 === null, outOfRange(sub.semester1Score), sub.semester1Review)}
                              title={reviewTitle(sub.semester1Review)}
                            />
                          </td>
                          <td className="py-1 px-2 text-center">
                            <Input
                              type="number"
                              min={0}
                              max={10}
                              step={0.1}
                              placeholder="0–10"
                              value={sub.semester2Score ?? ""}
                              onChange={(e) =>
                                handleScoreChange(activeYearIndex, sIdx, "semester2Score", e.target.value)
                              }
                              className={cellClass(s2 === null, outOfRange(sub.semester2Score), sub.semester2Review)}
                              title={reviewTitle(sub.semester2Review)}
                            />
                          </td>
                          <td className="py-2 px-3 text-center font-bold text-foreground">
                            {avg !== null ? avg.toFixed(1) : "-"}
                          </td>
                          <td className="py-2 px-2 text-center">
                            <button
                              type="button"
                              onClick={() => handleRemoveSubject(activeYearIndex, sIdx)}
                              className="text-muted-foreground hover:text-destructive cursor-pointer p-1 rounded"
                              title="Xóa môn"
                            >
                              <Trash2Icon className="size-3.5" />
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Add Subject Button */}
              <div className="flex justify-between items-center">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => handleAddSubject(activeYearIndex)}
                  className="gap-1.5 text-xs cursor-pointer"
                >
                  <PlusIcon className="size-3.5" />
                  Thêm môn học
                </Button>
                <span className="text-[11px] text-muted-foreground">
                  Điểm số tính theo thang điểm 10 của Việt Nam
                </span>
              </div>
            </div>
          )}

          {/* Dynamic GPA Calculation Card */}
          <div className="rounded-xl border border-brand/30 bg-brand-wash/30 p-4 shadow-2xs">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
              <div>
                <div className="font-semibold text-sm text-foreground flex items-center gap-1.5">
                  <span>Điểm TB tự tính</span>
                  <Badge variant="outline" className="text-[10px] border-brand/40 text-brand">
                    Tuyến tính: GPA 10 / 10 * 4.0
                  </Badge>
                </div>
                <div className="text-xs text-muted-foreground mt-0.5">
                  Được tính tự động từ toàn bộ các môn và năm học đã nhập ở trên.
                </div>
              </div>

              <div className="flex items-center gap-4">
                <div className="text-right">
                  <div className="text-[11px] text-muted-foreground">Thang điểm 10</div>
                  <div className="text-lg font-bold text-foreground">
                    {gpa10 !== null ? gpa10.toFixed(2) : "—"}
                  </div>
                </div>
                <div className="h-8 w-px bg-line" />
                <div className="text-right">
                  <div className="text-[11px] text-brand font-semibold">Quy đổi thang 4</div>
                  <div className="text-2xl font-extrabold text-brand">
                    {gpa4 !== null ? gpa4.toFixed(2) : "—"}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        </>
      )}

      {/* Validation Error Notice */}
      {validationError && (
        <Alert className="border-destructive/60 bg-destructive/10 animate-in fade-in-50">
          <AlertTriangleIcon className="size-4 text-destructive" />
          <AlertTitle className="text-sm font-semibold text-destructive">Chưa đủ dữ liệu bảng điểm</AlertTitle>
          <AlertDescription className="text-xs text-destructive font-medium leading-relaxed">
            {validationError}
          </AlertDescription>
        </Alert>
      )}

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5 cursor-pointer">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5 cursor-pointer">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
