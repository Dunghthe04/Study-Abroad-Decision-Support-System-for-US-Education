"use client";

import Image from "next/image";
import { LOGO_STRIP } from "@/lib/landing-schools";

/**
 * Dải logo trường chạy ngang ngay dưới phần đầu trang chủ.
 * Hai bản sao nối tiếp nhau, chạy 50% chiều rộng rồi lặp lại nên không bị giật.
 * Rê chuột thì dừng; người dùng chọn giảm chuyển động thì đứng yên.
 */
export function SchoolLogoStrip() {
  const items = [...LOGO_STRIP, ...LOGO_STRIP];
  return (
    <section aria-label="Một số trường học sinh Việt Nam quan tâm" className="border-b border-line bg-surface py-8">
      <p className="mb-5 text-center text-sm text-ink-2">Học sinh Việt Nam quan tâm những trường như</p>
      <div className="group relative overflow-hidden [mask-image:linear-gradient(to_right,transparent,black_8%,black_92%,transparent)]">
        <ul className="flex w-max animate-[usas-logo-strip_45s_linear_infinite] items-center gap-14 group-hover:[animation-play-state:paused] motion-reduce:animate-none">
          {items.map((s, i) => (
            <li key={`${s.name}-${i}`} aria-hidden={i >= LOGO_STRIP.length || undefined} className="flex shrink-0 items-center gap-3">
              <Image
                src={s.logo}
                alt={i < LOGO_STRIP.length ? s.name : ""}
                width={44}
                height={44}
                className="size-11 object-contain"
              />
              <span className="font-heading text-lg font-semibold whitespace-nowrap text-ink-2">{s.name}</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
