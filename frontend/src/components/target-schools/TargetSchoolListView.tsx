"use client";

import React, { useState, useEffect, useMemo } from "react";
import Link from "next/link";
import {
  BookmarkIcon,
  CalendarIcon,
  ClockIcon,
  ExternalLinkIcon,
  GraduationCapIcon,
  ListIcon,
  Loader2,
  MapPinIcon,
  PlusIcon,
  SearchIcon,
  Sparkles,
  Trash2Icon,
  TrendingUpIcon,
  CheckCircle2Icon,
  ShieldCheckIcon,
} from "lucide-react";
import { targetSchoolsApi } from "@/lib/api";
import type { TargetSchoolDto, TargetSchoolSummaryDto } from "@/types/api";
import { Button } from "@/components/ui/button";
import { ErrorFallback } from "@/components/ui/error-fallback";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { groupLabel, groupVariant } from "@/lib/recommendation-labels";
import { cn } from "@/lib/utils";

type ViewMode = "grid" | "timeline";
type CategoryFilter = "all" | "reach" | "match" | "safety";

export function TargetSchoolListView() {
  const [data, setData] = useState<TargetSchoolSummaryDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<CategoryFilter>("all");
  const [viewMode, setViewMode] = useState<ViewMode>("grid");

  // State cho dialog xác nhận xóa
  const [deletingSchool, setDeletingSchool] = useState<TargetSchoolDto | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const fetchData = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const summary = await targetSchoolsApi.list();
      setData(summary);
    } catch {
      setError("Không thể tải danh sách trường mục tiêu. Vui lòng thử lại sau.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchData();
  }, [fetchData]);

  // Xóa trường mục tiêu [UC-31]
  const handleDeleteConfirm = async () => {
    if (!deletingSchool) return;
    setIsDeleting(true);
    try {
      await targetSchoolsApi.remove(deletingSchool.id);
      toast.add({
        type: "info",
        title: "Đã xóa khỏi mục tiêu",
        description: `Đã xóa ${deletingSchool.universityName} khỏi danh sách trường mục tiêu.`,
      });
      setDeletingSchool(null);
      await fetchData();
    } catch {
      toast.add({
        type: "error",
        title: "Lỗi",
        description: "Không thể xóa trường mục tiêu. Vui lòng thử lại.",
      });
    } finally {
      setIsDeleting(false);
    }
  };

  const items = data?.items;

  // Lọc và tìm kiếm danh sách trường
  const filteredItems = useMemo(() => {
    if (!items) return [];
    const byDeadline = [...items].sort((a, b) => {
      if (!a.deadline && !b.deadline) return 0;
      if (!a.deadline) return 1;
      if (!b.deadline) return -1;
      return new Date(a.deadline).getTime() - new Date(b.deadline).getTime();
    });
    return (viewMode === "timeline" ? byDeadline : items).filter((item) => {
      // Lọc theo Category
      if (selectedCategory !== "all") {
        if ((item.category || "").toLowerCase() !== selectedCategory) {
          return false;
        }
      }
      // Lọc theo search
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        const matchName = item.universityName.toLowerCase().includes(q);
        const matchCode = item.universityCode.toLowerCase().includes(q);
        const matchCity = item.city?.toLowerCase().includes(q) ?? false;
        const matchState = item.state?.toLowerCase().includes(q) ?? false;
        const matchRound = item.applicationRound?.toLowerCase().includes(q) ?? false;
        return matchName || matchCode || matchCity || matchState || matchRound;
      }
      return true;
    });
  }, [items, selectedCategory, search]);

  // Tính số ngày còn lại đến hạn nộp
  const getDaysRemaining = (deadlineStr?: string | null) => {
    if (!deadlineStr) return null;
    const deadline = new Date(deadlineStr);
    const now = new Date();
    const diffTime = deadline.getTime() - now.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return diffDays;
  };

  const getCategoryConfig = (cat?: string | null) => {
    switch (cat?.toLowerCase()) {
      case "reach":
        return {
          label: groupLabel("R"),
          badgeVariant: groupVariant("R"),
          borderClass: "",
          desc: "Trường có tính cạnh tranh cao so với hồ sơ",
        };
      case "match":
        return {
          label: groupLabel("M"),
          badgeVariant: groupVariant("M"),
          borderClass: "",
          desc: "Điểm và hồ sơ nằm trong khoảng tuyển sinh",
        };
      case "safety":
        return {
          label: groupLabel("S"),
          badgeVariant: groupVariant("S"),
          borderClass: "",
          desc: "Hồ sơ vượt trội so với yêu cầu đầu vào",
        };
      default:
        return {
          label: "Chưa phân loại",
          badgeVariant: "neutral" as const,
          borderClass: "",
          desc: "",
        };
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-[400px] flex-col items-center justify-center gap-3">
        <Loader2 className="size-8 animate-spin text-brand" />
        <p className="text-sm text-muted-foreground">Đang tải danh sách trường mục tiêu của bạn...</p>
      </div>
    );
  }

  if (error) {
    return (
      <ErrorFallback
        description={error}
        action={{
          label: "Thử lại",
          onClick: fetchData,
        }}
      />
    );
  }

  const total = data?.totalCount ?? 0;
  const reach = data?.reachCount ?? 0;
  const match = data?.matchCount ?? 0;
  const safety = data?.safetyCount ?? 0;
  const nextDeadlineDays = getDaysRemaining(data?.nextDeadline);

  return (
    <div className="space-y-6">
      {/* Tiêu đề & Giới thiệu */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
              Danh sách trường mục tiêu
            </h1>
            <Badge variant="brand" className="font-semibold">
              {total} trường
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Kế hoạch nộp hồ sơ, phân bổ mục tiêu và theo dõi dòng thời gian hạn nộp của bạn.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link href="/recommendations">
            <Button size="sm" className="gap-1.5 ">
              <PlusIcon className="size-4" />
              Thêm trường mới
            </Button>
          </Link>
        </div>
      </div>

      {/* Thẻ Thống Kê Tổng Quan (Summary Cards) */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:gap-4">
        <Card
          className={cn(
            "cursor-pointer transition-all",
            selectedCategory === "all" ? "ring-2 ring-ink-2" : ""
          )}
          onClick={() => setSelectedCategory("all")}
        >
          <CardContent className="p-4 sm:p-5">
            <div className="flex items-center justify-between text-muted-foreground">
              <span className="text-xs font-semibold">Tổng mục tiêu</span>
              <GraduationCapIcon className="size-4 text-brand" />
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-numeric text-2xl font-bold text-foreground sm:text-3xl">
                {total}
              </span>
              <span className="text-xs text-muted-foreground">trường</span>
            </div>
            <p className="mt-1 text-[13px] text-muted-foreground">Toàn bộ kế hoạch lưu</p>
          </CardContent>
        </Card>

        <Card
          className={cn(
            "cursor-pointer transition-all",
            selectedCategory === "reach" ? "ring-2 ring-reach" : ""
          )}
          onClick={() => setSelectedCategory(selectedCategory === "reach" ? "all" : "reach")}
        >
          <CardContent className="p-4 sm:p-5">
            <div className="flex items-center justify-between text-muted-foreground">
              <span className="text-xs font-semibold text-reach">
                Khó vào
              </span>
              <TrendingUpIcon className="size-4 text-reach" />
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-numeric text-2xl font-bold text-reach sm:text-3xl">
                {reach}
              </span>
              <span className="text-xs text-muted-foreground">
                {total > 0 ? `(${Math.round((reach / total) * 100)}%)` : ""}
              </span>
            </div>
            <p className="mt-1 text-[13px] text-muted-foreground">Thử thách, khó vào</p>
          </CardContent>
        </Card>

        <Card
          className={cn(
            "cursor-pointer transition-all",
            selectedCategory === "match" ? "ring-2 ring-brand" : ""
          )}
          onClick={() => setSelectedCategory(selectedCategory === "match" ? "all" : "match")}
        >
          <CardContent className="p-4 sm:p-5">
            <div className="flex items-center justify-between text-muted-foreground">
              <span className="text-xs font-semibold text-brand">
                Vừa sức
              </span>
              <CheckCircle2Icon className="size-4 text-brand" />
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-numeric text-2xl font-bold text-brand sm:text-3xl">
                {match}
              </span>
              <span className="text-xs text-muted-foreground">
                {total > 0 ? `(${Math.round((match / total) * 100)}%)` : ""}
              </span>
            </div>
            <p className="mt-1 text-[13px] text-muted-foreground">Cạnh tranh ngang sức</p>
          </CardContent>
        </Card>

        <Card
          className={cn(
            "cursor-pointer transition-all",
            selectedCategory === "safety" ? "ring-2 ring-ok" : ""
          )}
          onClick={() => setSelectedCategory(selectedCategory === "safety" ? "all" : "safety")}
        >
          <CardContent className="p-4 sm:p-5">
            <div className="flex items-center justify-between text-muted-foreground">
              <span className="text-xs font-semibold text-ok">
                An toàn hơn
              </span>
              <ShieldCheckIcon className="size-4 text-ok" />
            </div>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-numeric text-2xl font-bold text-ok sm:text-3xl">
                {safety}
              </span>
              <span className="text-xs text-muted-foreground">
                {total > 0 ? `(${Math.round((safety / total) * 100)}%)` : ""}
              </span>
            </div>
            <p className="mt-1 text-[13px] text-muted-foreground">Đảm bảo cơ hội đỗ</p>
          </CardContent>
        </Card>
      </div>

      {/* Banner Hạn Chót Gần Nhất (Next Deadline Banner) */}
      {data?.nextDeadline && (
        <div className="flex flex-col gap-2 rounded-xl border border-warn/25 bg-warn-wash p-4 text-ink sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-surface text-warn">
              <ClockIcon className="size-5" />
            </div>
            <div>
              <p className="text-xs font-medium text-warn">
                Hạn chót nộp hồ sơ gần nhất
              </p>
              <p className="font-semibold text-sm">
                {new Date(data.nextDeadline).toLocaleDateString("vi-VN", {
                  weekday: "long",
                  day: "2-digit",
                  month: "2-digit",
                  year: "numeric",
                })}
              </p>
            </div>
          </div>
          {nextDeadlineDays !== null && (
            <Badge
              variant="outline"
              className={cn(
                "w-fit font-semibold",
                nextDeadlineDays <= 14 ? "border-risk/25 bg-risk-wash text-risk" : "border-warn/25 bg-surface text-warn"
              )}
            >
              {nextDeadlineDays > 0
                ? `Còn ${nextDeadlineDays} ngày nữa`
                : nextDeadlineDays === 0
                  ? "Hôm nay là hạn chót!"
                  : "Đã quá hạn"}
            </Badge>
          )}
        </div>
      )}

      {/* Thanh Điều Khiển: Tìm kiếm, Lọc phân nhóm & Chuyển chế độ xem */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative flex-1 sm:max-w-xs">
          <SearchIcon className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Tìm trường mục tiêu theo tên, mã hoặc bang"
            placeholder="Tìm theo tên trường, mã, bang..."
            className="pl-9"
          />
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Lọc Category Pills */}
          <div className="inline-flex rounded-lg border border-line bg-muted/40 p-0.5">
            <button
              type="button"
              onClick={() => setSelectedCategory("all")}
              className={cn(
                "rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                selectedCategory === "all"
                  ? "bg-surface font-semibold text-foreground "
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              Tất cả ({total})
            </button>
            <button
              type="button"
              onClick={() => setSelectedCategory("reach")}
              className={cn(
                "rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                selectedCategory === "reach"
                  ? "bg-surface font-semibold text-reach"
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              Khó vào ({reach})
            </button>
            <button
              type="button"
              onClick={() => setSelectedCategory("match")}
              className={cn(
                "rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                selectedCategory === "match"
                  ? "bg-surface font-semibold text-brand"
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              Vừa sức ({match})
            </button>
            <button
              type="button"
              onClick={() => setSelectedCategory("safety")}
              className={cn(
                "rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                selectedCategory === "safety"
                  ? "bg-surface font-semibold text-ok"
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              An toàn hơn ({safety})
            </button>
          </div>

          {/* Nút Đổi Chế Độ Xem: Danh sách vs Dòng thời gian */}
          <div className="inline-flex rounded-lg border border-line bg-muted/40 p-0.5">
            <button
              type="button"
              onClick={() => setViewMode("grid")}
              className={cn(
                "inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                viewMode === "grid"
                  ? "bg-surface font-semibold text-brand "
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              <ListIcon className="size-3.5" />
              <span>Danh sách</span>
            </button>
            <button
              type="button"
              onClick={() => setViewMode("timeline")}
              className={cn(
                "inline-flex items-center gap-1 rounded-md px-2.5 py-1 text-xs font-medium transition-colors",
                viewMode === "timeline"
                  ? "bg-surface font-semibold text-brand "
                  : "text-muted-foreground hover:text-foreground"
              )}
            >
              <CalendarIcon className="size-3.5" />
              <span>Dòng thời gian</span>
            </button>
          </div>
        </div>
      </div>

      {/* Nội dung danh sách / Dòng thời gian */}
      {filteredItems.length === 0 ? (
        <Card className="border-dashed py-12 text-center">
          <CardContent className="space-y-3">
            <div className="mx-auto flex size-12 items-center justify-center rounded-full bg-muted">
              <BookmarkIcon className="size-6 text-muted-foreground" />
            </div>
            <h3 className="font-semibold text-base text-foreground">
              {total === 0
                ? "Chưa có trường nào trong danh sách mục tiêu"
                : "Không tìm thấy trường nào phù hợp với bộ lọc"}
            </h3>
            <p className="mx-auto max-w-sm text-sm text-muted-foreground">
              {total === 0
                ? "Bắt đầu khám phá cơ sở dữ liệu các trường đại học tại Mỹ hoặc sử dụng trợ lý phân tích để lưu trường vào danh sách mục tiêu của bạn."
                : "Thử thay đổi từ khóa tìm kiếm hoặc bấm chọn lại phân loại mục tiêu."}
            </p>
            {total === 0 ? (
              <div className="flex justify-center gap-3 pt-2">
                <Link href="/recommendations">
                  <Button size="sm" className="gap-1.5">
                    <Sparkles className="size-4" />
                    Khám phá gợi ý trường
                  </Button>
                </Link>
                <Link href="/analysis">
                  <Button variant="outline" size="sm">
                    Phân tích hồ sơ
                  </Button>
                </Link>
              </div>
            ) : (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setSearch("");
                  setSelectedCategory("all");
                }}
              >
                Đặt lại bộ lọc
              </Button>
            )}
          </CardContent>
        </Card>
      ) : viewMode === "grid" ? (
        /* CHẾ ĐỘ XEM 1: DANH SÁCH THẺ (GRID VIEW) */
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {filteredItems.map((item) => {
            const catConfig = getCategoryConfig(item.category);
            const daysRemaining = getDaysRemaining(item.deadline);

            return (
              <Card
                key={item.id}
                className={cn(
                  "flex flex-col justify-between transition-colors",
                  catConfig.borderClass
                )}
              >
                <CardHeader className="p-4 pb-2">
                  <div className="flex items-start justify-between gap-2">
                    <Badge variant={catConfig.badgeVariant} className="text-xs font-semibold">
                      {catConfig.label}
                    </Badge>
                    <span className="font-mono text-xs text-muted-foreground">Mã {item.universityCode}</span>
                  </div>
                  <CardTitle className="pt-2 text-base font-bold leading-snug">
                    <Link
                      href={`/university/${item.universityCode}`}
                      className="hover:text-brand hover:underline"
                    >
                      {item.universityName}
                    </Link>
                  </CardTitle>
                  {(item.city || item.state) && (
                    <p className="flex items-center gap-1 text-xs text-muted-foreground">
                      <MapPinIcon className="size-3 shrink-0" />
                      {[item.city, item.state].filter(Boolean).join(", ")}
                    </p>
                  )}
                </CardHeader>

                <CardContent className="space-y-3 p-4 pt-2">
                  {/* Thông tin vòng nộp và hạn nộp */}
                  <div className="rounded-lg border border-line bg-muted/30 p-2.5 text-xs space-y-1.5">
                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground">Vòng nộp:</span>
                      <span className="font-semibold text-foreground">
                        {item.applicationRound || "Chưa xác định"}
                      </span>
                    </div>

                    <div className="flex items-center justify-between">
                      <span className="text-muted-foreground inline-flex items-center gap-1">
                        <CalendarIcon className="size-3" /> Hạn chót:
                      </span>
                      {item.deadline ? (
                        <span className="font-medium text-foreground">
                          {new Date(item.deadline).toLocaleDateString("vi-VN", {
                            day: "2-digit",
                            month: "2-digit",
                            year: "numeric",
                          })}
                        </span>
                      ) : (
                        <span className="text-muted-foreground italic">Chưa có hạn</span>
                      )}
                    </div>

                    {daysRemaining !== null && (
                      <div className="flex justify-end pt-0.5">
                        <span
                          className={cn(
                            "rounded px-1.5 py-0.5 text-xs font-semibold",
                            daysRemaining <= 14
                              ? "bg-risk-wash text-risk"
                              : daysRemaining <= 45
                                ? "bg-warn-wash text-warn"
                                : "bg-ok-wash text-ok"
                          )}
                        >
                          {daysRemaining > 0
                            ? `Còn ${daysRemaining} ngày`
                            : daysRemaining === 0
                              ? "Hạn chót hôm nay"
                              : "Đã qua hạn"}
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Tỉ lệ trúng tuyển & Loại trường */}
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>
                      {item.control === "public"
                        ? "Công lập"
                        : item.control === "private"
                          ? "Tư thục"
                          : ""}
                    </span>
                    {item.acceptanceRate !== null && item.acceptanceRate !== undefined && (
                      <span className="font-medium">
                        Tỉ lệ đỗ: {Math.round(item.acceptanceRate * 100)}%
                      </span>
                    )}
                  </div>

                  {/* Ghi chú cá nhân */}
                  {item.notes && (
                    <div className="border-t border-line/60 pt-2 text-xs text-muted-foreground">
                      <span className="font-semibold text-foreground/80">Ghi chú:</span>{" "}
                      <span className="italic">{item.notes}</span>
                    </div>
                  )}

                  {/* Các nút hành động */}
                  <div className="flex items-center justify-between border-t border-line/60 pt-3">
                    <Link href={`/university/${item.universityCode}`}>
                      <Button variant="ghost" size="sm" className="h-8 gap-1 px-2 text-xs text-brand">
                        <span>Chi tiết trường</span>
                        <ExternalLinkIcon className="size-3" />
                      </Button>
                    </Link>

                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setDeletingSchool(item)}
                      className="h-8 gap-1 px-2 text-xs text-destructive hover:bg-destructive/10 hover:text-destructive"
                    >
                      <Trash2Icon className="size-3.5" />
                      <span>Xóa</span>
                    </Button>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      ) : (
        /* CHẾ ĐỘ XEM 2: DÒNG THỜI GIAN HẠN NỘP (TIMELINE VIEW - THEO YÊU CẦU JIRA UC-33) */
        <Card>
          <CardHeader className="border-b border-line pb-3">
            <CardTitle className="flex items-center gap-2 text-base font-bold">
              <CalendarIcon className="size-4 text-brand" />
              <span>Dòng thời gian hạn nộp hồ sơ</span>
            </CardTitle>
            <p className="text-xs text-muted-foreground">
              Sắp xếp theo thứ tự thời gian hạn nộp từ gần đến xa.
            </p>
          </CardHeader>
          <CardContent className="p-4 sm:p-6">
            <div className="relative border-l-2 border-brand/20 pl-6 space-y-6 sm:space-y-8">
              {filteredItems.map((item) => {
                const catConfig = getCategoryConfig(item.category);
                const daysRemaining = getDaysRemaining(item.deadline);

                return (
                  <div key={item.id} className="relative group">
                    {/* Node tròn trên Timeline */}
                    <div
                      className={cn(
                        "absolute -left-[31px] top-1 flex size-4 items-center justify-center rounded-full border-2 bg-surface",
                        daysRemaining !== null && daysRemaining <= 14
                          ? "border-risk bg-risk"
                          : "border-brand bg-brand"
                      )}
                    />

                    <div className="rounded-xl border border-line bg-surface p-4 transition-all ">
                      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <Link
                              href={`/university/${item.universityCode}`}
                              className="font-bold text-base text-foreground hover:text-brand hover:underline"
                            >
                              {item.universityName}
                            </Link>
                            <Badge variant={catConfig.badgeVariant} className="text-xs">
                              {catConfig.label}
                            </Badge>
                          </div>
                          <p className="mt-0.5 text-xs text-muted-foreground">
                            {[item.city, item.state].filter(Boolean).join(", ")} · Mã {item.universityCode}
                          </p>
                        </div>

                        {/* Hạn nộp & Vòng nộp */}
                        <div className="flex items-center gap-3">
                          <div className="text-right sm:text-right">
                            <span className="block text-xs font-semibold text-foreground">
                              {item.applicationRound || "Vòng chưa xác định"}
                            </span>
                            <span className="text-xs text-muted-foreground">
                              {item.deadline
                                ? new Date(item.deadline).toLocaleDateString("vi-VN", {
                                  day: "2-digit",
                                  month: "2-digit",
                                  year: "numeric",
                                })
                                : "Chưa có hạn nộp"}
                            </span>
                          </div>

                          {daysRemaining !== null && (
                            <Badge
                              variant="outline"
                              className={cn(
                                "font-semibold text-xs",
                                daysRemaining <= 14
                                  ? "border-risk/25 bg-risk-wash text-risk"
                                  : "border-ok/25 bg-ok-wash text-ok"
                              )}
                            >
                              {daysRemaining > 0
                                ? `Còn ${daysRemaining} ngày`
                                : daysRemaining === 0
                                  ? "Hôm nay!"
                                  : "Đã qua"}
                            </Badge>
                          )}
                        </div>
                      </div>

                      {/* Ghi chú & Actions */}
                      {item.notes && (
                        <p className="mt-2 border-t border-line/50 pt-2 text-xs text-muted-foreground italic">
                          &ldquo;{item.notes}&rdquo;
                        </p>
                      )}

                      <div className="mt-3 flex items-center justify-end gap-2">
                        <Link href={`/university/${item.universityCode}`}>
                          <Button variant="outline" size="sm" className="h-7 text-xs">
                            Xem trường
                          </Button>
                        </Link>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setDeletingSchool(item)}
                          className="h-7 text-xs text-destructive hover:bg-destructive/10 hover:text-destructive"
                        >
                          Xóa
                        </Button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Dialog Xác Nhận Xóa [UC-31] */}
      <Dialog open={Boolean(deletingSchool)} onOpenChange={(open) => !open && setDeletingSchool(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2 text-destructive text-lg">
              <Trash2Icon className="size-5" />
              Xóa khỏi danh sách mục tiêu
            </DialogTitle>
            <DialogDescription>
              Bạn có chắc chắn muốn xóa{" "}
              <span className="font-semibold text-foreground">{deletingSchool?.universityName}</span> khỏi
              danh sách trường mục tiêu không?
            </DialogDescription>
          </DialogHeader>

          <p className="text-xs text-muted-foreground">
            Hành động này sẽ gỡ trường khỏi kế hoạch theo dõi và dòng thời gian nộp đơn của bạn. Bạn vẫn có thể
            lưu lại trường này bất kỳ lúc nào.
          </p>

          <DialogFooter className="gap-2 sm:gap-0">
            <Button
              type="button"
              variant="ghost"
              onClick={() => setDeletingSchool(null)}
              disabled={isDeleting}
            >
              Hủy
            </Button>
            <Button
              type="button"
              variant="destructive"
              onClick={handleDeleteConfirm}
              disabled={isDeleting}
              className="gap-2"
            >
              {isDeleting && <Loader2 className="size-4 animate-spin" />}
              Xác nhận xóa
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
