import type { Metadata } from "next";
import Link from "next/link";
import { SparklesIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

export const metadata: Metadata = {
  title: "Phân tích hồ sơ du học Mỹ – USAS",
  description: "Luồng phân tích hồ sơ du học 6 bước toàn diện: mục tiêu, bảng điểm OCR, chứng chỉ, tài chính, ngoại khóa và gợi ý trường.",
};

export default function AnalysisPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-8 py-8">
      <div className="space-y-3">
        <div className="inline-flex items-center gap-1.5 rounded-full bg-brand-wash px-3 py-1 text-xs font-semibold text-brand">
          <SparklesIcon className="size-3.5" />
          Luồng phân tích hồ sơ tập trung 6 bước
        </div>
        <h1 className="text-display text-brand-ink">
          Phân tích hồ sơ & Gợi ý trường du học Mỹ
        </h1>
        <p className="text-body text-muted-foreground max-w-2xl">
          Chào mừng bạn đến với quy trình phân tích hồ sơ toàn diện. Hệ thống sẽ đồng hành cùng bạn qua
          6 bước trực quan để xây dựng bức tranh năng lực và gợi ý các trường đại học Mỹ phù hợp nhất.
        </p>
      </div>

      <div className="rounded-2xl border border-line bg-surface p-6 sm:p-8 shadow-xs">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-h2 text-foreground">Bắt đầu quy trình 6 bước</h2>
            <p className="text-sm text-muted-foreground">
              Tự động lưu nháp, hỗ trợ tải ảnh học bạ để đọc điểm tự động và nhận kết quả tức thì.
            </p>
          </div>
          <Link
            href="/methodology"
            className={buttonVariants({ variant: "outline" })}
          >
            Xem phương pháp đánh giá
          </Link>
        </div>
      </div>
    </div>
  );
}
