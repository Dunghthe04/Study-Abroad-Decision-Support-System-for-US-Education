"use client";

import { cn } from "cn";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { SlmImportError, SlmImportRow } from "@/types/slm-sample";

const TABLE_CLASS =
  "border-separate border-spacing-0 [&_td]:border-r [&_td]:border-b [&_td]:border-border [&_td]:py-1.5 [&_td:last-child]:border-r-0 [&_th]:border-r [&_th]:border-b [&_th]:border-border [&_th]:bg-muted [&_th]:font-semibold [&_th:last-child]:border-r-0";

export const SCROLL_FRAME_CLASS =
  "flex min-h-0 flex-1 flex-col overflow-hidden rounded-md border border-border [&_[data-slot=table-container]]:min-h-0 [&_[data-slot=table-container]]:flex-1 [&_[data-slot=table-container]]:overflow-auto";

const ERROR_DISPLAY_LIMIT = 200;

export function importRowKey(sheetName: string, rowNumber: number): string {
  return `${sheetName}|${rowNumber}`;
}

function TextCell({ value, className }: { value: string | null; className?: string }) {
  if (value === null || value.trim() === "") {
    return <span className="text-muted-foreground">—</span>;
  }
  return (
    <div className={cn("max-w-48 truncate", className)} title={value}>
      {value}
    </div>
  );
}

interface PreviewTableProps {
  rows: SlmImportRow[];
  messagesByRow: Map<string, string[]>;
}

export function SlmImportPreviewTable({ rows, messagesByRow }: PreviewTableProps) {
  return (
    <div className={SCROLL_FRAME_CLASS}>
      <Table aria-label="Xem trước dữ liệu nhập" className={TABLE_CLASS}>
        <TableHeader className="sticky top-0 z-10 bg-card">
          <TableRow>
            <TableHead scope="col">Sheet</TableHead>
            <TableHead scope="col" className="text-right">Dòng</TableHead>
            <TableHead scope="col">Câu hỏi</TableHead>
            <TableHead scope="col">Hồ sơ</TableHead>
            <TableHead scope="col">Câu hệ thống đã hỏi</TableHead>
            <TableHead scope="col">Ý định</TableHead>
            <TableHead scope="col">Ngữ cảnh</TableHead>
            <TableHead scope="col">Tập</TableHead>
            <TableHead scope="col">Nhãn rà soát</TableHead>
            <TableHead scope="col">Kiểm tra</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row, index) => {
            const messages = messagesByRow.get(importRowKey(row.sheetName, row.rowNumber));
            return (
              <TableRow key={`${row.sheetName}-${row.rowNumber}-${index}`}>
                <TableCell>{row.sheetName}</TableCell>
                <TableCell className="tabular text-right">{row.rowNumber}</TableCell>
                <TableCell><TextCell value={row.question} className="max-w-72" /></TableCell>
                <TableCell><TextCell value={row.profile} /></TableCell>
                <TableCell><TextCell value={row.pendingQuestion} /></TableCell>
                <TableCell><TextCell value={row.intents} /></TableCell>
                <TableCell><TextCell value={row.context} /></TableCell>
                <TableCell><TextCell value={row.split} className="max-w-24" /></TableCell>
                <TableCell><TextCell value={row.sourceReview} /></TableCell>
                <TableCell>
                  {messages ? (
                    <div className="max-w-64 truncate text-risk" title={messages.join("\n")}>
                      {messages.join("; ")}
                    </div>
                  ) : (
                    <Badge variant="ok">Hợp lệ</Badge>
                  )}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}

export function SlmImportErrorTable({ errors }: { errors: SlmImportError[] }) {
  const shown = errors.slice(0, ERROR_DISPLAY_LIMIT);
  return (
    <>
      <div className={SCROLL_FRAME_CLASS}>
        <Table aria-label="Danh sách lỗi" className={TABLE_CLASS}>
          <TableHeader className="sticky top-0 z-10 bg-card">
            <TableRow>
              <TableHead scope="col">Sheet</TableHead>
              <TableHead scope="col" className="text-right">Dòng</TableHead>
              <TableHead scope="col">Lỗi</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {shown.map((item, index) => (
              <TableRow key={`${item.sheetName}-${item.rowNumber}-${index}`}>
                <TableCell>{item.sheetName}</TableCell>
                <TableCell className="tabular text-right">{item.rowNumber > 0 ? item.rowNumber : "—"}</TableCell>
                <TableCell className="whitespace-normal">{item.message}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {errors.length > shown.length && (
        <p className="shrink-0 text-xs text-muted-foreground">
          Hiển thị {shown.length} / {errors.length} lỗi đầu tiên.
        </p>
      )}
    </>
  );
}
