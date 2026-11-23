"use client";

import { useCallback, useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronDownIcon, ShieldCheckIcon, XIcon } from "lucide-react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { PolicyContent } from "@/components/policy/PolicyContent";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";
import { acceptPolicies, getPolicyAcceptanceStatus } from "@/lib/policy-acceptance-api";
import { formatPolicyDate, getCurrentPolicy } from "@/lib/policy-api";
import { POLICY_REQUIRED_EVENT } from "@/lib/policy-events";
import { useFetched } from "@/lib/use-fetched";
import type { AcceptedPolicy, PolicyAcceptanceItem, PolicyAcceptanceStatus } from "@/types/policy";

/** Chỉ ba loại tài khoản này phải đồng ý (khớp backend); admin và moderator là người nội bộ. */
const SUBJECT_ROLES = ["student", "parent", "center"];
/** Hai trang này luôn đọc được, kể cả khi đang bị chặn. */
const READABLE_PATHS = ["/terms", "/privacy"];

const KIND_LABELS: Record<string, string> = {
  terms_of_use: "Điều khoản sử dụng",
  privacy_policy: "Chính sách bảo mật",
};
const kindLabel = (item: PolicyAcceptanceItem) => KIND_LABELS[item.kind] ?? item.title ?? item.kind;

/**
 * [UC-104] Người dùng chưa đồng ý bản Điều khoản / Chính sách bảo mật bắt buộc thì thấy màn đồng ý thay cho cả ứng dụng.
 * Server chặn thật (403 policy_acceptance_required); màn này là chỗ để người dùng đọc và đồng ý, hoặc đăng xuất.
 * Bản mới KHÔNG bắt buộc chỉ hiện một dòng thông báo, tắt được.
 */
export function PolicyGate({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const pathname = usePathname();
  const subject = user !== null && SUBJECT_ROLES.includes(user.role);

  const [attempt, setAttempt] = useState(0);
  const key = subject ? `${user.id}:${attempt}` : "none";
  const { data: status, loading } = useFetched<PolicyAcceptanceStatus | null>(key, () =>
    subject ? getPolicyAcceptanceStatus() : Promise.resolve(null),
  );
  const refresh = useCallback(() => setAttempt((n) => n + 1), []);

  // Server báo 403 policy_acceptance_required ở bất kỳ lời gọi nào (vd. admin vừa công bố bản bắt buộc): kiểm tra lại
  useEffect(() => {
    window.addEventListener(POLICY_REQUIRED_EVENT, refresh);
    return () => window.removeEventListener(POLICY_REQUIRED_EVENT, refresh);
  }, [refresh]);

  const readable = READABLE_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  if (!subject || loading || !status || readable) return <>{children}</>;

  if (status.mustAccept) return <PolicyAcceptanceScreen status={status} onChanged={refresh} />;

  return (
    <>
      {status.hasUpdates && <PolicyUpdateNotice status={status} />}
      {children}
    </>
  );
}

// ---------------------------------------------------------------- màn bắt buộc đồng ý

function PolicyAcceptanceScreen({ status, onChanged }: { status: PolicyAcceptanceStatus; onChanged: () => void }) {
  const { logout } = useAuth();
  const blocking = status.items.filter((i) => i.blocks);
  const firstTime = blocking.some((i) => i.state === "never_accepted");

  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const accept = async () => {
    setError(null);
    setSubmitting(true);
    try {
      // Gửi ĐÚNG phiên bản đang hiển thị; admin vừa công bố bản khác thì server từ chối và không ghi gì
      const items: AcceptedPolicy[] = blocking.map((i) => ({ kind: i.kind as AcceptedPolicy["kind"], version: i.currentVersion ?? "" }));
      await acceptPolicies(items);
      onChanged();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Có lỗi kết nối máy chủ. Vui lòng thử lại.");
      if (err instanceof ApiError && err.status === 409) {
        setAgreed(false);   // nội dung đã đổi: phải đọc lại rồi đồng ý lại
        onChanged();
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div role="dialog" aria-modal="true" aria-labelledby="policy-gate-title" className="min-h-screen bg-surface px-4 py-10 sm:px-6">
      <div className="mx-auto max-w-2xl space-y-6">
        <div className="space-y-2">
          <div className="inline-flex size-11 items-center justify-center rounded-xl bg-brand/10 text-brand">
            <ShieldCheckIcon className="size-5" aria-hidden />
          </div>
          <h1 id="policy-gate-title" className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
            {firstTime ? "Đồng ý Điều khoản và Chính sách" : "Điều khoản đã được cập nhật"}
          </h1>
          <p className="text-sm text-ink-2">
            {firstTime
              ? "Để tiếp tục sử dụng USDSS, bạn cần đọc và đồng ý với các nội dung dưới đây."
              : "Chúng tôi vừa cập nhật nội dung dưới đây. Bạn cần đọc và đồng ý để tiếp tục sử dụng USDSS."}
          </p>
        </div>

        <div className="space-y-4">
          {blocking.map((item) => (
            <PolicyItemCard key={item.kind} item={item} />
          ))}
        </div>

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <div className="flex items-start gap-2">
          <Checkbox id="policy-gate-agree" checked={agreed} onCheckedChange={(c) => setAgreed(Boolean(c))} />
          <Label htmlFor="policy-gate-agree" className="leading-snug text-ink">
            Tôi đã đọc và đồng ý với {blocking.map(kindLabel).join(" và ")} của USDSS.
          </Label>
        </div>

        <div className="flex flex-col gap-2 sm:flex-row">
          <Button type="button" size="xl" className="flex-1" disabled={!agreed || submitting} loading={submitting} onClick={accept}>
            {submitting ? "Đang ghi nhận..." : "Đồng ý và tiếp tục"}
          </Button>
          <Button type="button" size="xl" variant="outline" className="flex-1" disabled={submitting} onClick={() => logout()}>
            Đăng xuất
          </Button>
        </div>
        <p className="text-body-s text-ink-3">
          Nếu không đồng ý, bạn sẽ không dùng được các chức năng của hệ thống. Bạn có thể đăng xuất bất cứ lúc nào.
        </p>
      </div>
    </div>
  );
}

function PolicyItemCard({ item }: { item: PolicyAcceptanceItem }) {
  const [open, setOpen] = useState(false);
  return (
    <section className="space-y-3 rounded-xl border border-line bg-background p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="font-semibold text-ink">{kindLabel(item)}</h2>
        {item.currentVersion && <span className="text-body-s text-ink-3">Phiên bản {item.currentVersion}</span>}
      </div>
      {item.state === "update_required" && item.changeSummary && (
        <p className="text-sm text-ink-2">
          <span className="font-medium text-ink">Có gì mới: </span>
          {item.changeSummary}
        </p>
      )}
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="inline-flex items-center gap-1 text-sm font-medium text-brand hover:underline"
      >
        {open ? "Ẩn toàn văn" : "Đọc toàn văn"}
        <ChevronDownIcon className={`size-4 transition-transform ${open ? "rotate-180" : ""}`} aria-hidden />
      </button>
      {open && <PolicyReader kind={item.kind} />}
    </section>
  );
}

function PolicyReader({ kind }: { kind: PolicyAcceptanceItem["kind"] }) {
  const { data, error, loading } = useFetched(kind, () => getCurrentPolicy(kind));
  if (loading) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải nội dung...
      </div>
    );
  }
  if (error || !data) {
    return (
      <Alert variant="destructive">
        <AlertDescription>Không tải được toàn văn. Bạn có thể thử lại sau hoặc mở trang riêng của văn bản này.</AlertDescription>
      </Alert>
    );
  }
  return (
    <div className="max-h-80 overflow-y-auto rounded-lg border border-line bg-surface p-4">
      <p className="mb-3 text-body-s text-ink-3">
        Phiên bản {data.version} · công bố {formatPolicyDate(data.publishedAt)}
      </p>
      <PolicyContent content={data.content} />
    </div>
  );
}

// ---------------------------------------------------------------- thông báo bản mới không bắt buộc

const NOTICE_KEY = "usas.policyNoticeDismissed";
const NOTICE_EVENT = "usas:policy-notice";

function readDismissed(): string | null {
  try {
    return localStorage.getItem(NOTICE_KEY);
  } catch {
    return null;   // trình duyệt chặn lưu trữ: coi như chưa tắt
  }
}

function subscribeNotice(onChange: () => void) {
  window.addEventListener(NOTICE_EVENT, onChange);
  window.addEventListener("storage", onChange);
  return () => {
    window.removeEventListener(NOTICE_EVENT, onChange);
    window.removeEventListener("storage", onChange);
  };
}

function PolicyUpdateNotice({ status }: { status: PolicyAcceptanceStatus }) {
  // Mỗi tổ hợp phiên bản chỉ hỏi một lần: có bản mới nữa thì thông báo lại
  const versions = status.items.map((i) => `${i.kind}:${i.currentVersion}`).join("|");
  const dismissed = useSyncExternalStore(subscribeNotice, readDismissed, () => versions);   // máy chủ: coi như đã tắt, tránh nhảy giao diện
  if (dismissed === versions) return null;

  const dismiss = () => {
    try {
      localStorage.setItem(NOTICE_KEY, versions);
    } catch {
      // Không lưu được: thông báo sẽ hiện lại ở lần tải sau, không sao
    }
    window.dispatchEvent(new Event(NOTICE_EVENT));
  };

  return (
    <div role="status" className="flex items-start gap-3 border-b border-line bg-brand/5 px-4 py-2.5 text-sm text-ink-2">
      <p className="flex-1">
        Điều khoản hoặc Chính sách bảo mật vừa được cập nhật (không cần đồng ý lại). Xem{" "}
        <Link href="/terms" className="font-medium text-brand hover:underline">Điều khoản sử dụng</Link> và{" "}
        <Link href="/privacy" className="font-medium text-brand hover:underline">Chính sách bảo mật</Link>.
      </p>
      <button type="button" onClick={dismiss} aria-label="Tắt thông báo" className="text-ink-3 hover:text-ink">
        <XIcon className="size-4" aria-hidden />
      </button>
    </div>
  );
}
