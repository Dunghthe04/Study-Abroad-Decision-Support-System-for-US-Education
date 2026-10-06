import type { Metadata } from "next";
import { Suspense } from "react";
import { UnlockAccountForm } from "@/components/auth/UnlockAccountForm";

export const metadata: Metadata = {
  title: "Mở khóa tài khoản – USAS",
  description: "Mở khóa tài khoản tạm thời bằng mã OTP gửi về email",
};

export default function UnlockAccountPage() {
  return (
    <div className="flex min-h-[calc(100vh-12rem)] items-center justify-center py-6">
      <Suspense fallback={<div className="text-center text-sm text-slate-500">Đang tải...</div>}>
        <UnlockAccountForm />
      </Suspense>
    </div>
  );
}
