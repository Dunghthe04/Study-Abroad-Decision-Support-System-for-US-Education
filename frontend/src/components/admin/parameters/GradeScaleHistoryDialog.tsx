"use client";

import { useRef, useState } from "react";
import { HistoryIcon } from "lucide-react";
import { getGradeScaleHistory, type GradeScaleHistoryItem } from "@/lib/admin-grade-scale-api";
import type { PagedResult } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { TablePagination } from "@/components/admin/shared/TablePagination";

const PAGE_SIZE = 10;

export function formatTime(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleString("vi-VN");
}

// Điểm chữ không bắt buộc (bảng mod tự nhập có thể không có): có thì ghi kèm sau GPA, không có thì bỏ qua
function withLetter(letter: string | undefined): string {
  return letter ? `, ${letter}` : "";
}

// Tóm tắt một lần sửa theo mốc điểm: mốc thêm, mốc bỏ, mốc đổi GPA hoặc điểm chữ
function describeChange(item: GradeScaleHistoryItem): string[] {
  const oldRules = new Map((item.old?.rules ?? []).map((r) => [r.minScore, r]));
  const newRules = new Map((item.new?.rules ?? []).map((r) => [r.minScore, r]));
  const lines: string[] = [];
  if (!item.old) lines.push("Lưu bảng lần đầu (trước đó dùng bảng mặc định).");
  const mins = [...new Set([...oldRules.keys(), ...newRules.keys()])].sort((a, b) => b - a);
  for (const min of mins) {
    const before = oldRules.get(min);
    const after = newRules.get(min);
    if (before && !after) lines.push(`Bỏ mốc ${min} (GPA ${before.gpa4}${withLetter(before.letterGrade)}).`);
    else if (!before && after && item.old) lines.push(`Thêm mốc ${min}: GPA ${after.gpa4}${withLetter(after.letterGrade)}.`);
    else if (before && after && before.gpa4 !== after.gpa4) lines.push(`Mốc ${min}: GPA ${before.gpa4} → ${after.gpa4}.`);
    else if (before && after && (before.letterGrade ?? "") !== (after.letterGrade ?? ""))
      lines.push(`Mốc ${min}: điểm chữ "${before.letterGrade ?? ""}" → "${after.letterGrade ?? ""}".`);
  }
  if (item.old && item.new && item.old.sourceName !== item.new.sourceName)
    lines.push(`Nguồn: "${item.old.sourceName}" → "${item.new.sourceName}".`);
  if (lines.length === 0) lines.push("Lưu lại, không đổi mốc nào.");
  return lines;
}

/**
 * Nút "Xem lịch sử sửa bảng" và popup lịch sử, mỗi trang PAGE_SIZE lần sửa, mới nhất ở trên.
 * Chỉ tải khi mở popup; mỗi lần mở lại về trang 1 nên lần sửa vừa lưu luôn hiện ở đầu.
 */
export function GradeScaleHistoryDialog() {
  const [open, setOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<GradeScaleHistoryItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  // Bấm nhanh qua nhiều trang thì chỉ nhận kết quả của lần bấm cuối
  const lastRequest = useRef(0);

  const loadPage = async (target: number) => {
    const request = ++lastRequest.current;
    setPage(target);
    setLoading(true);
    setError(false);
    try {
      const result = await getGradeScaleHistory(target, PAGE_SIZE);
      if (request === lastRequest.current) setData(result);
    } catch {
      if (request === lastRequest.current) setError(true);
    } finally {
      if (request === lastRequest.current) setLoading(false);
    }
  };

  const changeOpen = (next: boolean) => {
    setOpen(next);
    if (next) void loadPage(1);
  };

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => changeOpen(true)}>
        <HistoryIcon /> Xem lịch sử sửa bảng
      </Button>
      <Dialog open={open} onOpenChange={changeOpen}>
        <DialogContent className="flex max-h-[85vh] flex-col sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Lịch sử sửa bảng quy đổi</DialogTitle>
            <DialogDescription>Tất cả các lần sửa, mới nhất ở trên.</DialogDescription>
          </DialogHeader>

          <div className="min-h-0 flex-1 overflow-y-auto pr-1">
            {error ? (
              <Alert variant="destructive">
                <AlertDescription>Không tải được lịch sử. Vui lòng thử lại sau.</AlertDescription>
              </Alert>
            ) : loading && !data ? (
              <div className="flex items-center gap-2 text-muted-foreground">
                <Spinner /> Đang tải lịch sử...
              </div>
            ) : !data || data.items.length === 0 ? (
              <p className="text-muted-foreground">Chưa có lần sửa nào.</p>
            ) : (
              <ul className="flex flex-col gap-4" aria-busy={loading}>
                {data.items.map((item) => (
                  <li key={`${item.changedAt}-${item.actorUserId}`} className="border-l-2 pl-3">
                    <p className="font-medium">
                      {formatTime(item.changedAt)} · {item.actorName ?? "Không rõ người sửa"}
                    </p>
                    <ul className="text-muted-foreground">
                      {describeChange(item).map((line) => (
                        <li key={line} className="break-words">
                          {line}
                        </li>
                      ))}
                    </ul>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <TablePagination
            page={page}
            totalPages={data?.totalPages ?? 0}
            totalCount={data?.totalCount}
            itemLabel="lần sửa"
            disabled={loading}
            onPageChange={(next) => void loadPage(next)}
          />
        </DialogContent>
      </Dialog>
    </>
  );
}
