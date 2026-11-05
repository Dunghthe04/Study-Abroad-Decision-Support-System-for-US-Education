"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckIcon, ShieldCheckIcon, TriangleAlertIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { parentLinksApi, ApiError } from "@/lib/api";
import type { ParentInviteInfo } from "@/types/api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { Spinner } from "@/components/ui/spinner";

// [Q11] Trang phụ huynh mở từ link trong mail mời: đọc nội dung đồng ý rồi xác nhận.
// Luật BVDLCN 91/2025 Điều 24 + NĐ 356/2025 Điều 6: đồng ý phải chủ động (không tick sẵn) và kiểm chứng được.
export function ParentConsentForm() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const { user, isLoading } = useAuth();

  const [invite, setInvite] = useState<ParentInviteInfo | null>(null);
  const [loadError, setLoadError] = useState<string | null>(token ? null : "Link xác nhận không hợp lệ.");
  const [isLoadingInvite, setIsLoadingInvite] = useState(Boolean(token));
  const [agreed, setAgreed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [accepted, setAccepted] = useState(false);

  useEffect(() => {
    if (!token) return;
    parentLinksApi
      .lookupInvite(token)
      .then((res) => setInvite(res.data))
      .catch((err) => {
        if (err instanceof ApiError && err.status === 410) {
          setLoadError("Lời mời đã hết hạn. Hãy nhờ con gửi lại lời mời từ trang tài khoản.");
        } else if (err instanceof ApiError) {
          setLoadError("Link xác nhận không hợp lệ hoặc đã được sử dụng.");
        } else {
          setLoadError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
        }
      })
      .finally(() => setIsLoadingInvite(false));
  }, [token]);

  const handleAccept = async () => {
    setError(null);
    setIsSubmitting(true);
    try {
      await parentLinksApi.acceptInvite(token);
      setAccepted(true);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể xác nhận lúc này. Vui lòng thử lại.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // Sau khi đăng nhập quay lại đúng trang này (LoginForm chỉ nhận đường dẫn nội bộ)
  const loginHref = `/login?redirect=${encodeURIComponent(`/parent-consent?token=${encodeURIComponent(token)}`)}`;
  const emailMatches = Boolean(user && invite && user.email.toLowerCase() === invite.parentEmail.toLowerCase());

  if (isLoadingInvite || isLoading) {
    return (
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Spinner />
        Đang tải lời mời...
      </div>
    );
  }

  return (
    <Card className="mx-auto w-full max-w-xl">
      <CardHeader className="text-center">
        <CardTitle>
          <h1>Phụ huynh xác nhận đồng ý</h1>
        </CardTitle>
        <CardDescription>
          Liên kết tài khoản với con và cho phép hệ thống xử lý dữ liệu học tập của con.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-5">
        {loadError || !invite ? (
          <Alert variant="destructive">
            <TriangleAlertIcon aria-hidden />
            <AlertDescription>{loadError}</AlertDescription>
          </Alert>
        ) : accepted ? (
          <>
            <Alert variant="success" role="status">
              <CheckIcon aria-hidden />
              <AlertTitle>Đã xác nhận</AlertTitle>
              <AlertDescription>
                Tài khoản của bạn đã liên kết với {invite.studentName}. Bạn có thể rút lại sự đồng ý bất cứ lúc nào ở trang tài khoản.
              </AlertDescription>
            </Alert>
            <Link href="/account" className={buttonVariants({ size: "lg", className: "w-full" })}>
              Về trang tài khoản
            </Link>
          </>
        ) : (
          <>
            <div className="space-y-1">
              <div>
                <span className="text-label">Học sinh: </span>
                <span className="font-semibold">{invite.studentName}</span>
                {invite.studentAge != null && <span className="text-muted-foreground"> ({invite.studentAge} tuổi)</span>}
              </div>
              <div>
                <span className="text-label">Email phụ huynh được mời: </span>
                <span className="font-semibold">{invite.parentEmail}</span>
              </div>
              <div className="text-body-s">
                Lời mời hết hạn lúc {new Date(invite.expiresAt).toLocaleString("vi-VN")}.
              </div>
            </div>

            <Separator />

            {/* Nội dung đồng ý (phiên bản policyVersion được lưu kèm bản ghi đồng ý) */}
            <div className="space-y-3 text-sm">
              <h2 className="flex items-center gap-2 font-semibold">
                <ShieldCheckIcon className="size-4" aria-hidden />
                Nội dung đồng ý (phiên bản {invite.policyVersion})
              </h2>
              <div>
                <div className="font-medium">Dữ liệu được xử lý</div>
                <ul className="list-disc pl-5 text-muted-foreground">
                  <li>Họ tên, ngày sinh, email, số điện thoại của con.</li>
                  <li>Hồ sơ học thuật: trường, lớp, bảng điểm, chứng chỉ, hoạt động ngoại khóa.</li>
                  <li>Thông tin tài chính du học do gia đình tự khai (nếu nhập).</li>
                  <li>Nội dung con trao đổi với trợ lý tư vấn AI.</li>
                </ul>
              </div>
              <div>
                <div className="font-medium">Mục đích</div>
                <p className="text-muted-foreground">
                  Lưu hồ sơ, phân tích năng lực và gợi ý trường phù hợp cho việc du học Mỹ. Không bán hay chia sẻ dữ liệu cho bên thứ ba;
                  hồ sơ chỉ được gửi cho trung tâm tư vấn khi có sự đồng ý riêng.
                </p>
              </div>
              <div>
                <div className="font-medium">Quyền của phụ huynh</div>
                <p className="text-muted-foreground">
                  Xem các con đã liên kết và rút lại sự đồng ý bất cứ lúc nào ở trang tài khoản. Khi rút, hệ thống ngừng xử lý dữ liệu
                  của con (con dưới 16 tuổi quay về chế độ hạn chế).
                </p>
              </div>
              <p className="text-body-s">
                Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (Điều 24) và Nghị định 356/2025/NĐ-CP. Hệ thống lưu thời điểm,
                tài khoản và địa chỉ IP khi bạn xác nhận.
              </p>
            </div>

            <Separator />

            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            {!user ? (
              <div className="space-y-3">
                <p className="text-sm">
                  Vui lòng đăng nhập bằng tài khoản <strong>phụ huynh</strong> có email <strong>{invite.parentEmail}</strong> để xác nhận.
                </p>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Link href={loginHref} className={buttonVariants({ size: "lg", className: "flex-1" })}>
                    Đăng nhập
                  </Link>
                  <Link href="/register" className={buttonVariants({ size: "lg", variant: "outline", className: "flex-1" })}>
                    Đăng ký tài khoản phụ huynh
                  </Link>
                </div>
                <p className="text-body-s">
                  Sau khi đăng ký và xác thực email, hãy mở lại link trong mail mời để xác nhận.
                </p>
              </div>
            ) : user.role !== "parent" || !emailMatches ? (
              <Alert variant="warning">
                <TriangleAlertIcon aria-hidden />
                <AlertDescription>
                  Bạn đang đăng nhập bằng {user.email}. Chỉ tài khoản phụ huynh có email {invite.parentEmail} mới xác nhận được lời mời này.
                  Hãy đăng xuất và đăng nhập đúng tài khoản.
                </AlertDescription>
              </Alert>
            ) : (
              <div className="space-y-4">
                <div className="flex items-start gap-2">
                  <Checkbox id="parent-consent-agree" checked={agreed} onCheckedChange={(checked) => setAgreed(checked)} />
                  <Label htmlFor="parent-consent-agree" className="leading-snug">
                    Tôi là cha, mẹ hoặc người giám hộ hợp pháp của {invite.studentName}, đã đọc và đồng ý với nội dung trên.
                  </Label>
                </div>
                <Button type="button" size="lg" className="w-full" disabled={!agreed || isSubmitting} onClick={handleAccept}>
                  {isSubmitting && <Spinner data-icon="inline-start" />}
                  {isSubmitting ? "Đang xác nhận..." : "Đồng ý và liên kết"}
                </Button>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
