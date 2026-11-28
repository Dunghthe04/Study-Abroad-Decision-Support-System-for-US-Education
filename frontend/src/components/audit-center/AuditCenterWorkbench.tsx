"use client";

import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { ExternalLinkIcon } from "lucide-react";
import { FloatingAuditDock } from "@/components/audit-shared/FloatingAuditDock";
import { useAuth } from "@/contexts/AuthContext";
import type { UserRole } from "@/types/api";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { PublicCenterListView } from "@/components/centers/PublicCenterListView";
import { StudyCenterDetailView } from "@/components/centers/StudyCenterDetailView";
import { CenterCompareView } from "@/components/centers/CenterCompareView";
import { CenterStatusBanner } from "@/components/center/CenterStatusBanner";
import { CenterNav, CenterNavPathProvider } from "@/components/center/CenterNav";
import { CenterDashboardView } from "@/components/center/CenterDashboardView";
import { CenterLeadsListView } from "@/components/center/leads/CenterLeadsListView";
import { CenterReviewsPortalView } from "@/components/center/CenterReviewsPortalView";
import { CenterVerificationView } from "@/components/center/CenterVerificationView";
import { CenterProfileView } from "@/components/center/CenterProfileView";
import { RegisterCenterForm } from "@/components/center/RegisterCenterForm";
import { VerifyCenterEmailForm } from "@/components/center/VerifyCenterEmailForm";
import { clearPendingEmail, setPendingEmail } from "@/lib/pending-email";
import { cn } from "@/lib/utils";
import {
  AUDIT_PAGES,
  installAuditFetchInterceptor,
  MOCK_CENTERS,
  type AuditPageGroup,
  type AuditPageId,
  type AuditRuntimeConfig,
} from "./audit-mock-data";

const GROUP_LABELS: Record<AuditPageGroup, string> = {
  public: "Công khai (Public)",
  portal: "Cổng đối tác (/center)",
  register: "Đăng ký & Xác thực",
};

const ROLE_OPTIONS: { value: UserRole | "guest"; label: string }[] = [
  { value: "guest", label: "Khách" },
  { value: "student", label: "Học sinh" },
  { value: "parent", label: "Phụ huynh" },
  { value: "center", label: "Trung tâm" },
];

function mapHrefToAuditPage(href: string): AuditPageId | null {
  const path = href.split("?")[0].split("#")[0];
  if (path === "/centers") return "public_list";
  if (path === "/centers/compare") return "public_compare";
  if (/^\/centers\/[^/]+$/.test(path)) return "public_detail";
  if (path === "/center") return "portal_overview";
  if (path === "/center/leads") return "portal_leads";
  if (path === "/center/reviews") return "portal_reviews";
  if (path === "/center/verification") return "portal_verification";
  if (path === "/center/profile") return "portal_profile";
  if (path === "/register/center") return "register_form";
  if (path === "/register/center/verify-email") return "register_verify";
  return null;
}

export function AuditCenterWorkbench() {
  const searchParams = useSearchParams();
  const { refreshUser } = useAuth();

  const initialPage = useMemo(() => {
    const raw = searchParams.get("page") as AuditPageId | null;
    return AUDIT_PAGES.find((p) => p.id === raw) ?? AUDIT_PAGES[0];
  }, [searchParams]);

  const initialCase = useMemo(() => {
    const raw = searchParams.get("case");
    return initialPage.cases.find((c) => c.id === raw)?.id ?? initialPage.cases[0].id;
  }, [initialPage, searchParams]);

  const initialRole = useMemo(() => {
    const raw = searchParams.get("role") as (UserRole | "guest") | null;
    if (raw && ROLE_OPTIONS.some((r) => r.value === raw)) return raw;
    return initialPage.defaultRole;
  }, [initialPage, searchParams]);

  const [pageId, setPageId] = useState<AuditPageId>(initialPage.id);
  const [caseId, setCaseId] = useState<string>(initialCase);
  const [role, setRole] = useState<UserRole | "guest">(initialRole);
  const [renderBump, setRenderBump] = useState(0);
  const [interceptorReady, setInterceptorReady] = useState(false);

  const currentPageIndex = AUDIT_PAGES.findIndex((p) => p.id === pageId);
  const currentPage = AUDIT_PAGES[currentPageIndex] ?? AUDIT_PAGES[0];
  const currentCase = currentPage.cases.find((c) => c.id === caseId) ?? currentPage.cases[0];

  const configRef = useRef<AuditRuntimeConfig>({ pageId, caseId, role });
  useLayoutEffect(() => {
    configRef.current = { pageId, caseId, role };
  }, [pageId, caseId, role]);

  // Install fetch interceptor before child effects run so every real view receives mock data cleanly
  useLayoutEffect(() => {
    const cleanup = installAuditFetchInterceptor(() => configRef.current);
    queueMicrotask(() => setInterceptorReady(true));
    void refreshUser();
    return cleanup;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Sync URL query params so reload keeps the exact preview state
  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    url.searchParams.set("page", pageId);
    url.searchParams.set("case", caseId);
    url.searchParams.set("role", role);
    window.history.replaceState(null, "", url.toString());
  }, [pageId, caseId, role]);

  // Sync pending email for /register/center/verify-email cases
  useEffect(() => {
    if (pageId !== "register_verify") return;
    if (caseId === "prefilled") {
      setPendingEmail("tuvan@minhkhang.edu.vn");
    } else {
      clearPendingEmail();
    }
  }, [pageId, caseId]);

  const selectPage = useCallback(
    (nextPageId: AuditPageId) => {
      const target = AUDIT_PAGES.find((p) => p.id === nextPageId);
      if (!target) return;
      const nextCase = target.cases[0].id;
      const nextRole = target.cases[0].defaultRole ?? target.defaultRole;
      configRef.current = { pageId: nextPageId, caseId: nextCase, role: nextRole };
      setPageId(nextPageId);
      setCaseId(nextCase);
      setRole(nextRole);
      setRenderBump((n) => n + 1);
      void refreshUser();
    },
    [refreshUser],
  );

  const selectCase = useCallback(
    (nextCaseId: string) => {
      const targetCase = currentPage.cases.find((c) => c.id === nextCaseId);
      if (!targetCase) return;
      const nextRole = targetCase.defaultRole ?? role;
      configRef.current = { pageId, caseId: nextCaseId, role: nextRole };
      setCaseId(nextCaseId);
      if (targetCase.defaultRole) setRole(targetCase.defaultRole);
      setRenderBump((n) => n + 1);
      void refreshUser();
    },
    [currentPage.cases, pageId, refreshUser, role],
  );

  const selectRole = useCallback(
    (nextRole: UserRole | "guest") => {
      configRef.current = { pageId, caseId, role: nextRole };
      setRole(nextRole);
      setRenderBump((n) => n + 1);
      void refreshUser();
    },
    [caseId, pageId, refreshUser],
  );

  const stepPage = useCallback(
    (delta: number) => {
      const nextIdx = (currentPageIndex + delta + AUDIT_PAGES.length) % AUDIT_PAGES.length;
      selectPage(AUDIT_PAGES[nextIdx].id);
    },
    [currentPageIndex, selectPage],
  );

  // Alt + Left / Right fast-forwards pages; Alt + Up / Down cycles cases
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (!e.altKey) return;
      if (e.key === "ArrowRight") {
        e.preventDefault();
        stepPage(1);
      } else if (e.key === "ArrowLeft") {
        e.preventDefault();
        stepPage(-1);
      } else if (e.key === "ArrowDown") {
        e.preventDefault();
        const idx = currentPage.cases.findIndex((c) => c.id === caseId);
        const next = currentPage.cases[(idx + 1) % currentPage.cases.length];
        if (next) selectCase(next.id);
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        const idx = currentPage.cases.findIndex((c) => c.id === caseId);
        const prev = currentPage.cases[(idx - 1 + currentPage.cases.length) % currentPage.cases.length];
        if (prev) selectCase(prev.id);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [caseId, currentPage.cases, selectCase, stepPage]);

  // Keep internal navigation inside /audit/center when user clicks links inside previewed pages
  const handlePreviewClickCapture = (e: React.MouseEvent<HTMLDivElement>) => {
    const anchor = (e.target as HTMLElement | null)?.closest("a[href]") as HTMLAnchorElement | null;
    if (!anchor) return;
    const href = anchor.getAttribute("href");
    if (!href || href.startsWith("http") || href.startsWith("mailto:") || href.startsWith("#")) return;
    const mapped = mapHrefToAuditPage(href);
    if (mapped) {
      e.preventDefault();
      e.stopPropagation();
      selectPage(mapped);
    }
  };

  const renderPreviewBody = () => {
    if (!interceptorReady) {
      return (
        <div className="flex min-h-[60vh] items-center justify-center text-sm text-ink-3">
          Đang khởi tạo môi trường xem trước /audit/center...
        </div>
      );
    }

    const viewKey = `${pageId}:${caseId}:${role}:${renderBump}`;

    switch (pageId) {
      case "public_list": {
        const list = caseId === "empty" ? [] : MOCK_CENTERS;
        return (
          <div className="mx-auto w-full max-w-[1560px] px-4 py-8 sm:px-8 lg:px-12">
            <section className="space-y-6 pb-12">
              <PublicCenterListView key={viewKey} initialCenters={list} />
            </section>
          </div>
        );
      }
      case "public_detail": {
        const center =
          caseId === "unverified_minimal" ? MOCK_CENTERS[3] : MOCK_CENTERS[0];
        return (
          // Trang thật /centers/[id] không có lề bao ngoài: ảnh bìa tràn mép
          <StudyCenterDetailView key={viewKey} center={center} />
        );
      }
      case "public_compare": {
        const selected =
          caseId === "compare_3"
            ? [MOCK_CENTERS[0], MOCK_CENTERS[1], MOCK_CENTERS[2]]
            : caseId === "compare_2"
              ? [MOCK_CENTERS[0], MOCK_CENTERS[3]]
              : [MOCK_CENTERS[0]];
        return <CenterCompareView key={viewKey} selected={selected} all={MOCK_CENTERS} />;
      }
      case "portal_overview":
      case "portal_leads":
      case "portal_reviews":
      case "portal_verification":
      case "portal_profile": {
        const isReviewsPage = pageId === "portal_reviews";
        return (
          <CenterNavPathProvider value={currentPage.route}>
            <div className="min-h-screen bg-surface-2 flex flex-col">
              <CenterStatusBanner key={`banner:${viewKey}`} />
              {!isReviewsPage && <CenterNav />}
              {isReviewsPage ? (
                <CenterReviewsPortalView key={viewKey} />
              ) : (
                <main className="flex-1 container mx-auto px-4 sm:px-6 py-6">
                  {pageId === "portal_overview" && <CenterDashboardView key={viewKey} />}
                  {pageId === "portal_leads" && <CenterLeadsListView key={viewKey} />}
                  {pageId === "portal_verification" && <CenterVerificationView key={viewKey} />}
                  {pageId === "portal_profile" && <CenterProfileView key={viewKey} />}
                </main>
              )}
            </div>
          </CenterNavPathProvider>
        );
      }
      case "register_form":
        return (
          <AuthSplitLayout key={viewKey}>
            <RegisterCenterForm />
          </AuthSplitLayout>
        );
      case "register_verify":
        return (
          <AuthSplitLayout key={viewKey}>
            <VerifyCenterEmailForm />
          </AuthSplitLayout>
        );
    }
  };

  const groups: AuditPageGroup[] = ["public", "portal", "register"];

  return (
    <div className="relative min-h-screen w-full" onClickCapture={handlePreviewClickCapture}>
      {/* Authentic header for Public & Portal pages; Register pages use full-screen AuthSplitLayout */}
      {currentPage.group !== "register" && <SiteHeader />}

      {renderPreviewBody()}

      {/* Floating Debug Dock */}
      <FloatingAuditDock
        title="Center UI Audit"
        route={currentPage.route}
        caseLabel={currentCase.label}
        pageCounter={`${currentPageIndex + 1}/${AUDIT_PAGES.length}`}
        ariaLabel="Center UI Audit Debug Switcher"
        onPrevPage={() => stepPage(-1)}
        onNextPage={() => stepPage(1)}
        onReset={() => setRenderBump((n) => n + 1)}
      >
        {/* Fast-Forward Pages */}
        <div className="mt-2.5 space-y-2">
          {groups.map((group) => {
            const items = AUDIT_PAGES.filter((p) => p.group === group);
            return (
              <div key={group} className="space-y-1">
                <div className="text-[10px] font-semibold uppercase tracking-wider text-ink-3">
                  {GROUP_LABELS[group]}
                </div>
                <div className="flex flex-wrap gap-1">
                  {items.map((page) => {
                    const active = page.id === pageId;
                    return (
                      <button
                        key={page.id}
                        type="button"
                        onClick={() => selectPage(page.id)}
                        className={cn(
                          "rounded-md border px-2 py-1 text-[11px] font-medium transition-colors",
                          active
                            ? "border-brand bg-brand text-white"
                            : "border-line bg-surface-2/60 text-ink-2 hover:bg-surface-2 hover:text-ink",
                        )}
                      >
                        {page.shortLabel}
                      </button>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>

        {/* Cases / States */}
        <div className="mt-3 border-t border-line pt-2.5 space-y-1.5">
          <div className="flex items-center justify-between">
            <span className="text-[10px] font-semibold uppercase tracking-wider text-ink-3">
              Cases ({currentPage.cases.length})
            </span>
            <span className="text-[10px] text-ink-3">Alt+↑/↓ chuyển case</span>
          </div>
          <div className="flex flex-wrap gap-1">
            {currentPage.cases.map((c) => {
              const active = c.id === caseId;
              return (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => selectCase(c.id)}
                  className={cn(
                    "rounded-md border px-2 py-1 text-[11px] transition-colors text-left",
                    active
                      ? "border-amber-500/60 bg-amber-500/15 font-semibold text-ink"
                      : "border-line bg-surface text-ink-2 hover:bg-surface-2 hover:text-ink",
                  )}
                >
                  {c.label}
                </button>
              );
            })}
          </div>
          <p className="rounded-lg bg-surface-2 px-2.5 py-1.5 text-[11px] text-ink-2">
            {currentCase.description}
          </p>
        </div>

        {/* Role Override & Real Route Link */}
        <div className="mt-2.5 flex items-center justify-between gap-2 border-t border-line pt-2.5">
          <div className="flex items-center gap-1 flex-wrap">
            <span className="text-[10px] font-semibold uppercase tracking-wider text-ink-3 mr-0.5">
              Role:
            </span>
            {ROLE_OPTIONS.map((r) => (
              <button
                key={r.value}
                type="button"
                onClick={() => selectRole(r.value)}
                className={cn(
                  "rounded px-1.5 py-0.5 text-[10px] font-medium transition-colors",
                  role === r.value
                    ? "bg-ink text-white"
                    : "bg-surface-2 text-ink-2 hover:text-ink",
                )}
              >
                {r.label}
              </button>
            ))}
          </div>
          <a
            href={currentPage.route.replace("[id]", "ctr-1")}
            target="_blank"
            rel="noreferrer"
            className="inline-flex shrink-0 items-center gap-1 text-[10px] text-brand hover:underline"
            title="Mở route thật trong tab mới"
          >
            Route thật
            <ExternalLinkIcon className="h-3 w-3" />
          </a>
        </div>
      </FloatingAuditDock>
    </div>
  );
}
