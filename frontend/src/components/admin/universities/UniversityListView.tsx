"use client";

import { useEffect, useState } from "react";
import { ChevronLeftIcon, ChevronRightIcon, SearchIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  NativeSelect,
  NativeSelectOption,
} from "@/components/ui/native-select";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { getAdminUniversities } from "@/lib/admin-university-api";
import {
  controlLabel,
  formatAcceptanceRate,
  formatRetrievedAt,
} from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import { OfferingsPanel } from "./OfferingsPanel";
import type { PagedResult } from "@/types/api";
import type {
  AdminUniversityActiveFilter,
  AdminUniversityFilters,
  AdminUniversityListItem,
} from "@/types/admin-university";

const EMPTY_FILTERS: AdminUniversityFilters = {
  search: "",
  state: "",
  control: "",
  active: "all",
  page: 1,
};

interface LoadResult {
  key: string;
  data: PagedResult<AdminUniversityListItem> | null;
  error: string | null;
}

export function UniversityListView() {
  const [draft, setDraft] = useState<AdminUniversityFilters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<AdminUniversityFilters>(EMPTY_FILTERS);
  const [result, setResult] = useState<LoadResult | null>(null);
  const [selected, setSelected] = useState<AdminUniversityListItem | null>(
    null,
  );

  const requestKey = JSON.stringify(applied);
  const isLoading = result === null || result.key !== requestKey;
  const data = isLoading ? null : result.data;
  const error = isLoading ? null : result.error;

  useEffect(() => {
    let cancelled = false;

    getAdminUniversities(applied)
      .then((loaded) => {
        if (!cancelled) {
          setResult({ key: requestKey, data: loaded, error: null });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          const message =
            err instanceof Error
              ? err.message
              : "Không thể tải danh sách trường.";
          setResult({ key: requestKey, data: null, error: message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [applied, requestKey]);

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setApplied({ ...draft, page: 1 });
  };

  const handleReset = () => {
    setDraft(EMPTY_FILTERS);
    setApplied(EMPTY_FILTERS);
  };

  const goToPage = (page: number) => {
    setApplied({ ...applied, page });
  };

  const totalPages = data?.totalPages ?? 0;
  const currentPage = data?.page ?? applied.page;

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 lg:flex-row">
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-4">
        <form
          onSubmit={handleSubmit}
          className="flex shrink-0 flex-wrap items-end gap-3"
        >
          <label className="flex min-w-56 flex-1 flex-col gap-1 text-sm font-medium">
            Tìm theo tên hoặc mã
            <Input
              value={draft.search}
              onChange={(event) =>
                setDraft({ ...draft, search: event.target.value })
              }
              placeholder="Ví dụ: Oregon, 209542"
            />
          </label>
          <label className="flex w-24 flex-col gap-1 text-sm font-medium">
            Bang
            <Input
              value={draft.state}
              onChange={(event) =>
                setDraft({ ...draft, state: event.target.value })
              }
              placeholder="CA"
              maxLength={2}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium">
            Loại trường
            <NativeSelect
              value={draft.control}
              onChange={(event) =>
                setDraft({ ...draft, control: event.target.value })
              }
            >
              <NativeSelectOption value="">Tất cả</NativeSelectOption>
              <NativeSelectOption value="public">Công lập</NativeSelectOption>
              <NativeSelectOption value="private">Tư thục</NativeSelectOption>
            </NativeSelect>
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium">
            Hiển thị
            <NativeSelect
              value={draft.active}
              onChange={(event) =>
                setDraft({
                  ...draft,
                  active: event.target.value as AdminUniversityActiveFilter,
                })
              }
            >
              <NativeSelectOption value="all">Tất cả</NativeSelectOption>
              <NativeSelectOption value="active">
                Đang hiển thị
              </NativeSelectOption>
              <NativeSelectOption value="hidden">Đang ẩn</NativeSelectOption>
            </NativeSelect>
          </label>
          <div className="flex gap-2">
            <Button type="submit">
              <SearchIcon />
              Tìm
            </Button>
            <Button type="button" variant="outline" onClick={handleReset}>
              Xóa lọc
            </Button>
          </div>
        </form>

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <Card className="min-h-0 flex-1 py-0">
          <CardContent className="min-h-0 flex-1 p-0 [&_[data-slot=table-container]]:h-full [&_[data-slot=table-container]]:overflow-auto">
            <Table className="border-separate border-spacing-0 [&_td]:border-r [&_td]:border-b [&_td]:border-border [&_td]:py-1.5 [&_td:last-child]:border-r-0 [&_th]:border-r [&_th]:border-b [&_th]:border-border [&_th]:bg-muted [&_th]:font-semibold [&_th:last-child]:border-r-0 [&_tr:hover_td]:bg-brand-wash/40">
              <TableHeader className="sticky top-0 z-10 bg-card">
                <TableRow>
                  <TableHead>Mã</TableHead>
                  <TableHead>Tên trường</TableHead>
                  <TableHead>Bang</TableHead>
                  <TableHead>Loại</TableHead>
                  <TableHead className="text-right">Tỷ lệ nhận</TableHead>
                  <TableHead>Bậc học</TableHead>
                  <TableHead>Hiển thị</TableHead>
                  <TableHead>Dữ liệu cập nhật</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading &&
                  Array.from({ length: 6 }, (_, index) => (
                    <TableRow key={`loading-${index}`}>
                      <TableCell colSpan={8}>
                        <Skeleton className="h-5 w-full" />
                      </TableCell>
                    </TableRow>
                  ))}

                {!isLoading && data && data.items.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={8}
                      className="py-8 text-center text-muted-foreground"
                    >
                      Không có trường nào khớp bộ lọc.
                    </TableCell>
                  </TableRow>
                )}

                {!isLoading &&
                  data?.items.map((university) => (
                    <TableRow
                      key={university.id}
                      className="cursor-pointer"
                      onClick={() => setSelected(university)}
                    >
                      <TableCell className="font-mono text-xs">
                        {university.code}
                      </TableCell>
                      <TableCell>
                        <button
                          type="button"
                          className="text-left font-medium text-brand underline-offset-2 hover:underline"
                          onClick={(event) => {
                            event.stopPropagation();
                            setSelected(university);
                          }}
                        >
                          {university.name}
                        </button>
                        {university.city && (
                          <div className="text-xs text-muted-foreground">
                            {university.city}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{university.state ?? "—"}</TableCell>
                      <TableCell>{controlLabel(university.control)}</TableCell>
                      <TableCell className="text-right">
                        {formatAcceptanceRate(university.acceptanceRate)}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap gap-1">
                          {university.studyLevels.length === 0 && (
                            <span className="text-muted-foreground">—</span>
                          )}
                          {university.studyLevels.map((level) => (
                            <Badge key={level} variant="outline">
                              {studyLevelLabel(level)}
                            </Badge>
                          ))}
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant={university.isActive ? "brand" : "outline"}
                        >
                          {university.isActive ? "Đang hiển thị" : "Đang ẩn"}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        {formatRetrievedAt(university.latestRetrievedAt)}
                      </TableCell>
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 text-sm text-muted-foreground">
          <span>{data ? `${data.totalCount} trường` : "Đang tải..."}</span>
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isLoading || currentPage <= 1}
              onClick={() => goToPage(currentPage - 1)}
            >
              <ChevronLeftIcon />
              Trước
            </Button>
            <span>
              Trang {currentPage} / {Math.max(totalPages, 1)}
            </span>
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={isLoading || currentPage >= totalPages}
              onClick={() => goToPage(currentPage + 1)}
            >
              Sau
              <ChevronRightIcon />
            </Button>
          </div>
        </div>
      </div>
      <OfferingsPanel university={selected} onClose={() => setSelected(null)} />
    </div>
  );
}
