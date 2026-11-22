import type { ReactNode } from "react";
import { ArrowLeftIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

/** Tiêu đề bước: h1 nhận focus khi đổi bước (Wizard tìm theo data-step-heading) */
export function StepHeader({ title, lede }: { title: string; lede?: ReactNode }) {
  return (
    <header className="space-y-2">
      <h1 data-step-heading tabIndex={-1} className="text-h1 text-foreground outline-none">
        {title}
      </h1>
      {lede && <p className="text-body text-ink-2">{lede}</p>}
    </header>
  );
}

/** Hàng nút cuối bước: "Quay lại" bên trái (bước 1 không có), nút chính (children) bên phải */
export function StepNav({ onPrev, children }: { onPrev?: () => void; children: ReactNode }) {
  return (
    <div className="mt-12 flex items-center justify-between gap-3">
      {onPrev ? (
        <Button type="button" variant="ghost" size="lg" onClick={onPrev} className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
      ) : (
        <span />
      )}
      {children}
    </div>
  );
}

/** Kiểu thẻ lựa chọn duy nhất của luồng hồ sơ: thẻ cần role=radio/checkbox + aria-checked */
export const CHOICE_CARD =
  "cursor-pointer rounded-xl border border-line bg-surface p-4 text-left transition-colors hover:border-brand/40 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-checked:border-brand aria-checked:bg-brand-wash/50 aria-checked:ring-1 aria-checked:ring-brand aria-disabled:cursor-default";

/** Viên thuốc lựa chọn (cấp học, ngành phổ biến, bang...) */
export const CHOICE_PILL =
  "cursor-pointer rounded-full border border-line bg-surface px-3.5 py-1.5 text-sm font-medium text-foreground transition-colors hover:border-brand/40 focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-checked:border-brand aria-checked:bg-brand aria-checked:text-brand-foreground";
