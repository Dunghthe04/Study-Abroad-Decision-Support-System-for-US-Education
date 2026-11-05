"use client";

import { useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { UniversitySource } from "@/types/university";
import { UNIVERSITY_FIELD_LABELS, universityDate } from "@/lib/university-display";
import { DetailSection, SourceLink } from "./UniversityDetailParts";

const STATUS_LABELS: Record<string, string> = {
  verified: "Đã xác minh", unverified: "Chưa xác minh", derived: "Tính từ dữ liệu nguồn", not_published: "Không công bố",
};

export function UniversitySources({ sources }: { sources: UniversitySource[] }) {
  const [showAll, setShowAll] = useState(false);
  const [showReference, setShowReference] = useState(false);
  const referenceCount = sources.filter((source) => source.appliedToSnapshot === false).length;
  const relevant = sources.filter((source) => showReference || source.appliedToSnapshot !== false);
  const groups = new Map<string, { source: UniversitySource; fields: string[] }>();
  for (const source of relevant) {
    const key = JSON.stringify([source.sourceUrl, source.status, source.retrievedAt, source.dataYear, source.appliedToSnapshot]);
    const fieldLabel = UNIVERSITY_FIELD_LABELS[source.fieldName] ?? source.fieldName;
    const existing = groups.get(key);
    if (existing) {
      if (!existing.fields.includes(fieldLabel)) existing.fields.push(fieldLabel);
    } else {
      groups.set(key, { source, fields: [fieldLabel] });
    }
  }
  const items = [...groups.entries()];
  const visible = showAll ? items : items.slice(0, 6);
  return (
    <DetailSection id="sources" title="Nguồn & ngày dữ liệu" description="Trạng thái xác minh là của từng dữ liệu, không phải bảo đảm rằng liên kết nguồn hiện vẫn truy cập được.">
      {items.length ? (
        <ul className="space-y-3">
          {visible.map(([key, { source, fields }]) => (
            <li key={key} className="rounded-lg border border-border/80 p-4">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant={source.status === "verified" ? "secondary" : "outline"}>{STATUS_LABELS[source.status] ?? "Chưa rõ trạng thái"}</Badge>
                {source.appliedToSnapshot === false && <Badge variant="outline">Chỉ tham khảo</Badge>}
                {source.dataYear && <span className="text-xs text-muted-foreground">Kỳ dữ liệu: {source.dataYear}</span>}
              </div>
              <p className="mt-3 text-sm leading-relaxed">{fields.join(" · ")}</p>
              <div className="mt-2"><SourceLink url={source.sourceUrl} /></div>
              <p className="mt-2 text-xs text-muted-foreground">Ngày lấy dữ liệu: {universityDate(source.retrievedAt)}</p>
            </li>
          ))}
        </ul>
      ) : <p className="text-sm text-muted-foreground">Chưa có bằng chứng nguồn theo từng thuộc tính. Nguồn tổng quan của bậc học, nếu có, được ghi ở mục ngành học.</p>}
      <div className="mt-4 flex flex-wrap gap-2">
        {items.length > 6 && <Button type="button" variant="outline" size="sm" onClick={() => setShowAll(!showAll)}>{showAll ? "Thu gọn nguồn" : `Xem tất cả ${items.length} nhóm nguồn`}</Button>}
        {referenceCount > 0 && <Button type="button" variant="ghost" size="sm" onClick={() => { setShowReference(!showReference); setShowAll(false); }}>{showReference ? "Ẩn bằng chứng tham khảo" : `Bằng chứng tham khảo (${referenceCount})`}</Button>}
      </div>
      {showReference && <p className="mt-3 text-xs leading-relaxed text-muted-foreground">Bằng chứng chỉ tham khảo có thể chứa thông tin mâu thuẫn, thiếu hoặc không phù hợp để áp dụng. Chúng không thay thế các số liệu đang hiển thị trên trang.</p>}
    </DetailSection>
  );
}
