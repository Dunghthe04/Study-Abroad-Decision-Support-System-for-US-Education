import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { ForgotPasswordForm } from "@/components/auth/ForgotPasswordForm";

export const metadata: Metadata = {
  title: "Quên mật khẩu – USAS",
  description: "Yêu cầu mã xác thực để đặt lại mật khẩu tài khoản",
};

export default function ForgotPasswordPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <Suspense
        fallback={
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Spinner />
            Đang tải...
          </div>
        }
      >
        <ForgotPasswordForm />
      </Suspense>
    </div>
  );
}
