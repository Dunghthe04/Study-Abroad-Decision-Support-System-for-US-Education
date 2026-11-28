"use client";

import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ExternalLinkIcon } from "lucide-react";
import { FloatingAuditDock } from "@/components/audit-shared/FloatingAuditDock";
import { useAuth } from "@/contexts/AuthContext";
import type { UserRole } from "@/types/api";
import type { UniversitySearchParams } from "@/types/university";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { UniversityListView } from "@/components/universities/UniversityListView";
import { UniversityDetailView } from "@/components/universities/UniversityDetailView";
import { CostLookup } from "@/components/costs/CostLookup";
import { cn } from "@/lib/utils";
import {
  AUDIT_UNI_PAGES,
  filterMockUniversities,
  installUniAuditFetchInterceptor,
  MOCK_MIT_DETAIL,
  MOCK_MIT_SCHOLARSHIPS,
  MOCK_SPARSE_DETAIL,
  MOCK_STATE_LIVING_COSTS,
  MOCK_UNI_FILTER_OPTIONS,
  type UniAuditPageId,
  type UniAuditRuntimeConfig,
} from "./audit-uni-mock-data";

const ROLE_OPTIONS: { value: UserRole | "guest"; label: string }[] = [
  { value: "guest", label: "Khách" },
  { value: "student", label: "Học sinh" },
  { value: "parent", label: "Phụ huynh" },
];

function initialListParamsForCase(caseId: string): UniversitySearchParams {
  if (caseId === "filtered_narrow") {
    return { isIvy: true, maxTuitionUsd: 65000, sortBy: "rank", rankListId: "USN27-NU", page: 1, pageSize: 12, studyLevel: "undergraduate" };
  }
  if (caseId === "empty") {
    return { search: "Hogwarts", sortBy: "name", page: 1, pageSize: 12, studyLevel: "undergraduate" };
  }
  return { sortBy: "name", page: 1, pageSize: 12, studyLevel: "undergraduate" };
}

function initialCodesForCase(caseId: string): string[] {
  if (caseId === "school_single") return ["MIT"];
  if (caseId === "school_compare_3") return ["MIT", "PURDUE", "BERKELEY"];
  if (caseId === "school_incomplete") return ["SPARSE_CC", "MIT"];
  if (caseId === "school_too_many") return ["MIT", "HARVARD", "STANFORD", "PURDUE", "BERKELEY"];
  return [];
}

export function AuditUniversitiesWorkbench() {
  const searchParams = useSearchParams();
  const { refreshUser } = useAuth();

  const initialPage = useMemo(() => {
    const raw = searchParams.get("page") as UniAuditPageId | null;
    return AUDIT_UNI_PAGES.find((p) => p.id === raw) ?? AUDIT_UNI_PAGES[0];
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

  const [pageId, setPageId] = useState<UniAuditPageId>(initialPage.id);
  const [caseId, setCaseId] = useState<string>(initialCase);
  const [role, setRole] = useState<UserRole | "guest">(initialRole);
  const [listParams, setListParams] = useState<UniversitySearchParams>(() => initialListParamsForCase(initialCase));
  const [detailCode, setDetailCode] = useState<string>("MIT");
  const [renderBump, setRenderBump] = useState(0);
  const [interceptorReady, setInterceptorReady] = useState(false);

  const currentPageIndex = AUDIT_UNI_PAGES.findIndex((p) => p.id === pageId);
  const currentPage = AUDIT_UNI_PAGES[currentPageIndex] ?? AUDIT_UNI_PAGES[0];
  const currentCase = currentPage.cases.find((c) => c.id === caseId) ?? currentPage.cases[0];

  const configRef = useRef<UniAuditRuntimeConfig>({ pageId, caseId, role });
  useLayoutEffect(() => {
    configRef.current = { pageId, caseId, role };
  }, [pageId, caseId, role]);

  useLayoutEffect(() => {
    const cleanup = installUniAuditFetchInterceptor(() => configRef.current);
    queueMicrotask(() => setInterceptorReady(true));
    void refreshUser();
    return cleanup;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    url.searchParams.set("page", pageId);
    url.searchParams.set("case", caseId);
    url.searchParams.set("role", role);
    window.history.replaceState(null, "", url.toString());
  }, [pageId, caseId, role]);

  const selectPage = useCallback(
    (nextPageId: UniAuditPageId, explicitCaseId?: string) => {
      const target = AUDIT_UNI_PAGES.find((p) => p.id === nextPageId);
      if (!target) return;
      const nextCase = explicitCaseId && target.cases.some((c) => c.id === explicitCaseId)
        ? explicitCaseId
        : target.cases[0].id;
      const caseDef = target.cases.find((c) => c.id === nextCase) ?? target.cases[0];
      const nextRole = caseDef.defaultRole ?? target.defaultRole;
      configRef.current = { pageId: nextPageId, caseId: nextCase, role: nextRole };
      setPageId(nextPageId);
      setCaseId(nextCase);
      setRole(nextRole);
      if (nextPageId === "uni_list") setListParams(initialListParamsForCase(nextCase));
      if (nextPageId === "uni_detail") setDetailCode(nextCase === "sparse_missing" ? "SPARSE_CC" : "MIT");
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
      if (pageId === "uni_list") setListParams(initialListParamsForCase(nextCaseId));
      if (pageId === "uni_detail") setDetailCode(nextCaseId === "sparse_missing" ? "SPARSE_CC" : "MIT");
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
      const nextIdx = (currentPageIndex + delta + AUDIT_UNI_PAGES.length) % AUDIT_UNI_PAGES.length;
      selectPage(AUDIT_UNI_PAGES[nextIdx].id);
    },
    [currentPageIndex, selectPage],
  );

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

  // Intercept internal links inside preview so clicking a university card or /costs link stays inside /audit/universities
  const handlePreviewClickCapture = (e: React.MouseEvent<HTMLDivElement>) => {
    const anchor = (e.target as HTMLElement | null)?.closest("a[href]") as HTMLAnchorElement | null;
    if (!anchor) return;
    const href = anchor.getAttribute("href");
    if (!href || href.startsWith("http") || href.startsWith("mailto:") || href.startsWith("#") || href.startsWith("/audit")) return;

    const parsed = new URL(href, window.location.origin);
    if (parsed.pathname === "/universities") {
      e.preventDefault();
      e.stopPropagation();
      selectPage("uni_list");
      return;
    }
    const uniMatch = parsed.pathname.match(/^\/university\/([^/]+)$/);
    if (uniMatch) {
      e.preventDefault();
      e.stopPropagation();
      const code = decodeURIComponent(uniMatch[1]);
      selectPage("uni_detail", code === "SPARSE_CC" ? "sparse_missing" : "full_ivy");
      setDetailCode(code);
      return;
    }
    if (parsed.pathname === "/costs" || parsed.pathname === "/universities/compare" || parsed.pathname === "/universities/living-costs") {
      e.preventDefault();
      e.stopPropagation();
      const isState = parsed.searchParams.get("mode") === "state" || parsed.pathname === "/universities/living-costs";
      selectPage("uni_costs", isState ? "state_living_costs" : "school_single");
    }
  };

  const renderPreviewBody = () => {
    if (!interceptorReady) {
      return (
        <div className="flex min-h-[60vh] items-center justify-center text-sm text-ink-3">
          Đang khởi tạo môi trường xem trước /audit/universities...
        </div>
      );
    }

    const viewKey = `${pageId}:${caseId}:${role}:${renderBump}`;

    switch (pageId) {
      case "uni_list": {
        const data = filterMockUniversities(listParams);
        return (
          <section className="mx-auto w-full max-w-[1560px] px-4 pt-4 pb-8 sm:px-8 lg:flex lg:min-h-0 lg:flex-1 lg:flex-col lg:overflow-hidden lg:px-12 lg:pb-0">
            <UniversityListView
              key={`${viewKey}:${JSON.stringify(listParams)}`}
              initialData={data}
              options={MOCK_UNI_FILTER_OPTIONS}
              currentParams={listParams}
              onParamsChange={(next) => setListParams(next)}
            />
          </section>
        );
      }
      case "uni_detail": {
        const isSparse = caseId === "sparse_missing" || detailCode === "SPARSE_CC";
        const university = isSparse ? MOCK_SPARSE_DETAIL : MOCK_MIT_DETAIL;
        const scholarships = isSparse ? [] : MOCK_MIT_SCHOLARSHIPS;
        return (
          <UniversityDetailView
            key={viewKey}
            university={university}
            scholarships={scholarships}
            today="2026-11-28"
          />
        );
      }
      case "uni_costs": {
        const isState = caseId === "state_living_costs" || caseId === "state_error";
        const states = caseId === "state_error" ? null : MOCK_STATE_LIVING_COSTS;
        return (
          <CostLookup
            key={viewKey}
            mode={isState ? "state" : "school"}
            initialCodes={initialCodesForCase(caseId)}
            states={states}
            onCodesChange={() => undefined}
          />
        );
      }
    }
  };

  const lockedViewport = pageId === "uni_list";

  return (
    <div
      className={cn(
        "relative flex min-h-screen w-full flex-col",
        lockedViewport && "lg:h-dvh lg:max-h-dvh lg:overflow-hidden",
      )}
      onClickCapture={handlePreviewClickCapture}
    >
      <SiteHeader />

      <main className={cn("w-full flex-1", lockedViewport && "lg:flex lg:min-h-0 lg:flex-col lg:overflow-hidden")}>
        {renderPreviewBody()}
      </main>

      {/* Floating Debug Dock */}
      <FloatingAuditDock
        title="Universities UI Audit"
        route={currentPage.route}
        caseLabel={currentCase.label}
        pageCounter={`${currentPageIndex + 1}/${AUDIT_UNI_PAGES.length}`}
        ariaLabel="Universities UI Audit Debug Switcher"
        onPrevPage={() => stepPage(-1)}
        onNextPage={() => stepPage(1)}
        onReset={() => setRenderBump((n) => n + 1)}
      >
        {/* Fast-Forward Pages */}
        <div className="mt-2.5 space-y-1">
          <div className="text-[10px] font-semibold uppercase tracking-wider text-ink-3">
            Trang đại học
          </div>
          <div className="flex flex-wrap gap-1">
            {AUDIT_UNI_PAGES.map((page) => {
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

        {/* Role Override & Cross-link to /audit/center */}
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
          <div className="flex items-center gap-2 shrink-0">
            <Link href="/audit/center" className="text-[10px] text-ink-2 hover:text-ink hover:underline">
              /audit/center
            </Link>
            <a
              href={currentPage.route.replace("[code]", "MIT")}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-[10px] text-brand hover:underline"
              title="Mở route thật trong tab mới"
            >
              Route thật
              <ExternalLinkIcon className="h-3 w-3" />
            </a>
          </div>
        </div>
      </FloatingAuditDock>
    </div>
  );
}
