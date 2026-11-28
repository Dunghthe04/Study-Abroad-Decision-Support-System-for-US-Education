import type { Metadata } from "next";
import { Suspense } from "react";
import { AuditUniversitiesWorkbench } from "@/components/audit-universities/AuditUniversitiesWorkbench";

export const metadata: Metadata = {
  title: "Universities UI Audit Workbench – USDSS",
  description: "Temporary preview workbench for core university pages and UI states.",
};

export default function AuditUniversitiesPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center text-sm text-ink-3">
          Đang tải Universities UI Audit...
        </div>
      }
    >
      <AuditUniversitiesWorkbench />
    </Suspense>
  );
}
