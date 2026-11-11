"use client";

import React from "react";
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
    <div className="flex min-h-screen min-h-svh w-full flex-col min-[900px]:flex-row bg-surface">
      {/* Cột trái (hoặc băng ngang 160px trên điện thoại/màn hình <900px) */}
      <div className="relative isolate flex h-40 shrink-0 flex-col justify-end overflow-hidden bg-brand-ink text-white p-5 sm:p-6 min-[900px]:h-auto min-[900px]:min-h-screen min-[900px]:w-[62%] min-[900px]:flex-1 min-[900px]:justify-center min-[900px]:px-12 lg:min-[900px]:px-16 xl:min-[900px]:px-20">
        <SchoolPhotoBackdrop
          schools={LANDING_SCHOOLS}
          activeIndex={index}
          overlayClassName="min-[900px]:bg-[linear-gradient(135deg,rgb(10_66_86/0.92)_0%,rgb(10_66_86/0.75)_50%,rgb(10_66_86/0.45)_100%)]"
        />

        <div className="relative z-10 max-w-xl">
          <h1 className="font-heading font-semibold text-white text-xl leading-tight sm:text-2xl min-[900px]:text-4xl lg:min-[900px]:text-5xl min-[900px]:leading-[1.15] text-balance">
            Chọn trường Mỹ hợp với điểm số và ngân sách của bạn
          </h1>
        </div>

        <p className="absolute right-4 bottom-3 z-10 text-xs text-white/80 select-none sm:right-6 sm:bottom-4">
          Ảnh chụp tại {currentSchool.name}
        </p>
      </div>

      {/* Cột phải: form bắt đầu ở 33svh trên desktop */}
      <div className="flex flex-1 flex-col justify-start bg-surface px-5 py-6 sm:px-6 sm:py-8 min-[900px]:w-[38%] min-[900px]:min-w-[420px] min-[900px]:px-10 lg:min-[900px]:px-12 xl:min-[900px]:px-16 min-[900px]:pt-[33svh] min-[900px]:pb-16 overflow-y-auto">
        <div
          className={cn(
            "w-full max-w-[420px] mx-auto motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-2 motion-safe:duration-300",
            containerClassName,
          )}
        >
          {children}
        </div>
      </div>
    </div>
  );
}
