"use client";

import { useId, useState } from "react";
import {
  AlertTriangleIcon,
  ArrowLeftIcon,
  ArrowRightIcon,
  FileTextIcon,
  FileUpIcon,
  Loader2Icon,
  PlusIcon,
  Trash2Icon,
  UploadCloudIcon,
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

const DEFAULT_YEARS: AcademicYearDraft[] = [
  {
    gradeName: "Lớp 10",
    subjects: DEFAULT_SUBJECTS.map((s) => ({ subjectName: s, semester1Score: null, semester2Score: null })),
  },
  {
    gradeName: "Lớp 11",
    subjects: DEFAULT_SUBJECTS.map((s) => ({ subjectName: s, semester1Score: null, semester2Score: null })),
  },
  {
    gradeName: "Lớp 12",
    subjects: DEFAULT_SUBJECTS.map((s) => ({ subjectName: s, semester1Score: null, semester2Score: null })),
  },
];

// Helper tính GPA thang 4 từ thang 10 theo công thức tuyến tính người dùng yêu cầu: GPA 4.0 = GPA 10 / 10 * 4.0
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

export function Step2Transcript({ data, onUpdate, onPrev, onNext }: Step2TranscriptProps) {
  const years = data.academicYears && data.academicYears.length > 0 ? data.academicYears : DEFAULT_YEARS;
  const [activeYearIndex, setActiveYearIndex] = useState(0);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [ocrWarnings, setOcrWarnings] = useState<string[]>([]);
  const [showManualTable, setShowManualTable] = useState(
    Boolean(data.academicYears && data.academicYears.length > 0 && data.academicYears.some((y) => y.subjects.some((s) => s.semester1Score !== null)))
  );
  const [dragOver, setDragOver] = useState(false);
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

  const handleFileUpload = async (files: FileList | File[]) => {
    const fileArray = Array.from(files);
    if (fileArray.length === 0) return;

    setIsUploading(true);
    setUploadError(null);
    setOcrWarnings([]);

    try {
      const res = await profileFlowApi.uploadDraftTranscript(fileArray);

      if (res.ocrExtraction?.warnings && res.ocrExtraction.warnings.length > 0) {
        setOcrWarnings(res.ocrExtraction.warnings);
      }

      // If OCR extracted subjects, map them into the current active grade or detected grade
      if (res.ocrExtraction?.subjects && res.ocrExtraction.subjects.length > 0) {
        const detectedGrade = res.ocrExtraction.detectedGrade ?? "Lớp 10";
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
      } else {
        onUpdate({ draftTranscriptFile: res.draftFile });
      }

      setShowManualTable(true);
    } catch (err: unknown) {
      setUploadError(err instanceof Error ? err.message : "Đã có lỗi khi tải tệp bảng điểm");
    } finally {
      setIsUploading(false);
    }
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
            ? "Hệ thống đã tự động tính điểm theo công thức chuẩn. Bạn chỉ cần sửa ô nào chưa đúng."
            : "Đây là cách nhanh nhất. Bạn không phải gõ từng môn."}
        </p>
      </div>

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

      {/* Upload Dropzone (Visible when not in manual table, or toggleable) */}
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
                  Mô hình thị giác đang đọc các cột môn học và học kỳ trong ảnh/PDF. Vui lòng đợi trong giây lát.
                </div>
              </div>
            ) : (
              <div className="space-y-4 max-w-md">
                <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-brand-wash text-brand">
                  <UploadCloudIcon className="size-7" />
                </div>
                <div>
                  <div className="font-semibold text-base text-foreground">
                    Kéo thả hoặc chọn bảng điểm của bạn
                  </div>
                  <div className="text-xs text-muted-foreground mt-1 leading-relaxed">
                    Hỗ trợ tệp PDF hoặc ảnh chụp (PNG, JPG). Bạn có thể chọn nhiều ảnh cùng lúc cho từng kỳ.
                    Hệ thống sẽ tự đọc điểm, bạn chỉ cần kiểm tra lại.
                  </div>
                </div>

                <div className="pt-2">
                  <input
                    id={fileInputId}
                    type="file"
                    multiple
                    accept="image/*,.pdf"
                    className="hidden"
                    onChange={(e) => e.target.files && handleFileUpload(e.target.files)}
                  />
                  <label htmlFor={fileInputId}>
                    <span className="inline-flex items-center gap-2 rounded-xl bg-brand px-5 py-2.5 text-sm font-semibold text-white shadow-xs hover:bg-brand-ink cursor-pointer transition-colors">
                      <FileUpIcon className="size-4" />
                      Chọn tệp từ máy
                    </span>
                  </label>
                </div>
              </div>
            )}
          </div>

          {uploadError && (
            <div className="rounded-lg bg-risk-wash p-3 text-xs text-risk font-medium">
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
              Chưa có bảng điểm? Nhập tay
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
                onClick={() => setShowManualTable(false)}
                className="text-xs font-semibold text-brand hover:underline cursor-pointer"
              >
                Tải tệp khác
              </button>
            </div>
          )}

          {/* Academic Years Selector */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label className="text-sm font-semibold text-foreground">
                Điểm học bạ 3 năm gần nhất đến hiện tại
              </Label>
              <button
                type="button"
                onClick={() => setShowManualTable(false)}
                className="text-xs text-muted-foreground hover:text-foreground cursor-pointer"
              >
                Tải lại file bảng điểm
              </button>
            </div>

            <div className="flex gap-2 border-b border-line pb-2">
              {years.map((y, idx) => (
                <button
                  key={y.gradeName}
                  type="button"
                  onClick={() => setActiveYearIndex(idx)}
                  className={cn(
                    "rounded-lg px-4 py-2 text-xs font-semibold transition-all cursor-pointer",
                    activeYearIndex === idx
                      ? "bg-brand text-white shadow-xs"
                      : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
                  )}
                >
                  {y.gradeName}
                </button>
              ))}
            </div>
          </div>

          {/* Subjects Table */}
          <div className="overflow-hidden rounded-xl border border-line bg-surface shadow-xs">
            <table className="w-full text-left text-xs">
              <thead className="bg-ground/70 text-muted-foreground border-b border-line">
                <tr>
                  <th className="py-2.5 px-4 font-semibold">Môn học</th>
                  <th className="py-2.5 px-3 font-semibold text-center w-28">Học kỳ 1</th>
                  <th className="py-2.5 px-3 font-semibold text-center w-28">Học kỳ 2</th>
                  <th className="py-2.5 px-3 font-semibold text-center w-20">Trung bình</th>
                  <th className="py-2.5 px-2 text-center w-10"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {years[activeYearIndex]?.subjects.map((sub, sIdx) => {
                  const s1 = typeof sub.semester1Score === "number" ? sub.semester1Score : null;
                  const s2 = typeof sub.semester2Score === "number" ? sub.semester2Score : null;
                  let avg: string = "–";
                  if (s1 !== null && s2 !== null) {
                    avg = ((s1 + s2) / 2).toFixed(1);
                  } else if (s1 !== null) {
                    avg = s1.toFixed(1);
                  } else if (s2 !== null) {
                    avg = s2.toFixed(1);
                  }

                  return (
                    <tr key={`${sub.subjectName}-${sIdx}`} className="hover:bg-muted/30 transition-colors">
                      <td className="py-2 px-4 font-medium text-foreground">
                        {sub.subjectName}
                      </td>
                      <td className="py-2 px-3 text-center">
                        <Input
                          type="number"
                          step="0.1"
                          min="0"
                          max="10"
                          placeholder="–"
                          value={sub.semester1Score ?? ""}
                          onChange={(e) =>
                            handleScoreChange(activeYearIndex, sIdx, "semester1Score", e.target.value)
                          }
                          className="h-8 text-center text-xs font-semibold"
                        />
                      </td>
                      <td className="py-2 px-3 text-center">
                        <Input
                          type="number"
                          step="0.1"
                          min="0"
                          max="10"
                          placeholder="–"
                          value={sub.semester2Score ?? ""}
                          onChange={(e) =>
                            handleScoreChange(activeYearIndex, sIdx, "semester2Score", e.target.value)
                          }
                          className="h-8 text-center text-xs font-semibold"
                        />
                      </td>
                      <td className="py-2 px-3 text-center font-bold text-foreground">
                        {avg}
                      </td>
                      <td className="py-2 px-2 text-center">
                        <button
                          type="button"
                          onClick={() => handleRemoveSubject(activeYearIndex, sIdx)}
                          className="text-muted-foreground hover:text-destructive cursor-pointer p-1"
                          title="Xóa môn này"
                        >
                          <Trash2Icon className="size-3.5" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {/* Add Subject Row */}
            <div className="border-t border-line p-2.5 bg-ground/40 flex justify-between items-center">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => handleAddSubject(activeYearIndex)}
                className="text-xs text-brand font-medium gap-1 h-8"
              >
                <PlusIcon className="size-3.5" />
                Thêm môn học
              </Button>
              <span className="text-[11px] text-muted-foreground pr-2">
                Thang điểm 10 (0.0 – 10.0)
              </span>
            </div>
          </div>

          {/* Live GPA Calculation Box */}
          <div className="rounded-2xl border border-brand/20 bg-gradient-to-r from-brand-wash/50 to-surface p-5">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div className="space-y-0.5">
                <div className="text-xs font-semibold text-brand uppercase tracking-wider">
                  GPA tự tính (thang 4.0 chuẩn hóa)
                </div>
                <div className="text-xs text-muted-foreground">
                  Tự động cập nhật theo công thức tuyến tính chuẩn: GPA 4.0 = (GPA 10 / 10) × 4.0
                </div>
              </div>

              <div className="flex items-center gap-4">
                {gpa10 !== null && (
                  <div className="text-right">
                    <div className="text-[10px] text-muted-foreground uppercase">TB Thang 10</div>
                    <div className="text-numeric text-base font-semibold text-foreground">
                      {gpa10.toFixed(2)}
                    </div>
                  </div>
                )}
                <div className="rounded-xl bg-brand px-4 py-2 text-center text-white shadow-xs">
                  <div className="text-[10px] uppercase font-semibold text-white/80">Thang 4.0</div>
                  <div className="text-numeric text-xl font-black">
                    {gpa4 !== null ? gpa4.toFixed(2) : "–"}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={onNext} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
