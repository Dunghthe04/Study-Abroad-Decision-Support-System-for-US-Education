import { SlmStatusBadge } from "@/components/admin/knowledge/SlmStatusBadge";
import { TableFrame } from "@/components/admin/shared/EditableTable";
import { fieldLabels, formatDateTime, historyActionLabel } from "@/lib/slm-sample-format";
import type { SlmSampleHistoryItem } from "@/types/slm-sample";

function Timeline({ items, emptyText }: { items: SlmSampleHistoryItem[]; emptyText: string }) {
  if (items.length === 0) return <p className="border-t border-border px-3 py-2 text-sm text-muted-foreground">{emptyText}</p>;
  return (
    <ol className="divide-y divide-border border-t border-border">
      {items.map((item) => (
        <li key={item.id} className="space-y-1 px-3 py-2 text-sm">
          <div className="flex flex-wrap items-center justify-between gap-x-2">
            <span className="font-medium">{historyActionLabel(item.action)}</span>
            <time className="text-xs text-muted-foreground" dateTime={item.createdAt}>{formatDateTime(item.createdAt)}</time>
          </div>
          <div className="text-xs text-muted-foreground">{item.actorName ?? "Hệ thống"}</div>
          {item.fromStatus !== item.toStatus && item.toStatus && (
            <div className="flex items-center gap-1.5 text-xs">
              {item.fromStatus ? <SlmStatusBadge status={item.fromStatus} /> : <span className="text-muted-foreground">Mới</span>}
              <span aria-hidden>→</span>
              <SlmStatusBadge status={item.toStatus} />
            </div>
          )}
          {item.changedFields.length > 0 && <div className="text-xs">Đã sửa: {fieldLabels(item.changedFields)}</div>}
          {item.note && <div className="whitespace-pre-wrap break-words text-xs">Ghi chú: {item.note}</div>}
        </li>
      ))}
    </ol>
  );
}

export function SlmSampleHistory({ history }: { history: SlmSampleHistoryItem[] }) {
  return (
    <>
      <TableFrame title="Lịch sử chỉnh sửa">
        <Timeline items={history.filter((item) => item.action !== "review")} emptyText="Chưa có lịch sử." />
      </TableFrame>
      <TableFrame title="Lịch sử duyệt">
        <Timeline items={history.filter((item) => item.action === "review")} emptyText="Chưa được duyệt lần nào." />
      </TableFrame>
    </>
  );
}
