import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { ForgotPasswordForm } from "@/components/auth/ForgotPasswordForm";

export const metadata: Metadata = {
  title: "Quên mật khẩu – USAS",
  description: "Yêu cầu mã xác thực để đặt lại mật khẩu tài khoản",
};

export default function ForgotPasswordPage() {
  return (
    <AuthSplitLayout>
      <Suspense
        fallback={
          <div className="flex items-center gap-2 text-sm text-ink-2">
            <Spinner />
            Đang tải...
          </div>
        }
      >
        <ForgotPasswordForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
