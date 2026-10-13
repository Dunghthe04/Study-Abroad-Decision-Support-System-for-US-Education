"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { authApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

export function ForgotPasswordForm() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccessMessage(null);

    if (!email.trim()) {
      setError("Vui lòng nhập địa chỉ email của bạn.");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await authApi.forgotPassword({ email: email.trim() });
      setSuccessMessage(res.message || "Mã xác thực đặt lại mật khẩu đã được gửi đến email của bạn.");
      setTimeout(() => {
        router.push(`/reset-password?email=${encodeURIComponent(email.trim())}`);
      }, 1500);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Không thể gửi yêu cầu đặt lại mật khẩu. Vui lòng thử lại sau.");
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
          <h1>Quên mật khẩu?</h1>
        </CardTitle>
        <CardDescription>
          Nhập địa chỉ email đã đăng ký. Hệ thống sẽ gửi mã OTP gồm 6 chữ số để bạn thiết lập mật khẩu mới.
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
            <Field>
              <FieldLabel htmlFor="forgot-email-input">Địa chỉ Email</FieldLabel>
              <Input
                id="forgot-email-input"
                type="email"
                required
                maxLength={100}
                autoComplete="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="example@gmail.com"
              />
            </Field>

            <Button type="submit" size="lg" disabled={isSubmitting} className="w-full">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang gửi mã..." : "Gửi mã OTP xác nhận"}
            </Button>
          </FieldGroup>
        </form>

        <p className="text-center text-muted-foreground">
          Đã nhớ lại mật khẩu?{" "}
          <Link href="/login" className="text-primary hover:underline">
            Đăng nhập ngay
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}
