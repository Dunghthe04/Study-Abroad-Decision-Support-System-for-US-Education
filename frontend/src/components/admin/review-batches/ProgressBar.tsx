import { formatInt } from "@/lib/ai-usage-format";
import { progressOf } from "@/lib/review-batch-format";

/** Thanh tiến độ chấm: "Bạn đã chấm 7 / 21 câu". Có chữ và aria, không chỉ màu. */
export function ProgressBar({ graded, total, label }: { graded: number; total: number; label: string }) {
  const progress = progressOf(graded, total);
  return (
    <div className="flex flex-col gap-1">
      <p className="text-sm text-ink-2">
        {label} <span className="tabular font-medium text-ink">{formatInt(progress.graded)} / {formatInt(progress.total)}</span> câu ({progress.percent}%)
      </p>
      <div
        role="progressbar" aria-label={label} aria-valuemin={0} aria-valuemax={progress.total} aria-valuenow={progress.graded}
        className="h-2 w-full overflow-hidden rounded-full bg-ground"
      >
        <div className="h-full rounded-full bg-brand transition-[width]" style={{ width: `${progress.percent}%` }} />
      </div>
    </div>
  );
}
