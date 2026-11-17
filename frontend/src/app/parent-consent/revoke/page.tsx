import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { ParentRevokeForm } from "@/components/auth/ParentRevokeForm";

export const metadata: Metadata = {
  title: "Phụ huynh rút đồng ý – USAS",
  description: "Phụ huynh rút lại sự đồng ý cho hệ thống xử lý dữ liệu của con, không cần đăng nhập",
};

// [UC-117] Mở từ link trong mail xác nhận gửi phụ huynh
export default function ParentRevokePage() {
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
        <ParentRevokeForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
