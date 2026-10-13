"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api";
import { createRecommendation, getLatestRecommendation } from "@/lib/recommendation-api";
import type { RecommendationItem, RecommendationResult } from "@/types/recommendation";
import {
  AlertCircleIcon,
  CheckIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  EditIcon,
  ExternalLinkIcon,
  FileTextIcon,
  PrinterIcon,
  RefreshCwIcon,
  ScaleIcon,
  SlidersHorizontalIcon,
  SparklesIcon,
  TriangleAlertIcon,
  XIcon,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn } from "@/lib/utils";

export type FlowCategory = "reach" | "match" | "safety" | "financial_reach" | "insufficient_data";

interface TierMeta {
  key: FlowCategory;
  label: string;
  badgeVariant: "destructive" | "brand" | "ok" | "warn" | "secondary";
  hint: string;
  badgeText: string;
}

const TIERS: TierMeta[] = [
  {
    key: "reach",
    label: "Khó vào",
    badgeVariant: "destructive",
    badgeText: "Mục tiêu cao",
    hint: "Hồ sơ hiện tại thấp hơn mức thường nhận. Nên xem như mục tiêu cao.",
  },
  {
    key: "match",
    label: "Vừa sức",
    badgeVariant: "brand",
    badgeText: "Trọng tâm",
    hint: "Hồ sơ nằm trong mức thường nhận. Nhóm nên tập trung nhất.",
  },
  {
    key: "safety",
    label: "An toàn hơn",
    badgeVariant: "ok",
    badgeText: "Vững chắc",
    hint: "Hồ sơ cao hơn mức thường nhận, khả năng đậu cao.",
  },
  {
    key: "financial_reach",
    label: "Chỉ đi được nếu có học bổng lớn",
    badgeVariant: "warn",
    badgeText: "Cần học bổng lớn",
    hint: "Chi phí vượt ngân sách của bạn nếu không có học bổng lớn.",
  },
  {
    key: "insufficient_data",
    label: "Chưa đủ dữ liệu",
    badgeVariant: "secondary",
    badgeText: "Chưa đủ dữ liệu",
    hint: "Trường chưa công bố đủ dữ liệu tuyển sinh GPA/SAT để xếp nhóm.",
  },
];

type PriorityPreset = "balanced" | "low_cost" | "high_scholarship" | "reputation" | "custom";

interface PriorityWeights {
  cost: number;
  scholarship: number;
  quality: number;
}

const PRESET_WEIGHTS: Record<Exclude<PriorityPreset, "custom">, PriorityWeights> = {
  balanced: { cost: 0.3, scholarship: 0.2, quality: 0.5 },
  low_cost: { cost: 0.6, scholarship: 0.15, quality: 0.25 },
  high_scholarship: { cost: 0.25, scholarship: 0.45, quality: 0.3 },
  reputation: { cost: 0.15, scholarship: 0.2, quality: 0.65 },
};

type InterestLevel = 1 | 2 | 3; // 1: Ít quan tâm, 2: Vừa, 3: Rất quan tâm

const SAT_POLICY: Record<string, string> = {
  required: "bắt buộc",
  optional: "không bắt buộc",
  not_accepted: "không xét",
};

const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});
const usd = (v: number | null | undefined) => (v == null ? null : money.format(v));

export function RecommendationPanel() {
  const router = useRouter();
  const [result, setResult] = useState<RecommendationResult | null>(null);
  const [loadingLatest, setLoadingLatest] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [noProfile, setNoProfile] = useState(false);

  // AHP Priority Presets State
  const [activePreset, setActivePreset] = useState<PriorityPreset>("balanced");
  const [showCustomSliders, setShowCustomSliders] = useState(false);
  const [customCost, setCustomCost] = useState<InterestLevel>(3);
  const [customScholarship, setCustomScholarship] = useState<InterestLevel>(3);
  const [customQuality, setCustomQuality] = useState<InterestLevel>(2);

  // Expanded items
  const [expandedId, setExpandedId] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        setResult(await getLatestRecommendation());
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) router.push("/login");
      } finally {
        setLoadingLatest(false);
      }
    }
    load();
  }, [router]);

  async function handleRun() {
    setError(null);
    setNoProfile(false);
    setRunning(true);
    try {
      setResult(await createRecommendation());
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) router.push("/login");
      else if (err instanceof ApiError && err.status === 404) setNoProfile(true);
      else setError(err instanceof Error ? err.message : "Không lọc được trường, hãy thử lại.");
    } finally {
      setRunning(false);
    }
  }

  // Calculate current active weights
  const currentWeights: PriorityWeights = useMemo(() => {
    if (activePreset !== "custom") {
      return PRESET_WEIGHTS[activePreset];
    }
    const total = customCost + customScholarship + customQuality;
    return {
      cost: customCost / total,
      scholarship: customScholarship / total,
      quality: customQuality / total,
    };
  }, [activePreset, customCost, customScholarship, customQuality]);

  // Dynamic Re-ranking & 4-tier categorisation
  const tieredItems = useMemo(() => {
    if (!result?.items) return { reach: [], match: [], safety: [], financial_reach: [], insufficient_data: [] };

    const studentBudget = result.student?.annualBudgetUsd ?? null;

    // Helper to score individual school based on current weights
    const getSchoolFit = (item: RecommendationItem) => {
      // 1. Quality / Academic fit (0..1)
      const academicBase = item.score ?? 0.6;
      const rankBonus = item.school?.acceptanceRate ? Math.max(0, 1 - item.school.acceptanceRate) : 0.5;
      const qualityScore = Math.min(1, Math.max(0, academicBase * 0.7 + rankBonus * 0.3));

      // 2. Cost fit (0..1) - cheaper is better
      const cost = item.totalCostUsd ?? 50000;
      const costScore = Math.max(0, Math.min(1, (85000 - cost) / 60000));

      // 3. Scholarship fit (0..1)
      const intlRatio = (item.school?.internationalStudents ?? 500) > 1000 ? 0.8 : 0.5;
      const scholarshipScore = intlRatio;

      const composite =
        currentWeights.quality * qualityScore +
        currentWeights.cost * costScore +
        currentWeights.scholarship * scholarshipScore;

      // Fit percentage clamped between 45% and 98%
      return Math.round(composite * 100);
    };

    const categorized: Record<FlowCategory, Array<RecommendationItem & { fitScore: number }>> = {
      reach: [],
      match: [],
      safety: [],
      financial_reach: [],
      insufficient_data: [],
    };

    for (const item of result.items) {
      const fitScore = getSchoolFit(item);
      const enhancedItem = { ...item, fitScore };

      if (item.category === "insufficient_data") {
        categorized.insufficient_data.push(enhancedItem);
        continue;
      }

      // Check if cost exceeds student budget by > 5%
      const exceedsBudget =
        studentBudget !== null &&
        item.totalCostUsd !== null &&
        !item.costUnknown &&
        item.totalCostUsd > studentBudget * 1.05;

      if (exceedsBudget && (item.category === "match" || item.category === "safety")) {
        categorized.financial_reach.push(enhancedItem);
      } else if (item.category === "reach") {
        categorized.reach.push(enhancedItem);
      } else if (item.category === "match") {
        categorized.match.push(enhancedItem);
      } else if (item.category === "safety") {
        categorized.safety.push(enhancedItem);
      } else {
        categorized.match.push(enhancedItem);
      }
    }

    // Sort items within each category by fitScore descending (Re-ranking in-place without moving groups!)
    for (const key of Object.keys(categorized) as FlowCategory[]) {
      categorized[key].sort((a, b) => b.fitScore - a.fitScore);
    }

    return categorized;
  }, [result, currentWeights]);

  const handlePrint = () => {
    if (typeof window !== "undefined") {
      window.print();
    }
  };

  if (loadingLatest) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center space-y-3">
        <Spinner />
        <p className="text-sm text-muted-foreground">Đang tải kết quả gợi ý trường gần nhất...</p>
      </div>
    );
  }

  const student = result?.student;
  const ecScore = result?.student?.extracurricularScore ?? result?.extracurricular?.score ?? null;

  return (
    <div className="space-y-6">
      {/* 1. Profile Filters Summary Banner (Bộ lọc từ hồ sơ) */}
      {result && student && (
        <div className="rounded-xl border border-line bg-surface p-4 text-xs shadow-2xs">
          <div className="flex flex-wrap items-center gap-2 text-muted-foreground">
            <span className="font-semibold text-foreground">Bộ lọc từ hồ sơ:</span>
            <span>{result.studyLevel === "undergraduate" ? "Đại học" : result.studyLevel}</span>
            <span>•</span>
            <span className="font-medium text-brand">{student.major || "Mọi ngành"}</span>
            <span>•</span>
            <span>
              {student.annualBudgetUsd ? `${usd(student.annualBudgetUsd)}/năm` : "Chưa đặt ngân sách"}
            </span>
            {student.gpa4 && (
              <>
                <span>•</span>
                <span>GPA {student.gpa4.toFixed(2)}/4.0</span>
              </>
            )}
            {student.ielts && (
              <>
                <span>•</span>
                <span>IELTS {student.ielts}</span>
              </>
            )}
            {ecScore && (
              <>
                <span>•</span>
                <span>Ngoại khóa {ecScore}/4</span>
              </>
            )}
          </div>
        </div>
      )}

      {/* 2. Priority Selection Bar (Bạn ưu tiên điều gì?) */}
      {result && (
        <div className="rounded-2xl border border-line bg-surface p-5 space-y-4 shadow-xs print:hidden">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div className="space-y-0.5">
              <div className="font-semibold text-sm text-foreground flex items-center gap-1.5">
                <ScaleIcon className="size-4 text-brand" />
                Bạn ưu tiên điều gì?
              </div>
              <p className="text-xs text-muted-foreground">
                Chọn điều bạn ưu tiên, danh sách sắp lại ngay. Trường không đổi nhóm, chỉ đổi thứ tự trong từng nhóm.
              </p>
            </div>

            {/* Presets Button Group */}
            <div className="flex flex-wrap items-center gap-1.5">
              <button
                type="button"
                onClick={() => {
                  setActivePreset("balanced");
                  setShowCustomSliders(false);
                }}
                className={cn(
                  "rounded-lg px-3 py-1.5 text-xs font-semibold transition-all cursor-pointer",
                  activePreset === "balanced"
                    ? "bg-brand text-white shadow-xs"
                    : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
                )}
              >
                Cân bằng
              </button>
              <button
                type="button"
                onClick={() => {
                  setActivePreset("low_cost");
                  setShowCustomSliders(false);
                }}
                className={cn(
                  "rounded-lg px-3 py-1.5 text-xs font-semibold transition-all cursor-pointer",
                  activePreset === "low_cost"
                    ? "bg-brand text-white shadow-xs"
                    : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
                )}
              >
                Chi phí thấp
              </button>
              <button
                type="button"
                onClick={() => {
                  setActivePreset("high_scholarship");
                  setShowCustomSliders(false);
                }}
                className={cn(
                  "rounded-lg px-3 py-1.5 text-xs font-semibold transition-all cursor-pointer",
                  activePreset === "high_scholarship"
                    ? "bg-brand text-white shadow-xs"
                    : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
                )}
              >
                Học bổng nhiều
              </button>
              <button
                type="button"
                onClick={() => {
                  setActivePreset("reputation");
                  setShowCustomSliders(false);
                }}
                className={cn(
                  "rounded-lg px-3 py-1.5 text-xs font-semibold transition-all cursor-pointer",
                  activePreset === "reputation"
                    ? "bg-brand text-white shadow-xs"
                    : "bg-muted text-muted-foreground hover:bg-muted/80 hover:text-foreground"
                )}
              >
                Danh tiếng
              </button>
              <button
                type="button"
                onClick={() => {
                  setShowCustomSliders(!showCustomSliders);
                  setActivePreset("custom");
                }}
                className={cn(
                  "rounded-lg border px-3 py-1.5 text-xs font-semibold transition-all cursor-pointer flex items-center gap-1",
                  activePreset === "custom"
                    ? "border-brand bg-brand-wash text-brand font-bold"
                    : "border-line text-muted-foreground hover:border-brand/40 hover:text-foreground"
                )}
              >
                <SlidersHorizontalIcon className="size-3" />
                {showCustomSliders ? "Ẩn chỉnh mức" : "Chỉnh mức"}
              </button>
            </div>
          </div>

          {/* Expanded Custom Weights Sliders */}
          {showCustomSliders && (
            <div className="rounded-xl border border-line bg-ground/50 p-4 space-y-3 animate-in fade-in-50 duration-150">
              <div className="text-xs font-semibold text-foreground">
                Bạn đang ở chế độ Tùy chỉnh mức ưu tiên:
              </div>
              <div className="grid gap-3 sm:grid-cols-3 text-xs">
                {/* Chi phí */}
                <div className="rounded-lg bg-surface p-3 border border-line space-y-2">
                  <div className="font-semibold text-foreground">Chi phí</div>
                  <div className="flex gap-1">
                    {([1, 2, 3] as InterestLevel[]).map((lvl) => (
                      <button
                        key={lvl}
                        type="button"
                        onClick={() => {
                          setCustomCost(lvl);
                          setActivePreset("custom");
                        }}
                        className={cn(
                          "flex-1 rounded py-1 text-[11px] font-medium transition-colors cursor-pointer",
                          customCost === lvl
                            ? "bg-brand text-white font-semibold"
                            : "bg-muted text-muted-foreground hover:bg-muted/80"
                        )}
                      >
                        {lvl === 1 ? "Ít" : lvl === 2 ? "Vừa" : "Rất cao"}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Học bổng */}
                <div className="rounded-lg bg-surface p-3 border border-line space-y-2">
                  <div className="font-semibold text-foreground">Học bổng</div>
                  <div className="flex gap-1">
                    {([1, 2, 3] as InterestLevel[]).map((lvl) => (
                      <button
                        key={lvl}
                        type="button"
                        onClick={() => {
                          setCustomScholarship(lvl);
                          setActivePreset("custom");
                        }}
                        className={cn(
                          "flex-1 rounded py-1 text-[11px] font-medium transition-colors cursor-pointer",
                          customScholarship === lvl
                            ? "bg-brand text-white font-semibold"
                            : "bg-muted text-muted-foreground hover:bg-muted/80"
                        )}
                      >
                        {lvl === 1 ? "Ít" : lvl === 2 ? "Vừa" : "Rất cao"}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Chất lượng / Danh tiếng */}
                <div className="rounded-lg bg-surface p-3 border border-line space-y-2">
                  <div className="font-semibold text-foreground">Chất lượng đào tạo</div>
                  <div className="flex gap-1">
                    {([1, 2, 3] as InterestLevel[]).map((lvl) => (
                      <button
                        key={lvl}
                        type="button"
                        onClick={() => {
                          setCustomQuality(lvl);
                          setActivePreset("custom");
                        }}
                        className={cn(
                          "flex-1 rounded py-1 text-[11px] font-medium transition-colors cursor-pointer",
                          customQuality === lvl
                            ? "bg-brand text-white font-semibold"
                            : "bg-muted text-muted-foreground hover:bg-muted/80"
                        )}
                      >
                        {lvl === 1 ? "Ít" : lvl === 2 ? "Vừa" : "Rất cao"}
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Main Two-Column Layout */}
      <div className="grid gap-8 lg:grid-cols-12 items-start">
        {/* Left Column (8 cols): 4 School Tiers Table Cards */}
        <div className="lg:col-span-8 space-y-6">
          {running && (
            <Alert className="border-brand/30 bg-brand-wash/30">
              <SparklesIcon className="size-4 text-brand" />
              <AlertTitle className="text-brand font-semibold text-sm">Đang phân tích và gợi ý trường...</AlertTitle>
              <AlertDescription className="text-xs text-muted-foreground">
                Hệ thống đang chạy giải thuật AHP & SAW và AI đang viết nhận xét chi tiết cho từng trường.
                Vui lòng không đóng trang.
              </AlertDescription>
            </Alert>
          )}

          {noProfile && (
            <Alert className="border-warn/40 bg-warn-wash/40">
              <TriangleAlertIcon className="size-4 text-warn" />
              <AlertTitle className="text-warn font-semibold text-sm">Chưa có hồ sơ học sinh</AlertTitle>
              <AlertDescription className="text-xs text-muted-foreground">
                Bạn chưa hoàn thiện hồ sơ. Hãy{" "}
                <Link href="/analysis" className="font-semibold text-brand underline">
                  vào Luồng phân tích hồ sơ 6 bước
                </Link>{" "}
                để tải bảng điểm và nhận kết quả tức thì.
              </AlertDescription>
            </Alert>
          )}

          {error && (
            <Alert variant="destructive">
              <AlertCircleIcon className="size-4" />
              <AlertTitle>Lỗi phân tích</AlertTitle>
              <AlertDescription className="text-xs">{error}</AlertDescription>
            </Alert>
          )}

          {/* Render 4 Groups (Khó vào, Vừa sức, An toàn, Cần học bổng lớn) */}
          {result &&
            TIERS.map((tier) => {
              const items = tieredItems[tier.key] ?? [];
              if (items.length === 0) return null;

              return (
                <Card key={tier.key} className="overflow-hidden shadow-xs border-line">
                  <CardHeader className="bg-ground/50 border-b border-line/60 pb-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <CardTitle className="text-h3 text-foreground font-bold">
                          {tier.label}
                        </CardTitle>
                        <Badge variant={tier.badgeVariant} className="text-[11px] font-semibold">
                          {tier.badgeText}
                        </Badge>
                      </div>
                      <Badge variant="outline" className="text-xs font-semibold">
                        {items.length} trường
                      </Badge>
                    </div>
                    <CardDescription className="text-xs text-muted-foreground mt-0.5">
                      {tier.hint}
                    </CardDescription>
                  </CardHeader>

                  <Table>
                    <TableHeader className="bg-surface">
                      <TableRow className="text-xs">
                        <TableHead className="w-10 text-center">#</TableHead>
                        <TableHead>Trường</TableHead>
                        <TableHead className="text-right">Chi phí/năm</TableHead>
                        <TableHead className="text-center">Độ phù hợp</TableHead>
                        <TableHead className="w-24 text-right">Chi tiết</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody className="text-xs divide-y divide-line">
                      {items.map((item, idx) => {
                        const isExpanded = expandedId === item.offeringId;
                        return (
                          <Fragment key={item.offeringId}>
                            <TableRow className="hover:bg-muted/30 transition-colors">
                              <TableCell className="text-center font-bold text-muted-foreground">
                                {idx + 1}
                              </TableCell>
                              <TableCell className="align-top whitespace-normal">
                                <div className="font-semibold text-foreground text-sm leading-tight">
                                  {item.name}
                                </div>
                                <div className="text-body-s mt-0.5 text-muted-foreground flex items-center gap-2">
                                  <span>{[item.school?.city, item.state].filter(Boolean).join(", ") || "Mỹ"}</span>
                                  <span>•</span>
                                  <span>
                                    {item.school?.control === "public"
                                      ? "Công lập"
                                      : item.school?.control === "private"
                                      ? "Tư thục"
                                      : "Đại học"}
                                  </span>
                                </div>
                                {item.categoryReason && (
                                  <div className="text-xs text-muted-foreground/90 mt-1 line-clamp-1 italic">
                                    {item.categoryReason}
                                  </div>
                                )}
                              </TableCell>
                              <TableCell className="text-right font-medium text-foreground align-top">
                                {item.costUnknown || item.totalCostUsd == null ? (
                                  <span className="text-muted-foreground">Chưa công bố</span>
                                ) : (
                                  usd(item.totalCostUsd)
                                )}
                              </TableCell>
                              <TableCell className="text-center align-top">
                                <span className="inline-flex items-center justify-center rounded-full bg-brand-wash px-2.5 py-0.5 text-xs font-bold text-brand">
                                  {item.fitScore}%
                                </span>
                              </TableCell>
                              <TableCell className="text-right align-top">
                                <Button
                                  type="button"
                                  variant="ghost"
                                  size="xs"
                                  onClick={() => setExpandedId(isExpanded ? null : item.offeringId)}
                                  className="text-brand hover:text-brand-ink text-xs gap-1"
                                >
                                  {isExpanded ? "Thu gọn" : "Chi tiết"}
                                  {isExpanded ? (
                                    <ChevronUpIcon className="size-3.5" />
                                  ) : (
                                    <ChevronDownIcon className="size-3.5" />
                                  )}
                                </Button>
                              </TableCell>
                            </TableRow>

                            {/* Expanded Details Row */}
                            {isExpanded && (
                              <TableRow className="bg-ground/30">
                                <TableCell />
                                <TableCell colSpan={4} className="whitespace-normal p-4">
                                  <SchoolDetailsExtended item={item} />
                                </TableCell>
                              </TableRow>
                            )}
                          </Fragment>
                        );
                      })}
                    </TableBody>
                  </Table>
                </Card>
              );
            })}
        </div>

        {/* Right Column (4 cols): BÁO CÁO CỦA BẠN Sidebar */}
        <aside className="lg:col-span-4 space-y-6 sticky top-20 print:hidden">
          <Card className="rounded-2xl border border-line bg-surface p-6 shadow-xs space-y-5">
            <div>
              <div className="text-label text-xs uppercase tracking-wider text-muted-foreground font-semibold">
                BÁO CÁO CỦA BẠN
              </div>
              <h3 className="text-h3 text-foreground font-bold mt-1">
                Xuất & In danh sách trường
              </h3>
              <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                Báo cáo gồm hồ sơ năng lực của bạn và danh sách các trường được tối ưu theo cách ưu tiên bạn đang chọn.
              </p>
            </div>

            <div className="space-y-2.5 pt-1">
              <Button
                type="button"
                variant="outline"
                onClick={handlePrint}
                className="w-full justify-center gap-2 text-xs font-semibold h-10"
              >
                <PrinterIcon className="size-4" />
                In báo cáo / Tải PDF
              </Button>

              <Button
                type="button"
                onClick={handleRun}
                disabled={running}
                variant="secondary"
                className="w-full justify-center gap-2 text-xs font-semibold h-10"
              >
                {running ? <Spinner /> : <RefreshCwIcon className="size-4" />}
                Lọc lại theo hồ sơ
              </Button>

              <Link
                href="/analysis"
                className="inline-flex w-full items-center justify-center gap-1.5 rounded-lg border border-brand/30 bg-brand-wash/40 px-3 py-2.5 text-xs font-semibold text-brand hover:bg-brand-wash hover:text-brand-ink transition-colors"
              >
                <EditIcon className="size-3.5" />
                Sửa hồ sơ và gợi ý lại
              </Link>
            </div>
          </Card>

          {/* Student Profile Snapshot Card */}
          {student && (
            <Card className="rounded-2xl border border-line bg-surface p-5 text-xs space-y-3">
              <div className="font-semibold text-foreground text-sm flex items-center gap-1.5 border-b border-line pb-2">
                <FileTextIcon className="size-4 text-brand" />
                Tóm tắt hồ sơ của bạn
              </div>
              <div className="space-y-1.5 text-muted-foreground">
                <div className="flex justify-between">
                  <span>GPA Thang 4.0:</span>
                  <strong className="text-foreground">{student.gpa4 ? `${student.gpa4.toFixed(2)} / 4.0` : "–"}</strong>
                </div>
                <div className="flex justify-between">
                  <span>SAT Composite:</span>
                  <strong className="text-foreground">{student.sat ?? "–"}</strong>
                </div>
                <div className="flex justify-between">
                  <span>Tiếng Anh:</span>
                  <strong className="text-foreground">
                    {student.ielts ? `IELTS ${student.ielts}` : student.toefl ? `TOEFL ${student.toefl}` : "–"}
                  </strong>
                </div>
                <div className="flex justify-between">
                  <span>Ngoại khóa:</span>
                  <strong className="text-foreground">{ecScore ? `${ecScore} / 4.0` : "–"}</strong>
                </div>
                <div className="flex justify-between">
                  <span>Ngân sách / năm:</span>
                  <strong className="text-foreground">
                    {student.annualBudgetUsd ? usd(student.annualBudgetUsd) : "–"}
                  </strong>
                </div>
              </div>
            </Card>
          )}
        </aside>
      </div>
    </div>
  );
}

// Chi tiết trường mở rộng
function SchoolDetailsExtended({ item }: { item: RecommendationItem }) {
  const s = item.school;
  const na = "Chưa công bố";
  const english = [
    s?.minIelts != null && `IELTS ${s.minIelts}`,
    s?.minToefl != null && `TOEFL ${s.minToefl}`,
    s?.minDuolingo != null && `Duolingo ${s.minDuolingo}`,
  ].filter(Boolean);

  const stats: [string, string][] = [
    ["GPA TB sinh viên", s?.avgGpa4 != null ? `${s.avgGpa4.toFixed(2)}/4.0` : na],
    [
      "SAT 25% – 75%",
      s?.sat25 != null && s?.sat75 != null
        ? `${s.sat25} – ${s.sat75}${s.satPolicy ? ` (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : ""}`
        : s?.satPolicy ? `${na} (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : na,
    ],
    ["Tiếng Anh tối thiểu", english.length > 0 ? english.join(" · ") : na],
    ["Học phí hàng năm", usd(s?.tuitionUsd) ?? na],
    ["Sinh hoạt phí", usd(s?.livingUsd) ?? na],
    ["Phí khác", usd(s?.feesUsd) ?? na],
    ["Tỷ lệ trúng tuyển", s?.acceptanceRate != null ? `${Math.round(s.acceptanceRate * 100)}%` : na],
    ["Sinh viên quốc tế", s?.internationalStudents != null ? s.internationalStudents.toLocaleString("vi-VN") : na],
  ];

  return (
    <div className="space-y-4 text-xs">
      {/* AI Explanation / Nhận xét 3 câu */}
      {item.reason && (
        <Alert className="border-brand/20 bg-brand-wash/30">
          <SparklesIcon className="size-4 text-brand" />
          <AlertTitle className="text-brand font-semibold text-xs">Đánh giá độ phù hợp</AlertTitle>
          <AlertDescription className="text-xs text-foreground/90 mt-1 leading-relaxed">
            {item.reason}
          </AlertDescription>
        </Alert>
      )}

      {/* Strengths & Weaknesses */}
      {(item.strengths?.length || item.weaknesses?.length) && (
        <div className="grid gap-3 sm:grid-cols-2 pt-1">
          {!!item.strengths?.length && (
            <div className="rounded-lg bg-ok-wash/30 border border-ok/20 p-3 space-y-1">
              <div className="text-[11px] font-bold text-ok uppercase tracking-wider">Ưu điểm</div>
              <ul className="space-y-1 text-xs">
                {item.strengths.map((st, i) => (
                  <li key={i} className="flex items-start gap-1.5">
                    <CheckIcon className="size-3.5 text-ok shrink-0 mt-0.5" />
                    <span>{st}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {!!item.weaknesses?.length && (
            <div className="rounded-lg bg-risk-wash/30 border border-risk/20 p-3 space-y-1">
              <div className="text-[11px] font-bold text-risk uppercase tracking-wider">Điểm cần lưu ý</div>
              <ul className="space-y-1 text-xs">
                {item.weaknesses.map((w, i) => (
                  <li key={i} className="flex items-start gap-1.5">
                    <XIcon className="size-3.5 text-risk shrink-0 mt-0.5" />
                    <span>{w}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Stats Grid */}
      <dl className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1 border-t border-line">
        {stats.map(([k, v]) => (
          <div key={k} className="rounded-lg bg-surface border border-line p-2">
            <dt className="text-muted-foreground text-[10px]">{k}</dt>
            <dd className={v === na ? "text-muted-foreground text-xs" : "font-semibold text-xs text-foreground"}>
              {v}
            </dd>
          </div>
        ))}
      </dl>

      {/* School Website Link */}
      {s?.website && (
        <div className="pt-1">
          <a
            href={s.website.startsWith("http") ? s.website : `https://${s.website}`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-xs text-brand hover:underline font-semibold"
          >
            Trang web trường: {s.website}
            <ExternalLinkIcon className="size-3.5" />
          </a>
        </div>
      )}
    </div>
  );
}
