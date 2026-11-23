"use client";

import { useState } from "react";
import type { StateLivingCost } from "@/types/cost";

const MAX = 6;

/**
 * UC-28: chọn các bang và đặt cạnh nhau. Giá trị là chỉ số giá vùng (Mỹ = 100), không phải số USD,
 * nên không đổi thành USD/năm. Bang chưa có dữ liệu ghi rõ, không ước đoán.
 */
export function StateLivingCostCompare({ states }: { states: StateLivingCost[] }) {
  const [picked, setPicked] = useState<string[]>([]);
  const chosen = states.filter((s) => picked.includes(s.code));
  const scaleMax = Math.max(100, ...chosen.map((s) => s.index ?? 0));
  const hasAnyData = states.some((s) => s.index !== null);
  const toggle = (code: string) =>
    setPicked((p) => (p.includes(code) ? p.filter((c) => c !== code) : p.length >= MAX ? p : [...p, code]));

  return (
    <div className="space-y-6">
      {!hasAnyData && (
        <p className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
          Chưa có dữ liệu chi phí sinh hoạt cho bang nào. Dữ liệu sẽ hiện khi được nhập kèm nguồn và ngày lấy.
        </p>
      )}

      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">Chọn tối đa {MAX} bang ({picked.length} đã chọn)</legend>
        <div className="flex flex-wrap gap-2">
          {states.map((s) => (
            <label
              key={s.code}
              className={`cursor-pointer rounded-full border px-3 py-1 text-sm ${picked.includes(s.code) ? "border-primary bg-primary/10" : ""}`}
            >
              <input type="checkbox" className="sr-only" checked={picked.includes(s.code)} onChange={() => toggle(s.code)} />
              {s.name}
            </label>
          ))}
        </div>
      </fieldset>

      {chosen.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[480px] border-collapse text-sm">
            <thead>
              <tr className="border-b text-left">
                <th className="py-2 pr-3 font-medium">Bang</th>
                <th className="px-3 py-2 font-medium">Chỉ số giá vùng (Mỹ = 100)</th>
                <th className="px-3 py-2 font-medium">Chỉ số tiền thuê nhà</th>
                <th className="px-3 py-2 font-medium">Năm</th>
                <th className="px-3 py-2 font-medium">Nguồn</th>
              </tr>
            </thead>
            <tbody>
              {chosen.map((s) => (
                <tr key={s.code} className="border-b">
                  <td className="py-2 pr-3 font-medium">{s.name}</td>
                  <td className="px-3 py-2">
                    {s.index === null ? (
                      <span className="text-muted-foreground">Chưa có dữ liệu</span>
                    ) : (
                      <div className="flex items-center gap-2">
                        <span className="w-12 tabular-nums">{s.index}</span>
                        <div className="h-3 flex-1 overflow-hidden rounded bg-muted" aria-hidden="true">
                          <div className="h-full bg-primary" style={{ width: `${(s.index / scaleMax) * 100}%` }} />
                        </div>
                      </div>
                    )}
                  </td>
                  <td className="px-3 py-2 tabular-nums">{s.housingRentsIndex ?? "—"}</td>
                  <td className="px-3 py-2">{s.year ?? "—"}</td>
                  <td className="px-3 py-2 text-xs">
                    {s.sourceUrl ? <a href={s.sourceUrl} target="_blank" rel="noopener noreferrer" className="underline">{new URL(s.sourceUrl).hostname}</a> : "—"}
                    {s.retrievedAt && <div className="text-muted-foreground">Lấy ngày {new Date(s.retrievedAt).toLocaleDateString("vi-VN")}</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="text-xs text-muted-foreground">
        Chỉ số cao hơn 100 nghĩa là mặt bằng giá của bang cao hơn mức trung bình toàn nước Mỹ. Đây là chỉ số so sánh giữa các bang,
        không phải số tiền USD mỗi năm và không thay cho chi phí sinh hoạt ghi ở từng trường.
      </p>
    </div>
  );
}
