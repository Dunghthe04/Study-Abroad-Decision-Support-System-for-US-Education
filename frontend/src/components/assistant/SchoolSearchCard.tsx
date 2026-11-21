"use client";

// Thẻ danh sách trường khi học sinh tìm theo điều kiện: số liệu thật từ cơ sở dữ liệu (code dựng).
// Chỉ hiện vài trường đầu theo thứ tự xếp; mỗi trường có nút "Xem chi tiết" (trang trường),
// dưới danh sách là nút "Xem tất cả" mở trang /universities với đúng điều kiện và cách xếp đã dùng.

import Link from "next/link";
import { formatPct, formatUsd } from "@/lib/recommendation-labels";
import { universitiesListHref, universitiesListLabel } from "@/lib/school-search-link";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type {
  SchoolSearchData,
  SchoolSearchItem,
} from "@/types/recommendation";

const CONTROL: Record<string, string> = {
  public: "Công lập",
  private: "Tư thục",
};
const pct = (fraction: number) => formatPct(fraction);

// Lọc "không bắt buộc SAT, ACT" mà dữ liệu trường ghi điểm thi quan trọng: nêu cả hai thông tin, không suy ra nghĩa
const TEST_CAUTION: Record<string, string> = {
  very_important: "Không bắt buộc SAT, ACT, nhưng trường ghi điểm thi là yếu tố rất quan trọng",
  important: "Không bắt buộc SAT, ACT, nhưng trường ghi điểm thi là yếu tố quan trọng",
};

function Row({
  index,
  school,
  sort,
  testPolicy,
}: {
  index: number;
  school: SchoolSearchItem;
  sort?: string | null;
  testPolicy?: string | null;
}) {
  const tuitionSort = sort === "lowestTuition" || sort === "highestTuition";
  const totalSort = sort === "lowestCost" || sort === "highestCost";
  const place = [school.city, school.state].filter(Boolean).join(", ");
  const control = school.control ? CONTROL[school.control] : null;
  return (
    <li className="space-y-0.5 border-b border-line/60 py-2 last:border-b-0">
      <p className="text-[13px] font-bold text-foreground">
        {index}. {school.name}
      </p>
      {(place || control) && (
        <p className="text-xs text-ink-2">
          {[place, control].filter(Boolean).join(" · ")}
        </p>
      )}
      {/* Luôn hiện cả học phí và tổng chi phí cạnh nhau để học sinh không nhầm hai số; số dùng để xếp được in đậm */}
      <p className="text-xs text-ink-2">
        <span className={tuitionSort ? "font-semibold text-foreground" : undefined}>
          {school.tuitionUsd != null ? `Học phí ${formatUsd(school.tuitionUsd)}` : "Chưa có học phí"}
        </span>
        {" · "}
        <span className={totalSort ? "font-semibold text-foreground" : undefined}>
          {school.costUsd != null ? `Tổng chi phí ${formatUsd(school.costUsd)}/năm` : "Chưa có tổng chi phí"}
        </span>
      </p>
      <p className="text-xs text-ink-2">
        {[
          school.acceptRate != null ? `Tỷ lệ nhận ${pct(school.acceptRate)}` : null,
          school.bestRank != null ? `Hạng #${school.bestRank}` : null,
        ]
          .filter(Boolean)
          .join(" · ")}
      </p>
      {testPolicy === "optional" && school.testImportance && TEST_CAUTION[school.testImportance] && (
        <p className="text-xs text-warn">{TEST_CAUTION[school.testImportance]}</p>
      )}
      <div className="pt-1">
        <Link
          href={`/university/${encodeURIComponent(school.code)}`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Xem chi tiết
        </Link>
      </div>
    </li>
  );
}

export function SchoolSearchCard({ data }: { data: SchoolSearchData }) {
  const shown = data.items.slice(0, data.visible);
  const more = data.total - shown.length;
  return (
    <div className="mt-2.5 space-y-2 rounded-xl border border-line bg-surface p-3 text-sm">
      <div className="space-y-0.5">
        <p className="text-[13px] font-bold text-brand">
          Có {data.total} trường {data.summary}
        </p>
        <p className="text-xs text-ink-3">Xếp theo {data.sortLabel}</p>
        {data.note && <p className="text-xs text-ink-2">{data.note}</p>}
        {(data.sort === "lowestCost" || data.sort === "highestCost" || data.sort === "lowestTuition" || data.sort === "highestTuition") && (
          <p className="text-xs text-ink-3">Tổng chi phí = học phí + ăn ở + sinh hoạt + phí khác (ước tính của từng trường).</p>
        )}
      </div>
      <ol className="list-none">
        {shown.map((school, i) => (
          <Row key={school.code} index={i + 1} school={school} sort={data.sort} testPolicy={data.filters?.testPolicy} />
        ))}
      </ol>
      {more > 0 && (
        <Link
          href={universitiesListHref(data)}
          className={cn(buttonVariants({ variant: "outline", size: "sm" }), "text-xs font-semibold text-brand hover:bg-brand-wash")}
        >
          {universitiesListLabel(data)}
        </Link>
      )}
    </div>
  );
}
