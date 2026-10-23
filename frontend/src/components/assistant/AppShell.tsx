"use client";

import { usePathname } from "next/navigation";
import { isWideRoute } from "@/lib/layout-routes";
import { cn } from "@/lib/utils";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { AssistantLauncher, AssistantPanel, useAssistantVisible } from "./AssistantPanel";
import { useAssistant } from "./AssistantProvider";

/** Header + nội dung; khi khung trợ lý mở (≥900px) thì đẩy trang sang trái thay vì che */
export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { open, wide } = useAssistant();
  const visible = useAssistantVisible();
  const pushed = open && visible;

  return (
    <div
      className="flex min-h-full flex-1 flex-col"
      style={
        {
          "--assistant-w": wide ? "clamp(400px, calc(100vw - 600px), 680px)" : "400px",
        } as React.CSSProperties
      }
    >
      <div className={cn("flex flex-1 flex-col transition-[padding] duration-200", pushed && "min-[900px]:pr-(--assistant-w)")}>
        <SiteHeader />
        <main
          className={cn(
            "mx-auto w-full flex-1 px-4 py-8",
            isWideRoute(pathname) ? "max-w-[1320px] sm:px-6" : "max-w-5xl"
          )}
        >
          {children}
        </main>
      </div>
      <AssistantPanel />
      <AssistantLauncher />
    </div>
  );
}
