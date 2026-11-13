import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { VerifyCenterEmailForm } from "@/components/auth/VerifyCenterEmailForm";

export const metadata: Metadata = {
  title: "Xác thực Email Trung tâm – USAS",
  description: "Xác thực địa chỉ email bằng mã OTP để kích hoạt tài khoản đối tác",
};

export default function VerifyCenterEmailPage() {
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
        <VerifyCenterEmailForm />
      </Suspense>
    </AuthSplitLayout>
  );
}

