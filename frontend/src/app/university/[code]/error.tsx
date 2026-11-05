"use client";

import { AlertCircleIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function UniversityError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <section role="alert" className="mx-auto max-w-lg space-y-4 py-20 text-center">
      <AlertCircleIcon className="mx-auto size-12 text-muted-foreground" />
      <h1 className="text-2xl font-semibold">Chưa thể tải thông tin trường</h1>
      <p className="text-sm leading-relaxed text-muted-foreground">Kết nối đến hệ thống đang gặp sự cố. Vui lòng thử lại, không cần thay đổi mã trường.</p>
      <Button type="button" onClick={reset}>Thử lại</Button>
    </section>
  );
}
