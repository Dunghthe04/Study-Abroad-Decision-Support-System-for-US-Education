"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Building2Icon,
  SearchIcon,
  MapPinIcon,
  CheckCircle2Icon,
  StarIcon,
  ShieldCheckIcon,
  ShieldAlertIcon,
  RotateCcwIcon,
  FlameIcon,
  ChevronRightIcon,
  EyeIcon,
  EyeOffIcon,
  PencilIcon,
} from "lucide-react";
import { studyCentersApi, ApiError } from "@/lib/api";
import type { StudyCenter, PagedResult } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
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
import { toast } from "@/components/ui/toast";
import { AddCenterButton } from "@/components/admin/AddCenterButton";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { EditCenterButton } from "@/components/admin/centers/EditCenterButton";
import { cn } from "cn";

export function AdminCenterListView() {
  const router = useRouter();
  const [centers, setCenters] = useState<StudyCenter[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [search, setSearch] = useState("");
  const [cityFilter, setCityFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [isLoading, setIsLoading] = useState(true);
  const [confirmTarget, setConfirmTarget] = useState<{
    center: StudyCenter;
    action: "verify" | "unverify" | "hide" | "show";
  } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchCenters = useCallback(async (activePage: number, signal?: { cancelled: boolean }) => {
    setIsLoading(true);
    try {
      const res: PagedResult<StudyCenter> = await studyCentersApi.search({
        search: search.trim() || undefined,
        city: cityFilter !== "all" ? cityFilter : undefined,
        page: activePage,
        pageSize,
      });

      if (signal?.cancelled) return;

      let items = res.items || [];

      // Lọc client-side cho typeFilter và statusFilter nếu cần
      if (typeFilter !== "all") {
        items = items.filter((c) => (c.centerType || "study_abroad") === typeFilter);
      }
      if (statusFilter !== "all") {
        items = items.filter((c) => (c.verificationStatus || "unverified") === statusFilter);
      }

      setCenters(items);
      setTotalCount(res.totalCount || items.length);
    } catch (err) {
      if (!signal?.cancelled) {
        console.error("Lỗi tải danh sách trung tâm:", err);
      }
    } finally {
      if (!signal?.cancelled) {
        setIsLoading(false);
      }
    }
  }, [cityFilter, search, statusFilter, typeFilter]);

  useEffect(() => {
    const signal = { cancelled: false };
    void (async () => {
      await fetchCenters(page, signal);
    })();
    return () => {
      signal.cancelled = true;
    };
  }, [fetchCenters, page]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
  };

  const handleResetFilters = () => {
    setSearch("");
    setCityFilter("all");
    setTypeFilter("all");
    setStatusFilter("all");
    setPage(1);
  };

  const handleConfirmTarget = async () => {
    if (!confirmTarget) return;
    setIsSubmitting(true);
    const targetCenter = confirmTarget.center;
    const action = confirmTarget.action;

    try {
      if (action === "verify" || action === "unverify") {
        const targetVerified = action === "verify";
        const updated = await studyCentersApi.verify(targetCenter.id, targetVerified);
        setCenters((prev) =>
          prev.map((c) =>
            c.id === targetCenter.id
              ? {
                  ...c,
                  ...updated,
                  verificationStatus: targetVerified ? "verified" : "unverified",
                  verifiedAt: targetVerified ? (updated.verifiedAt ?? new Date().toISOString()) : null,
                  verifiedByUserId: targetVerified ? updated.verifiedByUserId : null,
                }
              : c
          )
        );

        toast.add({
          type: "success",
          title: targetVerified ? "Đã xác minh trung tâm" : "Đã hủy xác minh trung tâm",
          description: targetVerified
            ? `Trung tâm ${targetCenter.name} đã được xác minh thành công.`
            : `Đã hủy trạng thái xác minh của ${targetCenter.name}.`,
        });
      } else if (action === "hide" || action === "show") {
        const targetActive = action === "show";
        const updated = await studyCentersApi.setActiveStatus(targetCenter.id, targetActive);
        setCenters((prev) =>
          prev.map((c) =>
            c.id === targetCenter.id
              ? {
                  ...c,
                  ...updated,
                  isActive: targetActive,
                }
              : c
          )
        );

        toast.add({
          type: "success",
          title: targetActive ? "Đã hiện trung tâm" : "Đã ẩn trung tâm",
          description: targetActive
            ? `Trung tâm ${targetCenter.name} đã được hiển thị lại trên danh bạ và luồng gợi ý.`
            : `Trung tâm ${targetCenter.name} đã được ẩn khỏi danh bạ và luồng gợi ý.`,
        });
      }

      setConfirmTarget(null);
    } catch (err: unknown) {
      const msg = err instanceof ApiError ? err.message : "Thao tác không thành công.";
      toast.add({
        type: "error",
        title: "Thao tác thất bại",
        description: msg,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const getTypeBadge = (type?: string) => {
    switch (type) {
      case "hybrid":
        return <Badge variant="secondary" className="bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300 border-purple-200">Toàn diện (Du học & Luyện thi)</Badge>;
      case "english_training":
      case "test_prep":
        return <Badge variant="secondary" className="bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 border-amber-200">Luyện thi Chứng chỉ</Badge>;
      default:
        return <Badge variant="secondary" className="bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300 border-blue-200">Tư vấn Du học</Badge>;
    }
  };

  return (
    <div className="flex-1 min-h-0 w-full overflow-y-auto space-y-6 pr-1 pb-10">
      {/* Header bar */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
            Trung tâm tư vấn
          </h1>
          <p className="text-sm text-muted-foreground">
            Danh sách đơn vị tư vấn du học, hồ sơ pháp lý và kiểm duyệt
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void fetchCenters(page)}
            disabled={isLoading}
            className="gap-1.5"
          >
            <RotateCcwIcon className="size-3.5" />
            Làm mới
          </Button>
          <AddCenterButton size="sm" />
        </div>
      </div>

      {/* Filter toolbar */}
      <div className="bg-card border rounded-xl p-4 space-y-3 ">
        <form onSubmit={handleSearchSubmit} className="flex flex-col md:flex-row gap-3">
          <div className="relative flex-1">
            <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Tìm theo tên trung tâm, mã (CTY_013, Summit...), địa chỉ..."
              className="pl-9"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <select
              value={cityFilter}
              onChange={(e) => {
                setCityFilter(e.target.value);
                setPage(1);
              }}
              className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">Tất cả địa điểm</option>
              <option value="Hà Nội">Hà Nội</option>
              <option value="TP.HCM">TP. Hồ Chí Minh</option>
              <option value="Hải Phòng">Hải Phòng</option>
            </select>

            <select
              value={typeFilter}
              onChange={(e) => {
                setTypeFilter(e.target.value);
                setPage(1);
              }}
              className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">Mọi loại hình</option>
              <option value="study_abroad">Tư vấn Du học</option>
              <option value="english_training">Luyện thi Chứng chỉ</option>
              <option value="hybrid">Toàn diện (Du học & Luyện thi)</option>
            </select>

            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value);
                setPage(1);
              }}
              className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="all">Mọi trạng thái</option>
              <option value="verified">Đã xác minh</option>
              <option value="unverified">Chờ xác minh</option>
              <option value="hidden">Đã ẩn khỏi gợi ý</option>
            </select>

            <Button type="submit" variant="secondary" size="sm" className="h-9 gap-1.5">
              <SearchIcon className="w-3.5 h-3.5" />
              Tìm kiếm
            </Button>

            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={handleResetFilters}
              className="h-9 gap-1 text-muted-foreground hover:text-foreground"
            >
              <RotateCcwIcon className="w-3.5 h-3.5" />
              Đặt lại
            </Button>
          </div>
        </form>
      </div>

      {/* Centers Table */}
      <div className="bg-card border rounded-xl overflow-hidden w-full">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-muted/50 border-b text-xs font-semibold text-muted-foregroundr">
              <tr>
                <th className="py-3 px-4">Trung tâm & Mã</th>
                <th className="py-3 px-4">Mô hình hoạt động</th>
                <th className="py-3 px-4">Địa điểm & Pháp lý</th>
                <th className="py-3 px-4">Tỷ lệ Visa & Đánh giá</th>
                <th className="py-3 px-4">Trạng thái</th>
                <th className="py-3 px-4 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-muted-foreground">
                    Đang tải danh sách trung tâm...
                  </td>
                </tr>
              ) : centers.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-muted-foreground">
                    Không tìm thấy trung tâm nào khớp với bộ lọc tìm kiếm.
                  </td>
                </tr>
              ) : (
                centers.map((c) => {
                  const isVerified = c.verificationStatus === "verified";
                  return (
                    <tr
                      key={c.id}
                      onClick={() => router.push(`/admin/centers/${c.id}`)}
                      className="cursor-pointer transition-colors hover:bg-muted/40"
                    >
                      {/* Name & Logo */}
                      <td className="py-3.5 px-4">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-lg overflow-hidden border bg-muted shrink-0 flex items-center justify-center">
                            {c.logoUrl ? (
                              <img src={c.logoUrl} alt={c.name} className="w-full h-full object-cover" />
                            ) : (
                              <Building2Icon className="w-5 h-5 text-muted-foreground" />
                            )}
                          </div>
                          <div className="min-w-0">
                            <div className="font-semibold text-foreground truncate max-w-[240px]">
                              {c.name}
                            </div>
                            <div className="flex items-center gap-2 mt-0.5">
                              <Badge variant="outline" className="text-xs font-mono px-1.5 py-0">
                                {c.code}
                              </Badge>
                            </div>
                          </div>
                        </div>
                      </td>

                      {/* Center Type */}
                      <td className="py-3.5 px-4">
                        {getTypeBadge(c.centerType)}
                      </td>

                      {/* Location & License */}
                      <td className="py-3.5 px-4 text-xs space-y-1">
                        <div className="flex items-center gap-1 font-medium text-foreground">
                          <MapPinIcon className="w-3.5 h-3.5 text-brand shrink-0" />
                          <span className="truncate max-w-[200px]">
                            {c.district ? `${c.district}, ${c.city ?? ""}` : (c.city ?? "Toàn quốc")}
                          </span>
                        </div>
                        {c.licenseNumber && (
                          <div className="text-muted-foreground flex items-center gap-1 truncate max-w-[200px]">
                            <ShieldCheckIcon className="w-3 h-3 text-purple-600 shrink-0" />
                            {c.licenseNumber}
                          </div>
                        )}
                      </td>

                      {/* Visa Rate & Rating */}
                      <td className="py-3.5 px-4 text-xs space-y-1">
                        <div className="flex items-center gap-1 text-emerald-600 font-semibold">
                          <FlameIcon className="w-3.5 h-3.5" />
                          <span>Đỗ Visa: {c.visaSuccessRate ? `${c.visaSuccessRate}%` : "98.5%"}</span>
                        </div>
                        <div className="flex items-center gap-1 text-amber-500 font-medium">
                          <StarIcon className="w-3 h-3 fill-current" />
                          <span>{Number(c.averageRating ?? 4.9).toFixed(1)}</span>
                          <span className="text-muted-foreground">({c.reviewCount ?? 0} đánh giá)</span>
                        </div>
                      </td>

                      {/* Verification & Active Status */}
                      <td className="py-3.5 px-4">
                        <div className="flex flex-col gap-1 items-start">
                          {isVerified ? (
                            <div className="flex flex-col gap-0.5 items-start">
                              <Badge className="bg-emerald-600 text-white gap-1 text-xs px-2 py-0.5">
                                <CheckCircle2Icon className="w-3 h-3" />
                                Đã xác minh
                              </Badge>
                              {c.verifiedAt && (
                                <span className="text-xs text-muted-foreground">
                                  {new Date(c.verifiedAt).toLocaleDateString("vi-VN")}
                                </span>
                              )}
                            </div>
                          ) : (
                            <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-xs px-2 py-0.5">
                              Chờ duyệt
                            </Badge>
                          )}
                          {c.isActive === false && (
                            <Badge variant="outline" className="text-rose-600 border-rose-300 bg-rose-50 dark:bg-rose-950/40 text-xs px-1.5 py-0">
                              Đã ẩn
                            </Badge>
                          )}
                        </div>
                      </td>

                      {/* Actions */}
                      <td className="py-3.5 px-4 text-right">
                        <div className="inline-flex items-center justify-end gap-1.5">
                          {/* Sửa thông tin trung tâm (UC-72) */}
                          <div onClick={(e) => e.stopPropagation()}>
                            <EditCenterButton
                              center={c}
                              size="sm"
                              variant="ghost"
                              className="h-8 px-2 text-xs text-muted-foreground hover:text-foreground"
                              onSuccess={(updated) => {
                                setCenters((prev) =>
                                  prev.map((item) => (item.id === updated.id ? { ...item, ...updated } : item))
                                );
                              }}
                            >
                              <PencilIcon className="w-3.5 h-3.5 mr-1" />
                              <span className="hidden lg:inline">Sửa</span>
                            </EditCenterButton>
                          </div>

                          {/* Ẩn / Hiện trung tâm (UC-72) */}
                          {c.isActive === false ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="gap-1 text-xs h-8 text-emerald-600 hover:text-emerald-700 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 px-2"
                              onClick={(e) => {
                                e.stopPropagation();
                                setConfirmTarget({ center: c, action: "show" });
                              }}
                              title="Hiện lại trung tâm"
                            >
                              <EyeIcon className="w-3.5 h-3.5" />
                              <span className="hidden xl:inline">Hiện</span>
                            </Button>
                          ) : (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="gap-1 text-xs h-8 text-muted-foreground hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40 px-2"
                              onClick={(e) => {
                                e.stopPropagation();
                                setConfirmTarget({ center: c, action: "hide" });
                              }}
                              title="Ẩn trung tâm khỏi gợi ý"
                            >
                              <EyeOffIcon className="w-3.5 h-3.5" />
                              <span className="hidden xl:inline">Ẩn</span>
                            </Button>
                          )}

                          {/* Xác minh / Hủy duyệt (UC-59) */}
                          {isVerified ? (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="gap-1 text-xs h-8 text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950/40 px-2"
                              onClick={(e) => {
                                e.stopPropagation();
                                setConfirmTarget({ center: c, action: "unverify" });
                              }}
                              title="Hủy xác minh trung tâm"
                            >
                              <ShieldAlertIcon className="w-3.5 h-3.5" />
                              <span className="hidden sm:inline">Hủy duyệt</span>
                            </Button>
                          ) : (
                            <Button
                              variant="outline"
                              size="sm"
                              className="gap-1 text-xs h-8 text-emerald-600 border-emerald-300 hover:bg-emerald-50 dark:hover:bg-emerald-950/40 px-2"
                              onClick={(e) => {
                                e.stopPropagation();
                                setConfirmTarget({ center: c, action: "verify" });
                              }}
                              title="Xác minh trung tâm"
                            >
                              <ShieldCheckIcon className="w-3.5 h-3.5" />
                              <span className="hidden sm:inline">Xác minh</span>
                            </Button>
                          )}
                          <Link
                            href={`/admin/centers/${c.id}`}
                            className={cn(
                              buttonVariants({ variant: "outline", size: "sm" }),
                              "gap-1.5 text-xs h-8 hover:bg-muted text-foreground border-border"
                            )}
                            onClick={(e) => e.stopPropagation()}
                          >
                            <EyeIcon className="w-3.5 h-3.5 text-muted-foreground" />
                            <span className="hidden md:inline">Xem chi tiết</span>
                            <ChevronRightIcon className="w-3 h-3 text-muted-foreground" />
                          </Link>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="p-4 border-t">
          <TablePagination
            page={page}
            totalPages={Math.max(1, Math.ceil(totalCount / pageSize))}
            totalCount={totalCount}
            itemLabel="trung tâm tư vấn"
            disabled={isLoading}
            onPageChange={setPage}
          />
        </div>
      </div>

      {/* Quick Confirmation Dialog for Verify / Unverify / Hide / Show */}
      <Dialog
        open={confirmTarget !== null}
        onOpenChange={(open) => {
          if (!open && !isSubmitting) setConfirmTarget(null);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              {confirmTarget?.action === "verify" ? (
                <>
                  <ShieldCheckIcon className="w-5 h-5 text-emerald-600" />
                  <span>Xác minh trung tâm tư vấn</span>
                </>
              ) : confirmTarget?.action === "unverify" ? (
                <>
                  <ShieldAlertIcon className="w-5 h-5 text-rose-600" />
                  <span>Hủy xác minh trung tâm tư vấn</span>
                </>
              ) : confirmTarget?.action === "hide" ? (
                <>
                  <EyeOffIcon className="w-5 h-5 text-rose-600" />
                  <span>Ẩn trung tâm tư vấn</span>
                </>
              ) : (
                <>
                  <EyeIcon className="w-5 h-5 text-emerald-600" />
                  <span>Hiện lại trung tâm tư vấn</span>
                </>
              )}
            </DialogTitle>
            <DialogDescription className="pt-2 text-sm leading-relaxed">
              {confirmTarget?.action === "verify" ? (
                <>
                  Bạn có chắc chắn muốn xác minh trung tâm{" "}
                  <strong className="text-foreground">{confirmTarget?.center.name}</strong>?
                  <br />
                  Sau khi xác minh, trung tâm sẽ được gắn huy hiệu &quot;Đã xác minh&quot; và ưu tiên xuất hiện trong danh bạ đề xuất cho học sinh, phụ huynh.
                </>
              ) : confirmTarget?.action === "unverify" ? (
                <>
                  Bạn có chắc chắn muốn hủy trạng thái xác minh của trung tâm{" "}
                  <strong className="text-foreground">{confirmTarget?.center.name}</strong>?
                  <br />
                  Trung tâm sẽ chuyển về trạng thái Chưa xác minh (&quot;unverified&quot;) và không còn hiển thị huy hiệu xác minh trên toàn hệ thống.
                </>
              ) : confirmTarget?.action === "hide" ? (
                <>
                  Bạn có chắc chắn muốn ẩn trung tâm{" "}
                  <strong className="text-foreground">{confirmTarget?.center.name}</strong>?
                  <br />
                  Trung tâm sẽ bị ẩn khỏi danh bạ công khai và thuật toán gợi ý của AI sẽ không đề xuất trung tâm này cho học sinh.
                </>
              ) : (
                <>
                  Bạn có chắc chắn muốn kích hoạt và hiển thị lại trung tâm{" "}
                  <strong className="text-foreground">{confirmTarget?.center.name}</strong>?
                  <br />
                  Trung tâm sẽ xuất hiện trở lại trên danh bạ công khai và đủ điều kiện để AI gợi ý.
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="mt-4 gap-2 sm:gap-0">
            <Button
              variant="outline"
              onClick={() => setConfirmTarget(null)}
              disabled={isSubmitting}
            >
              Hủy bỏ
            </Button>
            <Button
              variant={
                confirmTarget?.action === "verify" || confirmTarget?.action === "show"
                  ? "default"
                  : "destructive"
              }
              onClick={handleConfirmTarget}
              disabled={isSubmitting}
              className={
                confirmTarget?.action === "verify" || confirmTarget?.action === "show"
                  ? "bg-emerald-600 hover:bg-emerald-700 text-white"
                  : undefined
              }
            >
              {isSubmitting ? (
                <>
                  <Spinner className="w-4 h-4 mr-1.5" />
                  Đang xử lý...
                </>
              ) : confirmTarget?.action === "verify" ? (
                <>
                  <CheckCircle2Icon className="w-4 h-4 mr-1.5" />
                  Xác nhận xác minh
                </>
              ) : confirmTarget?.action === "unverify" ? (
                <>
                  <ShieldAlertIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận hủy xác minh
                </>
              ) : confirmTarget?.action === "hide" ? (
                <>
                  <EyeOffIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận ẩn
                </>
              ) : (
                <>
                  <EyeIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận hiện
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
