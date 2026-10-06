import type { Metadata } from "next";
import { Suspense } from "react";
import { ResetPasswordForm } from "@/components/auth/ResetPasswordForm";

export const metadata: Metadata = {
  title: "Đặt lại mật khẩu – USAS",
  description: "Thiết lập mật khẩu mới bằng mã OTP xác thực",
};

export default function ResetPasswordPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <Suspense fallback={<div className="text-center text-sm text-slate-500">Đang tải...</div>}>
        <ResetPasswordForm />
      </Suspense>
    </div>
  );
}
