"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  InboxIcon,
  PhoneIcon,
  ShieldCheckIcon,
  LockIcon,
  SearchIcon,
  RefreshCwIcon,
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
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

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

export function CenterLeadsListView() {
  const [data, setData] = useState<CenterLeadsSummaryDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
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
    setPage(1);
    fetchLeads();
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

  return (
    <div className="space-y-6 max-w-7xl mx-auto px-4 py-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b pb-5">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Yêu cầu liên hệ & tư vấn nhận được
            </h1>
            <Badge variant="outline" className="bg-brand/5 border-brand/20 text-brand text-xs">
              Cổng đối tác
            </Badge>
          </div>
          <p className="text-sm text-muted-foreground mt-1">
            Xem và xử lý các yêu cầu liên hệ từ học sinh và phụ huynh. Tuân thủ quyền riêng tư dữ liệu theo Nghị định 356/2025/NĐ-CP.
          </p>
        </div>

        <Button
          variant="outline"
          size="sm"
          onClick={fetchLeads}
          disabled={isLoading}
          className="gap-2 shrink-0 self-start sm:self-auto"
        >
          <RefreshCwIcon className={cn("size-3.5", isLoading && "animate-spin")} />
          Làm mới
        </Button>
      </div>

      {/* KPI Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4">
        {/* Total */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-brand/40",
            statusFilter === "all" ? "ring-2 ring-brand border-brand" : "border-border"
          )}
          onClick={() => {
            setStatusFilter("all");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-muted-foreground">Tổng yêu cầu</p>
              <p className="text-2xl font-bold text-foreground mt-0.5">{totalCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-muted flex items-center justify-center text-muted-foreground">
              <InboxIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* New */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-blue-400",
            statusFilter === "new" ? "ring-2 ring-blue-500 border-blue-500 bg-blue-50/20" : "border-border"
          )}
          onClick={() => {
            setStatusFilter("new");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-blue-600 dark:text-blue-400">Chờ xử lý (Mới)</p>
              <p className="text-2xl font-bold text-blue-700 dark:text-blue-300 mt-0.5">{newCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-blue-100 dark:bg-blue-950/60 flex items-center justify-center text-blue-600 dark:text-blue-400">
              <ClockIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* Contacted */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-amber-400",
            statusFilter === "contacted" ? "ring-2 ring-amber-500 border-amber-500 bg-amber-50/20" : "border-border"
          )}
          onClick={() => {
            setStatusFilter("contacted");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-amber-600 dark:text-amber-400">Đang trao đổi</p>
              <p className="text-2xl font-bold text-amber-700 dark:text-amber-300 mt-0.5">{contactedCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-amber-100 dark:bg-amber-950/60 flex items-center justify-center text-amber-600 dark:text-amber-400">
              <PhoneIcon className="size-4" />
            </div>
          </CardContent>
        </Card>

        {/* Closed */}
        <Card
          size="sm"
          className={cn(
            "cursor-pointer transition-all hover:border-emerald-400",
            statusFilter === "closed" ? "ring-2 ring-emerald-500 border-emerald-500 bg-emerald-50/20" : "border-border"
          )}
          onClick={() => {
            setStatusFilter("closed");
            setPage(1);
          }}
        >
          <CardContent className="pt-4 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-emerald-600 dark:text-emerald-400">Đã hoàn tất</p>
              <p className="text-2xl font-bold text-emerald-700 dark:text-emerald-300 mt-0.5">{closedCount}</p>
            </div>
            <div className="size-9 rounded-lg bg-emerald-100 dark:bg-emerald-950/60 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
              <CheckCheckIcon className="size-4" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3">
        {/* Status Pills */}
        <div className="flex items-center gap-1.5 overflow-x-auto pb-1 sm:pb-0 [scrollbar-width:none]">
          <Button
            variant={statusFilter === "all" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setStatusFilter("all");
              setPage(1);
            }}
            className="rounded-full text-xs h-8"
          >
            Tất cả ({totalCount})
          </Button>
          <Button
            variant={statusFilter === "new" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setStatusFilter("new");
              setPage(1);
            }}
            className={cn(
              "rounded-full text-xs h-8",
              statusFilter === "new" && "bg-blue-600 hover:bg-blue-700 text-white"
            )}
          >
            Chờ xử lý ({newCount})
          </Button>
          <Button
            variant={statusFilter === "contacted" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setStatusFilter("contacted");
              setPage(1);
            }}
            className={cn(
              "rounded-full text-xs h-8",
              statusFilter === "contacted" && "bg-amber-600 hover:bg-amber-700 text-white"
            )}
          >
            Đang trao đổi ({contactedCount})
          </Button>
          <Button
            variant={statusFilter === "closed" ? "default" : "outline"}
            size="sm"
            onClick={() => {
              setStatusFilter("closed");
              setPage(1);
            }}
            className={cn(
              "rounded-full text-xs h-8",
              statusFilter === "closed" && "bg-emerald-600 hover:bg-emerald-700 text-white"
            )}
          >
            Đã hoàn tất ({closedCount})
          </Button>
        </div>

        {/* Search Input */}
        <form onSubmit={handleSearchSubmit} className="relative sm:w-72">
          <SearchIcon className="absolute left-2.5 top-2.5 size-3.5 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo SĐT, tên, lời nhắn..."
            className="pl-8 text-xs h-8"
          />
        </form>
      </div>

      {/* Main Content Area */}
      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-16 gap-3">
          <Spinner className="size-8" />
          <p className="text-sm text-muted-foreground">Đang tải danh sách yêu cầu liên hệ...</p>
        </div>
      ) : error ? (
        <div className="rounded-xl border border-destructive/20 bg-destructive/5 p-8 text-center">
          <p className="text-sm font-medium text-destructive">{error}</p>
          <Button onClick={fetchLeads} variant="outline" size="sm" className="mt-4 gap-2">
            Thử lại
          </Button>
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-border p-12 text-center bg-card">
          <div className="mx-auto size-12 rounded-full bg-muted flex items-center justify-center text-muted-foreground mb-3">
            <InboxIcon className="size-6" />
          </div>
          <h3 className="text-base font-semibold text-foreground">Không có yêu cầu liên hệ nào</h3>
          <p className="text-xs text-muted-foreground max-w-md mx-auto mt-1">
            {statusFilter !== "all" || search
              ? "Không tìm thấy yêu cầu liên hệ nào khớp với bộ lọc hiện tại. Thử xóa tìm kiếm hoặc chuyển sang bộ lọc khác."
              : "Trung tâm của bạn hiện chưa nhận được yêu cầu liên hệ mới nào từ học sinh hoặc phụ huynh."}
          </p>
          {(statusFilter !== "all" || search) && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setStatusFilter("all");
                setSearch("");
                setPage(1);
              }}
              className="mt-4 text-xs"
            >
              Đặt lại bộ lọc
            </Button>
          )}
        </div>
      ) : (
        <div className="rounded-xl border border-border overflow-hidden bg-card ">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b bg-muted/40 text-muted-foreground">
                  <th className="py-3 px-3 font-semibold whitespace-nowrap">Thời gian</th>
                  <th className="py-3 px-3 font-semibold">Người gửi</th>
                  <th className="py-3 px-3 font-semibold whitespace-nowrap">Số điện thoại</th>
                  <th className="py-3 px-3 font-semibold whitespace-nowrap">Bậc học</th>
                  <th className="py-3 px-3 font-semibold max-w-[180px]">Lời nhắn</th>
                  <th className="py-3 px-3 font-semibold whitespace-nowrap">Hồ sơ</th>
                  <th className="py-3 px-3 font-semibold whitespace-nowrap">Trạng thái</th>
                  <th className="py-3 px-3 font-semibold text-right whitespace-nowrap">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {items.map((lead) => {
                  const isNew = lead.status === "new";
                  const isContacted = lead.status === "contacted";
                  const isClosed = lead.status === "closed";

                  return (
                    <tr
                      key={lead.id}
                      className={cn(
                        "hover:bg-muted/30 transition-colors cursor-pointer",
                        isNew && "bg-blue-50/15 dark:bg-blue-950/10"
                      )}
                      onClick={() => setSelectedLead(lead)}
                    >
                      {/* Date */}
                      <td className="py-3.5 px-3 whitespace-nowrap text-muted-foreground">
                        <div>{new Date(lead.createdAt).toLocaleDateString("vi-VN")}</div>
                        <div className="text-xs text-muted-foreground/70">
                          {new Date(lead.createdAt).toLocaleTimeString("vi-VN", {
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                        </div>
                      </td>

                      {/* Student info */}
                      <td className="py-3.5 px-3 max-w-[185px]">
                        {lead.sharedProfile && lead.studentName ? (
                          <div className="min-w-0">
                            <div className="font-semibold text-foreground truncate" title={lead.studentName}>
                              {lead.studentName}
                            </div>
                            {lead.studentEmail && (
                              <div className="text-xs text-muted-foreground truncate" title={lead.studentEmail}>
                                {lead.studentEmail}
                              </div>
                            )}
                          </div>
                        ) : (
                          <span className="text-muted-foreground italic flex items-center gap-1 whitespace-nowrap">
                            <LockIcon className="size-3 shrink-0" />
                            Học sinh ẩn danh
                          </span>
                        )}
                      </td>

                      {/* Phone */}
                      <td className="py-3.5 px-3 whitespace-nowrap">
                        {lead.contactPhone ? (
                          <div className="flex items-center gap-1.5" onClick={(e) => e.stopPropagation()}>
                            <a
                              href={`tel:${lead.contactPhone}`}
                              className="font-mono text-brand font-medium hover:underline inline-flex items-center gap-1"
                            >
                              <PhoneIcon className="size-3 shrink-0" />
                              {lead.contactPhone}
                            </a>
                            <Button
                              variant="ghost"
                              size="icon-xs"
                              onClick={() => copyToClipboard(lead.contactPhone!, "Số điện thoại")}
                              title="Sao chép số điện thoại"
                              className="size-6 text-muted-foreground hover:text-foreground"
                            >
                              <CopyIcon className="size-3" />
                            </Button>
                          </div>
                        ) : (
                          <span className="text-muted-foreground">Chưa có</span>
                        )}
                      </td>

                      {/* Study level */}
                      <td className="py-3.5 px-3 whitespace-nowrap">
                        <Badge variant="secondary" className="font-normal text-xs">
                          {formatStudyLevel(lead.studyLevel)}
                        </Badge>
                      </td>

                      {/* Message preview */}
                      <td className="py-3.5 px-3 max-w-[180px]">
                        <p className="truncate text-foreground" title={lead.message ?? ""}>
                          {lead.message || <span className="text-muted-foreground italic">Không có lời nhắn</span>}
                        </p>
                      </td>

                      {/* Shared Profile Badge */}
                      <td className="py-3.5 px-3 whitespace-nowrap">
                        {lead.sharedProfile ? (
                          <Badge className="bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border-emerald-300 gap-1 text-xs">
                            <ShieldCheckIcon className="size-3" />
                            Đã chia sẻ
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="text-muted-foreground gap-1 text-xs">
                            <LockIcon className="size-3" />
                            Chỉ liên hệ
                          </Badge>
                        )}
                      </td>

                      {/* Status */}
                      <td className="py-3.5 px-3 whitespace-nowrap">
                        {isNew && (
                          <Badge className="bg-blue-600 text-white font-medium text-xs px-2 py-0.5">
                            Mới nhận
                          </Badge>
                        )}
                        {isContacted && (
                          <Badge className="bg-amber-600 text-white font-medium text-xs px-2 py-0.5">
                            Đang trao đổi
                          </Badge>
                        )}
                        {isClosed && (
                          <Badge variant="outline" className="text-emerald-700 border-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 font-medium text-xs px-2 py-0.5">
                            Đã hoàn tất
                          </Badge>
                        )}
                      </td>

                      {/* Action */}
                      <td className="py-3.5 px-3 text-right whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setSelectedLead(lead)}
                          className="h-7 text-xs px-2.5 gap-1.5"
                        >
                          <MessageSquareIcon className="size-3" />
                          Chi tiết & Trả lời
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

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
                  {selectedLead.status === "new" && (
                    <Badge className="bg-blue-600 text-white">Mới nhận</Badge>
                  )}
                  {selectedLead.status === "contacted" && (
                    <Badge className="bg-amber-600 text-white">Đang trao đổi</Badge>
                  )}
                  {selectedLead.status === "closed" && (
                    <Badge className="bg-emerald-600 text-white">Đã hoàn tất</Badge>
                  )}
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
                      <Badge className="bg-emerald-100 text-emerald-800 border-emerald-300 text-xs gap-1">
                        <ShieldCheckIcon className="size-3" />
                        Đã cấp quyền chia sẻ (Nghị định 356)
                      </Badge>
                    ) : (
                      <Badge variant="outline" className="text-xs text-muted-foreground gap-1">
                        <LockIcon className="size-3" />
                        Chưa cấp quyền chia sẻ
                      </Badge>
                    )}
                  </div>

                  {selectedLead.sharedProfile && selectedLead.profileSummary ? (
                    <Card size="sm" className="border-emerald-200 bg-emerald-50/20 dark:bg-emerald-950/20">
                      <CardContent className="pt-4 grid grid-cols-2 sm:grid-cols-3 gap-3 text-xs">
                        {/* GPA */}
                        <div className="p-2.5 rounded-lg bg-card border min-w-0">
                          <span className="text-muted-foreground block">GPA tích lũy:</span>
                          <span className="text-base font-bold text-emerald-600">
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
                        <ClockIcon className="size-3.5 text-blue-600" />
                        Chuyển về Mới nhận
                      </Button>
                    )}

                    {selectedLead.status !== "contacted" && (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={isUpdatingStatus}
                        onClick={() => handleStatusChange(selectedLead.id, "contacted")}
                        className="text-xs gap-1.5 text-amber-700 border-amber-300 bg-amber-50 hover:bg-amber-100"
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
                        className="text-xs gap-1.5 text-emerald-700 border-emerald-300 bg-emerald-50 hover:bg-emerald-100"
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
