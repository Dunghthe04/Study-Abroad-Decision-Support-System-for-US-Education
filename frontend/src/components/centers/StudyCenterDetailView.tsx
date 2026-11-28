"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import {
  AlertTriangleIcon,
  ArrowLeftIcon,
  ArrowUpRightIcon,
  BadgeCheckIcon,
  CheckIcon,
  ClockIcon,
  FileCheckIcon,
  FileTextIcon,
  GlobeIcon,
  HelpCircleIcon,
  MapPinIcon,
  MessageCircleIcon,
  ShieldCheckIcon,
  StarIcon,
} from "lucide-react";
import { studyCentersApi } from "@/lib/api";
import type { CenterGuidanceCardsDto, CenterReviewsDto, StudyCenter } from "@/types/api";
import { useCenterReviews } from "@/hooks/use-center-reviews";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerNeedsTrainingLicense, formatLegalDate, LEGAL_COMPLIANCE_MODE_LABELS } from "@/lib/center-legal";
import { feeHeadline } from "@/lib/center-fee";
import { centerServiceLabel } from "@/lib/center-services";
import { formatRating } from "@/lib/rating";
import { STUDY_LEVELS, studyLevelLabel } from "@/lib/study-levels";
import { universityHost, universityUrl } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { TrustIcon } from "./CenterBadges";
import { DetailRows, DetailSection, Gauge, SchoolImage, type GaugeTone } from "@/components/universities/UniversityDetailParts";
import { CenterReviewForm } from "./CenterReviewForm";
import { CenterReviewList } from "./CenterReviewList";
import { ContactRequestDialog } from "./ContactRequestDialog";

interface StudyCenterDetailViewProps {
  center: StudyCenter;
}

// Các chặng của một bộ hồ sơ du học Mỹ, theo thứ tự học sinh đi qua; mỗi mã dịch vụ rơi vào chặng đầu tiên khớp
const JOURNEY: { key: string; label: string; codes: string[] }[] = [
  { key: "orient", label: "Định hướng & chọn trường", codes: ["CHON_TRUONG"] },
  { key: "test", label: "Luyện thi chuẩn hóa", codes: ["LUYEN_THI", "LUYEN_THI_SAT", "LUYEN_THI_IELTS", "CHUNG_CHI"] },
  { key: "profile", label: "Bài luận & hoạt động", codes: ["SUA_BAI_LUAN", "HOAT_DONG_NGOAI_KHOA"] },
  { key: "aid", label: "Học bổng & tài chính", codes: ["SAN_HOC_BONG", "HOC_BONG", "CHUNG_MINH_TAI_CHINH"] },
  { key: "apply", label: "Nộp hồ sơ", codes: ["NOP_HO_SO", "HO_SO"] },
  { key: "visa", label: "Visa & lên đường", codes: ["LUYEN_PHONG_VAN", "HO_SO_VISA", "VISA"] },
];

const TONE_TEXT: Record<GaugeTone, string> = {
  brand: "text-brand",
  ok: "text-ok",
  warn: "text-warn",
  reach: "text-reach",
};

function formatMedianHours(hours: number) {
  return hours < 1 ? "dưới 1 giờ" : hours < 24 ? `${hours.toFixed(1)} giờ` : `${(hours / 24).toFixed(1)} ngày`;
}

function Stars({ value, className }: { value: number; className?: string }) {
  const pct = Math.max(0, Math.min(100, (value / 5) * 100));
  const row = (filled: boolean) =>
    Array.from({ length: 5 }, (_, i) => (
      <StarIcon key={i} className={cn("size-4 shrink-0", filled ? "fill-current text-gold" : "text-line")} />
    ));
  return (
    <span className={cn("relative inline-flex", className)} aria-hidden="true">
      <span className="flex gap-0.5">{row(false)}</span>
      <span className="absolute inset-y-0 left-0 flex gap-0.5 overflow-hidden" style={{ width: `${pct}%` }}>
        {row(true)}
      </span>
    </span>
  );
}

// Chỉ vẽ phân bố khi đã có đủ mọi đánh giá; một trang 20 bản ghi không đại diện cho toàn bộ
function RatingDistribution({ reviews }: { reviews: CenterReviewsDto }) {
  const counts = [5, 4, 3, 2, 1].map((star) => ({
    star,
    count: reviews.items.filter((r) => Math.round(r.rating) === star).length,
  }));
  const max = Math.max(1, ...counts.map((c) => c.count));
  return (
    <ul className="w-full max-w-sm space-y-1.5" aria-label="Phân bố số sao">
      {counts.map(({ star, count }) => (
        <li key={star} className="flex items-center gap-3 text-xs tabular-nums">
          <span className="inline-flex w-7 items-center gap-0.5 text-ink-2">
            {star}
            <StarIcon className="size-3 fill-current text-gold" aria-hidden="true" />
          </span>
          <span className="relative h-2 flex-1 overflow-hidden rounded-full bg-line">
            <span className="absolute inset-y-0 left-0 rounded-full bg-gold" style={{ width: `${(count / max) * 100}%` }} />
          </span>
          <span className="w-6 text-right text-ink-3">{count}</span>
        </li>
      ))}
    </ul>
  );
}

function Panel({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("overflow-hidden rounded-xl border border-line bg-surface", className)}>{children}</div>;
}

export function StudyCenterDetailView({ center }: StudyCenterDetailViewProps) {
  const profile = getEnrichedCenterProfile(center);
  const [activeSection, setActiveSection] = useState("overview");

  // Đánh giá lấy từ dữ liệu thật (không dùng số liệu mẫu)
  const {
    reviews,
    loading: reviewsLoading,
    error: reviewsError,
    loadingMore: reviewsLoadingMore,
    loadMoreError: reviewsLoadMoreError,
    hasMore: reviewsHasMore,
    loadMore: loadMoreReviews,
    refresh: refreshReviews,
    total: reviewTotal,
    average: reviewAverage,
  } = useCenterReviews(center.id);

  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";
  const [guidanceCards, setGuidanceCards] = useState<CenterGuidanceCardsDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    studyCentersApi
      .getGuidanceCards()
      .then((res) => {
        if (!cancelled) setGuidanceCards(res);
      })
      .catch(() => {
        // Không có thẻ tri thức thì bỏ qua im lặng theo đặc tả UC-40
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Nút liên hệ ở thanh tên còn trong tầm nhìn thì cột phải ẩn nút của nó, và ngược lại
  const headerCtaRef = useRef<HTMLDivElement>(null);
  const [headerCtaVisible, setHeaderCtaVisible] = useState(true);
  useEffect(() => {
    const el = headerCtaRef.current;
    if (!el) return;
    const io = new IntersectionObserver(([entry]) => setHeaderCtaVisible(entry.isIntersecting), {
      rootMargin: "-72px 0px 0px 0px", // chừa thanh đầu trang cố định
    });
    io.observe(el);
    return () => io.disconnect();
  }, []);

  const fee = feeHeadline(center.feeMin, center.feeMax);
  const hasFeeSection = Boolean(center.feeDescription || guidanceCards?.askCard || guidanceCards?.warningCard);

  const navigation = useMemo(
    () => [
      { id: "overview", label: "Tổng quan" },
      { id: "services", label: "Dịch vụ" },
      ...(hasFeeSection ? [{ id: "fees", label: "Trước khi ký" }] : []),
      { id: "legal", label: "Pháp lý & địa chỉ" },
      { id: "reviews", label: `Đánh giá (${reviewTotal})` },
    ],
    [reviewTotal, hasFeeSection],
  );

  useEffect(() => {
    const handleScroll = () => {
      const scrollPosition = window.scrollY + 160;
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

  // Hành trình dịch vụ: chặng nào trung tâm đồng hành, mã nào ngoài các chặng thì gom vào "Dịch vụ khác"
  const journey = JOURNEY.map((stage) => ({
    ...stage,
    services: center.services.filter((code) => stage.codes.includes(code)),
  }));
  const coveredStages = journey.filter((s) => s.services.length > 0).length;
  const otherServices = center.services.filter((code) => !JOURNEY.some((s) => s.codes.includes(code)));
  const extraLevels = center.studyLevels.filter((lvl) => !(STUDY_LEVELS as readonly string[]).includes(lvl));

  const staff = profile.staffCount;
  const certified = profile.certifiedCounselorCount;

  const yearsActive = profile.establishedYear ? new Date().getFullYear() - profile.establishedYear : null;
  const areaLabel = center.district ? `${center.district}, ${center.city ?? "Việt Nam"}` : center.city;

  const overviewRows = [
    { label: "Loại hình", value: profile.typeLabel },
    profile.establishedYear
      ? {
          label: "Thành lập",
          value: yearsActive && yearsActive > 0 ? `${profile.establishedYear} · ${yearsActive} năm hoạt động` : String(profile.establishedYear),
        }
      : null,
    staff != null ? { label: "Quy mô nhân sự", value: `${staff} người` } : null,
    certified != null ? { label: "Tư vấn viên có chứng chỉ", value: `${certified} người` } : null,
    areaLabel ? { label: "Khu vực", value: areaLabel } : null,
    center.serviceAreas?.length ? { label: "Khu vực phục vụ", value: center.serviceAreas.join(", ") } : null,
  ].filter((row): row is { label: string; value: string } => Boolean(row));

  const hasEnoughResponseSample = (center.responseSampleCount ?? 0) >= 5 && center.responseRatePercent != null;
  const responseRate = hasEnoughResponseSample ? center.responseRatePercent! / 100 : null;
  const responseTone: GaugeTone =
    responseRate == null ? "brand" : responseRate >= 0.8 ? "ok" : responseRate >= 0.5 ? "warn" : "reach";

  const needsTrainingLicense = centerNeedsTrainingLicense(center.centerType);
  const issuedDate = formatLegalDate(profile.licenseIssuedDate);
  const legalRows = [
    { label: "Địa chỉ", value: center.address ?? areaLabel ?? "—" },
    { label: "Hình thức pháp lý", value: LEGAL_COMPLIANCE_MODE_LABELS[profile.legalComplianceMode] },
    {
      label: profile.legalComplianceMode === "notified" ? "Mã hồ sơ thông báo" : "Giấy phép tư vấn du học",
      value: profile.licenseNumber ?? "—",
    },
    ...(profile.trainingLicenseNumber || needsTrainingLicense
      ? [{ label: "Giấy phép đào tạo ngoại ngữ", value: profile.trainingLicenseNumber ?? "—" }]
      : []),
    {
      label: "Cơ quan cấp / tiếp nhận",
      value: profile.licenseIssuedBy ? `${profile.licenseIssuedBy}${issuedDate ? ` · ${issuedDate}` : ""}` : "—",
    },
    { label: "Mã số thuế / ĐKKD", value: profile.taxCode ?? "—" },
    { label: "Người đại diện pháp luật", value: profile.legalRepresentative ?? "—" },
    {
      label: "Xác minh trên USDSS",
      value: isVerified
        ? `Đã xác minh${center.verifiedAt ? ` · ${new Date(center.verifiedAt).toLocaleDateString("vi-VN")}` : ""}`
        : "Chờ xác minh",
    },
  ];

  const licenseOrTax = center.licenseNumber || center.taxCode;
  const paperLines = [
    profile.legalComplianceMode === "licensed" ? "Cấp phép Sở GD&ĐT" : "Thông báo hoạt động Sở GD",
    licenseOrTax ? `Giấy phép / MST: ${licenseOrTax}` : null,
  ].filter((line): line is string => Boolean(line));

  const mapsUrl =
    center.latitude && center.longitude
      ? `https://www.google.com/maps/search/?api=1&query=${center.latitude},${center.longitude}`
      : center.address
        ? `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${center.name} ${center.address}`)}`
        : null;

  // Trụ sở và mọi chi nhánh, mỗi nơi một liên kết mở bản đồ
  const mapsSearch = (query: string) => `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(query)}`;
  const locations: { label: string; url: string }[] = [
    ...(mapsUrl ? [{ label: center.address ?? areaLabel ?? "Xem trên bản đồ", url: mapsUrl }] : []),
    ...(center.branches ?? [])
      .filter((b) => b && b !== center.address)
      .map((b) => ({ label: b, url: mapsSearch(`${center.name} ${b}`) })),
  ];

  const website = universityUrl(center.website);
  const fanpage = universityUrl(center.fanpage);
  const gallery = profile.galleryImages.map((img) => img.url).filter((url) => universityUrl(url));
  const fullDistribution = reviews && reviewTotal > 0 && reviews.items.length === reviews.totalCount;
  const zoom = "[&_img]:transition-transform [&_img]:duration-500 [&_img]:ease-out group-hover:[&_img]:scale-[1.03]";

  return (
    <article className="@container space-y-6 pb-12">
      {/* Nút tròn nổi góc trái trên, mờ nhẹ, không hiệu ứng khi rê chuột, luôn hiện khi cuộn, đưa về danh bạ trung tâm */}
      <Link
        href="/centers"
        aria-label="Về danh bạ trung tâm"
        title="Danh bạ trung tâm"
        className="fixed top-[calc(var(--header-h)+0.75rem)] left-4 z-30 flex size-11 items-center justify-center rounded-full border border-line bg-surface text-ink opacity-70 shadow-md @2xl:left-8"
      >
        <ArrowLeftIcon className="size-5" aria-hidden="true" />
      </Link>

      {/* Ảnh bìa tràn mép dưới thanh đầu trong suốt giống trang chi tiết trường */}
      <header>
        <div className="relative -mt-(--header-h) h-[min(50svh,440px)] min-h-64 overflow-hidden bg-brand-ink">
          <SchoolImage url={profile.coverImage} alt={`Không gian ${center.name}`} className="size-full" priority />
          <div className="absolute inset-x-0 top-0 h-48 bg-linear-to-b from-brand-ink/80 to-transparent" />
        </div>

        <div className="border-b border-line bg-surface">
          <div className="mx-auto flex w-full max-w-[1560px] flex-wrap items-center justify-between gap-5 px-4 pb-6 @2xl:px-8 @5xl:px-12">
            <div className="flex min-w-0 items-end gap-4">
              <SchoolImage
                url={profile.logoImage}
                alt={`Logo ${center.name}`}
                className="-mt-8 size-20 shrink-0 rounded-xl border border-line bg-white @2xl:-mt-10 @2xl:size-24"
                contain
              />
              <div className="min-w-0 pt-4">
                <div className="flex flex-wrap items-center gap-2.5">
                  <h1 className="text-2xl leading-tight font-semibold tracking-tight text-balance text-ink @2xl:text-3xl">
                    {center.name}
                  </h1>
                  {/* Cùng hai dấu với thẻ trong danh bạ: xác minh và giấy tờ pháp lý */}
                  <span className="flex items-center gap-2">
                    <TrustIcon label={isVerified ? "Đã xác minh" : "Chờ xác minh"}>
                      {isVerified ? (
                        <BadgeCheckIcon className="size-6 text-ok" aria-hidden />
                      ) : (
                        <ClockIcon className="size-6 text-warn" aria-hidden />
                      )}
                    </TrustIcon>
                    <TrustIcon label={paperLines.join(". ")} lines={paperLines}>
                      {profile.legalComplianceMode === "licensed" ? (
                        <FileCheckIcon className="size-6 text-ok" aria-hidden />
                      ) : (
                        <FileTextIcon className="size-6 text-muted-foreground" aria-hidden />
                      )}
                    </TrustIcon>
                  </span>
                </div>
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-ink-2">
                  {areaLabel && (
                    <span className="inline-flex items-center gap-1 text-ink">
                      <MapPinIcon className="size-3.5 text-brand" aria-hidden="true" />
                      {areaLabel}
                    </span>
                  )}
                  {areaLabel && <span aria-hidden="true">·</span>}
                  {reviewTotal > 0 && reviewAverage !== null ? (
                    <a href="#reviews" className="inline-flex items-center gap-1 font-semibold text-gold tabular-nums hover:underline">
                      <StarIcon className="size-3.5 fill-current" aria-hidden="true" />
                      {formatRating(reviewAverage)}
                      <span className="font-normal text-ink-2">({reviewTotal} đánh giá)</span>
                    </a>
                  ) : (
                    <span className="text-ink-3">Chưa có đánh giá</span>
                  )}
                </div>
              </div>
            </div>

            <div ref={headerCtaRef} className="flex flex-wrap items-center gap-3 pt-4">
              <ContactRequestDialog centerId={center.id} centerName={center.name} isVerified={isVerified} />
            </div>
          </div>
        </div>
      </header>

      <div className="mx-auto max-w-[1560px] space-y-6 px-4 @2xl:px-8 @5xl:px-12">
        <nav
          aria-label="Các mục thông tin trung tâm"
          className="sticky top-(--header-h) z-20 -mx-4 bg-background px-4 @2xl:-mx-8 @2xl:px-8 @5xl:-mx-12 @5xl:px-12"
        >
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
                      "-mb-px inline-flex items-center justify-center border-b-2 px-4 py-3 text-sm font-medium outline-none transition-colors",
                      isActive
                        ? "border-brand font-semibold text-brand"
                        : "border-transparent text-muted-foreground hover:border-brand/40 hover:text-foreground",
                    )}
                  >
                    {item.label}
                  </a>
                );
              })}
            </div>
          </div>
        </nav>

        <div className="grid items-start gap-6 @5xl:grid-cols-[minmax(0,1fr)_280px] @7xl:grid-cols-[minmax(0,1fr)_320px]">
          <div className="min-w-0 space-y-12">
            {/* TỔNG QUAN: mô tả, tỷ lệ tư vấn viên có chứng chỉ (vẽ như tỷ lệ nhận của trường), các dòng hồ sơ */}
            <DetailSection id="overview" title="Tổng quan">
              <Panel>
                <p className={cn("max-w-[70ch] p-5 text-sm leading-relaxed @2xl:p-6", center.description ? "text-ink-2" : "text-ink-3")}>
                  {center.description || "Trung tâm chưa cập nhật mô tả chi tiết."}
                </p>

                {/* Hai câu học sinh hỏi đầu tiên: tốn bao nhiêu, và trung tâm lo được bao nhiêu phần của bộ hồ sơ */}
                <div className="grid border-t border-line @2xl:grid-cols-2">
                  <div className="space-y-1 p-5 @2xl:p-6">
                    <p className="text-base font-medium text-ink">Mức phí tham khảo</p>
                    <p className="text-sm text-ink-2">Do trung tâm công bố, chưa phải báo giá</p>
                    {fee ? (
                      <p className="pt-2 text-4xl font-semibold tracking-tight text-brand tabular-nums @2xl:text-5xl">
                        {fee}
                        <span className="ml-1.5 text-xl font-semibold">₫</span>
                      </p>
                    ) : (
                      <p className="pt-2 text-base font-semibold text-ink-3">Trung tâm chưa công bố</p>
                    )}
                  </div>
                  <a
                    href="#services"
                    className="group space-y-1 border-t border-line p-5 transition-colors hover:bg-muted/40 @2xl:border-t-0 @2xl:border-l @2xl:p-6"
                  >
                    <p className="text-base font-medium text-ink">Đồng hành hồ sơ</p>
                    <p className="text-sm text-ink-2">Số chặng của bộ hồ sơ du học Mỹ trung tâm hỗ trợ</p>
                    <p className="flex items-baseline gap-1 pt-2 text-brand">
                      <span className="text-4xl font-semibold tracking-tight tabular-nums @2xl:text-5xl">{coveredStages}</span>
                      <span className="text-xl font-semibold text-ink-3">/ {JOURNEY.length} chặng</span>
                    </p>
                    <span className="flex gap-1 pt-2" aria-hidden="true">
                      {journey.map((s) => (
                        <span key={s.key} className={cn("h-1.5 flex-1 rounded-full", s.services.length > 0 ? "bg-brand" : "bg-line")} />
                      ))}
                    </span>
                  </a>
                </div>

                <div className="border-t border-line px-5 @2xl:px-6">
                  <DetailRows rows={overviewRows} />
                </div>
              </Panel>

              {profile.accreditations.length > 0 && (
                <div className="space-y-3">
                  <h3 className="text-sm font-semibold text-ink">Kiểm định &amp; đối tác quốc tế</h3>
                  <ul className="flex flex-wrap gap-2">
                    {profile.accreditations.map((acc) => (
                      <li
                        key={acc}
                        className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface py-2 pr-3.5 pl-2.5 text-sm font-medium text-ink"
                      >
                        <span className="flex size-6 items-center justify-center rounded-full bg-brand-wash text-brand">
                          <ShieldCheckIcon className="size-3.5" aria-hidden="true" />
                        </span>
                        {acc}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {center.branches && center.branches.length > 0 && (
                <div className="space-y-3">
                  <h3 className="text-sm font-semibold text-ink">Chi nhánh ({center.branches.length})</h3>
                  <ul className="grid gap-2 @3xl:grid-cols-2">
                    {center.branches.map((branch) => (
                      <li key={branch} className="flex items-start gap-2 text-sm text-ink-2">
                        <MapPinIcon className="mt-0.5 size-4 shrink-0 text-ink-3" aria-hidden="true" />
                        {branch}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </DetailSection>

            {/* DỊCH VỤ: hành trình hồ sơ, chặng nào trung tâm đồng hành được tô sáng */}
            <DetailSection
              id="services"
              title="Trung tâm đồng hành ở chặng nào"
              description={`Đi cùng ${coveredStages}/${JOURNEY.length} chặng của một bộ hồ sơ du học Mỹ, theo các dịch vụ trung tâm đăng ký.`}
            >
              <Panel className="p-5 @2xl:p-6">
                <ol className="grid gap-0 @3xl:grid-cols-6 @3xl:gap-3">
                  {journey.map((stage) => {
                    const covered = stage.services.length > 0;
                    return (
                      <li key={stage.key} className="relative flex gap-4 pb-6 last:pb-0 @3xl:block @3xl:pb-0">
                        <span
                          className={cn(
                            "relative z-10 flex size-7 shrink-0 items-center justify-center rounded-full",
                            covered ? "bg-brand text-white" : "border-2 border-dashed border-ink-3/50 bg-surface",
                          )}
                        >
                          {covered && <CheckIcon className="size-4" aria-hidden="true" />}
                        </span>
                        <div className="min-w-0 space-y-1.5 @3xl:mt-4">
                          <p className={cn("text-sm leading-snug font-semibold", covered ? "text-ink" : "text-ink-3")}>
                            {stage.label}
                            <span className="sr-only">{covered ? ": có hỗ trợ" : ": không có dịch vụ"}</span>
                          </p>
                          {covered ? (
                            <ul className="space-y-1">
                              {stage.services.map((code) => (
                                <li key={code} className="text-xs leading-snug text-ink-2">
                                  {centerServiceLabel(code)}
                                </li>
                              ))}
                            </ul>
                          ) : (
                            <p className="text-xs text-ink-3">Chưa hỗ trợ</p>
                          )}
                        </div>
                      </li>
                    );
                  })}
                </ol>

                {otherServices.length > 0 && (
                  <div className="mt-6 flex flex-wrap items-center gap-2 border-t border-line pt-5">
                    <span className="mr-1 text-xs font-medium text-ink-2">Dịch vụ khác</span>
                    {otherServices.map((code) => (
                      <Badge key={code} variant="secondary" className="py-1 text-xs">
                        {centerServiceLabel(code)}
                      </Badge>
                    ))}
                  </div>
                )}
              </Panel>

              {/* Bậc học: thang 5 nấc từ THCS/THPT đến Tiến sĩ, nấc trung tâm tư vấn được tô */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold text-ink">Bậc học trung tâm tư vấn</h3>
                <Panel className="p-5 @2xl:p-6">
                  <ol className="grid gap-2.5 @md:grid-cols-5 @md:gap-1.5" aria-label="Các bậc học">
                    {STUDY_LEVELS.map((lvl) => {
                      const on = center.studyLevels.includes(lvl);
                      return (
                        <li key={lvl} className="flex min-w-0 items-center gap-3 @md:block @md:space-y-2">
                          <span
                            aria-hidden="true"
                            className={cn("block h-2 w-10 shrink-0 rounded-full @md:w-auto", on ? "bg-brand" : "bg-line")}
                          />
                          <span className={cn("block text-xs leading-snug", on ? "font-semibold text-ink" : "text-ink-3")}>
                            {studyLevelLabel(lvl)}
                            <span className="sr-only">{on ? " (có tư vấn)" : " (không tư vấn)"}</span>
                          </span>
                        </li>
                      );
                    })}
                  </ol>
                  {extraLevels.length > 0 && (
                    <div className="mt-4 flex flex-wrap gap-1.5">
                      {extraLevels.map((lvl) => (
                        <Badge key={lvl} variant="secondary" className="py-1 text-xs">
                          {studyLevelLabel(lvl)}
                        </Badge>
                      ))}
                    </div>
                  )}
                </Panel>
              </div>
            </DetailSection>

            {/* CHI PHÍ & LƯU Ý: mức phí công bố đặt cạnh câu nên hỏi và dấu hiệu cảnh giác (UC-40) */}
            {hasFeeSection && (
              <DetailSection id="fees" title="Trước khi ký hợp đồng">
                {center.feeDescription && (
                  <Panel className="space-y-2 p-5 @2xl:p-6">
                    <h3 className="text-base font-medium text-ink">Phí gồm những gì</h3>
                    <p className="max-w-[75ch] text-sm leading-relaxed whitespace-pre-line text-ink-2">{center.feeDescription}</p>
                  </Panel>
                )}

                {(guidanceCards?.askCard || guidanceCards?.warningCard) && (
                  <div className="grid gap-4 @3xl:grid-cols-2">
                    {guidanceCards.askCard && (
                      <div className="rounded-xl bg-brand-wash p-5 @2xl:p-6">
                        <h3 className="flex items-center gap-2 text-base font-semibold text-brand-ink">
                          <HelpCircleIcon className="size-4 shrink-0 text-brand" aria-hidden="true" />
                          Nên hỏi trung tâm
                        </h3>
                        <p className="mt-3 text-sm leading-relaxed whitespace-pre-line text-brand-ink/80">
                          {guidanceCards.askCard.content}
                        </p>
                      </div>
                    )}
                    {guidanceCards.warningCard && (
                      <div className="rounded-xl bg-warn-wash p-5 @2xl:p-6">
                        <h3 className="flex items-center gap-2 text-base font-semibold text-warn">
                          <AlertTriangleIcon className="size-4 shrink-0" aria-hidden="true" />
                          Dấu hiệu cảnh giác
                        </h3>
                        <p className="mt-3 text-sm leading-relaxed whitespace-pre-line text-ink-2">
                          {guidanceCards.warningCard.content}
                        </p>
                      </div>
                    )}
                  </div>
                )}
              </DetailSection>
            )}

            {/* KHÔNG GIAN: ảnh đầu lớn, các ảnh sau xếp bên cạnh; không chú thích dưới ảnh */}
            {gallery.length > 0 && (
              <DetailSection title="Không gian trung tâm">
                <div
                  className={cn(
                    "grid gap-3",
                    gallery.length === 1 && "aspect-[21/9]",
                    gallery.length === 2 && "aspect-[2/1] grid-cols-2",
                    gallery.length >= 3 && "aspect-[16/9] grid-cols-3 grid-rows-2",
                  )}
                >
                  {gallery.slice(0, 3).map((url, i) => (
                    <a
                      key={`${url}-${i}`}
                      href={universityUrl(url) ?? undefined}
                      target="_blank"
                      rel="noopener noreferrer"
                      aria-label={`Mở ảnh ${i + 1} của ${center.name}`}
                      className={cn(
                        "group overflow-hidden rounded-xl bg-muted",
                        gallery.length >= 3 && i === 0 && "col-span-2 row-span-2",
                      )}
                    >
                      <SchoolImage url={url} alt={`Ảnh ${i + 1} của ${center.name}`} className={cn("size-full", zoom)} />
                    </a>
                  ))}
                </div>
                {gallery.length > 3 && (
                  <div className="grid grid-cols-3 gap-3">
                    {gallery.slice(3).map((url, i) => (
                      <a
                        key={`${url}-${i + 3}`}
                        href={universityUrl(url) ?? undefined}
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label={`Mở ảnh ${i + 4} của ${center.name}`}
                        className="group aspect-[4/3] overflow-hidden rounded-xl bg-muted"
                      >
                        <SchoolImage url={url} alt={`Ảnh ${i + 4} của ${center.name}`} className={cn("size-full", zoom)} />
                      </a>
                    ))}
                  </div>
                )}
              </DetailSection>
            )}

            {/* PHÁP LÝ & ĐỊA CHỈ */}
            <DetailSection id="legal" title="Pháp lý & địa chỉ">
              <div>
                <Panel className="px-5 @2xl:px-6">
                  <DetailRows rows={legalRows} />
                  {profile.legalDocumentUrls.length > 0 && (
                    <div className="space-y-2 border-t border-line py-4">
                      <p className="flex items-center gap-1.5 text-xs font-semibold text-ink">
                        <FileTextIcon className="size-3.5 text-brand" aria-hidden="true" />
                        Tài liệu pháp lý đính kèm ({profile.legalDocumentUrls.length})
                      </p>
                      <ul className="flex flex-wrap gap-x-4 gap-y-1">
                        {profile.legalDocumentUrls.map((url, i) => (
                          <li key={url}>
                            <a
                              href={universityUrl(url) ?? undefined}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-1 text-sm text-brand underline-offset-2 hover:underline"
                            >
                              Tài liệu {i + 1}
                              <ArrowUpRightIcon className="size-3.5" aria-hidden="true" />
                            </a>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </Panel>
              </div>
            </DetailSection>

            {/* ĐÁNH GIÁ */}
            <DetailSection id="reviews" title={`Đánh giá từ cộng đồng (${reviewTotal})`}>
              <Panel className="flex flex-col gap-6 p-5 @2xl:flex-row @2xl:items-center @2xl:justify-between @2xl:p-6">
                {reviewTotal > 0 && reviewAverage !== null ? (
                  <div className="space-y-1">
                    <p className="text-base font-medium text-ink">Điểm cộng đồng</p>
                    <div className="flex items-baseline gap-1.5 pt-1 text-gold">
                      <span className="text-5xl font-semibold tracking-tight tabular-nums @2xl:text-6xl">
                        {formatRating(reviewAverage)}
                      </span>
                      <span className="text-xl font-semibold text-ink-3">/ 5</span>
                    </div>
                    <Stars value={reviewAverage} />
                    <p className="pt-1 text-sm text-ink-2">Từ {reviewTotal} lượt đánh giá của học sinh và phụ huynh</p>
                  </div>
                ) : (
                  <div className="space-y-1">
                    <p className="text-base font-medium text-ink">Chưa có đánh giá</p>
                    <p className="text-sm text-ink-2">Trung tâm này chưa nhận lượt đánh giá nào từ học sinh hay phụ huynh.</p>
                  </div>
                )}
                {fullDistribution && <RatingDistribution reviews={reviews} />}
              </Panel>

              <CenterReviewForm centerId={center.id} onCreated={refreshReviews} />

              <CenterReviewList
                reviews={reviews}
                error={reviewsError}
                loading={reviewsLoading}
                hasMore={reviewsHasMore}
                loadingMore={reviewsLoadingMore}
                loadMoreError={reviewsLoadMoreError}
                onLoadMore={loadMoreReviews}
                centerId={center.id}
                onReviewRemoved={refreshReviews}
              />
            </DetailSection>
          </div>

          {/* CỘT PHẢI DÍNH: độ phản hồi đo trên hệ thống, liên hệ và kênh chính thức */}
          <aside
            aria-label="Phản hồi và liên hệ"
            className="space-y-5 @5xl:sticky @5xl:top-[calc(var(--header-h)+4.5rem)]"
          >
            <DetailSection title="Độ phản hồi">
              <Panel>
                <div className="flex items-center justify-between gap-4 p-5">
                  <div className="min-w-0 space-y-1">
                    <p className="text-xs text-ink-2">Trả lời yêu cầu trong 90 ngày</p>
                    {responseRate != null ? (
                      <>
                        <p className={cn("flex items-baseline gap-0.5", TONE_TEXT[responseTone])}>
                          <span className="text-4xl font-semibold tracking-tight tabular-nums">{center.responseRatePercent}</span>
                          <span className="text-lg font-semibold">%</span>
                        </p>
                        <p className="text-xs text-ink-3">
                          {center.medianResponseHours != null && `Trung vị ${formatMedianHours(center.medianResponseHours)} · `}
                          {center.responseSampleCount} yêu cầu
                        </p>
                      </>
                    ) : (
                      <>
                        <p className="text-base font-semibold text-ink-2">Chưa đủ dữ liệu</p>
                        <p className="text-xs text-ink-3">Cần tối thiểu 5 yêu cầu qua hệ thống</p>
                      </>
                    )}
                  </div>
                  {responseRate != null ? (
                    <Gauge value={responseRate} label="Tỷ lệ phản hồi" tone={responseTone} className="w-24" />
                  ) : (
                    <Gauge value={0} label="Chưa đủ dữ liệu phản hồi" className="w-24" />
                  )}
                </div>

                {/* Đầu trang nút nằm ở thanh tên; cuộn qua thì trượt xuống hiện trong cột phải */}
                <div
                  inert={headerCtaVisible}
                  className={cn(
                    "grid transition-[grid-template-rows,opacity] duration-300 ease-out",
                    headerCtaVisible ? "grid-rows-[0fr] opacity-0" : "grid-rows-[1fr] opacity-100",
                  )}
                >
                  <div className="min-h-0 overflow-hidden">
                    <div className="px-5 pb-5 [&_button]:w-full [&_button]:justify-start">
                      <ContactRequestDialog centerId={center.id} centerName={center.name} isVerified={isVerified} />
                    </div>
                  </div>
                </div>

                <ul className="space-y-2 border-t border-line p-5 text-sm">
                  {[
                    website && { href: website, icon: GlobeIcon, label: universityHost(website) },
                    fanpage && { href: fanpage, icon: MessageCircleIcon, label: "Fanpage chính thức" },
                    ...locations.map((loc) => ({ href: loc.url, icon: MapPinIcon, label: loc.label })),
                  ]
                    .filter((l): l is { href: string; icon: typeof GlobeIcon; label: string } => Boolean(l))
                    .map(({ href, icon: Icon, label }) => (
                      <li key={`${label}-${href}`}>
                        <a
                          href={href}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex max-w-full items-start gap-2 text-ink-2 hover:text-ink hover:underline underline-offset-2"
                        >
                          <Icon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                          <span className="min-w-0 break-words">{label}</span>
                        </a>
                      </li>
                    ))}
                </ul>

                <p className="flex items-start gap-2 border-t border-line bg-muted/40 px-5 py-3.5 text-xs leading-relaxed text-ink-2">
                  <ShieldCheckIcon className="mt-0.5 size-3.5 shrink-0 text-brand" aria-hidden="true" />
                  Yêu cầu gửi qua USDSS được lưu lại an toàn để bảo vệ bạn.
                </p>
              </Panel>
            </DetailSection>
          </aside>
        </div>
      </div>
    </article>
  );
}
