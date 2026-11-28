"use client";

import Image from "next/image";
import { universityUrl } from "@/lib/university-display";
import type { UniversityListItem } from "@/types/university";

interface SchoolLogoStripProps {
  schools: readonly UniversityListItem[];
}

/**
 * Dải logo trường chạy ngang ngay dưới phần đầu trang chủ, lấy trực tiếp từ DB.
 * Hai bản sao nối tiếp nhau, chạy 50% chiều rộng rồi lặp lại nên không bị giật.
 * Rê chuột thì dừng; người dùng chọn giảm chuyển động thì đứng yên.
 */
export function SchoolLogoStrip({ schools }: SchoolLogoStripProps) {
  const logos = schools
    .map((s) => ({ name: s.name, logo: universityUrl(s.logoUrl) }))
    .filter((s): s is { name: string; logo: string } => Boolean(s.logo));

  if (logos.length === 0) return null;

  const items = [...logos, ...logos];
  return (
    // Cùng khung rộng và lề với các mục bên dưới (1560px), nền trong suốt
    <section
      aria-label="Một số trường học sinh Việt Nam quan tâm"
      className="mx-auto mt-10 max-w-[1560px] px-4 sm:mt-14 sm:px-8 lg:px-12"
    >
      <div className="group relative overflow-hidden [mask-image:linear-gradient(to_right,transparent,black_8%,black_92%,transparent)]">
        <ul className="flex w-max animate-[usas-logo-strip_45s_linear_infinite] items-center gap-24 group-hover:[animation-play-state:paused] motion-reduce:animate-none">
          {items.map((s, i) => (
            <li key={`${s.name}-${i}`} aria-hidden={i >= logos.length || undefined} className="shrink-0">
              {/* Chỉ logo; tên trường nằm ở alt và title cho người dùng trình đọc màn hình và khi rê chuột */}
              <Image
                src={s.logo}
                alt={i < logos.length ? s.name : ""}
                title={s.name}
                width={64}
                height={64}
                className="size-16 object-contain"
              />
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
