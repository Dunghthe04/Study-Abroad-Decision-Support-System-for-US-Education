"use client";

// 1.5e: vào làm thay một con managed rồi tải lại sang /advisor (mở khung chat). Đang làm thay con khác thì
// backend đổi sang con này. Lỗi (không phải con managed của mình...) thì báo và cho về Con của tôi.

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/lib/api";

export function ActAndOpenChat({ childId }: { childId: string }) {
  const { startActing } = useAuth();
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);

  useEffect(() => {
    // Strict mode chạy effect 2 lần: chỉ gọi một lần
    if (started.current) return;
    started.current = true;
    startActing(childId, "/advisor").catch((err: unknown) =>
      setError(err instanceof ApiError ? err.message : "Chưa mở được hồ sơ của con. Vui lòng thử lại."),
    );
  }, [childId, startActing]);

  return (
    <div className="mx-auto max-w-md space-y-4 px-4 py-12">
      {error ? (
        <>
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
          <Link href="/parent/children" className={buttonVariants({ variant: "outline" })}>
            Về Con của tôi
          </Link>
        </>
      ) : (
        <p role="status" className="flex items-center gap-2 text-muted-foreground">
          <Spinner /> Đang mở hồ sơ của con…
        </p>
      )}
    </div>
  );
}
