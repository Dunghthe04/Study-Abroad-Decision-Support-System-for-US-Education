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
import { KnowledgeEmbedProgress } from "@/components/admin/knowledge-base/KnowledgeEmbedProgress";
import type { KnowledgeImportResult } from "@/types/knowledge-base";

export function KnowledgeImportResultDialog({ result, onClose }: { result: KnowledgeImportResult | null; onClose: () => void }) {
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
                {result.totalRows} dòng: thêm mới {result.created}, cập nhật {result.updated}, không đổi {result.unchanged}, lỗi {result.errors.length}.
              </DialogDescription>
            </DialogHeader>
            <DialogBody className="space-y-3">
              <KnowledgeEmbedProgress />
              {result.errors.length > 0 && (
                <ul className="max-h-80 space-y-1 overflow-y-auto rounded-md border border-border p-3 text-sm">
                  {result.errors.map((item, index) => (
                    <li key={`${item.rowNumber}-${index}`}>Dòng {item.rowNumber}: {item.message}</li>
                  ))}
                </ul>
              )}
            </DialogBody>
            <DialogFooter layout="single">
              <DialogCancel onClick={onClose}>Đóng</DialogCancel>
            </DialogFooter>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}