"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowRightIcon, ShieldAlertIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
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

  const { login, user, isLoading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [lockType, setLockType] = useState<"temp_locked" | "unverified" | null>(null);

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
        if (err.status === 423) {
          setLockType("temp_locked");
          setError("Tài khoản của bạn đã bị khóa do nhập sai mật khẩu quá 5 lần liên tiếp.");
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

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          Đăng nhập vào USAS
        </h1>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>
            <p>{error}</p>

            {lockType === "temp_locked" && (
              <p className="mt-1.5 flex items-start gap-1.5 text-xs font-medium">
                <ShieldAlertIcon className="size-3.5 shrink-0 mt-0.5" aria-hidden="true" />
                <span>
                  Chỉ Quản trị viên (Admin) mới có quyền mở khóa tài khoản này. Vui lòng liên hệ ban quản trị để được hỗ trợ.
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

          <Button type="submit" size="xl" disabled={isSubmitting} className="w-full">
            {isSubmitting && <Spinner data-icon="inline-start" />}
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
