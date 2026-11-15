import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { ResetPasswordForm } from "@/components/auth/ResetPasswordForm";

export const metadata: Metadata = {
  title: "Đặt lại mật khẩu – USAS",
  description: "Thiết lập mật khẩu mới bằng mã OTP xác thực",
};

export default function ResetPasswordPage() {
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
        <ResetPasswordForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
