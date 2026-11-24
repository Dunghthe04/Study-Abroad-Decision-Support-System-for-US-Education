"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckIcon, TriangleAlertIcon } from "lucide-react";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentRevokeInfo } from "@/types/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";

const INVALID_LINK = "Liên kết rút đồng ý không hợp lệ hoặc đã được sử dụng.";
const NETWORK_ERROR = "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";

/** 403 là con đã đủ 16 tuổi: hiện câu của máy chủ; lỗi khác dùng một câu chung, không lộ thông tin. */
function lookupErrorMessage(err: unknown): string {
  if (err instanceof ApiError) return err.status === 403 && err.message ? err.message : INVALID_LINK;
  return NETWORK_ERROR;
}

// [UC-117] Phụ huynh mở từ link trong mail xác nhận đồng ý: rút đồng ý không cần đăng nhập.
// Chỉ rút đồng ý, không xóa tài khoản của con. Bấm nút xác nhận là đủ, không bắt gõ chữ.
export function ParentRevokeForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [info, setInfo] = useState<ParentRevokeInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : INVALID_LINK);
  const [isLoadingInfo, setIsLoadingInfo] = useState(Boolean(token));
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [revoked, setRevoked] = useState(false);

  useEffect(() => {
    if (!token) return;
    parentLinksApi
      .lookupRevoke(token)
      .then((res) => setInfo(res.data))
      .catch((err) => setLoadError(lookupErrorMessage(err)))
      .finally(() => setIsLoadingInfo(false));
  }, [token]);

  const handleRevoke = async () => {
    setError(null);
    setIsSubmitting(true);
    try {
      await parentLinksApi.revokeByToken(token);
      setRevoked(true);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setError(err.message || "Liên kết vừa được cập nhật. Vui lòng tải lại trang.");
      } else {
        setError(lookupErrorMessage(err));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoadingInfo) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">Rút đồng ý của phụ huynh</h1>
        <p className="text-sm text-ink-2">Ngừng cho phép hệ thống xử lý dữ liệu học tập của con.</p>
      </div>

      {loadError || !info ? (
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError}</AlertDescription>
        </Alert>
      ) : revoked ? (
        <div className="space-y-6">
          <Alert variant="success" role="status">
            <CheckIcon aria-hidden />
            <AlertTitle>Đã rút đồng ý</AlertTitle>
            <AlertDescription>
              Hệ thống đã ngừng xử lý dữ liệu của {info.studentName} và đã báo cho con qua email. Tài khoản của con vẫn còn; con cần phụ
              huynh đồng ý lại để dùng tiếp các chức năng cần hồ sơ.
            </AlertDescription>
          </Alert>
          <Link href="/" className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}>
            Về trang chủ
          </Link>
        </div>
      ) : (
        <>
          <div className="space-y-1.5 text-sm text-ink">
            <div>
              <span className="text-label">Học sinh: </span>
              <span className="font-semibold">{info.studentName}</span>
            </div>
            {info.acceptedAt && (
              <div>
                <span className="text-label">Bạn đã đồng ý ngày: </span>
                <span className="font-semibold">{new Date(info.acceptedAt).toLocaleDateString("vi-VN")}</span>
              </div>
            )}
          </div>

          <ul className="list-disc space-y-1 pl-5 text-sm text-ink-2">
            <li>Hệ thống ngừng xử lý hồ sơ học tập của con; con tạm không dùng được các chức năng cần hồ sơ.</li>
            <li>Liên kết giữa tài khoản của bạn và của con bị hủy. Tài khoản và dữ liệu của con không bị xóa.</li>
            <li>Con sẽ nhận email báo việc này. Muốn cho phép lại, con gửi lời mời mới từ trang Tài khoản.</li>
          </ul>

          {error && (
            <Alert variant="destructive">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Button type="button" variant="destructive" size="xl" className="w-full" onClick={handleRevoke} disabled={isSubmitting} loading={isSubmitting}>
            {isSubmitting ? "Đang rút đồng ý..." : "Rút đồng ý"}
          </Button>
        </>
      )}
    </div>
  );
}
