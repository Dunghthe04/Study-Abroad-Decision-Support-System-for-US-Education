import type { Metadata } from "next";
import Link from "next/link";
import { ArrowLeftIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

export const metadata: Metadata = {
  title: "Điều khoản sử dụng – USDSS",
  description: "Điều khoản sử dụng hệ thống hỗ trợ ra quyết định du học Mỹ USAS (USDSS)",
};

export default function TermsPage() {
  return (
    <main className="min-h-screen bg-surface px-4 py-12 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-3xl space-y-8">
        <Link href="/register" className={buttonVariants({ variant: "ghost", size: "sm" })}>
          <ArrowLeftIcon className="mr-1.5 size-4" /> Quay lại đăng ký
        </Link>

        <div className="space-y-3">
          <h1 className="font-heading text-3xl font-bold tracking-tight text-ink">
            Điều khoản sử dụng
          </h1>
          <p className="text-sm text-ink-3">Cập nhật lần cuối: Tháng 11/2026</p>
        </div>

        <div className="prose prose-slate max-w-none space-y-6 text-sm leading-relaxed text-ink-2">
          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">1. Giới thiệu</h2>
            <p>
              Chào mừng bạn đến với Hệ thống Hỗ trợ Ra Quyết định Du học Mỹ (USAS / USDSS). Khi đăng ký tài khoản và sử dụng dịch vụ của chúng tôi, bạn đồng ý tuân thủ các điều khoản và điều kiện được nêu tại đây.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">2. Quyền và trách nhiệm của người dùng</h2>
            <p>
              - Bạn cam kết cung cấp thông tin học tập, hồ sơ cá nhân và tài chính chính xác, trung thực.
            </p>
            <p>
              - Bạn có trách nhiệm bảo mật thông tin đăng nhập và mật khẩu tài khoản cá nhân.
            </p>
            <p>
              - Đối với học sinh dưới 16 tuổi, hệ thống yêu cầu sự đồng ý từ cha mẹ hoặc người giám hộ hợp pháp theo quy định của pháp luật.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">3. Khuyến nghị và tư vấn AI</h2>
            <p>
              Các gợi ý trường, phân tích học thuật, dự báo khả năng trúng tuyển và tư vấn từ trợ lý AI mang tính chất định hướng và hỗ trợ ra quyết định. Quyết định nộp hồ sơ và ứng tuyển cuối cùng thuộc về người dùng.
            </p>
          </section>

          <section className="space-y-2">
            <h2 className="text-base font-semibold text-ink">4. Giới hạn trách nhiệm</h2>
            <p>
              Hệ thống không can thiệp vào quy trình xét tuyển của các trường đại học tại Mỹ và không cam kết kết quả trúng tuyển thực tế của bất kỳ cơ sở đào tạo nào.
            </p>
          </section>
        </div>
      </div>
    </main>
  );
}
