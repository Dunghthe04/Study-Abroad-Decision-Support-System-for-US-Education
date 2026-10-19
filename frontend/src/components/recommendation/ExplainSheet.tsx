"use client";

import { useEffect, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Spinner } from "@/components/ui/spinner";
import { recommendationApi } from "@/lib/recommendation-api";
import type { SchoolExplanation } from "@/types/recommendation";

interface ExplainSheetProps {
  code: string | null; // null = đóng
  onClose: () => void;
}

// Bảng trượt bên phải: mở ra thì gọi Qwen viết lời tư vấn cho trường đó.
export function ExplainSheet({ code, onClose }: ExplainSheetProps) {
  // Kết quả gắn với mã trường. Mở trường khác thì kết quả cũ tự bị bỏ qua, không cần reset trong effect.
  const [result, setResult] = useState<{ code: string; data?: SchoolExplanation; error?: string } | null>(null);
  const data = result?.code === code ? (result.data ?? null) : null;
  const error = result?.code === code ? (result.error ?? null) : null;

  useEffect(() => {
    if (!code) return;
    let cancelled = false; // người dùng đóng hoặc mở trường khác trước khi Qwen trả lời
    recommendationApi
      .explain(code)
      .then((res) => {
        if (!cancelled) setResult({ code, data: res });
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setResult({ code, error: err instanceof Error ? err.message : "Chưa viết được lời tư vấn." });
      });
    return () => {
      cancelled = true;
    };
  }, [code]);

  return (
    <Sheet open={code !== null} onOpenChange={(open) => !open && onClose()}>
      <SheetContent className="overflow-y-auto">
        <SheetHeader>
          <SheetTitle>{data?.name ?? "Tư vấn cho trường này"}</SheetTitle>
          <SheetDescription>Viết từ số liệu đã chấm, chỉ để tham khảo.</SheetDescription>
        </SheetHeader>

        <div className="space-y-5 px-4 pb-6 text-sm">
          {!data && !error && (
            <div className="flex items-center gap-2 text-muted-foreground">
              <Spinner />
              Đang viết lời tư vấn...
            </div>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertDescription className="text-xs">{error}</AlertDescription>
            </Alert>
          )}

          {data && (
            <>
              <p className="leading-relaxed text-foreground">{data.summary}</p>
              <ExplainList title="Điểm mạnh" items={data.strengths} />
              <ExplainList title="Cần cải thiện" items={data.improvements} />
            </>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}

function ExplainList({ title, items }: { title: string; items: string[] }) {
  if (items.length === 0) return null;
  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold text-foreground">{title}</p>
      <ul className="space-y-1 text-xs text-ink-2">
        {items.map((item, i) => (
          <li key={i}>• {item}</li>
        ))}
      </ul>
    </div>
  );
}
