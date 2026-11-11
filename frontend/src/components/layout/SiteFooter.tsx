import Link from "next/link";
import { PHOTO_CREDITS } from "@/lib/landing-schools";

const COLUMNS: { title: string; links: { href: string; label: string }[] }[] = [
  {
    title: "Công cụ",
    links: [
      { href: "/analysis", label: "Phân tích hồ sơ" },
      { href: "/recommendations", label: "Trường gợi ý" },
      { href: "/target-schools", label: "Trường mục tiêu" },
      { href: "/advisor", label: "Trợ lý AI" },
    ],
  },
  {
    title: "Khám phá",
    links: [
      { href: "/centers", label: "Trung tâm tư vấn" },
      { href: "/methodology", label: "Giới thiệu phương pháp" },
    ],
  },
  {
    title: "Tài khoản",
    links: [
      { href: "/login", label: "Đăng nhập" },
      { href: "/register", label: "Đăng ký" },
      { href: "/account", label: "Tài khoản của tôi" },
    ],
  },
];

/** Chân trang cho trang chủ, dùng màu theo token của hệ thống */
export function SiteFooter() {
  return (
    <footer className="mt-8 border-t border-line bg-surface text-ink">
      <div className="mx-auto grid max-w-[1560px] gap-10 px-4 py-14 sm:px-8 lg:grid-cols-12 lg:px-12">
        <div className="space-y-4 lg:col-span-5">
          <div className="flex items-center gap-2.5">
            <span className="font-heading text-xl font-semibold">USAS</span>
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
          <span>© 2026 USAS · Study Abroad Decision Support System for US Education</span>
          <span className="max-w-xl sm:text-right">
            Thông tin chỉ mang tính tham khảo, không cam kết kết quả trúng tuyển hay học bổng. Hãy kiểm tra trang chính thức của trường trước khi quyết định.
          </span>
        </div>
        <p className="mx-auto max-w-[1560px] px-4 pb-6 text-xs text-ink-3 sm:px-8 lg:px-12">Ảnh khuôn viên từ Wikimedia Commons: {PHOTO_CREDITS}.</p>
      </div>
    </footer>
  );
}
