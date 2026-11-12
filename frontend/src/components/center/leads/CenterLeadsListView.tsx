"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  InboxIcon,
  PhoneIcon,
  ShieldCheckIcon,
  LockIcon,
  SearchIcon,
  RotateCcwIcon,
  ClockIcon,
  CheckCheckIcon,
  CopyIcon,
  GraduationCapIcon,
  InfoIcon,
  MessageSquareIcon,
} from "lucide-react";
import { centerLeadsApi, ApiError } from "@/lib/api";
import { LeadConversationPanel } from "./LeadConversationPanel";
import type {
  CenterReceivedLeadDto,
  CenterLeadsSummaryDto,
} from "@/types/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import {
  DataTable,
  type DataTableColumn,
  type DataTableToolbarSlots,
} from "@/components/admin/shared/DataTable";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { OptionSelect } from "@/components/admin/universities/OptionSelect";
import { cn } from "@/lib/utils";

const LEAD_STATUS_FILTER_OPTIONS: { value: string; label: string }[] = [
  { value: "all", label: "Tất cả trạng thái" },
  { value: "new", label: "Mới nhận" },
  { value: "contacted", label: "Đã liên hệ" },
  { value: "closed", label: "Đã đóng" },
];

type StatusFilter = "all" | "new" | "contacted" | "closed";

function formatStudyLevel(level?: string | null): string {
  if (!level) return "Chưa xác định";
  const map: Record<string, string> = {
    bachelor: "Đại học (Cử nhân)",
    undergraduate: "Đại học (Cử nhân)",
    community_college: "Cao đẳng cộng đồng (2+2)",
    master: "Thạc sĩ (Master)",
    phd: "Tiến sĩ (PhD)",
    high_school: "Trung học (High School)",
    highschool: "Trung học phổ thông",
    secondary: "Trung học (THCS/THPT)",
    test_prep: "Luyện thi SAT / IELTS",
    exchange: "Giao lưu văn hóa",
    transfer: "Chuyển tiếp (Transfer)",
  };
  return map[level.toLowerCase()] ?? level;
}

function formatBudgetTier(tier?: string | null): string {
  if (!tier) return "Chưa xác định";
  const map: Record<string, string> = {
    tier_under_20k: "Dưới $20,000 / năm",
    tier_20k_35k: "$20,000 - $35,000 / năm",
    tier_35k_50k: "$35,000 - $50,000 / năm",
    tier_50k_70k: "$50,000 - $70,000 / năm",
    tier_above_70k: "Trên $70,000 / năm (Không giới hạn)",
  };
  return map[tier] ?? tier;
}

function LeadStatusBadge({ status }: { status: string }) {
  if (status === "new") return <Badge variant="brand">Mới nhận</Badge>;
  if (status === "contacted") return <Badge variant="warn">Đang trao đổi</Badge>;
  if (status === "closed") return <Badge variant="ok">Đã hoàn tất</Badge>;
  return null;
}

export function CenterLeadsListView() {
  const [data, setData] = useState<CenterLeadsSummaryDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 10;

  // Selected lead for detail modal
  const [selectedLead, setSelectedLead] = useState<CenterReceivedLeadDto | null>(null);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);

  const fetchLeads = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await centerLeadsApi.list({
        page,
        pageSize,
        status: statusFilter === "all" ? undefined : statusFilter,
        search: search.trim() || undefined,
      });
      setData(res);
    } catch (err: unknown) {
      const msg = err instanceof ApiError ? err.message : "Không thể tải danh sách yêu cầu.";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, [page, pageSize, statusFilter, search]);

  useEffect(() => {
    let ignore = false;
    async function load() {
      try {
        const res = await centerLeadsApi.list({
          page,
          pageSize,
          status: statusFilter === "all" ? undefined : statusFilter,
          search: search.trim() || undefined,
        });
        if (!ignore) {
          setData(res);
          setIsLoading(false);
        }
      } catch (err: unknown) {
        if (!ignore) {
          const msg = err instanceof ApiError ? err.message : "Không thể tải danh sách yêu cầu.";
          setError(msg);
          setIsLoading(false);
        }
      }
    }
    load();
    return () => {
      ignore = true;
    };
  }, [page, pageSize, statusFilter, search]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput.trim());
    setPage(1);
  };

  const handleResetFilters = () => {
    setStatusFilter("all");
    setSearchInput("");
    setSearch("");
    setPage(1);
  };

  const applyLeadStatusUpdate = useCallback(
    (leadId: string, newStatus: string, updatedLead?: CenterReceivedLeadDto) => {
      setData((prev) => {
        if (!prev) return null;
        const previousStatus = prev.items.find((i) => i.id === leadId)?.status;
        if (!previousStatus || previousStatus === newStatus) {
          return updatedLead
            ? {
                ...prev,
                items: prev.items.map((item) => (item.id === leadId ? updatedLead : item)),
              }
            : prev;
        }

        const delta = (target: string) =>
          newStatus === target ? 1 : previousStatus === target ? -1 : 0;

        return {
          ...prev,
          items: prev.items.map((item) =>
            item.id === leadId ? (updatedLead ?? { ...item, status: newStatus }) : item
          ),
          newCount: prev.newCount + delta("new"),
          contactedCount: prev.contactedCount + delta("contacted"),
          closedCount: prev.closedCount + delta("closed"),
        };
      });

      setSelectedLead((prev) => {
        if (!prev || prev.id !== leadId) return prev;
        return updatedLead ?? { ...prev, status: newStatus };
      });
    },
    []
  );

  const handleStatusChange = async (leadId: string, newStatus: string) => {
    setIsUpdatingStatus(true);
    try {
      const updated = await centerLeadsApi.updateStatus(leadId, newStatus);
      toast.add({
        type: "success",
        title: "Cập nhật trạng thái thành công",
        description: `Yêu cầu đã được chuyển sang trạng thái "${
          newStatus === "new" ? "Mới nhận" : newStatus === "contacted" ? "Đã liên hệ" : "Đã đóng"
        }".`,
      });

      applyLeadStatusUpdate(leadId, updated.status, updated);
    } catch {
      toast.add({
        type: "error",
        title: "Lỗi cập nhật",
        description: "Không thể cập nhật trạng thái yêu cầu. Vui lòng thử lại.",
      });
    } finally {
      setIsUpdatingStatus(false);
    }
  };

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text);
    toast.add({
      type: "info",
      title: "Đã sao chép",
      description: `${label}: ${text}`,
    });
  };

  const totalCount = data?.totalCount ?? 0;
  const newCount = data?.newCount ?? 0;
  const contactedCount = data?.contactedCount ?? 0;
  const closedCount = data?.closedCount ?? 0;
  const items = data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const hasActiveFilter = statusFilter !== "all" || search.length > 0;

  const leadColumns: DataTableColumn<CenterReceivedLeadDto>[] = [
    {
      id: "createdAt",
      label: "Thời gian",
      render: (lead) => (
        <div className="whitespace-nowrap text-xs text-ink-2">
          <div className="flex items-center gap-1.5 font-medium text-ink">
            {lead.status === "new" && (
              <span className="size-1.5 rounded-full bg-brand shrink-0" aria-hidden />
            )}
            {new Date(lead.createdAt).toLocaleDateString("vi-VN")}
          </div>
          <div className="text-ink-3">
            {new Date(lead.createdAt).toLocaleTimeString("vi-VN", {
              hour: "2-digit",
              minute: "2-digit",
            })}
          </div>
        </div>
      ),
    },
    {
      id: "student",
      label: "Người gửi",
      required: true,
      render: (lead) =>
        lead.sharedProfile && lead.studentName ? (
          <div className="min-w-0 max-w-[190px]">
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                setSelectedLead(lead);
              }}
              className="font-medium text-brand hover:underline text-left truncate block max-w-full"
              title={lead.studentName}
            >
              {lead.studentName}
            </button>
            {lead.studentEmail && (
              <div className="text-xs text-ink-2 truncate" title={lead.studentEmail}>
                {lead.studentEmail}
              </div>
            )}
          </div>
        ) : (
          <span className="text-ink-3 italic flex items-center gap-1 whitespace-nowrap text-xs">
            <LockIcon className="size-3 shrink-0" />
            Học sinh ẩn danh
          </span>
        ),
    },
    {
      id: "phone",
      label: "Số điện thoại",
      render: (lead) =>
        lead.contactPhone ? (
          <div className="flex items-center gap-1.5 whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
            <a
              href={`tel:${lead.contactPhone}`}
              className="font-mono text-brand font-medium hover:underline inline-flex items-center gap-1 text-xs"
            >
              <PhoneIcon className="size-3 shrink-0" />
              {lead.contactPhone}
            </a>
            <Button
              type="button"
              variant="ghost"
              size="icon-xs"
              onClick={() => copyToClipboard(lead.contactPhone!, "Số điện thoại")}
              title="Sao chép số điện thoại"
              className="size-6 text-ink-3 hover:text-ink"
            >
              <CopyIcon className="size-3" />
            </Button>
          </div>
        ) : (
          <span className="text-xs text-ink-3">Chưa có</span>
        ),
    },
    {
      id: "studyLevel",
      label: "Bậc học",
      render: (lead) => (
        <Badge variant="secondary" className="font-normal text-xs whitespace-nowrap">
          {formatStudyLevel(lead.studyLevel)}
        </Badge>
      ),
    },
    {
      id: "message",
      label: "Lời nhắn",
      render: (lead) => (
        <p className="truncate max-w-[190px] text-xs text-ink" title={lead.message ?? ""}>
          {lead.message || <span className="text-ink-3 italic">Không có lời nhắn</span>}
        </p>
      ),
    },
    {
      id: "sharedProfile",
      label: "Hồ sơ",
      render: (lead) =>
        lead.sharedProfile ? (
          <Badge variant="ok" className="gap-1 text-xs whitespace-nowrap">
            <ShieldCheckIcon className="size-3" />
            Đã chia sẻ
          </Badge>
        ) : (
          <Badge variant="neutral" className="gap-1 text-xs whitespace-nowrap">
            <LockIcon className="size-3" />
            Chỉ liên hệ
          </Badge>
        ),
    },
    {
      id: "status",
      label: "Trạng thái",
      render: (lead) => <LeadStatusBadge status={lead.status} />,
    },
    {
      id: "actions",
      label: "Thao tác",
      required: true,
      headerClassName: "w-px whitespace-nowrap text-right",
      cellClassName: "w-px text-right whitespace-nowrap",
      render: (lead) => (
        <div onClick={(e) => e.stopPropagation()} className="inline-flex justify-end">
          <Button
            type="button"
            variant="outline"
            size="xs"
            onClick={() => setSelectedLead(lead)}
            className="gap-1"
          >
            <MessageSquareIcon className="size-3" />
            Chi tiết &amp; Trả lời
          </Button>
        </div>
      ),
    },
  ];

  const leadToolbar: DataTableToolbarSlots = {
    filters: (
      <OptionSelect
        id="lead-status-filter"
        value={statusFilter}
        options={LEAD_STATUS_FILTER_OPTIONS}
        onChange={(val) => {
          setStatusFilter(val as StatusFilter);
          setPage(1);
        }}
        size="sm"
        className="w-[11.5rem] bg-surface"
      />
    ),
    search: (
      <form onSubmit={handleSearchSubmit} role="search" className="relative">
        <Input
          id="center-leads-search"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder="Tìm theo SĐT, tên, lời nhắn..."
          aria-label="Tìm kiếm yêu cầu tư vấn"
          className="pr-10 bg-surface"
        />
        <Button
          type="submit"
          variant="ghost"
          size="icon-sm"
          className="absolute top-1/2 right-1 -translate-y-1/2"
          aria-label="Tìm kiếm"
        >
          <SearchIcon className="size-4" />
        </Button>
      </form>
    ),
    actions: (
      <>
        {hasActiveFilter && (
          <Button type="button" variant="outline" size="sm" onClick={handleResetFilters}>
            Xóa lọc
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void fetchLeads()}
          disabled={isLoading}
          className="gap-1.5"
        >
          <RotateCcwIcon className={cn("size-3.5", isLoading && "animate-spin")} />
          Làm mới
        </Button>
      </>
    ),
  };

  return (
    <div className="space-y-6 py-6 pb-14">
      {/* Page Header */}
      <div className="border-b border-line pb-5">
        <div className="flex flex-wrap items-center gap-2.5">
          <h1 className="text-h2">
            Yêu cầu liên hệ &amp; tư vấn nhận được
          </h1>
          <Badge variant="brand" className="text-xs">
            Cổng đối tác
          </Badge>
        </div>
        <p className="text-sm text-ink-2 mt-1">
          Xem và xử lý các yêu cầu liên hệ từ học sinh và phụ huynh. Tuân thủ quyền riêng tư dữ liệu theo Nghị định 356/2025/NĐ-CP.
        </p>
      </div>

      {/* KPI Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4">
        {/* Total */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-brand/40",
            statusFilter === "all" ? "ring-2 ring-brand border-brand" : "border-line"
          )}
          onClick={() => {
            setStatusFilter("all");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-ink-2">Tổng yêu cầu</p>
              <p className="font-heading text-2xl font-bold text-ink tabular-nums mt-0.5">{totalCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-ground flex items-center justify-center text-ink-2">
              <InboxIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* New */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-brand/40",
            statusFilter === "new" ? "ring-2 ring-brand border-brand bg-brand-wash/35" : "border-line"
          )}
          onClick={() => {
            setStatusFilter("new");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-brand">Chờ xử lý (Mới)</p>
              <p className="font-heading text-2xl font-bold text-brand-ink tabular-nums mt-0.5">{newCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-brand-wash flex items-center justify-center text-brand">
              <ClockIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* Contacted */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-warn/40",
            statusFilter === "contacted" ? "ring-2 ring-warn border-warn bg-warn-wash/40" : "border-line"
          )}
          onClick={() => {
            setStatusFilter("contacted");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-warn">Đang trao đổi</p>
              <p className="font-heading text-2xl font-bold text-warn tabular-nums mt-0.5">{contactedCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-warn-wash flex items-center justify-center text-warn">
              <PhoneIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* Closed */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-ok/40",
            statusFilter === "closed" ? "ring-2 ring-ok border-ok bg-ok-wash/40" : "border-line"
          )}
          onClick={() => {
            setStatusFilter("closed");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-ok">Đã hoàn tất</p>
              <p className="font-heading text-2xl font-bold text-ok tabular-nums mt-0.5">{closedCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-ok-wash flex items-center justify-center text-ok">
              <CheckCheckIcon className="size-4" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Main DataTable */}
      <DataTable
        columns={leadColumns}
        items={items}
        getRowId={(lead) => lead.id}
        selectedRowId={selectedLead?.id}
        onRowSelect={(lead) => setSelectedLead(lead)}
        isLoading={isLoading}
        error={error ?? undefined}
        emptyMessage={
          hasActiveFilter
            ? "Không tìm thấy yêu cầu liên hệ nào khớp với bộ lọc hiện tại."
            : "Trung tâm của bạn hiện chưa nhận được yêu cầu liên hệ mới nào từ học sinh hoặc phụ huynh."
        }
        label="Danh sách yêu cầu tư vấn"
        toolbar={leadToolbar}
        footer={
          <TablePagination
            page={page}
            totalPages={totalPages}
            totalCount={totalCount}
            itemLabel="yêu cầu"
            disabled={isLoading}
            onPageChange={setPage}
          />
        }
      />

      {/* Detail Dialog */}
      <Dialog open={selectedLead !== null} onOpenChange={(open) => !open && setSelectedLead(null)}>
        <DialogContent className="w-full sm:max-w-3xl max-h-[90vh] overflow-y-auto p-6">
          {selectedLead && (
            <>
              <DialogHeader>
                <div className="flex flex-wrap items-center justify-between gap-2 pr-8">
                  <DialogTitle className="text-xl font-bold flex items-center gap-2">
                    <InboxIcon className="size-5 text-brand shrink-0" />
                    Chi tiết yêu cầu liên hệ
                  </DialogTitle>
                  <LeadStatusBadge status={selectedLead.status} />
                </div>
                <DialogDescription>
                  Gửi lúc {new Date(selectedLead.createdAt).toLocaleString("vi-VN")} tới trung tâm {selectedLead.studyCenterName}.
                </DialogDescription>
              </DialogHeader>

              <div className="space-y-5 my-2 min-w-0">
                {/* Contact Card */}
                <Card size="sm" className="bg-muted/30">
                  <CardContent className="pt-4 space-y-3">
                    <h4 className="text-xs font-semibold text-muted-foreground">
                      Thông tin liên hệ
                    </h4>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
                      <div className="min-w-0">
                        <span className="text-xs text-muted-foreground block">Người gửi:</span>
                        <span className="font-semibold text-foreground break-words">
                          {selectedLead.sharedProfile && selectedLead.studentName ? (
                            selectedLead.studentName
                          ) : (
                            <span className="italic text-muted-foreground flex items-center gap-1 mt-0.5">
                              <LockIcon className="size-3" />
                              Học sinh ẩn danh (không chia sẻ họ tên)
                            </span>
                          )}
                        </span>
                      </div>

                      <div className="min-w-0">
                        <span className="text-xs text-muted-foreground block">Email:</span>
                        <span className="font-medium text-foreground block min-w-0">
                          {selectedLead.sharedProfile && selectedLead.studentEmail ? (
                            <a
                              href={`mailto:${selectedLead.studentEmail}`}
                              className="text-brand hover:underline break-all inline-block max-w-full focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded-xs"
                            >
                              {selectedLead.studentEmail}
                            </a>
                          ) : (
                            <span className="italic text-muted-foreground">Không chia sẻ</span>
                          )}
                        </span>
                      </div>

                      <div>
                        <span className="text-xs text-muted-foreground block">Số điện thoại liên hệ:</span>
                        {selectedLead.contactPhone ? (
                          <div className="flex items-center gap-2 mt-0.5">
                            <a
                              href={`tel:${selectedLead.contactPhone}`}
                              className="font-mono font-bold text-brand hover:underline flex items-center gap-1.5"
                            >
                              <PhoneIcon className="size-3.5" />
                              {selectedLead.contactPhone}
                            </a>
                            <Button
                              variant="ghost"
                              size="icon-xs"
                              onClick={() => copyToClipboard(selectedLead.contactPhone!, "Số điện thoại")}
                              title="Sao chép"
                              className="size-6 text-muted-foreground hover:text-foreground"
                            >
                              <CopyIcon className="size-3" />
                            </Button>
                          </div>
                        ) : (
                          <span className="text-muted-foreground">Chưa có</span>
                        )}
                      </div>

                      <div>
                        <span className="text-xs text-muted-foreground block">Bậc học quan tâm:</span>
                        <span className="font-medium text-foreground">
                          {formatStudyLevel(selectedLead.studyLevel)}
                        </span>
                      </div>
                    </div>
                  </CardContent>
                </Card>

                {/* Message Content */}
                <div className="space-y-1.5">
                  <h4 className="text-xs font-semibold text-muted-foreground">
                    Lời nhắn từ học sinh / phụ huynh
                  </h4>
                  <div className="rounded-lg border bg-card p-3.5 text-sm text-foreground whitespace-pre-wrap leading-relaxed">
                    {selectedLead.message || (
                      <span className="text-muted-foreground italic">Không có nội dung tin nhắn.</span>
                    )}
                  </div>
                </div>

                {/* Shared Profile Section */}
                <div className="space-y-2 min-w-0">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h4 className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
                      <GraduationCapIcon className="size-4 text-brand shrink-0" />
                      Hồ sơ học sinh được chia sẻ
                    </h4>
                    {selectedLead.sharedProfile ? (
                      <Badge variant="ok" className="text-xs gap-1">
                        <ShieldCheckIcon className="size-3" />
                        Đã cấp quyền chia sẻ (Nghị định 356)
                      </Badge>
                    ) : (
                      <Badge variant="neutral" className="text-xs gap-1">
                        <LockIcon className="size-3" />
                        Chưa cấp quyền chia sẻ
                      </Badge>
                    )}
                  </div>

                  {selectedLead.sharedProfile && selectedLead.profileSummary ? (
                    <Card size="sm" className="border-ok/30 bg-ok-wash/25">
                      <CardContent className="pt-4 grid grid-cols-2 sm:grid-cols-3 gap-3 text-xs">
                        {/* GPA */}
                        <div className="p-2.5 rounded-lg bg-surface border border-line min-w-0">
                          <span className="text-ink-3 block">GPA tích lũy:</span>
                          <span className="font-heading text-base font-bold text-ok tabular-nums">
                            {selectedLead.profileSummary.overallGpa != null
                              ? `${selectedLead.profileSummary.overallGpa} / 4.0`
                              : "Chưa cập nhật"}
                          </span>
                        </div>

                        {/* Major */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">Ngành dự kiến:</span>
                          <span className="font-semibold text-foreground break-words">
                            {selectedLead.profileSummary.intendedMajor || "Chưa chọn ngành"}
                          </span>
                        </div>

                        {/* Grade / Level */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">Khối lớp hiện tại:</span>
                          <span className="font-semibold text-foreground break-words">
                            {selectedLead.profileSummary.currentGrade || "Chưa cập nhật"}
                          </span>
                        </div>

                        {/* English Test */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">Chứng chỉ tiếng Anh:</span>
                          <span className="font-semibold text-foreground break-words">
                            {selectedLead.profileSummary.englishTestType
                              ? `${selectedLead.profileSummary.englishTestType.toUpperCase()}: ${
                                  selectedLead.profileSummary.englishScore ?? "Chưa có điểm"
                                }`
                              : "Chưa có"}
                          </span>
                        </div>

                        {/* Standardized Test */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">SAT / ACT:</span>
                          <span className="font-semibold text-foreground break-words">
                            {selectedLead.profileSummary.satScore
                              ? `SAT: ${selectedLead.profileSummary.satScore}`
                              : selectedLead.profileSummary.actScore
                                ? `ACT: ${selectedLead.profileSummary.actScore}`
                                : "Chưa có"}
                          </span>
                        </div>

                        {/* EC Tier */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">Bậc ngoại khóa:</span>
                          <span className="font-semibold text-foreground break-words">
                            {selectedLead.profileSummary.extracurricularTier
                              ? `Bậc ${selectedLead.profileSummary.extracurricularTier}`
                              : "Chưa phân tích"}
                          </span>
                        </div>

                        {/* Budget */}
                        <div className="p-2.5 rounded-lg bg-card border col-span-2 sm:col-span-3 min-w-0">
                          <span className="text-muted-foreground block">Khoảng ngân sách dự kiến:</span>
                          <span className="font-semibold text-brand break-words">
                            {formatBudgetTier(selectedLead.profileSummary.budgetTier)}
                          </span>
                        </div>
                      </CardContent>
                    </Card>
                  ) : (
                    <div className="rounded-lg border border-dashed p-4 text-center bg-muted/20">
                      <InfoIcon className="size-5 text-muted-foreground mx-auto mb-1" />
                      <p className="text-xs text-muted-foreground leading-relaxed max-w-md mx-auto">
                        Học sinh lựa chọn không chia sẻ hồ sơ học thuật theo Nghị định 356/2025/NĐ-CP và chính sách bảo vệ dữ liệu.
                        Trung tâm vui lòng liên hệ qua số điện thoại để tư vấn và trao đổi thêm thông tin.
                      </p>
                    </div>
                  )}
                </div>

                {/* [UC-74] Lead Conversation & Reply Panel */}
                <LeadConversationPanel
                  key={selectedLead.id}
                  leadId={selectedLead.id}
                  leadStatus={selectedLead.status}
                  onLeadStatusChange={(newStatus) =>
                    applyLeadStatusUpdate(selectedLead.id, newStatus)
                  }
                />

                {/* Status Transition Control */}
                <div className="space-y-2 pt-2 border-t">
                  <h4 className="text-xs font-semibold text-muted-foreground">
                    Chuyển trạng thái xử lý
                  </h4>
                  <div className="flex flex-wrap items-center gap-2">
                    {selectedLead.status !== "new" && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={isUpdatingStatus}
                        onClick={() => handleStatusChange(selectedLead.id, "new")}
                        className="text-xs gap-1.5"
                      >
                        <ClockIcon className="size-3.5 text-brand" />
                        Chuyển về Mới nhận
                      </Button>
                    )}

                    {selectedLead.status !== "contacted" && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={isUpdatingStatus}
                        onClick={() => handleStatusChange(selectedLead.id, "contacted")}
                        className="text-xs gap-1.5 text-warn border-warn/30 bg-warn-wash/60 hover:bg-warn-wash"
                      >
                        <PhoneIcon className="size-3.5" />
                        Đã liên hệ với học sinh
                      </Button>
                    )}

                    {selectedLead.status !== "closed" && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={isUpdatingStatus}
                        onClick={() => handleStatusChange(selectedLead.id, "closed")}
                        className="text-xs gap-1.5 text-ok border-ok/30 bg-ok-wash/60 hover:bg-ok-wash"
                      >
                        <CheckCheckIcon className="size-3.5" />
                        Đóng yêu cầu (Hoàn tất)
                      </Button>
                    )}
                  </div>
                </div>
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => setSelectedLead(null)}>
                  Đóng
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
