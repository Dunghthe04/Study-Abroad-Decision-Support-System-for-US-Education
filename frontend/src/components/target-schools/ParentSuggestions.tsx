"use client";

// Trường phụ huynh gợi ý (status parent_suggested), hiện ở đầu trang Trường mục tiêu của con.
// "Thêm vào danh sách" = thêm trường như bình thường (backend đổi dòng gợi ý thành considering, giữ lời nhắn làm ghi chú).
// "Bỏ qua" = xóa dòng gợi ý. Không gợi ý nào thì không hiện gì.

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { ApiError, targetSchoolsApi } from "@/lib/api";
import { useFetched } from "@/lib/use-fetched";
import type { TargetSchoolDto } from "@/types/api";

export function ParentSuggestions({ onAccepted }: { onAccepted: () => void }) {
  const [version, setVersion] = useState(0);
  const { data } = useFetched(`parent-suggestions-${version}`, () =>
    targetSchoolsApi.list({ status: "parent_suggested" }),
  );
  const [busy, setBusy] = useState<string | null>(null);

  const act = async (t: TargetSchoolDto, accept: boolean) => {
    setBusy(t.id);
    try {
      if (accept) {
        await targetSchoolsApi.add({ universityId: t.universityId });
        toast.add({ type: "success", title: "Đã thêm vào danh sách", description: t.universityName });
      } else {
        await targetSchoolsApi.remove(t.id);
        toast.add({ type: "info", title: "Đã bỏ qua gợi ý", description: t.universityName });
      }
      if (accept) onAccepted();
    } catch (err) {
      toast.add({
        type: "error",
        title: "Chưa xử lý được gợi ý",
        description: err instanceof ApiError ? err.message : "Vui lòng thử lại.",
      });
    } finally {
      setBusy(null);
      setVersion((v) => v + 1);
    }
  };

  const items = data?.items ?? [];
  if (items.length === 0) return null;

  return (
    <section className="space-y-2 rounded-2xl border border-brand/20 bg-brand-wash p-4">
      <div>
        <h2 className="text-sm font-semibold text-foreground">Phụ huynh gợi ý</h2>
        <p className="text-xs text-muted-foreground">
          Các trường này chưa nằm trong danh sách của bạn. Bạn chọn thêm vào danh sách hoặc bỏ qua.
        </p>
      </div>
      <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
        {items.map((t) => (
          <li key={t.id} className="flex flex-wrap items-center justify-between gap-3 p-3">
            <div className="min-w-0 space-y-0.5">
              <p className="text-sm font-semibold text-foreground">{t.universityName}</p>
              <p className="text-xs text-muted-foreground">
                {[t.city, t.state].filter(Boolean).join(", ") || "Chưa rõ địa điểm"}
              </p>
              {t.notes && <p className="text-xs text-ink-2">Lời nhắn: {t.notes}</p>}
            </div>
            <div className="flex gap-2">
              <Button size="sm" disabled={busy === t.id} onClick={() => act(t, true)} loading={busy === t.id}>
                Thêm vào danh sách
              </Button>
              <Button size="sm" variant="outline" disabled={busy === t.id} onClick={() => act(t, false)}>
                Bỏ qua
              </Button>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
