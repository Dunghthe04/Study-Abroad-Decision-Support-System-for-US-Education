"use client";

import { formatRating } from "@/lib/rating";
import Link from "next/link";
import {
  ArrowLeftIcon,
  CheckCircle2Icon,
  MapPinIcon,
  ClockIcon,
  StarIcon,
  ShieldCheckIcon,
  UsersIcon,
  GraduationCapIcon,
  Building2Icon,
  FlameIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { useCenterReviews } from "@/hooks/use-center-reviews";
import { CenterReviewList } from "./CenterReviewList";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerServiceLabel } from "@/lib/center-services";
import { studyLevelLabel } from "@/lib/study-levels";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { CenterLegalInfoCard } from "./CenterLegalInfoCard";
import { ContactRequestDialog } from "./ContactRequestDialog";

interface StudyCenterDetailViewProps {
  center: StudyCenter;
}

export function StudyCenterDetailView({ center }: StudyCenterDetailViewProps) {
  const profile = getEnrichedCenterProfile(center);

  // Đánh giá lấy từ dữ liệu thật (không dùng số liệu mẫu)
  const {
    reviews,
    loading: reviewsLoading,
    error: reviewsError,
    loadingMore: reviewsLoadingMore,
    loadMoreError: reviewsLoadMoreError,
    hasMore: reviewsHasMore,
    loadMore: loadMoreReviews,
    total: reviewTotal,
    average: reviewAverage,
  } = useCenterReviews(center.id);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";

  return (
    <article className="py-6 pb-16 space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-ink-3">
        <div className="flex items-center gap-2 min-w-0">
          <Link
            href="/centers"
            className="inline-flex items-center gap-1.5 font-medium transition-colors hover:text-brand"
          >
            <ArrowLeftIcon className="size-3.5" />
            Danh bạ trung tâm
          </Link>
          <span>/</span>
          <span className="truncate font-medium text-ink">{center.name}</span>
        </div>
        <Badge variant="neutral" className="font-mono text-xs">
          Mã: {center.code}
        </Badge>
      </div>

      {/* Hero Banner Card matching USAS design tokens */}
      <div className="overflow-hidden rounded-xl border border-line bg-surface">
        <div className="relative h-48 sm:h-56 w-full overflow-hidden bg-brand-ink">
          {profile.coverImage && (
            <div
              className="h-full w-full bg-cover bg-center"
              style={{ backgroundImage: `url(${profile.coverImage})` }}
            />
          )}
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/55 via-black/15 to-transparent" />
        </div>

        <div className="px-6 pb-6 pt-3">
          <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-end min-w-0">
              <div className="relative -mt-12 sm:-mt-16 flex size-20 sm:size-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border-4 border-surface bg-surface">
                {profile.logoImage ? (
                  <img
                    src={profile.logoImage}
                    alt={center.name}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <Building2Icon className="size-8 text-brand/40" />
                )}
              </div>

              <div className="min-w-0 space-y-2 pb-0.5">
                <div className="flex flex-wrap items-center gap-2.5">
                  <h1 className="font-heading text-2xl font-bold tracking-tight text-ink sm:text-3xl">
                    {center.name}
                  </h1>
                  {isVerified ? (
                    <Badge variant="ok" className="gap-1 px-2.5 py-0.5 text-xs">
                      <CheckCircle2Icon className="size-3.5" />
                      Đã xác minh Sở GD&amp;ĐT
                    </Badge>
                  ) : (
                    <Badge variant="warn" className="px-2.5 py-0.5 text-xs">
                      Chờ xác minh
                    </Badge>
                  )}
                </div>

                <div className="flex flex-wrap items-center gap-x-2.5 gap-y-1 text-xs text-ink-2 sm:text-sm">
                  <Badge variant="brand">{profile.typeLabel}</Badge>
                  {center.city && (
                    <span className="inline-flex items-center gap-1 font-medium text-ink">
                      <MapPinIcon className="size-3.5 text-brand" />
                      {center.city}
                    </span>
                  )}
                  <span aria-hidden="true">·</span>
                  {reviewTotal > 0 && reviewAverage !== null ? (
                    <div className="inline-flex items-center gap-1 font-semibold text-gold tabular-nums">
                      <StarIcon className="size-3.5 fill-current" />
                      <span>{formatRating(reviewAverage)}</span>
                      <span className="font-normal text-ink-2">({reviewTotal} đánh giá)</span>
                    </div>
                  ) : (
                    <span className="text-ink-3">Chưa có đánh giá</span>
                  )}
                </div>
              </div>
            </div>

            <div className="flex shrink-0 flex-wrap items-center gap-2 pb-1">
              <ContactRequestDialog centerId={center.id} centerName={center.name} isVerified={isVerified} />
            </div>
          </div>
        </div>
      </div>

      {/* 4 Key Highlight Cards */}
      {(profile.visaSuccessRate != null || profile.staffCount != null || profile.licenseNumber || profile.taxCode) && (
      <div className="grid grid-cols-1 sm:grid-cols-[repeat(auto-fit,minmax(14rem,1fr))] gap-4">
        {profile.visaSuccessRate != null && (
        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0 border-line">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-ink-2 font-medium truncate">
              Tỷ lệ đỗ Visa Mỹ
            </span>
            <div className="w-8 h-8 rounded-lg bg-ok-wash text-ok flex items-center justify-center shrink-0">
              <FlameIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="font-heading text-xl sm:text-2xl font-bold tracking-tight text-ink tabular-nums truncate">
              {profile.visaSuccessRate}%
            </div>
            <div className="text-xs text-ink-3 truncate mt-0.5">
              Do trung tâm công bố
            </div>
          </div>
        </Card>
        )}

        {profile.staffCount != null && (
        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0 border-line">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-ink-2 font-medium truncate">
              Quy mô nhân sự
            </span>
            <div className="w-8 h-8 rounded-lg bg-brand-wash text-brand-ink flex items-center justify-center shrink-0">
              <UsersIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="font-heading text-xl sm:text-2xl font-bold tracking-tight text-ink tabular-nums truncate">
              {profile.staffCount}{" "}
              <span className="font-sans text-xs sm:text-sm font-normal text-ink-2">chuyên viên</span>
            </div>
            <div className="text-xs text-ink-3 truncate mt-0.5">
              Do trung tâm công bố
            </div>
          </div>
        </Card>
        )}

        {(profile.licenseNumber || profile.taxCode) && (
        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0 border-line">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-ink-2 font-medium truncate">
              Pháp lý cơ sở
            </span>
            <div className="w-8 h-8 rounded-lg bg-reach-wash text-reach flex items-center justify-center shrink-0">
              <ShieldCheckIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="text-base sm:text-lg font-bold tracking-tight text-foreground truncate" title={profile.licenseNumber ?? undefined}>
              {profile.licenseNumber ?? `MST: ${profile.taxCode}`}
            </div>
            {[profile.licenseNumber && profile.taxCode ? `MST: ${profile.taxCode}` : null, profile.licenseIssuedBy].some(Boolean) && (
              <div className="text-xs text-muted-foreground truncate mt-0.5" title={profile.licenseIssuedBy ?? undefined}>
                {[profile.licenseNumber && profile.taxCode ? `MST: ${profile.taxCode}` : null, profile.licenseIssuedBy]
                  .filter(Boolean)
                  .join(" · ")}
              </div>
            )}
          </div>
        </Card>
        )}
      </div>
      )}

      {/* Main Tabs Navigation */}
      <Tabs defaultValue="tong-quan" className="space-y-4">
        <TabsList className="grid w-full grid-cols-3 max-w-xl h-11 p-1 rounded-xl border-2 border-border/90 bg-muted/90 ">
          <TabsTrigger value="tong-quan" className="text-xs sm:text-sm font-medium">Tổng quan & Cơ sở</TabsTrigger>
          <TabsTrigger value="dich-vu" className="text-xs sm:text-sm font-medium">Dịch vụ</TabsTrigger>
          <TabsTrigger value="danh-gia" className="text-xs sm:text-sm font-medium">Đánh giá ({reviewTotal})</TabsTrigger>
        </TabsList>

        {/* TAB 1: TỔNG QUAN & CƠ SỞ VẬT CHẤT */}
        <TabsContent value="tong-quan" className="space-y-6 pt-2">
          {/* Giới thiệu */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg flex items-center gap-2">
                <Building2Icon className="w-5 h-5 text-brand" />
                Giới thiệu về trung tâm
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm leading-relaxed text-muted-foreground">
              {center.description && <p>{center.description}</p>}

              {/* Chứng nhận quốc tế */}
              {profile.accreditations.length > 0 && (
                <div className="pt-2 border-t">
                  <div className="text-xs font-semibold text-foreground mb-2">
                    Chứng nhận kiểm định & Đối tác quốc tế:
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {profile.accreditations.map((acc) => (
                      <Badge key={acc} variant="outline" className="bg-muted/40 font-normal text-xs py-1">
                        <ShieldCheckIcon className="w-3.5 h-3.5 text-brand mr-1" />
                        {acc}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          {/* Gallery hình ảnh thực tế */}
          {profile.galleryImages.length > 0 && (
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg">Không gian & Cơ sở vật chất</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                {profile.galleryImages.map((img, i) => (
                  <div key={i} className="group relative rounded-lg overflow-hidden border bg-muted aspect-video">
                    <img
                      src={img.url}
                      alt={img.caption}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                    />
                    <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-transparent flex items-end p-2.5">
                      <span className="text-white text-xs font-medium leading-tight">{img.caption}</span>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
          )}

          {/* Vị trí & Liên hệ */}
          <div className="grid sm:grid-cols-2 gap-4">
            <CenterLegalInfoCard profile={profile} mode="public" showMapsLink />

            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base flex items-center gap-2">
                  <ClockIcon className="w-4 h-4 text-brand" />
                  Kênh tiếp nhận
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div className="text-xs text-muted-foreground flex items-start gap-2 bg-muted/40 p-2.5 rounded-lg">
                  <ShieldCheckIcon className="w-4 h-4 text-brand shrink-0 mt-0.5" />
                  <span>
                    Toàn bộ quy trình tiếp nhận tư vấn và đăng ký dịch vụ du học được hỗ trợ và bảo trợ tập trung qua hệ thống USAS để đảm bảo tính xác thực và quyền lợi của người dùng.
                  </span>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* TAB 2: DỊCH VỤ & KHÓA HỌC */}
        <TabsContent value="dich-vu" className="space-y-6 pt-2">
          {/* Nhóm Dịch vụ Du học */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg flex items-center gap-2">
                <GraduationCapIcon className="w-5 h-5 text-brand" />
                Các Dịch vụ Hồ sơ Du học Mỹ
              </CardTitle>
              <CardDescription className="text-xs">
                Các gói dịch vụ chuyên môn được xác thực trên hệ thống USAS
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid sm:grid-cols-2 gap-3">
                {center.services.map((svc) => (
                  <div key={svc} className="p-3.5 rounded-lg border bg-card/60 flex items-start gap-3">
                    <div className="w-8 h-8 rounded-md bg-brand/10 text-brand flex items-center justify-center shrink-0 mt-0.5">
                      <CheckCircle2Icon className="w-4 h-4" />
                    </div>
                    <div className="min-w-0">
                      <div className="text-sm font-semibold text-foreground">{centerServiceLabel(svc)}</div>
                      <div className="text-xs text-muted-foreground mt-0.5">
                        {svc === "CHON_TRUONG" && "Định vị danh sách trường Reach - Target - Safety theo ngân sách và năng lực."}
                        {svc === "SAN_HOC_BONG" && "Xây dựng chiến lược tối ưu gói hỗ trợ tài chính Need-based và Merit-based."}
                        {svc === "SUA_BAI_LUAN" && "Chấm chữa bài luận cá nhân Common App & SOP cùng cố vấn chuyên môn."}
                        {svc === "LUYEN_PHONG_VAN" && "Luyện phản xạ phỏng vấn visa F-1 bám sát luật di trú và tính trung thực."}
                        {svc === "LUYEN_THI" && "Đào tạo cấp tốc chứng chỉ tiếng Anh và chuẩn hóa học thuật."}
                        {!["CHON_TRUONG", "SAN_HOC_BONG", "SUA_BAI_LUAN", "LUYEN_PHONG_VAN", "LUYEN_THI"].includes(svc) &&
                          "Hỗ trợ chuyên môn theo yêu cầu của học sinh."}
                      </div>
                    </div>
                  </div>
                ))}
              </div>

              {/* Bậc học hỗ trợ */}
              {center.studyLevels.length > 0 && (
                <div className="pt-3 border-t">
                  <div className="text-xs font-semibold text-foreground mb-2">Bậc học trung tâm tư vấn:</div>
                  <div className="flex flex-wrap gap-1.5">
                    {center.studyLevels.map((lvl) => (
                      <Badge key={lvl} variant="secondary" className="text-xs py-1">
                        {studyLevelLabel(lvl)}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

        </TabsContent>

        {/* TAB 3: ĐÁNH GIÁ & NHẬN XÉT (dữ liệu thật từ học sinh / phụ huynh) */}
        <TabsContent value="danh-gia" className="space-y-6 pt-2">
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="text-lg">Điểm đánh giá cộng đồng</CardTitle>
                  <CardDescription className="text-xs">
                    {reviewTotal > 0
                      ? `Dựa trên ${reviewTotal} lượt đánh giá từ học sinh và phụ huynh`
                      : "Chưa có lượt đánh giá nào cho trung tâm này"}
                  </CardDescription>
                </div>
                {reviewTotal > 0 && reviewAverage !== null && (
                  <div className="text-right">
                    <div className="font-heading text-2xl font-bold text-ink flex items-center gap-1 tabular-nums">
                      <StarIcon className="w-5 h-5 text-gold fill-current" />
                      {formatRating(reviewAverage)} / 5.0
                    </div>
                  </div>
                )}
              </div>
            </CardHeader>
          </Card>

          <CenterReviewList
            reviews={reviews}
            error={reviewsError}
            loading={reviewsLoading}
            hasMore={reviewsHasMore}
            loadingMore={reviewsLoadingMore}
            loadMoreError={reviewsLoadMoreError}
            onLoadMore={loadMoreReviews}
          />
        </TabsContent>
      </Tabs>
    </article>
  );
}
