"use client";

// Thẻ danh sách trường khi học sinh tìm theo điều kiện: số liệu thật từ cơ sở dữ liệu (code dựng).
// Hiện sẵn vài trường đầu theo thứ tự xếp, "Xem thêm" mở phần còn lại ngay trong thẻ (dữ liệu đã có sẵn, không gửi thêm tin).

import { useState } from "react";
import { formatPct, formatUsd } from "@/lib/recommendation-labels";
import { Button } from "@/components/ui/button";
import type {
  SchoolSearchData,
  SchoolSearchItem,
} from "@/types/recommendation";
import { RememberedLine } from "./RememberedLine";

const CONTROL: Record<string, string> = {
  public: "Công lập",
  private: "Tư thục",
};
const pct = (fraction: number) => formatPct(fraction);

function Row({ index, school }: { index: number; school: SchoolSearchItem }) {
  const place = [school.city, school.state].filter(Boolean).join(", ");
  const control = school.control ? CONTROL[school.control] : null;
  return (
    <li className="space-y-0.5 border-b border-line/60 py-2 last:border-b-0">
      <p className="text-[13px] font-bold text-foreground">
        {index}. {school.name}
      </p>
      {(place || control) && (
        <p className="text-[11px] text-ink-2">
          {[place, control].filter(Boolean).join(" · ")}
        </p>
      )}
      <p className="text-[11px] text-ink-2">
        {[
          school.costUsd != null
            ? `Chi phí ${formatUsd(school.costUsd)}/năm`
            : "Chưa có chi phí",
          school.acceptRate != null
            ? `Tỷ lệ nhận ${pct(school.acceptRate)}`
            : null,
          school.bestRank != null ? `Hạng #${school.bestRank}` : null,
        ]
          .filter(Boolean)
          .join(" · ")}
      </p>
    </li>
  );
}

export function SchoolSearchCard({
  data,
  remember = false,
}: {
  data: SchoolSearchData;
  remember?: boolean;
}) {
  const [expanded, setExpanded] = useState(false);
  const shown = expanded ? data.items : data.items.slice(0, data.visible);
  const hidden = data.items.length - data.visible;
  const clipped = data.total > data.items.length;
  return (
    <div className="mt-2.5 space-y-2 rounded-xl border border-line bg-surface p-3 text-sm">
      <div className="space-y-0.5">
        <p className="text-[13px] font-bold text-brand">
          Có {data.total} trường {data.summary}
        </p>
        <p className="text-[11px] text-ink-3">Xếp theo {data.sortLabel}</p>
      </div>
      <ol className="list-none">
        {shown.map((school, i) => (
          <Row key={school.code} index={i + 1} school={school} />
        ))}
      </ol>
      {hidden > 0 && (
        <Button
          variant="outline"
          size="sm"
          onClick={() => setExpanded((v) => !v)}
          className="text-xs font-semibold text-brand hover:bg-brand-wash"
        >
          {expanded ? "Thu gọn" : `Xem thêm ${hidden} trường`}
        </Button>
      )}
      {remember && (
        <RememberedLine
          about={`đang lọc ${data.summary}`}
          hint="Bạn nói thêm điều kiện, ví dụ “chỉ lấy công lập thôi”, hoặc đổi cách xếp, ví dụ “rẻ nhất lên đầu”."
        />
      )}
      {expanded && clipped && (
        <p className="text-[11px] text-ink-3">
          Còn {data.total - data.items.length} trường nữa, bạn thêm điều kiện
          (bang, ngành, loại trường) để thu hẹp danh sách.
        </p>
      )}
    </div>
  );
}
