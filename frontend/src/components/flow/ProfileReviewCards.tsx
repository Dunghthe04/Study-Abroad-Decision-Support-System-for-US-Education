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
  calculateGradeTrend,
  draftGpa,
  labelOf,
} from "@/lib/profile-flow-options";
import { GpaConversionNote } from "./GpaConversionNote";
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

/** Một mục hồ sơ: tên mục + nút Sửa (chỉ khi có onEdit), nội dung bên dưới, lỗi của bước (nếu có) ở cuối */
function ReviewSection({
  icon: Icon,
  title,
  onEdit,
  errors,
  children,
}: {
  icon: LucideIcon;
  title: string;
  onEdit?: () => void;
  errors?: string[];
  children: React.ReactNode;
}) {
  const hasErrors = Boolean(errors && errors.length > 0);
  return (
    <section className="py-6">
      <div className="flex min-h-8 items-center justify-between gap-3">
        <h3 className={cn("flex items-center gap-2 text-sm font-semibold", hasErrors ? "text-destructive" : "text-ink")}>
          <Icon aria-hidden className="size-4 text-ink-2" />
          {title}
        </h3>
        {onEdit && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={onEdit}
            aria-label={`Sửa ${title.toLowerCase()}`}
            className="gap-1 px-2 text-brand hover:bg-brand-wash"
          >
            <Edit3Icon className="size-3.5" />
            Sửa
          </Button>
        )}
      </div>
      <div className="mt-2 space-y-1.5 text-sm leading-relaxed text-ink-2">{children}</div>
      {hasErrors && (
        <ul role="alert" className="mt-3 space-y-1 text-xs font-medium text-destructive">
          {errors!.map((e, i) => (
            <li key={i}>{e}</li>
          ))}
        </ul>
      )}
    </section>
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
  className?: string;
}

/** 5 mục hồ sơ (bước 1–5) — dùng ở bước 6, thẻ hồ sơ trang Tài khoản và "Con của tôi" */
export function ProfileReviewCards({ draft, metadata, draftTranscript, onEditStep, errorsByStep, className }: ProfileReviewCardsProps) {
  useGradeScale(); // GPA thang 4 quy đổi theo bảng moderator sửa, vẽ lại khi bảng tải xong
  const { step1, step2, step3, step4, step5 } = draft;
  const edit = (step: number) => (onEditStep ? () => onEditStep(step) : undefined);

  const allStates = metadata?.states ?? [];
  const certificationTypes = metadata?.certificationTypes ?? [];
  const currentCerts = step3.certifications.filter((c) => !c.isTarget);
  const targetCerts = step3.certifications.filter((c) => c.isTarget);
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

  // Cùng cách tính với BE (TranscriptGpa): GPA tổng hoặc tính từ điểm môn [UC-18]
  const { gpa10, gpa4 } = draftGpa(step2);
  const isDirectScale4 = gpa10 == null && gpa4 != null;
  const gpa = gpa4 != null ? gpa4.toFixed(2) : "–";
  const gpa10Text = gpa10 != null ? gpa10.toFixed(1) : isDirectScale4 ? "Chưa có dữ liệu" : "–";
  const gradeTrend = calculateGradeTrend(step2.academicYears);

  // Chỉ những năm có môn mới hiện ở phần "Xem điểm từng môn"
  const yearsWithSubjects = step2.academicYears.filter((y) => y.subjects.some((s) => s.subjectName));

  const activitiesCount = step5.activities.filter((a) => a.category === "activity").length;
  const awardsCount = step5.activities.filter((a) => a.category === "achievement").length;

  return (
    <div className={cn("divide-y divide-dashed divide-line", className)}>
      <ReviewSection icon={GraduationCapIcon} title="Mục tiêu của bạn" onEdit={edit(1)} errors={errorsByStep?.[1]}>
        <div className="text-sm font-semibold text-ink">
          {labelOf(EDUCATION_LEVEL_OPTIONS, step1.currentEducationLevel) || "Chưa chọn"}
          {step1.currentGrade && ` · ${step1.currentGrade}`}
          {" → "}
          <span className="text-brand">{studyLevelLabel}</span>
          {step1.targetIntake && ` · ${step1.targetIntake}`}
        </div>
        {step1.currentSchool && <div>{step1.currentSchool}</div>}
        {step1.currentEducationLevel === "high_school" && step1.educationSystem && (
          <div>
            <strong className="text-ink">Hệ chương trình: </strong>
            {labelOf(EDUCATION_SYSTEM_OPTIONS, step1.educationSystem)}
          </div>
        )}
        <div>
          <strong className="text-ink">Ngành: </strong>
          {majorLabel || (step1.majorUndecided ? "Cần tư vấn thêm" : "Chưa chọn")}
        </div>
        <div>
          <strong className="text-ink">Bang: </strong>
          {stateNames || "Tất cả các bang"}
          {step1.targetSchoolType && step1.targetSchoolType !== "all" && ` · ${labelOf(SCHOOL_TYPE_OPTIONS, step1.targetSchoolType)}`}
        </div>
        {step1.expectedGraduationYear && (
          <div>
            <strong className="text-ink">Tốt nghiệp dự kiến: </strong>
            {step1.expectedGraduationYear}
          </div>
        )}
      </ReviewSection>

      <ReviewSection icon={FileTextIcon} title="Bảng điểm" onEdit={edit(2)} errors={errorsByStep?.[2]}>
        <div className="space-y-2">
          {draftTranscript && (
            <div className="flex items-center gap-1.5 text-ok">
              <CheckCircle2Icon aria-hidden className="size-3.5" />
              Đã tải tệp: {draftTranscript.originalFileName}
            </div>
          )}
          <div>
            <strong className="text-ink">
              {isDirectScale4 ? "GPA thang 4.0: " : "Điểm TB tự tính: "}
            </strong>
            <span className="text-numeric">{isDirectScale4 ? gpa : gpa10Text}</span>
            {" · "}
            {isDirectScale4 ? (
              <>thang 10: <span className="text-numeric">{gpa10Text}</span></>
            ) : (
              <>thang 4 (tham khảo): <span className="text-numeric">{gpa}</span></>
            )}
          </div>
          {gpa10 != null && <GpaConversionNote className="pt-1" />}
          {gradeTrend.hasEnoughData && (
            <div className="rounded-lg border border-line/80 bg-ground/60 p-2.5 text-xs space-y-1">
              <div className="font-semibold text-foreground">Xu hướng điểm theo năm học</div>
              <div className="flex flex-wrap gap-1.5 text-[11px] text-muted-foreground">
                {gradeTrend.yearlyAverages.map((yr) => (
                  <span key={yr.gradeName} className="rounded bg-surface px-1.5 py-0.5 border border-line">
                    {yr.gradeName}: <strong className="text-foreground">{yr.gpa10.toFixed(1)}</strong> ({yr.gpa4.toFixed(2)})
                  </span>
                ))}
              </div>
              <p className="text-muted-foreground">{gradeTrend.summaryMessage}</p>
            </div>
          )}
          {yearsWithSubjects.length > 0 && (
            <details className="group">
              <summary className="flex w-fit cursor-pointer list-none items-center gap-1.5 font-medium text-brand">
                Xem điểm từng môn
                <ChevronDownIcon className="size-3.5 transition-transform group-open:rotate-180" />
              </summary>
              <div className="mt-2 space-y-3">
                {yearsWithSubjects.map((year, yi) => (
                  <table key={`${year.gradeName}-${yi}`} className="w-full text-left">
                    <caption className="pb-1 text-left font-semibold text-ink">{year.gradeName}</caption>
                    <thead>
                      <tr className="text-ink-2">
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
                            <td className="py-0.5 text-ink">{s.subjectName}</td>
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
      </ReviewSection>

      <ReviewSection icon={BookOpenIcon} title="Chứng chỉ" onEdit={edit(3)} errors={errorsByStep?.[3]}>
        {/* Điểm đã thi và điểm mục tiêu (dự định thi) tách hai nhóm, mục tiêu khác màu để không đọc nhầm (nhận xét demo 28/11) */}
        {targetCerts.length > 0 && <div className="text-xs font-semibold text-ink-2">Hiện có</div>}
        {currentCerts.length > 0 ? (
          currentCerts.map((cert) => (
            <div key={cert.id} className="text-ink">
              {formatCertSummary(cert, findCertType(certificationTypes, cert.type))}
            </div>
          ))
        ) : (
          <div>{targetCerts.length > 0 ? "Chưa có chứng chỉ đã thi" : "Chưa có chứng chỉ"}</div>
        )}
        {targetCerts.length > 0 && (
          <div className="mt-2 space-y-1 rounded-lg border border-gold/30 bg-gold-wash p-2.5">
            <div className="text-xs font-semibold text-gold">Mục tiêu (chưa thi)</div>
            {targetCerts.map((cert) => (
              <div key={cert.id} className="font-medium text-gold">
                {formatCertSummary(cert, findCertType(certificationTypes, cert.type))}
              </div>
            ))}
          </div>
        )}
        {step3.englishPlanned && <div>Tiếng Anh: chưa thi và chưa có dự định thi</div>}
        {step3.standardizedPlanned && <div>Bài thi chuẩn hóa: chưa thi và chưa có dự định thi</div>}
      </ReviewSection>

      <ReviewSection icon={DollarSignIcon} title="Tài chính" onEdit={edit(4)} errors={errorsByStep?.[4]}>
        <div className="text-sm font-semibold text-ink">{budgetTierLabel} / năm</div>
        <div>
          <strong className="text-ink">Học bổng: </strong>
          {financialNeedLabel}
        </div>
        {step4.fundingSource && (
          <div>
            <strong className="text-ink">Nguồn chi trả: </strong>
            {labelOf(FUNDING_SOURCE_OPTIONS, step4.fundingSource)}
          </div>
        )}
      </ReviewSection>

      <ReviewSection
        icon={TrophyIcon}
        title="Hoạt động & giải thưởng"
        onEdit={edit(5)}
        errors={errorsByStep?.[5]}
      >
        {step5.activities.length > 0 ? (
          <>
            <div className="text-sm font-semibold text-ink">
              {activitiesCount} hoạt động · {awardsCount} giải
            </div>
            <ul className="space-y-2">
              {step5.activities.map((a, i) => (
                <li key={a.id ?? i} className="space-y-0.5">
                  <div>
                    <strong className="text-ink">{a.name}</strong>
                    {" · "}
                    {labelOf(IMPACT_LEVEL_OPTIONS, a.impactLevel) || "Chưa chọn phạm vi"}
                    {a.category === "activity"
                      ? ` · ${labelOf(ACTIVITY_ROLE_OPTIONS, a.role) || "Chưa chọn vai trò"}${a.durationMonths ? ` · ${a.durationMonths} tháng` : ""}`
                      : ` · Năm ${a.year ?? "?"}`}
                  </div>
                  {a.description && (
                    <p className="text-xs italic text-ink-2">
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
      </ReviewSection>
    </div>
  );
}
