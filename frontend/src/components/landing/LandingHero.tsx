"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { ArrowRightIcon, MapPinIcon, PauseIcon, PlayIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { GROUP_LABELS, formatGpa, formatPct, groupVariant } from "@/lib/recommendation-labels";
import { SAMPLE_PROFILE } from "@/lib/landing-schools";
import { isValidRate, universityUrl } from "@/lib/university-display";
import type { UniversityListItem } from "@/types/university";
import { SchoolPhotoBackdrop, useSchoolPhotoCycle } from "./SchoolPhotoBackdrop";
import { cn } from "@/lib/utils";

const INTERVAL_MS = 6000;
const HERO_LIMIT = 4;

interface HeroSchool {
  name: string;
  city: string;
  image: string;
  acceptRate: number | null;
  group: "R" | "M" | "S";
}

// ponytail: nhóm minh họa cho SAMPLE_PROFILE suy từ tỉ lệ đỗ trong DB; đổi sang endpoint riêng nếu cần gắn hồ sơ thật
function sampleGroup(rate: number | null): "R" | "M" | "S" {
  if (!isValidRate(rate) || rate < 0.2) return "R";
  if (rate < 0.6) return "M";
  return "S";
}

interface LandingHeroProps {
  schools: readonly UniversityListItem[];
  loading?: boolean;
}

/**
 * Phần đầu trang chủ: ảnh khuôn viên trường từ DB tràn màn hình, đổi trường mỗi 6 giây.
 * Thẻ bên phải đi cùng ảnh: trường đang hiện, tỷ lệ nhận chung và nhóm với hồ sơ mẫu,
 * để khách thấy ngay sản phẩm làm gì thay vì một câu khẩu hiệu.
 */
export function LandingHero({ schools, loading = false }: LandingHeroProps) {
  const [paused, setPaused] = useState(false);

  const heroSchools = useMemo<HeroSchool[]>(() => {
    const withCover = schools
      .map((s) => {
        const image = universityUrl(s.coverUrl);
        if (!image) return null;
        const acceptRate = isValidRate(s.acceptanceRate) ? s.acceptanceRate : null;
        return {
          name: s.name,
          city: [s.city, s.state].filter(Boolean).join(", "),
          image,
          acceptRate,
          group: sampleGroup(acceptRate),
        };
      })
      .filter((s): s is HeroSchool => s !== null);

    // Ưu tiên trường có tỉ lệ đỗ để thẻ bên phải hiển thị đủ số liệu
    const withRate = withCover.filter((s) => s.acceptRate != null);
    const withoutRate = withCover.filter((s) => s.acceptRate == null);
    return [...withRate, ...withoutRate].slice(0, HERO_LIMIT);
  }, [schools]);

  const count = heroSchools.length;
  const [index, setIndex] = useSchoolPhotoCycle(count, INTERVAL_MS, paused);
  const activeIndex = count > 0 ? index % count : 0;
  const current = heroSchools[activeIndex];

  return (
    <section
      aria-label="Giới thiệu USDSS"
      className="relative isolate -mt-[var(--header-h)] flex min-h-[min(100svh,960px)] items-end overflow-hidden bg-brand-deep text-white"
    >
      {/* Ảnh nền: mọi ảnh nằm chồng lên nhau, ảnh đang chọn hiện dần */}
      {count > 0 && <SchoolPhotoBackdrop schools={heroSchools} activeIndex={activeIndex} />}

      <div className="mx-auto grid w-full max-w-[1560px] gap-10 px-4 pt-[calc(var(--header-h)+4rem)] pb-12 sm:px-8 sm:pb-16 lg:grid-cols-12 lg:items-end lg:px-12 lg:pb-20">
        <div className="space-y-6 lg:col-span-8 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-3 motion-safe:duration-700">
          <h1 className="text-4xl leading-[1.1] font-semibold tracking-[-0.02em] text-balance sm:text-6xl">
            Chọn trường Mỹ hợp với điểm số và ngân sách của bạn
          </h1>
          <p className="max-w-[56ch] text-base leading-relaxed text-pretty text-white/85 sm:text-lg">
            Nhập GPA, điểm thi và ngân sách. USDSS so hồ sơ với số liệu tuyển sinh của từng trường, xếp vào bốn nhóm và giải thích lý do cho từng trường.
          </p>
          <div className="flex flex-wrap gap-3">
            <Link href="/analysis" className={buttonVariants({ size: "xl", className: "!bg-white !text-brand-deep hover:!bg-white/90" })}>
              Phân tích hồ sơ
              <ArrowRightIcon className="transition-transform group-hover/button:translate-x-0.5" />
            </Link>
            <Link
              href="/methodology"
              className={buttonVariants({
                variant: "outline",
                size: "xl",
                className: "!border-white/40 !bg-transparent !text-white hover:!bg-white/10",
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
            <div className="grid grid-cols-[1fr_1fr_1.7fr] gap-3 pb-4">
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
                <p className="text-numeric text-xl font-semibold text-ink whitespace-nowrap sm:text-2xl">
                  {`> $${SAMPLE_PROFILE.budgetUsd.toLocaleString("vi-VN")}`}
                </p>
              </div>
            </div>

            <ul className="divide-y divide-line border-t border-line">
              {loading && count === 0
                ? Array.from({ length: HERO_LIMIT }).map((_, i) => (
                    <li key={i} className="flex items-center justify-between gap-3 py-3">
                      <div className="space-y-1.5">
                        <Skeleton className="h-4 w-44" />
                        <Skeleton className="h-3 w-24" />
                      </div>
                      <Skeleton className="h-5 w-16 rounded-full" />
                    </li>
                  ))
                : heroSchools.map((s, i) => (
                    <li key={s.name}>
                      <button
                        type="button"
                        onClick={() => setIndex(i)}
                        aria-pressed={i === activeIndex}
                        className={cn(
                          "flex w-full cursor-pointer items-center justify-between gap-3 py-3 text-left outline-none transition-colors focus-visible:ring-3 focus-visible:ring-ring/50",
                          i === activeIndex ? "text-ink" : "text-ink-2 hover:text-ink",
                        )}
                      >
                        <span className="min-w-0">
                          <span className="flex items-center gap-2 text-sm font-medium">
                            <span
                              aria-hidden
                              className={cn("size-1.5 shrink-0 rounded-full transition-colors", i === activeIndex ? "bg-brand" : "bg-line")}
                            />
                            <span className="truncate">{s.name}</span>
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

          {current && (
            <div className="mt-3 flex max-w-md items-center justify-between gap-3 text-xs text-white/80">
              <span className="inline-flex items-center gap-1.5">
                <MapPinIcon className="size-3.5 shrink-0" />
                {current.city ? `${current.name} · ${current.city}` : current.name}
              </span>
              {count > 1 && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => setPaused((p) => !p)}
                  aria-label={paused ? "Tiếp tục đổi ảnh" : "Dừng đổi ảnh"}
                  className="text-white/80 hover:bg-white/10 hover:text-white dark:hover:bg-white/10 focus-visible:border-transparent focus-visible:ring-white/50"
                >
                  {paused ? <PlayIcon className="size-3.5" /> : <PauseIcon className="size-3.5" />}
                </Button>
              )}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
