"use client";

import React, { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckIcon, ShieldCheckIcon, TriangleAlertIcon, XIcon } from "lucide-react";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentInviteInfo } from "@/types/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";

/** Trang đăng ký đọc khóa này để điền sẵn email phụ huynh (không đặt email trên URL) */
export const REGISTER_PARENT_EMAIL_KEY = "usas.registerParentEmail";

const EXPIRED_MESSAGE = "Lời mời đã hết hạn (quá 7 ngày). Hãy nhờ con gửi lại lời mời từ trang Tài khoản.";
const INVALID_MESSAGE = "Link xác nhận không hợp lệ.";

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString("vi-VN") : "";
}

// [UC-115] Trang phụ huynh mở từ link trong mail mời: đọc nội dung rồi Đồng ý / Từ chối, không cần tài khoản.
// Luật BVDLCN 91/2025 Điều 24 + NĐ 356/2025 Điều 6: đồng ý phải chủ động (không tick sẵn) và kiểm chứng được.
export function ParentConsentForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  // Phụ huynh bấm nút Từ chối trong mail: vẫn phải bấm lại ở đây (mở link không đổi trạng thái)
  const choseReject = searchParams.get("choice") === "reject";

  const [invite, setInvite] = useState<ParentInviteInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : INVALID_MESSAGE);
  const [isLoadingInvite, setIsLoadingInvite] = useState(Boolean(token));
  const [isGuardian, setIsGuardian] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState<"accept" | "reject" | null>(null);

  const loadInvite = useCallback(
    () =>
      parentLinksApi
        .lookupInvite(token)
        .then((res) => setInvite(res.data))
        .catch((err) => {
          if (err instanceof ApiError && err.status === 410) {
            setLoadError(EXPIRED_MESSAGE);
          } else if (err instanceof ApiError) {
            setLoadError(INVALID_MESSAGE);
          } else {
            setLoadError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
          }
        })
        .finally(() => setIsLoadingInvite(false)),
    [token],
  );

  useEffect(() => {
    if (token) loadInvite();
  }, [token, loadInvite]);

  const decide = async (choice: "accept" | "reject") => {
    setError(null);
    setSubmitting(choice);
    try {
      const res = choice === "accept" ? await parentLinksApi.acceptInvite(token, isGuardian) : await parentLinksApi.rejectInvite(token);
      setInvite(res.data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        // Đã có người trả lời bằng link này (tab khác, bấm 2 lần): tải lại để hiện kết quả đã ghi nhận
        await loadInvite();
      } else if (err instanceof ApiError && err.status === 410) {
        setLoadError(EXPIRED_MESSAGE);
      } else if (err instanceof ApiError) {
        setError(err.message || "Không thể ghi nhận lúc này. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setSubmitting(null);
    }
  };

  if (isLoadingInvite) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2">
        <Spinner />
        Đang tải lời mời...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Phụ huynh xác nhận đồng ý
        </h1>
        <p className="text-sm text-ink-2">
          Cho phép hệ thống xử lý dữ liệu học tập của con. Bạn không cần tài khoản để trả lời.
        </p>
      </div>

      {loadError || !invite ? (
        <Alert variant="destructive">
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>{loadError ?? INVALID_MESSAGE}</AlertDescription>
        </Alert>
      ) : invite.state === "accepted" ? (
        <div className="space-y-6">
          <Alert variant="success" role="status">
            <CheckIcon aria-hidden />
            <AlertTitle>Đã ghi nhận bạn đồng ý</AlertTitle>
            <AlertDescription>
              Bạn đã đồng ý cho hệ thống xử lý dữ liệu của {invite.studentName} lúc {formatDate(invite.decidedAt)}. Chúng tôi đã gửi tới{" "}
              {invite.parentEmail} một email xác nhận có link <strong>Rút đồng ý</strong>, dùng được bất cứ lúc nào mà không cần đăng nhập.
            </AlertDescription>
          </Alert>
          <p className="text-sm text-ink-2">
            Muốn xem các con đã liên kết và quản lý đồng ý trên web? Đăng ký tài khoản phụ huynh bằng đúng email {invite.parentEmail},
            hệ thống sẽ tự liên kết.
          </p>
          {/* Trang đăng ký chọn sẵn Phụ huynh; email truyền qua sessionStorage, không đặt trên URL */}
          <Link
            href="/register?role=parent"
            onClick={() => {
              try {
                sessionStorage.setItem(REGISTER_PARENT_EMAIL_KEY, invite.parentEmail);
              } catch {
                // Trình duyệt chặn lưu trữ: vẫn mở trang đăng ký, phụ huynh tự gõ email
              }
            }}
            className={buttonVariants({ size: "xl", variant: "outline", className: "w-full" })}
          >
            Đăng ký tài khoản phụ huynh
          </Link>
        </div>
      ) : invite.state === "rejected" ? (
        <Alert variant="warning" role="status">
          <XIcon aria-hidden />
          <AlertTitle>Đã ghi nhận bạn từ chối</AlertTitle>
          <AlertDescription>
            Bạn đã từ chối lúc {formatDate(invite.decidedAt)}. Hệ thống không xử lý dữ liệu học tập của {invite.studentName}. Nếu bấm nhầm,
            hãy nhờ con gửi lại lời mời từ trang Tài khoản.
          </AlertDescription>
        </Alert>
      ) : (
        <>
          <div className="space-y-1.5 text-sm text-ink">
            <div>
              <span className="text-label">Học sinh: </span>
              <span className="font-semibold">{invite.studentName}</span>
              {invite.studentAge != null && <span className="text-ink-2"> ({invite.studentAge} tuổi)</span>}
            </div>
            <div>
              <span className="text-label">Email phụ huynh được mời: </span>
              <span className="font-semibold">{invite.parentEmail}</span>
            </div>
            {invite.expiresAt && (
              <div className="text-body-s text-ink-3">Lời mời hết hạn lúc {formatDate(invite.expiresAt)}.</div>
            )}
          </div>

          <Separator />

          {/* Nội dung đồng ý (phiên bản policyVersion được lưu kèm bản ghi đồng ý) */}
          <div className="space-y-3 text-sm">
            <h2 className="flex items-center gap-2 font-semibold text-ink">
              <ShieldCheckIcon className="size-4 text-brand" aria-hidden />
              Nội dung đồng ý (phiên bản {invite.policyVersion})
            </h2>
            <div className="space-y-1">
              <div className="font-medium text-ink">Dữ liệu được xử lý</div>
              <ul className="list-disc pl-5 text-ink-2 space-y-0.5">
                <li>Họ tên, ngày sinh, email, số điện thoại của con.</li>
                <li>Hồ sơ học thuật: trường, lớp, bảng điểm, chứng chỉ, hoạt động ngoại khóa.</li>
                <li>Thông tin tài chính du học do gia đình tự khai (nếu nhập).</li>
                <li>Nội dung con trao đổi với trợ lý tư vấn AI.</li>
              </ul>
            </div>
            <div className="space-y-1">
              <div className="font-medium text-ink">Mục đích</div>
              <p className="text-ink-2">
                Lưu hồ sơ, phân tích năng lực và gợi ý trường phù hợp cho việc du học Mỹ. Không bán hay chia sẻ dữ liệu cho bên thứ ba;
                hồ sơ chỉ được gửi cho trung tâm tư vấn khi bạn xác nhận riêng từng lần.
              </p>
            </div>
            <div className="space-y-1">
              <div className="font-medium text-ink">Quyền của phụ huynh</div>
              <p className="text-ink-2">
                Rút lại sự đồng ý bất cứ lúc nào bằng link trong email xác nhận gửi sau khi bạn đồng ý (không cần đăng nhập). Khi rút,
                hệ thống ngừng xử lý dữ liệu của con (con dưới 16 tuổi quay về chế độ hạn chế).
              </p>
            </div>
            <p className="text-body-s text-ink-3">
              Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (Điều 24) và Nghị định 356/2025/NĐ-CP. Hệ thống lưu thời điểm,
              email nhận thư mời và địa chỉ IP khi bạn trả lời.
            </p>
          </div>

          <Separator />

          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {invite.blockedReason && (
            <Alert variant="destructive">
              <TriangleAlertIcon aria-hidden />
              <AlertDescription>{invite.blockedReason}</AlertDescription>
            </Alert>
          )}

          {choseReject && (
            <p className="text-sm text-ink-2">
              Bạn đã chọn <strong className="text-ink">Từ chối</strong> trong email. Bấm nút Từ chối bên dưới để xác nhận.
            </p>
          )}

          <div className="space-y-4">
            {!invite.blockedReason && (
            <div className="flex items-start gap-2">
              <Checkbox id="parent-consent-guardian" checked={isGuardian} onCheckedChange={(checked) => setIsGuardian(Boolean(checked))} />
              <Label htmlFor="parent-consent-guardian" className="leading-snug text-ink">
                Tôi là cha, mẹ hoặc người giám hộ hợp pháp của {invite.studentName}, đã đọc nội dung trên.
              </Label>
            </div>
            )}
            <div className="flex flex-col gap-2 sm:flex-row">
              {!invite.blockedReason && (
              <Button
                type="button"
                size="xl"
                className="flex-1"
                disabled={!isGuardian || submitting !== null}
                onClick={() => decide("accept")}
              >
                {submitting === "accept" && <Spinner data-icon="inline-start" />}
                {submitting === "accept" ? "Đang ghi nhận..." : "Đồng ý"}
              </Button>
              )}
              <Button
                type="button"
                size="xl"
                variant="outline"
                className="flex-1"
                disabled={submitting !== null}
                onClick={() => decide("reject")}
              >
                {submitting === "reject" && <Spinner data-icon="inline-start" />}
                {submitting === "reject" ? "Đang ghi nhận..." : "Từ chối"}
              </Button>
            </div>
            <p className="text-body-s text-ink-3">
              Nếu bạn không phải phụ huynh của {invite.studentName}, hãy đóng trang này; hệ thống không xử lý dữ liệu của em khi chưa có
              phụ huynh đồng ý.
            </p>
          </div>
        </>
      )}
    </div>
  );
}
