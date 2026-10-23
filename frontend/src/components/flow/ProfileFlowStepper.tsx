"use client";

import { CheckIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import { findCertType, getCertDisplayName } from "@/lib/certification-utils";
import { DEFAULT_BUDGET_TIERS, calculateLinearGpa } from "@/lib/profile-flow-options";
import type { CurrentEducationLevel, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

export interface StepMeta {
  step: number;
  title: string;
  /** nhãn ngắn cho dải bước ngang */
  short: string;
  subtitle: string;
}

export const FLOW_STEPS: StepMeta[] = [
  {
    step: 1,
    title: "Bạn & mục tiêu",
    short: "Mục tiêu",
    subtitle: "Cấp học hiện tại, bậc muốn học",
  },
  {
    step: 2,
    title: "Bảng điểm",
    short: "Bảng điểm",
    subtitle: "Tải lên hoặc nhập tay",
  },
  {
    step: 3,
    title: "Chứng chỉ",
    short: "Chứng chỉ",
    subtitle: "Tiếng Anh, SAT, ACT...",
  },
  {
    step: 4,
    title: "Tài chính",
    short: "Tài chính",
    subtitle: "Ngân sách cho mỗi năm",
  },
  {
    step: 5,
    title: "Hoạt động & giải thưởng",
    short: "Hoạt động",
    subtitle: "Ngoại khóa, thành tích",
  },
  {
    step: 6,
    title: "Xem lại & nhận gợi ý",
    short: "Xem lại",
    subtitle: "Kiểm tra rồi gửi",
  },
];

const SHORT_LEVEL_LABELS: Record<CurrentEducationLevel, string> = {
  middle_school: "THCS",
  high_school: "THPT",
  college_university: "Cao đẳng / Đại học",
  master: "Thạc sĩ",
};

/** Tóm tắt dữ liệu đã điền của một bước; null khi bước chưa có gì → hiện subtitle mặc định */
function stepSummary(step: number, draft: ProfileFlowDraft, metadata: ProfileMetadataResponse | null): string | null {
  switch (step) {
    case 1: {
      const { currentEducationLevel, currentGrade, targetDegreeLevel } = draft.step1;
      if (!currentEducationLevel) return null;
      const target = metadata?.studyLevels.find((l) => l.key === targetDegreeLevel)?.label;
      return [SHORT_LEVEL_LABELS[currentEducationLevel], currentGrade].filter(Boolean).join(" · ") + (target ? ` → ${target}` : "");
    }
    case 2: {
      const years = draft.step2.academicYears ?? [];
      const { gpa10 } = calculateLinearGpa(years);
      if (gpa10 === null) return null;
      const grades = years.map((y) => y.gradeName.replace(/^Lớp\s*/, "")).join(", ");
      return `Lớp ${grades} · TB ${gpa10.toFixed(1)} / thang 10`;
    }
    case 3: {
      const certs = draft.step3.certifications ?? [];
      if (certs.length === 0) return draft.step3.englishPlanned ? "Chưa có, sẽ thi tiếng Anh sau" : null;
      const types = metadata?.certificationTypes ?? [];
      return certs
        .map((c) => {
          const name = getCertDisplayName(c, findCertType(types, c.type));
          return c.overall != null ? `${name} ${c.overall}` : name;
        })
        .join(" · ");
    }
    case 4: {
      const tier = draft.step4.annualBudgetTier;
      if (!tier) return null;
      return (
        metadata?.budgetTiers.find((t) => t.key === tier)?.label ??
        DEFAULT_BUDGET_TIERS.find((t) => t.key === tier)?.label ??
        null
      );
    }
    case 5: {
      const items = draft.step5.activities ?? [];
      if (items.length === 0) return null;
      const awards = items.filter((a) => a.category === "achievement").length;
      return `${items.length - awards} hoạt động · ${awards} giải`;
    }
    default:
      return null;
  }
}

interface ProfileFlowStepperProps {
  /** sidebar: cột bên phải; strip: dải ngang khi khung trợ lý đang mở */
  variant?: "sidebar" | "strip";
  draft: ProfileFlowDraft;
  metadata: ProfileMetadataResponse | null;
  currentStep: number;
  lastCompletedStep: number;
  onSelectStep: (step: number) => void;
}

export function ProfileFlowStepper({
  variant = "sidebar",
  draft,
  metadata,
  currentStep,
  lastCompletedStep,
  onSelectStep,
}: ProfileFlowStepperProps) {
  const stateOf = (step: number) => ({
    isCompleted: step < currentStep || step <= lastCompletedStep,
    isCurrent: step === currentStep,
    isClickable: step <= Math.max(currentStep, lastCompletedStep + 1),
  });

  if (variant === "strip") {
    return (
      <nav aria-label="Tiến trình hoàn thiện hồ sơ" className="overflow-x-auto rounded-xl border border-line bg-surface px-3 py-3.5 shadow-xs">
        {/* 6 cột đều nhau, chấm nằm giữa cột, đường nối chạy từ chấm trước tới chấm này */}
        <ol className="grid min-w-[456px] grid-cols-6">
          {FLOW_STEPS.map((s, i) => {
            const { isCompleted, isCurrent, isClickable } = stateOf(s.step);
            return (
              <li key={s.step} className="relative flex justify-center">
                {i > 0 && (
                  <span
                    aria-hidden="true"
                    className={cn(
                      "absolute top-[14px] right-[calc(50%+22px)] left-[calc(-50%+22px)] h-0.5 rounded-full",
                      isCompleted || isCurrent ? "bg-brand" : "bg-line"
                    )}
                  />
                )}
                <button
                  type="button"
                  disabled={!isClickable}
                  onClick={() => isClickable && onSelectStep(s.step)}
                  aria-current={isCurrent ? "step" : undefined}
                  title={s.title}
                  className={cn(
                    "flex flex-col items-center gap-1.5 text-center",
                    isClickable ? "cursor-pointer" : "cursor-not-allowed opacity-50"
                  )}
                >
                  <span
                    className={cn(
                      "flex size-[30px] items-center justify-center rounded-full border text-xs font-semibold transition-colors",
                      isCurrent && "border-brand bg-surface text-brand ring-4 ring-brand-wash",
                      !isCurrent && isCompleted && "border-brand bg-brand text-white",
                      !isCurrent && !isCompleted && "border-line bg-surface text-muted-foreground"
                    )}
                  >
                    {isCompleted && !isCurrent ? <CheckIcon className="size-3.5 stroke-[2.5]" /> : s.step}
                  </span>
                  <span
                    className={cn(
                      "text-[11.5px] leading-tight",
                      isCurrent ? "font-semibold text-brand" : "text-muted-foreground"
                    )}
                  >
                    {s.short}
                  </span>
                </button>
              </li>
            );
          })}
        </ol>
      </nav>
    );
  }

  return (
    <aside className="rounded-xl border border-line bg-surface p-6 shadow-xs">
      <div className="space-y-6">
        <div>
          <h2 className="text-label text-xs tracking-wider text-muted-foreground font-semibold">
            HỒ SƠ CỦA BẠN
          </h2>
        </div>

        <nav className="space-y-4" aria-label="Tiến trình hoàn thiện hồ sơ">
          {FLOW_STEPS.map((s) => {
            const { isCompleted, isCurrent, isClickable } = stateOf(s.step);
            const summary = stepSummary(s.step, draft, metadata);

            return (
              <button
                key={s.step}
                type="button"
                disabled={!isClickable}
                onClick={() => isClickable && onSelectStep(s.step)}
                className={cn(
                  "group flex w-full items-start gap-3 text-left transition-all rounded-lg p-2 -mx-2",
                  isCurrent && "bg-brand-wash/50",
                  isClickable && !isCurrent && "hover:bg-muted/50 cursor-pointer",
                  !isClickable && "opacity-45 cursor-not-allowed"
                )}
              >
                {/* Step indicator circle */}
                <div
                  className={cn(
                    "flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors mt-0.5",
                    isCurrent && "bg-brand text-white shadow-xs",
                    !isCurrent && isCompleted && "bg-ok text-white",
                    !isCurrent && !isCompleted && "bg-muted text-muted-foreground border border-line"
                  )}
                >
                  {isCompleted && !isCurrent ? (
                    <CheckIcon className="size-3.5 stroke-[2.5]" />
                  ) : (
                    <span>{s.step}</span>
                  )}
                </div>

                {/* Step text */}
                <div className="min-w-0 flex-1">
                  <div
                    className={cn(
                      "text-sm font-semibold leading-tight transition-colors",
                      isCurrent && "text-brand",
                      !isCurrent && isCompleted && "text-foreground group-hover:text-brand",
                      !isCurrent && !isCompleted && "text-muted-foreground"
                    )}
                  >
                    {s.title}
                  </div>
                  <div
                    className={cn(
                      "text-xs mt-0.5 leading-snug line-clamp-2",
                      summary ? "text-foreground/80" : "text-muted-foreground"
                    )}
                  >
                    {summary ?? s.subtitle}
                  </div>
                </div>
              </button>
            );
          })}
        </nav>

        {/* Helper Note */}
        <div className="rounded-xl border border-line/80 bg-ground/60 p-3.5 text-xs text-muted-foreground leading-relaxed">
          Mất khoảng 4 phút. Mọi thứ được lưu tự động. Bạn có thể quay lại bước đã làm để sửa,
          các bước sau sẽ mở khi bạn đi tới.
        </div>
      </div>
    </aside>
  );
}
