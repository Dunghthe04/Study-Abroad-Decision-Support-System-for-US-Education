import type { Metadata } from "next";
import { PlansView } from "@/components/billing/PlansView";

export const metadata: Metadata = {
  title: "Gói dịch vụ – USAS",
  description: "Các gói USDSS Premium Cá nhân và Gia đình: giá, thời hạn và số câu hỏi ước tính.",
};

// Trang công khai: khách xem bảng giá, học sinh/phụ huynh đã đăng nhập thấy gói hiện tại và nút mua.
export default function PlansPage() {
  return (
    <div className="py-6">
      <PlansView />
    </div>
  );
}
