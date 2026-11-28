import type { Metadata } from "next";
import { Suspense } from "react";
import { AuditCommunityWorkbench } from "@/components/audit-community/AuditCommunityWorkbench";

export const metadata: Metadata = {
  title: "Community UI Audit Workbench – USDSS",
  description: "Temporary preview workbench for community chat and forum moderation pages.",
};

export default function AuditCommunityPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center text-sm text-ink-3">
          Đang tải Community UI Audit...
        </div>
      }
    >
      <AuditCommunityWorkbench />
    </Suspense>
  );
}
