"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowLeftIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

export function VerifyEmailForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialEmail = searchParams.get("email") || "";

  const { verifyEmail } = useAuth();

  const [email, setEmail] = useState(initialEmail);
  const [otpCode, setOtpCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Bộ đếm gửi lại OTP (cooldown 60s)
  const [cooldown, setCooldown] = useState(60);
  const [isResending, setIsResending] = useState(false);

  useEffect(() => {
    if (cooldown > 0) {
      const timer = setTimeout(() => setCooldown(cooldown - 1), 1000);
      return () => clearTimeout(timer);
    }
  }, [cooldown]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    const cleanOtp = otpCode.trim();
    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email.");
      return;
    }
    if (!cleanOtp || cleanOtp.length !== 6 || !/^\d{6}$/.test(cleanOtp)) {
      setError("Mã OTP phải gồm đúng 6 chữ số.");
      return;
    }

    setIsSubmitting(true);
    try {
      const user = await verifyEmail({ email: email.trim(), otpCode: cleanOtp });

      if (user.role === "center" && user.status === "pending") {
        setSuccessMessage("Xác thực email thành công! Tài khoản đại diện trung tâm của bạn đang chờ Quản trị viên duyệt hồ sơ.");
      } else {
        setSuccessMessage("Kích hoạt tài khoản thành công! Đang chuyển hướng...");
        setTimeout(() => {
          router.push("/account");
        }, 1500);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Mã xác thực không hợp lệ hoặc đã hết hạn.");
      } else {
        setError("Có lỗi kết nối máy chủ. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResendOtp = async () => {
    if (cooldown > 0 || isResending) return;
    setError(null);
    setSuccessMessage(null);

    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email để gửi lại mã.");
      return;
    }

    setIsResending(true);
    try {
      const res = await authApi.resendOtp({
        email: email.trim(),
        purpose: "verify_email",
      });
      setSuccessMessage(res.message || "Mã OTP mới đã được gửi tới email của bạn.");
      setCooldown(60); // Đặt lại bộ đếm 60 giây
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể gửi lại mã lúc này. Vui lòng thử lại sau.");
      } else {
        setError("Có lỗi kết nối máy chủ khi gửi lại mã OTP.");
      }
    } finally {
      setIsResending(false);
    }
  };

  return (
    <Card className="mx-auto w-full max-w-md">
      <CardHeader className="text-center">
        <CardTitle>
          <h1>Xác thực Email</h1>
        </CardTitle>
        <CardDescription>
          Vui lòng nhập mã OTP gồm 6 chữ số vừa được gửi đến hòm thư của bạn để kích hoạt tài khoản.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-6">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {successMessage && (
          <Alert variant="success" role="status">
            <AlertDescription>{successMessage}</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit}>
          <FieldGroup>
            {/* Email */}
            <Field>
              <FieldLabel htmlFor="verify-email-input">Địa chỉ Email</FieldLabel>
              <Input
                id="verify-email-input"
                type="email"
                required
                maxLength={100}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="example@gmail.com"
              />
            </Field>

            {/* Mã OTP */}
            <Field>
              <div className="flex items-center justify-between">
                <FieldLabel htmlFor="otp-input">Mã xác thực OTP (6 chữ số)</FieldLabel>
                <Badge variant="warn">Hiệu lực trong 5 phút</Badge>
              </div>
              <Input
                id="otp-input"
                type="text"
                required
                maxLength={6}
                pattern="[0-9]*"
                inputMode="numeric"
                autoComplete="one-time-code"
                value={otpCode}
                onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                placeholder="123456"
                className="text-center font-mono tracking-widest"
              />
              <FieldDescription>
                Mã sẽ tự hủy sau 5 lần nhập sai. Tối đa 5 lần gửi mã mỗi giờ.
              </FieldDescription>
            </Field>

            <Button type="submit" size="lg" disabled={isSubmitting || otpCode.length !== 6} className="w-full">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang xác thực..." : "Kích hoạt tài khoản"}
            </Button>
          </FieldGroup>
        </form>

        {/* Gửi lại OTP */}
        <div className="flex flex-col items-center gap-2 text-muted-foreground">
          <div>
            Chưa nhận được mã?{" "}
            <Button
              type="button"
              variant="link"
              onClick={handleResendOtp}
              disabled={cooldown > 0 || isResending}
            >
              {cooldown > 0 ? `Gửi lại sau (${cooldown}s)` : isResending ? "Đang gửi..." : "Gửi lại mã OTP"}
            </Button>
          </div>
          <div>
            <Link href="/login" className={buttonVariants({ variant: "ghost", size: "sm" })}>
              <ArrowLeftIcon aria-hidden="true" />
              Quay lại đăng nhập
            </Link>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
