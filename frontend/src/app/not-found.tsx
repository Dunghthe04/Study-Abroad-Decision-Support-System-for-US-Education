import type { Metadata } from "next";
import { ErrorFallback } from "@/components/ui/error-fallback";

export const metadata: Metadata = {
  title: "Không tìm thấy trang – USAS",
  description: "Trang bạn đang tìm kiếm không tồn tại hoặc đã bị di chuyển.",
};

export default function RootNotFound() {
  return (
    <ErrorFallback
      title="Không tìm thấy trang"
      description="Trang bạn đang tìm kiếm không tồn tại hoặc đã bị di chuyển. Vui lòng kiểm tra lại đường dẫn."
      action={{
        label: "Về trang chủ",
        href: "/",
      }}
    />
  );
}
