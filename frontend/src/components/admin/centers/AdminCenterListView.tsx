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
  RotateCcwIcon,
  FlameIcon,
  ChevronRightIcon,
  EyeIcon,
} from "lucide-react";
import { studyCentersApi } from "@/lib/api";
import type { StudyCenter, PagedResult } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AddCenterButton } from "@/components/admin/AddCenterButton";
import { TablePagination } from "@/components/admin/shared/TablePagination";
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
      <div className="bg-card border rounded-xl p-4 space-y-3 shadow-sm">
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
      <div className="bg-card border rounded-xl overflow-hidden shadow-sm w-full">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-muted/50 border-b text-xs font-semibold uppercase text-muted-foreground tracking-wider">
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
                      onClick={() => router.push(`/moderator/centers/${c.id}`)}
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
                              <Badge variant="outline" className="text-[10px] font-mono px-1.5 py-0">
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

                      {/* Verification Status */}
                      <td className="py-3.5 px-4">
                        {isVerified ? (
                          <Badge className="bg-emerald-600 text-white gap-1 text-xs">
                            <CheckCircle2Icon className="w-3 h-3" />
                            Đã xác minh
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-xs">
                            Chờ duyệt
                          </Badge>
                        )}
                      </td>

                      {/* Actions */}
                      <td className="py-3.5 px-4 text-right">
                        <Link
                          href={`/moderator/centers/${c.id}`}
                          className={cn(
                            buttonVariants({ variant: "outline", size: "sm" }),
                            "gap-1.5 text-xs h-8 hover:bg-muted text-foreground border-border"
                          )}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <EyeIcon className="w-3.5 h-3.5 text-muted-foreground" />
                          Xem chi tiết
                          <ChevronRightIcon className="w-3 h-3 text-muted-foreground" />
                        </Link>
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
    </div>
  );
}
