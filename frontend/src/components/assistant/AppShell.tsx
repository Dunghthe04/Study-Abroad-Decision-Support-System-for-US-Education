"use client";

import { usePathname } from "next/navigation";
import { isAdminRoute } from "@/lib/admin-nav";
import { isAuthRoute, isFullRoute, isWideRoute } from "@/lib/layout-routes";
import { cn } from "@/lib/utils";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { ActingAsBanner } from "@/components/parent/ActingAsBanner";
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
  const authRoute = isAuthRoute(pathname);
  const lockedViewport = pathname === "/universities";
  const showAssistant = !adminRoute && !authRoute;
  const pushed = showAssistant && open && visible;

  return (
    <div
      className={cn(
        "flex min-h-full flex-1 flex-col",
        lockedViewport && "lg:h-dvh lg:max-h-dvh lg:flex-none lg:overflow-hidden",
      )}
      style={
        {
          "--assistant-w": wide ? "clamp(400px, calc(100vw - 600px), 680px)" : "400px",
        } as React.CSSProperties
      }
    >
      <div
        className={cn(
          "flex flex-1 flex-col transition-[padding] duration-200",
          lockedViewport && "lg:min-h-0 lg:overflow-hidden",
          pushed && "min-[900px]:pr-(--assistant-w)",
        )}
      >
        {!adminRoute && !authRoute && <SiteHeader />}
        {!adminRoute && !authRoute && <ActingAsBanner />}
        <div id="page-banner-slot" />
        <main
          className={cn(
            "w-full flex-1",
            adminRoute || authRoute
              ? "flex flex-col"
              : isFullRoute(pathname)
                ? lockedViewport
                  ? "lg:flex lg:min-h-0 lg:flex-col lg:overflow-hidden"
                  : "" // trang tự lo khoảng đệm: thanh bên dính mép, phần chính có lề riêng
                : cn("mx-auto px-4 py-8", isWideRoute(pathname) ? "max-w-[1560px] px-4 sm:px-8 lg:px-12" : "max-w-5xl"),
          )}
        >
          {children}
        </main>
        {(pathname === "/" || pathname === "/methodology") && <SiteFooter />}
      </div>
      {showAssistant && <AssistantPanel />}
      {showAssistant && <AssistantLauncher />}
      <Toaster />
    </div>
  );
}
