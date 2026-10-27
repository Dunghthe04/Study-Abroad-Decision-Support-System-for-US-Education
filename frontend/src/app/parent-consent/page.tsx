import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { ParentConsentForm } from "@/components/auth/ParentConsentForm";

export const metadata: Metadata = {
  title: "Phụ huynh xác nhận đồng ý – USAS",
  description: "Phụ huynh xác nhận liên kết tài khoản và đồng ý cho xử lý dữ liệu cá nhân của con",
};

export default function ParentConsentPage() {
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
        <ParentConsentForm />
      </Suspense>
    </div>
  );
}
