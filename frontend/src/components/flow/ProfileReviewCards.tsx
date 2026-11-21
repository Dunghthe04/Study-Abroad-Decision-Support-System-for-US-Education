"use client";

import {
  BookOpenIcon,
  CheckCircle2Icon,
  ChevronDownIcon,
  DollarSignIcon,
  Edit3Icon,
  type LucideIcon,
  FileTextIcon,
  GraduationCapIcon,
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
import { findCertType, formatCertSummary } from "@/lib/certification-utils";
import type { DraftTranscriptFile, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

/** Hồ sơ BE trả về (AcademicProfileResponse.details) có thể để null ở mảng → điền mặc định để thẻ đọc được */
export function normalizeProfileDetails(details: Partial<ProfileFlowDraft> | null | undefined): ProfileFlowDraft | null {
  if (!details) return null;
  const step1 = details.step1 ?? {};
  const step2 = details.step2 ?? { academicYears: [] };
  const step3 = details.step3 ?? { certifications: [], englishPlanned: false };
  return {
    step1: { ...step1, targetStates: step1.targetStates ?? [] },
    step2: {
      ...step2,
      academicYears: (step2.academicYears ?? []).map((y) => ({ ...y, subjects: y.subjects ?? [] })),
    },
    step3: { ...step3, certifications: step3.certifications ?? [], englishPlanned: Boolean(step3.englishPlanned) },
    step4: details.step4 ?? {},
    step5: { activities: details.step5?.activities ?? [] },
  };
}

/** Một thẻ: eyebrow + nút Sửa (chỉ khi có onEdit), nội dung bên dưới, lỗi của bước (nếu có) ở cuối */
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
  onEdit?: () => void;
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
      <div className="flex min-h-7 items-center justify-between">
        <div className="flex items-center gap-1.5 text-xs font-semibold text-muted-foreground">
          <Icon className="size-3.5 text-brand" />
          {eyebrow}
        </div>
        {onEdit && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onEdit}
            className="gap-1 px-2 text-xs font-semibold text-brand hover:bg-brand-wash"
          >
            <Edit3Icon className="size-3.5" />
            Sửa
          </Button>
        )}
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

function formatScore(score: number | null | undefined) {
  return score != null ? String(score) : "–";
}

interface ProfileReviewCardsProps {
  draft: ProfileFlowDraft;
  metadata: ProfileMetadataResponse | null;
  draftTranscript?: DraftTranscriptFile | null;
  /** Không truyền thì ẩn nút "Sửa" (xem chỉ đọc: tài khoản phụ huynh, "Con của tôi") */
  onEditStep?: (stepNumber: number) => void;
  /** Lỗi BE theo từng bước, hiện ở thẻ của bước đó */
  errorsByStep?: Record<number, string[]>;
}

/** 5 thẻ hồ sơ (bước 1–5) — dùng ở bước 6, thẻ hồ sơ trang Tài khoản và "Con của tôi" */
export function ProfileReviewCards({ draft, metadata, draftTranscript, onEditStep, errorsByStep }: ProfileReviewCardsProps) {
  useGradeScale(); // GPA thang 4 quy đổi theo bảng moderator sửa, vẽ lại khi bảng tải xong
  const { step1, step2, step3, step4, step5 } = draft;
  const edit = (step: number) => (onEditStep ? () => onEditStep(step) : undefined);

  const allStates = metadata?.states ?? [];
  const certificationTypes = metadata?.certificationTypes ?? [];
  const stateNames = (step1.targetStates ?? [])
    .map((code) => allStates.find((s) => s.code === code)?.name ?? code)
    .join(", ");

  const studyLevelLabel =
    metadata?.studyLevels.find((l) => l.key === step1.targetDegreeLevel)?.label ??
    step1.targetDegreeLevel ??
    "Chưa chọn";

  // Ngành lưu tên Anh, hiện tên Việt nếu có trong bảng majors
  const majorLabel =
    metadata?.majors.find((m) => m.value.toLowerCase() === step1.targetMajor?.toLowerCase())?.label ??
    step1.targetMajor;

  // UC-94: phụ huynh nhập số tiền tối đa chính xác thì hiện đúng số đó (đã lưu: USD, chưa lưu: VNĐ), không hiện khoảng
  const budgetTierLabel =
    step4.annualBudgetUsd != null
      ? `Tối đa ${step4.annualBudgetUsd.toLocaleString("vi-VN")} USD`
      : step4.annualBudgetVnd != null
        ? `Tối đa ${step4.annualBudgetVnd.toLocaleString("vi-VN")} VNĐ`
        : (metadata?.budgetTiers.find((b) => b.key === step4.annualBudgetTier)?.label ??
          DEFAULT_BUDGET_TIERS.find((b) => b.key === step4.annualBudgetTier)?.label ??
          "Chưa chọn");

  const need = SCHOLARSHIP_NEED_OPTIONS.find((o) => o.value === step4.needFinancialAid);
  const financialNeedLabel = need ? `${need.label} (${need.description.toLowerCase()})` : "Chưa chọn";

  // Cùng cách tính với BE (TranscriptGpa): GPA tổng hoặc tính từ điểm môn
  const { gpa10, gpa4 } = draftGpa(step2);
  const gpa = gpa4 != null ? gpa4.toFixed(2) : "–";
  const gpa10Text = gpa10 != null ? gpa10.toFixed(1) : "–";

  // Chỉ những năm có môn mới hiện ở phần "Xem điểm từng môn"
  const yearsWithSubjects = step2.academicYears.filter((y) => y.subjects.some((s) => s.subjectName));

  const activitiesCount = step5.activities.filter((a) => a.category === "activity").length;
  const awardsCount = step5.activities.filter((a) => a.category === "achievement").length;

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <ReviewCard icon={GraduationCapIcon} eyebrow="Bạn & mục tiêu" onEdit={edit(1)} errors={errorsByStep?.[1]}>
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
          {majorLabel || (step1.majorUndecided ? "Chưa rõ" : "Chưa chọn")}
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

      <ReviewCard icon={FileTextIcon} eyebrow="Bảng điểm" onEdit={edit(2)} errors={errorsByStep?.[2]}>
        <div className="space-y-2">
          {draftTranscript && (
            <div className="flex items-center gap-1 text-xs text-ok">
              <CheckCircle2Icon className="size-3" />
              Đã tải tệp: {draftTranscript.originalFileName}
            </div>
          )}
          <div className="mx-auto w-fit rounded-xl bg-brand-wash px-3 py-1.5 text-center">
            <div className="text-xs font-semibold text-brand-ink">Điểm TB tự tính</div>
            <div className="text-numeric text-base font-bold text-brand">{gpa10Text}</div>
            <div className="text-xs text-brand-ink">thang 4 (tham khảo): {gpa}</div>
          </div>
          {yearsWithSubjects.length > 0 && (
            <details className="group rounded-lg border border-line">
              <summary className="flex cursor-pointer list-none items-center justify-between px-3 py-1.5 font-semibold text-foreground">
                Xem điểm từng môn
                <ChevronDownIcon className="size-3.5 transition-transform group-open:rotate-180" />
              </summary>
              <div className="space-y-3 border-t border-line px-3 py-2">
                {yearsWithSubjects.map((year, yi) => (
                  <table key={`${year.gradeName}-${yi}`} className="w-full text-left">
                    <caption className="pb-1 text-left font-semibold text-foreground">{year.gradeName}</caption>
                    <thead>
                      <tr className="text-muted-foreground">
                        <th className="font-medium">Môn</th>
                        <th className="w-12 text-right font-medium">HK1</th>
                        <th className="w-12 text-right font-medium">HK2</th>
                      </tr>
                    </thead>
                    <tbody>
                      {year.subjects
                        .filter((s) => s.subjectName)
                        .map((s, si) => (
                          <tr key={`${s.subjectName}-${si}`} className="border-t border-line/60">
                            <td className="py-0.5 text-foreground">{s.subjectName}</td>
                            <td className="text-numeric text-right">{formatScore(s.semester1Score)}</td>
                            <td className="text-numeric text-right">{formatScore(s.semester2Score)}</td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                ))}
              </div>
            </details>
          )}
        </div>
      </ReviewCard>

      <ReviewCard icon={BookOpenIcon} eyebrow="Chứng chỉ" onEdit={edit(3)} errors={errorsByStep?.[3]}>
        {step3.certifications.length > 0 ? (
          step3.certifications.map((cert) => (
            <div key={cert.id} className="text-foreground">
              {formatCertSummary(cert, findCertType(certificationTypes, cert.type))}
            </div>
          ))
        ) : (
          <div>Chưa có chứng chỉ</div>
        )}
        {step3.englishPlanned && <div>Tiếng Anh: chưa thi và chưa có dự định thi</div>}
        {step3.standardizedPlanned && <div>Bài thi chuẩn hóa: chưa thi và chưa có dự định thi</div>}
      </ReviewCard>

      <ReviewCard icon={DollarSignIcon} eyebrow="Tài chính" onEdit={edit(4)} errors={errorsByStep?.[4]}>
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
        onEdit={edit(5)}
        errors={errorsByStep?.[5]}
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
  );
}
