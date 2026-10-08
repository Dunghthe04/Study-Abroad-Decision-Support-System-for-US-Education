"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowRightIcon, HourglassIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirect = searchParams.get("redirect") || "/account";

  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [lockType, setLockType] = useState<"temp_locked" | "unverified" | null>(null);

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
      await login({ email: email.trim(), password });
      router.push(redirect);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Sai tài khoản hoặc mật khẩu.");
        if (err.status === 423) {
          setLockType("temp_locked");
        } else if (err.status === 403 && (err.message.includes("chưa được kích hoạt") || err.message.toLowerCase().includes("unverified"))) {
          setLockType("unverified");
        }
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="mx-auto w-full max-w-md">
      <CardHeader className="text-center">
        <CardTitle>
          <h1>Đăng nhập</h1>
        </CardTitle>
        <CardDescription>
          Truy cập tài khoản học sinh / phụ huynh để quản lý thông tin
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>
              <p>{error}</p>

              {lockType === "temp_locked" && (
                <p>
                  <span className="inline-flex items-center gap-1 font-medium">
                    <HourglassIcon className="size-3.5" aria-hidden="true" />
                    Tự động mở khóa:
                  </span> Tài khoản của bạn sẽ được hệ thống tự động mở khóa sau 15 phút. Bạn không cần thực hiện thêm thao tác nào, vui lòng quay lại sau thời gian trên.
                </p>
              )}
            </AlertDescription>
          </Alert>
        )}

        {error && lockType === "unverified" && (
          <Link
            href={`/verify-email?email=${encodeURIComponent(email.trim())}`}
            className={buttonVariants({ size: "sm" })}
          >
            Kích hoạt tài khoản bằng mã OTP
            <ArrowRightIcon aria-hidden="true" />
          </Link>
        )}

        <form onSubmit={handleSubmit}>
          <FieldGroup>
            {/* Email */}
            <Field>
              <FieldLabel htmlFor="email">Email</FieldLabel>
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
              <div className="flex items-center justify-between">
                <FieldLabel htmlFor="password">Mật khẩu</FieldLabel>
                <Link
                  href="/forgot-password"
                  className="text-primary hover:underline"
                >
                  Quên mật khẩu?
                </Link>
              </div>
              <Input
                id="password"
                type="password"
                required
                maxLength={100}
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
              />
            </Field>

            <Button type="submit" size="lg" disabled={isSubmitting} className="w-full">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang đăng nhập..." : "Đăng nhập"}
            </Button>
          </FieldGroup>
        </form>

        <p className="text-center text-muted-foreground">
          Chưa có tài khoản?{" "}
          <Link href="/register" className="text-primary hover:underline">
            Đăng ký ngay
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}
