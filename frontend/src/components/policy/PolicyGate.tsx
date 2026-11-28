"use client";

import { useCallback, useEffect, useRef, useState, useSyncExternalStore, type ReactNode } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ShieldCheckIcon, XIcon } from "lucide-react";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  Dialog,
  DialogAction,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import { acceptPolicies, getPolicyAcceptanceStatus } from "@/lib/policy-acceptance-api";
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
const KIND_HREFS: Record<string, string> = {
  terms_of_use: "/terms",
  privacy_policy: "/privacy",
};
const kindLabel = (item: PolicyAcceptanceItem) => KIND_LABELS[item.kind] ?? item.title ?? item.kind;
const kindHref = (item: PolicyAcceptanceItem) => KIND_HREFS[item.kind] ?? "/terms";

/**
 * [UC-104] Người dùng chưa đồng ý bản Điều khoản / Chính sách bảo mật bắt buộc thì thấy một hộp nhỏ nổi trên ứng dụng
 * (trang phía sau làm mờ). Tên văn bản là link mở tab mới; chỉ một nút Đồng ý, dòng chữ ngay dưới nút nói rõ đồng ý với gì.
 * Server chặn thật (403 policy_acceptance_required); hộp này là chỗ để người dùng đồng ý, hoặc đăng xuất.
 * Bản mới KHÔNG bắt buộc chỉ hiện một dòng thông báo, tắt được.
 */
export function PolicyGate({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const pathname = usePathname();
  const subject = user !== null && SUBJECT_ROLES.includes(user.role);

  const [attempt, setAttempt] = useState(0);
  const key = subject ? `${user.id}:${attempt}` : "none";
  const { data, loading } = useFetched<{ userId: string; status: PolicyAcceptanceStatus } | null>(key, async () =>
    subject ? { userId: user.id, status: await getPolicyAcceptanceStatus() } : null,
  );
  const refresh = useCallback(() => setAttempt((n) => n + 1), []);

  // Trang phía sau đã có lời gọi bị chặn 403 (dữ liệu chưa tải được): đồng ý xong thì dựng lại trang một lần để nó tự tải lại.
  // Không có lời gọi nào bị chặn thì giữ nguyên trang, không mất dữ liệu đang nhập.
  const missed = useRef(false);
  const [epoch, setEpoch] = useState(0);
  const accepted = useCallback(() => {
    if (missed.current) {
      missed.current = false;
      setEpoch((n) => n + 1);
    }
    refresh();
  }, [refresh]);

  // Server báo 403 policy_acceptance_required ở bất kỳ lời gọi nào (vd. admin vừa công bố bản bắt buộc): kiểm tra lại
  useEffect(() => {
    const onRequired = () => {
      missed.current = true;
      refresh();
    };
    window.addEventListener(POLICY_REQUIRED_EVENT, onRequired);
    return () => window.removeEventListener(POLICY_REQUIRED_EVENT, onRequired);
  }, [refresh]);

  // Chỉ tin kết quả của CHÍNH người đang đăng nhập (đổi tài khoản không tải lại trang thì kết quả cũ thuộc người khác).
  // Đang tải lại thì vẫn dùng kết quả cũ của người này, để hộp không nhấp nháy.
  const status = subject && data?.userId === user.id ? data.status : null;
  const readable = READABLE_PATHS.some((p) => pathname === p || pathname.startsWith(`${p}/`));
  const blocked = status?.mustAccept === true && !readable;

  // Cấu trúc cố định (thông báo, trang, hộp) để trang phía sau không bị dựng lại mỗi lần đổi trạng thái
  return (
    <>
      {status && !blocked && status.hasUpdates && !status.mustAccept ? <PolicyUpdateNotice status={status} /> : null}
      <div key={epoch} inert={blocked} aria-hidden={blocked || undefined} className="contents">
        {children}
      </div>
      {blocked && status ? <PolicyAcceptanceDialog status={status} loading={loading} onAccepted={accepted} onChanged={refresh} /> : null}
    </>
  );
}

// ---------------------------------------------------------------- hộp bắt buộc đồng ý

function PolicyAcceptanceDialog({
  status,
  loading,
  onAccepted,
  onChanged,
}: {
  status: PolicyAcceptanceStatus;
  loading: boolean;
  /** Đồng ý thành công: kiểm tra lại trạng thái và cho trang phía sau tải lại dữ liệu bị chặn */
  onAccepted: () => void;
  onChanged: () => void;
}) {
  const { logout } = useAuth();
  const blocking = status.items.filter((i) => i.blocks);
  const firstTime = blocking.some((i) => i.state === "never_accepted");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const accept = async () => {
    setError(null);
    setSubmitting(true);
    try {
      // Gửi ĐÚNG phiên bản đang hiển thị; admin vừa công bố bản khác thì server từ chối và không ghi gì
      const items: AcceptedPolicy[] = blocking.map((i) => ({ kind: i.kind as AcceptedPolicy["kind"], version: i.currentVersion ?? "" }));
      await acceptPolicies(items);
      onAccepted();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Có lỗi kết nối máy chủ. Vui lòng thử lại.");
      if (err instanceof ApiError && err.status === 409) onChanged();   // nội dung đã đổi: tải lại để hiện bản mới
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open disablePointerDismissal onOpenChange={() => {}}>
      <DialogContent variant="action" size="lg">
        <DialogHeader>
          <DialogIcon tone="brand">
            <ShieldCheckIcon aria-hidden />
          </DialogIcon>
          <DialogTitle>
            {firstTime ? "Trước khi tiếp tục" : "Chúng tôi vừa cập nhật điều khoản"}
          </DialogTitle>
          <DialogDescription>
            {firstTime
              ? "USDSS cần bạn xác nhận đã xem các nội dung sau."
              : "Vui lòng xem nội dung mới và xác nhận để tiếp tục sử dụng USDSS."}
          </DialogDescription>
        </DialogHeader>

        <DialogBody className="space-y-4 text-left">
          <ul className="space-y-3">
            {blocking.map((item) => (
              <li key={item.kind} className="space-y-0.5">
                <div className="flex flex-wrap items-baseline justify-between gap-2">
                  <Link href={kindHref(item)} target="_blank" rel="noopener noreferrer" className="font-medium text-brand hover:underline">
                    {kindLabel(item)}
                  </Link>
                  {item.currentVersion && <span className="text-body-s text-ink-3">Phiên bản {item.currentVersion}</span>}
                </div>
                {item.state === "update_required" && item.changeSummary && <p className="text-sm text-ink-2">Có gì mới: {item.changeSummary}</p>}
              </li>
            ))}
          </ul>

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <p className="text-center text-body-s text-ink-3">
            Bằng việc bấm nút bên dưới, bạn đồng ý với{" "}
            {blocking.map((item, i) => (
              <span key={item.kind}>
                {i > 0 && (i === blocking.length - 1 ? " và " : ", ")}
                <Link href={kindHref(item)} target="_blank" rel="noopener noreferrer" className="font-semibold text-ink hover:underline">
                  {kindLabel(item)}
                </Link>
              </span>
            ))}{" "}
            của USDSS.
          </p>
        </DialogBody>

        <DialogFooter layout="stack">
          <DialogAction type="button" tone="primary" autoFocus disabled={submitting || loading} loading={submitting} onClick={accept}>
            Đồng ý và tiếp tục
          </DialogAction>
          <DialogAction type="button" tone="ghost" disabled={submitting} onClick={() => logout()}>
            Không đồng ý, đăng xuất
          </DialogAction>
        </DialogFooter>
      </DialogContent>
    </Dialog>
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
