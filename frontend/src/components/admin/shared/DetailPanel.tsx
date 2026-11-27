"use client";

import { useEffect, useState, type ReactNode } from "react";
import { XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface DetailPanelProps {
  title: string;
  description?: string;
  onClose: () => void;
  closeDisabled?: boolean;
  footer?: ReactNode;
  className?: string;
  children: ReactNode;
}

export function DetailPanel({
  title,
  description,
  onClose,
  closeDisabled = false,
  footer,
  className,
  children,
}: DetailPanelProps) {
  return (
    <aside
      aria-label={title}
      className={cn(
        "flex w-full shrink-0 flex-col overflow-hidden rounded-xl bg-card ring-1 ring-border animate-in fade-in-0 slide-in-from-right-4 duration-200 motion-reduce:animate-none lg:h-full lg:min-h-0 lg:w-[26rem] xl:w-[30rem]",
        className,
      )}
    >
      <header className="flex shrink-0 items-start justify-between gap-3 border-b border-border/60 px-4 pt-4 pb-3">
        <div className="min-w-0">
          <h2 className="text-lg font-semibold leading-tight">{title}</h2>
          {description && <p className="text-sm text-muted-foreground">{description}</p>}
        </div>
        <Button type="button" variant="ghost" size="icon" aria-label="Đóng" onClick={onClose} disabled={closeDisabled}>
          <XIcon />
        </Button>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
        <div className="space-y-5">{children}</div>
      </div>
      {footer && (
        <footer className="flex shrink-0 flex-wrap items-center justify-end gap-2 border-t border-border bg-card px-4 py-3">
          {footer}
        </footer>
      )}
    </aside>
  );
}

const CLOSE_ANIMATION_MS = 200;

interface RightPanelSlotProps<T> {
  item: T | null | undefined;
  children: (active: T) => ReactNode;
}

/** Giữ nội dung panel 200ms khi đóng để chạy hiệu ứng trượt ra bên phải và thu gọn mượt mà. */
export function RightPanelSlot<T>({ item, children }: RightPanelSlotProps<T>) {
  const [retained, setRetained] = useState<T | null>(item ?? null);
  const [closing, setClosing] = useState(false);

  if (item != null && item !== retained) {
    setRetained(item);
    if (closing) setClosing(false);
  } else if (item == null && retained !== null && !closing) {
    setClosing(true);
  }

  useEffect(() => {
    if (!closing) return;
    const timer = window.setTimeout(() => {
      setRetained(null);
      setClosing(false);
    }, CLOSE_ANIMATION_MS);
    return () => window.clearTimeout(timer);
  }, [closing]);

  if (retained == null) return null;

  return (
    <div
      data-state={closing ? "closed" : "open"}
      className={cn(
        "w-full shrink-0 overflow-hidden transition-[width,opacity,transform] duration-200 ease-out motion-reduce:transition-none lg:h-full lg:min-h-0",
        closing
          ? "pointer-events-none translate-x-6 opacity-0 lg:w-0 xl:w-0"
          : "translate-x-0 opacity-100 lg:w-[26rem] xl:w-[30rem]",
      )}
    >
      {children(retained)}
    </div>
  );
}
