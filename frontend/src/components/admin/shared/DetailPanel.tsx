"use client";

import type { ReactNode } from "react";
import { XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

interface DetailPanelProps {
  title: string;
  description?: string;
  onClose: () => void;
  closeDisabled?: boolean;
  children: ReactNode;
}

export function DetailPanel({ title, description, onClose, closeDisabled = false, children }: DetailPanelProps) {
  return (
    <aside
      aria-label={title}
      className="w-full shrink-0 rounded-xl bg-card p-4 ring-1 ring-border lg:h-full lg:min-h-0 lg:w-[26rem] lg:overflow-y-auto xl:w-[30rem]"
    >
      <div className="space-y-5">
        <header className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h2 className="text-lg font-semibold leading-tight">{title}</h2>
            {description && <p className="text-sm text-muted-foreground">{description}</p>}
          </div>
          <Button type="button" variant="ghost" size="icon" aria-label="Đóng" onClick={onClose} disabled={closeDisabled}>
            <XIcon />
          </Button>
        </header>
        {children}
      </div>
    </aside>
  );
}
