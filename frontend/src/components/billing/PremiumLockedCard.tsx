import Link from "next/link";
import { LockIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/** Thẻ khóa tính năng Premium (chat, gợi ý trường, mô phỏng): lời giải thích và nút "Xem gói" sang /plans. */
export function PremiumLockedCard({
  title = "Tính năng này cần tài khoản USDSS Premium",
  note,
  className,
}: {
  title?: string;
  note?: string | null;
  className?: string;
}) {
  return (
    <div className={cn("flex items-start gap-3 rounded-xl border border-brand/20 bg-brand-wash p-3.5", className)}>
      <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-surface text-brand">
        <LockIcon className="size-4" aria-hidden />
      </span>
      <div className="min-w-0 space-y-2">
        <p className="text-sm font-semibold text-ink">{title}</p>
        {note && <p className="text-xs text-ink-2">{note}</p>}
        <Link href="/plans" className={buttonVariants({ size: "sm" })}>
          Xem gói
        </Link>
      </div>
    </div>
  );
}
