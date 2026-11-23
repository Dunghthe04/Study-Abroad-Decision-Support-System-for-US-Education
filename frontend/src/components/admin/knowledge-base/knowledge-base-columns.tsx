import { Trash2Icon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { docTypeName, firstLine, formatDateTime, labelName } from "@/lib/knowledge-base-format";
import type { KnowledgeEntry } from "@/types/knowledge-base";

interface ColumnHandlers {
  onOpen: (entry: KnowledgeEntry) => void;
  onDelete: (entry: KnowledgeEntry) => void;
}

export function knowledgeBaseColumns({ onOpen, onDelete }: ColumnHandlers): DataTableColumn<KnowledgeEntry>[] {
  return [
    {
      id: "entryNo",
      label: "#",
      required: true,
      headerClassName: "w-16 text-right",
      cellClassName: "w-16 text-right tabular",
      render: (entry) => entry.entryNo,
    },
    {
      id: "docType",
      label: "Loại",
      headerClassName: "w-44",
      cellClassName: "w-44 whitespace-normal",
      render: (entry) => <Badge variant="neutral">{docTypeName(entry.docType)}</Badge>,
    },
    {
      id: "label",
      label: "Nhãn",
      headerClassName: "w-40",
      cellClassName: "w-40 whitespace-normal",
      render: (entry) => labelName(entry.label),
    },
    {
      id: "question",
      label: "Câu hỏi",
      required: true,
      cellClassName: "max-w-md whitespace-normal",
      render: (entry) => (
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
      ),
    },
    {
      id: "source",
      label: "Nguồn",
      headerClassName: "w-48",
      cellClassName: "w-48 whitespace-normal",
      render: (entry) => <span className="line-clamp-2" title={entry.source ?? entry.sourceUrl ?? undefined}>{entry.source ? firstLine(entry.source) : (entry.sourceUrl ?? "—")}</span>,
    },
    {
      id: "topicCode",
      label: "Mã chủ đề",
      headerClassName: "w-28",
      cellClassName: "w-28",
      render: (entry) => entry.topicCode ?? "—",
    },
    {
      id: "retrievedAt",
      label: "Ngày thu thập",
      headerClassName: "w-32",
      cellClassName: "w-32 tabular",
      render: (entry) => entry.retrievedAt,
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
      render: (entry) => (
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
