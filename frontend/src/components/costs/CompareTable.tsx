import { RANK_LABEL, type CellRank } from "@/lib/cost-format";
import { cn } from "@/lib/utils";

/**
 * Bảng so sánh dùng chung cho chi phí trường và chi phí bang: lưới viền mảnh, góc vuông, chữ căn trái.
 * Cột đầu là nhãn dòng (tên đậm, dòng phụ nhạt); ô tốt nhất trong hàng đậm và đổ xám, không dùng màu xanh đỏ.
 */
const LINE = "border-r border-b border-ink/70";

export function CompareTable({ children, minWidth = 560 }: { children: React.ReactNode; minWidth?: number }) {
  return (
    <div className="overflow-x-auto">
      <table className={"w-full border-separate border-spacing-0 border-t border-l border-ink/70 bg-surface text-sm"} style={{ minWidth }}>
        {children}
      </table>
    </div>
  );
}

export function HeadCell({ children, className }: { children?: React.ReactNode; className?: string }) {
  return (
    <th scope="col" className={cn(LINE, "px-4 py-3.5 text-left align-top font-semibold text-ink", className)}>
      {children}
    </th>
  );
}

/** Ô góc trên-trái: cố định cùng cột nhãn dòng để không trôi khi cuộn ngang. */
export function CornerCell({ children }: { children?: React.ReactNode }) {
  return (
    <th scope="col" className={cn(LINE, "sticky left-0 z-10 w-[26%] min-w-[9rem] bg-surface px-4 py-3.5 text-left align-top text-sm font-normal text-ink-2")}>
      {children}
    </th>
  );
}

export function RowHead({ title, sub, className }: { title: string; sub?: string; className?: string }) {
  return (
    <th scope="row" className={cn(LINE, "sticky left-0 z-10 w-[26%] min-w-[9rem] bg-surface px-4 py-3.5 text-left align-top", className)}>
      <span className="block font-semibold text-ink">{title}</span>
      {sub && <span className="mt-0.5 block text-sm font-normal text-ink-2">{sub}</span>}
    </th>
  );
}

export function Cell({ rank, className, children }: { rank?: CellRank; className?: string; children: React.ReactNode }) {
  return (
    <td className={cn(LINE, "px-4 py-3.5 text-left align-top tabular-nums", rank === "best" && "bg-ink/10 font-semibold", className)}>
      {children}
      {rank && <span className="sr-only"> ({RANK_LABEL[rank]})</span>}
    </td>
  );
}
