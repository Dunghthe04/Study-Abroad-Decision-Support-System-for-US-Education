"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import {
  SearchIcon,
  MapPinIcon,
  CheckCircle2Icon,
  StarIcon,
  PhoneIcon,
  ChevronRightIcon,
  Building2Icon,
  RotateCcwIcon,
  ShieldCheckIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { centerServiceLabel } from "@/lib/center-services";
import { studyLevelLabel } from "@/lib/study-levels";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { AddCenterButton } from "@/components/admin/AddCenterButton";
import { cn } from "cn";

interface PublicCenterListViewProps {
  initialCenters: StudyCenter[];
}

const CITY_OPTIONS = [
  { value: "all", label: "Toàn quốc" },
  { value: "Hà Nội", label: "Hà Nội" },
  { value: "TP. Hồ Chí Minh", label: "TP. Hồ Chí Minh" },
  { value: "Hải Phòng", label: "Hải Phòng" },
  { value: "Đà Nẵng", label: "Đà Nẵng" },
];

const TYPE_OPTIONS = [
  { value: "all", label: "Tất cả mô hình" },
  { value: "hybrid", label: "Toàn diện (Du học & Luyện thi)" },
  { value: "study_abroad", label: "Tư vấn du học" },
  { value: "english_training", label: "Luyện thi chứng chỉ" },
];

export function PublicCenterListView({ initialCenters }: PublicCenterListViewProps) {
  const { user } = useAuth();
  const isAdmin = user?.role === "admin" || user?.role === "moderator";

  const [search, setSearch] = useState("");
  const [selectedCity, setSelectedCity] = useState("all");
  const [selectedType, setSelectedType] = useState("all");

  const filteredCenters = useMemo(() => {
    return initialCenters.filter((center) => {
      // Search by name, code, address
      if (search.trim()) {
        const q = search.toLowerCase().trim();
        const matchesName = center.name.toLowerCase().includes(q);
        const matchesCode = center.code.toLowerCase().includes(q);
        const matchesAddress = (center.address ?? "").toLowerCase().includes(q);
        const matchesCity = (center.city ?? "").toLowerCase().includes(q);
        if (!matchesName && !matchesCode && !matchesAddress && !matchesCity) {
          return false;
        }
      }

      // Filter by city
      if (selectedCity !== "all") {
        if (!center.city?.toLowerCase().includes(selectedCity.toLowerCase())) {
          return false;
        }
      }

      // Filter by type
      if (selectedType !== "all") {
        const centerType = center.centerType || "study_abroad";
        if (centerType !== selectedType) {
          return false;
        }
      }

      return true;
    });
  }, [initialCenters, search, selectedCity, selectedType]);

  const handleResetFilters = () => {
    setSearch("");
    setSelectedCity("all");
    setSelectedType("all");
  };

  return (
    <div className="space-y-6">
      {/* Admin Quick Switch Notice Banner */}
      {isAdmin && (
        <div className="p-3.5 rounded-xl border border-brand/20 bg-brand/5 flex flex-wrap items-center justify-between gap-3 text-sm">
          <div className="flex items-center gap-2 text-foreground">
            <ShieldCheckIcon className="w-4 h-4 text-brand shrink-0" />
            <span>
              Bạn đang xem danh bạ với tư cách <strong>Quản trị viên</strong>.
            </span>
          </div>
          <div className="flex items-center gap-2">
            <AddCenterButton />
            <Link
              href="/admin/centers"
              className={cn(buttonVariants({ variant: "default", size: "sm" }), "text-xs gap-1")}
            >
              Chuyển tới Quản trị Trung tâm
              <ChevronRightIcon className="w-3.5 h-3.5" />
            </Link>
          </div>
        </div>
      )}

      {/* Header Info */}
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
          Danh bạ Trung tâm Tư vấn Du học Mỹ
        </h1>
        <p className="text-sm text-muted-foreground">
          Hệ thống các tổ chức tư vấn du học, luyện thi chuẩn hóa và đào tạo kỹ năng đã được xác minh trên USAS.
        </p>
      </div>

      {/* Search & Filter Toolbar */}
      <div className="bg-card border rounded-xl p-4 shadow-sm space-y-3.5">
        <div className="relative">
          <SearchIcon className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm kiếm theo tên trung tâm, mã cơ sở hoặc địa chỉ..."
            className="pl-10 h-10 text-sm"
          />
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3 pt-1 border-t">
          {/* City filter pills */}
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="text-xs text-muted-foreground font-medium mr-1">Địa điểm:</span>
            {CITY_OPTIONS.map((city) => (
              <Button
                key={city.value}
                type="button"
                variant={selectedCity === city.value ? "default" : "outline"}
                size="sm"
                onClick={() => setSelectedCity(city.value)}
                className="text-xs h-7.5 px-2.5 rounded-full"
              >
                {city.label}
              </Button>
            ))}
          </div>

          {/* Type filter & Reset */}
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={selectedType}
              onChange={(e) => setSelectedType(e.target.value)}
              className="text-xs h-8 px-2.5 rounded-lg border border-input bg-background font-medium focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {TYPE_OPTIONS.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>

            {(search || selectedCity !== "all" || selectedType !== "all") && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={handleResetFilters}
                className="text-xs h-8 gap-1 text-muted-foreground hover:text-foreground"
              >
                <RotateCcwIcon className="w-3 h-3" />
                Đặt lại
              </Button>
            )}
          </div>
        </div>
      </div>

      {/* Results Count */}
      <div className="text-xs text-muted-foreground">
        Tìm thấy <strong>{filteredCenters.length}</strong> / <strong>{initialCenters.length}</strong> trung tâm tư vấn
      </div>

      {/* Centers Cards Grid */}
      {filteredCenters.length === 0 ? (
        <div className="p-12 text-center border rounded-xl bg-card space-y-3">
          <Building2Icon className="w-10 h-10 text-muted-foreground/40 mx-auto" />
          <div className="font-semibold text-foreground text-sm">Không tìm thấy trung tâm phù hợp</div>
          <p className="text-xs text-muted-foreground max-w-sm mx-auto">
            Không có trung tâm nào khớp với từ khóa tìm kiếm hoặc bộ lọc hiện tại. Bạn vui lòng thử lại với tiêu chí khác.
          </p>
          <Button variant="outline" size="sm" onClick={handleResetFilters} className="text-xs">
            Xóa bộ lọc
          </Button>
        </div>
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-2">
          {filteredCenters.map((c) => {
            const profile = getEnrichedCenterProfile(c);
            const isVerified = c.verificationStatus === "verified" || profile.center.verificationStatus === "verified";

            return (
              <li key={c.id}>
                <Card className="h-full flex flex-col justify-between hover:border-brand/40 hover:shadow-md transition-all group overflow-hidden bg-card">
                  {/* Card Header with Logo, Name, Badge */}
                  <CardHeader className="pb-3">
                    <div className="flex items-start gap-3">
                      <div className="w-12 h-12 rounded-xl overflow-hidden border bg-muted shrink-0 flex items-center justify-center shadow-xs">
                        <img
                          src={profile.logoImage}
                          alt={c.name}
                          className="w-full h-full object-cover"
                        />
                      </div>

                      <div className="min-w-0 flex-1">
                        <div className="flex items-start justify-between gap-2">
                          <Link href={`/centers/${c.id}`} className="hover:text-brand transition-colors">
                            <CardTitle className="text-base sm:text-lg group-hover:text-brand transition-colors line-clamp-1">
                              {c.name}
                            </CardTitle>
                          </Link>
                          {c.city && (
                            <Badge variant="outline" className="shrink-0 font-normal text-xs px-2 py-0">
                              <MapPinIcon className="w-3 h-3 text-brand mr-1" />
                              {c.city}
                            </Badge>
                          )}
                        </div>

                        <div className="flex flex-wrap items-center gap-2 mt-1 text-xs">
                          {isVerified ? (
                            <span className="inline-flex items-center gap-1 text-[11px] text-emerald-600 font-medium">
                              <CheckCircle2Icon className="w-3.5 h-3.5" />
                              Đã xác minh
                            </span>
                          ) : (
                            <span className="text-[11px] text-amber-600 font-medium">
                              Chờ xác minh
                            </span>
                          )}
                          <span>•</span>
                          <span className="text-[11px] text-muted-foreground">{profile.typeLabel}</span>
                          <span>•</span>
                          <div className="inline-flex items-center gap-0.5 text-amber-500 font-semibold text-[11px]">
                            <StarIcon className="w-3 h-3 fill-current" />
                            <span>{profile.rating.toFixed(1)}</span>
                            <span className="text-muted-foreground font-normal">({profile.reviewCount})</span>
                          </div>
                        </div>
                      </div>
                    </div>

                    {c.address && (
                      <CardDescription className="text-xs pt-1 line-clamp-1">
                        {c.address}
                      </CardDescription>
                    )}
                  </CardHeader>

                  {/* Card Content - Services */}
                  <CardContent className="pt-0 space-y-2.5">
                    {c.services.length > 0 && (
                      <div className="flex flex-wrap gap-1">
                        {c.services.slice(0, 4).map((s) => (
                          <Badge key={s} variant="secondary" className="font-normal text-[11px] py-0 px-2 bg-muted/60">
                            {centerServiceLabel(s)}
                          </Badge>
                        ))}
                        {c.services.length > 4 && (
                          <Badge variant="outline" className="font-normal text-[10px] py-0 px-1.5 text-muted-foreground">
                            +{c.services.length - 4} dịch vụ
                          </Badge>
                        )}
                      </div>
                    )}

                    {c.studyLevels.length > 0 && (
                      <div className="text-[11px] text-muted-foreground line-clamp-1">
                        <strong className="text-foreground font-medium">Bậc học:</strong>{" "}
                        {c.studyLevels.map(studyLevelLabel).join(", ")}
                      </div>
                    )}
                  </CardContent>

                  {/* Card Footer */}
                  <CardFooter className="pt-3 pb-3 border-t mt-auto flex justify-between items-center text-xs bg-muted/20">
                    <div className="text-muted-foreground text-[11px] flex items-center gap-2">
                      <span className="font-mono">Mã: {c.code}</span>
                      {c.phone && (
                        <>
                          <span>•</span>
                          <span className="inline-flex items-center gap-1 text-emerald-600">
                            <PhoneIcon className="w-3 h-3" />
                            {c.phone}
                          </span>
                        </>
                      )}
                    </div>

                    <Link
                      href={`/centers/${c.id}`}
                      className={cn(buttonVariants({ variant: "ghost", size: "sm" }), "gap-1 text-xs hover:text-brand h-8 -mr-2")}
                    >
                      Xem chi tiết
                      <ChevronRightIcon className="w-3.5 h-3.5" />
                    </Link>
                  </CardFooter>
                </Card>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
