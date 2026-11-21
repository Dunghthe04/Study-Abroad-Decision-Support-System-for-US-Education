import Link from "next/link";
import { ArrowRightIcon, LockIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/** Thẻ khóa tính năng Premium (chat, gợi ý trường, mô phỏng): lời giải thích và nút "Nâng cấp USDSS Premium" sang /plans. */
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
    <div className={cn("flex items-start gap-3 rounded-xl border border-brand/20 bg-brand-wash p-4", className)}>
      <span className="flex size-9 shrink-0 items-center justify-center rounded-xl border border-brand/15 bg-surface text-brand">
        <LockIcon className="size-4" aria-hidden />
      </span>
      <div className="min-w-0 space-y-2">
        <p className="text-sm font-semibold text-ink">{title}</p>
        {note && <p className="text-xs leading-relaxed text-ink-2">{note}</p>}
        <Link href="/plans" className={buttonVariants({ size: "sm", className: "rounded-lg font-semibold" })}>
          Nâng cấp USDSS Premium
          <ArrowRightIcon className="size-3.5" aria-hidden />
        </Link>
      </div>
    </div>
  );
}
