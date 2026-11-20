"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { CheckIcon, TriangleAlertIcon, XIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { formatVnDateTime, getBillingError } from "@/lib/billing-api";
import { familyApi, familyErrorMessage } from "@/lib/family-api";
import type { FamilyInviteLookupDto } from "@/types/billing";

/** Trang xác thực email đọc khóa này để quay lại đây sau khi đăng ký (đường dẫn nội bộ, không phải email). */
export const AUTH_RETURN_TO_KEY = "usas.authReturnTo";

const SEAT_LABEL = { student: "Học sinh", parent: "Phụ huynh" } as const;

const STATUS_TEXT: Record<string, string> = {
  accepted: "Lời mời này đã được chấp nhận.",
  declined: "Lời mời này đã bị từ chối.",
  cancelled: "Chủ gói đã hủy lời mời này.",
  expired: "Lời mời đã hết hạn. Hãy nhờ chủ gói gửi lại lời mời.",
};

// [UC-130] Người được mời mở link trong mail: xem chủ gói và loại chỗ, đăng nhập nếu cần, rồi chấp nhận hoặc từ chối.
export function FamilyInviteForm() {
  const router = useRouter();
  const token = useSearchParams().get("token") ?? "";
  const { user, isLoading: authLoading, logout } = useAuth();

  const [invite, setInvite] = useState<FamilyInviteLookupDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : "Link lời mời không hợp lệ hoặc đã bị hủy.");
  const [result, setResult] = useState<"accepted" | "declined" | null>(null);
  const [busy, setBusy] = useState<"accept" | "decline" | null>(null);
  const [error, setError] = useState<{ code: string | null; text: string } | null>(null);

  useEffect(() => {
    if (!token) return;
    familyApi.lookupInvite(token).then(setInvite, (err: unknown) => setLoadError(familyErrorMessage(err)));
  }, [token]);

  const returnPath = `/family/invite?token=${encodeURIComponent(token)}`;
  const loginHref = `/login?redirect=${encodeURIComponent(returnPath)}`;

  const decide = async (choice: "accept" | "decline") => {
    setBusy(choice);
    setError(null);
    try {
      if (choice === "accept") await familyApi.acceptInvite(token);
      else await familyApi.declineInvite(token);
      setResult(choice === "accept" ? "accepted" : "declined");
    } catch (err) {
      setError({ code: getBillingError(err)?.code ?? null, text: familyErrorMessage(err) });
    } finally {
      setBusy(null);
    }
  };

  const switchAccount = async () => {
    await logout();
    router.push(loginHref);
  };

  const heading = (
    <div className="space-y-1">
      <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">Lời mời gói Gia đình</h1>
      <p className="text-sm text-ink-2">Dùng chung lượt hỏi của gói USDSS Premium Gia đình.</p>
    </div>
  );

  if (loadError) {
    return (
      <div className="space-y-6">
        {heading}
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError}</AlertDescription>
        </Alert>
      </div>
    );
  }
  if (!invite || authLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải lời mời...
      </div>
    );
  }

  if (result === "accepted") {
    return (
      <div className="space-y-6">
        {heading}
        <Alert variant="success" role="status">
          <CheckIcon aria-hidden />
          <AlertTitle>Bạn đã tham gia gói Gia đình của {invite.ownerName}</AlertTitle>
          <AlertDescription>Từ giờ bạn dùng chung lượt hỏi của gói này.</AlertDescription>
        </Alert>
        <Link href="/account#family" className={buttonVariants({ size: "xl", className: "w-full" })}>
          Xem gói Gia đình
        </Link>
      </div>
    );
  }
  if (result === "declined") {
    return (
      <div className="space-y-6">
        {heading}
        <Alert variant="warning" role="status">
          <XIcon aria-hidden />
          <AlertTitle>Bạn đã từ chối lời mời</AlertTitle>
          <AlertDescription>Nếu bấm nhầm, hãy nhờ {invite.ownerName} gửi lại lời mời.</AlertDescription>
        </Alert>
        <Link href="/" className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}>
          Về trang chủ
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {heading}
      <div className="space-y-1.5 text-sm text-ink">
        <div>
          <span className="text-label">Chủ gói: </span>
          <span className="font-semibold">{invite.ownerName}</span>
        </div>
        <div>
          <span className="text-label">Bạn được mời vào chỗ: </span>
          <span className="font-semibold">{SEAT_LABEL[invite.seatRole]}</span>
        </div>
        <div className="text-body-s text-ink-3">Lời mời hết hạn lúc {formatVnDateTime(invite.expiresAt)}.</div>
      </div>
      <Alert variant="info">
        <AlertDescription>Chủ gói sẽ thấy phần trăm lượt hỏi bạn đã dùng trong gói</AlertDescription>
      </Alert>

      {invite.status !== "pending" ? (
        <Alert variant="warning">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{STATUS_TEXT[invite.status] ?? "Lời mời này không còn hiệu lực."}</AlertDescription>
        </Alert>
      ) : !user ? (
        <div className="space-y-3">
          <p className="text-sm text-ink-2">
            Đăng nhập bằng đúng email được mời để trả lời. Chưa có tài khoản thì đăng ký tài khoản {SEAT_LABEL[invite.seatRole]} bằng email
            đó, xác thực email rồi quay lại đây.
          </p>
          <Link href={loginHref} className={buttonVariants({ size: "xl", className: "w-full" })}>
            Đăng nhập
          </Link>
          <Link
            href={invite.seatRole === "parent" ? "/register?role=parent" : "/register"}
            onClick={() => {
              try {
                sessionStorage.setItem(AUTH_RETURN_TO_KEY, returnPath);
              } catch {
                // Trình duyệt chặn lưu trữ: sau khi xác thực sẽ về trang chủ, người dùng mở lại link trong mail
              }
            }}
            className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}
          >
            Đăng ký tài khoản
          </Link>
        </div>
      ) : (
        <div className="space-y-3">
          {error && (
            <Alert variant="destructive" role="alert">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription className="space-y-2">
                <p>{error.text}</p>
                {error.code === "wrong_account" && (
                  <Button type="button" variant="outline" size="sm" onClick={switchAccount}>
                    Đăng nhập bằng tài khoản khác
                  </Button>
                )}
                {error.code === "pending_family_order" && (
                  <Link href="/orders" className="font-medium underline">
                    Xem đơn hàng
                  </Link>
                )}
              </AlertDescription>
            </Alert>
          )}
          <div className="grid grid-cols-2 gap-3">
            <Button type="button" size="xl" variant="outline" disabled={busy !== null} onClick={() => decide("decline")}>
              {busy === "decline" ? "Đang gửi..." : "Từ chối"}
            </Button>
            <Button type="button" size="xl" disabled={busy !== null} onClick={() => decide("accept")}>
              {busy === "accept" ? "Đang tham gia..." : "Chấp nhận"}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
