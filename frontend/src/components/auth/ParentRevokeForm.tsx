"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckIcon, TriangleAlertIcon } from "lucide-react";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentRevokeInfo } from "@/types/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Spinner } from "@/components/ui/spinner";

const INVALID_LINK = "Liên kết rút đồng ý không hợp lệ hoặc đã được sử dụng.";
const NETWORK_ERROR = "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.";

/** 403 là con đã đủ 16 tuổi: hiện câu của máy chủ; lỗi khác dùng một câu chung, không lộ thông tin. */
function lookupErrorMessage(err: unknown): string {
  if (err instanceof ApiError) return err.status === 403 && err.message ? err.message : INVALID_LINK;
  return NETWORK_ERROR;
}

// [UC-117] Phụ huynh mở từ link trong mail xác nhận đồng ý: rút đồng ý không cần đăng nhập.
// [UC-111 3a] Hai lựa chọn: chỉ rút đồng ý (bấm nút là đủ) hoặc rút kèm xóa tài khoản con dưới 16 (phải tick đã hiểu).
// Link chỉ mở được khi con dưới 16 (máy chủ trả 403 khi con đủ 16) nên lựa chọn xóa luôn có.
export function ParentRevokeForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  const [info, setInfo] = useState<ParentRevokeInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : INVALID_LINK);
  const [isLoadingInfo, setIsLoadingInfo] = useState(Boolean(token));
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [revoked, setRevoked] = useState(false);
  const [choice, setChoice] = useState<"revoke" | "delete">("revoke");
  const [deleteUnderstood, setDeleteUnderstood] = useState(false);
  const [deletedMessage, setDeletedMessage] = useState<string | null>(null);

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
      if (choice === "delete") {
        const res = await parentLinksApi.revokeAndDeleteByToken(token);
        setDeletedMessage(res.message || "Đã rút đồng ý và yêu cầu xóa tài khoản của con.");
      } else {
        await parentLinksApi.revokeByToken(token);
      }
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
        <p className="text-sm text-ink-2">Ngừng cho phép hệ thống xử lý dữ liệu học tập của con, có thể kèm xóa tài khoản của con.</p>
      </div>

      {loadError || !info ? (
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError}</AlertDescription>
        </Alert>
      ) : revoked && deletedMessage ? (
        <div className="space-y-6">
          <Alert variant="success" role="status">
            <CheckIcon aria-hidden />
            <AlertTitle>Đã rút đồng ý và yêu cầu xóa tài khoản</AlertTitle>
            <AlertDescription>
              {deletedMessage} Chúng tôi đã gửi tới email của bạn hai nút &quot;Hủy xóa và đồng ý lại cho con&quot; và &quot;Chỉ hủy xóa&quot;,
              dùng được tới ngày xóa vĩnh viễn nếu bạn đổi ý.
            </AlertDescription>
          </Alert>
          <Link href="/" className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}>
            Về trang chủ
          </Link>
        </div>
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

          <fieldset className="space-y-3">
            <legend className="mb-2 text-sm font-semibold text-ink">Bạn muốn làm gì?</legend>
            <label
              className={`flex cursor-pointer gap-3 rounded-xl border p-4 text-sm ${choice === "revoke" ? "border-brand bg-brand-wash" : "border-line"}`}
            >
              <input
                type="radio"
                name="revoke-choice"
                className="mt-1"
                checked={choice === "revoke"}
                onChange={() => setChoice("revoke")}
              />
              <span className="space-y-1">
                <span className="block font-semibold text-ink">Chỉ rút đồng ý</span>
                <span className="block text-ink-2">
                  Hệ thống ngừng xử lý hồ sơ học tập của con; con tạm không dùng được các chức năng cần hồ sơ. Liên kết giữa bạn và con bị
                  hủy. Tài khoản và dữ liệu của con không bị xóa; muốn cho phép lại, con gửi lời mời mới từ trang Tài khoản.
                </span>
              </span>
            </label>
            <label
              className={`flex cursor-pointer gap-3 rounded-xl border p-4 text-sm ${choice === "delete" ? "border-risk bg-risk-wash" : "border-line"}`}
            >
              <input
                type="radio"
                name="revoke-choice"
                className="mt-1"
                checked={choice === "delete"}
                onChange={() => setChoice("delete")}
              />
              <span className="space-y-1">
                <span className="block font-semibold text-ink">Rút đồng ý và xóa tài khoản của con</span>
                <span className="block text-ink-2">
                  Ngoài việc rút đồng ý, tài khoản của con bị khóa ngay và xóa vĩnh viễn sau 30 ngày. Con không tự khôi phục được; bạn nhận
                  email có nút hủy xóa dùng được tới ngày xóa vĩnh viễn.
                </span>
              </span>
            </label>
          </fieldset>

          <p className="text-sm text-ink-2">Con sẽ nhận email báo việc này.</p>

          {choice === "delete" && (
            <div className="flex items-start gap-2">
              <Checkbox
                id="parent-revoke-delete-understood"
                checked={deleteUnderstood}
                onCheckedChange={(checked) => setDeleteUnderstood(Boolean(checked))}
              />
              <Label htmlFor="parent-revoke-delete-understood" className="leading-snug text-ink">
                Tôi hiểu tài khoản và dữ liệu của {info.studentName} sẽ bị xóa vĩnh viễn nếu không hủy trong 30 ngày.
              </Label>
            </div>
          )}

          {error && (
            <Alert variant="destructive">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Button
            type="button"
            variant="destructive"
            size="xl"
            className="w-full"
            onClick={handleRevoke}
            disabled={isSubmitting || (choice === "delete" && !deleteUnderstood)}
            loading={isSubmitting}
          >
            {isSubmitting ? "Đang xử lý..." : choice === "delete" ? "Rút đồng ý và xóa tài khoản" : "Rút đồng ý"}
          </Button>
        </>
      )}
    </div>
  );
}
