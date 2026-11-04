"use client";

import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

interface TablePaginationProps {
  page: number;
  totalPages: number;
  totalCount?: number;
  itemLabel?: string;
  disabled?: boolean;
  onPageChange: (page: number) => void;
}

export function TablePagination({ page, totalPages, totalCount, itemLabel = "bản ghi", disabled = false, onPageChange }: TablePaginationProps) {
  return (
    <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 text-sm text-muted-foreground">
      <span>{totalCount === undefined ? "—" : `${totalCount} ${itemLabel}`}</span>
      <div className="flex items-center gap-2">
        <Button type="button" variant="outline" size="sm" disabled={disabled || page <= 1} onClick={() => onPageChange(page - 1)}>
          <ChevronLeftIcon /> Trước
        </Button>
        <span>Trang {page} / {Math.max(totalPages, 1)}</span>
        <Button type="button" variant="outline" size="sm" disabled={disabled || page >= totalPages} onClick={() => onPageChange(page + 1)}>
          Sau <ChevronRightIcon />
        </Button>
      </div>
    </div>
  );
}
