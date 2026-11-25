"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowRightIcon, RotateCcwIcon, ShieldAlertIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError, authApi } from "@/lib/api";
import type { PendingDeletionProblem } from "@/types/api";
import { setPendingEmail } from "@/lib/pending-email";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { PasswordInput } from "@/components/auth/PasswordInput";
import { Spinner } from "@/components/ui/spinner";

export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  // Mặc định về trang chủ; chỉ nhận redirect là đường dẫn nội bộ (chặn "//evil.com" và "/\evil.com",
  // trình duyệt coi "\" như "/" nên "/\evil.com" cũng là URL sang domain khác)
  const redirectParam = searchParams.get("redirect");
  const redirect =
    redirectParam && /^\/(?![\/\\])/.test(redirectParam) ? redirectParam : "/";

  const { login, user, isLoading, refreshUser } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [lockType, setLockType] = useState<"temp_locked" | "admin_locked" | "unverified" | "pending_deletion" | null>(null);
  // [UC-80] Mã khôi phục server trả kèm khi đăng nhập vào tài khoản đang chờ xóa
  const [restoreToken, setRestoreToken] = useState<string | null>(null);
  const [isRestoring, setIsRestoring] = useState(false);

  // Nếu người dùng đã đăng nhập sẵn mà truy cập /login:
  // Tài khoản admin/moderator tự động chuyển thẳng vào /admin
  useEffect(() => {
    if (!isLoading && user) {
      if (user.role === "admin" || user.role === "moderator") {
        const target = redirectParam && redirectParam !== "/" ? redirect : "/admin";
        router.replace(target);
      } else {
        router.replace(redirect);
      }
    }
  }, [isLoading, user, redirect, redirectParam, router]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLockType(null);
    setRestoreToken(null);

    if (!email.trim() || !password) {
      setError("Vui lòng nhập đầy đủ email và mật khẩu.");
      return;
    }

    setIsSubmitting(true);
    try {
      // [AC-2] Đăng nhập: Gọi API xác thực
      const loggedUser = await login({ email: email.trim(), password });
      if (loggedUser.role === "admin" || loggedUser.role === "moderator") {
        const target = redirectParam && redirectParam !== "/" ? redirect : "/admin";
        router.push(target);
      } else {
        router.push(redirect);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        const pending = getPendingDeletion(err);
        if (isPendingDeletionByParent(err)) {
          // [UC-75 4b] Người đại diện yêu cầu xóa, học sinh dưới 16: không tự khôi phục được, chỉ hiện câu của máy chủ
          setError(err.message || "Người đại diện của bạn đã yêu cầu xóa tài khoản này.");
        } else if (pending) {
          setLockType("pending_deletion");
          setRestoreToken(pending.restoreToken);
          setError(`Tài khoản đang chờ xóa và sẽ bị xóa hẳn vào ngày ${formatDate(pending.deletionDate)}. Bạn có thể khôi phục để dùng tiếp.`);
        } else if (err.status === 423) {
          const isTempLock = err.message?.includes("5 lần");
          setLockType(isTempLock ? "temp_locked" : "admin_locked");
          setError(
            err.message ||
              (isTempLock
                ? "Tài khoản tạm khóa 15 phút do nhập sai mật khẩu 5 lần liên tiếp."
                : "Tài khoản của bạn đã bị khóa bởi Quản trị viên.")
          );
        } else if (err.status === 403 && (err.message.includes("chưa được kích hoạt") || err.message.toLowerCase().includes("unverified"))) {
          setError(err.message);
          setLockType("unverified");
        } else {
          setError(err.message || "Sai tài khoản hoặc mật khẩu.");
        }
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // [UC-80] Khôi phục thành công thì server đăng nhập luôn: tải lại người dùng, effect phía trên tự chuyển trang
  const handleRestore = async () => {
    if (!restoreToken) return;
    setIsRestoring(true);
    try {
      await authApi.restore({ email: email.trim(), restoreToken });
      setError(null);
      setLockType(null);
      setRestoreToken(null);
      await refreshUser();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      setLockType(null);
      setRestoreToken(null);
    } finally {
      setIsRestoring(false);
    }
  };

  // [AUTH-099] Chưa biết đã đăng nhập hay chưa, hoặc đã đăng nhập và đang chuyển đi: không hiện form
  if (isLoading || user) {
    return (
      <div className="flex items-center gap-2 text-sm text-ink-2" role="status">
        <Spinner />
        Đang tải...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Đăng nhập vào USDSS
        </h1>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>
            <p>{error}</p>

            {lockType === "temp_locked" && (
              <p className="mt-2 flex items-start gap-1.5 text-xs font-medium">
                <ShieldAlertIcon className="size-3.5 shrink-0 mt-0.5" aria-hidden="true" />
                <span>
                  Tài khoản tự mở khóa sau 15 phút. Nếu cần mở sớm, vui lòng liên hệ Quản trị viên qua{" "}
                  <a
                    href="mailto:support@usas.edu.vn?subject=[Yêu cầu mở khóa tài khoản]"
                    className="font-semibold text-brand underline underline-offset-2 hover:opacity-80"
                  >
                    support@usas.edu.vn
                  </a>.
                </span>
              </p>
            )}

            {lockType === "admin_locked" && (
              <p className="mt-2 flex items-start gap-1.5 text-xs font-medium">
                <ShieldAlertIcon className="size-3.5 shrink-0 mt-0.5" aria-hidden="true" />
                <span>
                  Nếu bạn là đại diện trung tâm muốn rút lại yêu cầu xóa hoặc cần hỗ trợ mở lại tài khoản, vui lòng phản hồi email thông báo duyệt xóa hoặc gửi thư trực tiếp tới{" "}
                  <a
                    href="mailto:support@usas.edu.vn?subject=[Yêu cầu mở lại tài khoản trung tâm]"
                    className="font-semibold text-brand underline underline-offset-2 hover:opacity-80"
                  >
                    support@usas.edu.vn
                  </a>.
                </span>
              </p>
            )}
          </AlertDescription>
        </Alert>
      )}

      {error && lockType === "unverified" && (
        <Link
          href="/verify-email"
          onClick={() => setPendingEmail(email.trim())}
          className={buttonVariants({ size: "sm" })}
        >
          Kích hoạt tài khoản bằng mã OTP
          <ArrowRightIcon aria-hidden="true" />
        </Link>
      )}

      {error && lockType === "pending_deletion" && restoreToken && (
        <Button type="button" size="sm" onClick={handleRestore} disabled={isRestoring} loading={isRestoring}>
          <RotateCcwIcon aria-hidden="true" />
          {isRestoring ? "Đang khôi phục..." : "Khôi phục và đăng nhập"}
        </Button>
      )}

      <form onSubmit={handleSubmit}>
        <FieldGroup>
          {/* Email của bạn */}
          <Field>
            <FieldLabel htmlFor="email">Email của bạn</FieldLabel>
            <Input
              id="email"
              type="email"
              required
              maxLength={100}
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="example@gmail.com"
            />
          </Field>

          {/* Mật khẩu */}
          <Field>
            <FieldLabel htmlFor="password">Mật khẩu</FieldLabel>
            <PasswordInput
              id="password"
              required
              maxLength={100}
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
            />
          </Field>

          <Button type="submit" size="xl" disabled={isSubmitting} className="w-full" loading={isSubmitting}>
            {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
          </Button>
        </FieldGroup>
      </form>

      <div className="space-y-2 pt-2 text-center text-sm text-ink-2">
        <p>
          Chưa có tài khoản?{" "}
          <Link href="/register" className="font-semibold text-brand hover:underline">
            Đăng ký ngay
          </Link>
        </p>
        <p>
          Quên mật khẩu?{" "}
          <Link href="/forgot-password" className="font-semibold text-brand hover:underline">
            Khôi phục tài khoản
          </Link>
        </p>
      </div>
    </div>
  );
}

/** [UC-80] Lỗi 403 đăng nhập vào tài khoản đang chờ xóa: lấy ngày xóa và mã khôi phục */
function getPendingDeletion(err: ApiError): PendingDeletionProblem | null {
  const body = err.body as Partial<PendingDeletionProblem> | null;
  if (err.status !== 403 || body?.code !== "account_pending_deletion" || !body.restoreToken) return null;
  return { code: body.code, deletionDate: body.deletionDate ?? "", restoreToken: body.restoreToken };
}

/** [UC-75 4b] Tài khoản bị xóa theo yêu cầu người đại diện khi học sinh dưới 16: không có nút khôi phục */
function isPendingDeletionByParent(err: ApiError): boolean {
  const body = err.body as { code?: string } | null;
  return err.status === 403 && body?.code === "account_pending_deletion_by_parent";
}

function formatDate(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? "sắp tới" : date.toLocaleDateString("vi-VN");
}
