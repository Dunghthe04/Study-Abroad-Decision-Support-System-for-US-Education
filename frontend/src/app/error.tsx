"use client";

import { useEffect } from "react";
import { ErrorFallback } from "@/components/ui/error-fallback";

export default function RootError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <ErrorFallback
      title="Đã có lỗi xảy ra"
      description={error.message || "Đã xảy ra sự cố ngoài ý muốn. Vui lòng thử lại sau."}
      reset={reset}
    />
  );
}
