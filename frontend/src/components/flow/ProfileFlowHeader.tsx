"use client";

import Link from "next/link";
import { CheckIcon, Loader2Icon, AlertCircleIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

interface ProfileFlowHeaderProps {
  saveStatus: "saved" | "saving" | "error" | "idle";
  onExit: () => void;
}

export function ProfileFlowHeader({ saveStatus, onExit }: ProfileFlowHeaderProps) {
  return (
    <header className="sticky top-0 z-30 border-b border-line bg-surface/95 backdrop-blur-sm">
      <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        {/* Brand */}
        <div className="flex items-center gap-3">
          <Link
            href="/"
            className="flex items-center gap-2 font-heading text-lg font-bold tracking-tight text-brand-ink transition-opacity hover:opacity-90"
          >
            <span className="flex size-7 items-center justify-center rounded-lg bg-brand text-white font-sans text-xs font-black shadow-xs">
              US
            </span>
            <span>USAS</span>
          </Link>
          <span className="hidden sm:inline-block text-xs font-medium text-muted-foreground border-l border-line pl-3">
            Hồ sơ chọn trường
          </span>
        </div>

        {/* Save Status & Exit Button */}
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5 text-xs">
            {saveStatus === "saving" && (
              <span className="inline-flex items-center gap-1 text-muted-foreground animate-pulse">
                <Loader2Icon className="size-3.5 animate-spin text-brand" />
                <span>Đang lưu nháp...</span>
              </span>
            )}
            {saveStatus === "saved" && (
              <span className="inline-flex items-center gap-1 text-ok font-medium">
                <CheckIcon className="size-3.5 text-ok" />
                <span>Đã lưu nháp</span>
              </span>
            )}
            {saveStatus === "error" && (
              <span className="inline-flex items-center gap-1 text-warn font-medium">
                <AlertCircleIcon className="size-3.5 text-warn" />
                <span>Chưa lưu được nháp</span>
              </span>
            )}
            {saveStatus === "idle" && (
              <span className="text-muted-foreground">Tự động lưu nháp</span>
            )}
          </div>

          <Button
            variant="ghost"
            size="sm"
            onClick={onExit}
            className="text-muted-foreground hover:text-foreground text-xs gap-1"
          >
            <XIcon className="size-3.5" />
            Thoát
          </Button>
        </div>
      </div>
    </header>
  );
}
