import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { VerifyEmailForm } from "@/components/auth/VerifyEmailForm";

export const metadata: Metadata = {
  title: "Xác thực Email – USAS",
  description: "Xác thực địa chỉ email bằng mã OTP để kích hoạt tài khoản",
};

export default function VerifyEmailPage() {
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
        <VerifyEmailForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
