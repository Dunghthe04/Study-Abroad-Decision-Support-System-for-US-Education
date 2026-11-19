"use client";

import { useMemo, useState } from "react";
import {
  CalendarIcon,
  DownloadIcon,
  ExternalLinkIcon,
  EyeIcon,
  FileTextIcon,
  LockIcon,
  MailWarningIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  UserIcon,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { accessLogsApi } from "@/lib/api";
import { useFetched } from "@/lib/use-fetched";
import { cn } from "@/lib/utils";
import type {
  AccessLogCategory,
  AccessLogDto,
  AccessLogFilterQuery,
  AccessLogSummaryDto,
} from "@/types/api";

const PAGE_SIZE = 20;

const CATEGORY_TABS: { id: AccessLogCategory; label: string; icon: React.ElementType }[] = [
  { id: "all_sensitive", label: "Dữ liệu nhạy cảm", icon: ShieldAlertIcon },
  { id: "center_document", label: "Tài liệu xác minh", icon: FileTextIcon },
  { id: "reported_message", label: "Tin nhắn báo cáo", icon: MailWarningIcon },
  { id: "slm_export", label: "Xuất mẫu SLM (UC-102)", icon: DownloadIcon },
  { id: "all", label: "Tất cả", icon: ShieldCheckIcon },
];

function formatVietnamDateTime(isoString: string): string {
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return isoString;
    return new Intl.DateTimeFormat("vi-VN", {
      timeZone: "Asia/Ho_Chi_Minh",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false,
    }).format(date);
  } catch {
    return isoString;
  }
}

function getTodayString(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function getDaysAgoString(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function summarizeDetail(action: string, detailJson: string | null | undefined): string {
  if (!detailJson) return "—";
  try {
    const data = JSON.parse(detailJson);
    if (action === "center.document_viewed") {
      const parts: string[] = [];
      if (data.fileName) parts.push(`Tệp: ${data.fileName}`);
      if (data.documentType) parts.push(`Loại: ${data.documentType}`);
      return parts.length > 0 ? parts.join(" • ") : detailJson;
    }
    if (action === "lead_message.report_queue_view") {
      return `Lọc: ${data.filter ?? "pending"} (Trang ${data.page ?? 1})`;
    }
    if (action === "lead_message.report_view") {
      return `Ngữ cảnh: ±${data.contextSize ?? 3} tin (Lead: ${data.leadId ? String(data.leadId).slice(0, 8) + "…" : "—"})`;
    }
    if (action === "slm_sample.export") {
      return `Định dạng: ${(data.format ?? "jsonl").toUpperCase()} • Số lượng: ${data.count ?? 0} mẫu`;
    }
    // generic
    const keys = Object.keys(data).slice(0, 3);
    return keys.map((k) => `${k}: ${data[k]}`).join(" • ");
  } catch {
    return detailJson.length > 60 ? detailJson.slice(0, 60) + "…" : detailJson;
  }
}

function getActionBadgeVariant(action: string): "reach" | "warn" | "ok" | "outline" | "neutral" {
  if (action === "center.document_viewed") return "reach";
  if (action.startsWith("lead_message.")) return "warn";
  if (action === "slm_sample.export") return "ok";
  return "outline";
}

function getRoleBadgeVariant(role: string | null | undefined): "brand" | "reach" | "neutral" {
  if (role === "admin") return "brand";
  if (role === "moderator") return "reach";
  return "neutral";
}

function getRoleLabel(role: string | null | undefined): string {
  if (role === "admin") return "Quản trị viên";
  if (role === "moderator") return "Kiểm duyệt viên";
  if (role === "center") return "Trung tâm";
  if (role === "student") return "Học sinh";
  if (role === "parent") return "Phụ huynh";
  return role || "Hệ thống";
}

/**
 * [UC-97] Xem và lọc nhật ký truy cập và xuất dữ liệu nhạy cảm (Administrator only).
 * Quy tắc: Nhật ký không sửa và không xóa được.
 */
export function AccessLogsView() {
  const [category, setCategory] = useState<AccessLogCategory>("all_sensitive");
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(1);
  const [detailItem, setDetailItem] = useState<AccessLogDto | null>(null);

  // Search commit on Enter or button
  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setDebouncedSearch(searchTerm.trim());
    setPage(1);
  };

  const handleResetFilters = () => {
    setCategory("all_sensitive");
    setSearchTerm("");
    setDebouncedSearch("");
    setFromDate("");
    setToDate("");
    setPage(1);
  };

  const setPresetRange = (preset: "today" | "7d" | "30d" | "all") => {
    if (preset === "all") {
      setFromDate("");
      setToDate("");
    } else if (preset === "today") {
      const today = getTodayString();
      setFromDate(today);
      setToDate(today);
    } else if (preset === "7d") {
      setFromDate(getDaysAgoString(7));
      setToDate(getTodayString());
    } else if (preset === "30d") {
      setFromDate(getDaysAgoString(30));
      setToDate(getTodayString());
    }
    setPage(1);
  };

  const filterQuery: AccessLogFilterQuery = useMemo(
    () => ({
      category,
      search: debouncedSearch || undefined,
      from: fromDate || undefined,
      to: toDate || undefined,
      page,
      pageSize: PAGE_SIZE,
    }),
    [category, debouncedSearch, fromDate, toDate, page]
  );

  const queryKey = useMemo(
    () =>
      `access-logs:${category}:${debouncedSearch}:${fromDate}:${toDate}:${page}`,
    [category, debouncedSearch, fromDate, toDate, page]
  );

  const summaryKey = useMemo(
    () => `access-logs-summary:${fromDate}:${toDate}`,
    [fromDate, toDate]
  );

  const { data: logsData, loading, error } = useFetched(queryKey, () =>
    accessLogsApi.list(filterQuery)
  );

  const { data: summaryData } = useFetched(summaryKey, () =>
    accessLogsApi.summary({ from: fromDate || undefined, to: toDate || undefined })
  );

  const totalPages = logsData?.totalPages ?? 1;
  const items = logsData?.items ?? [];

  return (
    <div className="flex flex-col gap-6">
      {/* Header & Immutability Rule Alert */}
      <div className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
              Nhật ký truy cập
            </h1>
            <p className="text-sm text-muted-foreground">
              [UC-97] Giám sát ai đã xem hoặc xuất dữ liệu nhạy cảm (tài liệu xác minh trung tâm, tin nhắn bị báo cáo, xuất mẫu ở UC-102).
            </p>
          </div>
          <Badge variant="ok" className="h-7 gap-1.5 px-3 text-xs font-semibold">
            <LockIcon className="size-3.5" />
            Nhật ký bất biến (Append-only)
          </Badge>
        </div>

        <div className="rounded-lg border border-border/80 bg-muted/40 p-3 text-xs text-muted-foreground">
          <span className="font-semibold text-foreground">Quy tắc bảo mật:</span> Dữ liệu nhật ký được lưu trữ vĩnh viễn,
          chỉ cho phép đọc và không thể sửa đổi hoặc xóa dưới bất kỳ hình thức nào.
        </div>
      </div>

      {/* KPI Summary Cards */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Tổng lượt truy cập nhạy cảm
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-foreground">
                {summaryData?.totalSensitiveCount ?? "—"}
              </span>
              <ShieldAlertIcon className="size-6 text-brand" />
            </div>
          </CardContent>
        </Card>

        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Xem tài liệu xác minh
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-reach">
                {summaryData?.centerDocumentViews ?? "—"}
              </span>
              <FileTextIcon className="size-6 text-reach" />
            </div>
          </CardContent>
        </Card>

        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Đọc tin nhắn bị báo cáo
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-warn">
                {summaryData?.reportedMessageViews ?? "—"}
              </span>
              <MailWarningIcon className="size-6 text-warn" />
            </div>
          </CardContent>
        </Card>

        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Xuất mẫu SLM (UC-102)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <span className="text-2xl font-bold text-ok">
                {summaryData?.slmSampleExports ?? "—"}
              </span>
              <DownloadIcon className="size-6 text-ok" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filter Toolbar */}
      <Card>
        <CardContent className="flex flex-col gap-4 p-4">
          {/* Category Tabs */}
          <div className="flex flex-wrap gap-1.5 border-b border-border pb-3">
            {CATEGORY_TABS.map((tab) => {
              const active = category === tab.id;
              const Icon = tab.icon;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => {
                    setCategory(tab.id);
                    setPage(1);
                  }}
                  className={cn(
                    "flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-colors",
                    active
                      ? "bg-brand font-semibold text-white shadow-sm"
                      : "bg-muted/60 text-muted-foreground hover:bg-muted hover:text-foreground"
                  )}
                >
                  <Icon className="size-3.5" />
                  {tab.label}
                </button>
              );
            })}
          </div>

          {/* Search & Date Controls */}
          <div className="grid grid-cols-1 gap-3 md:grid-cols-12">
            {/* Search Box */}
            <form onSubmit={handleSearchSubmit} className="flex gap-2 md:col-span-5">
              <div className="relative flex-1">
                <SearchIcon className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  type="search"
                  placeholder="Tìm theo tên, email người thực hiện hoặc IP..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-8 text-xs"
                />
              </div>
              <Button type="submit" size="sm" variant="secondary">
                Tìm
              </Button>
            </form>

            {/* Date Range Picker */}
            <div className="flex flex-wrap items-center gap-2 md:col-span-5">
              <div className="flex items-center gap-1 text-xs text-muted-foreground">
                <CalendarIcon className="size-3.5" />
                <span>Từ:</span>
                <input
                  type="date"
                  value={fromDate}
                  onChange={(e) => {
                    setFromDate(e.target.value);
                    setPage(1);
                  }}
                  className="h-8 rounded-md border border-input bg-background px-2 text-xs"
                />
              </div>
              <div className="flex items-center gap-1 text-xs text-muted-foreground">
                <span>Đến:</span>
                <input
                  type="date"
                  value={toDate}
                  onChange={(e) => {
                    setToDate(e.target.value);
                    setPage(1);
                  }}
                  className="h-8 rounded-md border border-input bg-background px-2 text-xs"
                />
              </div>
            </div>

            {/* Quick Presets & Reset */}
            <div className="flex items-center justify-end gap-1 md:col-span-2">
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setPresetRange("today")}
                className="h-7 px-2 text-xs"
              >
                Hôm nay
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setPresetRange("7d")}
                className="h-7 px-2 text-xs"
              >
                7 ngày
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={handleResetFilters}
                title="Đặt lại bộ lọc"
                className="h-7 px-2 text-xs text-muted-foreground hover:text-foreground"
              >
                <RotateCcwIcon className="size-3.5" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Error state */}
      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Data Table */}
      <div className="relative rounded-xl border border-border bg-card overflow-hidden">
        {loading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-background/50 backdrop-blur-[1px]">
            <Spinner className="size-6 text-brand" />
          </div>
        )}

        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/40 hover:bg-muted/40">
                <TableHead className="w-40 text-xs font-semibold">Thời gian</TableHead>
                <TableHead className="w-56 text-xs font-semibold">Người thực hiện</TableHead>
                <TableHead className="w-52 text-xs font-semibold">Loại thao tác</TableHead>
                <TableHead className="text-xs font-semibold">Đối tượng & Chi tiết</TableHead>
                <TableHead className="w-36 text-xs font-semibold">IP & Thiết bị</TableHead>
                <TableHead className="w-20 text-right text-xs font-semibold">Chi tiết</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-32 text-center text-sm text-muted-foreground">
                    {loading ? "Đang tải nhật ký..." : "Không tìm thấy dòng nhật ký truy cập nào phù hợp."}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((log) => (
                  <TableRow key={log.id} className="hover:bg-muted/30">
                    {/* Timestamp */}
                    <TableCell className="font-mono text-xs whitespace-nowrap text-muted-foreground">
                      {formatVietnamDateTime(log.createdAt)}
                    </TableCell>

                    {/* Actor */}
                    <TableCell>
                      <div className="flex flex-col gap-0.5">
                        <div className="flex items-center gap-1.5">
                          <span className="font-medium text-foreground">
                            {log.actorFullName || (log.actorUserId ? "Người dùng ẩn" : "Hệ thống")}
                          </span>
                          <Badge
                            variant={getRoleBadgeVariant(log.actorRole)}
                            className="h-4 px-1.5 text-[10px]"
                          >
                            {getRoleLabel(log.actorRole)}
                          </Badge>
                        </div>
                        {log.actorEmail && (
                          <span className="text-xs text-muted-foreground truncate max-w-[200px]">
                            {log.actorEmail}
                          </span>
                        )}
                      </div>
                    </TableCell>

                    {/* Action */}
                    <TableCell>
                      <div className="flex flex-col gap-1">
                        <Badge
                          variant={getActionBadgeVariant(log.action)}
                          className="w-fit text-xs font-medium"
                        >
                          {log.actionDescription}
                        </Badge>
                        <span className="font-mono text-[11px] text-muted-foreground">
                          {log.action}
                        </span>
                      </div>
                    </TableCell>

                    {/* Summary Detail */}
                    <TableCell className="max-w-[320px]">
                      <div className="flex flex-col gap-0.5">
                        <span className="text-xs text-foreground truncate" title={log.detailJson ?? ""}>
                          {summarizeDetail(log.action, log.detailJson)}
                        </span>
                        {log.entityType && (
                          <span className="font-mono text-[10px] text-muted-foreground">
                            {log.entityType}{log.entityId ? ` #${log.entityId.slice(0, 8)}` : ""}
                          </span>
                        )}
                      </div>
                    </TableCell>

                    {/* IP & Device */}
                    <TableCell>
                      <div className="flex flex-col gap-0.5 text-xs text-muted-foreground font-mono">
                        <span>{log.ipAddress || "—"}</span>
                        {log.userAgent && (
                          <span
                            className="text-[10px] text-muted-foreground/80 truncate max-w-[120px]"
                            title={log.userAgent}
                          >
                            {log.userAgent}
                          </span>
                        )}
                      </div>
                    </TableCell>

                    {/* Action button */}
                    <TableCell className="text-right">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() => setDetailItem(log)}
                        className="h-7 w-7 p-0"
                        title="Xem chi tiết kỹ thuật"
                      >
                        <EyeIcon className="size-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>

        {/* Pagination */}
        <div className="border-t border-border p-3">
          <TablePagination
            page={page}
            totalPages={totalPages}
            totalCount={logsData?.totalCount}
            itemLabel="dòng nhật ký"
            disabled={loading}
            onPageChange={setPage}
          />
        </div>
      </div>

      {/* Detail Dialog */}
      <Dialog open={detailItem !== null} onOpenChange={(open) => !open && setDetailItem(null)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ShieldAlertIcon className="size-5 text-brand" />
              Chi tiết nhật ký truy cập
            </DialogTitle>
            <DialogDescription>
              Mã bản ghi: <span className="font-mono font-medium text-foreground">{detailItem?.id}</span>
            </DialogDescription>
          </DialogHeader>

          {detailItem && (
            <div className="flex flex-col gap-4 text-xs">
              <div className="grid grid-cols-2 gap-3 rounded-lg border border-border bg-muted/30 p-3">
                <div>
                  <span className="text-muted-foreground">Thời gian (Việt Nam):</span>
                  <div className="font-semibold text-foreground">
                    {formatVietnamDateTime(detailItem.createdAt)}
                  </div>
                </div>
                <div>
                  <span className="text-muted-foreground">Thời gian (UTC):</span>
                  <div className="font-mono text-foreground">{detailItem.createdAt}</div>
                </div>

                <div>
                  <span className="text-muted-foreground">Người thực hiện:</span>
                  <div className="font-semibold text-foreground">
                    {detailItem.actorFullName || "—"} ({getRoleLabel(detailItem.actorRole)})
                  </div>
                  <div className="text-muted-foreground">{detailItem.actorEmail || "—"}</div>
                </div>
                <div>
                  <span className="text-muted-foreground">Actor User ID:</span>
                  <div className="font-mono text-muted-foreground">{detailItem.actorUserId || "null"}</div>
                </div>

                <div>
                  <span className="text-muted-foreground">Thao tác:</span>
                  <div>
                    <Badge variant={getActionBadgeVariant(detailItem.action)}>
                      {detailItem.actionDescription}
                    </Badge>
                  </div>
                  <div className="font-mono text-[11px] text-muted-foreground mt-0.5">
                    {detailItem.action}
                  </div>
                </div>
                <div>
                  <span className="text-muted-foreground">Đối tượng:</span>
                  <div className="font-mono text-foreground">
                    {detailItem.entityType || "—"} {detailItem.entityId ? `(${detailItem.entityId})` : ""}
                  </div>
                </div>

                <div>
                  <span className="text-muted-foreground">Địa chỉ IP:</span>
                  <div className="font-mono text-foreground">{detailItem.ipAddress || "—"}</div>
                </div>
                <div>
                  <span className="text-muted-foreground">User Agent:</span>
                  <div className="text-[11px] text-muted-foreground break-all">
                    {detailItem.userAgent || "—"}
                  </div>
                </div>
              </div>

              {/* JSON Detail */}
              <div className="flex flex-col gap-1">
                <span className="font-semibold text-foreground">Dữ liệu chi tiết (DetailJson):</span>
                <pre className="max-h-60 overflow-auto rounded-lg border border-border bg-muted/60 p-3 font-mono text-[11px] leading-relaxed text-foreground">
                  {detailItem.detailJson
                    ? (() => {
                        try {
                          return JSON.stringify(JSON.parse(detailItem.detailJson), null, 2);
                        } catch {
                          return detailItem.detailJson;
                        }
                      })()
                    : "null"}
                </pre>
              </div>
            </div>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDetailItem(null)}>
              Đóng
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
