import Link from "next/link";
import { ArrowLeftIcon } from "lucide-react";

/** Nút tròn nổi góc trái trên của trang chi tiết (trường, trung tâm): mờ nhẹ, không hiệu ứng khi rê chuột, luôn hiện khi cuộn. Đặt trong khối `@container`. */
export function FloatingBackButton({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      aria-label={label}
      title={label}
      className="fixed top-[calc(var(--header-h)+0.75rem)] left-4 z-30 flex size-11 items-center justify-center rounded-full border border-line bg-surface text-ink opacity-70 shadow-md @2xl:left-8"
    >
      <ArrowLeftIcon className="size-5" aria-hidden="true" />
    </Link>
  );
}
