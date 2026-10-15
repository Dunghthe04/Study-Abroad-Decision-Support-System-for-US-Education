"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowRightIcon, Loader2Icon, SparklesIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";

interface RedirectToAnalysisProps {
  sourceTitle: string;
}

export function RedirectToAnalysis({ sourceTitle }: RedirectToAnalysisProps) {
  const router = useRouter();
  const [seconds, setSeconds] = useState(3);

  useEffect(() => {
    const timer = setInterval(() => {
      setSeconds((prev) => {
        if (prev <= 1) {
          clearInterval(timer);
          router.replace("/analysis");
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [router]);

  return (
    <div className="mx-auto max-w-xl py-16 text-center space-y-6">
      <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-brand-wash text-brand">
        <SparklesIcon className="size-7" />
      </div>

      <div className="space-y-2">
        <h1 className="text-h1 text-foreground">
          Đang chuyển hướng tới Luồng hồ sơ thống nhất
        </h1>
        <p className="text-sm text-muted-foreground leading-relaxed">
          Trang <strong>{sourceTitle}</strong> đã được tích hợp hoàn toàn vào quy trình nhập hồ sơ
          một cửa 6 bước tại <strong>/analysis</strong>, hỗ trợ tải ảnh học bạ tự động đọc điểm OCR
          và tính GPA 4.0 chuẩn hóa.
        </p>
      </div>

      <div className="flex items-center justify-center gap-2 text-xs text-brand font-medium">
        <Loader2Icon className="size-3.5 animate-spin" />
        <span>Tự động chuyển tiếp sau {seconds} giây...</span>
      </div>

      <div className="pt-2">
        <Link
          href="/analysis"
          className={buttonVariants({ variant: "default", size: "lg", className: "gap-2" })}
        >
          Chuyển tới Luồng phân tích ngay
          <ArrowRightIcon className="size-4" />
        </Link>
      </div>
    </div>
  );
}
