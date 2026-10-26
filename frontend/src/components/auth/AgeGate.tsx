"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { ShieldAlertIcon } from "lucide-react";
import { useAgeBlock, type AgeFeature } from "@/lib/age-features";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

interface AgeGateProps {
  feature: AgeFeature;
  children: ReactNode;
}

// [Q11] Bọc trang/nút của chức năng bị chặn theo tuổi: bị chặn thì thay bằng thông báo, được dùng thì hiện bình thường.
export function AgeGate({ feature, children }: AgeGateProps) {
  const block = useAgeBlock(feature);
  if (!block) return <>{children}</>;

  return (
    <div className="flex min-h-[50vh] items-center justify-center p-4">
      <Card className="w-full max-w-lg text-center">
        <CardHeader>
          <CardTitle className="flex items-center justify-center gap-2">
            <ShieldAlertIcon className="size-5" aria-hidden />
            <h2>{block.needParentConsent ? "Cần phụ huynh đồng ý" : "Chức năng dành cho người từ 18 tuổi"}</h2>
          </CardTitle>
          <CardDescription>{block.message}</CardDescription>
        </CardHeader>
        {block.needParentConsent && (
          <CardContent className="space-y-3">
            <p className="text-sm text-muted-foreground">
              Trong lúc chờ, bạn vẫn xem được danh sách trường, trung tâm và phương pháp đánh giá.
            </p>
            <Link href="/account" className={buttonVariants({ size: "lg" })}>
              Quản lý liên kết phụ huynh
            </Link>
          </CardContent>
        )}
      </Card>
    </div>
  );
}
