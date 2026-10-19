"use client";

import {
  ArrowLeftIcon,
  BookOpenIcon,
  CheckCircle2Icon,
  DollarSignIcon,
  Edit3Icon,
  FileTextIcon,
  GraduationCapIcon,
  Loader2Icon,
  SparklesIcon,
  TrophyIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  ACTIVITY_ROLE_OPTIONS,
  DEFAULT_BUDGET_TIERS,
  EDUCATION_LEVEL_OPTIONS,
  IMPACT_LEVEL_OPTIONS,
  SCHOLARSHIP_NEED_OPTIONS,
  SCHOOL_TYPE_OPTIONS,
  labelOf,
} from "@/lib/profile-flow-options";
import { findCertType, formatCertSummary, formatYearMonth } from "@/lib/certification-utils";
import type { DraftTranscriptFile, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

interface Step6ReviewProps {
  draft: ProfileFlowDraft;
  draftTranscript?: DraftTranscriptFile | null;
  metadata: ProfileMetadataResponse | null;
  onEditStep: (stepNumber: number) => void;
  onPrev: () => void;
  onSubmit: () => void;
  isSubmitting: boolean;
}

export function Step6Review({
  draft,
  draftTranscript,
  metadata,
  onEditStep,
  onPrev,
  onSubmit,
  isSubmitting,
}: Step6ReviewProps) {
  const { step1, step2, step3, step4, step5 } = draft;

  // Resolve state names
  const allStates = metadata?.states ?? [];
  const certificationTypes = metadata?.certificationTypes ?? [];
  const stateNames = (step1.targetStates ?? [])
    .map((code) => allStates.find((s) => s.code === code)?.name ?? code)
    .join(", ");

  // Target Degree Level Label
  const studyLevelLabel =
    metadata?.studyLevels.find((l) => l.key === step1.targetDegreeLevel)?.label ??
    step1.targetDegreeLevel ??
    "Chưa chọn";

  // Budget Tier Label
  const budgetTierLabel =
    metadata?.budgetTiers.find((b) => b.key === step4.annualBudgetTier)?.label ??
    DEFAULT_BUDGET_TIERS.find((b) => b.key === step4.annualBudgetTier)?.label ??
    "Chưa chọn";

  // Financial need label
  const need = SCHOLARSHIP_NEED_OPTIONS.find((o) => o.value === step4.needFinancialAid);
  const financialNeedLabel = need ? `${need.label} (${need.description.toLowerCase()})` : "Chưa chọn";

  // Transcript stats
  const totalSubjects = step2.academicYears?.reduce((sum, y) => sum + y.subjects.length, 0) ?? 0;
  const totalYears = step2.academicYears?.length ?? 0;
  const gpa = step2.calculatedGpa4Scale ?? (step2.manualGpa10Scale ? ((step2.manualGpa10Scale / 10) * 4).toFixed(2) : "–");

  // Activities & Awards count
  const activitiesCount = step5.activities.filter((a) => a.category === "activity").length;
  const awardsCount = step5.activities.filter((a) => a.category === "achievement").length;

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 6/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">Xem lại hồ sơ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Kiểm tra nhanh một lượt. Bấm &quot;Sửa&quot; ở mục nào bạn muốn chỉnh lại trước khi gửi.
        </p>
      </div>

      {/* Review Cards Grid */}
      <div className="space-y-4">
        {/* 1. Bạn & Mục tiêu */}
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs hover:border-brand/40 transition-colors">
          <div className="flex items-center justify-between border-b border-line/60 pb-3">
            <div className="flex items-center gap-2 text-sm font-bold text-foreground">
              <GraduationCapIcon className="size-4 text-brand" />
              <span>BẠN & MỤC TIÊU</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEditStep(1)}
              className="text-xs text-brand font-semibold hover:bg-brand-wash h-8 gap-1"
            >
              <Edit3Icon className="size-3.5" />
              Sửa
            </Button>
          </div>
          <div className="pt-3 text-xs space-y-1.5 text-muted-foreground leading-relaxed">
            <div>
              <strong className="text-foreground">Cấp học: </strong>
              {labelOf(EDUCATION_LEVEL_OPTIONS, step1.currentEducationLevel) || "Chưa chọn"}
              {step1.currentGrade && ` · ${step1.currentGrade}`}
              {step1.currentSchool && ` · ${step1.currentSchool}`}
            </div>
            <div>
              <strong className="text-foreground">Muốn học ở Mỹ: </strong>
              <span className="text-brand font-semibold">{studyLevelLabel}</span>
            </div>
            <div>
              <strong className="text-foreground">Ngành & Địa điểm: </strong>
              {step1.targetMajor || "Mọi ngành"}
              {stateNames && ` · Bang: ${stateNames}`}
              {step1.targetSchoolType && step1.targetSchoolType !== "all" && ` · ${labelOf(SCHOOL_TYPE_OPTIONS, step1.targetSchoolType)}`}
            </div>
            {step1.expectedGraduationYear && (
              <div>
                <strong className="text-foreground">Năm tốt nghiệp dự kiến: </strong>
                {step1.expectedGraduationYear}
              </div>
            )}
          </div>
        </div>

        {/* 2. Bảng điểm */}
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs hover:border-brand/40 transition-colors">
          <div className="flex items-center justify-between border-b border-line/60 pb-3">
            <div className="flex items-center gap-2 text-sm font-bold text-foreground">
              <FileTextIcon className="size-4 text-brand" />
              <span>BẢNG ĐIỂM</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEditStep(2)}
              className="text-xs text-brand font-semibold hover:bg-brand-wash h-8 gap-1"
            >
              <Edit3Icon className="size-3.5" />
              Sửa
            </Button>
          </div>
          <div className="pt-3 flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs text-muted-foreground">
            <div>
              <div>
                <strong className="text-foreground">Dữ liệu điểm: </strong>
                {totalYears > 0 ? `${totalYears} năm học · ${totalSubjects} môn học` : "Chưa nhập điểm"}
              </div>
              {draftTranscript && (
                <div className="text-[11px] text-ok mt-0.5 flex items-center gap-1">
                  <CheckCircle2Icon className="size-3" />
                  Đã tải tệp: {draftTranscript.originalFileName}
                </div>
              )}
            </div>
            <div className="rounded-xl bg-brand-wash px-3 py-1.5 text-center">
              <div className="text-[10px] text-brand-ink uppercase font-semibold">GPA Thang 4.0</div>
              <div className="text-numeric text-base font-bold text-brand">{gpa}</div>
            </div>
          </div>
        </div>

        {/* 3. Chứng chỉ */}
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs hover:border-brand/40 transition-colors">
          <div className="flex items-center justify-between border-b border-line/60 pb-3">
            <div className="flex items-center gap-2 text-sm font-bold text-foreground">
              <BookOpenIcon className="size-4 text-brand" />
              <span>CHỨNG CHỈ</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEditStep(3)}
              className="text-xs text-brand font-semibold hover:bg-brand-wash h-8 gap-1"
            >
              <Edit3Icon className="size-3.5" />
              Sửa
            </Button>
          </div>
          <div className="pt-3 text-xs space-y-1.5 text-muted-foreground">
            {step3.certifications.length > 0 ? (
              step3.certifications.map((cert) => (
                <div key={cert.id}>
                  {formatCertSummary(cert, findCertType(certificationTypes, cert.type))}
                </div>
              ))
            ) : (
              <div>Chưa có chứng chỉ</div>
            )}
            {step3.englishPlanned && (
              <div>
                <strong className="text-foreground">Dự kiến thi tiếng Anh: </strong>
                {formatYearMonth(step3.englishPlannedDate) ?? "chưa chọn thời gian"}
              </div>
            )}
          </div>
        </div>

        {/* 4. Tài chính */}
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs hover:border-brand/40 transition-colors">
          <div className="flex items-center justify-between border-b border-line/60 pb-3">
            <div className="flex items-center gap-2 text-sm font-bold text-foreground">
              <DollarSignIcon className="size-4 text-brand" />
              <span>TÀI CHÍNH</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEditStep(4)}
              className="text-xs text-brand font-semibold hover:bg-brand-wash h-8 gap-1"
            >
              <Edit3Icon className="size-3.5" />
              Sửa
            </Button>
          </div>
          <div className="pt-3 text-xs space-y-1.5 text-muted-foreground">
            <div>
              <strong className="text-foreground">Ngân sách dự kiến: </strong>
              {budgetTierLabel}
            </div>
            <div>
              <strong className="text-foreground">Nhu cầu học bổng: </strong>
              {financialNeedLabel}
            </div>
          </div>
        </div>

        {/* 5. Ngoại khóa & Giải thưởng */}
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs hover:border-brand/40 transition-colors">
          <div className="flex items-center justify-between border-b border-line/60 pb-3">
            <div className="flex items-center gap-2 text-sm font-bold text-foreground">
              <TrophyIcon className="size-4 text-brand" />
              <span>HOẠT ĐỘNG & GIẢI THƯỞNG</span>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onEditStep(5)}
              className="text-xs text-brand font-semibold hover:bg-brand-wash h-8 gap-1"
            >
              <Edit3Icon className="size-3.5" />
              Sửa
            </Button>
          </div>
          <div className="pt-3 text-xs text-muted-foreground">
            {step5.activities.length > 0 ? (
              <div className="space-y-2">
                <div className="flex gap-4">
                  <span>
                    <strong className="text-foreground">{activitiesCount}</strong> hoạt động ngoại khóa
                  </span>
                  <span>
                    <strong className="text-foreground">{awardsCount}</strong> giải thưởng / thành tích
                  </span>
                </div>
                <ul className="space-y-1">
                  {step5.activities.map((a, i) => (
                    <li key={a.id ?? i}>
                      <strong className="text-foreground">{a.name}</strong>
                      {" · "}
                      {labelOf(IMPACT_LEVEL_OPTIONS, a.impactLevel) || "Chưa chọn phạm vi"}
                      {a.category === "activity"
                        ? ` · ${labelOf(ACTIVITY_ROLE_OPTIONS, a.role) || "Chưa chọn vai trò"}${a.durationMonths ? ` · ${a.durationMonths} tháng` : ""}`
                        : ` · Năm ${a.year ?? "?"}`}
                    </li>
                  ))}
                </ul>
              </div>
            ) : (
              <span>Chưa có hoạt động ngoại khóa</span>
            )}
          </div>
        </div>
      </div>

      {/* Helpful Hint */}
      <div className="rounded-xl border border-brand/20 bg-brand-wash/30 p-4 text-xs text-muted-foreground leading-relaxed">
        💡 <strong className="text-brand-ink">Lưu ý: </strong>
        Ưu tiên chi phí, học bổng hay danh tiếng bạn sẽ chọn ngay ở trang kết quả gợi ý trường,
        và có thể đổi lại bao nhiêu lần cũng được!
      </div>

      {/* Bottom Actions */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button
          onClick={onSubmit}
          disabled={isSubmitting}
          size="lg"
          className="gap-2 bg-brand hover:bg-brand-ink text-white font-semibold shadow-xs px-6"
        >
          {isSubmitting ? (
            <>
              <Loader2Icon className="size-4 animate-spin" />
              Đang lưu và phân tích gợi ý...
            </>
          ) : (
            <>
              <SparklesIcon className="size-4" />
              Nhận gợi ý trường
            </>
          )}
        </Button>
      </div>
    </div>
  );
}
