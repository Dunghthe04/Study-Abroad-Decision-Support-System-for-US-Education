"use client";

import { useMemo, useState } from "react";
import {
  CalendarIcon,
  CheckIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  CopyIcon,
  DownloadIcon,
  EyeIcon,
  FileTextIcon,
  LockIcon,
  MailWarningIcon,
  RotateCcwIcon,
  SearchIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  UserCheckIcon,
  UsersIcon,
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
  { id: "all_sensitive", label: "Dữ liệu nhạy cảm (UC-97)", icon: ShieldAlertIcon },
  { id: "center_document", label: "Tài liệu trung tâm", icon: FileTextIcon },
  { id: "reported_message", label: "Tin nhắn vi phạm", icon: MailWarningIcon },
  { id: "slm_export", label: "Xuất mẫu SLM (UC-102)", icon: DownloadIcon },
  { id: "parent", label: "Đại diện & Phụ huynh", icon: UsersIcon },
  { id: "account", label: "Tài khoản & Phân quyền", icon: UserCheckIcon },
  { id: "all", label: "Tất cả nhật ký", icon: ShieldCheckIcon },
];

/** Ánh xạ tên thao tác sang mô tả tiếng Việt thân thiện */
const ACTION_FRIENDLY_NAMES: Record<string, string> = {
  // Dữ liệu nhạy cảm UC-97
  "center.document_viewed": "Xem tài liệu xác minh trung tâm",
  "lead_message.report_queue_view": "Xem danh sách tin nhắn bị báo cáo",
  "lead_message.report_view": "Đọc tin nhắn bị báo cáo & ngữ cảnh",
  "slm_sample.export": "Xuất mẫu huấn luyện SLM (UC-102)",
  "review_batch.view_item": "Xem tin nhắn chat kiểm thử (đợt duyệt)",

  // Phụ huynh & Đại diện
  "parent.acting.start": "Bắt đầu quyền đại diện con",
  "parent.acting.stop": "Kết thúc quyền đại diện con",
  "parent.link.create": "Gửi yêu cầu liên kết phụ huynh",
  "parent.link.confirm": "Xác nhận liên kết phụ huynh",
  "parent.share.consent": "Đồng ý chia sẻ hồ sơ học sinh",
  "parent.managed_child.create": "Tạo hồ sơ học sinh được quản lý",

  // Tài khoản & Người dùng
  "user.lock": "Khóa tài khoản người dùng",
  "user.unlock": "Mở khóa tài khoản người dùng",
  "user.role_change": "Thay đổi phân quyền vai trò",
  "user.birth_date_edit": "Cập nhật ngày sinh và tuổi",
  "user.anonymized": "Ẩn danh tài khoản",
  "user.deleted": "Xóa vĩnh viễn tài khoản",

  // Trung tâm
  "center.verify": "Phê duyệt / Từ chối xác minh trung tâm",
  "center.review_remove": "Gỡ đánh giá trung tâm vi phạm",
  "center.change_review": "Duyệt thay đổi thông tin trung tâm",
  "center.delete_request": "Yêu cầu xóa tài khoản trung tâm",

  // Tin nhắn & Lead
  "lead_message.remove": "Gỡ bỏ tin nhắn vi phạm",
  "lead.status_change": "Cập nhật trạng thái yêu cầu tư vấn",

  // Trường & Học bổng
  "target_school.add": "Thêm trường mục tiêu",
  "target_school.remove": "Xóa trường mục tiêu",
  "target_school.update": "Cập nhật trường mục tiêu",
  "university.update": "Cập nhật thông tin trường ĐH",
  "university.hide": "Ẩn thông tin trường ĐH",
  "scholarship.update": "Cập nhật thông tin học bổng",
  "grade_scale.update": "Cập nhật bảng quy đổi điểm",
};

/** Ánh xạ tên loại thực thể sang tiếng Việt */
const ENTITY_FRIENDLY_NAMES: Record<string, string> = {
  user: "Người dùng",
  lead_message: "Tin nhắn tư vấn",
  center_document: "Tài liệu xác minh",
  study_center: "Trung tâm tư vấn",
  target_school: "Trường mục tiêu",
  university: "Trường đại học",
  scholarship: "Học bổng",
  app_setting: "Cài đặt hệ thống",
  parent_link: "Liên kết phụ huynh",
  managed_child: "Hồ sơ con quản lý",
  slm_sample: "Mẫu huấn luyện SLM",
};

/** Ánh xạ tên trường trong JSON sang nhãn tiếng Việt */
const FIELD_FRIENDLY_NAMES: Record<string, string> = {
  managedChildId: "Mã học sinh đại diện",
  childId: "Mã học sinh",
  parentId: "Mã phụ huynh",
  reason: "Lý do thao tác",
  fileName: "Tên tài liệu",
  documentType: "Loại hồ sơ tài liệu",
  count: "Số lượng bản ghi",
  format: "Định dạng xuất tệp",
  filter: "Bộ lọc trạng thái",
  page: "Trang dữ liệu",
  leadId: "Mã yêu cầu tư vấn",
  messageId: "Mã tin nhắn",
  contextSize: "Số tin nhắn ngữ cảnh",
  centerId: "Mã trung tâm",
  universityId: "Mã trường ĐH",
  role: "Vai trò",
  status: "Trạng thái",
  oldValue: "Giá trị trước đó",
  newValue: "Giá trị mới",
  note: "Ghi chú",
};

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

function getFriendlyActionName(action: string, defaultDescription?: string): string {
  if (ACTION_FRIENDLY_NAMES[action]) {
    return ACTION_FRIENDLY_NAMES[action];
  }
  if (defaultDescription && defaultDescription !== action) {
    return defaultDescription;
  }
  // Tự động chuẩn hóa chuỗi dạng "abc.xyz_def" thành "Abc › Xyz Def"
  const parts = action.split(/[._]/).filter(Boolean);
  if (parts.length > 0) {
    return parts.map((p) => p.charAt(0).toUpperCase() + p.slice(1).toLowerCase()).join(" › ");
  }
  return action;
}

function getFriendlyEntityName(entityType?: string | null): string {
  if (!entityType) return "Đối tượng chung";
  return ENTITY_FRIENDLY_NAMES[entityType.toLowerCase()] ?? entityType;
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
    if (action === "review_batch.view_item") {
      return `Đợt duyệt: ${data.batchId ? String(data.batchId).slice(0, 8) + "…" : "—"}`;
    }
    if (action === "parent.acting.stop" || action === "parent.acting.start") {
      const child = data.managedChildId ? `Con: ${String(data.managedChildId).slice(0, 8)}…` : "";
      return child || "Thao tác đại diện học sinh";
    }
    // generic key-values
    const entries = Object.entries(data).slice(0, 3);
    return entries
      .map(([k, v]) => {
        const label = FIELD_FRIENDLY_NAMES[k] ?? k;
        const valStr = typeof v === "object" ? JSON.stringify(v) : String(v);
        return `${label}: ${valStr.length > 15 ? valStr.slice(0, 15) + "…" : valStr}`;
      })
      .join(" • ");
  } catch {
    return detailJson.length > 60 ? detailJson.slice(0, 60) + "…" : detailJson;
  }
}

function getActionBadgeVariant(action: string): "reach" | "warn" | "ok" | "outline" | "neutral" {
  if (action === "center.document_viewed" || action.startsWith("center.")) return "reach";
  if (action.startsWith("lead_message.") || action.includes("lock") || action.includes("delete")) return "warn";
  if (action === "slm_sample.export") return "ok";
  if (action.startsWith("parent.")) return "neutral";
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
 * Quy tắc: Nhật ký bất biến, không sửa và không xóa được.
 */
export function AccessLogsView() {
  // Mặc định là "all_sensitive" (Dữ liệu nhạy cảm theo đúng đặc tả UC-97)
  const [category, setCategory] = useState<AccessLogCategory>("all_sensitive");
  const [searchTerm, setSearchTerm] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [page, setPage] = useState(1);
  const [detailItem, setDetailItem] = useState<AccessLogDto | null>(null);
  const [showRawJson, setShowRawJson] = useState(false);
  const [copiedId, setCopiedId] = useState(false);

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
    () => `access-logs:${category}:${debouncedSearch}:${fromDate}:${toDate}:${page}`,
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

  // Parse detail JSON thành danh sách key-value thân thiện
  const parsedDetailFields = useMemo(() => {
    if (!detailItem?.detailJson) return [];
    try {
      const parsed = JSON.parse(detailItem.detailJson);
      if (typeof parsed !== "object" || parsed === null) return [];
      return Object.entries(parsed).map(([key, val]) => ({
        label: FIELD_FRIENDLY_NAMES[key] ?? key,
        rawKey: key,
        value: typeof val === "object" ? JSON.stringify(val, null, 2) : String(val),
      }));
    } catch {
      return [];
    }
  }, [detailItem]);

  const copyToClipboard = (text: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedId(true);
    setTimeout(() => setCopiedId(false), 2000);
  };

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
              [UC-97] Giám sát hoạt động hệ thống và ai đã xem hoặc xuất dữ liệu nhạy cảm (tài liệu xác minh trung tâm, tin nhắn bị báo cáo, xuất mẫu ở UC-102).
            </p>
          </div>
          <Badge variant="ok" className="h-7 gap-1.5 px-3 text-xs font-semibold">
            <LockIcon className="size-3.5" />
            Nhật ký bất biến (Append-only)
          </Badge>
        </div>

        <div className="rounded-lg border border-border/80 bg-muted/40 p-3 text-xs text-muted-foreground">
          <span className="font-semibold text-foreground">Quy tắc bảo mật:</span> Dữ liệu nhật ký được ghi tuần tự, lưu trữ vĩnh viễn,
          chỉ cho phép đọc và không thể sửa đổi hoặc xóa dưới bất kỳ hình thức nào.
        </div>
      </div>

      {/* KPI Summary Cards */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {/* Card 1: Tổng lượt nhạy cảm & Toàn hệ thống */}
        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground flex items-center justify-between">
              <span>Tổng lượt nhạy cảm (UC-97)</span>
              {summaryData?.totalAllCount !== undefined && summaryData.totalAllCount > 0 && (
                <span className="text-[10px] text-muted-foreground font-normal">
                  / {summaryData.totalAllCount} tổng số
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <div>
                <span className="text-2xl font-bold text-foreground">
                  {summaryData?.totalSensitiveCount ?? 0}
                </span>
                <div className="text-[10px] text-muted-foreground mt-0.5">
                  Toàn hệ thống: {summaryData?.totalAllCount ?? logsData?.totalCount ?? 0} lượt
                </div>
              </div>
              <ShieldAlertIcon className="size-6 text-brand" />
            </div>
          </CardContent>
        </Card>

        {/* Card 2: Tài liệu xác minh */}
        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Xem tài liệu xác minh (UC-59)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <div>
                <span className="text-2xl font-bold text-reach">
                  {summaryData?.centerDocumentViews ?? 0}
                </span>
                <div className="text-[10px] text-muted-foreground mt-0.5">
                  Giấy phép & Hồ sơ trung tâm
                </div>
              </div>
              <FileTextIcon className="size-6 text-reach" />
            </div>
          </CardContent>
        </Card>

        {/* Card 3: Đọc tin nhắn bị báo cáo */}
        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Đọc tin nhắn bị báo cáo (UC-58)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <div>
                <span className="text-2xl font-bold text-warn">
                  {summaryData?.reportedMessageViews ?? 0}
                </span>
                <div className="text-[10px] text-muted-foreground mt-0.5">
                  Tin nhắn vi phạm & Ngữ cảnh
                </div>
              </div>
              <MailWarningIcon className="size-6 text-warn" />
            </div>
          </CardContent>
        </Card>

        {/* Card 4: Xuất mẫu SLM */}
        <Card size="sm" className="bg-card/80">
          <CardHeader className="pb-1">
            <CardTitle className="text-xs font-medium text-muted-foreground">
              Xuất mẫu SLM (UC-102)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center justify-between">
              <div>
                <span className="text-2xl font-bold text-ok">
                  {summaryData?.slmSampleExports ?? 0}
                </span>
                <div className="text-[10px] text-muted-foreground mt-0.5">
                  Tập dữ liệu huấn luyện AI
                </div>
              </div>
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
                    "flex cursor-pointer items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50",
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
                  placeholder="Tìm theo tên, email, IP hoặc mã thực thể..."
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
                className="px-2 text-xs"
              >
                Hôm nay
              </Button>
              <Button
                type="button"
                size="sm"
                variant="ghost"
                onClick={() => setPresetRange("7d")}
                className="px-2 text-xs"
              >
                7 ngày
              </Button>
              <Button
                type="button"
                size="icon-sm"
                variant="ghost"
                onClick={handleResetFilters}
                title="Đặt lại bộ lọc về mặc định"
                aria-label="Đặt lại bộ lọc về mặc định"
                className="text-muted-foreground hover:text-foreground"
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
                <TableHead className="w-60 text-xs font-semibold">Thao tác</TableHead>
                <TableHead className="text-xs font-semibold">Đối tượng & Chi tiết</TableHead>
                <TableHead className="w-36 text-xs font-semibold">IP & Thiết bị</TableHead>
                <TableHead className="w-20 text-right text-xs font-semibold">Chi tiết</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-44 text-center text-sm text-muted-foreground">
                    {loading ? (
                      "Đang tải nhật ký..."
                    ) : category === "all_sensitive" ? (
                      <div className="flex flex-col items-center justify-center gap-2 py-4">
                        <ShieldCheckIcon className="size-8 text-muted-foreground/60" />
                        <p className="font-medium text-foreground">
                          Chưa ghi nhận lượt truy cập dữ liệu nhạy cảm nào
                        </p>
                        <p className="text-xs text-muted-foreground max-w-md">
                          Chưa có lượt xem tài liệu xác minh (UC-59), đọc tin nhắn báo cáo (UC-58) hay xuất mẫu SLM (UC-102) trong khoảng thời gian này.
                        </p>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            setCategory("all");
                            setPage(1);
                          }}
                          className="mt-2 text-xs gap-1.5"
                        >
                          <UsersIcon className="size-3.5" />
                          <span>Xem tất cả nhật ký hệ thống</span>
                        </Button>
                      </div>
                    ) : (
                      "Không tìm thấy dòng nhật ký nào phù hợp trong danh mục này."
                    )}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((log) => {
                  const friendlyAction = getFriendlyActionName(log.action, log.actionDescription);
                  const friendlyEntity = getFriendlyEntityName(log.entityType);

                  return (
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
                            <span className="text-xs text-muted-foreground truncate max-w-[200px]" title={log.actorEmail}>
                              {log.actorEmail}
                            </span>
                          )}
                        </div>
                      </TableCell>

                      {/* Action */}
                      <TableCell>
                        <div className="flex flex-col gap-1">
                          <span className="text-xs font-semibold text-foreground">
                            {friendlyAction}
                          </span>
                          <div className="flex items-center gap-1.5">
                            <Badge
                              variant={getActionBadgeVariant(log.action)}
                              className="text-[10px] h-4 px-1.5 font-normal"
                            >
                              {log.actionCategory || "Hệ thống"}
                            </Badge>
                            <span className="font-mono text-[10px] text-muted-foreground truncate max-w-[140px]" title={log.action}>
                              {log.action}
                            </span>
                          </div>
                        </div>
                      </TableCell>

                      {/* Summary Detail */}
                      <TableCell className="max-w-[320px]">
                        <div className="flex flex-col gap-1">
                          <span className="text-xs text-foreground truncate" title={log.detailJson ?? ""}>
                            {summarizeDetail(log.action, log.detailJson)}
                          </span>
                          {log.entityType && (
                            <div className="flex items-center gap-1 text-[11px] text-muted-foreground">
                              <span className="font-medium text-foreground/80">{friendlyEntity}</span>
                              {log.entityId && (
                                <span className="font-mono text-[10px] text-muted-foreground/70">
                                  #{log.entityId.slice(0, 8)}
                                </span>
                              )}
                            </div>
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
                          onClick={() => {
                            setDetailItem(log);
                            setShowRawJson(false);
                            setCopiedId(false);
                          }}
                          className="px-2 text-xs gap-1 hover:text-brand"
                          title="Xem chi tiết đầy đủ"
                        >
                          <EyeIcon className="size-3.5" />
                          <span>Xem</span>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })
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

      {/* Detail Dialog - Fix tràn giao diện bằng sm:max-w-2xl và layout cuộn riêng */}
      <Dialog open={detailItem !== null} onOpenChange={(open) => !open && setDetailItem(null)}>
        <DialogContent className="w-full sm:max-w-2xl max-h-[88vh] flex flex-col p-6 overflow-hidden">
          <DialogHeader className="shrink-0 pb-2 border-b border-border">
            <div className="flex items-center justify-between gap-2 pr-6">
              <DialogTitle className="flex items-center gap-2 text-base font-bold">
                <ShieldAlertIcon className="size-5 text-brand" />
                Chi tiết nhật ký truy cập
              </DialogTitle>
            </div>
            <DialogDescription className="flex items-center gap-2 pt-1 text-xs">
              <span>Mã bản ghi:</span>
              <code className="font-mono font-medium text-foreground bg-muted px-1.5 py-0.5 rounded text-[11px]">
                {detailItem?.id}
              </code>
              {detailItem?.id && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-xs"
                  onClick={() => copyToClipboard(detailItem.id)}
                  className="text-muted-foreground hover:text-foreground"
                  title="Sao chép mã bản ghi"
                  aria-label="Sao chép mã bản ghi"
                >
                  {copiedId ? <CheckIcon className="size-3 text-ok" /> : <CopyIcon className="size-3" />}
                </Button>
              )}
            </DialogDescription>
          </DialogHeader>

          {detailItem && (
            <div className="flex-1 overflow-y-auto pr-1 space-y-4 py-2 text-xs">
              {/* Thẻ 1: Thông tin người thực hiện & Thiết bị */}
              <div className="rounded-lg border border-border bg-card p-3 shadow-xs space-y-2.5">
                <div className="font-semibold text-foreground text-xs border-b border-border/60 pb-1 flex items-center justify-between">
                  <span>Thông tin thực hiện & Thiết bị</span>
                  <Badge variant={getRoleBadgeVariant(detailItem.actorRole)} className="text-[10px]">
                    {getRoleLabel(detailItem.actorRole)}
                  </Badge>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 min-w-0">
                  <div className="space-y-0.5 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Người thực hiện:</span>
                    <div className="font-medium text-foreground truncate">
                      {detailItem.actorFullName || (detailItem.actorUserId ? "Người dùng ẩn" : "Hệ thống")}
                    </div>
                    {detailItem.actorEmail && (
                      <div className="text-muted-foreground text-[11px] truncate" title={detailItem.actorEmail}>
                        {detailItem.actorEmail}
                      </div>
                    )}
                  </div>

                  <div className="space-y-0.5 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Thời gian ghi nhận:</span>
                    <div className="font-medium text-foreground">
                      {formatVietnamDateTime(detailItem.createdAt)} (Việt Nam)
                    </div>
                    <div className="font-mono text-[10px] text-muted-foreground truncate">
                      UTC: {detailItem.createdAt}
                    </div>
                  </div>

                  <div className="space-y-0.5 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Địa chỉ IP:</span>
                    <div className="font-mono text-foreground">{detailItem.ipAddress || "Không ghi nhận"}</div>
                  </div>

                  <div className="space-y-0.5 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Mã người dùng (Actor ID):</span>
                    <div className="font-mono text-muted-foreground text-[11px] break-all">
                      {detailItem.actorUserId || "null (Hệ thống)"}
                    </div>
                  </div>

                  <div className="sm:col-span-2 space-y-0.5 min-w-0">
                    <span className="text-muted-foreground text-[11px]">User Agent / Trình duyệt:</span>
                    <div className="text-[11px] text-muted-foreground break-all bg-muted/40 p-2 rounded border border-border/50">
                      {detailItem.userAgent || "Không có thông tin"}
                    </div>
                  </div>
                </div>
              </div>

              {/* Thẻ 2: Hành động & Đối tượng thao tác */}
              <div className="rounded-lg border border-border bg-card p-3 shadow-xs space-y-2.5">
                <div className="font-semibold text-foreground text-xs border-b border-border/60 pb-1">
                  Hành động & Đối tượng
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 min-w-0">
                  <div className="space-y-1 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Tên thao tác:</span>
                    <div className="text-sm font-bold text-foreground">
                      {getFriendlyActionName(detailItem.action, detailItem.actionDescription)}
                    </div>
                    <div className="flex items-center gap-1.5">
                      <Badge variant={getActionBadgeVariant(detailItem.action)} className="text-[10px]">
                        {detailItem.actionCategory || "Hệ thống"}
                      </Badge>
                      <code className="font-mono text-[11px] text-muted-foreground">
                        {detailItem.action}
                      </code>
                    </div>
                  </div>

                  <div className="space-y-1 min-w-0">
                    <span className="text-muted-foreground text-[11px]">Đối tượng tác động:</span>
                    <div className="font-semibold text-foreground">
                      {getFriendlyEntityName(detailItem.entityType)}
                    </div>
                    <div className="font-mono text-[11px] text-muted-foreground break-all">
                      Loại: <span className="text-foreground">{detailItem.entityType || "—"}</span>
                      {detailItem.entityId && (
                        <span> • ID: #{detailItem.entityId}</span>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* Thẻ 3: Dữ liệu chi tiết thao tác */}
              <div className="rounded-lg border border-border bg-card p-3 shadow-xs space-y-2.5">
                <div className="flex items-center justify-between border-b border-border/60 pb-1">
                  <span className="font-semibold text-foreground text-xs">Chi tiết dữ liệu thực hiện</span>
                  {detailItem.detailJson && (
                    <button
                      type="button"
                      onClick={() => setShowRawJson((prev) => !prev)}
                      className="text-[11px] text-brand hover:underline font-medium flex items-center gap-1 cursor-pointer rounded-sm focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
                    >
                      {showRawJson ? "Ẩn JSON gốc" : "Xem JSON kỹ thuật"}
                      {showRawJson ? <ChevronDownIcon className="size-3" /> : <ChevronRightIcon className="size-3" />}
                    </button>
                  )}
                </div>

                {parsedDetailFields.length > 0 ? (
                  <div className="grid grid-cols-1 gap-2">
                    {parsedDetailFields.map((field) => (
                      <div
                        key={field.rawKey}
                        className="flex flex-col sm:flex-row sm:items-center justify-between gap-1 p-2 rounded bg-muted/40 border border-border/40 text-xs"
                      >
                        <span className="font-medium text-muted-foreground sm:w-1/3">
                          {field.label}:
                        </span>
                        <span className="font-mono text-foreground sm:w-2/3 break-all">
                          {field.value}
                        </span>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-muted-foreground text-xs italic p-1">
                    {detailItem.detailJson ? "Dữ liệu định dạng đặc biệt." : "Không có thuộc tính chi tiết đính kèm."}
                  </div>
                )}

                {/* Raw JSON viewer khi bật */}
                {showRawJson && detailItem.detailJson && (
                  <div className="mt-2 space-y-1">
                    <span className="text-[11px] text-muted-foreground">Chuỗi JSON gốc:</span>
                    <pre className="max-h-52 overflow-x-auto overflow-y-auto rounded border border-border bg-muted/80 p-2.5 font-mono text-[11px] leading-relaxed text-foreground break-all whitespace-pre-wrap">
                      {(() => {
                        try {
                          return JSON.stringify(JSON.parse(detailItem.detailJson), null, 2);
                        } catch {
                          return detailItem.detailJson;
                        }
                      })()}
                    </pre>
                  </div>
                )}
              </div>
            </div>
          )}

          <DialogFooter className="shrink-0 mt-3 pt-3 border-t border-border flex justify-end">
            <Button type="button" variant="outline" onClick={() => setDetailItem(null)}>
              Đóng
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
