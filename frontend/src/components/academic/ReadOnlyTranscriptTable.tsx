"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { PencilIcon, RefreshCwIcon } from "lucide-react";
import type { TranscriptScore } from "@/types/academic";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

interface ReadOnlyTranscriptTableProps {
  scores: TranscriptScore[];
  onRefreshAnalysis?: () => void;
  isAnalyzing?: boolean;
}

export function ReadOnlyTranscriptTable({
  scores,
  onRefreshAnalysis,
  isAnalyzing = false,
}: ReadOnlyTranscriptTableProps) {
  const [filterTerm, setFilterTerm] = useState<number | "all">("all");
  const [searchSubject, setSearchSubject] = useState("");

  const uniqueTerms = useMemo(() => {
    const map = new Map<number, string>();
    scores.forEach((s) => {
      if (!map.has(s.termOrder)) {
        map.set(s.termOrder, s.termName);
      }
    });
    return Array.from(map.entries())
      .map(([order, name]) => ({ order, name }))
      .sort((a, b) => a.order - b.order);
  }, [scores]);

  const filteredScores = useMemo(() => {
    return scores.filter((item) => {
      const matchTerm = filterTerm === "all" || item.termOrder === filterTerm;
      const matchSearch =
        !searchSubject.trim() ||
        item.subject.toLowerCase().includes(searchSubject.toLowerCase().trim());
      return matchTerm && matchSearch;
    });
  }, [scores, filterTerm, searchSubject]);

  const stats = useMemo(() => {
    if (filteredScores.length === 0) return { count: 0, totalCredits: 0, avgRaw: 0 };
    const count = filteredScores.length;
    const totalCredits = filteredScores.reduce((acc, cur) => acc + (cur.credits || 1), 0);
    const sumWeightedScore = filteredScores.reduce(
      (acc, cur) => acc + cur.score * (cur.credits || 1),
      0
    );
    const avgRaw = totalCredits > 0 ? sumWeightedScore / totalCredits : 0;
    return { count, totalCredits, avgRaw: Math.round(avgRaw * 100) / 100 };
  }, [filteredScores]);

  const getGroupBadgeVariant = (groupKey: string) => {
    switch (groupKey) {
      case "stem":
      case "languages":
      case "social_sciences":
        return "brand";
      default:
        return "neutral";
    }
  };

  return (
    <Card>
      {/* Header Toolbar */}
      <CardHeader>
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          <div>
            <div className="flex items-center gap-2">
              <CardTitle>
                Bảng Điểm Học Tập & Quy Đổi WES 4.0
              </CardTitle>
              <Badge variant="brand">
                {scores.length} đầu điểm
              </Badge>
            </div>
            <CardDescription>
              Dữ liệu trích xuất từ hồ sơ học bạ học sinh đã khai báo, được quy đổi theo thang điểm chuẩn WES của Mỹ.
            </CardDescription>
          </div>

          <div className="flex items-center gap-2">
            {onRefreshAnalysis && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onRefreshAnalysis}
                disabled={isAnalyzing}
              >
                <RefreshCwIcon aria-hidden="true" />
                {isAnalyzing ? "Đang tính..." : "Cập nhật kết quả"}
              </Button>
            )}

            <Link
              href="/profile/academic"
              className={buttonVariants({ size: "sm" })}
            >
              <PencilIcon aria-hidden="true" /> Chỉnh sửa bảng điểm
            </Link>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Filter and Search Bar */}
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
          {/* Term Tabs */}
          <div className="flex flex-wrap gap-1">
            <Button
              type="button"
              size="xs"
              variant={filterTerm === "all" ? "default" : "secondary"}
              aria-pressed={filterTerm === "all"}
              onClick={() => setFilterTerm("all")}
            >
              Tất cả ({scores.length})
            </Button>
            {uniqueTerms.map((t) => {
              const count = scores.filter((s) => s.termOrder === t.order).length;
              const isActive = filterTerm === t.order;
              return (
                <Button
                  key={t.order}
                  type="button"
                  size="xs"
                  variant={isActive ? "default" : "secondary"}
                  aria-pressed={isActive}
                  onClick={() => setFilterTerm(t.order)}
                >
                  {t.name} ({count})
                </Button>
              );
            })}
          </div>

          {/* Search Input */}
          <div className="w-full sm:w-56">
            <Input
              id="readonly-search-subject"
              type="text"
              aria-label="Lọc theo tên môn học"
              value={searchSubject}
              onChange={(e) => setSearchSubject(e.target.value)}
              placeholder="Tìm kiếm môn học..."
            />
          </div>
        </div>

        {/* Table */}
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Học kỳ</TableHead>
              <TableHead>Tên môn học</TableHead>
              <TableHead>Phân nhóm</TableHead>
              <TableHead className="text-center">Điểm hệ 10</TableHead>
              <TableHead className="text-center">Tín chỉ / Hệ số</TableHead>
              <TableHead className="text-center">Điểm GPA 4.0 (WES)</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {filteredScores.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} className="text-center whitespace-normal text-muted-foreground">
                  {scores.length === 0
                    ? "Chưa có môn học nào trong bảng điểm. Vui lòng bấm 'Chỉnh sửa bảng điểm' để nhập liệu."
                    : "Không tìm thấy môn học nào khớp với bộ lọc."}
                </TableCell>
              </TableRow>
            ) : (
              filteredScores.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>
                    {item.termName}
                  </TableCell>
                  <TableCell className="whitespace-normal">
                    {item.subject}
                  </TableCell>
                  <TableCell>
                    <Badge variant={getGroupBadgeVariant(item.subjectGroup)}>
                      {item.subjectGroupName}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-center text-numeric">
                    {item.score.toFixed(1)}
                  </TableCell>
                  <TableCell className="text-center">
                    {item.credits || 1}
                  </TableCell>
                  <TableCell className="text-center text-numeric">
                    {item.gpa4.toFixed(2)}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
          {filteredScores.length > 0 && (
            <TableFooter>
              <TableRow>
                <TableCell colSpan={3}>
                  Tổng kết hiển thị ({stats.count} môn):
                </TableCell>
                <TableCell className="text-center">
                  TB: {stats.avgRaw.toFixed(2)}
                </TableCell>
                <TableCell className="text-center">
                  {stats.totalCredits} TC
                </TableCell>
                <TableCell className="text-center text-muted-foreground">
                  * Thang chuẩn WES
                </TableCell>
              </TableRow>
            </TableFooter>
          )}
        </Table>
      </CardContent>
    </Card>
  );
}
