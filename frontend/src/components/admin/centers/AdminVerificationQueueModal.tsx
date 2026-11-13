"use client";

import React, { useCallback, useEffect, useState } from "react";
import {
  ShieldCheckIcon,
  CheckCircle2Icon,
  ClockIcon,
  AlertCircleIcon,
  SearchIcon,
  RotateCcwIcon,
  XIcon
} from "lucide-react";
import { adminCenterVerificationsApi } from "@/lib/api";
import type { CenterVerificationDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
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

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative w-full max-w-4xl max-h-[90vh] flex flex-col rounded-xl border border-line bg-surface shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-line px-6 py-4 bg-surface-2">
          <div className="flex items-center gap-2.5">
            <ShieldCheckIcon className="h-5 w-5 text-brand" />
            <div>
              <h2 className="text-lg font-bold text-ink">
                Hàng đợi kiểm duyệt xác minh trung tâm (UC-59)
              </h2>
              <p className="text-xs text-ink-3">
                Đối chiếu hồ sơ tự đăng ký của các trung tâm tư vấn với Cổng ĐKDN Quốc gia.
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="p-1 rounded-lg text-ink-3 hover:bg-surface-3 hover:text-ink transition-colors"
          >
            <XIcon className="h-5 w-5" />
          </button>
        </div>

        {/* Toolbar Filter & Search */}
        <div className="border-b border-line px-6 py-3 bg-surface flex flex-col sm:flex-row items-center justify-between gap-3">
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
                className={`px-2.5 py-1 text-xs font-medium rounded-lg transition-colors whitespace-nowrap ${
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
              size="sm"
              onClick={fetchQueue}
              disabled={loading}
              className="h-8 px-2.5"
            >
              <RotateCcwIcon className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
            </Button>
          </form>
        </div>

        {/* List Content */}
        <div className="flex-1 overflow-y-auto p-6">
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
                        size="sm"
                        variant={isPending ? "default" : "outline"}
                        onClick={() => setSelectedRequestId(req.id)}
                        className="text-xs h-8"
                      >
                        {isPending ? "Đối chiếu & Duyệt (UC-59)" : "Xem chi tiết"}
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-line px-6 py-3 bg-surface-2 flex items-center justify-between text-xs text-ink-3">
          <span>Tổng số: {totalCount} yêu cầu xác minh</span>
          <Button variant="ghost" size="sm" onClick={onClose} className="h-8 text-xs">
            Đóng
          </Button>
        </div>
      </div>

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
    </div>
  );
}
