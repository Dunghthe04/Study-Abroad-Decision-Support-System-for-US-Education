import type { Metadata } from "next";
import { Suspense } from "react";
import { ForgotPasswordForm } from "@/components/auth/ForgotPasswordForm";

export const metadata: Metadata = {
  title: "Quên mật khẩu – USAS",
  description: "Yêu cầu mã xác thực để đặt lại mật khẩu tài khoản",
};

export default function ForgotPasswordPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <Suspense fallback={<div className="text-center text-sm text-slate-500">Đang tải...</div>}>
        <ForgotPasswordForm />
      </Suspense>
    </div>
  );
}
