"use client";

import { cn } from "@/lib/utils";
import { SCHOLARSHIP_NEED_OPTIONS } from "@/lib/profile-flow-options";

/** Câu F2: ba thẻ mức cần học bổng giống bước 4 của form; bấm thẻ thì gửi đúng chữ của thẻ như tin nhắn */
export function ScholarshipWidget({ data, onPick }: { data?: Record<string, unknown> | null; onPick?: (text: string) => void }) {
  // advisor gửi options [{value, label}] (buttons.SCHOLARSHIP_LABELS); thiếu thì lấy chữ của form
  const options = (data?.options as { value: string; label: string }[] | undefined) ?? [];
  const labels = Object.fromEntries(options.map((o) => [o.value, o.label]));
  // Đang sửa hồ sơ: đánh dấu thẻ đang lưu (data.initial.need)
  const current = (data?.initial as { need?: string } | undefined)?.need;
  return (
    <div className="mt-2.5 grid gap-2 sm:grid-cols-3">
      {SCHOLARSHIP_NEED_OPTIONS.map((opt) => {
        const label = labels[opt.value] ?? opt.label;
        const selected = opt.value === current;
        return (
          <button
            key={opt.value}
            type="button"
            disabled={!onPick}
            onClick={() => onPick?.(label)}
            className={cn(
              "flex flex-col items-start rounded-xl border border-line bg-surface p-3 text-left transition-[color,background-color,border-color,transform,scale] duration-[var(--cds-dur-fast)] focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 active:not-disabled:scale-[0.975] motion-reduce:transition-none motion-reduce:active:scale-100 cursor-pointer disabled:cursor-default",
              selected && "border-brand ring-1 ring-brand",
              onPick ? "hover:border-brand/40 hover:bg-muted/30" : "opacity-60"
            )}
          >
            <span className="flex items-center gap-2 text-sm font-semibold text-foreground">
              <span className={cn("size-3.5 rounded-full border", selected ? "border-brand bg-brand" : "border-line")} />
              {label}
            </span>
            <span className="mt-1 text-xs text-muted-foreground">{opt.description}</span>
          </button>
        );
      })}
    </div>
  );
}
