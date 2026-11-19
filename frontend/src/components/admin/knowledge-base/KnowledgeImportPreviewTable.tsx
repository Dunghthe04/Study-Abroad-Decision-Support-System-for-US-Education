"use client";

import { cn } from "cn";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { KnowledgeImportError, KnowledgeImportRow } from "@/types/knowledge-base";

const TABLE_CLASS =
  "border-separate border-spacing-0 [&_td]:border-r [&_td]:border-b [&_td]:border-border [&_td]:py-1.5 [&_td:last-child]:border-r-0 [&_th]:border-r [&_th]:border-b [&_th]:border-border [&_th]:bg-muted [&_th]:font-semibold [&_th:last-child]:border-r-0";

export const SCROLL_FRAME_CLASS =
  "flex min-h-0 flex-1 flex-col overflow-hidden rounded-md border border-border [&_[data-slot=table-container]]:min-h-0 [&_[data-slot=table-container]]:flex-1 [&_[data-slot=table-container]]:overflow-auto";

const ERROR_DISPLAY_LIMIT = 200;

function TextCell({ value, className }: { value: string | null; className?: string }) {
  if (value === null || value.trim() === "") return <span className="text-muted-foreground">—</span>;
  return (
    <div className={cn("max-w-48 truncate", className)} title={value}>
      {value}
    </div>
  );
}

/** Lỗi theo số dòng; KB chỉ có một sheet nên không cần ghép tên sheet vào khóa như SLM. */
export function KnowledgeImportPreviewTable({ rows, messagesByRow }: { rows: KnowledgeImportRow[]; messagesByRow: Map<number, string[]> }) {
  return (
    <div className={SCROLL_FRAME_CLASS}>
      <Table aria-label="Xem trước dữ liệu nhập" className={TABLE_CLASS}>
        <TableHeader className="sticky top-0 z-10 bg-card">
          <TableRow>
            <TableHead scope="col" className="text-right">Dòng</TableHead>
            <TableHead scope="col" className="text-right">Mã</TableHead>
            <TableHead scope="col">Nhãn</TableHead>
            <TableHead scope="col">Bậc học</TableHead>
            <TableHead scope="col">Câu hỏi</TableHead>
            <TableHead scope="col">Câu trả lời</TableHead>
            <TableHead scope="col">Nguồn</TableHead>
            <TableHead scope="col">Kiểm tra</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((row) => {
            const messages = messagesByRow.get(row.rowNumber);
            return (
              <TableRow key={row.rowNumber}>
                <TableCell className="tabular text-right">{row.rowNumber}</TableCell>
                <TableCell className="tabular text-right">{row.id ?? "—"}</TableCell>
                <TableCell><TextCell value={row.label} /></TableCell>
                <TableCell><TextCell value={row.level} className="max-w-32" /></TableCell>
                <TableCell><TextCell value={row.question} className="max-w-72" /></TableCell>
                <TableCell><TextCell value={row.answer} className="max-w-72" /></TableCell>
                <TableCell><TextCell value={row.source} /></TableCell>
                <TableCell>
                  {messages ? (
                    <div className="max-w-64 truncate text-risk" title={messages.join("\n")}>{messages.join("; ")}</div>
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

export function KnowledgeImportErrorTable({ errors }: { errors: KnowledgeImportError[] }) {
  const shown = errors.slice(0, ERROR_DISPLAY_LIMIT);
  return (
    <>
      <div className={SCROLL_FRAME_CLASS}>
        <Table aria-label="Danh sách lỗi" className={TABLE_CLASS}>
          <TableHeader className="sticky top-0 z-10 bg-card">
            <TableRow>
              <TableHead scope="col" className="text-right">Dòng</TableHead>
              <TableHead scope="col">Lỗi</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {shown.map((item, index) => (
              <TableRow key={`${item.rowNumber}-${index}`}>
                <TableCell className="tabular text-right">{item.rowNumber}</TableCell>
                <TableCell className="whitespace-normal">Dòng {item.rowNumber}: {item.message}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {errors.length > shown.length && (
        <p className="shrink-0 text-xs text-muted-foreground">Hiển thị {shown.length} / {errors.length} lỗi đầu tiên.</p>
      )}
    </>
  );
}