import Link from "next/link";
import { ArrowRightIcon, BookOpenIcon, CompassIcon, MessageSquareIcon, SparklesIcon, UserIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

const features = [
  {
    href: "/analysis",
    title: "Phân tích tư vấn (6 bước)",
    icon: CompassIcon,
    highlight: true,
    description: "Nhập hồ sơ một luồng duy nhất, hỗ trợ tải ảnh học bạ tự động đọc điểm OCR và nhận gợi ý 4 nhóm trường.",
  },
  {
    href: "/methodology",
    title: "Giới thiệu phương pháp",
    icon: BookOpenIcon,
    highlight: false,
    description: "Mô hình tuyển sinh toàn diện (Holistic Review), thuật toán AHP & SAW phân loại Reach / Match / Safety minh bạch.",
  },
  {
    href: "/advisor",
    title: "Chat AI tư vấn",
    icon: MessageSquareIcon,
    highlight: false,
    description: "Hỏi về visa F-1, hồ sơ, chi phí, học bổng cho mọi bậc học. Câu trả lời chính xác có trích nguồn dữ liệu.",
  },
  {
    href: "/account",
    title: "Hồ sơ cá nhân",
    icon: UserIcon,
    highlight: false,
    description: "Quản lý thông tin tài khoản, xem lại lộ trình và bảo mật thông tin hồ sơ du học an toàn.",
  },
];

export default function HomePage() {
  return (
    <section className="space-y-10">
      <div className="relative overflow-hidden rounded-2xl border border-line bg-gradient-to-b from-brand-wash/30 via-surface to-surface p-8 sm:p-12">
        <div className="max-w-3xl space-y-4">
          <div className="inline-flex items-center gap-2 rounded-full border border-brand/20 bg-brand-wash px-3 py-1 text-xs font-semibold text-brand">
            <SparklesIcon className="size-3.5" />
            Hệ thống hỗ trợ ra quyết định du học Mỹ thông minh
          </div>
          <h1 className="text-display text-brand-ink">
            Study Abroad Decision Support System
          </h1>
          <p className="text-body text-muted-foreground text-base sm:text-lg leading-relaxed">
            Nền tảng hỗ trợ học sinh, sinh viên và phụ huynh ra quyết định du học Mỹ từ THPT, đại học
            đến thạc sĩ, tiến sĩ với thuật toán đa tiêu chí khoa học và AI có kiểm soát.
          </p>
          <div className="flex flex-wrap items-center gap-3 pt-2">
            <Link
              href="/analysis"
              className={buttonVariants({ variant: "default", size: "lg" })}
            >
              Bắt đầu phân tích hồ sơ
              <ArrowRightIcon className="size-4 ml-1.5" />
            </Link>
            <Link
              href="/methodology"
              className={buttonVariants({ variant: "outline", size: "lg" })}
            >
              Tìm hiểu phương pháp
            </Link>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        <h2 className="text-h2 text-foreground">Tính năng chính</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {features.map((f) => {
            const Icon = f.icon;
            return (
              <Link key={f.href} href={f.href} className="block group">
                <Card className={`h-full transition-all group-hover:border-brand/50 group-hover:shadow-sm ${f.highlight ? "border-brand/40 bg-brand-wash/15 ring-1 ring-brand/20" : ""}`}>
                  <CardHeader>
                    <div className="mb-2 flex size-9 items-center justify-center rounded-lg bg-brand-wash text-brand transition-colors group-hover:bg-brand group-hover:text-white">
                      <Icon className="size-4.5" />
                    </div>
                    <CardTitle>
                      <h3 className="text-h3 text-foreground group-hover:text-brand transition-colors">{f.title}</h3>
                    </CardTitle>
                    <CardDescription className="text-sm leading-relaxed">{f.description}</CardDescription>
                  </CardHeader>
                </Card>
              </Link>
            );
          })}
        </div>
      </div>
    </section>
  );
}

