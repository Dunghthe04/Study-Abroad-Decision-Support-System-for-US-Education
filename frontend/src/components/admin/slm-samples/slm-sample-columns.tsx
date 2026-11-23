import { Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { SlmStatusBadge } from "@/components/admin/slm-samples/SlmStatusBadge";
import { InlineSelectCell } from "@/components/admin/shared/InlineSelectCell";
import { formatDateTime, SAMPLE_TYPE_OPTIONS, SPLIT_OPTIONS } from "@/lib/slm-sample-format";
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
          {sample.reviewNote && (
            <div className="text-xs text-muted-foreground">{sample.reviewStatus === "approved" ? "Ghi chú" : "Lý do"}: {sample.reviewNote}</div>
          )}
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
      id: "creator",
      label: "Người tạo",
      headerClassName: "w-36",
      cellClassName: "w-36 whitespace-normal",
      render: (sample) => sample.createdByName ?? "—",
    },
    {
      id: "createdAt",
      label: "Ngày tạo",
      headerClassName: "w-40",
      cellClassName: "w-40",
      render: (sample) => formatDateTime(sample.createdAt),
    },
    {
      id: "reviewStatus",
      label: "Trạng thái",
      headerClassName: "w-32",
      cellClassName: "w-32",
      render: (sample) => <SlmStatusBadge status={sample.reviewStatus} />,
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-20 text-center",
      cellClassName: "w-20 text-center",
      render: (sample) => (
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label="Xóa mẫu"
          title="Xóa mẫu"
          className="bg-transparent text-destructive hover:bg-destructive/10 hover:text-destructive"
          onClick={(event) => {
            event.stopPropagation();
            onDelete(sample);
          }}
        >
          <Trash2Icon />
        </Button>
      ),
    },
  ];
}
