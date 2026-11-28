"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  AlertTriangleIcon,
  ArrowLeftIcon,
  ArrowRightIcon,
  ArrowRightLeftIcon,
  BookOpenIcon,
  CheckCircle2Icon,
  FileTextIcon,
  FileUpIcon,
  Loader2Icon,
  PlusIcon,
  RefreshCwIcon,
  Trash2Icon,
  UploadCloudIcon,
  Edit3Icon,
  TrendingUpIcon,
  TrendingDownIcon,
  XIcon,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  AppDialog,
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { StepHeader, StepNav } from "./StepChrome";
import {
  ActionList,
  ActionRow,
  AddRow,
  ChoiceCards,
  ChoicePicker,
  DATA_TABLE,
  Field,
  FieldError,
  FormBody,
  FormGroup,
  FormSection,
  GroupResult,
  GroupTabs,
  INLINE_INPUT,
  SegmentedControl,
} from "./FormKit";
import { cn } from "@/lib/utils";
import { GpaConversionNote } from "./GpaConversionNote";
import { useGradeScale } from "@/hooks/use-grade-scale";
import { profileFlowApi } from "@/lib/profile-flow-api";
import {
  calculateGradeTrend,
  calculateTranscriptGpa,
  toGpa4,
  yearAverage,
} from "@/lib/profile-flow-options";
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
  { value: "overall", label: "Nhập GPA tổng", description: "Nhập điểm trung bình chung, đơn giản hơn" },
  { value: "transcript", label: "Nhập từng môn", description: "Chi tiết hơn, hệ thống tự tính GPA" },
];

// Dải điểm ước tính chuẩn cho học sinh Việt Nam và Quốc tế (áp dụng khi học sinh chưa rõ GPA)
export const ESTIMATOR_PRESETS_10 = [
  { label: "Xuất sắc / Top đầu", range: "9.0 – 10.0", midpoint: 9.2, desc: "Học sinh giỏi toàn diện, hầu hết môn trên 9.0" },
  { label: "Giỏi mức cao", range: "8.5 – 8.9", midpoint: 8.7, desc: "Thành tích học tập tốt, nhiều môn đạt 9 phẩy" },
  { label: "Giỏi tiêu chuẩn", range: "8.0 – 8.4", midpoint: 8.2, desc: "Mức học sinh giỏi phổ biến ở các trường THPT" },
  { label: "Khá tốt", range: "7.5 – 7.9", midpoint: 7.7, desc: "Năng lực khá, có một số môn thế mạnh" },
  { label: "Khá tiêu chuẩn", range: "7.0 – 7.4", midpoint: 7.2, desc: "Điểm số các môn đều ở mức khá" },
  { label: "Trung bình khá", range: "6.5 – 6.9", midpoint: 6.7, desc: "Điểm các môn dao động từ 6.5 đến 7.0" },
];

export const ESTIMATOR_PRESETS_4 = [
  { label: "Mostly A+ / A", range: "3.9 – 4.0", midpoint: 3.95, desc: "Gần như toàn bộ các môn đạt điểm A/A+" },
  { label: "Mostly A / A-", range: "3.7 – 3.89", midpoint: 3.8, desc: "Đa số các môn đạt điểm A và A-" },
  { label: "Mostly A- / B+", range: "3.4 – 3.69", midpoint: 3.5, desc: "Sự kết hợp giữa điểm A- và B+" },
  { label: "Mostly B+ / B", range: "3.1 – 3.39", midpoint: 3.2, desc: "Điểm trung bình phổ biến mức B+" },
  { label: "Mostly B / B-", range: "2.8 – 3.09", midpoint: 2.9, desc: "Điểm trung bình mức B và B-" },
  { label: "Mostly B- / C+", range: "2.4 – 2.79", midpoint: 2.6, desc: "Cần cải thiện thêm ở các kỳ tới" },
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

const SUGGESTED_ADDITIONAL_SUBJECTS = [
  "Công nghệ",
  "Giáo dục kinh tế & Pháp luật",
  "Giáo dục công dân",
  "Tiếng Pháp",
  "Tiếng Trung",
  "Âm nhạc",
  "Mĩ thuật",
  "Tâm lý học",
];

const normalizeGrade = (g: string) =>
  g.normalize("NFD").replace(/\p{M}/gu, "").replace(/đ/gi, "d").replace(/\s+/g, " ").trim().toLowerCase();

// Sinh danh sách các năm học theo chuẩn tuyển sinh Mỹ (luôn bắt đầu từ Lớp 9 đến lớp/năm hiện tại)
export function getInitialAcademicYears(level?: CurrentEducationLevel | null, grade?: string | null): AcademicYearDraft[] {
  let gradeNames: string[] = ["Lớp 9", "Lớp 10", "Lớp 11", "Lớp 12"];
  const g = grade ? normalizeGrade(grade) : "";

  if (g === "lop 9") {
    gradeNames = ["Lớp 9"];
  } else if (g === "lop 10") {
    gradeNames = ["Lớp 9", "Lớp 10"];
  } else if (g === "lop 11") {
    gradeNames = ["Lớp 9", "Lớp 10", "Lớp 11"];
  } else if (g === "lop 12") {
    gradeNames = ["Lớp 9", "Lớp 10", "Lớp 11", "Lớp 12"];
  } else if (level === "middle_school") {
    // Học sinh THCS (Lớp 6, 7, 8) chuẩn bị lộ trình 4 năm cấp 3
    gradeNames = ["Lớp 9", "Lớp 10", "Lớp 11", "Lớp 12"];
  } else if (level === "college_university") {
    if (g === "nam 4" || g === "nam cuoi" || g.includes("tot nghiep")) {
      gradeNames = ["Năm 1", "Năm 2", "Năm 3", "Năm 4"];
    } else if (g === "nam 3") {
      gradeNames = ["Năm 1", "Năm 2", "Năm 3"];
    } else if (g === "nam 2") {
      gradeNames = ["Năm 1", "Năm 2"];
    } else if (g === "nam 1") {
      gradeNames = ["Năm 1"];
    } else {
      gradeNames = ["Năm 1", "Năm 2", "Năm 3"];
    }
  } else if (level === "master") {
    if (g === "nam 2" || g.includes("tot nghiep")) {
      gradeNames = ["Năm 1", "Năm 2"];
    } else {
      gradeNames = ["Năm 1"];
    }
  }

  return gradeNames.map((gn) => ({
    gradeName: gn,
    subjects: DEFAULT_SUBJECTS.map((s) => ({
      subjectName: s,
      semester1Score: null,
      semester2Score: null,
    })),
  }));
}

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

type TermTarget = { gradeName: string | null; semester: 1 | 2 | "all" | null };

// Trạng thái gán kỳ cho các term chưa rõ khối lớp / kỳ học:
// - Nếu có 2 kỳ: hỏi chọn lớp -> điền cả 2 kỳ luôn
// - Nếu không rõ lớp lẫn kỳ:
//     + Bước 1 ("choose_grade"): hỏi chọn lớp
//     + Bước 2 ("choose_semester"): hỏi chọn kỳ
type PendingAssignStep = "choose_grade" | "choose_semester";

// Bản ghi thông tin vừa gán điểm vào một khối lớp & học kỳ để hiện thông báo vàng xác nhận
interface AssignmentNotice {
  id: string;
  sourceTerms: ExtractedTerm[];
  targetGrade: string;
  semester: 1 | 2 | "all";
  subjectCount: number;
}

// "Lớp 12 HK1" → { Lớp 12, 1 }; "Lớp 12 - Cả năm" → { Lớp 12, all }; "HK1" -> { null, 1 }; nếu không có gì -> { null, null }
function resolveTerm(termName: string): TermTarget {
  if (!termName || !termName.trim()) {
    return { gradeName: null, semester: null };
  }
  const trimmed = termName.trim();

  // 1. Kiểm tra format "Lớp 10 HK1" / "Lớp 11 - Học kỳ 2" / "HK1" / "Kỳ 2"
  const m = /^(.*?)\s*-?\s*(?:HK|Học kỳ|Kỳ)\s*([12])\s*$/i.exec(trimmed);
  if (m) {
    const rawGrade = m[1].trim();
    const sem: 1 | 2 = m[2] === "2" ? 2 : 1;
    return {
      gradeName: rawGrade ? rawGrade : null,
      semester: sem,
    };
  }

  // 2. Kiểm tra "Lớp 10 Cả năm" / "Lớp 12 - Ca nam" / "Cả năm"
  const allYear = /^(.*?)\s*[-–]?\s*(cả năm|ca nam)\s*$/i.exec(trimmed);
  if (allYear) {
    const rawGrade = allYear[1].trim();
    return {
      gradeName: rawGrade ? rawGrade : null,
      semester: "all",
    };
  }

  // 3. Chỉ có tên lớp: "Lớp 10", "Lớp 11", "Năm 1"
  if (/^(lop|nam)\s*\d+$/i.test(trimmed)) {
    return { gradeName: trimmed, semester: "all" };
  }

  return { gradeName: null, semester: null };
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
  useGradeScale(); // GPA thang 4 quy đổi theo bảng moderator sửa, vẽ lại khi bảng tải xong
  // Khởi tạo các năm học dựa theo bậc học & lớp hiện tại của người dùng
  const defaultYears = useMemo(
    () => getInitialAcademicYears(currentEducationLevel, currentGrade),
    [currentEducationLevel, currentGrade]
  );

  // Khối lớp cao nhất được phép hiển thị dựa trên khai báo ở Bước 1
  const maxAllowedGradeName = useMemo(() => {
    if (!defaultYears || defaultYears.length === 0) return null;
    return defaultYears[defaultYears.length - 1]?.gradeName ?? null;
  }, [defaultYears]);

  const isGrade9 = Boolean(
    currentGrade === "Lớp 9" || (currentGrade && normalizeGrade(currentGrade) === "lop 9")
  );

  const [isGrade9EarlyRoadmap, setIsGrade9EarlyRoadmap] = useState<boolean>(() => {
    if (!isGrade9) return false;
    return Boolean(data.academicYears?.some((y) => y.subjects.some((s) => s.subjectName === "GPA Mục Tiêu")));
  });

  // Học sinh THCS Lớp 8 trở xuống HOẶC học sinh Lớp 9 đã xóa khối Lớp 9 vì chưa có điểm:
  // Chuyển sang giao diện thiết lập mục tiêu 4 năm cấp 3
  const isMiddleSchoolEarly = Boolean(
    (currentEducationLevel === "middle_school" &&
      (currentGrade === "Lớp 6" ||
        currentGrade === "Lớp 7" ||
        currentGrade === "Lớp 8" ||
        !currentGrade ||
        currentGrade.includes("6") ||
        currentGrade.includes("7") ||
        currentGrade.includes("8"))) ||
    (isGrade9 && isGrade9EarlyRoadmap)
  );

  const prevGradeRef = useRef(currentGrade);
  const [userRemovedGrades, setUserRemovedGrades] = useState<string[]>([]);

  // Khi người dùng thay đổi lớp ở Bước 1 (ví dụ từ Lớp 9 sang Lớp 10, 11 hay 12),
  // reset lại trạng thái xóa để Bước 2 tự động hiển thị đầy đủ các lớp từ Lớp 9 đến lớp đã chọn.
  useEffect(() => {
    if (prevGradeRef.current !== currentGrade) {
      prevGradeRef.current = currentGrade;
      setUserRemovedGrades([]);
      setIsGrade9EarlyRoadmap(false);
    }
  }, [currentGrade]);

  // Luôn bắt đầu từ defaultYears (chứa đầy đủ các lớp từ Lớp 9 đến currentGrade).
  // Nếu người dùng chủ động xóa khối lớp nào ở Bước 2 thì loại ra; đồng thời giữ nguyên điểm đã có trong data.academicYears.
  const years = useMemo<AcademicYearDraft[]>(() => {
    if (isMiddleSchoolEarly) {
      if (
        data.academicYears &&
        data.academicYears.length > 0 &&
        data.academicYears.some((y) => y.subjects.some((s) => s.subjectName === "GPA Mục Tiêu"))
      ) {
        return data.academicYears;
      }
      return ["Lớp 9", "Lớp 10", "Lớp 11", "Lớp 12"].map((g) => ({
        gradeName: g,
        subjects: [
          {
            subjectName: "GPA Mục Tiêu",
            semester1Score: null,
            semester2Score: null,
          },
        ],
      }));
    }

    // Lọc theo các khối lớp chưa bị người dùng bấm xóa
    const activeGradeTemplates = defaultYears.filter(
      (dy) => !userRemovedGrades.includes(normalizeGrade(dy.gradeName))
    );

    return activeGradeTemplates.map((dy) => {
      const existing = data.academicYears?.find(
        (ay) => normalizeGrade(ay.gradeName) === normalizeGrade(dy.gradeName)
      );
      if (existing) {
        return existing;
      }
      return dy;
    });
  }, [isMiddleSchoolEarly, data.academicYears, defaultYears, userRemovedGrades]);

  // Đồng bộ lại data.academicYears lên flow state khi danh sách khối lớp thay đổi
  useEffect(() => {
    if (isMiddleSchoolEarly) return;
    const currentKeys = (data.academicYears ?? []).map((y) => normalizeGrade(y.gradeName)).join(",");
    const expectedKeys = years.map((y) => normalizeGrade(y.gradeName)).join(",");
    if (currentKeys !== expectedKeys) {
      onUpdate({ academicYears: years });
    }
  }, [years, data.academicYears, onUpdate, isMiddleSchoolEarly]);

  const [activeYearIndexRaw, setActiveYearIndex] = useState(0);
  // Clamp activeYearIndex trong phạm vi hợp lệ — tính thuần túy, không dùng effect
  const activeYearIndex = Math.min(activeYearIndexRaw, Math.max(0, years.length - 1));

  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [ocrFailedNotice, setOcrFailedNotice] = useState<string | null>(null);
  const [ocrSuccessNotice, setOcrSuccessNotice] = useState<string | null>(null);
  const [ocrWarnings, setOcrWarnings] = useState<string[]>([]);
  const [pendingTerms, setPendingTerms] = useState<ExtractedTerm[] | null>(null);
  // Lớp đã chọn khi gán kỳ (bước 1: chọn lớp; bước 2: chọn kỳ nếu cần)
  const [pendingGrade, setPendingGrade] = useState<string | null>(null);
  const [pendingAssignStep, setPendingAssignStep] = useState<PendingAssignStep>("choose_grade");
  const [assignmentNotices, setAssignmentNotices] = useState<AssignmentNotice[]>([]);
  // Lớp đang bấm "Sai lớp" (mở hộp chọn lớp đúng)
  const [reassignTargetId, setReassignTargetId] = useState<string | null>(null);
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
  const [showEstimator, setShowEstimator] = useState(false);
  const [isAddSubjectOpen, setIsAddSubjectOpen] = useState(false);
  const [newSubjectName, setNewSubjectName] = useState("");
  const [addSubjectError, setAddSubjectError] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{
    title: string;
    description: string;
    confirmLabel: string;
    onConfirm: () => void;
  } | null>(null);
  const fileInputId = useId();
  const overallInputId = useId();

  const gpaModeOptions = useMemo<{ value: GpaMode; label: string; description: string }[]>(() => {
    if (isMiddleSchoolEarly) {
      return [
        {
          value: "overall" as const,
          label: "Nhập 1 số GPA mục tiêu",
          description: "Điền một điểm GPA mục tiêu chung duy nhất.",
        },
        {
          value: "transcript" as const,
          label: "Lộ trình mục tiêu 4 năm (Lớp 9 - 12)",
          description: "Tự đặt mục tiêu điểm cho từng năm để thấy lộ trình tiến bộ.",
        },
      ];
    }
    return GPA_MODE_OPTIONS;
  }, [isMiddleSchoolEarly]);

  // Lấy điểm mục tiêu của một năm (cho học sinh THCS)
  const getYearTargetScore = (y: AcademicYearDraft): number | null => {
    const targetSub = y.subjects.find((s) => s.subjectName === "GPA Mục Tiêu");
    if (targetSub && targetSub.semester1Score != null) return targetSub.semester1Score;
    // Không có dòng mục tiêu: TB các môn, mỗi môn = TB cả năm (HK2 hệ số 2). Chỉ làm tròn số hiển thị
    const averages = y.subjects
      .map((s) => yearAverage(s.semester1Score ?? null, s.semester2Score ?? null))
      .filter((avg): avg is number => avg !== null);
    if (averages.length === 0) return null;
    const mean = averages.reduce((sum, avg) => sum + avg, 0) / averages.length;
    return Math.round(mean * 10) / 10;
  };

  // Cập nhật điểm mục tiêu của một năm cho học sinh THCS
  const handleYearTargetChange = (yearIdx: number, valStr: string) => {
    const val = valStr.trim() === "" ? null : Number(valStr);
    const numVal = val === null || Number.isNaN(val) ? null : val;
    setValidationError(null);
    const updatedYears = years.map((y, idx) => {
      if (idx !== yearIdx) return y;
      return {
        ...y,
        subjects: [
          {
            subjectName: "GPA Mục Tiêu",
            semester1Score: numVal,
            semester2Score: numVal,
          },
        ],
      };
    });
    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
  };

  const { gpa10, gpa4 } = calculateTranscriptGpa(years);
  const reviewCount = countReviewCells(years);
  const gradeTrend = calculateGradeTrend(years);

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
      setConfirmAction({
        title: "Chuyển sang nhập GPA tổng?",
        description: "Chuyển sang nhập GPA tổng sẽ xóa điểm các môn đã nhập. Bạn có chắc không?",
        confirmLabel: "Chuyển và xóa điểm",
        onConfirm: () => {
          onUpdate({ gpaMode: "overall", academicYears: [], manualGpa10Scale: null, calculatedGpa4Scale: null });
          setShowManualTable(false);
        },
      });
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

    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
  };

  const openAddSubjectDialog = (yearIdx: number) => {
    setActiveYearIndex(yearIdx);
    setNewSubjectName("");
    setAddSubjectError(null);
    setIsAddSubjectOpen(true);
  };

  const handleConfirmAddSubject = () => {
    const trimmed = newSubjectName.trim();
    if (!trimmed) {
      setAddSubjectError("Vui lòng nhập tên môn học.");
      return;
    }

    const currentYear = years[activeYearIndex];
    if (currentYear?.subjects.some((s) => s.subjectName.toLowerCase() === trimmed.toLowerCase())) {
      setAddSubjectError(`Môn "${trimmed}" đã có trong bảng điểm của ${currentYear.gradeName}.`);
      return;
    }

    const updatedYears = years.map((y, yIdx) => {
      if (yIdx !== activeYearIndex) return y;
      return {
        ...y,
        subjects: [...y.subjects, { subjectName: trimmed, semester1Score: null, semester2Score: null }],
      };
    });

    onUpdate({ academicYears: updatedYears });
    setIsAddSubjectOpen(false);
    setNewSubjectName("");
    setAddSubjectError(null);
  };

  const handleRemoveSubject = (yearIdx: number, subIdx: number) => {
    const updatedYears = years.map((y, yIdx) => {
      if (yIdx !== yearIdx) return y;
      return {
        ...y,
        subjects: y.subjects.filter((_, sIdx) => sIdx !== subIdx),
      };
    });

    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
  };

  // Xóa khối lớp hiện tại (chỉ lớp hiện tại mới được xóa nếu học sinh chưa có điểm năm nay; lớp cũ không được xóa)
  // Đặc biệt: Với Lớp 9, nếu chưa có điểm kỳ nào thì xóa sẽ chuyển sang chế độ lộ trình mục tiêu 4 năm (như Lớp 8)
  const handleRemoveCurrentGradeYear = () => {
    if (!maxAllowedGradeName) return;

    // TH: Học sinh Lớp 9 xóa Lớp 9 vì chưa có điểm kỳ nào
    // -> Chuyển xuống như Lớp 8 (thiết lập lộ trình mục tiêu 4 năm Lớp 9 - 12)
    if (isGrade9) {
      const targetYear = years.find((y) => normalizeGrade(y.gradeName) === "lop 9");
      const hasScores = targetYear?.subjects.some(
        (s) => s.semester1Score !== null || s.semester2Score !== null
      );
      const applyGrade9EarlyRoadmap = () => {
        const roadmapYears: AcademicYearDraft[] = ["Lớp 9", "Lớp 10", "Lớp 11", "Lớp 12"].map((g) => ({
          gradeName: g,
          subjects: [
            {
              subjectName: "GPA Mục Tiêu",
              semester1Score: null,
              semester2Score: null,
            },
          ],
        }));

        onUpdate({
          gpaMode: "transcript",
          academicYears: roadmapYears,
          manualGpa10Scale: null,
          calculatedGpa4Scale: null,
        });
        setIsGrade9EarlyRoadmap(true);
        setActiveYearIndex(0);
        setValidationError(null);
      };
      if (hasScores) {
        setConfirmAction({
          title: "Chuyển sang lộ trình mục tiêu 4 năm?",
          description:
            "Khối Lớp 9 đang có điểm. Bạn có chắc muốn xóa và chuyển sang thiết lập lộ trình mục tiêu 4 năm (như Lớp 8) không?",
          confirmLabel: "Xóa và chuyển",
          onConfirm: applyGrade9EarlyRoadmap,
        });
        return;
      }
      applyGrade9EarlyRoadmap();
      return;
    }

    if (years.length <= 1) {
      setValidationError("Bảng điểm cần giữ lại ít nhất một năm học đã có điểm.");
      return;
    }

    const currentGradeNormalized = normalizeGrade(maxAllowedGradeName);
    const targetYear = years.find((y) => normalizeGrade(y.gradeName) === currentGradeNormalized);
    if (!targetYear) return;

    const hasScores = targetYear.subjects.some(
      (s) => s.semester1Score !== null || s.semester2Score !== null
    );

    const applyRemoveGradeYear = () => {
      setUserRemovedGrades((prev) => [...prev, currentGradeNormalized]);
      const updatedYears = years.filter((y) => normalizeGrade(y.gradeName) !== currentGradeNormalized);
      const newGpas = calculateTranscriptGpa(updatedYears);
      onUpdate({
        academicYears: updatedYears,
        manualGpa10Scale: newGpas.gpa10,
        calculatedGpa4Scale: newGpas.gpa4,
      });
      setActiveYearIndex((prev) => Math.max(0, Math.min(prev, updatedYears.length - 1)));
      setValidationError(null);
    };

    if (hasScores) {
      setConfirmAction({
        title: `Xóa khối ${targetYear.gradeName}?`,
        description: `Khối ${targetYear.gradeName} đang có điểm. Bạn có chắc muốn xóa khối này (vì chưa hoàn tất năm học) không?`,
        confirmLabel: "Xóa khối",
        onConfirm: applyRemoveGradeYear,
      });
      return;
    }

    applyRemoveGradeYear();
  };

  // Quay lại chế độ nhập bảng điểm Lớp 9 cho học sinh Lớp 9 đang ở chế độ lộ trình mục tiêu
  const handleSwitchToGrade9Transcript = () => {
    setUserRemovedGrades([]);
    const initialGrade9 = getInitialAcademicYears(currentEducationLevel, "Lớp 9");
    onUpdate({
      gpaMode: "transcript",
      academicYears: initialGrade9,
      manualGpa10Scale: null,
      calculatedGpa4Scale: null,
    });
    setIsGrade9EarlyRoadmap(false);
    setActiveYearIndex(0);
    setValidationError(null);
  };

  // Thêm lại khối lớp hiện tại (tối đa bằng đúng lớp đã khai báo ở Bước 1, không được vượt quá)
  const handleAddCurrentGradeYear = () => {
    if (!maxAllowedGradeName) return;
    const norm = normalizeGrade(maxAllowedGradeName);
    setUserRemovedGrades((prev) => prev.filter((g) => g !== norm));
    if (years.some((y) => normalizeGrade(y.gradeName) === norm)) return;

    const newYear: AcademicYearDraft = {
      gradeName: maxAllowedGradeName,
      subjects: DEFAULT_SUBJECTS.map((s) => ({
        subjectName: s,
        semester1Score: null,
        semester2Score: null,
      })),
    };

    const updatedYears = [...years, newYear].sort((a, b) => gradeOrder(a.gradeName) - gradeOrder(b.gradeName));
    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
    const newIdx = updatedYears.findIndex(
      (y) => normalizeGrade(y.gradeName) === norm
    );
    if (newIdx !== -1) setActiveYearIndex(newIdx);
    setValidationError(null);
  };

  // Bổ sung Lớp 9 cho học sinh có nháp chỉ có Lớp 10-12 (chuẩn 4 năm Common App Mỹ)
  const handleAddGrade9 = () => {
    if (years.some((y) => normalizeGrade(y.gradeName) === "lop 9")) return;
    const grade9Year: AcademicYearDraft = {
      gradeName: "Lớp 9",
      subjects: DEFAULT_SUBJECTS.map((s) => ({
        subjectName: s,
        semester1Score: null,
        semester2Score: null,
      })),
    };
    const updatedYears = [grade9Year, ...years].sort((a, b) => gradeOrder(a.gradeName) - gradeOrder(b.gradeName));
    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
    setActiveYearIndex(0);
  };

  // Gán 1-chạm khi ảnh bảng điểm thiếu thông tin khối lớp hoặc kỳ học
  // Logic phân nhánh:
  //   1. Có lớp được đọc ra -> Tự động điền thẳng theo kỳ đã đọc, không hỏi gì cả.
  //   2. Không có lớp nhưng có 2 kỳ (pendingTerms.length >= 2) -> Hỏi chọn lớp, chọn xong điền cả 2 kỳ luôn.
  //   3. Không có thông tin lớp lẫn kỳ:
  //       + Bước 1 ("choose_grade"): hỏi chọn lớp trước.
  //       + Bước 2 ("choose_semester"): hỏi chọn kỳ nào (HK1, HK2, Cả năm).
  //       + Nếu chọn lớp đã hoàn thành (past) mà chỉ điền 1 kỳ -> cảnh báo cần hoàn thiện học kỳ còn thiếu!
  const isCompletedGrade = (gradeName: string): boolean => {
    if (!maxAllowedGradeName) return false;
    return normalizeGrade(gradeName) !== normalizeGrade(maxAllowedGradeName);
  };

  const handleChooseGrade = (targetGradeName: string) => {
    if (!pendingTerms || pendingTerms.length === 0) return;

    // TH 1: Bảng điểm có 2 kỳ -> điền cả 2 kỳ vào lớp được chọn luôn, không hỏi kỳ
    if (pendingTerms.length >= 2) {
      const term1 = pendingTerms[0];
      const term2 = pendingTerms[1];
      const r1 = resolveTerm(term1.termName || "");
      const r2 = resolveTerm(term2.termName || "");
      let sem1Term = term1;
      let sem2Term = term2;
      if (r1.semester === 2 && r2.semester === 1) {
        sem1Term = term2;
        sem2Term = term1;
      }
      const syntheticTerms: ExtractedTerm[] = [
        { termName: `${targetGradeName} HK1`, scores: sem1Term.scores },
        { termName: `${targetGradeName} HK2`, scores: sem2Term.scores },
      ];
      const targetIdx = years.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(targetGradeName));
      const fallback = targetIdx !== -1 ? targetIdx : activeYearIndex;
      const updatedYears = mergeExtractedTerms(years, syntheticTerms, fallback);
      const newGpas = calculateTranscriptGpa(updatedYears);

      onUpdate({
        gpaMode: "transcript",
        academicYears: updatedYears,
        manualGpa10Scale: newGpas.gpa10,
        calculatedGpa4Scale: newGpas.gpa4,
      });

      if (targetIdx !== -1) setActiveYearIndex(targetIdx);
      setPendingTerms(null);
      setPendingGrade(null);
      setPendingAssignStep("choose_grade");
      setShowManualTable(true);
      setAssignmentNotices((prev) => [...prev, {
        id: `${normalizeGrade(targetGradeName)}-${Date.now()}`,
        sourceTerms: syntheticTerms,
        targetGrade: targetGradeName,
        semester: "all",
        subjectCount: syntheticTerms[0].scores.length + (syntheticTerms[1]?.scores.length || 0),
      }]);
      setOcrSuccessNotice(
        `Đã tự động điền cả 2 học kỳ vào ${targetGradeName}. Vui lòng kiểm tra lại điểm trong bảng bên dưới.`
      );
      return;
    }

    // TH 2: Chỉ có 1 term chưa rõ lớp
    const singleTerm = pendingTerms[0];
    const resolved = resolveTerm(singleTerm.termName || "");

    // Nếu term này OCR vốn đã đọc ra được kỳ cụ thể (ví dụ HK1, HK2, Cả năm):
    if (resolved.semester !== null) {
      const syntheticTerm: ExtractedTerm = {
        termName: `${targetGradeName} ${resolved.semester === "all" ? "Cả năm" : `HK${resolved.semester}`}`,
        scores: singleTerm.scores,
      };
      const targetIdx = years.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(targetGradeName));
      const fallback = targetIdx !== -1 ? targetIdx : activeYearIndex;
      const updatedYears = mergeExtractedTerms(years, [syntheticTerm], fallback);
      const newGpas = calculateTranscriptGpa(updatedYears);

      onUpdate({
        gpaMode: "transcript",
        academicYears: updatedYears,
        manualGpa10Scale: newGpas.gpa10,
        calculatedGpa4Scale: newGpas.gpa4,
      });

      if (targetIdx !== -1) setActiveYearIndex(targetIdx);
      setPendingTerms(null);
      setPendingGrade(null);
      setPendingAssignStep("choose_grade");
      setShowManualTable(true);
      const resolvedSemester = resolved.semester;
      setAssignmentNotices((prev) => [...prev, {
        id: `${normalizeGrade(targetGradeName)}-${Date.now()}`,
        sourceTerms: [syntheticTerm],
        targetGrade: targetGradeName,
        semester: resolvedSemester,
        subjectCount: singleTerm.scores.length,
      }]);

      if (resolved.semester !== "all" && isCompletedGrade(targetGradeName)) {
        const afterYear = updatedYears.find((y) => normalizeGrade(y.gradeName) === normalizeGrade(targetGradeName));
        const hasSem1 = afterYear?.subjects.some((s) => s.semester1Score !== null && s.semester1Score !== undefined);
        const hasSem2 = afterYear?.subjects.some((s) => s.semester2Score !== null && s.semester2Score !== undefined);
        if (!hasSem1 || !hasSem2) {
          const missing = !hasSem1 ? "Học kỳ 1" : "Học kỳ 2";
          setOcrSuccessNotice(
            `Đã điền điểm vào ${resolved.semester === 1 ? "Học kỳ 1" : "Học kỳ 2"} của ${targetGradeName}. ` +
            `⚠️ ${targetGradeName} là năm học đã kết thúc — bạn cần bổ sung thêm điểm ${missing} để hồ sơ hợp lệ trước khi tiếp tục!`
          );
          return;
        }
      }

      setOcrSuccessNotice(
        `Đã điền thành công điểm vào ${targetGradeName} (${resolved.semester === "all" ? "Cả năm" : `Học kỳ ${resolved.semester}`}). Vui lòng kiểm tra lại bảng điểm bên dưới.`
      );
      return;
    }

    // Nếu term hoàn toàn không có thông tin kỳ nào -> Chuyển sang bước hỏi kỳ
    setPendingGrade(targetGradeName);
    setPendingAssignStep("choose_semester");
  };

  const handleChooseSemester = (semester: 1 | 2 | "all") => {
    if (!pendingTerms || pendingTerms.length === 0 || !pendingGrade) return;
    const termToAssign = pendingTerms[0];
    const syntheticTerm: ExtractedTerm = {
      termName: `${pendingGrade} ${semester === "all" ? "Cả năm" : `HK${semester}`}`,
      scores: termToAssign.scores,
    };
    const targetIdx = years.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(pendingGrade));
    const fallback = targetIdx !== -1 ? targetIdx : activeYearIndex;
    const updatedYears = mergeExtractedTerms(years, [syntheticTerm], fallback);
    const newGpas = calculateTranscriptGpa(updatedYears);
    onUpdate({
      gpaMode: "transcript",
      academicYears: updatedYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });
    if (targetIdx !== -1) setActiveYearIndex(targetIdx);
    const remaining = pendingTerms.slice(1);
    setPendingTerms(remaining.length > 0 ? remaining : null);
    const chosenGrade = pendingGrade;
    setPendingGrade(null);
    setPendingAssignStep("choose_grade");
    setShowManualTable(true);
    setAssignmentNotices((prev) => [...prev, {
      id: `${normalizeGrade(chosenGrade)}-${Date.now()}`,
      sourceTerms: [syntheticTerm],
      targetGrade: chosenGrade,
      semester: semester,
      subjectCount: termToAssign.scores.length,
    }]);

    // Nếu lớp đã hoàn thành (past) và vừa gán chỉ 1 kỳ → cảnh báo cần điền HK còn thiếu
    if (semester !== "all" && isCompletedGrade(chosenGrade)) {
      const afterYear = updatedYears.find((y) => normalizeGrade(y.gradeName) === normalizeGrade(chosenGrade));
      const hasSem1 = afterYear?.subjects.some((s) => s.semester1Score !== null && s.semester1Score !== undefined);
      const hasSem2 = afterYear?.subjects.some((s) => s.semester2Score !== null && s.semester2Score !== undefined);
      if (!hasSem1 || !hasSem2) {
        const missing = !hasSem1 ? "Học kỳ 1" : "Học kỳ 2";
        setOcrSuccessNotice(
          `Đã điền điểm vào ${semester === 1 ? "Học kỳ 1" : "Học kỳ 2"} của ${chosenGrade}. ` +
          `⚠️ ${chosenGrade} là năm học đã kết thúc — bạn cần bổ sung thêm điểm ${missing} để hồ sơ hợp lệ trước khi tiếp tục!`
        );
        return;
      }
    }
    setOcrSuccessNotice(
      `Đã điền thành công điểm vào ${chosenGrade} (${semester === "all" ? "Cả năm" : `Học kỳ ${semester}`}). Vui lòng kiểm tra lại bảng điểm bên dưới.`
    );
  };

  const handleDismissPendingTerms = () => {
    setPendingTerms(null);
    setPendingGrade(null);
    setPendingAssignStep("choose_grade");
  };

  // "Sai lớp" trên thông báo của 1 lớp → chuyển toàn bộ điểm của lớp đó (giữ nguyên HK1/HK2 và cờ cam) sang lớp khác
  const handleReassignGrade = (oldGrade: string, newGradeName: string) => {
    const oldKey = normalizeGrade(oldGrade);
    const oldYear = years.find((y) => normalizeGrade(y.gradeName) === oldKey);
    if (!oldYear || normalizeGrade(newGradeName) === oldKey) {
      setReassignTargetId(null);
      return;
    }

    // 1. Gom điểm lớp cũ thành 2 kỳ để gộp sang lớp mới (cờ cam đi theo ô)
    const toScores = (field: "semester1Score" | "semester2Score") =>
      oldYear.subjects
        .filter((s) => typeof s[field] === "number")
        .map((s) => {
          const review = s[REVIEW_FIELD[field]];
          return {
            subject: s.subjectName,
            score: s[field] as number,
            needsReview: Boolean(review),
            reviewReason: review ?? null,
          };
        });
    const movedTerms: ExtractedTerm[] = [
      { termName: `${newGradeName} HK1`, scores: toScores("semester1Score") },
      { termName: `${newGradeName} HK2`, scores: toScores("semester2Score") },
    ].filter((t) => t.scores.length > 0);

    // 2. Xóa điểm ở lớp cũ (giữ danh sách môn)
    const clearedYears = years.map((y) =>
      normalizeGrade(y.gradeName) !== oldKey
        ? y
        : {
            ...y,
            subjects: y.subjects.map((s) => ({
              ...s,
              semester1Score: null,
              semester2Score: null,
              semester1Review: null,
              semester2Review: null,
            })),
          }
    );

    // 3. Gộp vào lớp mới
    const targetIdx = clearedYears.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(newGradeName));
    const finalYears = mergeExtractedTerms(clearedYears, movedTerms, targetIdx !== -1 ? targetIdx : 0);
    const newGpas = calculateTranscriptGpa(finalYears);

    onUpdate({
      gpaMode: "transcript",
      academicYears: finalYears,
      manualGpa10Scale: newGpas.gpa10,
      calculatedGpa4Scale: newGpas.gpa4,
    });

    const newIdx = finalYears.findIndex((y) => normalizeGrade(y.gradeName) === normalizeGrade(newGradeName));
    if (newIdx !== -1) setActiveYearIndex(newIdx);
    // Thông báo của lớp cũ chuyển sang lớp mới, gộp với thông báo sẵn có của lớp mới (nếu có)
    setAssignmentNotices((prev) =>
      prev.map((n) => (normalizeGrade(n.targetGrade) === oldKey ? { ...n, targetGrade: newGradeName } : n))
    );
    setReassignTargetId(null);
  };

  // "Xác nhận đúng hết" trên thông báo của 1 lớp → bỏ cờ cam của riêng lớp đó và đóng thông báo
  const handleConfirmGrade = (gradeName: string) => {
    const key = normalizeGrade(gradeName);
    onUpdate({
      academicYears: years.map((y) =>
        normalizeGrade(y.gradeName) !== key
          ? y
          : { ...y, subjects: y.subjects.map((s) => ({ ...s, semester1Review: null, semester2Review: null })) }
      ),
    });
    setAssignmentNotices((prev) => prev.filter((n) => normalizeGrade(n.targetGrade) !== key));
    setValidationError(null);
  };

  // Mỗi lớp đúng 1 thông báo: lớp vừa được điền điểm hoặc còn ô tô cam
  const gradeNotices = years
    .map((y) => {
      const key = normalizeGrade(y.gradeName);
      const reviewCells = countReviewCells([y]);
      const assigned = assignmentNotices.some((n) => normalizeGrade(n.targetGrade) === key);
      const hasSem1 = y.subjects.some((s) => typeof s.semester1Score === "number");
      const hasSem2 = y.subjects.some((s) => typeof s.semester2Score === "number");
      const subjectCount = y.subjects.filter(
        (s) => typeof s.semester1Score === "number" || typeof s.semester2Score === "number"
      ).length;
      return { gradeName: y.gradeName, reviewCells, assigned, hasSem1, hasSem2, subjectCount };
    })
    .filter((g) => (g.assigned || g.reviewCells > 0) && g.subjectCount > 0);

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
    setPendingTerms(null);
    setAssignmentNotices([]);

    try {
      // BE chỉ nhận 1 file mỗi lần tải
      const res = await profileFlowApi.uploadDraftTranscript(fileArray.slice(0, 1));
      onTranscriptUploaded?.(res.draftTranscript ?? null);

      if (res.warnings && res.warnings.length > 0) {
        setOcrWarnings(res.warnings);
      }

      const extractedTerms = (res.extractedTerms ?? []).filter((t) => t.scores?.length > 0);

      if (extractedTerms.length > 0) {
        // Tách term đã rõ năm học và term chưa rõ năm học để xử lý thông minh
        const knownTerms: ExtractedTerm[] = [];
        const ambiguousTerms: ExtractedTerm[] = [];

        for (const term of extractedTerms) {
          const resolved = resolveTerm(term.termName || "");
          const isKnownYear =
            resolved.gradeName !== null &&
            years.some((y) => normalizeGrade(y.gradeName) === normalizeGrade(resolved.gradeName!));

          if (isKnownYear) {
            knownTerms.push(term);
          } else {
            ambiguousTerms.push(term);
          }
        }

        let currentYears = years;

        if (knownTerms.length > 0) {
          currentYears = mergeExtractedTerms(currentYears, knownTerms, activeYearIndex);
          const newGpas = calculateTranscriptGpa(currentYears);
          onUpdate({
            gpaMode: "transcript",
            academicYears: currentYears,
            manualGpa10Scale: newGpas.gpa10,
            calculatedGpa4Scale: newGpas.gpa4,
          });
          setShowManualTable(true);

          // Tạo notice cho từng khối lớp riêng biệt (per-grade)
          const termsByGrade = new Map<string, { terms: ExtractedTerm[]; gradeName: string; semester: 1 | 2 | "all" }>();
          for (const term of knownTerms) {
            const resolved = resolveTerm(term.termName || "");
            const gName = resolved.gradeName || currentYears[0]?.gradeName || "Lớp 10";
            const key = normalizeGrade(gName);
            const existing = termsByGrade.get(key);
            if (existing) {
              existing.terms.push(term);
              if (existing.semester !== "all" && resolved.semester !== existing.semester) {
                existing.semester = "all";
              }
            } else {
              termsByGrade.set(key, { terms: [term], gradeName: gName, semester: resolved.semester || "all" });
            }
          }
          const newNotices: AssignmentNotice[] = [];
          for (const [, entry] of termsByGrade) {
            const totalScores = entry.terms.reduce((sum, t) => sum + (t.scores?.length || 0), 0);
            newNotices.push({
              id: `${normalizeGrade(entry.gradeName)}-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
              sourceTerms: entry.terms,
              targetGrade: entry.gradeName,
              semester: entry.semester,
              subjectCount: totalScores,
            });
          }
          setAssignmentNotices((prev) => [...prev, ...newNotices]);
        }

        if (ambiguousTerms.length > 0) {
          // Bảng điểm thiếu thông tin khối lớp -> hiện Quick Assignment để người dùng bấm chọn 1 chạm
          setPendingTerms(ambiguousTerms);
          setShowManualTable(true);
        } else {
          setPendingTerms(null);
          const filledYears = currentYears.filter((y) =>
            y.subjects.some((s) => s.semester1Score != null || s.semester2Score != null)
          );

          if (filledYears.length < currentYears.length) {
            const missingYears = currentYears
              .filter((y) => y.subjects.every((s) => s.semester1Score == null && s.semester2Score == null))
              .map((y) => y.gradeName);

            setOcrSuccessNotice(
              `Đã nhận diện thành công điểm của ${filledYears.map((y) => y.gradeName).join(", ")}. Lưu ý: Bạn đang còn trống ${missingYears.join(", ")}. Vui lòng tải thêm ảnh bảng điểm hoặc tự nhập các môn còn thiếu.`
            );
          } else {
            setOcrSuccessNotice(
              `Đã nhận diện thành công đầy đủ điểm các năm (${filledYears.map((y) => y.gradeName).join(", ")}) từ bảng điểm. Vui lòng kiểm tra lại điểm trong bảng bên dưới.`
            );
          }
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
        document.getElementById(overallInputId)?.focus();
        return;
      }
      if (overallValue < 0 || overallValue > Number(overallScale)) {
        setValidationError(`GPA thang ${overallScale} phải từ 0 đến ${overallScale}.`);
        document.getElementById(overallInputId)?.focus();
        return;
      }
      setValidationError(null);
      onNext();
      return;
    }

    // Đối với học sinh THCS (Lớp 8 trở xuống): Chỉ kiểm tra điểm mục tiêu các năm 9-12
    if (isMiddleSchoolEarly) {
      const missing = years.some((y) => getYearTargetScore(y) == null);
      if (missing) {
        setValidationError(
          "Vui lòng nhập điểm GPA mục tiêu (thang điểm 10) cho các năm từ Lớp 9 đến Lớp 12 để hoàn tất lộ trình."
        );
        return;
      }
      const invalid = years.some((y) => {
        const s = getYearTargetScore(y);
        return s != null && (s < 0 || s > 10);
      });
      if (invalid) {
        setValidationError("Điểm mục tiêu các năm phải nằm trong khoảng từ 0 đến 10.");
        return;
      }
      setValidationError(null);
      onNext();
      return;
    }

    if (pendingTerms && pendingTerms.length > 0) {
      setValidationError(
        "Bạn có bảng điểm vừa tải lên chưa được gán vào kỳ học. Vui lòng bấm chọn kỳ học tương ứng ở khung màu xanh bên dưới trước khi tiếp tục."
      );
      return;
    }

    if (reviewCount > 0) {
      setValidationError(
        `Bảng điểm còn ${reviewCount} ô tô cam cần kiểm tra (${years
          .filter((y) => y.subjects.some((s) => s.semester1Review || s.semester2Review))
          .map((y) => y.gradeName)
          .join(", ")}). Hãy đối chiếu với bảng điểm gốc rồi bấm "Xác nhận đúng hết" ở thông báo của từng lớp.`
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

    // Kiểm tra tính đầy đủ của các năm học:
    // 1. Các năm học đã hoàn thành (các lớp dưới lớp hiện tại, hoặc tất cả các lớp nếu đã xóa lớp hiện tại):
    //    BẮT BUỘC phải nhập đủ cả 2 kỳ (Học kỳ 1 VÀ Học kỳ 2).
    // 2. Chỉ duy nhất năm học hiện tại đang học dở (nếu còn giữ trong danh sách):
    //    Được phép chỉ nhập 1 kỳ (Học kỳ 1). Nếu chưa có điểm kỳ nào thì nhắc xóa khối này.
    for (let idx = 0; idx < years.length; idx++) {
      const y = years[idx];
      const hasSem1 = y.subjects.some((s) => s.semester1Score !== null && s.semester1Score !== undefined);
      const hasSem2 = y.subjects.some((s) => s.semester2Score !== null && s.semester2Score !== undefined);

      const isOngoingCurrentGrade = Boolean(
        maxAllowedGradeName && normalizeGrade(y.gradeName) === normalizeGrade(maxAllowedGradeName)
      );

      if (isOngoingCurrentGrade) {
        if (!hasSem1 && !hasSem2) {
          setValidationError(
            `Bạn đang để trống điểm của ${y.gradeName}. Nếu bạn chưa học xong hoặc chưa có điểm năm nay, vui lòng bấm “Xóa khối này” dưới bảng điểm ${y.gradeName} để tiếp tục với điểm các năm trước.`
          );
          setShowManualTable(true);
          setActiveYearIndex(idx);
          return;
        }
        if (!hasSem1 && hasSem2) {
          setValidationError(
            `Bạn đang nhập điểm Học kỳ 2 của ${y.gradeName} nhưng thiếu Học kỳ 1. Vui lòng bổ sung điểm Học kỳ 1.`
          );
          setShowManualTable(true);
          setActiveYearIndex(idx);
          return;
        }
        // hasSem1 = true -> Cho phép tiếp tục dù hasSem2 = false (đang học dở năm nay)
      } else {
        if (!hasSem1 && !hasSem2) {
          setValidationError(
            `Hồ sơ cần có điểm của ${y.gradeName}. Vui lòng nhập điểm hoặc tải ảnh bảng điểm của năm học này.`
          );
          setShowManualTable(true);
          setActiveYearIndex(idx);
          return;
        }
        if (!hasSem1) {
          setValidationError(
            `Năm học ${y.gradeName} đã kết thúc nên bắt buộc phải có đủ điểm cả 2 kỳ. Bạn đang thiếu điểm Học kỳ 1 của ${y.gradeName}.`
          );
          setShowManualTable(true);
          setActiveYearIndex(idx);
          return;
        }
        if (!hasSem2) {
          setValidationError(
            `Năm học ${y.gradeName} đã kết thúc nên bắt buộc phải có đủ điểm cả 2 kỳ. Bạn đang thiếu điểm Học kỳ 2 của ${y.gradeName}.`
          );
          setShowManualTable(true);
          setActiveYearIndex(idx);
          return;
        }
      }
    }

    setValidationError(null);
    onNext();
  };

  // Các dòng thao tác dưới bảng điểm (xóa / thêm lại khối lớp), chỉ để dựng giao diện
  const activeYear = years[activeYearIndex];
  const activeIsCurrent = Boolean(
    activeYear && maxAllowedGradeName && normalizeGrade(activeYear.gradeName) === normalizeGrade(maxAllowedGradeName)
  );
  const canAddCurrentYear = Boolean(
    maxAllowedGradeName && !years.some((y) => normalizeGrade(y.gradeName) === normalizeGrade(maxAllowedGradeName))
  );
  const canAddGrade9 = !years.some((y) => normalizeGrade(y.gradeName) === "lop 9") && !isGrade9;
  const hasYearActions = activeIsCurrent || canAddCurrentYear || canAddGrade9;
  const estimatorPresets = overallScale === "10" ? ESTIMATOR_PRESETS_10 : ESTIMATOR_PRESETS_4;
  const overallLabel = isMiddleSchoolEarly ? "GPA mục tiêu" : "GPA tổng";

  const modeTabs = (
    <GroupTabs options={gpaModeOptions} value={mode} onChange={handleModeChange} ariaLabel="Cách nhập GPA" />
  );
  // Số GPA hiện tại: GPA tổng thang 4 thì chưa có số thang 10; nhập từng môn thì lấy số hệ thống đã tính
  const shownGpa10 = mode === "overall" ? (overallScale === "10" ? overallValue : null) : gpa10;
  const shownGpa4 =
    mode === "overall"
      ? overallScale === "10"
        ? overallValue != null
          ? toGpa4(overallValue)
          : null
        : overallValue
      : gpa4;
  const fmtGpa = (v: number | null | undefined) => (v != null ? v.toFixed(2) : "—");
  const gpaFooter = (
    <div className="space-y-3">
      <GroupResult
        label={isMiddleSchoolEarly ? "GPA mục tiêu của bạn là" : "Điểm trung bình của bạn là"}
        items={[
          { label: "Thang 10", value: fmtGpa(shownGpa10) },
          { label: "Quy đổi thang 4", value: fmtGpa(shownGpa4) },
        ]}
      />
      {mode !== "overall" && shownGpa4 !== null && <GpaConversionNote />}
    </div>
  );

  return (
    <>
      <StepHeader title="Bảng điểm của bạn" />
      <FormBody>
        {mode === "overall" ? (
          <FormGroup title={overallLabel} tabs={modeTabs} footer={gpaFooter}>
            <FormSection title="Thang điểm">
              <ChoicePicker
                ariaLabel="Thang điểm"
                fill
                options={[
                  { value: "10", label: "Thang 10" },
                  { value: "4", label: "Thang 4" },
                ]}
                value={overallScale}
                onChange={(v) => v && handleOverallScaleChange(v as GpaScale)}
              />
            </FormSection>

            <FormSection
              title={`${overallLabel} (0–${overallScale})`}
              required
              description={
                isMiddleSchoolEarly
                  ? "Điểm trung bình kỳ vọng chung cho bậc THPT."
                  : "Điểm trung bình chung trên học bạ hoặc bảng điểm."
              }
              aside={overallScale === "10" ? <GpaConversionNote /> : undefined}
              error={validationError}
            >
              <Input
                id={overallInputId}
                aria-label={`${overallLabel} (0–${overallScale})`}
                type="number"
                inputMode="decimal"
                min={0}
                max={Number(overallScale)}
                step={0.01}
                placeholder={overallScale === "10" ? "VD: 8.5" : "VD: 3.4"}
                value={overallValue ?? ""}
                onChange={(e) => handleOverallChange(e.target.value)}
                aria-invalid={Boolean(validationError) || undefined}
                className="h-10 w-full"
              />
              {overallScale === "10" ? (
                <div className="mt-1.5 text-xs text-muted-foreground">
                  Quy đổi thang 4:{" "}
                  <strong className="text-brand">
                    {data.manualGpa10Scale != null ? toGpa4(data.manualGpa10Scale).toFixed(2) : "—"}
                  </strong>
                </div>
              ) : (
                <div className="mt-1.5 text-xs text-muted-foreground">
                  GPA thang 10: <strong className="text-foreground">Chưa có dữ liệu</strong>
                </div>
              )}
            </FormSection>

            <FormSection
              title="Chưa rõ GPA?"
              description="Chọn mức gần đúng nhất, hệ thống sẽ điền giúp bạn."
              open={showEstimator}
              onToggle={() => setShowEstimator((v) => !v)}
            >
              <ChoiceCards
                ariaLabel="Mức học lực"
                options={estimatorPresets.map((p) => ({
                  value: p.range,
                  label: `${p.label} (${p.range})`,
                  description: p.desc,
                }))}
                value={estimatorPresets.find((p) => p.midpoint === overallValue)?.range ?? null}
                onChange={(range) => {
                  const preset = estimatorPresets.find((p) => p.range === range);
                  if (preset) handleOverallChange(String(preset.midpoint));
                }}
              />
            </FormSection>
          </FormGroup>
        ) : isMiddleSchoolEarly ? (
          <>
            {/* Lộ trình mục tiêu 4 năm cho học sinh THCS (Lớp 8 trở xuống hoặc Lớp 9 chưa có điểm) */}
            <FormGroup title="Điểm mục tiêu" tabs={modeTabs} footer={gpaFooter}>
            <FormSection
              title="Điểm mục tiêu 4 năm cấp 3"
              required
              wide
              description={
                isGrade9 && isGrade9EarlyRoadmap
                  ? "Bạn chưa có điểm Lớp 9 nên đặt mục tiêu cho cả 4 năm. Nhập GPA thang 10 bạn muốn đạt mỗi năm."
                  : "Đại học Mỹ xét điểm từ Lớp 9 đến Lớp 12. Nhập GPA thang 10 bạn muốn đạt mỗi năm."
              }
              error={validationError}
            >
              <div className="overflow-hidden rounded-xl border border-line bg-surface">
                <table className={DATA_TABLE}>
                  <thead className="border-b border-line bg-(--surface-2) text-xs font-medium text-ink-2">
                    <tr>
                      <th className="px-3 py-2.5 text-left">Năm học</th>
                      <th className="w-44 px-3 py-2.5 text-center">GPA mục tiêu (0–10)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {years.map((y, yIdx) => {
                      const targetScore = getYearTargetScore(y);
                      const usYearName =
                        yIdx === 0 ? "Freshman" : yIdx === 1 ? "Sophomore" : yIdx === 2 ? "Junior" : "Senior";
                      return (
                        <tr key={y.gradeName}>
                          <td className="px-3 py-2 font-medium text-ink">
                            {y.gradeName} <span className="font-normal text-ink-2">· {usYearName}</span>
                          </td>
                          <td className="p-0">
                            <input
                              type="number"
                              min={0}
                              max={10}
                              step={0.1}
                              placeholder="VD: 8.5"
                              value={targetScore ?? ""}
                              onChange={(e) => handleYearTargetChange(yIdx, e.target.value)}
                              aria-label={`GPA mục tiêu ${y.gradeName}`}
                              className={cn(INLINE_INPUT, NUMBER_INPUT)}
                            />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              {isGrade9 && isGrade9EarlyRoadmap && (
                <ActionList>
                  <ActionRow icon={<ArrowLeftIcon />} onClick={handleSwitchToGrade9Transcript}>
                    Nhập bảng điểm Lớp 9
                  </ActionRow>
                </ActionList>
              )}
            </FormSection>

              {gradeTrend.hasEnoughData && (
                <FormSection title="Xu hướng điểm mục tiêu">
                  <TrendSummary trend={gradeTrend} />
                </FormSection>
              )}
            </FormGroup>
          </>
        ) : (
          <>
            <FormGroup title="Học bạ" tabs={modeTabs} footer={gpaFooter}>
            <FormSection
              title="Học bạ của bạn"
              description="Tải PDF hoặc ảnh chụp, hệ thống tự điền điểm vào bảng bên dưới."
              wide
            >
              <input
                id={fileInputId}
                type="file"
                accept={TRANSCRIPT_ACCEPT}
                className="hidden"
                onChange={(e) => e.target.files && handleFileUpload(e.target.files)}
              />
              <div
                role="group"
                aria-label="Tải học bạ"
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
                  "rounded-xl border border-input bg-surface p-4",
                  dragOver && "border-brand bg-brand-wash/40"
                )}
              >
                {isUploading ? (
                  <div role="status" className="flex items-center gap-3 py-2 text-sm text-ink">
                    <Loader2Icon className="size-5 shrink-0 animate-spin text-brand" />
                    Đang đọc bảng điểm, vui lòng chờ trong giây lát…
                  </div>
                ) : (
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex min-w-0 items-center gap-3">
                      <span
                        aria-hidden
                        className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-(--surface-2) text-ink-2"
                      >
                        {draftTranscript ? <FileTextIcon className="size-5" /> : <UploadCloudIcon className="size-5" />}
                      </span>
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-ink">
                          {draftTranscript ? draftTranscript.originalFileName : "Kéo thả học bạ vào đây"}
                        </p>
                        <p className="text-xs text-ink-2">
                          {draftTranscript
                            ? "Đã đọc xong. Kiểm tra lại điểm trong bảng bên dưới."
                            : "PDF, PNG hoặc JPG, tối đa 10MB"}
                        </p>
                      </div>
                    </div>
                    <Button
                      type="button"
                      variant={draftTranscript ? "outline" : "default"}
                      onClick={triggerFileInput}
                      className="shrink-0 gap-2"
                    >
                      <FileUpIcon className="size-4" />
                      {draftTranscript ? "Tải tệp khác" : "Chọn tệp từ máy"}
                    </Button>
                  </div>
                )}
              </div>

              {uploadError && <FieldError>{uploadError}</FieldError>}

              {ocrWarnings.length > 0 && (
                <Alert className="border-warn/40 bg-warn-wash/40">
                  <AlertTriangleIcon className="size-4 text-warn" />
                  <AlertTitle className="text-sm font-semibold text-ink">Lưu ý khi đọc bảng điểm</AlertTitle>
                  <AlertDescription className="space-y-1 text-sm text-ink-2">
                    {ocrWarnings.map((w, idx) => (
                      <div key={idx}>• {w}</div>
                    ))}
                  </AlertDescription>
                </Alert>
              )}

              {ocrSuccessNotice && (
                <Alert className="border-brand/40 bg-brand-wash/40">
                  <CheckCircle2Icon className="size-4 text-brand" />
                  <AlertTitle className="text-sm font-semibold text-ink">Đã trích xuất điểm thành công</AlertTitle>
                  <AlertDescription className="text-sm text-ink-2">{ocrSuccessNotice}</AlertDescription>
                </Alert>
              )}

              {ocrFailedNotice && (
                <Alert className="border-warn/40 bg-warn-wash/40">
                  <AlertTriangleIcon className="size-4 text-warn" />
                  <AlertTitle className="text-sm font-semibold text-ink">Chưa nhận diện được bảng điểm</AlertTitle>
                  <AlertDescription className="space-y-3 text-sm text-ink-2">
                    <p>{ocrFailedNotice}</p>
                    <div className="flex flex-wrap gap-2">
                      <Button type="button" size="sm" onClick={triggerFileInput} className="gap-1.5">
                        <RefreshCwIcon className="size-3.5" />
                        Chọn tệp rõ nét hơn
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setShowManualTable(true);
                          setOcrFailedNotice(null);
                        }}
                        className="gap-1.5"
                      >
                        <Edit3Icon className="size-3.5" />
                        Điền điểm bằng tay
                      </Button>
                    </div>
                  </AlertDescription>
                </Alert>
              )}

              {/* Gán nhanh khi bảng điểm thiếu khối lớp / học kỳ */}
              {pendingTerms && pendingTerms.length > 0 && (
                <div className="space-y-4 rounded-xl border border-brand/40 bg-brand-wash/40 p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <h3 className="text-sm font-semibold text-ink">
                        {pendingTerms.length >= 2
                          ? `Đã nhận diện bảng điểm 2 học kỳ (${
                              pendingTerms[0].scores.length + (pendingTerms[1]?.scores.length || 0)
                            } môn)`
                          : pendingAssignStep === "choose_semester"
                          ? `Chọn học kỳ cho ${pendingGrade}`
                          : `Đã nhận diện bảng điểm (${pendingTerms[0].scores.length} môn)`}
                      </h3>
                      <p className="mt-1 text-sm text-ink-2">
                        {pendingTerms.length >= 2
                          ? "Chưa rõ khối lớp. Chọn lớp để điền cả 2 kỳ."
                          : pendingAssignStep === "choose_semester"
                          ? `Bạn muốn điền vào học kỳ nào của ${pendingGrade}?`
                          : "Chưa rõ khối lớp. Chọn lớp bạn muốn điền điểm."}
                      </p>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-xs"
                      onClick={handleDismissPendingTerms}
                      className="text-ink-2 hover:text-ink"
                      title="Bỏ qua"
                      aria-label="Bỏ qua"
                    >
                      <XIcon className="size-4" />
                    </Button>
                  </div>

                  {pendingTerms.length >= 2 || pendingAssignStep === "choose_grade" ? (
                    <ChoicePicker
                      ariaLabel={pendingTerms.length >= 2 ? "Chọn lớp để điền cả 2 kỳ" : "Chọn lớp"}
                      options={years.map((y) => ({ value: y.gradeName, label: y.gradeName }))}
                      value={null}
                      onChange={(v) => v && handleChooseGrade(v)}
                    />
                  ) : (
                    <div className="space-y-3">
                      {pendingGrade && isCompletedGrade(pendingGrade) && (
                        <p className="flex items-start gap-2 text-sm text-warn">
                          <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" />
                          <span>
                            {pendingGrade} là năm học đã kết thúc nên cần đủ điểm cả 2 học kỳ. Nếu chỉ điền 1 kỳ, bạn
                            sẽ cần bổ sung kỳ còn lại trước khi tiếp tục.
                          </span>
                        </p>
                      )}
                      <div className="flex flex-wrap items-center gap-3">
                        <ChoicePicker
                          ariaLabel={`Điền vào ${pendingGrade}`}
                          options={[
                            { value: "1", label: "Học kỳ 1" },
                            { value: "2", label: "Học kỳ 2" },
                            { value: "all", label: "Cả năm" },
                          ]}
                          value={null}
                          onChange={(v) => v && handleChooseSemester(v === "all" ? "all" : (Number(v) as 1 | 2))}
                        />
                        <Button
                          type="button"
                          size="sm"
                          variant="ghost"
                          onClick={() => {
                            setPendingGrade(null);
                            setPendingAssignStep("choose_grade");
                          }}
                          className="text-ink-2 hover:text-ink"
                        >
                          Chọn lại lớp
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </FormSection>

            <FormSection
              title="Điểm từng môn"
              description={`Các năm học tính đến ${currentGrade || "lớp hiện tại"}. Điểm theo thang 10 của Việt Nam, sửa trực tiếp trong bảng nếu cần.`}
              error={validationError}
              wide
            >
              {/* Mỗi lớp đúng 1 thông báo: vừa được điền điểm hoặc còn ô tô cam */}
              {gradeNotices.map((g) => (
                <div key={g.gradeName} className="space-y-3 rounded-xl border border-warn/40 bg-warn-wash/40 p-4">
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-ink">
                      {g.hasSem1 && g.hasSem2
                        ? `Đã nhập đủ ${g.gradeName}`
                        : `Đã nhập ${g.gradeName} (${g.hasSem1 ? "Học kỳ 1" : "Học kỳ 2"})`}
                    </p>
                    <p className="text-sm text-ink-2">
                      <strong className="font-semibold text-ink">{g.subjectCount} môn học</strong> đang ở{" "}
                      <strong className="font-semibold text-ink">{g.gradeName}</strong>. Đối chiếu với bảng điểm gốc xem
                      đúng lớp và học kỳ chưa.
                    </p>
                    {g.reviewCells > 0 && (
                      <p className="text-sm text-ink-2">
                        Có {g.reviewCells} ô tô cam (điểm đọc từ ảnh, chênh lệch bất thường hoặc bị ghi đè / trùng môn),
                        rê chuột vào ô để xem lý do.
                      </p>
                    )}
                    {isCompletedGrade(g.gradeName) && !(g.hasSem1 && g.hasSem2) && (
                      <p className="text-sm font-medium text-risk">
                        {g.gradeName} là năm học đã kết thúc. Bạn cần bổ sung học kỳ còn lại để hồ sơ hợp lệ.
                      </p>
                    )}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setReassignTargetId(g.gradeName)}
                      className="gap-1.5"
                    >
                      <RefreshCwIcon className="size-3.5" />
                      Sai lớp
                    </Button>
                    <Button type="button" size="sm" onClick={() => handleConfirmGrade(g.gradeName)} className="gap-1.5">
                      <CheckCircle2Icon className="size-3.5" />
                      Xác nhận đúng hết
                    </Button>
                  </div>
                </div>
              ))}

              {years.length > 0 && (
                <SegmentedControl
                  ariaLabel="Năm học"
                  options={years.map((y, idx) => ({ value: String(idx), label: y.gradeName }))}
                  value={String(activeYearIndex)}
                  onChange={(v) => setActiveYearIndex(Number(v))}
                />
              )}

              {activeYear && (
                <div className="overflow-hidden rounded-xl border border-line bg-surface">
                  <table className={DATA_TABLE}>
                    <thead className="border-b border-line bg-(--surface-2) text-xs font-medium text-ink-2">
                      <tr>
                        <th className="px-3 py-2.5 text-left">Môn học</th>
                        <th className="w-28 px-3 py-2.5 text-center">Học kỳ 1</th>
                        <th className="w-28 px-3 py-2.5 text-center">Học kỳ 2</th>
                        <th className="w-24 px-3 py-2.5 text-center">Trung bình</th>
                        <th className="w-20 px-2 py-2.5 text-center">Thao tác</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-line">
                      {activeYear.subjects.map((sub, sIdx, subjects) => {
                        const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
                        const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
                        // TB cả năm (HK2 hệ số 2), làm tròn 1 chữ số chỉ để hiển thị
                        const yearAvg = yearAverage(s1, s2);
                        const avg = yearAvg === null ? null : Math.round(yearAvg * 10) / 10;

                        // Sau khi AI điền điểm, ô còn trống trong năm đã có điểm được tô vàng để nhắc kiểm tra
                        const flagEmpty =
                          ocrSuccessNotice !== null &&
                          subjects.some((x) => x.semester1Score != null || x.semester2Score != null);
                        const outOfRange = (v: number | null | undefined) => typeof v === "number" && (v < 0 || v > 10);
                        const cellClass = (empty: boolean, review?: string | null) =>
                          cn(
                            INLINE_INPUT,
                            NUMBER_INPUT,
                            flagEmpty && empty && "bg-warn-wash/40",
                            review && "bg-warn-wash ring-1 ring-warn/60"
                          );
                        const reviewTitle = (review?: string | null) =>
                          review ? REVIEW_LABELS[review] ?? "Ô này cần kiểm tra lại." : undefined;

                        return (
                          <tr key={sIdx}>
                            <td className="px-3 py-2 font-medium text-ink">{sub.subjectName}</td>
                            <td className="p-0">
                              <input
                                type="number"
                                min={0}
                                max={10}
                                step={0.1}
                                placeholder="0–10"
                                value={sub.semester1Score ?? ""}
                                onChange={(e) =>
                                  handleScoreChange(activeYearIndex, sIdx, "semester1Score", e.target.value)
                                }
                                aria-label={`${sub.subjectName}, học kỳ 1`}
                                aria-invalid={outOfRange(sub.semester1Score) || undefined}
                                className={cellClass(s1 === null, sub.semester1Review)}
                                title={reviewTitle(sub.semester1Review)}
                              />
                            </td>
                            <td className="p-0">
                              <input
                                type="number"
                                min={0}
                                max={10}
                                step={0.1}
                                placeholder="0–10"
                                value={sub.semester2Score ?? ""}
                                onChange={(e) =>
                                  handleScoreChange(activeYearIndex, sIdx, "semester2Score", e.target.value)
                                }
                                aria-label={`${sub.subjectName}, học kỳ 2`}
                                aria-invalid={outOfRange(sub.semester2Score) || undefined}
                                className={cellClass(s2 === null, sub.semester2Review)}
                                title={reviewTitle(sub.semester2Review)}
                              />
                            </td>
                            <td className="px-3 py-2 text-center font-semibold tabular-nums text-ink">
                              {avg !== null ? avg.toFixed(1) : "-"}
                            </td>
                            <td className="px-2 py-1 text-center">
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon-xs"
                                onClick={() => handleRemoveSubject(activeYearIndex, sIdx)}
                                className="text-ink-2 hover:text-destructive"
                                title="Xóa môn"
                                aria-label={`Xóa môn ${sub.subjectName}`}
                              >
                                <Trash2Icon className="size-3.5" />
                              </Button>
                            </td>
                          </tr>
                        );
                      })}
                      <AddRow colSpan={5} onClick={() => openAddSubjectDialog(activeYearIndex)}>
                        <PlusIcon className="size-4" />
                        Thêm môn học
                      </AddRow>
                    </tbody>
                  </table>
                </div>
              )}

              {hasYearActions && (
                <ActionList>
                  {activeIsCurrent && (
                    <ActionRow icon={<Trash2Icon />} onClick={handleRemoveCurrentGradeYear}>
                      {isGrade9
                        ? "Chưa có điểm Lớp 9? Chuyển sang lộ trình mục tiêu 4 năm"
                        : `Chưa có điểm ${activeYear?.gradeName}? Xóa khối này`}
                    </ActionRow>
                  )}
                  {canAddCurrentYear && (
                    <ActionRow icon={<PlusIcon />} onClick={handleAddCurrentGradeYear}>
                      Thêm {maxAllowedGradeName} (đang học)
                    </ActionRow>
                  )}
                  {canAddGrade9 && (
                    <ActionRow icon={<PlusIcon />} onClick={handleAddGrade9}>
                      Thêm Lớp 9 (chuẩn tuyển sinh Mỹ)
                    </ActionRow>
                  )}
                </ActionList>
              )}
            </FormSection>

              {gradeTrend.hasEnoughData && (
                <FormSection title="Xu hướng điểm số">
                  <TrendSummary trend={gradeTrend} />
                </FormSection>
              )}
            </FormGroup>
          </>
        )}
      </FormBody>

      {/* Hộp thêm môn học */}
      <Dialog open={isAddSubjectOpen} onOpenChange={setIsAddSubjectOpen}>
        <DialogContent variant="form" size="md">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleConfirmAddSubject();
            }}
            noValidate
            className="flex min-h-0 flex-1 flex-col gap-6"
          >
            <DialogHeader>
              <DialogIcon tone="brand">
                <BookOpenIcon aria-hidden />
              </DialogIcon>
              <DialogTitle>Thêm môn học</DialogTitle>
              <DialogDescription>
                Bổ sung môn vào bảng điểm {years[activeYearIndex]?.gradeName || "năm học này"}.
              </DialogDescription>
            </DialogHeader>

            <DialogBody className="space-y-5">
              <Field label="Tên môn học" htmlFor="new-subject-input" required error={addSubjectError}>
                <Input
                  id="new-subject-input"
                  autoFocus
                  placeholder="Ví dụ: Công nghệ, Tiếng Pháp, GDCD"
                  value={newSubjectName}
                  onChange={(e) => {
                    setNewSubjectName(e.target.value);
                    if (addSubjectError) setAddSubjectError(null);
                  }}
                  className={cn(addSubjectError && "border-destructive focus-visible:ring-destructive")}
                />
              </Field>

              <Field label="Môn tự chọn phổ biến">
                <ChoicePicker
                  ariaLabel="Môn tự chọn phổ biến"
                  maxChips={SUGGESTED_ADDITIONAL_SUBJECTS.length}
                  options={SUGGESTED_ADDITIONAL_SUBJECTS.filter(
                    (suggested) =>
                      !years[activeYearIndex]?.subjects.some(
                        (s) => s.subjectName.toLowerCase() === suggested.toLowerCase()
                      )
                  ).map((suggested) => ({ value: suggested, label: suggested }))}
                  value={newSubjectName}
                  onChange={(v) => {
                    setNewSubjectName(v ?? "");
                    if (addSubjectError) setAddSubjectError(null);
                  }}
                />
              </Field>
            </DialogBody>

            <DialogFooter layout="pair">
              <DialogCancel>Hủy</DialogCancel>
              <DialogAction type="submit" tone="primary">
                Thêm môn
              </DialogAction>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Hộp chọn lại lớp khi bấm "Sai lớp" */}
      <Dialog
        open={reassignTargetId !== null}
        onOpenChange={(open) => {
          if (!open) setReassignTargetId(null);
        }}
      >
        <DialogContent variant="form" size="md">
          <DialogHeader>
            <DialogIcon tone="brand">
              <ArrowRightLeftIcon aria-hidden />
            </DialogIcon>
            <DialogTitle>Chọn lại khối lớp</DialogTitle>
            <DialogDescription>
              Điểm đang được điền vào <strong className="text-ink">{reassignTargetId}</strong>. Chuyển toàn bộ điểm này
              sang lớp nào?
            </DialogDescription>
          </DialogHeader>

          <DialogBody>
            <ChoicePicker
              ariaLabel="Khối lớp"
              options={years.map((y) => ({ value: y.gradeName, label: y.gradeName }))}
              value={years.find((y) => reassignTargetId !== null && normalizeGrade(y.gradeName) === normalizeGrade(reassignTargetId))?.gradeName ?? null}
              onChange={(v) => {
                if (v && reassignTargetId) handleReassignGrade(reassignTargetId, v);
              }}
            />
          </DialogBody>

          <DialogFooter layout="single">
            <DialogCancel>Đóng</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AppDialog
        open={confirmAction !== null}
        onOpenChange={(open) => {
          if (!open) setConfirmAction(null);
        }}
        variant="action"
        icon={<AlertTriangleIcon aria-hidden />}
        iconTone="warning"
        title={confirmAction?.title ?? ""}
        description={confirmAction?.description}
        cancelLabel="Hủy"
        confirmLabel={confirmAction?.confirmLabel ?? "Xác nhận"}
        confirmTone="destructive"
        onConfirm={() => {
          confirmAction?.onConfirm();
          setConfirmAction(null);
        }}
      />

      <StepNav onPrev={onPrev}>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </>
  );
}

/** Ẩn nút tăng giảm của ô số để ô trong bảng gọn như ô văn bản */
const NUMBER_INPUT = "[appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none";

type GradeTrend = ReturnType<typeof calculateGradeTrend>;

/** Xu hướng điểm qua các năm: một dòng kết luận, lời giải thích và điểm từng năm */
function TrendSummary({ trend }: { trend: GradeTrend }) {
  const down = trend.trend === "downward";
  const label =
    trend.trend === "strong_upward"
      ? "Tiến bộ vượt trội"
      : trend.trend === "slight_upward"
      ? "Tiến bộ tích cực"
      : trend.trend === "stable"
      ? "Phong độ ổn định"
      : "Cần chú ý phục hồi";
  const Icon = down ? TrendingDownIcon : TrendingUpIcon;
  return (
    <div className="space-y-2">
      <p className={cn("flex items-center gap-2 text-sm font-medium", down ? "text-warn" : "text-ok")}>
        <Icon aria-hidden className="size-4" />
        {label}
      </p>
      <p className="text-sm text-ink-2">{trend.summaryMessage}</p>
      <p className="text-sm tabular-nums text-ink">
        {trend.yearlyAverages.map((y) => `${y.gradeName}: ${y.gpa10.toFixed(2)}`).join("  →  ")}
      </p>
    </div>
  );
}
