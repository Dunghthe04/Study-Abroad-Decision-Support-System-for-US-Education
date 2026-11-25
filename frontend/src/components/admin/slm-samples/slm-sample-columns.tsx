import { RotateCcwIcon, Trash2Icon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { DataTableColumn } from "@/components/admin/shared/DataTable";
import { InlineSelectCell } from "@/components/admin/shared/InlineSelectCell";
import { formatDateTime, SAMPLE_TYPE_OPTIONS, sampleTypeLabel, SPLIT_OPTIONS } from "@/lib/slm-sample-format";
import type { SaveSlmSampleRequest, SlmSample, SlmSampleType, SlmSplit } from "@/types/slm-sample";

interface ColumnHandlers {
  deleted: boolean;
  onEdit: (sample: SlmSample) => void;
  onDelete: (sample: SlmSample) => void;
  onRestore: (sample: SlmSample) => void;
  onChangeField: (sample: SlmSample, change: Partial<SaveSlmSampleRequest>) => Promise<void>;
}

export function slmSampleColumns({ deleted, onEdit, onDelete, onRestore, onChangeField }: ColumnHandlers): DataTableColumn<SlmSample>[] {
  return [
    {
      id: "question",
      label: "Câu hỏi",
      required: true,
      cellClassName: "max-w-md whitespace-normal",
      render: (sample) => (
        <button
          type="button"
          className="line-clamp-2 cursor-pointer rounded-sm text-left font-medium text-brand underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          onClick={(event) => {
            event.stopPropagation();
            onEdit(sample);
          }}
        >
          {sample.question}
        </button>
      ),
    },
    {
      id: "sampleType",
      label: "Loại mẫu",
      headerClassName: "w-48",
      cellClassName: "w-48",
      render: (sample) =>
        deleted ? (
          <Badge variant="neutral">{sampleTypeLabel(sample.sampleType)}</Badge>
        ) : (
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
      render: (sample) =>
        deleted ? (
          sample.split
        ) : (
          <InlineSelectCell
            value={sample.split}
            options={SPLIT_OPTIONS}
            label="Đổi tập dữ liệu"
            onChange={(value) => onChangeField(sample, { split: value as SlmSplit })}
          />
        ),
    },
    {
      id: "version",
      label: "Phiên bản",
      headerClassName: "w-24 text-right",
      cellClassName: "w-24 text-right tabular",
      render: (sample) => `v${sample.version}`,
    },
    {
      id: "creator",
      label: "Người tạo",
      headerClassName: "w-36",
      cellClassName: "w-36 whitespace-normal",
      render: (sample) =>
        sample.createdByName ?? <span title="Chưa có dữ liệu" aria-label="Chưa có dữ liệu" className="text-muted-foreground">—</span>,
    },
    {
      id: "createdAt",
      label: "Ngày tạo",
      headerClassName: "w-40",
      cellClassName: "w-40",
      render: (sample) => formatDateTime(sample.createdAt),
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-20 text-center",
      cellClassName: "w-20 text-center",
      render: (sample) =>
        deleted ? (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Khôi phục mẫu"
            title="Khôi phục mẫu"
            onClick={(event) => {
              event.stopPropagation();
              onRestore(sample);
            }}
          >
            <RotateCcwIcon />
          </Button>
        ) : (
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
