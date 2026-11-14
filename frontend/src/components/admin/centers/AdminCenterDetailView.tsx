"use client";

import { formatRating } from "@/lib/rating";
import { useState } from "react";
import { useCenterReviews } from "@/hooks/use-center-reviews";
import { CenterReviewList } from "@/components/centers/CenterReviewList";
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
  EyeIcon,
  EyeOffIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { studyCentersApi, ApiError } from "@/lib/api";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerServiceLabel } from "@/lib/center-services";
import { studyLevelLabel } from "@/lib/study-levels";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import {
  CenterActionConfirmDialog,
  type CenterConfirmAction,
} from "@/components/admin/centers/CenterActionConfirmDialog";
import { CenterStatusBadge } from "@/components/centers/CenterBadges";
import { EditCenterButton } from "@/components/admin/centers/EditCenterButton";
import { CenterLegalInfoCard } from "@/components/centers/CenterLegalInfoCard";

interface AdminCenterDetailViewProps {
  center: StudyCenter;
}

export function AdminCenterDetailView({ center: initialCenter }: AdminCenterDetailViewProps) {
  const [updatedCenter, setUpdatedCenter] = useState<StudyCenter | null>(null);
  const [confirmAction, setConfirmAction] = useState<CenterConfirmAction | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const center = updatedCenter ?? initialCenter;
  const profile = getEnrichedCenterProfile(center);
  // Đánh giá thật từ backend (không dùng dữ liệu mẫu)
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
  const isActive = center.isActive !== false;

  const handleConfirmAction = async (verificationNote?: string) => {
    if (!confirmAction) return;
    setIsSubmitting(true);

    try {
      if (confirmAction === "verify" || confirmAction === "unverify") {
        const targetVerified = confirmAction === "verify";
        const updated = await studyCentersApi.verify(center.id, targetVerified, verificationNote);
        setUpdatedCenter({
          ...center,
          ...updated,
          verificationStatus: targetVerified ? "verified" : "unverified",
          verifiedAt: targetVerified ? (updated.verifiedAt ?? new Date().toISOString()) : null,
          verifiedByUserId: targetVerified ? updated.verifiedByUserId : null,
        });

        toast.add({
          type: "success",
          title: targetVerified ? "Đã xác minh trung tâm" : "Đã hủy xác minh trung tâm",
          description: targetVerified
            ? `Trung tâm ${center.name} đã được xác minh thành công.`
            : `Đã hủy trạng thái xác minh của ${center.name}.`,
        });
      } else if (confirmAction === "hide" || confirmAction === "show") {
        const targetActive = confirmAction === "show";
        const updated = await studyCentersApi.setActiveStatus(center.id, targetActive);
        setUpdatedCenter({
          ...center,
          ...updated,
          isActive: targetActive,
        });

        toast.add({
          type: "success",
          title: targetActive ? "Đã hiện trung tâm" : "Đã ẩn trung tâm",
          description: targetActive
            ? `Trung tâm ${center.name} đã được hiển thị lại trên danh bạ và luồng gợi ý.`
            : `Trung tâm ${center.name} đã được ẩn khỏi danh bạ và luồng gợi ý.`,
        });
      }

      setConfirmAction(null);
    } catch (err: unknown) {
      const msg = err instanceof ApiError ? err.message : "Thao tác không thành công.";
      toast.add({
        type: "error",
        title: "Thao tác thất bại",
        description: msg,
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="flex-1 min-h-0 w-full overflow-y-auto space-y-6 pr-1 pb-16">
      {/* Admin Action Bar & Breadcrumbs */}
      <div className="flex flex-wrap items-center justify-between gap-3 p-3.5 rounded-xl border bg-muted/40">
        <div className="flex items-center gap-2 text-sm">
          <Link
            href="/admin/centers"
            className="inline-flex items-center gap-1.5 text-muted-foreground hover:text-foreground font-medium transition-colors"
          >
            <ArrowLeftIcon className="w-4 h-4" />
            Danh sách trung tâm
          </Link>
          <span className="text-muted-foreground">/</span>
          <span className="text-foreground font-semibold truncate max-w-[280px]">
            {center.name}
          </span>
          <Badge variant="outline" className="text-xs font-mono ml-1">
            {center.code}
          </Badge>
          {!isActive && (
            <Badge variant="risk" className="text-xs px-2 py-0.5 ml-1">
              Đã ẩn khỏi gợi ý
            </Badge>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {/* Nút Sửa thông tin trung tâm (UC-72) */}
          <EditCenterButton
            center={center}
            onSuccess={(updated) => setUpdatedCenter(updated)}
            className="h-8 gap-1.5 text-xs"
          />

          {/* Nút Ẩn / Hiện trung tâm (UC-72) */}
          {isActive ? (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmAction("hide")}
              className="h-8 gap-1.5 text-xs text-ink-2 hover:text-risk hover:border-risk/30 hover:bg-risk-wash"
              title="Ẩn trung tâm khỏi luồng gợi ý và danh bạ"
            >
              <EyeOffIcon className="w-3.5 h-3.5" />
              Ẩn trung tâm
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setConfirmAction("show")}
              className="h-8 gap-1.5 text-xs text-ok border-ok/30 bg-ok-wash/60 hover:bg-ok-wash"
              title="Hiện lại trung tâm trên danh bạ và luồng gợi ý"
            >
              <EyeIcon className="w-3.5 h-3.5" />
              Hiện trung tâm
            </Button>
          )}

          {/* Trạng thái xác minh (UC-59): chỉ duyệt qua hàng đợi hồ sơ ở danh sách trung tâm */}
          <div className="flex items-center gap-2">
            <CenterStatusBadge status={center.verificationStatus} />
            {isVerified && center.verifiedAt && (
              <span className="hidden sm:inline-flex items-center gap-1 text-xs text-muted-foreground">
                <ClockIcon className="w-3 h-3" />
                {new Date(center.verifiedAt).toLocaleDateString("vi-VN")}
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Hero Banner Section */}
      <div className="relative rounded-xl overflow-hidden border border-border bg-card ">
        {/* Cover Background */}
        <div
          className="h-44 sm:h-52 w-full bg-cover bg-center relative bg-brand-ink"
          style={profile.coverImage ? { backgroundImage: `url(${profile.coverImage})` } : undefined}
        >
          <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
          <div className="absolute top-4 right-4 flex gap-2">
            <Badge
              variant="secondary"
              className="bg-background/90 text-foreground font-medium text-xs px-2.5 py-1 "
            >
              Mã: {center.code}
            </Badge>
          </div>
        </div>

        {/* Header Content Area */}
        <div className="px-6 pb-6 pt-0 relative">
          <div className="flex flex-col sm:flex-row items-start sm:items-end justify-between gap-4 -mt-12 sm:-mt-14">
            {/* Logo / Avatar */}
            <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-xl overflow-hidden border-4 border-background bg-card shrink-0 flex items-center justify-center">
              {profile.logoImage ? (
                <img
                  src={profile.logoImage}
                  alt={center.name}
                  className="w-full h-full object-cover"
                />
              ) : (
                <Building2Icon className="w-10 h-10 text-brand/40" />
              )}
            </div>

            {/* Kênh tư vấn USAS (Ẩn hotline & website để tránh liên hệ trực tiếp ngoài hệ thống) */}
            <div className="flex flex-wrap items-center gap-2 shrink-0 pb-1">
              <div className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-brand/20 bg-brand-wash text-xs font-medium text-brand-ink">
                <ShieldCheckIcon className="w-3.5 h-3.5 text-brand shrink-0" />
                <span>Tư vấn qua hệ thống USAS</span>
              </div>
            </div>
          </div>

          {/* Title & Info Block */}
          <div className="mt-4 space-y-2">
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="font-heading text-2xl sm:text-3xl font-bold tracking-tight text-ink">
                {center.name}
              </h1>
              <Badge variant="brand">
                {profile.typeLabel}
              </Badge>
            </div>

            <div className="flex flex-wrap items-center gap-2 text-xs sm:text-sm text-ink-2">
              {center.city && (
                <span className="inline-flex items-center gap-1 font-medium text-ink">
                  <MapPinIcon className="w-3.5 h-3.5 text-brand" />
                  {center.district ? `${center.district}, ${center.city}` : center.city}
                </span>
              )}
              <span>·</span>
              {reviewTotal > 0 && reviewAverage !== null ? (
                <div className="inline-flex items-center gap-1 text-gold font-semibold tabular-nums">
                  <StarIcon className="w-3.5 h-3.5 fill-current" />
                  <span>{formatRating(reviewAverage)}</span>
                  <span className="text-ink-2 font-normal">({reviewTotal} đánh giá)</span>
                </div>
              ) : (
                <span className="text-ink-3">Chưa có đánh giá</span>
              )}
              {profile.establishedYear != null && (
                <>
                  <span>·</span>
                  <span>Năm thành lập: <strong className="text-ink">{profile.establishedYear}</strong></span>
                </>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* 4 Key Highlight Cards */}
      {(profile.visaSuccessRate != null || profile.staffCount != null || profile.licenseNumber || profile.taxCode) && (
      <div className="grid grid-cols-1 sm:grid-cols-[repeat(auto-fit,minmax(14rem,1fr))] gap-3 sm:gap-3.5">
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
            <div className="text-base sm:text-lg font-bold tracking-tight text-ink truncate" title={profile.licenseNumber ?? undefined}>
              {profile.licenseNumber ?? `MST: ${profile.taxCode}`}
            </div>
            {[profile.licenseNumber && profile.taxCode ? `MST: ${profile.taxCode}` : null, profile.licenseIssuedBy].some(Boolean) && (
              <div className="text-xs text-ink-3 truncate mt-0.5" title={profile.licenseIssuedBy ?? undefined}>
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

          {/* Vị trí & Liên hệ */}
          <div className="grid sm:grid-cols-2 gap-4">
            <CenterLegalInfoCard profile={profile} mode="admin" />

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
                    Toàn bộ quy trình tiếp nhận tư vấn, đánh giá hồ sơ và đối soát dịch vụ được thực hiện tập trung qua hệ thống USAS để bảo đảm quyền lợi và bảo mật thông tin.
                  </span>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        {/* TAB 2: DỊCH VỤ & KHÓA HỌC */}
        <TabsContent value="dich-vu" className="space-y-6 pt-2">
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

          {/* Khóa đào tạo */}
        </TabsContent>

        {/* TAB 3: ĐÁNH GIÁ & NHẬN XÉT (dữ liệu thật) */}
        <TabsContent value="danh-gia" className="space-y-6 pt-2">
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

      {/* Confirmation Dialog for Verify / Unverify / Hide / Show */}
      <CenterActionConfirmDialog
        center={center}
        action={confirmAction}
        isSubmitting={isSubmitting}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleConfirmAction}
      />
    </div>
  );
}
