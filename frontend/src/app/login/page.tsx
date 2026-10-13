import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { LoginForm } from "@/components/auth/LoginForm";

export const metadata: Metadata = {
  title: "Đăng nhập – USAS",
  description: "Đăng nhập vào tài khoản học sinh hoặc phụ huynh",
};

export default function LoginPage() {
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
        <LoginForm />
      </Suspense>
    </div>
  );
}
