"use client";

import { useState, type ReactNode } from "react";
import { CheckIcon, ChevronRightIcon, ListIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { cn } from "@/lib/utils";
import { HeaderToggle } from "./StepChrome";

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

/** Mục "Nhu cầu" trên đầu danh sách bước: onEdit có thì bấm để sửa, không có thì chỉ đọc kèm ghi chú */
export interface NeedsEntry {
  labels: string;
  note?: string;
  onEdit?: () => void;
}

interface ProfileFlowStepperProps {
  /** sidebar: cột trái từ 1024px; compact: thanh dính trên đầu + ngăn kéo (màn hẹp hoặc khung trợ lý đang mở) */
  variant?: "sidebar" | "compact";
  currentStep: number;
  /** Các bước 1–5 đã điền đủ mục bắt buộc */
  readySteps: number[];
  onSelectStep: (step: number) => void;
  needs?: NeedsEntry | null;
  /** Nội dung đáy thanh bên (hiện không dùng) */
  footer?: ReactNode;
  /** compact: ngăn kéo mở từ dưới, hoặc từ trái khi khung trợ lý đang mở */
  sheetSide?: "bottom" | "left";
  /** compact: ẩn từ 1024px khi khung trợ lý đóng (đã có sidebar) */
  className?: string;
}

export function ProfileFlowStepper({
  variant = "sidebar",
  currentStep,
  readySteps,
  onSelectStep,
  needs,
  footer,
  sheetSide = "bottom",
  className,
}: ProfileFlowStepperProps) {
  const [sheetOpen, setSheetOpen] = useState(false);
  // Bước 1–5 mở tự do, điền theo thứ tự nào cũng được; "Xem lại và gửi" chỉ mở khi cả 5 bước đã đủ.
  // Dấu tích theo dữ liệu thật của bước; vòng quay ở bước đang mở mà chưa đủ
  const reviewUnlocked = [1, 2, 3, 4, 5].every((s) => readySteps.includes(s));
  const stateOf = (step: number) => ({
    isCompleted: readySteps.includes(step),
    isWorking: step === currentStep && !readySteps.includes(step),
    isCurrent: step === currentStep,
    isClickable: step < 6 || reviewUnlocked,
  });

  const list = (afterPick?: () => void) => (
    <nav aria-label="Tiến trình hồ sơ">
      <ol className="space-y-1">
        {FLOW_STEPS.map((s) => {
          const { isCompleted, isWorking, isCurrent, isClickable } = stateOf(s.step);
          const done = isCompleted;
          return (
            <li key={s.step} className="relative">
              <button
                type="button"
                disabled={!isClickable}
                aria-current={isCurrent ? "step" : undefined}
                onClick={() => {
                  if (!isClickable) return;
                  onSelectStep(s.step);
                  afterPick?.();
                }}
                className={cn(
                  "group relative flex w-full cursor-pointer items-center gap-3 rounded-xl px-3 py-2.5 text-left transition-[background-color,box-shadow] duration-200 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-white/50 disabled:cursor-default",
                  isCurrent ? "bg-white/12 ring-1 ring-white/20" : isClickable && "hover:bg-white/8",
                )}
              >
                <StepIcon state={done ? "done" : isWorking ? "current" : "todo"} />
                <span className={cn("min-w-0 flex-1", !isClickable && "opacity-(--cds-opacity-disabled)")}>
                  <span className={cn("block truncate font-heading text-[0.9375rem]", isCurrent ? "font-semibold text-white" : done ? "font-medium text-white" : "text-white/70")}>
                    {s.title}
                  </span>
                </span>
                {isCurrent && <ChevronRightIcon aria-hidden className="size-4 shrink-0 text-white/70" />}
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );

  const needsBlock = needs && (
    <div className="flex items-center gap-3 rounded-xl border border-dashed border-white/30 px-3.5 py-3">
      <div className="min-w-0 flex-1">
        <div className="text-xs text-white/65">Nhu cầu của bạn</div>
        <div className="mt-0.5 font-heading text-[0.9375rem] text-white">{needs.labels}</div>
        {needs.note && <div className="mt-0.5 text-xs text-white/65">{needs.note}</div>}
      </div>
      {needs.onEdit && (
        <Button
          type="button"
          size="sm"
          onClick={needs.onEdit}
          aria-label="Đổi nhu cầu"
          className="shrink-0 bg-brand-wash text-brand hover:bg-white"
        >
          Đổi
        </Button>
      )}
    </div>
  );

  if (variant === "compact") {
    const current = FLOW_STEPS.find((s) => s.step === currentStep);
    return (
      <div
        className={cn(
          // Bản ngang thu gọn của thanh bước bên trái: nền xanh đậm nhạt hơn một bậc, cùng biểu tượng bước.
          // Màn hẹp dính dưới thanh trên cùng; từ 1024px trang không cuộn nên để yên ở đầu bảng
          "@container sticky top-(--header-h) z-20 bg-[color-mix(in_oklab,var(--brand),white_14%)] px-3 py-2 text-white lg:static",
          className,
        )}
      >
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setSheetOpen(true)}
            aria-label={`Các bước – đang ở ${current?.title ?? ""}`}
            className="shrink-0 gap-1.5 text-white hover:bg-white/10 hover:text-white"
          >
            <ListIcon className="size-4" aria-hidden />
            <span className="hidden sm:inline">Các bước</span>
          </Button>
          <ol aria-label="Tiến trình hồ sơ" className="flex min-w-0 flex-1 items-center justify-center-safe gap-1 overflow-x-auto">
            {FLOW_STEPS.map((s) => {
              const { isCompleted, isWorking, isCurrent, isClickable } = stateOf(s.step);
              return (
                <li key={s.step} className={cn(isCurrent && "min-w-0")}>
                  <button
                    type="button"
                    disabled={!isClickable}
                    aria-current={isCurrent ? "step" : undefined}
                    aria-label={s.title}
                    onClick={() => isClickable && onSelectStep(s.step)}
                    className={cn(
                      "flex min-w-0 cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-white/50 disabled:cursor-default",
                      isCurrent ? "bg-white/12 ring-1 ring-white/20" : isClickable && "hover:bg-white/8",
                      !isClickable && "opacity-(--cds-opacity-disabled)",
                    )}
                  >
                    <StepIcon state={isCompleted ? "done" : isWorking ? "current" : "todo"} />
                    {/* Đủ chỗ thì hiện tên mọi bước; hẹp thì chỉ bước đang mở có tên, các bước khác còn biểu tượng */}
                    <span
                      className={cn(
                        "truncate font-heading text-sm",
                        isCurrent ? "text-white" : "hidden text-white/80 @5xl:inline",
                      )}
                    >
                      {s.title}
                    </span>
                  </button>
                </li>
              );
            })}
          </ol>
          <HeaderToggle tone="inverse" />
        </div>
        <Sheet open={sheetOpen} onOpenChange={setSheetOpen}>
          <SheetContent
            side={sheetSide}
            className={cn("overflow-y-auto bg-brand px-4 pb-6 text-white", sheetSide === "bottom" ? "max-h-[85dvh] rounded-t-2xl pt-4" : "pt-4")}
          >
            <SheetHeader className="px-1">
              <SheetTitle className="text-base font-semibold text-white">Hồ sơ chọn trường</SheetTitle>
            </SheetHeader>
            {list(() => setSheetOpen(false))}
            {needsBlock && <div className="mt-6" onClick={() => setSheetOpen(false)}>{needsBlock}</div>}
            {footer && <div className="mt-6">{footer}</div>}
          </SheetContent>
        </Sheet>
      </div>
    );
  }

  return (
    <aside className="flow-rail-enter hidden w-[300px] shrink-0 flex-col bg-brand px-5 py-6 text-white lg:flex lg:h-full lg:overflow-y-auto">
      <h2 className="font-heading text-2xl leading-snug text-white">Hồ sơ chọn trường</h2>
      <p className="mt-2 text-sm leading-relaxed text-white/70">
        Cho chúng tôi biết điểm số, chứng chỉ và ngân sách của bạn để tìm ra những trường Mỹ phù hợp nhất.
      </p>
      <div className="mt-6">{list()}</div>
      <div className="mt-auto space-y-4 pt-6">
        {needsBlock}
        {footer}
      </div>
    </aside>
  );
}

/** Biểu tượng trạng thái bước: xong = vòng đỏ có dấu tích, đang làm = vòng đỏ quay quanh chấm đỏ, chưa tới = vòng rỗng */
function StepIcon({ state }: { state: "done" | "current" | "todo" }) {
  if (state === "done") {
    return (
      <span aria-hidden className="flow-pop flex size-5 shrink-0 items-center justify-center rounded-full bg-risk text-white">
        <CheckIcon className="size-3 stroke-[3]" />
      </span>
    );
  }
  if (state === "current") {
    return (
      <span aria-hidden className="relative flex size-5 shrink-0 items-center justify-center">
        {/* Vòng đỏ quay quanh chấm đỏ: bước đang làm; giảm chuyển động thì vòng đứng yên */}
        <span className="absolute inset-0 animate-spin rounded-full border-2 border-risk/30 border-t-risk [animation-duration:1.2s] motion-reduce:animate-none" />
        <span className="size-2.5 rounded-full bg-risk" />
      </span>
    );
  }
  return <span aria-hidden className="size-5 shrink-0 rounded-full border-2 border-white/35 bg-brand" />;
}
