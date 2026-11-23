"use client";

import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { KnowledgeEmbedProgress } from "@/components/admin/knowledge-base/KnowledgeEmbedProgress";
import type { KnowledgeImportResult } from "@/types/knowledge-base";

export function KnowledgeImportResultDialog({ result, onClose }: { result: KnowledgeImportResult | null; onClose: () => void }) {
  return (
    <Dialog open={result !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl">
        {result ? (
          <>
            <DialogHeader>
              <DialogTitle>Kết quả nhập file</DialogTitle>
              <DialogDescription>
                {result.totalRows} dòng: thêm mới {result.created}, cập nhật {result.updated}, không đổi {result.unchanged}, bỏ qua chờ duyệt {result.pending}, lỗi {result.errors.length}.
              </DialogDescription>
            </DialogHeader>
            <KnowledgeEmbedProgress />
            {result.errors.length > 0 && (
              <ul className="max-h-80 space-y-1 overflow-y-auto rounded-md border border-border p-3 text-sm">
                {result.errors.map((item, index) => (
                  <li key={`${item.rowNumber}-${index}`}>Dòng {item.rowNumber}: {item.message}</li>
                ))}
              </ul>
            )}
            <DialogFooter>
              <Button type="button" onClick={onClose}>Đóng</Button>
            </DialogFooter>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}