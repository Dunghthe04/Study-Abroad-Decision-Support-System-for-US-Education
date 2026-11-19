import type { Metadata } from "next";
import Link from "next/link";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { AddManagedChildButton } from "@/components/parent/ManagedChildActions";
import { buttonVariants } from "@/components/ui/button";

// 1.5e: nút "Thêm con" trong chat của phụ huynh trỏ tới đây. Mở sẵn hộp thêm con; thêm xong vào làm thay
// con mới rồi mở khung chat để chat hỏi hồ sơ của con. Đường tĩnh "add" được ưu tiên hơn [studentId].
export const metadata: Metadata = {
  title: "Thêm con – USAS",
};

export default function AddChildPage() {
  return (
    <ProtectedRoute allowedRoles={["parent"]}>
      <div className="mx-auto max-w-md space-y-4 px-4 py-12">
        <h1 className="text-xl font-semibold">Thêm con chưa có tài khoản</h1>
        <p className="text-muted-foreground">
          Thêm con để nhập hồ sơ và chat thay con. Thêm xong, khung chat mở lại để hỏi hồ sơ của con.
        </p>
        <div className="flex flex-wrap gap-2">
          <AddManagedChildButton variant="default" defaultOpen next="/advisor" />
          <Link href="/parent/children" className={buttonVariants({ variant: "outline" })}>
            Về Con của tôi
          </Link>
        </div>
      </div>
    </ProtectedRoute>
  );
}
