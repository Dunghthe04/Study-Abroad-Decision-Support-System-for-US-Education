"use client";

import { AlertTriangleIcon, CheckCircle2Icon } from "lucide-react";
import {
  Dialog,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { SlmImportResult } from "@/types/slm-sample";

export function SlmImportResultDialog({ result, onClose }: { result: SlmImportResult | null; onClose: () => void }) {
  return (
    <Dialog open={result !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent variant="form" size="md">
        {result ? (
          <>
            <DialogHeader>
              <DialogIcon tone={result.errors.length > 0 ? "warning" : "success"}>
                {result.errors.length > 0 ? <AlertTriangleIcon /> : <CheckCircle2Icon />}
              </DialogIcon>
              <DialogTitle>Kết quả nhập file</DialogTitle>
              <DialogDescription>
                {result.totalRows} dòng: thêm mới {result.inserted}, trùng {result.duplicates}, lỗi {result.errors.length}.
              </DialogDescription>
            </DialogHeader>
            {result.errors.length > 0 ? (
              <DialogBody>
                <div className="overflow-x-auto rounded-md border border-line">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Sheet</TableHead>
                        <TableHead>Dòng</TableHead>
                        <TableHead>Lỗi</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {result.errors.map((item, index) => (
                        <TableRow key={`${item.sheetName}-${item.rowNumber}-${index}`}>
                          <TableCell>{item.sheetName}</TableCell>
                          <TableCell className="tabular">{item.rowNumber}</TableCell>
                          <TableCell className="whitespace-normal">{item.message}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </DialogBody>
            ) : null}
            <DialogFooter layout="single">
              <DialogCancel onClick={onClose}>Đóng</DialogCancel>
            </DialogFooter>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
