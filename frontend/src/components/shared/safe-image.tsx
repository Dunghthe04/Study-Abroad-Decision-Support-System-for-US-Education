"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";

/**
 * Ảnh từ link ngoài: có khung xương (skeleton) trong lúc tải, và khi link hỏng hoặc không có ảnh
 * thì hiện `fallback` thay vì biểu tượng ảnh vỡ. Chiếm đầy khung cha (cha cần `relative` + kích thước).
 * Dùng cho mọi ảnh bìa, logo, ảnh trung tâm lấy từ URL người dùng nhập.
 */
export function SafeImage({
  src,
  alt,
  className,
  fallback,
  fit = "cover",
}: {
  src: string | null | undefined;
  alt: string;
  className?: string;
  fallback: ReactNode;
  fit?: "cover" | "contain";
}) {
  const [state, setState] = useState<{ src: string | null | undefined; status: "loading" | "loaded" | "failed" }>({
    src,
    status: "loading",
  });
  const ref = useRef<HTMLImageElement>(null);
  // Đổi link thì tải lại từ đầu
  const status = state.src === src ? state.status : "loading";

  useEffect(() => {
    // Ảnh đã có trong bộ nhớ đệm có thể xong trước khi React gắn onLoad
    const img = ref.current;
    // hoặc đã lỗi trước đó (trang dựng sẵn từ máy chủ), khi ấy onError không bao giờ chạy
    if (img?.complete) setState({ src, status: img.naturalWidth > 0 ? "loaded" : "failed" });
  }, [src]);

  if (!src || status === "failed") return <>{fallback}</>;

  return (
    <>
      {status === "loading" && <span aria-hidden="true" className="absolute inset-0 animate-pulse bg-muted" />}
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        ref={ref}
        src={src}
        alt={alt}
        onLoad={() => setState({ src, status: "loaded" })}
        onError={() => setState({ src, status: "failed" })}
        className={cn(
          "absolute inset-0 size-full transition-[opacity,transform] duration-500 ease-out",
          fit === "contain" ? "object-contain" : "object-cover",
          status === "loaded" ? "opacity-100" : "opacity-0",
          className,
        )}
      />
    </>
  );
}
