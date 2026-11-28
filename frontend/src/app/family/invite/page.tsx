import type { Metadata } from "next";
import { Suspense } from "react";
import { Spinner } from "@/components/ui/spinner";
import { AuthSplitLayout } from "@/components/auth/AuthSplitLayout";
import { FamilyInviteForm } from "@/components/billing/FamilyInviteForm";

export const metadata: Metadata = {
  title: "Lời mời gia đình – USDSS",
  description: "Chấp nhận hoặc từ chối lời mời tham gia gói USDSS Premium Gia đình",
};

export default function FamilyInvitePage() {
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
        <FamilyInviteForm />
      </Suspense>
    </AuthSplitLayout>
  );
}
