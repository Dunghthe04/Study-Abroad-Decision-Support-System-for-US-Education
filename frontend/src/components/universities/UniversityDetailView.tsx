"use client";

import { useEffect, useMemo, useState } from "react";
import { ArrowUpRightIcon, Building2Icon, GlobeIcon, GraduationCapIcon, MapPinIcon, UsersIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AspectRatio } from "@/components/ui/aspect-ratio";
import { Card, CardContent } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { cn } from "cn";
import { studyLevelLabel } from "@/lib/study-levels";
import { campusSettingLabel, schoolControlLabel, universityNumber, universityUrl } from "@/lib/university-display";
import type { UniversityDetail } from "@/types/university";
import { DetailSection, GaugeDataItem, GridDataItem, SchoolImage, SourceLink } from "./UniversityDetailParts";
import { UniversityAdmissions, UniversityCampusLife, UniversityCosts, UniversityMajors, UniversityStatistics } from "./UniversityOfferingSections";
import { UniversitySources } from "./UniversitySources";
import { AddTargetSchoolButton } from "@/components/target-schools/AddTargetSchoolButton";

function formatRank(display: string | null | undefined, rank: number): string {
  if (display) {
    const cleaned = display.replace(/\s*\(tie\)/i, "").trim();
    if (cleaned) return cleaned;
  }
  return `#${rank}`;
}

function getRankColorClass(rank: number): string {
  if (rank <= 3) return "text-brand font-black text-xl";
  if (rank <= 10) return "text-brand font-bold text-lg";
  if (rank <= 50) return "text-ink font-bold text-lg";
  return "text-ink-3 font-semibold text-lg";
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
      { id: "sources", label: "Nguồn dữ liệu" },
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

  const overviewItems = [
    location ? { icon: MapPinIcon, label: "Vị trí", value: location } : null,
    university.control ? { icon: Building2Icon, label: "Loại trường", value: schoolControlLabel(university.control) } : null,
    offering?.campusSetting ? { icon: Building2Icon, label: "Bối cảnh khuôn viên", value: campusSettingLabel(offering.campusSetting) } : null,
    offering?.studentFacultyRatio != null ? { icon: UsersIcon, label: "Tỷ lệ SV / giảng viên", value: `${offering.studentFacultyRatio} : 1` } : null,
    university.internationalStudentCount != null ? { icon: UsersIcon, label: "Sinh viên quốc tế", value: universityNumber(university.internationalStudentCount) } : null,
    university.offerings.length ? {
      icon: GraduationCapIcon,
      label: "Bậc học có dữ liệu",
      value: university.offerings.map((item) => studyLevelLabel(item.studyLevel)).join(", "),
    } : null,
  ].filter((item): item is NonNullable<typeof item> => Boolean(item));

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
          <div className="mx-auto flex w-full max-w-[1320px] flex-wrap items-center justify-between gap-5 px-4 pb-6 sm:px-6">
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

      <div className="mx-auto max-w-[1320px] px-4 sm:px-6 space-y-6">
        {/* Nền đặc và tràn ra cả phần lề hai bên, để viền thẻ bên dưới không lộ ra khi cuộn.
            Cuộn ngang bằng overflow gốc, ẩn thanh cuộn (ScrollArea luôn vẽ thêm thanh dọc) */}
        <nav aria-label="Các mục thông tin trường" className="sticky top-(--header-h) z-20 -mx-4 bg-background px-4 sm:-mx-6 sm:px-6">
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
        <div className="min-w-0 space-y-5">
          <DetailSection id="overview" title="Tổng quan">
            <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
              {university.acceptanceRate != null && (
                <GaugeDataItem
                  label="Tỷ lệ nhận chung"
                  value={university.acceptanceRate}
                  subtitle="Tỷ lệ trúng tuyển toàn trường"
                />
              )}
              {overviewItems.map((item) => (
                <GridDataItem
                  key={item.label}
                  icon={item.icon}
                  label={item.label}
                  value={item.value}
                />
              ))}
            </div>
            {website && <div className="mt-5 space-y-4"><Separator /><SourceLink url={website} /></div>}
          </DetailSection>

          {offering ? (
            <div key={offering.studyLevel} className="space-y-5">
              <UniversityCosts offering={offering} />
              <UniversityAdmissions offering={offering} />
              <UniversityCampusLife offering={offering} />
              <UniversityStatistics offering={offering} internationalStudentCount={university.internationalStudentCount} />
              <UniversityMajors offering={offering} />
            </div>
          ) : (
            <DetailSection title="Thông tin tuyển sinh & chi phí"><p className="text-sm leading-relaxed text-muted-foreground">Trường chưa có dữ liệu bậc học trong hệ thống. Bạn có thể tham khảo website chính thức của trường.</p></DetailSection>
          )}

          {university.galleryUrls.length > 0 && (
            <DetailSection title="Hình ảnh trường">
              <div className="grid grid-cols-2 gap-3">
                {university.galleryUrls.map((url, index) => <a key={`${url}-${index}`} href={universityUrl(url) ?? undefined} target="_blank" rel="noopener noreferrer" aria-label={`Mở ảnh ${index + 1} của ${university.name}`}><AspectRatio ratio={4 / 3}><SchoolImage url={url} alt={`Ảnh ${index + 1} của ${university.name}`} className="h-full w-full rounded-md bg-muted" /></AspectRatio></a>)}
              </div>
            </DetailSection>
          )}
          <UniversitySources sources={university.sources} />
        </div>

        <aside aria-label="Thông tin bổ sung" className="space-y-5 lg:sticky lg:top-[calc(var(--header-h)+4.5rem)]">
          {university.rankings.length > 0 && (
            <DetailSection title="Xếp hạng đã ghi nhận">
              <ul className="divide-y divide-border/70">
                {university.rankings.map((ranking) => {
                  const safeSourceUrl = universityUrl(ranking.sourceUrl);
                  return (
                    <li key={`${ranking.publisher}-${ranking.name}-${ranking.edition}`} className="py-3.5 first:pt-0 last:pb-0">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0 flex-1 space-y-1">
                          {safeSourceUrl ? (
                            <a
                              href={safeSourceUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="text-sm font-medium leading-snug text-foreground transition-colors hover:text-brand hover:underline"
                            >
                              {ranking.name}
                            </a>
                          ) : (
                            <p className="text-sm font-medium leading-snug text-foreground">{ranking.name}</p>
                          )}
                          <p className="text-xs text-muted-foreground">
                            {ranking.publisher} · {ranking.edition ?? ranking.editionYear ?? "Chưa có năm"}
                          </p>
                        </div>
                        <span className={cn("shrink-0 tracking-tight", getRankColorClass(ranking.rank))}>
                          {formatRank(ranking.rankDisplay, ranking.rank)}
                        </span>
                      </div>
                    </li>
                  );
                })}
              </ul>
            </DetailSection>
          )}
        </aside>
      </div>
    </div>
  </article>
  );
}
