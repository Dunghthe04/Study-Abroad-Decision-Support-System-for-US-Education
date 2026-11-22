"use client";

import { useState, type ReactNode } from "react";
import { CheckIcon, ListIcon, ListChecksIcon, PencilIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { useGradeScale } from "@/hooks/use-grade-scale";
import { cn } from "@/lib/utils";
import { findCertType, getCertDisplayName } from "@/lib/certification-utils";
import { DEFAULT_BUDGET_TIERS, draftGpa, gpaModeOf } from "@/lib/profile-flow-options";
import type { CurrentEducationLevel, ProfileFlowDraft, ProfileMetadataResponse } from "@/types/profile-flow";

export interface StepMeta {
  step: number;
  title: string;
  subtitle: string;
}

export const FLOW_STEPS: StepMeta[] = [
  {
    step: 1,
    title: "Mục tiêu của bạn",
    subtitle: "Cấp học hiện tại, bậc muốn học",
  },
  {
    step: 2,
    title: "Bảng điểm",
    subtitle: "Tải lên hoặc nhập tay",
  },
  {
    step: 3,
    title: "Chứng chỉ",
    subtitle: "Tiếng Anh, SAT, ACT...",
  },
  {
    step: 4,
    title: "Tài chính",
    subtitle: "Ngân sách cho mỗi năm",
  },
  {
    step: 5,
    title: "Hoạt động & giải thưởng",
    subtitle: "Ngoại khóa, thành tích",
  },
  {
    step: 6,
    title: "Xem lại và gửi",
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
      const { gpa10, gpa4 } = draftGpa(draft.step2);
      if (gpaModeOf(draft.step2) === "overall") {
        if (gpa10 !== null) return `GPA ${gpa10.toFixed(1)} / thang 10`;
        return gpa4 !== null ? `GPA ${gpa4.toFixed(2)} / thang 4` : null;
      }
      if (gpa10 === null) return null;
      const years = draft.step2.academicYears ?? [];
      const grades = years.map((y) => y.gradeName.replace(/^Lớp\s*/, "")).join(", ");
      return `Lớp ${grades} · TB ${gpa10.toFixed(1)} / thang 10`;
    }
    case 3: {
      const certs = draft.step3.certifications ?? [];
      if (certs.length === 0) return draft.step3.englishPlanned ? "Chưa thi, chưa có dự định thi" : null;
      const types = metadata?.certificationTypes ?? [];
      return certs
        .map((c) => {
          const name = getCertDisplayName(c, findCertType(types, c.type));
          const label = c.overall != null ? `${name} ${c.overall}` : name;
          return c.isTarget ? `${label} (dự định)` : label;
        })
        .join(" · ");
    }
    case 4: {
      const tier = draft.step4.annualBudgetTier;
      if (!tier) return null;
      // UC-94: có số tiền tối đa/năm chính xác (phụ huynh nhập) thì ghi đúng số đó
      const exactUsd = draft.step4.annualBudgetUsd;
      if (exactUsd != null) return `Tối đa ${exactUsd.toLocaleString("vi-VN")} USD/năm`;
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

/** Mục "Nhu cầu" trên đầu danh sách bước: onEdit có thì bấm để sửa, không có thì chỉ đọc kèm ghi chú */
export interface NeedsEntry {
  labels: string;
  note?: string;
  onEdit?: () => void;
}

interface ProfileFlowStepperProps {
  /** sidebar: cột trái từ 1024px; compact: thanh dính trên đầu + ngăn kéo (màn hẹp hoặc khung trợ lý đang mở) */
  variant?: "sidebar" | "compact";
  draft: ProfileFlowDraft;
  metadata: ProfileMetadataResponse | null;
  currentStep: number;
  lastCompletedStep: number;
  onSelectStep: (step: number) => void;
  needs?: NeedsEntry | null;
  /** Trạng thái lưu, "Tiếp tục bằng chat", Thoát */
  footer?: ReactNode;
  /** compact: ngăn kéo mở từ dưới, hoặc từ trái khi khung trợ lý đang mở */
  sheetSide?: "bottom" | "left";
  /** compact: ẩn từ 1024px khi khung trợ lý đóng (đã có sidebar) */
  className?: string;
}

export function ProfileFlowStepper({
  variant = "sidebar",
  draft,
  metadata,
  currentStep,
  lastCompletedStep,
  onSelectStep,
  needs,
  footer,
  sheetSide = "bottom",
  className,
}: ProfileFlowStepperProps) {
  useGradeScale(); // GPA thang 4 quy đổi theo bảng moderator sửa, vẽ lại khi bảng tải xong
  const [sheetOpen, setSheetOpen] = useState(false);
  const stateOf = (step: number) => ({
    isCompleted: step < currentStep || step <= lastCompletedStep,
    isCurrent: step === currentStep,
    isClickable: step <= Math.max(currentStep, lastCompletedStep + 1),
  });

  const list = (afterPick?: () => void) => (
    <nav aria-label="Tiến trình hồ sơ">
      {needs && (
        <div className="mb-6 flex items-start gap-3">
          <span aria-hidden className="flex size-8 shrink-0 items-center justify-center rounded-full bg-brand-wash text-brand">
            <ListChecksIcon className="size-4" />
          </span>
          <div className="min-w-0 flex-1">
            <div className="text-sm font-semibold leading-8 text-ink">Nhu cầu</div>
            <div className="text-xs leading-snug text-ink-2">{needs.labels}</div>
            {needs.note && <div className="mt-0.5 text-xs leading-snug text-ink-2">{needs.note}</div>}
          </div>
          {needs.onEdit && (
            <button
              type="button"
              onClick={() => {
                afterPick?.();
                needs.onEdit?.();
              }}
              className="mt-1 flex size-6 shrink-0 cursor-pointer items-center justify-center rounded-md text-ink-2 hover:bg-brand-wash hover:text-brand focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
            >
              <PencilIcon className="size-3.5" aria-hidden />
              <span className="sr-only">Sửa nhu cầu</span>
            </button>
          )}
        </div>
      )}
      <ol>
        {FLOW_STEPS.map((s, i) => {
          const { isCompleted, isCurrent, isClickable } = stateOf(s.step);
          const done = isCompleted && !isCurrent;
          const summary = stepSummary(s.step, draft, metadata);
          return (
            <li key={s.step} className="relative pb-6 last:pb-0">
              {i < FLOW_STEPS.length - 1 && (
                <span
                  aria-hidden
                  className={cn("absolute top-9 -bottom-1 left-[14px] w-1 rounded-full", isCompleted ? "bg-ok/45" : "bg-ok-wash")}
                />
              )}
              <button
                type="button"
                disabled={!isClickable}
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => {
                  if (!isClickable) return;
                  onSelectStep(s.step);
                  afterPick?.();
                }}
                className="group flex w-full items-start gap-3 rounded-lg text-left focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
              >
                <span
                  className={cn(
                    "relative flex size-8 shrink-0 items-center justify-center rounded-full text-sm font-semibold transition-colors",
                    done && "bg-ok text-white",
                    isCurrent && "bg-brand text-brand-foreground ring-4 ring-brand-wash",
                    !done && !isCurrent && "border-2 border-line bg-surface text-ink-2",
                    !isClickable && "opacity-(--cds-opacity-disabled)"
                  )}
                >
                  {done ? <CheckIcon className="flow-pop size-4 stroke-[2.5]" aria-hidden /> : s.step}
                </span>
                <span className={cn("min-w-0 flex-1", !isClickable && "opacity-(--cds-opacity-disabled)")}>
                  <span
                    className={cn(
                      "block text-sm font-semibold leading-8 transition-colors",
                      isCurrent ? "text-brand" : isClickable ? "text-ink group-hover:text-brand" : "text-ink-2"
                    )}
                  >
                    {s.title}
                  </span>
                  {(summary || isCurrent) && (
                    <span className="-mt-1 block text-xs leading-snug text-ink-2 line-clamp-2">
                      {summary ?? s.subtitle}
                    </span>
                  )}
                </span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );

  if (variant === "compact") {
    const current = FLOW_STEPS.find((s) => s.step === currentStep);
    return (
      <div className={cn("sticky top-(--header-h) z-20 border-b border-line bg-surface/95 px-4 py-3 backdrop-blur", className)}>
        <div className="flex items-center justify-between gap-3">
          <p className="min-w-0 truncate text-sm font-semibold text-ink">
            Bước {currentStep}/{FLOW_STEPS.length} · {current?.title}
          </p>
          <Button type="button" variant="ghost" size="sm" onClick={() => setSheetOpen(true)} className="gap-1.5">
            <ListIcon className="size-4" aria-hidden />
            Các bước
          </Button>
        </div>
        <div className="mt-2 h-1 overflow-hidden rounded-full bg-ok-wash">
          <div
            className="h-full rounded-full bg-ok transition-[width] duration-300"
            style={{ width: `${(currentStep / FLOW_STEPS.length) * 100}%` }}
          />
        </div>
        <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
          <SheetContent
            side={sheetSide}
            className={cn("overflow-y-auto bg-ground px-5 pb-6", sheetSide === "bottom" ? "max-h-[85dvh] rounded-t-2xl pt-4" : "pt-4")}
          >
            <SheetHeader className="px-0">
              <SheetTitle className="text-label">Hồ sơ chọn trường</SheetTitle>
            </SheetHeader>
            {list(() => setSheetOpen(false))}
            {footer && (
              <div onClick={() => setSheetOpen(false)} className="mt-2 flex flex-col items-start gap-3 border-t border-line pt-4">
                {footer}
              </div>
            )}
          </SheetContent>
        </Sheet>
      </div>
    );
  }

  return (
    <aside className="flow-rail-enter hidden border-r border-line bg-ground px-5 py-8 lg:sticky lg:top-(--header-h) lg:flex lg:h-[calc(100dvh-var(--header-h))] lg:flex-col lg:overflow-y-auto">
      <h2 className="text-label mb-6">Hồ sơ chọn trường</h2>
      {list()}
      {footer && <div className="mt-auto flex flex-col items-start gap-3 pt-8">{footer}</div>}
    </aside>
  );
}
