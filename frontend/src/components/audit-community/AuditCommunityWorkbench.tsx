"use client";

import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import {
  ExternalLinkIcon,
  MessageSquarePlusIcon,
  ShieldAlertIcon,
} from "lucide-react";
import { FloatingAuditDock } from "@/components/audit-shared/FloatingAuditDock";
import { useAuth } from "@/contexts/AuthContext";
import { useHeaderHidden } from "@/lib/header-visibility";
import type { UserRole } from "@/types/api";
import type { CommunityMessageDto } from "@/types/community";
import { emitRealtimeMock, setRealtimeMockHandler } from "@/lib/realtime";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { CommunityChatView } from "@/components/community/CommunityChatView";
import { AdminForumView } from "@/components/admin/forum/AdminForumView";
import { cn } from "@/lib/utils";
import {
  AUDIT_COMMUNITY_PAGES,
  installCommunityAuditFetchInterceptor,
  membersForCase,
  type CommunityAuditPageId,
  type CommunityAuditRuntimeConfig,
} from "./audit-community-mock-data";

const ROLE_OPTIONS: { value: UserRole; label: string }[] = [
  { value: "student", label: "Học sinh" },
  { value: "parent", label: "Phụ huynh" },
  { value: "center", label: "Trung tâm" },
  { value: "moderator", label: "Kiểm duyệt" },
  { value: "admin", label: "Quản trị" },
];

export function AuditCommunityWorkbench() {
  const searchParams = useSearchParams();
  const { refreshUser, user } = useAuth();
  const headerHidden = useHeaderHidden();

  const initialPage = useMemo(() => {
    const raw = searchParams.get("page") as CommunityAuditPageId | null;
    return AUDIT_COMMUNITY_PAGES.find((p) => p.id === raw) ?? AUDIT_COMMUNITY_PAGES[0];
  }, [searchParams]);

  const initialCase = useMemo(() => {
    const raw = searchParams.get("case");
    return initialPage.cases.find((c) => c.id === raw)?.id ?? initialPage.cases[0].id;
  }, [initialPage, searchParams]);

  const initialRole = useMemo(() => {
    const raw = searchParams.get("role") as UserRole | null;
    if (raw && ROLE_OPTIONS.some((r) => r.value === raw)) return raw;
    return initialPage.defaultRole;
  }, [initialPage, searchParams]);

  const [pageId, setPageId] = useState<CommunityAuditPageId>(initialPage.id);
  const [caseId, setCaseId] = useState<string>(initialCase);
  const [role, setRole] = useState<UserRole>(initialRole);
  const [renderBump, setRenderBump] = useState(0);
  const [interceptorReady, setInterceptorReady] = useState(false);

  const currentPageIndex = AUDIT_COMMUNITY_PAGES.findIndex((p) => p.id === pageId);
  const currentPage = AUDIT_COMMUNITY_PAGES[currentPageIndex] ?? AUDIT_COMMUNITY_PAGES[0];
  const currentCase = currentPage.cases.find((c) => c.id === caseId) ?? currentPage.cases[0];

  const configRef = useRef<CommunityAuditRuntimeConfig>({ pageId, caseId, role });
  useLayoutEffect(() => {
    configRef.current = { pageId, caseId, role };
  }, [pageId, caseId, role]);

  useLayoutEffect(() => {
    const cleanupFetch = installCommunityAuditFetchInterceptor(() => configRef.current);
    setRealtimeMockHandler(async (method) => {
      if (method === "JoinCommunity") {
        const cfg = configRef.current;
        return { members: membersForCase(cfg.pageId, cfg.caseId, cfg.role) };
      }
      return undefined;
    });
    queueMicrotask(() => setInterceptorReady(true));
    void refreshUser();
    return () => {
      cleanupFetch();
      setRealtimeMockHandler(null);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ponytail: the real /me can land after the mocked one; re-sync until the mocked role sticks
  useEffect(() => {
    if (interceptorReady && user && user.role !== role) void refreshUser();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [interceptorReady, user?.role, role]);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const url = new URL(window.location.href);
    url.searchParams.set("page", pageId);
    url.searchParams.set("case", caseId);
    url.searchParams.set("role", role);
    window.history.replaceState(null, "", url.toString());
  }, [pageId, caseId, role]);

  const selectPage = useCallback(
    (nextPageId: CommunityAuditPageId) => {
      const target = AUDIT_COMMUNITY_PAGES.find((p) => p.id === nextPageId);
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
      const nextRole = targetCase.defaultRole ?? (pageId === "community_chat" && role === "moderator" ? "student" : role);
      configRef.current = { pageId, caseId: nextCaseId, role: nextRole };
      setCaseId(nextCaseId);
      setRole(nextRole);
      setRenderBump((n) => n + 1);
      void refreshUser();
    },
    [currentPage.cases, pageId, refreshUser, role],
  );

  const selectRole = useCallback(
    (nextRole: UserRole) => {
      configRef.current = { pageId, caseId, role: nextRole };
      setRole(nextRole);
      setRenderBump((n) => n + 1);
      void refreshUser();
    },
    [caseId, pageId, refreshUser],
  );

  const stepPage = useCallback(
    (delta: number) => {
      const nextIdx = (currentPageIndex + delta + AUDIT_COMMUNITY_PAGES.length) % AUDIT_COMMUNITY_PAGES.length;
      selectPage(AUDIT_COMMUNITY_PAGES[nextIdx].id);
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

  const simulateIncomingMessage = () => {
    const msg: CommunityMessageDto = {
      id: `cmsg-sim-${Date.now()}`,
      userId: "usr-student-2",
      authorName: "Trần Minh Anh",
      authorRole: "student",
      content: "Mình vừa nhận email phỏng vấn alumni rồi mọi người ơi, hồi hộp quá!",
      createdAt: new Date().toISOString(),
      isMine: false,
    };
    emitRealtimeMock("newCommunityMessage", msg);
  };

  const simulateRestrictionToggle = () => {
    const nextRestricted = caseId !== "restricted";
    if (nextRestricted) {
      selectCase("restricted");
    } else {
      selectCase("populated_active");
    }
  };

  const renderPreviewBody = () => {
    // ponytail: wait for the mocked role, else ProtectedRoute redirects on the real user's role
    if (!interceptorReady || user?.role !== role) {
      return (
        <div className="flex min-h-[60vh] items-center justify-center text-sm text-ink-3">
          Đang khởi tạo môi trường xem trước /audit/community...
        </div>
      );
    }

    const viewKey = `${pageId}:${caseId}:${role}:${renderBump}`;

    if (pageId === "admin_forum") {
      return (
        <div className="mx-auto w-full max-w-5xl px-4 py-8">
          <AdminForumView key={viewKey} />
        </div>
      );
    }

    // ponytail: /community is a full-bleed route now, preview it the same way
    return <CommunityChatView key={viewKey} />;
  };

  return (
    <div className="relative flex min-h-screen w-full flex-col">
      {pageId !== "admin_forum" && !headerHidden && <SiteHeader />}

      <main className="w-full flex-1">{renderPreviewBody()}</main>

      {/* Floating Debug Dock */}
      <FloatingAuditDock
        title="Community UI Audit"
        route={currentPage.route}
        caseLabel={currentCase.label}
        pageCounter={`${currentPageIndex + 1}/${AUDIT_COMMUNITY_PAGES.length}`}
        ariaLabel="Community UI Audit Debug Switcher"
        onPrevPage={() => stepPage(-1)}
        onNextPage={() => stepPage(1)}
        onReset={() => setRenderBump((n) => n + 1)}
      >
        {/* Pages */}
        <div className="mt-2.5 space-y-1">
          <div className="text-[10px] font-semibold uppercase tracking-wider text-ink-3">
            Trang cộng đồng
          </div>
          <div className="flex flex-wrap gap-1">
            {AUDIT_COMMUNITY_PAGES.map((page) => {
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

        {/* Cases */}
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

        {/* Live Realtime Triggers for /community */}
        {pageId === "community_chat" && (
          <div className="mt-2.5 flex flex-wrap gap-1.5 border-t border-line pt-2.5">
            <button
              type="button"
              onClick={simulateIncomingMessage}
              className="inline-flex items-center gap-1 rounded-md border border-line bg-surface-2 px-2 py-1 text-[11px] font-medium text-ink hover:bg-surface-3"
            >
              <MessageSquarePlusIcon className="h-3 w-3 text-brand" />
              Giả lập tin mới đến
            </button>
            <button
              type="button"
              onClick={simulateRestrictionToggle}
              className="inline-flex items-center gap-1 rounded-md border border-line bg-surface-2 px-2 py-1 text-[11px] font-medium text-ink hover:bg-surface-3"
            >
              <ShieldAlertIcon className="h-3 w-3 text-amber-600" />
              {caseId === "restricted" ? "Bỏ hạn chế chat" : "Bật hạn chế chat"}
            </button>
          </div>
        )}

        {/* Role & Audit Links */}
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
              /center
            </Link>
            <Link href="/audit/universities" className="text-[10px] text-ink-2 hover:text-ink hover:underline">
              /universities
            </Link>
            <a
              href={currentPage.route}
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
