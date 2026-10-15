"use client";

import { CheckIcon } from "lucide-react";
import { cn } from "@/lib/utils";

export interface StepMeta {
  step: number;
  title: string;
  subtitle: string;
}

export const FLOW_STEPS: StepMeta[] = [
  {
    step: 1,
    title: "Bạn & mục tiêu",
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
    title: "Xem lại & nhận gợi ý",
    subtitle: "Kiểm tra rồi gửi",
  },
];

interface ProfileFlowStepperProps {
  currentStep: number;
  lastCompletedStep: number;
  onSelectStep: (step: number) => void;
}

export function ProfileFlowStepper({
  currentStep,
  lastCompletedStep,
  onSelectStep,
}: ProfileFlowStepperProps) {
  return (
    <aside className="rounded-2xl border border-line bg-surface p-6 shadow-xs sticky top-20">
      <div className="space-y-6">
        <div>
          <h2 className="text-label text-xs tracking-wider text-muted-foreground font-semibold">
            HỒ SƠ CỦA BẠN
          </h2>
        </div>

        <nav className="space-y-4" aria-label="Tiến trình hoàn thiện hồ sơ">
          {FLOW_STEPS.map((s) => {
            const isCompleted = s.step < currentStep || s.step <= lastCompletedStep;
            const isCurrent = s.step === currentStep;
            const isClickable = s.step <= Math.max(currentStep, lastCompletedStep + 1);

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
                  <div className="text-xs text-muted-foreground mt-0.5 leading-snug line-clamp-1">
                    {s.subtitle}
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
