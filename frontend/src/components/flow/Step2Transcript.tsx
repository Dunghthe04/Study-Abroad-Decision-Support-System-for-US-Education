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
import type { AcademicYearDraft, SubjectDraft, TranscriptDraft } from "@/types/profile-flow";

interface Step2TranscriptProps {
  data: TranscriptDraft;
  currentEducationLevel?: string;
  currentGrade?: string;
  onUpdate: (updated: Partial<TranscriptDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

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
function getInitialAcademicYears(level?: string, grade?: string): AcademicYearDraft[] {
  let gradeNames: string[] = ["Lớp 10", "Lớp 11", "Lớp 12"];

  if (level === "Trung học cơ sở") {
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
  } else if (level === "Trung học phổ thông") {
    if (grade === "Lớp 12") {
      gradeNames = ["Lớp 10", "Lớp 11", "Lớp 12"];
    } else if (grade === "Lớp 11") {
      gradeNames = ["Lớp 9", "Lớp 10", "Lớp 11"];
    } else if (grade === "Lớp 10") {
      gradeNames = ["Lớp 8", "Lớp 9", "Lớp 10"];
    } else {
      gradeNames = ["Lớp 10", "Lớp 11", "Lớp 12"];
    }
  } else if (level === "Cao đẳng / Đại học") {
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
  } else if (level === "Thạc sĩ") {
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

// Helper tính GPA thang 4 từ thang 10 theo công thức tuyến tính: GPA 4.0 = GPA 10 / 10 * 4.0
function calculateLinearGpa(years: AcademicYearDraft[]): { gpa10: number | null; gpa4: number | null } {
  let totalScore10 = 0;
  let count = 0;

  for (const yr of years) {
    for (const sub of yr.subjects) {
      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;

      let subAvg: number | null = null;
      if (s1 !== null && s2 !== null) {
        subAvg = (s1 + s2) / 2;
      } else if (s1 !== null) {
        subAvg = s1;
      } else if (s2 !== null) {
        subAvg = s2;
      }

      if (subAvg !== null && subAvg >= 0 && subAvg <= 10) {
        totalScore10 += subAvg;
        count++;
      }
    }
  }

  if (count === 0) return { gpa10: null, gpa4: null };

  const gpa10 = Math.round((totalScore10 / count) * 100) / 100;
  const gpa4 = Math.round(((gpa10 / 10) * 4.0) * 100) / 100;

  return { gpa10, gpa4 };
}

export function Step2Transcript({
  data,
  currentEducationLevel,
  currentGrade,
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
  const fileInputId = useId();

  const { gpa10, gpa4 } = calculateLinearGpa(years);

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
        return { ...s, [field]: val };
      });
      return { ...y, subjects: newSubjects };
    });

    const newGpas = calculateLinearGpa(updatedYears);
    onUpdate({
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

    setIsUploading(true);
    setUploadError(null);
    setOcrFailedNotice(null);
    setOcrSuccessNotice(null);
    setOcrWarnings([]);

    try {
      const res = await profileFlowApi.uploadDraftTranscript(fileArray);

      if (res.ocrExtraction?.warnings && res.ocrExtraction.warnings.length > 0) {
        setOcrWarnings(res.ocrExtraction.warnings);
      }

      // Kiểm tra nếu OCR bóc tách được môn học
      if (res.ocrExtraction?.subjects && res.ocrExtraction.subjects.length > 0) {
        const detectedGrade = res.ocrExtraction.detectedGrade ?? years[0]?.gradeName ?? "Lớp 10";
        let targetIdx = years.findIndex((y) => y.gradeName.includes(detectedGrade));
        if (targetIdx === -1) targetIdx = activeYearIndex;

        const updatedYears = years.map((y, yIdx) => {
          if (yIdx !== targetIdx) return y;

          // Merge OCR subjects
          const mergedSubjects: SubjectDraft[] = [...y.subjects];
          for (const ocrSub of res.ocrExtraction!.subjects) {
            const existing = mergedSubjects.find(
              (s) => s.subjectName.toLowerCase() === ocrSub.subjectName.toLowerCase()
            );
            if (existing) {
              if (ocrSub.semester1Score !== null && ocrSub.semester1Score !== undefined) {
                existing.semester1Score = ocrSub.semester1Score;
              }
              if (ocrSub.semester2Score !== null && ocrSub.semester2Score !== undefined) {
                existing.semester2Score = ocrSub.semester2Score;
              }
            } else {
              mergedSubjects.push({
                subjectName: ocrSub.subjectName,
                semester1Score: ocrSub.semester1Score ?? null,
                semester2Score: ocrSub.semester2Score ?? null,
              });
            }
          }

          return { ...y, subjects: mergedSubjects };
        });

        const newGpas = calculateLinearGpa(updatedYears);
        onUpdate({
          draftTranscriptFile: res.draftFile,
          academicYears: updatedYears,
          manualGpa10Scale: newGpas.gpa10,
          calculatedGpa4Scale: newGpas.gpa4,
        });

        setActiveYearIndex(targetIdx);
        setShowManualTable(true);

        const filledYearsCount = updatedYears.filter((y) =>
          y.subjects.some((s) => s.semester1Score !== null || s.semester2Score !== null)
        ).length;

        if (filledYearsCount < updatedYears.length) {
          const missingYears = updatedYears
            .filter((y) => y.subjects.every((s) => s.semester1Score === null && s.semester2Score === null))
            .map((y) => y.gradeName);

          setOcrSuccessNotice(
            `Đã nhận diện ${res.ocrExtraction.subjects.length} môn của ${detectedGrade}. Lưu ý: Hồ sơ du học cần đủ điểm các năm (${updatedYears.map((y) => y.gradeName).join(", ")}). Bạn đang thiếu ${missingYears.join(", ")}. Vui lòng tiếp tục tải thêm ảnh bảng điểm hoặc tự điền điểm vào bảng bên dưới.`
          );
        } else {
          setOcrSuccessNotice(
            `Đã nhận diện thành công ${res.ocrExtraction.subjects.length} môn học từ bảng điểm. Vui lòng kiểm tra lại điểm trong bảng bên dưới.`
          );
        }
      } else {
        // Tải file lên được nhưng AI không đọc được điểm (ảnh mờ, scan lệch...)
        onUpdate({ draftTranscriptFile: res.draftFile });
        setOcrFailedNotice(
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
    }
  };

  const handleNextClick = () => {
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
          {showManualTable ? "Kiểm tra lại điểm của bạn" : "Tải bảng điểm của bạn lên"}
        </h1>
        <p className="text-body text-muted-foreground">
          {showManualTable
            ? "Hệ thống tự động tính điểm theo công thức chuẩn (GPA 4.0 = GPA 10 / 10 * 4.0). Bạn chỉ cần kiểm tra và chỉnh sửa điểm chưa đúng."
            : "Chụp hoặc scan bảng điểm 3 năm gần nhất. Hệ thống AI sẽ tự động đọc điểm, bạn không phải gõ từng môn."}
        </p>
      </div>

      {/* Hidden file input */}
      <input
        id={fileInputId}
        type="file"
        multiple
        accept="image/*,.pdf"
        className="hidden"
        onChange={(e) => e.target.files && handleFileUpload(e.target.files)}
      />

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

      {/* Upload Dropzone (Visible when not in manual table) */}
      {!showManualTable ? (
        <div className="space-y-4">
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
              "flex flex-col items-center justify-center rounded-2xl border-2 border-dashed p-8 sm:p-12 text-center transition-all",
              dragOver
                ? "border-brand bg-brand-wash/30 scale-[1.01]"
                : "border-line bg-surface hover:border-brand/50 hover:bg-muted/20"
            )}
          >
            {isUploading ? (
              <div className="space-y-3">
                <Loader2Icon className="mx-auto size-10 animate-spin text-brand" />
                <div className="font-semibold text-sm text-foreground">
                  Đang phân tích bảng điểm bằng Ollama AI...
                </div>
                <div className="text-xs text-muted-foreground max-w-sm">
                  Mô hình thị giác đang đọc các cột môn học và điểm số từ ảnh. Vui lòng đợi trong giây lát.
                </div>
              </div>
            ) : (
              <div className="space-y-4 max-w-md">
                <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-brand-wash text-brand">
                  <UploadCloudIcon className="size-7" />
                </div>
                <div>
                  <div className="font-semibold text-base text-foreground">
                    Kéo thả hoặc chọn tệp bảng điểm của bạn
                  </div>
                  <div className="text-xs text-muted-foreground mt-1 leading-relaxed">
                    Hỗ trợ file PDF hoặc ảnh chụp (PNG, JPG). Bạn có thể tải nhiều ảnh cùng lúc cho các kỳ.
                    Hệ thống tự động tính GPA thang 4.0 và 10.
                  </div>
                </div>

                <div className="pt-2">
                  <Button
                    type="button"
                    onClick={triggerFileInput}
                    className="gap-2 cursor-pointer shadow-xs font-semibold text-sm"
                  >
                    <FileUpIcon className="size-4" />
                    Chọn tệp từ máy
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

          {/* Skip upload link */}
          <div className="text-center pt-2">
            <button
              type="button"
              onClick={() => setShowManualTable(true)}
              className="text-xs font-semibold text-brand hover:underline cursor-pointer"
            >
              Chưa có file bảng điểm? Nhập điểm bằng tay vào bảng 3 năm gần nhất →
            </button>
          </div>
        </div>
      ) : (
        /* Manual Table View with Years Tabs and Subjects */
        <div className="space-y-6">
          {/* File attached banner if any */}
          {data.draftTranscriptFile && (
            <div className="flex items-center justify-between rounded-xl border border-line bg-surface p-3 text-xs">
              <div className="flex items-center gap-2 text-foreground font-medium">
                <FileTextIcon className="size-4 text-brand" />
                <span>{data.draftTranscriptFile.originalFileName}</span>
                <Badge variant="secondary" className="text-[10px]">Đã quét</Badge>
              </div>
              <button
                type="button"
                onClick={triggerFileInput}
                className="text-xs font-semibold text-brand hover:underline cursor-pointer"
              >
                Tải lại file khác
              </button>
            </div>
          )}

          {/* Academic Years Selector */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <div>
                <Label className="text-sm font-semibold text-foreground">
                  Bảng điểm các năm học (3 năm gần nhất)
                </Label>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Đang hiển thị các năm học tính đến lớp {currentGrade || "hiện tại"}.
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
                </button>
              ))}
            </div>
          </div>

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
                    {years[activeYearIndex].subjects.map((sub, sIdx) => {
                      const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
                      const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
                      const avg =
                        s1 !== null && s2 !== null
                          ? Math.round(((s1 + s2) / 2) * 10) / 10
                          : s1 !== null
                          ? s1
                          : s2;

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
                              placeholder="-"
                              value={sub.semester1Score ?? ""}
                              onChange={(e) =>
                                handleScoreChange(activeYearIndex, sIdx, "semester1Score", e.target.value)
                              }
                              className="h-8 text-center text-xs w-20 mx-auto"
                            />
                          </td>
                          <td className="py-1 px-2 text-center">
                            <Input
                              type="number"
                              min={0}
                              max={10}
                              step={0.1}
                              placeholder="-"
                              value={sub.semester2Score ?? ""}
                              onChange={(e) =>
                                handleScoreChange(activeYearIndex, sIdx, "semester2Score", e.target.value)
                              }
                              className="h-8 text-center text-xs w-20 mx-auto"
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
                  <span>Điểm trung bình tích lũy (GPA)</span>
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
                  <div className="text-[11px] text-brand font-semibold">Quy đổi thang 4.0</div>
                  <div className="text-2xl font-extrabold text-brand">
                    {gpa4 !== null ? gpa4.toFixed(2) : "—"}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
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
