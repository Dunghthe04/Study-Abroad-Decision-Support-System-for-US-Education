"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import { ArrowUpRightIcon, Building2Icon, GlobeIcon, HomeIcon, MapPinIcon, TreesIcon, WarehouseIcon, type LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AspectRatio } from "@/components/ui/aspect-ratio";
import { Card, CardContent } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { cn } from "cn";
import { studyLevelLabel } from "@/lib/study-levels";
import { campusSettingLabel, schoolControlLabel, universityNumber, universityUrl } from "@/lib/university-display";
import type { UniversityDetail } from "@/types/university";
import { acceptanceRateTone, DetailSection, Gauge, SchoolImage } from "./UniversityDetailParts";
import { UniversityAdmissions, UniversityCampusLife, UniversityCosts, UniversityMajors, UniversityStatistics } from "./UniversityOfferingSections";
import { findFieldSource, SourceTooltip } from "./UniversitySourceTooltip";
import { AddTargetSchoolButton } from "@/components/target-schools/AddTargetSchoolButton";

function cleanRankNumber(display: string | null | undefined, rank: number): string {
  if (display) {
    const cleaned = display.replace(/\s*\(tie\)/i, "").replace(/^#/, "").trim();
    if (cleaned) return cleaned;
  }
  return String(rank);
}

function rankTone(rank: number): string {
  if (rank <= 3) return "text-risk";
  if (rank <= 10) return "text-warn";
  if (rank <= 25) return "text-gold";
  return "text-ink";
}

function rankTextSize(rankStr: string): string {
  const digits = rankStr.replace(/\D/g, "").length;
  if (digits >= 4) return "text-2xl";
  if (digits === 3) return "text-3xl";
  return "text-4xl";
}

function campusSettingIcon(value: string): LucideIcon {
  if (value.startsWith("City")) return Building2Icon;
  if (value.startsWith("Suburban")) return HomeIcon;
  if (value.startsWith("Town")) return WarehouseIcon;
  if (value.startsWith("Rural")) return TreesIcon;
  return MapPinIcon;
}

export function UniversityDetailView({ university }: { university: UniversityDetail }) {
  const [selectedLevel, setSelectedLevel] = useState(university.offerings[0]?.studyLevel ?? "");
  const [activeSection, setActiveSection] = useState("overview");
  const offering = university.offerings.find((item) => item.studyLevel === selectedLevel) ?? university.offerings[0];
  const location = [university.city, university.stateName ?? university.state].filter(Boolean).join(", ");
  const website = universityUrl(university.website);

  const hasCampusData = Boolean(
    offering && (
      offering.fyOnCampusPct != null ||
      offering.ugOnCampusPct != null ||
      offering.housingTypes ||
      offering.housingOnlyUsd != null ||
      offering.activities
    )
  );

  const navigation = useMemo(
    () => [
      { id: "overview", label: "Tổng quan" },
      ...(offering
        ? [
            { id: "costs", label: "Chi phí & hỗ trợ" },
            { id: "admissions", label: "Tuyển sinh" },
            ...(hasCampusData ? [{ id: "campus", label: "Khuôn viên & nhà ở" }] : []),
            { id: "statistics", label: "Thống kê" },
            { id: "majors", label: "Ngành học" },
          ]
        : []),
    ],
    [offering, hasCampusData],
  );

  useEffect(() => {
    const handleScroll = () => {
      const scrollPosition = window.scrollY + 140;
      for (let i = navigation.length - 1; i >= 0; i--) {
        const item = navigation[i];
        const element = document.getElementById(item.id);
        if (element && element.offsetTop <= scrollPosition) {
          setActiveSection(item.id);
          break;
        }
      }
    };
    window.addEventListener("scroll", handleScroll, { passive: true });
    handleScroll();
    return () => window.removeEventListener("scroll", handleScroll);
  }, [navigation]);

  const overviewRows: { label: string; value: ReactNode; icon?: LucideIcon }[] = [
    offering?.campusSetting ? { label: "Bối cảnh khuôn viên", value: campusSettingLabel(offering.campusSetting), icon: campusSettingIcon(offering.campusSetting) } : null,
    university.internationalStudentCount != null ? { label: "Sinh viên quốc tế", value: <SourceTooltip source={findFieldSource(university.sources, "international_student_count")}>{universityNumber(university.internationalStudentCount)}</SourceTooltip> } : null,
  ].filter((item): item is NonNullable<typeof item> => Boolean(item));

  const acceptanceTone = university.acceptanceRate != null ? acceptanceRateTone(university.acceptanceRate) : "brand";
  const acceptanceToneClass = {
    reach: "text-reach",
    warn: "text-warn",
    ok: "text-ok",
    brand: "text-brand",
  }[acceptanceTone];
  const acceptancePct = university.acceptanceRate != null ? (university.acceptanceRate > 1 ? university.acceptanceRate : university.acceptanceRate * 100) : null;
  const acceptanceDisplayNumber = acceptancePct != null ? universityNumber(Number(acceptancePct.toFixed(1))) : null;

  return (
    <article className="space-y-6 pb-12">
      {/* Ảnh bìa lớn tràn mép, nằm dưới thanh đầu trong suốt giống trang chủ */}
      <header>
        <div className="relative -mt-(--header-h) h-[min(60svh,520px)] min-h-72 overflow-hidden bg-brand-ink">
          <SchoolImage url={university.coverUrl} alt={`Khuôn viên ${university.name}`} className="size-full" priority />
          {/* Chỉ làm tối phần trên để chữ thanh đầu đọc được */}
          <div className="absolute inset-x-0 top-0 h-48 bg-linear-to-b from-brand-ink/80 to-transparent" />
        </div>

        {/* Dưới ảnh: logo đè nhẹ lên mép ảnh, tên và vị trí bên trái, các nút bên phải canh giữa theo khối tên */}
        <div className="border-b border-line bg-surface">
          <div className="mx-auto flex w-full max-w-[1560px] flex-wrap items-center justify-between gap-5 px-4 pb-6 sm:px-8 lg:px-12">
            <div className="flex min-w-0 items-end gap-4">
              <SchoolImage url={university.logoUrl} alt={`Logo ${university.name}`} className="-mt-8 size-20 shrink-0 rounded-xl border border-line bg-white sm:-mt-10 sm:size-24" contain />
              <div className="min-w-0 pt-4">
                <h1 className="text-2xl leading-tight font-semibold tracking-tight text-balance text-ink sm:text-3xl">{university.name}</h1>
                {/* Vị trí · loại trường, ngăn cách bằng dấu chấm giữa */}
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-ink-2">
                  {[location, university.control && schoolControlLabel(university.control)].filter(Boolean).join(" · ")}
                  {university.isIvy && <Badge variant="secondary">Ivy League</Badge>}
                </div>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3 pt-4">
              <AddTargetSchoolButton
                universityCode={university.code}
                universityName={university.name}
                universityCity={university.city}
                universityState={university.state}
                size="lg"
                className="!border-transparent !bg-brand-wash !text-brand-ink hover:!bg-brand-wash/70"
              />
              {website && (
                <Button
                  size="lg"
                  nativeButton={false}
                  role="link"
                  render={<a href={website} target="_blank" rel="noopener noreferrer" />}
                >
                  <GlobeIcon />Website trường<ArrowUpRightIcon />
                </Button>
              )}
            </div>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-[1560px] px-4 sm:px-8 lg:px-12 space-y-6">
        {/* Nền đặc và tràn ra cả phần lề hai bên, để viền thẻ bên dưới không lộ ra khi cuộn.
            Cuộn ngang bằng overflow gốc, ẩn thanh cuộn (ScrollArea luôn vẽ thêm thanh dọc) */}
        <nav aria-label="Các mục thông tin trường" className="sticky top-(--header-h) z-20 -mx-4 sm:-mx-8 lg:-mx-12 bg-background px-4 sm:px-8 lg:px-12">
          <div className="overflow-x-auto border-b border-border [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
            <div className="flex w-max gap-1">
              {navigation.map((item) => {
                const isActive = activeSection === item.id;
                return (
                  <a
                    key={item.id}
                    href={`#${item.id}`}
                    onClick={() => setActiveSection(item.id)}
                    className={cn(
                      "inline-flex items-center justify-center px-4 py-3 text-sm font-medium transition-colors border-b-2 -mb-px outline-none",
                      isActive
                        ? "border-brand text-brand font-semibold"
                        : "border-transparent text-muted-foreground hover:text-foreground hover:border-brand/40"
                    )}
                  >
                    {item.label}
                  </a>
                );
              })}
            </div>
          </div>
        </nav>

      {university.offerings.length > 1 && (
        <Card size="sm">
          <CardContent className="flex flex-wrap items-center gap-3">
            <Label htmlFor="university-study-level">Bậc học</Label>
            <Select value={selectedLevel} onValueChange={(value) => { if (value !== null) setSelectedLevel(value); }} items={university.offerings.map((item) => ({ value: item.studyLevel, label: studyLevelLabel(item.studyLevel) }))}>
              <SelectTrigger id="university-study-level"><SelectValue /></SelectTrigger>
              <SelectContent>
                {university.offerings.map((item) => <SelectItem key={item.studyLevel} value={item.studyLevel}>{studyLevelLabel(item.studyLevel)}</SelectItem>)}
              </SelectContent>
            </Select>
            {offering?.academicYear && <span className="text-xs text-muted-foreground">Năm chi phí: {offering.academicYear}</span>}
          </CardContent>
        </Card>
      )}

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_280px] xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="min-w-0 space-y-12">
          <DetailSection id="overview" title="Tổng quan">
            <div className="overflow-hidden rounded-xl border border-line bg-surface">
              {university.acceptanceRate != null && (
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-6 p-5 sm:p-6">
                  <div className="space-y-1">
                    <p className="text-base font-medium text-ink">Tỷ lệ nhận chung</p>
                    <p className="text-sm text-ink-2">Tỷ lệ trúng tuyển toàn trường</p>
                    <div className="pt-2 flex items-baseline gap-1">
                      <span className={cn("text-5xl sm:text-6xl font-semibold tracking-tight tabular-nums", acceptanceToneClass)}>
                        <SourceTooltip source={findFieldSource(university.sources, "acceptance_rate")}>
                          {acceptanceDisplayNumber}
                        </SourceTooltip>
                      </span>
                      <span className={cn("text-2xl font-semibold", acceptanceToneClass)}>%</span>
                    </div>
                  </div>
                  <div className="shrink-0 self-center sm:self-auto">
                    <Gauge
                      value={university.acceptanceRate}
                      label="Tỷ lệ nhận chung"
                      tone={acceptanceTone}
                      className="w-48 sm:w-56 md:w-60"
                    />
                  </div>
                </div>
              )}

              {overviewRows.length > 0 && (
                <div className={cn("divide-y divide-line", university.acceptanceRate != null && "border-t border-line")}>
                  {overviewRows.map((row) => (
                    <div key={row.label} className="flex items-center justify-between px-5 py-3.5 sm:px-6">
                      <span className="text-sm text-ink-2">{row.label}</span>
                      <span className="inline-flex items-center gap-2 text-sm font-semibold text-ink tabular-nums">
                        {row.icon && <row.icon className="size-4 text-ink-2" aria-hidden="true" />}
                        {row.value}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </DetailSection>

          {offering ? (
            <div key={offering.studyLevel} className="space-y-12">
              <UniversityCosts offering={offering} sources={university.sources} />
              <UniversityAdmissions offering={offering} sources={university.sources} />
              <UniversityCampusLife offering={offering} sources={university.sources} />
              <UniversityStatistics offering={offering} internationalStudentCount={university.internationalStudentCount} sources={university.sources} />
              <UniversityMajors offering={offering} sources={university.sources} />
            </div>
          ) : (
            <DetailSection title="Thông tin tuyển sinh & chi phí">
              <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
                <p className="text-sm leading-relaxed text-ink-2">
                  Trường chưa có dữ liệu bậc học trong hệ thống. Bạn có thể tham khảo website chính thức của trường.
                </p>
              </div>
            </DetailSection>
          )}

          {university.galleryUrls.length > 0 && (
            <DetailSection title="Hình ảnh trường">
              <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
                <div className="grid grid-cols-2 gap-3">
                  {university.galleryUrls.map((url, index) => (
                    <a key={`${url}-${index}`} href={universityUrl(url) ?? undefined} target="_blank" rel="noopener noreferrer" aria-label={`Mở ảnh ${index + 1} của ${university.name}`}>
                      <AspectRatio ratio={4 / 3}>
                        <SchoolImage url={url} alt={`Ảnh ${index + 1} của ${university.name}`} className="h-full w-full rounded-md bg-muted" />
                      </AspectRatio>
                    </a>
                  ))}
                </div>
              </div>
            </DetailSection>
          )}
        </div>

        <aside aria-label="Thông tin bổ sung" className="space-y-5 lg:sticky lg:top-[calc(var(--header-h)+4.5rem)]">
          {university.rankings.length > 0 && (
            <DetailSection title="Xếp hạng đã ghi nhận">
              <div className="overflow-hidden rounded-xl border border-line bg-surface">
                <ul>
                  {university.rankings.map((ranking) => {
                    const safeSourceUrl = universityUrl(ranking.sourceUrl);
                    const numStr = cleanRankNumber(ranking.rankDisplay, ranking.rank);
                    return (
                      <li key={`${ranking.publisher}-${ranking.name}-${ranking.edition}`} className="group flex items-center gap-4 pl-4 sm:pl-5">
                        <div className="flex shrink-0 items-baseline gap-0.5 font-heading font-semibold tabular-nums">
                          <span className="text-sm font-normal opacity-70">#</span>
                          <span className={cn("leading-none", rankTone(ranking.rank), rankTextSize(numStr))}>
                            {numStr}
                          </span>
                        </div>
                        <div className="min-w-0 flex-1 py-3.5 pr-4 sm:pr-5 border-b border-line/70 group-last:border-b-0 space-y-0.5">
                          {safeSourceUrl ? (
                            <a
                              href={safeSourceUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="line-clamp-2 text-sm font-medium leading-snug text-ink transition-colors hover:text-brand hover:underline"
                            >
                              {ranking.name}
                            </a>
                          ) : (
                            <p className="line-clamp-2 text-sm font-medium leading-snug text-ink">{ranking.name}</p>
                          )}
                          <p className="text-xs text-ink-3">
                            {ranking.publisher} · {ranking.edition ?? ranking.editionYear ?? "Chưa có năm"}
                          </p>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              </div>
            </DetailSection>
          )}
        </aside>
      </div>
      </div>
    </article>
  );
}
