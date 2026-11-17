import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { LeadParentConfirmForm } from "@/components/auth/LeadParentConfirmForm";

export const metadata: Metadata = {
  title: "Phụ huynh xác nhận yêu cầu liên hệ – USAS",
  description: "Phụ huynh đồng ý hoặc từ chối yêu cầu liên hệ trung tâm du học của con dưới 16 tuổi",
};

// [UC-116] Link trong mail: /parent-consent/lead?token=...(&choice=reject)
export default function LeadParentConfirmPage() {
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
        <LeadParentConfirmForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
