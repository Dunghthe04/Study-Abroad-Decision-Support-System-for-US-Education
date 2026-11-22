import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeftIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

export const metadata: Metadata = {
  title: "Chính sách bảo mật – USDSS",
  description: "Chính sách quyền riêng tư và bảo vệ dữ liệu cá nhân của hệ thống USAS (USDSS)",
};

export default function PrivacyPage() {
  return (
    <main className="min-h-screen bg-surface px-4 py-12 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-3xl space-y-8">
        <Link href="/register" className={buttonVariants({ variant: "ghost", size: "sm" })}>
          <ArrowLeftIcon className="mr-1.5 size-4" /> Quay lại đăng ký
        </Link>

        <div className="space-y-3">
          <h1 className="font-heading text-3xl font-bold tracking-tight text-ink">
            Chính sách bảo mật & Quyền riêng tư
          </h1>
          <p className="text-sm text-ink-3">Cập nhật lần cuối: Tháng 11/2026</p>
        </div>

        <div className="prose prose-slate max-w-none space-y-6 text-sm leading-relaxed text-ink-2">
          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">1. Thu thập dữ liệu</h2>
            <p>
              Chúng tôi chỉ thu thập các thông tin cần thiết phục vụ việc tư vấn du học, bao gồm: họ tên, ngày sinh, email, số điện thoại, điểm số học tập (GPA, chứng chỉ tiếng Anh, SAT/ACT) và ngân sách dự kiến của gia đình.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">2. Bảo vệ dữ liệu trẻ vị thành niên</h2>
            <p>
              Tuân thủ Nghị định 13/2023/NĐ-CP về Bảo vệ dữ liệu cá nhân, đối với dữ liệu của học sinh dưới 16 tuổi, chúng tôi bắt buộc phải có sự đồng ý của cha mẹ hoặc người giám hộ hợp pháp trước khi kích hoạt các tính năng phân tích chuyên sâu.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">3. Sử dụng và chia sẻ thông tin</h2>
            <p>
              - Dữ liệu của bạn chỉ được sử dụng cho thuật toán gợi ý trường và hỗ trợ chuyên gia/AI tư vấn.
            </p>
            <p>
              - Chúng tôi tuyệt đối không bán dữ liệu cá nhân cho bên thứ ba. Hồ sơ chỉ được chia sẻ tới trung tâm tư vấn khi bạn chủ động gửi yêu cầu kết nối.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">4. Quyền của người dùng với dữ liệu</h2>
            <p>
              Bạn có toàn quyền xem, chỉnh sửa, yêu cầu trích xuất hoặc xóa vĩnh viễn tài khoản và dữ liệu cá nhân của mình bất kỳ lúc nào trong phần Cài đặt tài khoản.
            </p>
          </section>
        </div>
      </div>
    </main>
  );
}
