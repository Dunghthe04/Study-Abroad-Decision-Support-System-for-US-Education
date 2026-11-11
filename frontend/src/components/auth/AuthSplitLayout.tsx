"use client";

import React from "react";
import Link from "next/link";
import { LANDING_SCHOOLS } from "@/lib/landing-schools";
import { SchoolPhotoBackdrop, useSchoolPhotoCycle } from "@/components/landing/SchoolPhotoBackdrop";
import { cn } from "@/lib/utils";

interface AuthSplitLayoutProps {
  children: React.ReactNode;
  containerClassName?: string;
}

/**
 * Bố cục màn hình chia đôi cho các trang xác thực (đăng nhập, đăng ký, quên mật khẩu, đặt lại mật khẩu, xác thực email, phụ huynh đồng ý):
 * - Cột trái: ảnh khuôn viên các trường đại học thay đổi theo thời gian, tiêu đề lớn và tên trường đang hiển thị.
 * - Cột phải: form nhập liệu trên nền surface, bắt đầu ở khoảng 1/3 chiều cao màn hình (33svh).
 * - Màn hình hẹp (<900px): cột ảnh chuyển thành băng ngang khoảng 160px trên cùng, form hiển thị toàn chiều rộng bên dưới.
 */
export function AuthSplitLayout({ children, containerClassName }: AuthSplitLayoutProps) {
  const [index] = useSchoolPhotoCycle(LANDING_SCHOOLS.length);
  const currentSchool = LANDING_SCHOOLS[index];

  return (
    // Desktop: khung cao đúng một màn hình, trang không cuộn; form dài (đăng ký) chỉ cuộn bên trong cột phải
    <div className="flex min-h-svh w-full flex-col bg-surface min-[900px]:h-svh min-[900px]:flex-row min-[900px]:overflow-hidden">
      {/* Cột trái (hoặc băng ngang 160px trên điện thoại/màn hình <900px) */}
      <div className="relative isolate flex h-40 shrink-0 flex-col justify-end overflow-hidden bg-brand-ink p-5 text-white sm:p-6 min-[900px]:h-full min-[900px]:w-[62%] min-[900px]:flex-1 min-[900px]:justify-center min-[900px]:px-12 lg:min-[900px]:px-16 xl:min-[900px]:px-20">
        <SchoolPhotoBackdrop
          schools={LANDING_SCHOOLS}
          activeIndex={index}
          overlayClassName="min-[900px]:bg-linear-135"
        />

        {/* Chữ USAS về trang chủ, góc trên bên trái */}
        <Link
          href="/"
          className="absolute top-4 left-5 z-10 flex items-center gap-2 rounded-md font-heading text-lg font-bold tracking-tight text-white outline-none focus-visible:ring-3 focus-visible:ring-white/60 sm:left-6 min-[900px]:top-8 min-[900px]:left-12 lg:min-[900px]:left-16 xl:min-[900px]:left-20"
        >
          <span className="flex size-[30px] items-center justify-center rounded-lg bg-white font-sans text-xs font-black text-brand-ink">US</span>
          USAS
        </Link>

        <div className="relative z-10 max-w-xl">
          <h1 className="font-heading font-semibold text-white text-xl leading-tight sm:text-2xl min-[900px]:text-4xl lg:min-[900px]:text-5xl min-[900px]:leading-[1.15] text-balance">
            Lưu hồ sơ và danh sách trường của bạn ở một nơi
          </h1>
        </div>

        <p className="absolute right-4 bottom-3 z-10 text-xs text-white/80 select-none sm:right-6 sm:bottom-4">
          Ảnh chụp tại {currentSchool.name}
        </p>
      </div>

      {/* Cột phải: form nằm giữa theo cả chiều dọc và ngang.
          Dùng my-auto trong khung cuộn (không dùng justify-center) để form dài vẫn cuộn được từ đầu, không bị cắt mất phần trên */}
      <div className="flex flex-1 flex-col overflow-y-auto bg-surface px-5 py-8 sm:px-6 min-[900px]:h-full min-[900px]:w-[38%] min-[900px]:min-w-[420px] min-[900px]:px-10 min-[900px]:py-12 lg:min-[900px]:px-12 xl:min-[900px]:px-16">
        {/* Form rộng hơn, ô nhập cao 44px chữ 16px cho mọi form xác thực, không viền khung */}
        <div
          className={cn(
            "mx-auto w-full max-w-[480px] min-[900px]:my-auto [&_[data-slot=input]]:h-11 [&_[data-slot=input]]:px-3.5 [&_[data-slot=input]]:text-base [&_[data-slot=field-label]]:text-[15px] motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-2 motion-safe:duration-300",
            containerClassName,
          )}
        >
          {children}
        </div>
      </div>
    </div>
  );
}
