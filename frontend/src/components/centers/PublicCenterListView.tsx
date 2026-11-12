"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  SearchIcon,
  MapPinIcon,
  CheckCircle2Icon,
  StarIcon,
  ChevronRightIcon,
  Building2Icon,
  RotateCcwIcon,
  ShieldCheckIcon,
  FlameIcon,
  EyeIcon,
  ExternalLinkIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { TablePagination } from "@/components/admin/shared/TablePagination";
import { cn } from "cn";

interface PublicCenterListViewProps {
  initialCenters: StudyCenter[];
}

const CITY_OPTIONS = [
  { value: "all", label: "Tất cả địa điểm" },
  { value: "Hà Nội", label: "Hà Nội" },
  { value: "TP.HCM", label: "TP. Hồ Chí Minh" },
  { value: "Hải Phòng", label: "Hải Phòng" },
  { value: "Đà Nẵng", label: "Đà Nẵng" },
];

const TYPE_OPTIONS = [
  { value: "all", label: "Mọi loại hình" },
  { value: "study_abroad", label: "Tư vấn Du học" },
  { value: "english_training", label: "Luyện thi Chứng chỉ" },
  { value: "hybrid", label: "Toàn diện (Du học & Luyện thi)" },
];

const STATUS_OPTIONS = [
  { value: "all", label: "Tất cả trạng thái" },
  { value: "verified", label: "Đã xác minh" },
  { value: "unverified", label: "Chờ xác minh" },
];

export function PublicCenterListView({ initialCenters }: PublicCenterListViewProps) {
  const router = useRouter();
  const { user } = useAuth();
  const isAdmin = user?.role === "admin" || user?.role === "moderator";

  const [search, setSearch] = useState("");
  const [cityFilter, setCityFilter] = useState("all");
  const [typeFilter, setTypeFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const filteredCenters = useMemo(() => {
    return initialCenters.filter((c) => {
      // Đối với người dùng phổ thông (học sinh, phụ huynh): Chỉ hiển thị trung tâm đã xác minh
      if (!isAdmin && c.verificationStatus !== "verified") {
        return false;
      }

      // Tìm kiếm từ khóa
      if (search.trim()) {
        const q = search.toLowerCase().trim();
        const matchesName = c.name.toLowerCase().includes(q);
        const matchesCode = c.code.toLowerCase().includes(q);
        const matchesAddress = (c.address ?? "").toLowerCase().includes(q);
        const matchesCity = (c.city ?? "").toLowerCase().includes(q);
        if (!matchesName && !matchesCode && !matchesAddress && !matchesCity) {
          return false;
        }
      }

      // Lọc thành phố
      if (cityFilter !== "all") {
        const targetCity = cityFilter === "TP.HCM" ? "hồ chí minh" : cityFilter.toLowerCase();
        if (!c.city?.toLowerCase().includes(targetCity)) {
          return false;
        }
      }

      // Lọc mô hình hoạt động
      if (typeFilter !== "all") {
        const centerType = c.centerType || "study_abroad";
        if (centerType !== typeFilter) {
          return false;
        }
      }

      // Lọc trạng thái xác thực (chỉ dành cho admin/moderator)
      if (isAdmin && statusFilter !== "all") {
        const status = c.verificationStatus || "unverified";
        if (status !== statusFilter) {
          return false;
        }
      }

      return true;
    });
  }, [initialCenters, search, cityFilter, typeFilter, statusFilter, isAdmin]);

  // Phân trang
  const totalCount = filteredCenters.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const paginatedCenters = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filteredCenters.slice(start, start + pageSize);
  }, [filteredCenters, page, pageSize]);

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
    <div className="space-y-6">
      {/* Moderator Quick Switch Notice Banner */}
      {isAdmin && (
        <div className="p-3.5 rounded-xl border border-brand/20 bg-brand/5 flex flex-wrap items-center justify-between gap-3 text-sm">
          <div className="flex items-center gap-2 text-foreground">
            <ShieldCheckIcon className="w-4 h-4 text-brand shrink-0" />
            <span>Bạn đang đăng nhập với quyền <strong>{user?.role === "admin" ? "Quản trị viên" : "Moderator"}</strong>. Xem trang kiểm duyệt để thêm mới hoặc duyệt hồ sơ cơ sở.</span>
          </div>
          <Link
            href="/admin/centers"
            className={cn(buttonVariants({ size: "sm" }), "gap-1.5 text-xs bg-brand text-white hover:bg-brand/90")}
          >
            <span>Vào Quản lý trung tâm (Moderator)</span>
            <ExternalLinkIcon className="w-3.5 h-3.5" />
          </Link>
        </div>
      )}

      {/* Header bar */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b pb-5">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground flex items-center gap-2">
            <Building2Icon className="w-6 h-6 text-brand" />
            Danh bạ Trung tâm Tư vấn Du học & Đào tạo
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Danh bạ các trung tâm tư vấn du học và đào tạo chứng chỉ chuẩn hóa uy tín được xác minh trên hệ thống USAS.
          </p>
        </div>
      </div>

      {/* Filter toolbar */}
      <div className="bg-card border rounded-xl p-4 ">
        <form onSubmit={(e) => { e.preventDefault(); setPage(1); }} className="flex flex-col lg:flex-row gap-3">
          <div className="relative flex-1 min-w-[240px]">
            <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              aria-label="Tìm trung tâm theo tên, mã hoặc địa chỉ"
              placeholder="Tìm theo tên trung tâm, mã (CTY_013, Summit...), địa chỉ..."
              className="pl-9 w-full"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2 shrink-0">
            <select
              value={cityFilter}
              aria-label="Lọc theo thành phố"
              onChange={(e) => {
                setCityFilter(e.target.value);
                setPage(1);
              }}
              className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {CITY_OPTIONS.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>

            <select
              value={typeFilter}
              aria-label="Lọc theo loại trung tâm"
              onChange={(e) => {
                setTypeFilter(e.target.value);
                setPage(1);
              }}
              className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {TYPE_OPTIONS.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>

            {isAdmin && (
              <select
                value={statusFilter}
                aria-label="Lọc theo trạng thái xác minh"
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
                className="h-9 px-3 rounded-md border border-input bg-background text-sm text-foreground focus:outline-none focus:ring-1 focus:ring-ring"
              >
                {STATUS_OPTIONS.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            )}

            <Button type="submit" variant="secondary" size="sm" className="h-9 gap-1.5">
              <SearchIcon className="w-3.5 h-3.5" />
              Tìm kiếm
            </Button>

            {(search || cityFilter !== "all" || typeFilter !== "all" || (isAdmin && statusFilter !== "all")) && (
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
            )}
          </div>
        </form>
      </div>

      {/* Centers Table - Aligned with Admin UI */}
      <div className="bg-card border rounded-xl overflow-hidden w-full">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm min-w-[760px]">
            <thead className="bg-muted/50 border-b text-xs font-semibold text-muted-foreground">
              <tr>
                <th className="py-3 px-4 min-w-[220px]">Trung tâm & Mã</th>
                <th className="py-3 px-4">Mô hình hoạt động</th>
                <th className="py-3 px-4">Địa điểm & Pháp lý</th>
                <th className="py-3 px-4">Tỷ lệ Visa & Đánh giá</th>
                {isAdmin && <th className="py-3 px-4">Trạng thái</th>}
                <th className="py-3 px-4 text-right min-w-[130px]">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {paginatedCenters.length === 0 ? (
                <tr>
                  <td colSpan={isAdmin ? 6 : 5} className="py-12 text-center text-muted-foreground">
                    Không tìm thấy trung tâm nào khớp với bộ lọc tìm kiếm.
                  </td>
                </tr>
              ) : (
                paginatedCenters.map((c) => {
                  const isVerified = c.verificationStatus === "verified";
                  return (
                    <tr
                      key={c.id}
                      onClick={() => router.push(`/centers/${c.id}`)}
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
                            <div className="font-semibold text-foreground flex items-center gap-1.5">
                              <span className="truncate max-w-[220px]">{c.name}</span>
                              {isVerified && (
                                <span title="Đã xác minh bởi USAS" className="inline-flex text-emerald-600 shrink-0">
                                  <CheckCircle2Icon className="w-4 h-4" />
                                </span>
                              )}
                            </div>
                            <div className="flex items-center gap-2 mt-0.5">
                              <Badge variant="outline" className="text-[13px] font-mono px-1.5 py-0">
                                {c.code}
                              </Badge>
                              {isVerified ? (
                                <Badge variant="secondary" className="bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300 border-emerald-200 text-[13px] px-1.5 py-0">
                                  Đã xác minh
                                </Badge>
                              ) : (
                                <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-[13px] px-1.5 py-0">
                                  Chờ duyệt
                                </Badge>
                              )}
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
                        {c.taxCode && (
                          <div className="text-[11px] text-muted-foreground font-mono truncate max-w-[200px]">
                            MST: {c.taxCode}
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

                      {/* Verification Status (Only for Admin) */}
                      {isAdmin && (
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
                      )}

                      {/* Actions */}
                      <td className="py-3.5 px-4 text-right">
                        <Link
                          href={`/centers/${c.id}`}
                          className={cn(
                            buttonVariants({ variant: "outline", size: "sm" }),
                            "gap-1.5 text-xs h-8 hover:bg-muted text-foreground border-border whitespace-nowrap"
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
        {totalPages > 1 && (
          <div className="p-4 border-t">
            <TablePagination
              page={page}
              totalPages={totalPages}
              totalCount={totalCount}
              itemLabel="trung tâm"
              onPageChange={(p) => setPage(p)}
            />
          </div>
        )}
      </div>
    </div>
  );
}
