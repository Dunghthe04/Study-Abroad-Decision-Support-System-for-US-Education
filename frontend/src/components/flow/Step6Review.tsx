"use client";

import {
  AlertCircleIcon,
  ArrowLeftIcon,
  BookOpenIcon,
  CheckCircle2Icon,
  DollarSignIcon,
  Edit3Icon,
  type LucideIcon,
  FileTextIcon,
  GraduationCapIcon,
  Loader2Icon,
  MessageCircleIcon,
  SparklesIcon,
  TrophyIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { useGradeScale } from "@/hooks/use-grade-scale";
import { cn } from "@/lib/utils";
import {
  ACTIVITY_ROLE_OPTIONS,
  DEFAULT_BUDGET_TIERS,
  EDUCATION_LEVEL_OPTIONS,
  EDUCATION_SYSTEM_OPTIONS,
  FUNDING_SOURCE_OPTIONS,
  IMPACT_LEVEL_OPTIONS,
  SCHOLARSHIP_NEED_OPTIONS,
  SCHOOL_TYPE_OPTIONS,
  draftGpa,
  labelOf,
} from "@/lib/profile-flow-options";
import { findCertType, formatCertSummary, formatYearMonth } from "@/lib/certification-utils";
import type { DraftTranscriptFile, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

// BE trả lỗi khi gửi hồ sơ thành một chuỗi "Bước 1: ... Bước 3: ..." → tách theo bước để hiện ở thẻ của bước đó
function splitStepErrors(message: string | null | undefined) {
  const byStep: Record<number, string[]> = {};
  const other: string[] = [];
  for (const part of (message ?? "").split(/(?=Bước \d+: )/)) {
    const text = part.trim();
    if (!text) continue;
    const m = /^Bước (\d+): ([\s\S]+)$/.exec(text);
    if (m) {
      const detail = m[2].charAt(0).toUpperCase() + m[2].slice(1);
      (byStep[Number(m[1])] ??= []).push(detail);
    } else {
      other.push(text);
    }
  }
  return { byStep, other };
}

/** Một thẻ trong lưới xem lại: eyebrow + nút Sửa, nội dung bên dưới, lỗi của bước (nếu có) ở cuối */
function ReviewCard({
  icon: Icon,
  eyebrow,
  onEdit,
  errors,
  className,
  children,
}: {
  icon: LucideIcon;
  eyebrow: string;
  onEdit: () => void;
  errors?: string[];
  className?: string;
  children: React.ReactNode;
}) {
  const hasErrors = Boolean(errors && errors.length > 0);
  return (
    <div
      className={cn(
        "rounded-2xl border bg-surface p-5 transition-colors",
        hasErrors ? "border-destructive/60" : "border-line hover:border-brand/40",
        className
      )}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-1.5 text-xs font-semibold text-muted-foreground">
          <Icon className="size-3.5 text-brand" />
          {eyebrow}
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={onEdit}
          className="h-7 gap-1 px-2 text-xs font-semibold text-brand hover:bg-brand-wash"
        >
          <Edit3Icon className="size-3.5" />
          Sửa
        </Button>
      </div>
      <div className="mt-2 space-y-1.5 text-xs leading-relaxed text-muted-foreground">{children}</div>
      {hasErrors && (
        <ul className="mt-3 space-y-1 border-t border-destructive/20 pt-2 text-xs font-medium text-destructive">
          {errors!.map((e, i) => (
            <li key={i}>{e}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

interface Step6ReviewProps {
  draft: ProfileFlowDraft;
  draftTranscript?: DraftTranscriptFile | null;
  metadata: ProfileMetadataResponse | null;
  onEditStep: (stepNumber: number) => void;
  onPrev: () => void;
  onSubmit: () => void;
  /** Form mở từ chat: nút cuối lưu hồ sơ rồi quay về chat thay vì gợi ý trường */
  backToChat?: boolean;
  isSubmitting: boolean;
  /** Lỗi BE trả về khi gửi hồ sơ */
  submitError?: string | null;
}

export function Step6Review({
  draft,
  draftTranscript,
  metadata,
  onEditStep,
  onPrev,
  onSubmit,
  backToChat = false,
  isSubmitting,
  submitError,
}: Step6ReviewProps) {
  useGradeScale(); // GPA thang 4 quy đổi theo bảng moderator sửa, vẽ lại khi bảng tải xong
  const { step1, step2, step3, step4, step5 } = draft;
  const stepErrors = splitStepErrors(submitError);

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

  // Ngành lưu tên Anh, hiện tên Việt nếu có trong bảng majors
  const majorLabel =
    metadata?.majors.find((m) => m.value.toLowerCase() === step1.targetMajor?.toLowerCase())?.label ??
    step1.targetMajor;

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
  // Cùng cách tính với BE (TranscriptGpa): GPA tổng hoặc tính từ điểm môn
  const { gpa10, gpa4 } = draftGpa(step2);
  const gpa = gpa4 != null ? gpa4.toFixed(2) : "–";
  const gpa10Text = gpa10 != null ? gpa10.toFixed(1) : "–";
  const gradeNames = (step2.academicYears ?? []).map((y) => y.gradeName).join(", ");

  // Activities & Awards count
  const activitiesCount = step5.activities.filter((a) => a.category === "activity").length;
  const awardsCount = step5.activities.filter((a) => a.category === "achievement").length;

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand">
          Bước 6/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">Xem lại hồ sơ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Kiểm tra nhanh một lượt. Bấm &quot;Sửa&quot; ở mục nào bạn muốn chỉnh lại trước khi gửi.
        </p>
      </div>

      {/* Review Cards Grid */}
      <div className="grid gap-4 sm:grid-cols-2">
        <ReviewCard icon={GraduationCapIcon} eyebrow="Bạn & mục tiêu" onEdit={() => onEditStep(1)} errors={stepErrors.byStep[1]}>
          <div className="text-sm font-semibold text-foreground">
            {labelOf(EDUCATION_LEVEL_OPTIONS, step1.currentEducationLevel) || "Chưa chọn"}
            {step1.currentGrade && ` · ${step1.currentGrade}`}
            {" → "}
            <span className="text-brand">{studyLevelLabel}</span>
            {step1.targetIntake && ` · ${step1.targetIntake}`}
          </div>
          {step1.currentSchool && <div>{step1.currentSchool}</div>}
          {step1.currentEducationLevel === "high_school" && step1.educationSystem && (
            <div>
              <strong className="text-foreground">Hệ chương trình: </strong>
              {labelOf(EDUCATION_SYSTEM_OPTIONS, step1.educationSystem)}
            </div>
          )}
          <div>
            <strong className="text-foreground">Ngành: </strong>
            {majorLabel || "Chưa chọn"}
          </div>
          <div>
            <strong className="text-foreground">Bang: </strong>
            {stateNames || "Không lọc"}
            {step1.targetSchoolType && step1.targetSchoolType !== "all" && ` · ${labelOf(SCHOOL_TYPE_OPTIONS, step1.targetSchoolType)}`}
          </div>
          {step1.expectedGraduationYear && (
            <div>
              <strong className="text-foreground">Tốt nghiệp dự kiến: </strong>
              {step1.expectedGraduationYear}
            </div>
          )}
        </ReviewCard>

        <ReviewCard icon={FileTextIcon} eyebrow="Bảng điểm" onEdit={() => onEditStep(2)} errors={stepErrors.byStep[2]}>
          <div className="flex items-start justify-between gap-3">
            <div className="space-y-1">
              <div className="text-sm font-semibold text-foreground">
                {totalYears > 0 ? `${totalSubjects} môn · ${gradeNames}` : "Chưa nhập điểm"}
              </div>
              {draftTranscript && (
                <div className="flex items-center gap-1 text-xs text-ok">
                  <CheckCircle2Icon className="size-3" />
                  Đã tải tệp: {draftTranscript.originalFileName}
                </div>
              )}
            </div>
            <div className="shrink-0 rounded-xl bg-brand-wash px-3 py-1.5 text-center">
              <div className="text-xs font-semibold text-brand-ink">Điểm TB tự tính</div>
              <div className="text-numeric text-base font-bold text-brand">{gpa10Text}</div>
              <div className="text-xs text-brand-ink">thang 4 (tham khảo): {gpa}</div>
            </div>
          </div>
        </ReviewCard>

        <ReviewCard icon={BookOpenIcon} eyebrow="Chứng chỉ" onEdit={() => onEditStep(3)} errors={stepErrors.byStep[3]}>
          {step3.certifications.length > 0 ? (
            step3.certifications.map((cert) => (
              <div key={cert.id} className="text-foreground">
                {formatCertSummary(cert, findCertType(certificationTypes, cert.type))}
              </div>
            ))
          ) : (
            <div>Chưa có chứng chỉ</div>
          )}
          {step3.englishPlanned && (
            <div>
              <strong className="text-foreground">Dự kiến thi tiếng Anh: </strong>
              {[
                step3.englishPlannedTestType?.toUpperCase(),
                step3.englishPlannedTargetScore != null ? `mục tiêu ${step3.englishPlannedTargetScore}` : null,
                formatYearMonth(step3.englishPlannedDate) ? `thi ${formatYearMonth(step3.englishPlannedDate)}` : null,
              ]
                .filter(Boolean)
                .join(" · ") || "chưa chọn thời gian"}
            </div>
          )}
          {step3.standardizedPlanned && (
            <div>
              <strong className="text-foreground">Dự kiến thi chuẩn hóa: </strong>
              {[
                step3.standardizedPlannedTestType?.toUpperCase(),
                step3.standardizedPlannedTargetScore != null ? `mục tiêu ${step3.standardizedPlannedTargetScore}` : null,
                formatYearMonth(step3.standardizedPlannedDate) ? `thi ${formatYearMonth(step3.standardizedPlannedDate)}` : null,
              ]
                .filter(Boolean)
                .join(" · ") || "chưa chọn thời gian"}
            </div>
          )}
        </ReviewCard>

        <ReviewCard icon={DollarSignIcon} eyebrow="Tài chính" onEdit={() => onEditStep(4)} errors={stepErrors.byStep[4]}>
          <div className="text-sm font-semibold text-foreground">{budgetTierLabel} / năm</div>
          <div>
            <strong className="text-foreground">Học bổng: </strong>
            {financialNeedLabel}
          </div>
          {step4.fundingSource && (
            <div>
              <strong className="text-foreground">Nguồn chi trả: </strong>
              {labelOf(FUNDING_SOURCE_OPTIONS, step4.fundingSource)}
            </div>
          )}
        </ReviewCard>

        <ReviewCard
          icon={TrophyIcon}
          eyebrow="Hoạt động & giải thưởng"
          onEdit={() => onEditStep(5)}
          errors={stepErrors.byStep[5]}
          className="sm:col-span-2"
        >
          {step5.activities.length > 0 ? (
            <>
              <div className="text-sm font-semibold text-foreground">
                {activitiesCount} hoạt động · {awardsCount} giải
              </div>
              <ul className="space-y-2">
                {step5.activities.map((a, i) => (
                  <li key={a.id ?? i} className="space-y-0.5">
                    <div>
                      <strong className="text-foreground">{a.name}</strong>
                      {" · "}
                      {labelOf(IMPACT_LEVEL_OPTIONS, a.impactLevel) || "Chưa chọn phạm vi"}
                      {a.category === "activity"
                        ? ` · ${labelOf(ACTIVITY_ROLE_OPTIONS, a.role) || "Chưa chọn vai trò"}${a.durationMonths ? ` · ${a.durationMonths} tháng` : ""}`
                        : ` · Năm ${a.year ?? "?"}`}
                    </div>
                    {a.description && (
                      <p className="text-xs text-muted-foreground italic pl-2 border-l border-brand/40">
                        &ldquo;{a.description}&rdquo;
                      </p>
                    )}
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <div>Chưa có hoạt động ngoại khóa</div>
          )}
        </ReviewCard>
      </div>

      {/* Helpful Hint */}
      <div className="rounded-xl border border-brand/20 bg-brand-wash/30 p-4 text-xs text-muted-foreground leading-relaxed">
        💡 <strong className="text-brand-ink">Lưu ý: </strong>
        Ưu tiên chi phí, học bổng hay danh tiếng bạn sẽ chọn ngay ở trang kết quả gợi ý trường,
        và có thể đổi lại bao nhiêu lần cũng được!
      </div>

      {/* Lỗi khi gửi: lỗi theo bước đã hiện ở thẻ bên trên, ở đây nhắc lại và hiện lỗi chung */}
      {submitError && (
        <div className="flex gap-2 rounded-xl border border-destructive/20 bg-destructive/10 p-3 text-xs font-medium text-destructive">
          <AlertCircleIcon className="mt-0.5 size-4 shrink-0" />
          <div className="space-y-1">
            {Object.keys(stepErrors.byStep).length > 0 && (
              <div>Hồ sơ còn lỗi ở các mục viền đỏ bên trên. Bấm &quot;Sửa&quot; ở mục đó để chỉnh lại.</div>
            )}
            {stepErrors.other.map((e, i) => (
              <div key={i}>{e}</div>
            ))}
          </div>
        </div>
      )}

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
          className="gap-2 bg-brand hover:bg-brand-ink text-white font-semibold px-6"
        >
          {isSubmitting ? (
            <>
              <Loader2Icon className="size-4 animate-spin" />
              {backToChat ? "Đang lưu hồ sơ..." : "Đang lưu và phân tích gợi ý..."}
            </>
          ) : backToChat ? (
            <>
              <MessageCircleIcon className="size-4" />
              Lưu và quay về chat
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
