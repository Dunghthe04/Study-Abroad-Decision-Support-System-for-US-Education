"use client";

import { useState } from "react";
import { CheckIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError, centerSuspensionApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { CenterSuspensionProposalDto, SuspensionProposalStatus } from "@/types/api";

const PAGE_SIZE = 20;

type StatusFilter = SuspensionProposalStatus | "all";

const STATUS_OPTIONS: { id: StatusFilter; label: string }[] = [
  { id: "pending", label: "Chờ quyết định" },
  { id: "approved", label: "Đã tạm ngừng" },
  { id: "rejected", label: "Đã từ chối" },
  { id: "all", label: "Tất cả" },
];

const STATUS_LABEL: Record<string, { label: string; variant: "warn" | "risk" | "neutral" }> = {
  pending: { label: "Chờ quyết định", variant: "warn" },
  approved: { label: "Đã tạm ngừng", variant: "risk" },
  rejected: { label: "Đã từ chối", variant: "neutral" },
};

/**
 * [USAS-2080 / UC-58] Đề nghị tạm ngừng trung tâm do Moderator gửi (trung tâm bị gỡ tin lặp lại).
 * Admin quyết định: đồng ý thì trung tâm chuyển sang trạng thái tạm ngừng, từ chối thì giữ nguyên. Moderator chỉ xem.
 */
export function SuspensionProposalsView() {
  const { user } = useAuth();
  const canDecide = user?.role === "admin";

  const [status, setStatus] = useState<StatusFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [decision, setDecision] = useState<{ proposal: CenterSuspensionProposalDto; approve: boolean } | null>(null);
  const [note, setNote] = useState("");
  const [isSending, setIsSending] = useState(false);

  const query = useFetched(`proposals|${status}|${page}|${version}`, () =>
    centerSuspensionApi.list({ status, page, pageSize: PAGE_SIZE }),
  );
  const list = query.data;

  const closeDialog = () => {
    if (isSending) return;
    setDecision(null);
    setNote("");
  };

  const confirm = async () => {
    if (!decision || isSending) return;
    const { proposal, approve } = decision;
    setIsSending(true);
    try {
      await centerSuspensionApi.decide(proposal.id, approve, note);
      toast.add({
        type: "success",
        title: approve ? "Đã tạm ngừng trung tâm" : "Đã từ chối đề nghị",
        description: approve ? `${proposal.studyCenterName} không còn nhận yêu cầu mới.` : "Trung tâm vẫn hoạt động bình thường.",
      });
      setDecision(null);
      setNote("");
      if (list && list.items.length === 1 && page > 1) setPage(page - 1);
      setVersion((current) => current + 1);
    } catch (err: unknown) {
      toast.add({
        type: "error",
        title: "Quyết định thất bại",
        description: err instanceof ApiError ? err.message : "Không thể lưu quyết định. Vui lòng thử lại.",
      });
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="text-h2">Đề nghị tạm ngừng trung tâm</h1>
        <p className="text-sm text-ink-2">
          Trung tâm bị gỡ tin nhắn lặp lại được Moderator đề nghị tạm ngừng.
          {canDecide ? " Bạn quyết định đồng ý tạm ngừng hoặc từ chối." : " Bạn chỉ xem, Admin là người quyết định."}
        </p>
      </header>

      <Segmented
        label="Trạng thái đề nghị"
        value={status}
        options={STATUS_OPTIONS}
        onChange={(next) => {
          setStatus(next);
          setPage(1);
        }}
      />

      {query.error ? (
        <Alert variant="destructive">
          <AlertDescription>{query.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={query.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Gửi lúc</TableHead>
              <TableHead>Trung tâm</TableHead>
              <TableHead className="text-right">Số tin bị gỡ</TableHead>
              <TableHead className="min-w-56">Lý do của Moderator</TableHead>
              <TableHead>Trạng thái</TableHead>
              {canDecide ? <TableHead className="w-48"><span className="sr-only">Thao tác</span></TableHead> : null}
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => {
              const badge = STATUS_LABEL[item.status] ?? { label: item.status, variant: "neutral" as const };
              return (
                <TableRow key={item.id}>
                  <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                  <TableCell>{item.studyCenterName}</TableCell>
                  <TableCell className="tabular text-right">{item.removedMessageCount}</TableCell>
                  <TableCell className="max-w-md whitespace-pre-wrap break-words">
                    {item.reason}
                    {item.decisionNote ? (
                      <span className="mt-1 block text-xs text-ink-2">Ghi chú của Admin: {item.decisionNote}</span>
                    ) : null}
                  </TableCell>
                  <TableCell>
                    <Badge variant={badge.variant}>{badge.label}</Badge>
                    {item.decidedAt ? <span className="mt-1 block text-xs text-ink-3">{formatDateTime(item.decidedAt)}</span> : null}
                  </TableCell>
                  {canDecide ? (
                    <TableCell className="text-right">
                      {item.status === "pending" ? (
                        <div className="inline-flex gap-1.5">
                          <Button type="button" variant="outline" size="sm" onClick={() => setDecision({ proposal: item, approve: true })} className="gap-1 text-risk hover:text-risk">
                            <CheckIcon className="size-3.5" />
                            Tạm ngừng
                          </Button>
                          <Button type="button" variant="outline" size="sm" onClick={() => setDecision({ proposal: item, approve: false })} className="gap-1">
                            <XIcon className="size-3.5" />
                            Từ chối
                          </Button>
                        </div>
                      ) : null}
                    </TableCell>
                  ) : null}
                </TableRow>
              );
            })}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={canDecide ? 6 : 5} className="py-8 text-center text-ink-2">
                  Không có đề nghị nào.
                </TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={canDecide ? 6 : 5} className="py-8 text-center text-ink-2">
                  {query.loading ? "Đang tải..." : "Không có dữ liệu."}
                </TableCell>
              </TableRow>
            ) : null}
          </TableBody>
        </Table>
      </div>

      {list ? (
        <TablePagination
          page={list.page}
          totalPages={list.totalPages}
          totalCount={list.totalCount}
          itemLabel="đề nghị"
          disabled={query.loading}
          onPageChange={setPage}
        />
      ) : null}

      <AlertDialog open={decision !== null} onOpenChange={(open) => !open && closeDialog()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {decision?.approve ? `Tạm ngừng ${decision.proposal.studyCenterName}?` : "Từ chối đề nghị này?"}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {decision?.approve
                ? "Trung tâm chuyển sang trạng thái tạm ngừng: không còn nhận yêu cầu liên hệ mới. Việc này được ghi nhật ký."
                : "Trung tâm vẫn hoạt động bình thường. Việc từ chối được ghi nhật ký."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <Textarea
            value={note}
            onChange={(event) => setNote(event.target.value)}
            maxLength={1000}
            rows={2}
            disabled={isSending}
            placeholder="Ghi chú (không bắt buộc, tối đa 1000 ký tự)"
            aria-label="Ghi chú quyết định"
            className="resize-y text-sm"
          />
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isSending}>Hủy</AlertDialogCancel>
            <Button
              type="button"
              variant={decision?.approve ? "destructive" : "default"}
              onClick={() => void confirm()}
              disabled={isSending}
              className="gap-1.5"
            >
              {isSending ? <Spinner className="size-3.5" /> : null}
              {decision?.approve ? "Tạm ngừng trung tâm" : "Từ chối đề nghị"}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
