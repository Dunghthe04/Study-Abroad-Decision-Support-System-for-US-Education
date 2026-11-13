"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { MailCheckIcon, ShieldAlertIcon } from "lucide-react";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { clearPendingEmail, usePendingEmail } from "@/lib/pending-email";

export function VerifyCenterEmailForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const urlEmail = searchParams.get("email");

  const pendingEmail = usePendingEmail();
  const [typedEmail, setEmail] = useState<string | null>(null);
  const email = typedEmail ?? urlEmail ?? pendingEmail ?? "";
  const [otpCode, setOtpCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [cooldown, setCooldown] = useState(60);
  const [isResending, setIsResending] = useState(false);

  useEffect(() => {
    if (cooldown > 0) {
      const timer = setTimeout(() => setCooldown((c) => c - 1), 1000);
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
      await authApi.verifyCenterEmail({ email: email.trim(), code: cleanOtp });
      clearPendingEmail();
      setSuccessMessage("Xác thực email thành công! Đang chuyển hướng đến Cổng đối tác...");
      setTimeout(() => {
        router.push("/center");
      }, 1000);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Mã OTP không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResend = async () => {
    if (cooldown > 0 || isResending || !email.trim()) return;

    setError(null);
    setSuccessMessage(null);
    setIsResending(true);
    try {
      const res = await authApi.resendOtp({ email: email.trim(), purpose: "verify_email" });
      setSuccessMessage(res.message ?? "Đã gửi lại mã OTP vào hộp thư.");
      setCooldown(60);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Không thể gửi lại mã OTP lúc này. Vui lòng thử lại sau.");
      }
    } finally {
      setIsResending(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2 text-center">
        <div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-brand/10 text-brand">
          <MailCheckIcon className="h-6 w-6" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-ink">
          Xác thực Email trung tâm
        </h1>
        <p className="text-sm text-ink-2">
          Mã xác thực gồm 6 chữ số đã được gửi đến hộp thư của bạn. Vui lòng nhập mã để kích hoạt tài khoản.
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {successMessage && (
        <Alert className="border-brand/30 bg-brand/5 text-brand">
          <AlertDescription>{successMessage}</AlertDescription>
        </Alert>
      )}

      <form onSubmit={handleSubmit}>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="email">Email tài khoản</FieldLabel>
            <Input
              id="email"
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="example@gmail.com"
            />
          </Field>

          <Field>
            <FieldLabel htmlFor="otp">Mã xác thực OTP (6 chữ số)</FieldLabel>
            <Input
              id="otp"
              type="text"
              inputMode="numeric"
              pattern="\d{6}"
              maxLength={6}
              required
              autoFocus
              autoComplete="one-time-code"
              value={otpCode}
              onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
              placeholder="123456"
              className="text-center font-mono text-xl tracking-[0.4em]"
            />
            <FieldDescription>Mã có hiệu lực trong vòng 5 phút</FieldDescription>
          </Field>

          <Button type="submit" size="xl" disabled={isSubmitting} className="w-full">
            {isSubmitting && <Spinner data-icon="inline-start" />}
            {isSubmitting ? "Đang xác thực..." : "Kích hoạt tài khoản"}
          </Button>
        </FieldGroup>
      </form>

      <div className="flex items-center justify-between text-sm text-ink-2 pt-2">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={cooldown > 0 || isResending}
          onClick={handleResend}
          className="text-brand hover:text-brand-hover p-0 h-auto"
        >
          {isResending ? (
            <span className="flex items-center gap-1.5">
              <Spinner /> Đang gửi...
            </span>
          ) : cooldown > 0 ? (
            `Gửi lại mã sau ${cooldown}s`
          ) : (
            "Gửi lại mã OTP"
          )}
        </Button>

        <Link href="/login" className="text-ink-3 hover:text-ink hover:underline">
          Quay lại đăng nhập
        </Link>
      </div>

      <div className="rounded-lg border border-line bg-surface-2 p-3 text-xs text-ink-3 flex items-start gap-2">
        <ShieldAlertIcon className="h-4 w-4 text-brand shrink-0 mt-0.5" />
        <span>
          Lưu ý: Kích hoạt email giúp tài khoản chuyển sang trạng thái Hoạt động. Sau khi đăng nhập, trung tâm nộp hồ sơ pháp lý để được Moderator xét duyệt.
        </span>
      </div>
    </div>
  );
}

