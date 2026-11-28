import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { ParentRestoreForm } from "@/components/auth/ParentRestoreForm";

export const metadata: Metadata = {
  title: "Khôi phục tài khoản – USDSS",
  description: "Người đại diện hủy yêu cầu xóa tài khoản của con bằng nút trong email, không cần đăng nhập",
};

// [UC-75 4b] Mở từ hai nút trong mail xác nhận người đại diện yêu cầu xóa tài khoản của con
export default function ParentRestorePage() {
  return (
    <AuthSplitLayout containerClassName="max-w-[540px]">
      <Suspense
        fallback={
          <div className="flex items-center gap-2 text-sm text-ink-2">
            <Spinner />
            Đang tải...
          </div>
        }
      >
        <ParentRestoreForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
