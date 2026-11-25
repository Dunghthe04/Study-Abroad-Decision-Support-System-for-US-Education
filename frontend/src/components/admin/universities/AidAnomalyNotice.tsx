"use client";

// [UC-22, UC-62] Trường có hỗ trợ tài chính trung bình cho du học sinh LỚN HƠN học phí: dữ liệu bất thường.
// Gợi ý trường không dùng số này để trừ chi phí (xử lý như không có dữ liệu hỗ trợ), nên Moderator cần kiểm tra lại nguồn.
// Chỉ hiện khi có trường bất thường; lỗi tải thì không hiện gì.

import { useEffect, useState } from "react";
import { TriangleAlertIcon } from "lucide-react";
import { getAdminAidAnomalies } from "@/lib/admin-university-api";
import { studyLevelLabel } from "@/lib/study-levels";
import type { AdminAidAnomaly } from "@/types/admin-university";

const USD = new Intl.NumberFormat("vi-VN");

export function AidAnomalyNotice() {
  const [items, setItems] = useState<AdminAidAnomaly[]>([]);

  useEffect(() => {
    let alive = true;
    void getAdminAidAnomalies().then((list) => alive && setItems(list));
    return () => {
      alive = false;
    };
  }, []);

  if (items.length === 0) return null;
  return (
    <details className="rounded-xl border border-warn/40 bg-warn/5 px-4 py-3 text-sm">
      <summary className="flex cursor-pointer list-none items-center gap-2 font-semibold text-warn [&::-webkit-details-marker]:hidden">
        <TriangleAlertIcon className="size-4" />
        {items.length} bậc học có hỗ trợ tài chính trung bình lớn hơn học phí, cần kiểm tra lại nguồn
      </summary>
      <p className="mt-2 text-xs text-muted-foreground">
        Gợi ý trường không dùng số hỗ trợ này để trừ chi phí. Dữ liệu hỗ trợ tài chính cập nhật bằng đợt nạp dữ liệu, không sửa tại màn hình trường.
      </p>
      <ul className="mt-2 space-y-1.5">
        {items.map((a) => (
          <li key={`${a.universityId}-${a.studyLevel}`} className="text-xs">
            <span className="font-semibold text-foreground">{a.name}</span> ({studyLevelLabel(a.studyLevel)}): học phí {USD.format(a.tuitionUsd)} USD,
            hỗ trợ trung bình {USD.format(a.intlAidAvgUsd)} USD
            {a.sourceUrl && (
              <>
                {" · "}
                <a href={a.sourceUrl} target="_blank" rel="noopener noreferrer" className="text-brand underline-offset-2 hover:underline">
                  nguồn
                </a>
              </>
            )}
          </li>
        ))}
      </ul>
    </details>
  );
}
