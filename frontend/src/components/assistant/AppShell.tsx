"use client";

import { usePathname } from "next/navigation";
import { isAdminRoute } from "@/lib/admin-nav";
import { useHeaderHidden } from "@/lib/header-visibility";
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
  const { open, panelMode } = useAssistant();
  const visible = useAssistantVisible();
  const adminRoute = isAdminRoute(pathname);
  const authRoute = isAuthRoute(pathname);
  // /analysis: biểu mẫu tự cuộn bên trong, trang không được cao hơn màn hình (tránh thanh cuộn thừa ở đáy)
  const lockedViewport = pathname === "/universities" || pathname === "/scholarships" || pathname.startsWith("/analysis");
  const showAssistant = !adminRoute && !authRoute;
  const docked = panelMode === "compact" || panelMode === "half";
  const pushed = showAssistant && open && visible && docked;

  // Trang phân tích cho ẩn thanh trên cùng để dành chỗ cho biểu mẫu; nút ở thanh đầu bước bật/tắt
  const hideable = pathname.startsWith("/analysis");
  const headerHidden = useHeaderHidden();
  const collapsed = hideable && headerHidden;

  return (
    <div
      className={cn(
        "flex min-h-full flex-1 flex-col",
        lockedViewport && "lg:h-dvh lg:max-h-dvh lg:flex-none lg:overflow-hidden",
      )}
      style={
        {
          "--assistant-w": panelMode === "half" ? "50vw" : "400px",
          ...(collapsed && { "--header-h": "0px" }),
        } as React.CSSProperties
      }
    >
      <div
        className={cn(
          "flex flex-1 flex-col transition-[padding] duration-200",
          lockedViewport && "lg:min-h-0 lg:overflow-hidden",
          pushed && "min-[900px]:pr-(--assistant-w)",
          // Trang phân tích: khung chat nổi trên nền xanh đậm cùng màu thanh bước
          pushed && hideable && "min-[900px]:bg-brand",
        )}
      >
        {!adminRoute && !authRoute && !collapsed && <SiteHeader />}
        {!adminRoute && !authRoute && !collapsed && <ActingAsBanner />}
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
      {/* Trang phân tích có nút chat ở thanh bước; nút nổi sẽ che nút "Tiếp tục" */}
      {showAssistant && !hideable && <AssistantLauncher />}
      <Toaster />
    </div>
  );
}
