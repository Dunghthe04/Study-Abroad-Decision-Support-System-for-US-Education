import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { ResetPasswordForm } from "@/components/auth/ResetPasswordForm";

export const metadata: Metadata = {
  title: "Đặt lại mật khẩu – USAS",
  description: "Thiết lập mật khẩu mới bằng mã OTP xác thực",
};

export default function ResetPasswordPage() {
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
        <ResetPasswordForm />
      </Suspense>
    </div>
  );
}
