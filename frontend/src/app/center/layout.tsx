import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { CenterStatusBanner } from "@/components/center/CenterStatusBanner";
import { CenterNav } from "@/components/center/CenterNav";

export const metadata: Metadata = {
  title: "Cổng đối tác Trung tâm tư vấn – USAS",
  description: "Bảng điều khiển và quản lý hồ sơ đối tác trung tâm tư vấn du học Mỹ",
};

export default function CenterLayout({ children }: { children: React.ReactNode }) {
  return (
    <ProtectedRoute allowedRoles={["center", "moderator", "admin"]}>
      <div className="min-h-screen bg-surface-2 flex flex-col">
        <CenterStatusBanner />
        <CenterNav />
        <main className="flex-1 container mx-auto px-4 sm:px-6 py-6">
          {children}
        </main>
      </div>
    </ProtectedRoute>
  );
}

