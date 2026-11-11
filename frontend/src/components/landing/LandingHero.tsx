"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowRightIcon, MapPinIcon, PauseIcon, PlayIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { GROUP_LABELS, formatGpa, formatPct, groupVariant } from "@/lib/recommendation-labels";
import { HERO_SCHOOLS, SAMPLE_PROFILE } from "@/lib/landing-schools";
import { SchoolPhotoBackdrop, useSchoolPhotoCycle } from "./SchoolPhotoBackdrop";
import { cn } from "@/lib/utils";

const INTERVAL_MS = 6000;

/**
 * Phần đầu trang chủ: ảnh khuôn viên trường thật tràn màn hình, đổi trường mỗi 6 giây.
 * Thẻ bên phải đi cùng ảnh: trường đang hiện, tỷ lệ nhận chung và nhóm với hồ sơ mẫu,
 * để khách thấy ngay sản phẩm làm gì thay vì một câu khẩu hiệu.
 */
export function LandingHero() {
  const [paused, setPaused] = useState(false);
  const count = HERO_SCHOOLS.length;
  const [index, setIndex] = useSchoolPhotoCycle(count, INTERVAL_MS, paused);

  const current = HERO_SCHOOLS[index];

  return (
    <section
      aria-label="Giới thiệu USAS"
      className="relative isolate -mt-[var(--header-h)] flex min-h-[min(100svh,960px)] items-end overflow-hidden bg-brand-ink text-white"
    >
      {/* Ảnh nền: mọi ảnh nằm chồng lên nhau, ảnh đang chọn hiện dần */}
      <SchoolPhotoBackdrop schools={HERO_SCHOOLS} activeIndex={index} />

      <div className="mx-auto grid w-full max-w-[1560px] gap-10 px-4 pt-[calc(var(--header-h)+4rem)] pb-12 sm:px-8 sm:pb-16 lg:grid-cols-12 lg:items-end lg:px-12 lg:pb-20">
        <div className="space-y-6 lg:col-span-8 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-3 motion-safe:duration-700">
          <h1 className="text-4xl leading-[1.1] font-semibold tracking-[-0.02em] text-balance sm:text-6xl">
            Chọn trường Mỹ hợp với điểm số và ngân sách của bạn
          </h1>
          <p className="max-w-[56ch] text-base leading-relaxed text-pretty text-white/85 sm:text-lg">
            Nhập GPA, điểm thi và ngân sách. USAS so hồ sơ với số liệu tuyển sinh của từng trường, xếp vào bốn nhóm và giải thích lý do cho từng trường.
          </p>
          <div className="flex flex-wrap gap-3">
            <Link href="/analysis" className={buttonVariants({ size: "xl", className: "!bg-accent !text-accent-foreground hover:!bg-accent/90 active:translate-y-px" })}>
              Phân tích hồ sơ
              <ArrowRightIcon className="transition-transform group-hover/button:translate-x-0.5" />
            </Link>
            <Link
              href="/methodology"
              className={buttonVariants({
                variant: "outline",
                size: "xl",
                className: "!border-white/40 !bg-transparent !text-white hover:!bg-white/10 active:translate-y-px",
              })}
            >
              Cách hệ thống xếp trường
            </Link>
          </div>
        </div>

        {/* Thẻ đi cùng ảnh nền: đổi theo trường đang hiện */}
        <div className="lg:col-span-4 lg:justify-self-end">
          <div className="w-full max-w-md rounded-xl bg-surface p-5 text-ink">
            {/* Hàng 3 chỉ số hồ sơ mẫu */}
            <div className="grid grid-cols-3 gap-3 pb-4">
              <div>
                <p className="text-xs text-ink-2">GPA</p>
                <p className="text-numeric text-2xl font-semibold text-ink">
                  {formatGpa(SAMPLE_PROFILE.gpa, 1)}
                </p>
              </div>
              <div>
                <p className="text-xs text-ink-2">SAT</p>
                <p className="text-numeric text-2xl font-semibold text-ink">
                  {SAMPLE_PROFILE.sat}
                </p>
              </div>
              <div>
                <p className="text-xs text-ink-2">Ngân sách</p>
                <p className="text-numeric text-2xl font-semibold text-ink whitespace-nowrap">
                  {`> $${SAMPLE_PROFILE.budgetUsd.toLocaleString("vi-VN")}`}
                </p>
              </div>
            </div>

            <ul className="divide-y divide-line border-t border-line">
              {HERO_SCHOOLS.map((s, i) => (
                <li key={s.name}>
                  <button
                    type="button"
                    onClick={() => setIndex(i)}
                    aria-pressed={i === index}
                    className={cn(
                      "flex w-full items-center justify-between gap-3 py-3 text-left outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
                      i === index ? "text-ink" : "text-ink-2 hover:text-ink",
                    )}
                  >
                    <span className="min-w-0">
                      <span className="flex items-center gap-2 text-sm font-medium">
                        <span
                          aria-hidden
                          className={cn("size-1.5 shrink-0 rounded-full transition-colors", i === index ? "bg-brand" : "bg-line")}
                        />
                        {s.name}
                      </span>
                      <span className="block pl-3.5 text-xs text-ink-2">
                        Tỉ lệ đỗ <span className="text-numeric">{formatPct(s.acceptRate)}</span>
                      </span>
                    </span>
                    <Badge variant={groupVariant(s.group)}>{GROUP_LABELS[s.group]}</Badge>
                  </button>
                </li>
              ))}
            </ul>
          </div>

          <div className="mt-3 flex max-w-md items-center justify-between gap-3 text-xs text-white/80">
            <span className="inline-flex items-center gap-1.5">
              <MapPinIcon className="size-3.5" />
              {current.name} · {current.city}
            </span>
            {count > 1 && (
              <button
                type="button"
                onClick={() => setPaused((p) => !p)}
                aria-label={paused ? "Tiếp tục đổi ảnh" : "Dừng đổi ảnh"}
                className="inline-flex size-8 items-center justify-center rounded-md text-white/80 outline-none transition-colors hover:bg-white/10 hover:text-white focus-visible:ring-3 focus-visible:ring-white/50"
              >
                {paused ? <PlayIcon className="size-3.5" /> : <PauseIcon className="size-3.5" />}
              </button>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}
