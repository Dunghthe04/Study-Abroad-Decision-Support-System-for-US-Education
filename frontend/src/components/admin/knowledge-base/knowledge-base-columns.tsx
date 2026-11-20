import { ArchiveRestoreIcon, Trash2Icon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { SlmStatusBadge } from "@/components/admin/slm-samples/SlmStatusBadge";
import { firstLine, formatDateTime, labelName, levelName } from "@/lib/knowledge-base-format";
import type { KnowledgeEntry } from "@/types/knowledge-base";

interface ColumnHandlers {
  selectedIds: Set<string>;
  onToggle: (id: string, checked: boolean) => void;
  onOpen: (entry: KnowledgeEntry) => void;
  onDelete: (entry: KnowledgeEntry) => void;
  onRestore: (entry: KnowledgeEntry) => void;
}

export function knowledgeBaseColumns({ selectedIds, onToggle, onOpen, onDelete, onRestore }: ColumnHandlers): DataTableColumn<KnowledgeEntry>[] {
  return [
    {
      id: "select",
      label: "Chọn",
      required: true,
      headerClassName: "w-12 text-center",
      cellClassName: "w-12 text-center",
      render: (entry) => (
        // stopPropagation: tích ô chọn không mở panel chi tiết của dòng
        <span onClick={(event) => event.stopPropagation()}>
          <Checkbox checked={selectedIds.has(entry.id)} onCheckedChange={(checked) => onToggle(entry.id, checked === true)} aria-label={`Chọn tri thức #${entry.entryNo}`} />
        </span>
      ),
    },
    {
      id: "entryNo",
      label: "#",
      required: true,
      headerClassName: "w-16 text-right",
      cellClassName: "w-16 text-right tabular",
      render: (entry) => entry.entryNo,
    },
    {
      id: "label",
      label: "Nhãn",
      headerClassName: "w-40",
      cellClassName: "w-40 whitespace-normal",
      render: (entry) => labelName(entry.label),
    },
    {
      id: "levels",
      label: "Bậc học",
      headerClassName: "w-36",
      cellClassName: "w-36 whitespace-normal",
      render: (entry) => (
        <span className="flex flex-wrap gap-1">
          {entry.levels.length === 0 ? "—" : entry.levels.map((level) => <Badge key={level} variant="neutral">{levelName(level)}</Badge>)}
        </span>
      ),
    },
    {
      id: "question",
      label: "Câu hỏi",
      required: true,
      cellClassName: "max-w-md whitespace-normal",
      render: (entry) => (
        <>
          <button
            type="button"
            className="line-clamp-2 text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-ring"
            onClick={(event) => {
              event.stopPropagation();
              onOpen(entry);
            }}
          >
            {entry.question}
          </button>
          {entry.origin === "chat-unanswered" && <Badge variant="reach" className="mt-1">Từ chat chưa trả lời</Badge>}
          {entry.reviewNote && <div className="text-xs text-muted-foreground">{entry.reviewStatus === "approved" ? "Ghi chú" : "Lý do"}: {entry.reviewNote}</div>}
        </>
      ),
    },
    {
      id: "source",
      label: "Nguồn",
      headerClassName: "w-48",
      cellClassName: "w-48 whitespace-normal",
      render: (entry) => <span className="line-clamp-2" title={entry.source ?? undefined}>{firstLine(entry.source)}</span>,
    },
    {
      id: "reviewStatus",
      label: "Trạng thái",
      headerClassName: "w-32",
      cellClassName: "w-32",
      render: (entry) => <SlmStatusBadge status={entry.reviewStatus} />,
    },
    {
      id: "updatedAt",
      label: "Cập nhật",
      headerClassName: "w-40",
      cellClassName: "w-40",
      render: (entry) => formatDateTime(entry.updatedAt ?? entry.createdAt),
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-20 text-center",
      cellClassName: "w-20 text-center",
      render: (entry) =>
        entry.deletedAt ? (
          <Button type="button" variant="ghost" size="icon-sm" aria-label="Khôi phục" title="Khôi phục" onClick={(event) => { event.stopPropagation(); onRestore(entry); }}>
            <ArchiveRestoreIcon />
          </Button>
        ) : (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Xóa tri thức"
            title="Xóa tri thức"
            className="bg-transparent text-destructive hover:bg-destructive/10 hover:text-destructive"
            onClick={(event) => {
              event.stopPropagation();
              onDelete(entry);
            }}
          >
            <Trash2Icon />
          </Button>
        ),
    },
  ];
}