"use client";

import { usePathname } from "next/navigation";
import { isAdminRoute } from "@/lib/admin-nav";
import { isFullRoute, isWideRoute } from "@/lib/layout-routes";
import { cn } from "@/lib/utils";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { Toaster } from "@/components/ui/toast";
import { useAutoHideScrollbar } from "@/hooks/use-auto-hide-scrollbar";
import { AssistantLauncher, AssistantPanel, useAssistantVisible } from "./AssistantPanel";
import { useAssistant } from "./AssistantProvider";

/** Header + nội dung; khi khung trợ lý mở (≥900px) thì đẩy trang sang trái thay vì che */
export function AppShell({ children }: { children: React.ReactNode }) {
  useAutoHideScrollbar();
  const pathname = usePathname();
  const { open, wide } = useAssistant();
  const visible = useAssistantVisible();
  const adminRoute = isAdminRoute(pathname);
  const showAssistant = !adminRoute;
  const pushed = showAssistant && open && visible;

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
        {!adminRoute && <SiteHeader />}
        <main
          className={cn(
            "w-full flex-1",
            adminRoute
              ? "flex flex-col"
              : isFullRoute(pathname)
                ? "" // trang tự lo khoảng đệm: thanh bên dính mép, phần chính có lề riêng
                : cn("mx-auto px-4 py-8", isWideRoute(pathname) ? "max-w-[1320px] sm:px-6" : "max-w-5xl"),
          )}
        >
          {children}
        </main>
      </div>
      {showAssistant && <AssistantPanel />}
      {showAssistant && <AssistantLauncher />}
      <Toaster />
    </div>
  );
}
