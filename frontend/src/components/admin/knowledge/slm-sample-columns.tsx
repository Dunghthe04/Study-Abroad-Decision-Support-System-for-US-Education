import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { sampleTypeLabel, statusBadgeVariant, statusLabel } from "@/lib/slm-sample-format";
import type { SlmSample } from "@/types/slm-sample";

interface ColumnHandlers {
  selectedIds: Set<string>;
  onToggle: (id: string, checked: boolean) => void;
  onEdit: (sample: SlmSample) => void;
  onDelete: (sample: SlmSample) => void;
}

export function slmSampleColumns({ selectedIds, onToggle, onEdit, onDelete }: ColumnHandlers): DataTableColumn<SlmSample>[] {
  return [
    {
      id: "select",
      label: "Chọn",
      required: true,
      headerClassName: "w-12 text-center",
      cellClassName: "w-12 text-center",
      render: (sample) => (
        <span onClick={(event) => event.stopPropagation()}>
          <Checkbox
            checked={selectedIds.has(sample.id)}
            onCheckedChange={(checked) => onToggle(sample.id, checked === true)}
            aria-label="Chọn mẫu"
          />
        </span>
      ),
    },
    {
      id: "question",
      label: "Câu hỏi",
      required: true,
      cellClassName: "max-w-md whitespace-normal",
      render: (sample) => (
        <>
          <button
            type="button"
            className="line-clamp-2 text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-ring"
            onClick={(event) => {
              event.stopPropagation();
              onEdit(sample);
            }}
          >
            {sample.question}
          </button>
          {sample.reviewNote && <div className="text-xs text-muted-foreground">Ghi chú: {sample.reviewNote}</div>}
        </>
      ),
    },
    {
      id: "sampleType",
      label: "Loại mẫu",
      cellClassName: "whitespace-nowrap",
      render: (sample) => sampleTypeLabel(sample.sampleType),
    },
    {
      id: "split",
      label: "Tập",
      render: (sample) => <Badge variant="outline">{sample.split}</Badge>,
    },
    {
      id: "reviewStatus",
      label: "Trạng thái",
      render: (sample) => <Badge variant={statusBadgeVariant(sample.reviewStatus)}>{statusLabel(sample.reviewStatus)}</Badge>,
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-32",
      render: (sample) => (
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={(event) => {
            event.stopPropagation();
            onDelete(sample);
          }}
        >
          Xóa
        </Button>
      ),
    },
  ];
}
