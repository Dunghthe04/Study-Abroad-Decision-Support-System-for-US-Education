import Image from "next/image";
import Link from "next/link";
import { cn } from "@/lib/utils";

const COLUMNS: { title: string; links: { href: string; label: string }[] }[] = [
  {
    title: "Công cụ",
    links: [
      { href: "/analysis", label: "Phân tích hồ sơ" },
      { href: "/recommendations", label: "Trường gợi ý" },
      { href: "/target-schools", label: "Trường mục tiêu" },
      { href: "/costs", label: "Tra cứu chi phí theo trường" },
      { href: "/costs?mode=state", label: "Tra cứu chi phí theo bang" },
      { href: "/advisor", label: "Trợ lý AI" },
    ],
  },
  {
    title: "Khám phá",
    links: [
      { href: "/universities", label: "Tra cứu trường Mỹ" },
      { href: "/scholarships", label: "Học bổng" },
      { href: "/centers", label: "Trung tâm tư vấn" },
      { href: "/community", label: "Cộng đồng" },
      { href: "/methodology", label: "Giới thiệu phương pháp" },
      { href: "/plans", label: "Gói dịch vụ" },
    ],
  },
  {
    title: "Tài khoản & Pháp lý",
    links: [
      { href: "/account", label: "Tài khoản của tôi" },
      { href: "/login", label: "Đăng nhập" },
      { href: "/register", label: "Đăng ký" },
      { href: "/terms", label: "Điều khoản sử dụng" },
      { href: "/privacy", label: "Chính sách bảo mật" },
    ],
  },
];

export interface FooterBarProps {
  /** `default` cho các trang khách hàng thông thường; `compact` cho khung hẹp cần chân trang gọn. */
  variant?: "default" | "compact";
  className?: string;
}

/** Chân trang tái sử dụng cho các tuyến khách hàng, dùng màu theo token của hệ thống */
export function FooterBar({ variant = "default", className }: FooterBarProps) {
  if (variant === "compact") {
    return (
      <footer className={cn("border-t border-line bg-surface text-ink", className)}>
        <div className="mx-auto flex max-w-[1560px] flex-col gap-2 px-4 py-4 text-xs text-ink-3 sm:flex-row sm:items-center sm:justify-between sm:px-8 lg:px-12">
          <span>© 2026 USDSS · Study Abroad Decision Support System for US Education</span>
          <div className="flex flex-wrap items-center gap-4">
            <Link href="/methodology" className="transition-colors hover:text-brand">
              Phương pháp
            </Link>
            <Link href="/terms" className="transition-colors hover:text-brand">
              Điều khoản
            </Link>
            <Link href="/privacy" className="transition-colors hover:text-brand">
              Bảo mật
            </Link>
          </div>
        </div>
      </footer>
    );
  }

  return (
    <footer className={cn("mt-8 border-t border-line bg-surface text-ink", className)}>
      <div className="mx-auto grid max-w-[1560px] gap-10 px-4 py-14 sm:px-8 lg:grid-cols-12 lg:px-12">
        <div className="space-y-4 lg:col-span-5">
          <div className="flex items-center gap-2.5">
            <span className="flex size-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white p-0.5">
              <Image
                src="/icons/USDSS.png"
                alt=""
                width={32}
                height={32}
                className="size-8 object-contain"
              />
            </span>
            <span className="font-heading text-xl font-semibold">USDSS</span>
          </div>
          <p className="max-w-sm text-sm leading-relaxed text-ink-2">
            Hệ thống hỗ trợ ra quyết định du học Mỹ: phân tích hồ sơ, gợi ý trường, giải đáp bằng tiếng Việt và kết nối trung tâm tư vấn.
          </p>
        </div>

        <div className="grid grid-cols-2 gap-8 sm:grid-cols-3 lg:col-span-7">
          {COLUMNS.map((c) => (
            <div key={c.title}>
              <h3 className="mb-3 text-sm font-semibold text-ink">{c.title}</h3>
              <ul className="space-y-2.5">
                {c.links.map((l) => (
                  <li key={l.href}>
                    <Link href={l.href} className="text-sm text-ink-2 transition-colors hover:text-brand">
                      {l.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>

      <div className="border-t border-line">
        <div className="mx-auto flex max-w-[1560px] flex-col gap-2 px-4 py-6 text-xs text-ink-3 sm:flex-row sm:items-center sm:justify-between sm:px-8 lg:px-12">
          <span>© 2026 USDSS · Study Abroad Decision Support System for US Education</span>
          <span className="max-w-xl sm:text-right">
            Thông tin chỉ mang tính tham khảo, không cam kết kết quả trúng tuyển hay học bổng. Hãy kiểm tra trang chính thức của trường trước khi quyết định.
          </span>
        </div>
      </div>
    </footer>
  );
}
