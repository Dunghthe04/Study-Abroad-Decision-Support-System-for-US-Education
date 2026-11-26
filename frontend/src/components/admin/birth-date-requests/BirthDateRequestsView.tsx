"use client";

import { useEffect, useState, type ReactNode } from "react";
import { CheckIcon, EyeIcon, FileSearchIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { EVIDENCE_TYPE_LABELS } from "@/components/account/BirthDateRequestDialog";
import { ApiError, adminBirthDateRequestsApi } from "@/lib/api";
import { formatDateTime } from "@/lib/ai-usage-format";
import { useFetched } from "@/lib/use-fetched";
import type { AdminBirthDateRequestDto, AdminBirthDateRequestFilter, BirthDateChangeStatus } from "@/types/api";

const PAGE_SIZE = 20;
const MAX_REJECT_REASON = 500;

const FILTER_OPTIONS: { id: AdminBirthDateRequestFilter; label: string }[] = [
  { id: "pending", label: "Chờ duyệt" },
  { id: "approved", label: "Đã duyệt" },
  { id: "rejected", label: "Từ chối" },
  { id: "cancelled", label: "Đã hủy" },
  { id: "all", label: "Tất cả" },
];

const EMPTY_TEXT: Record<AdminBirthDateRequestFilter, string> = {
  pending: "Không còn yêu cầu nào chờ duyệt.",
  approved: "Chưa có yêu cầu nào được duyệt.",
  rejected: "Chưa có yêu cầu nào bị từ chối.",
  cancelled: "Chưa có yêu cầu nào bị người dùng hủy.",
  all: "Chưa có yêu cầu sửa ngày sinh nào.",
};

const ROLE_LABELS: Record<string, string> = { student: "Học sinh", parent: "Phụ huynh" };

function formatDob(value: string | null): string {
  if (!value) return "Chưa có";
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : value;
}

function ageText(age: number | null): string {
  return age === null ? "" : ` (${age} tuổi)`;
}

function StatusBadge({ status }: { status: BirthDateChangeStatus }) {
  if (status === "approved") return <Badge variant="ok">Đã duyệt</Badge>;
  if (status === "rejected") return <Badge variant="risk">Từ chối</Badge>;
  if (status === "cancelled") return <Badge variant="neutral">Đã hủy</Badge>;
  return <Badge variant="warn">Chờ duyệt</Badge>;
}

function ThresholdBadges({ item }: { item: AdminBirthDateRequestDto }) {
  if (item.crossedThresholds.length === 0) return <span className="text-ink-2">Không</span>;
  const rising = item.currentAge === null || item.requestedAge > item.currentAge;
  return (
    <span className="flex flex-wrap gap-1">
      {item.crossedThresholds.map((age) => (
        <Badge key={age} variant="warn">
          {rising ? "Lên" : "Xuống dưới"} {age}
        </Badge>
      ))}
    </span>
  );
}

/**
 * [UC-150] Administrator xem yêu cầu sửa ngày sinh (mặc định Chờ duyệt, cũ nhất trước), mở chi tiết, xem minh chứng
 * (mỗi lần mở ghi nhật ký truy cập UC-91), Duyệt hoặc Từ chối kèm lý do.
 */
export function BirthDateRequestsView({ initialRequestId }: { initialRequestId: string | null }) {
  const [filter, setFilter] = useState<AdminBirthDateRequestFilter>("pending");
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [openId, setOpenId] = useState<string | null>(initialRequestId);

  const queue = useFetched(`birth-date-requests|${filter}|${page}|${version}`, () =>
    adminBirthDateRequestsApi.list({ status: filter, page, pageSize: PAGE_SIZE }),
  );
  const list = queue.data;

  const changeFilter = (next: AdminBirthDateRequestFilter) => {
    setFilter(next);
    setPage(1);
  };

  const handleClosed = (changed: boolean) => {
    setOpenId(null);
    if (!changed) return;
    if (list && list.items.length === 1 && page > 1 && filter === "pending") setPage(page - 1);
    setVersion((current) => current + 1);
  };

  return (
    <div className="flex flex-col gap-4">
      <Segmented label="Lọc yêu cầu" value={filter} options={FILTER_OPTIONS} onChange={changeFilter} />

      {queue.error ? (
        <Alert variant="destructive">
          <AlertDescription>{queue.error}</AlertDescription>
        </Alert>
      ) : null}

      <div data-loading={queue.loading ? "true" : "false"} className="overflow-x-auto rounded-md border border-line data-[loading=true]:opacity-60">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Gửi lúc</TableHead>
              <TableHead>Người dùng</TableHead>
              <TableHead>Ngày sinh hiện tại</TableHead>
              <TableHead>Ngày sinh đề nghị</TableHead>
              <TableHead>Vượt mốc</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="w-24"><span className="sr-only">Thao tác</span></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {list?.items.map((item) => (
              <TableRow key={item.id}>
                <TableCell className="tabular whitespace-nowrap">{formatDateTime(item.createdAt)}</TableCell>
                <TableCell>
                  <span className="block font-medium">{item.fullName}</span>
                  <span className="block text-xs text-ink-2">{item.email} · {ROLE_LABELS[item.role] ?? item.role}</span>
                </TableCell>
                <TableCell className="tabular whitespace-nowrap">{formatDob(item.currentBirthDate)}{ageText(item.currentAge)}</TableCell>
                <TableCell className="tabular whitespace-nowrap">{formatDob(item.requestedBirthDate)}{ageText(item.requestedAge)}</TableCell>
                <TableCell><ThresholdBadges item={item} /></TableCell>
                <TableCell><StatusBadge status={item.status} /></TableCell>
                <TableCell className="text-right">
                  <Button type="button" variant="outline" size="sm" onClick={() => setOpenId(item.id)} className="gap-1">
                    <EyeIcon className="size-3.5" />
                    Xem
                  </Button>
                </TableCell>
              </TableRow>
            ))}
            {list && list.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">{EMPTY_TEXT[filter]}</TableCell>
              </TableRow>
            ) : null}
            {!list ? (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-ink-2">{queue.loading ? "Đang tải..." : "Không có dữ liệu."}</TableCell>
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
          itemLabel="yêu cầu"
          disabled={queue.loading}
          onPageChange={setPage}
        />
      ) : null}

      {openId ? <RequestDetailDialog key={openId} id={openId} onClosed={handleClosed} /> : null}
    </div>
  );
}

function DetailRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid grid-cols-[9rem_1fr] gap-2 text-sm">
      <dt className="text-ink-2">{label}</dt>
      <dd className="min-w-0 break-words">{children}</dd>
    </div>
  );
}

interface EvidencePreview {
  url: string;
  isPdf: boolean;
}

/** Chi tiết một yêu cầu: minh chứng chỉ tải khi Admin bấm xem (máy chủ ghi nhật ký mỗi lần mở). */
function RequestDetailDialog({ id, onClosed }: { id: string; onClosed: (changed: boolean) => void }) {
  const [version, setVersion] = useState(0);
  const detail = useFetched(`birth-date-request|${id}|${version}`, () => adminBirthDateRequestsApi.get(id));
  const item = detail.data?.data ?? null;

  const [changed, setChanged] = useState(false);
  const [busy, setBusy] = useState<"approve" | "reject" | "evidence" | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [rejectReason, setRejectReason] = useState("");
  const [rejectError, setRejectError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [evidence, setEvidence] = useState<EvidencePreview | null>(null);

  // Bỏ URL tạm của minh chứng khi đóng hoặc mở lại
  useEffect(() => () => {
    if (evidence) URL.revokeObjectURL(evidence.url);
  }, [evidence]);

  const close = () => {
    if (busy && busy !== "evidence") return;
    onClosed(changed);
  };

  const errorText = (err: unknown, fallback: string) => (err instanceof ApiError ? err.message : fallback);

  const reloadAfterConflict = (err: unknown) => {
    if (err instanceof ApiError && err.status === 409) {
      setChanged(true);
      setRejecting(false);
      setVersion((current) => current + 1);
    }
  };

  const openEvidence = async () => {
    if (!item || busy) return;
    setBusy("evidence");
    setActionError(null);
    try {
      const blob = await adminBirthDateRequestsApi.openEvidence(item.id);
      setEvidence({ url: URL.createObjectURL(blob), isPdf: blob.type === "application/pdf" });
    } catch (err: unknown) {
      setActionError(errorText(err, "Không mở được minh chứng. Vui lòng thử lại."));
    } finally {
      setBusy(null);
    }
  };

  const approve = async () => {
    if (!item || busy) return;
    setBusy("approve");
    setActionError(null);
    try {
      const res = await adminBirthDateRequestsApi.approve(item.id);
      toast.add({ type: "success", title: "Đã duyệt yêu cầu", description: res.message || `Ngày sinh của ${item.fullName} đã được cập nhật.` });
      setEvidence(null);
      onClosed(true);
    } catch (err: unknown) {
      setActionError(errorText(err, "Không duyệt được yêu cầu. Vui lòng thử lại."));
      reloadAfterConflict(err);
    } finally {
      setBusy(null);
    }
  };

  const reject = async () => {
    if (!item || busy) return;
    const reason = rejectReason.trim();
    if (!reason) {
      setRejectError("Vui lòng nhập lý do từ chối.");
      return;
    }
    setBusy("reject");
    setActionError(null);
    try {
      const res = await adminBirthDateRequestsApi.reject(item.id, reason);
      toast.add({ type: "success", title: "Đã từ chối yêu cầu", description: res.message || "Người dùng đã nhận thông báo kèm lý do." });
      setEvidence(null);
      onClosed(true);
    } catch (err: unknown) {
      if (err instanceof ApiError && err.status === 400) setRejectError(err.message);
      else setActionError(errorText(err, "Không từ chối được yêu cầu. Vui lòng thử lại."));
      reloadAfterConflict(err);
    } finally {
      setBusy(null);
    }
  };

  const pending = item?.status === "pending";

  return (
    <Dialog open onOpenChange={(open) => !open && close()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Yêu cầu sửa ngày sinh</DialogTitle>
          <DialogDescription>
            Đối chiếu ngày sinh đề nghị với minh chứng (nếu có) rồi Duyệt hoặc Từ chối. Duyệt hay từ chối đều xóa tệp minh chứng.
          </DialogDescription>
        </DialogHeader>

        {detail.error && !item ? (
          <Alert variant="destructive">
            <AlertDescription>{detail.error}</AlertDescription>
          </Alert>
        ) : null}
        {!item && !detail.error ? <p className="py-6 text-center text-sm text-ink-2">Đang tải...</p> : null}

        {item ? (
          <div className="flex flex-col gap-4">
            <dl className="flex flex-col gap-2">
              <DetailRow label="Người dùng">
                <span className="font-medium">{item.fullName}</span>
                <span className="block text-xs text-ink-2">{item.email} · {ROLE_LABELS[item.role] ?? item.role}</span>
              </DetailRow>
              <DetailRow label="Trạng thái"><StatusBadge status={item.status} /></DetailRow>
              <DetailRow label="Gửi lúc">{formatDateTime(item.createdAt)}</DetailRow>
              <DetailRow label="Ngày sinh hiện tại">{formatDob(item.currentBirthDate)}{ageText(item.currentAge)}</DetailRow>
              <DetailRow label="Ngày sinh đề nghị">
                <span className="font-medium">{formatDob(item.requestedBirthDate)}</span>{ageText(item.requestedAge)}
              </DetailRow>
              <DetailRow label="Vượt mốc tuổi"><ThresholdBadges item={item} /></DetailRow>
              <DetailRow label="Lý do"><span className="whitespace-pre-wrap">{item.reason}</span></DetailRow>
              {item.representativeEmail ? (
                <DetailRow label="Email người đại diện">{item.representativeEmail}</DetailRow>
              ) : null}
              {item.evidenceType ? (
                <DetailRow label="Loại minh chứng">{EVIDENCE_TYPE_LABELS[item.evidenceType] ?? item.evidenceType}</DetailRow>
              ) : null}
              {item.rejectReason ? (
                <DetailRow label="Lý do từ chối"><span className="whitespace-pre-wrap">{item.rejectReason}</span></DetailRow>
              ) : null}
              {item.reviewedAt ? <DetailRow label="Xử lý lúc">{formatDateTime(item.reviewedAt)}</DetailRow> : null}
            </dl>

            {item.hasEvidence ? (
              <div className="flex flex-col gap-2">
                {evidence ? (
                  evidence.isPdf ? (
                    <iframe src={evidence.url} title="Minh chứng ngày sinh" className="h-[60vh] w-full rounded-md border border-line" />
                  ) : (
                    // eslint-disable-next-line @next/next/no-img-element -- ảnh tạm từ blob, không qua next/image
                    <img src={evidence.url} alt="Minh chứng ngày sinh" className="max-h-[60vh] w-full rounded-md border border-line object-contain" />
                  )
                ) : (
                  <Button type="button" variant="outline" size="sm" onClick={() => void openEvidence()} loading={busy === "evidence"} disabled={busy !== null} className="w-fit gap-1.5">
                    <FileSearchIcon className="size-3.5" />
                    Xem minh chứng{item.evidenceFileName ? ` (${item.evidenceFileName})` : ""}
                  </Button>
                )}
                <p className="text-xs text-ink-2">Mỗi lần mở minh chứng được ghi vào nhật ký truy cập.</p>
              </div>
            ) : item.crossedThresholds.length > 0 && pending ? (
              <p className="text-sm text-ink-2">Không có tệp minh chứng.</p>
            ) : null}

            {pending && rejecting ? (
              <Field data-invalid={rejectError ? true : undefined}>
                <FieldLabel htmlFor="birth-reject-reason">Lý do từ chối</FieldLabel>
                <Textarea
                  id="birth-reject-reason"
                  value={rejectReason}
                  onChange={(event) => {
                    setRejectReason(event.target.value);
                    setRejectError(null);
                  }}
                  maxLength={MAX_REJECT_REASON}
                  rows={3}
                  disabled={busy !== null}
                  placeholder={`Người dùng sẽ thấy lý do này (tối đa ${MAX_REJECT_REASON} ký tự)`}
                  aria-invalid={rejectError ? true : undefined}
                  className="resize-y text-sm"
                />
                {rejectError ? <FieldError>{rejectError}</FieldError> : null}
              </Field>
            ) : null}

            {actionError ? (
              <Alert variant="destructive">
                <AlertDescription>{actionError}</AlertDescription>
              </Alert>
            ) : null}
          </div>
        ) : null}

        <DialogFooter>
          {pending && rejecting ? (
            <>
              <Button type="button" variant="outline" disabled={busy !== null} onClick={() => { setRejecting(false); setRejectError(null); }}>
                Quay lại
              </Button>
              <Button type="button" variant="destructive" loading={busy === "reject"} disabled={busy !== null} onClick={() => void reject()} className="gap-1.5">
                <XIcon className="size-3.5" />
                Xác nhận từ chối
              </Button>
            </>
          ) : pending ? (
            <>
              <Button type="button" variant="outline" disabled={busy !== null} onClick={() => { setRejecting(true); setActionError(null); }} className="gap-1.5 text-risk hover:text-risk">
                <XIcon className="size-3.5" />
                Từ chối
              </Button>
              <Button type="button" loading={busy === "approve"} disabled={busy !== null} onClick={() => void approve()} className="gap-1.5">
                <CheckIcon className="size-3.5" />
                Duyệt
              </Button>
            </>
          ) : (
            <Button type="button" variant="outline" onClick={close}>Đóng</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
