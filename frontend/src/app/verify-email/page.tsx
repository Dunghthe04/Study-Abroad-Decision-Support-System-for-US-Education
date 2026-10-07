import type { Metadata } from "next";
import { Suspense } from "react";
import { VerifyEmailForm } from "@/components/auth/VerifyEmailForm";

export const metadata: Metadata = {
  title: "Xác thực Email – USAS",
  description: "Xác thực địa chỉ email bằng mã OTP để kích hoạt tài khoản",
};

export default function VerifyEmailPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <Suspense fallback={<div className="text-center text-sm text-slate-500">Đang tải...</div>}>
        <VerifyEmailForm />
      </Suspense>
    </div>
  );
}
