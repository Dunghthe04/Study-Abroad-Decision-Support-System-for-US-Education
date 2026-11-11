"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";

export interface SchoolPhotoItem {
  name: string;
  image: string;
}

export function useSchoolPhotoCycle(count: number, intervalMs = 6000, paused = false) {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    if (paused || count < 2) return;
    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduce) return;
    const id = window.setInterval(() => setIndex((i) => (i + 1) % count), intervalMs);
    return () => window.clearInterval(id);
  }, [count, intervalMs, paused]);

  return [index, setIndex] as const;
}

interface SchoolPhotoBackdropProps {
  schools: readonly SchoolPhotoItem[];
  activeIndex: number;
  className?: string;
  overlayClassName?: string;
}

export function SchoolPhotoBackdrop({
  schools,
  activeIndex,
  className,
  overlayClassName,
}: SchoolPhotoBackdropProps) {
  return (
    <div aria-hidden className={cn("absolute inset-0 -z-10", className)}>
      {schools.map((s, i) => (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          key={s.name}
          src={s.image}
          alt=""
          fetchPriority={i === 0 ? "high" : "low"}
          loading={i === 0 ? "eager" : "lazy"}
          className={cn(
            "absolute inset-0 size-full object-cover transition-[opacity,scale] duration-[1600ms] ease-out motion-reduce:transition-none",
            i === activeIndex ? "scale-100 opacity-100" : "scale-[1.04] opacity-0",
          )}
        />
      ))}
      {/* Lớp tối để chữ trắng đọc được trên mọi ảnh (đạt AA ở vùng chữ) */}
      <div
        className={cn(
          "absolute inset-0 bg-[linear-gradient(to_top,rgb(10_66_86/0.92)_0%,rgb(10_66_86/0.7)_45%,rgb(10_66_86/0.35)_100%)]",
          overlayClassName,
        )}
      />
    </div>
  );
}
