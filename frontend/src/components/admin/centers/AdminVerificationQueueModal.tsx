"use client";

import React, { useCallback, useEffect, useState } from "react";
import {
  ShieldCheckIcon,
  CheckCircle2Icon,
  ClockIcon,
  AlertCircleIcon,
  SearchIcon,
  RotateCcwIcon,
} from "lucide-react";
import { adminCenterVerificationsApi } from "@/lib/api";
import type { CenterVerificationDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
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
import { ReviewVerificationModal } from "@/components/admin/centers/ReviewVerificationModal";

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onRefreshCenters?: () => void;
}

export function AdminVerificationQueueModal({
  isOpen,
  onClose,
  onRefreshCenters,
}: Props) {
  const [items, setItems] = useState<CenterVerificationDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState("all");
  const [search, setSearch] = useState("");
  const [selectedRequestId, setSelectedRequestId] = useState<string | null>(null);

  const fetchQueue = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminCenterVerificationsApi.list({
        status: statusFilter === "all" ? undefined : statusFilter,
        search: search.trim() || undefined,
        page: 1,
        pageSize: 50,
      });
      setItems(res.items);
      setTotalCount(res.totalCount);
    } catch (e) {
      console.error("Failed to fetch verification queue", e);
    } finally {
      setLoading(false);
    }
  }, [search, statusFilter]);

  useEffect(() => {
    if (!isOpen) return;
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void fetchQueue();
      }
    });
    return () => {
      active = false;
    };
  }, [fetchQueue, isOpen]);

  return (
    <>
      <Dialog open={isOpen} onOpenChange={(next) => !next && onClose()}>
        <DialogContent variant="form" size="2xl">
          <DialogHeader>
            <DialogIcon tone="brand">
              <ShieldCheckIcon />
            </DialogIcon>
            <DialogTitle>Hàng đợi kiểm duyệt xác minh trung tâm</DialogTitle>
            <DialogDescription>
              Đối chiếu hồ sơ tự đăng ký của các trung tâm tư vấn với Cổng ĐKDN Quốc gia.
            </DialogDescription>
          </DialogHeader>

          <DialogBody className="space-y-4">
            {/* Toolbar Filter & Search */}
            <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pb-3 border-b border-line">
              <div className="flex items-center gap-1.5 overflow-x-auto w-full sm:w-auto">
                {[
                  { id: "all", label: "Tất cả" },
                  { id: "pending_review", label: "Chờ duyệt" },
                  { id: "verified", label: "Đã duyệt" },
                  { id: "rejected", label: "Bị từ chối" },
                  { id: "suspended", label: "Tạm ngưng" },
                ].map((tab) => (
                  <button
                    key={tab.id}
                    type="button"
                    onClick={() => setStatusFilter(tab.id)}
                    className={`cursor-pointer px-2.5 py-1 text-xs font-medium rounded-lg transition-colors whitespace-nowrap focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 ${
                      statusFilter === tab.id
                        ? "bg-brand/10 text-brand font-semibold"
                        : "text-ink-2 hover:bg-surface-2"
                    }`}
                  >
                    {tab.label}
                  </button>
                ))}
              </div>

              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  void fetchQueue();
                }}
                className="flex items-center gap-2 w-full sm:w-auto"
              >
                <div className="relative flex-1 sm:w-56">
                  <SearchIcon className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-ink-3" />
                  <Input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Tìm tên, MST..."
                    className="pl-8 h-8 text-xs"
                  />
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="icon"
                  onClick={fetchQueue}
                  disabled={loading}
                  aria-label="Tải lại danh sách"
                >
                  <RotateCcwIcon className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
                </Button>
              </form>
            </div>

            {/* List Content */}
            {loading ? (
              <div className="flex h-48 items-center justify-center gap-2 text-ink-2 text-sm">
                <Spinner />
                <span>Đang tải danh sách hồ sơ...</span>
              </div>
            ) : items.length === 0 ? (
              <div className="flex flex-col items-center justify-center h-48 text-center text-ink-3 space-y-2">
                <ShieldCheckIcon className="h-8 w-8 text-ink-3/50" />
                <p className="text-sm">Không có yêu cầu xác minh nào phù hợp với bộ lọc.</p>
              </div>
            ) : (
              <div className="divide-y divide-line rounded-lg border border-line bg-surface overflow-hidden">
                {items.map((req) => {
                  const isPending = req.status === "pending_review";
                  const isVerified = req.status === "verified";
                  const isRejected = req.status === "rejected";
                  const isChangesRequested = req.status === "changes_requested";
                  const isSuspended = req.status === "suspended";

                  return (
                    <div
                      key={req.id}
                      className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-surface-2/40 transition-colors"
                    >
                      <div className="space-y-1 max-w-lg">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-bold text-ink text-sm">
                            {req.centerName || req.legalName}
                          </span>
                          {isPending && (
                            <Badge variant="outline" className="border-sky-500/30 bg-sky-500/10 text-sky-700 dark:text-sky-300 text-[11px] gap-1">
                              <ClockIcon className="h-3 w-3" />
                              Chờ duyệt
                            </Badge>
                          )}
                          {isChangesRequested && (
                            <Badge variant="outline" className="border-amber-500/40 bg-amber-500/10 text-amber-700 dark:text-amber-400 text-[11px] gap-1">
                              <RotateCcwIcon className="h-3 w-3" />
                              Yêu cầu bổ sung
                            </Badge>
                          )}
                          {isVerified && (
                            <Badge variant="outline" className="border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 text-[11px] gap-1">
                              <CheckCircle2Icon className="h-3 w-3" />
                              Đã duyệt
                            </Badge>
                          )}
                          {isRejected && (
                            <Badge variant="outline" className="border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-300 text-[11px] gap-1">
                              <AlertCircleIcon className="h-3 w-3" />
                              Bị từ chối
                            </Badge>
                          )}
                          {isSuspended && (
                            <Badge variant="outline" className="border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-300 text-[11px] gap-1">
                              <AlertCircleIcon className="h-3 w-3" />
                              Tạm ngưng
                            </Badge>
                          )}
                        </div>

                        <p className="text-xs text-ink-2">
                          Tên ĐKKD: <span className="font-medium text-ink">{req.legalName}</span> • MST: <span className="font-mono font-medium text-brand">{req.taxCode}</span>
                        </p>
                        <p className="text-[11px] text-ink-3">
                          Đại diện: {req.legalRepresentative} • Nộp lúc: {new Date(req.submittedAt).toLocaleDateString("vi-VN")} • {req.documents.length} tài liệu
                        </p>
                      </div>

                      <div className="flex items-center gap-2 shrink-0">
                        <Button
                          variant={isPending ? "default" : "outline"}
                          onClick={() => setSelectedRequestId(req.id)}
                          className="text-xs"
                        >
                          {isPending ? "Đối chiếu & Duyệt" : "Xem chi tiết"}
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            <p className="text-xs text-ink-3">Tổng số: {totalCount} yêu cầu xác minh</p>
          </DialogBody>

          <DialogFooter layout="single">
            <DialogCancel onClick={onClose}>Đóng</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Review Dialog */}
      <ReviewVerificationModal
        requestId={selectedRequestId}
        isOpen={!!selectedRequestId}
        onClose={() => setSelectedRequestId(null)}
        onSuccess={() => {
          void fetchQueue();
          onRefreshCenters?.();
        }}
      />
    </>
  );
}
