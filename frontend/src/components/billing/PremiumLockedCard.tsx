import Link from "next/link";
import { ArrowRightIcon, LockIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/** Thẻ khóa tính năng Premium (chat, gợi ý trường, mô phỏng): lời giải thích và nút "Nâng cấp USDSS Premium" sang /plans. */
export function PremiumLockedCard({
  title = "Tính năng này cần tài khoản USDSS Premium",
  note,
  ctaLabel = "Nâng cấp USDSS Premium",
  className,
}: {
  title?: string;
  note?: string | null;
  ctaLabel?: string;
  className?: string;
}) {
  return (
    <div className={cn("flex items-start gap-3 rounded-xl border border-brand/20 bg-brand-wash py-3.5 pr-4 pl-5", className)}>
      <LockIcon className="mt-0.5 size-4.5 shrink-0 text-brand" aria-hidden />
      <div className="min-w-0 space-y-2.5">
        <div>
          <p className="text-sm font-semibold text-ink">{title}</p>
          {note && <p className="text-xs leading-relaxed text-ink-2">{note}</p>}
        </div>
        <Link href="/plans" className={buttonVariants({ size: "sm", className: "rounded-lg font-semibold" })}>
          {ctaLabel}
          <ArrowRightIcon className="size-3.5" aria-hidden />
        </Link>
      </div>
    </div>
  );
}
