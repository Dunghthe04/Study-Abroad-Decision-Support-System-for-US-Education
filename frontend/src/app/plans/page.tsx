import type { Metadata } from "next";
import { PlansView } from "@/components/billing/PlansView";

export const metadata: Metadata = {
  title: "Gói dịch vụ – USDSS",
  description: "Các gói USDSS Miễn phí, Premium Cá nhân và Gia đình: giá, thời hạn và quyền lợi.",
};

// Trang công khai: khách xem bảng giá, học sinh/phụ huynh đã đăng nhập thấy gói hiện tại và nút mua.
export default function PlansPage() {
  return (
    <div className="pt-8 pb-6 sm:pt-12 lg:pt-16">
      <PlansView />
    </div>
  );
}
