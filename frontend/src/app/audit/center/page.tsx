import type { Metadata } from "next";
import { Suspense } from "react";
import { AuditCenterWorkbench } from "@/components/audit-center/AuditCenterWorkbench";

export const metadata: Metadata = {
  title: "Center UI Audit Workbench – USDSS",
  description: "Temporary preview workbench for all center-related pages and UI states.",
};

export default function AuditCenterPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center text-sm text-ink-3">
          Đang tải Center UI Audit...
        </div>
      }
    >
      <AuditCenterWorkbench />
    </Suspense>
  );
}
