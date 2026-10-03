import { Suspense } from "react";
import { LoginForm } from "@/components/auth/LoginForm";

export const metadata = {
  title: "Đăng nhập – USAS",
  description: "Đăng nhập hệ thống hỗ trợ ra quyết định du học Mỹ",
};

export default function LoginPage() {
  return (
    <main className="flex min-h-[calc(100vh-140px)] items-center justify-center py-10 px-4">
      <Suspense
        fallback={
          <div className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-8 text-center text-slate-500 shadow-sm">
            <span className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-slate-900 border-t-transparent" />
            <p className="mt-2 text-sm">Đang tải...</p>
          </div>
        }
      >
        <LoginForm />
      </Suspense>
    </main>
  );
}
