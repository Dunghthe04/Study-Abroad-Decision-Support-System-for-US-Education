import type { Metadata } from "next";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ActAndOpenChat } from "@/components/parent/ActAndOpenChat";

// 1.5e: nút tên con trong chat của phụ huynh trỏ tới đây: vào làm thay con đó rồi mở khung chat
export const metadata: Metadata = {
  title: "Đang mở hồ sơ của con – USAS",
};

interface Props {
  params: Promise<{ childId: string }>;
}

export default async function ParentActPage({ params }: Props) {
  const { childId } = await params;
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <ActAndOpenChat childId={decodeURIComponent(childId)} />
    </ProtectedRoute>
  );
}
