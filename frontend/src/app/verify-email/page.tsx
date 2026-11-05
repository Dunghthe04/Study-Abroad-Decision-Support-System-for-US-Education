import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { VerifyEmailForm } from "@/components/auth/VerifyEmailForm";

export const metadata: Metadata = {
  title: "Xác thực Email – USAS",
  description: "Xác thực địa chỉ email bằng mã OTP để kích hoạt tài khoản",
};

export default function VerifyEmailPage() {
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
        <VerifyEmailForm />
      </Suspense>
    </div>
  );
}
