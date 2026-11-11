"use client";

import { ErrorFallback } from "@/components/ui/error-fallback";

export default function UniversityError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <ErrorFallback
      title="Chưa thể tải thông tin trường"
      description="Kết nối đến hệ thống đang gặp sự cố. Vui lòng thử lại, không cần thay đổi mã trường."
      reset={reset}
    />
  );
}
