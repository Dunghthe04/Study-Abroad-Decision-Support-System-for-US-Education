import { Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { InlineSelectCell } from "@/components/admin/shared/InlineSelectCell";
import { SAMPLE_TYPE_OPTIONS, SPLIT_OPTIONS } from "@/lib/slm-sample-format";
import type { SaveSlmSampleRequest, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

interface ColumnHandlers {
  selectedIds: Set<string>;
  onToggle: (id: string, checked: boolean) => void;
  onEdit: (sample: SlmSample) => void;
  onDelete: (sample: SlmSample) => void;
  onChangeField: (sample: SlmSample, change: Partial<SaveSlmSampleRequest>) => Promise<void>;
}

export function slmSampleColumns({ selectedIds, onToggle, onEdit, onDelete, onChangeField }: ColumnHandlers): DataTableColumn<SlmSample>[] {
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
      headerClassName: "w-48",
      cellClassName: "w-48",
      render: (sample) => (
        <InlineSelectCell
          value={sample.sampleType}
          options={SAMPLE_TYPE_OPTIONS}
          label="Đổi loại mẫu"
          onChange={(value) => onChangeField(sample, { sampleType: value as SlmSampleType })}
        />
      ),
    },
    {
      id: "split",
      label: "Tập",
      headerClassName: "w-32",
      cellClassName: "w-32",
      render: (sample) => (
        <InlineSelectCell
          value={sample.split}
          options={SPLIT_OPTIONS}
          label="Đổi tập dữ liệu"
          onChange={(value) => onChangeField(sample, { split: value as SlmSplit })}
        />
      ),
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-28",
      render: (sample) => (
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={(event) => {
            event.stopPropagation();
            onDelete(sample);
          }}
        >
          <Trash2Icon />
          Xóa
        </Button>
      ),
    },
  ];
}
