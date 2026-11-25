import type { Metadata } from "next";
import { Suspense } from "react";
import { AgeGate } from "@/components/auth/AgeGate";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { Spinner } from "@/components/ui/spinner";
import { ChildParentPreferenceView } from "@/components/parent/ChildParentPreferenceView";

export const metadata: Metadata = {
  title: "So với cha mẹ – USDSS",
  description: "Xem danh sách trường theo ưu tiên của cha mẹ, đặt cạnh danh sách của bạn",
};

// UC-76 (2e): trang của con, chỉ đọc
export default function ParentPreferencesPage() {
  return (
    <ProtectedRoute allowedRoles={["student"]}>
      <AgeGate feature="process_student_data">
        <Suspense
          fallback={
            <div className="flex min-h-[50vh] items-center justify-center">
              <Spinner className="size-8 text-brand" />
            </div>
          }
        >
          <ChildParentPreferenceView />
        </Suspense>
      </AgeGate>
    </ProtectedRoute>
  );
}
