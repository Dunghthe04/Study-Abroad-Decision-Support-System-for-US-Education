"use client";

import { useState } from "react";
import Link from "next/link";
import {
  ArrowLeftIcon,
  CheckCircle2Icon,
  MapPinIcon,
  ClockIcon,
  StarIcon,
  ShieldCheckIcon,
  ShieldAlertIcon,
  AwardIcon,
  UsersIcon,
  GraduationCapIcon,
  BookOpenIcon,
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "@/components/ui/toast";
import { CreateCenterAccountButton } from "@/components/admin/centers/CreateCenterAccountButton";
import { EditCenterButton } from "@/components/admin/centers/EditCenterButton";

interface AdminCenterDetailViewProps {
  center: StudyCenter;
}

function parseScholarshipStat(raw?: string): { amount: string; note?: string } {
  if (!raw) return { amount: "—" };
  const trimmed = raw.trim();
  const match = trimmed.match(/^(.*?)\s*\((.*?)\)$/);
  if (match) {
    return {
      amount: match[1].trim(),
      note: match[2].trim(),
    };
  }
  return { amount: trimmed };
}

export function AdminCenterDetailView({ center: initialCenter }: AdminCenterDetailViewProps) {
  const [updatedCenter, setUpdatedCenter] = useState<StudyCenter | null>(null);
  const [confirmAction, setConfirmAction] = useState<"verify" | "unverify" | "hide" | "show" | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const center = updatedCenter ?? initialCenter;
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";
  const isActive = center.isActive !== false;
  const scholarshipStat = parseScholarshipStat(profile.totalScholarshipsWon);

  const handleConfirmAction = async () => {
    if (!confirmAction) return;
    setIsSubmitting(true);

    try {
      if (confirmAction === "verify" || confirmAction === "unverify") {
        const targetVerified = confirmAction === "verify";
        const updated = await studyCentersApi.verify(center.id, targetVerified);
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
            <Badge variant="outline" className="text-rose-600 border-rose-300 bg-rose-50 dark:bg-rose-950/40 text-xs px-2 py-0.5 ml-1">
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
              className="h-8 gap-1.5 text-xs text-muted-foreground hover:text-rose-600 hover:border-rose-200 hover:bg-rose-50 dark:hover:bg-rose-950/30"
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
              className="h-8 gap-1.5 text-xs text-emerald-700 border-emerald-300 bg-emerald-50/50 hover:bg-emerald-100/60 dark:bg-emerald-950/30 dark:hover:bg-emerald-950/60"
              title="Hiện lại trung tâm trên danh bạ và luồng gợi ý"
            >
              <EyeIcon className="w-3.5 h-3.5" />
              Hiện trung tâm
            </Button>
          )}

          {/* Trạng thái xác minh và Nút Xác minh / Hủy xác minh (UC-59) */}
          {isVerified ? (
            <div className="flex items-center gap-2">
              <Badge className="bg-emerald-600 text-white gap-1 text-xs px-2.5 py-1">
                <CheckCircle2Icon className="w-3.5 h-3.5" />
                Đã xác minh Sở GD&ĐT
              </Badge>
              {center.verifiedAt && (
                <span className="hidden sm:inline-flex items-center gap-1 text-xs text-muted-foreground">
                  <ClockIcon className="w-3 h-3" />
                  {new Date(center.verifiedAt).toLocaleDateString("vi-VN")}
                </span>
              )}
              <Button
                variant="outline"
                size="sm"
                onClick={() => setConfirmAction("unverify")}
                className="h-8 gap-1.5 text-xs text-rose-600 border-rose-200 hover:bg-rose-50 dark:hover:bg-rose-950/30"
              >
                <ShieldAlertIcon className="w-3.5 h-3.5" />
                Hủy xác minh
              </Button>
            </div>
          ) : (
            <div className="flex items-center gap-2">
              <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-xs px-2.5 py-1">
                Chờ duyệt hồ sơ
              </Badge>
              <Button
                size="sm"
                onClick={() => setConfirmAction("verify")}
                className="h-8 gap-1.5 text-xs bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm"
              >
                <ShieldCheckIcon className="w-3.5 h-3.5" />
                Xác minh trung tâm
              </Button>
            </div>
          )}

          <CreateCenterAccountButton
            initialName={center.name}
            initialEmail={center.email || ""}
            initialPhone={center.phone || ""}
          />
        </div>
      </div>

      {/* Hero Banner Section */}
      <div className="relative rounded-xl overflow-hidden border border-border bg-card shadow-sm">
        {/* Cover Background */}
        <div
          className="h-44 sm:h-52 w-full bg-cover bg-center relative bg-gradient-to-r from-slate-800 via-sky-900 to-slate-900"
          style={{ backgroundImage: `url(${profile.coverImage})` }}
        >
          <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
          <div className="absolute top-4 right-4 flex gap-2">
            <Badge
              variant="secondary"
              className="bg-background/90 backdrop-blur-md text-foreground font-medium text-xs px-2.5 py-1 shadow-sm"
            >
              Mã: {center.code}
            </Badge>
          </div>
        </div>

        {/* Header Content Area */}
        <div className="px-6 pb-6 pt-0 relative">
          <div className="flex flex-col sm:flex-row items-start sm:items-end justify-between gap-4 -mt-12 sm:-mt-14">
            {/* Logo / Avatar */}
            <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-xl overflow-hidden border-4 border-background bg-card shadow-md shrink-0 flex items-center justify-center">
              <img
                src={profile.logoImage}
                alt={center.name}
                className="w-full h-full object-cover"
              />
            </div>

            {/* Kênh tư vấn USAS (Ẩn hotline & website để tránh liên hệ trực tiếp ngoài hệ thống) */}
            <div className="flex flex-wrap items-center gap-2 shrink-0 pb-1">
              <div className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-brand/20 bg-brand/5 text-xs font-medium text-brand">
                <ShieldCheckIcon className="w-3.5 h-3.5 text-brand shrink-0" />
                <span>Tư vấn qua hệ thống USAS</span>
              </div>
            </div>
          </div>

          {/* Title & Info Block */}
          <div className="mt-4 space-y-2">
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
                {center.name}
              </h1>
              <Badge variant="secondary" className="font-medium bg-brand/10 text-brand border-brand/20">
                {profile.typeLabel}
              </Badge>
            </div>

            <div className="flex flex-wrap items-center gap-2 text-xs sm:text-sm text-muted-foreground">
              {center.city && (
                <span className="inline-flex items-center gap-1 font-medium text-foreground">
                  <MapPinIcon className="w-3.5 h-3.5 text-brand" />
                  {center.district ? `${center.district}, ${center.city}` : center.city}
                </span>
              )}
              <span>•</span>
              <div className="inline-flex items-center gap-1 text-amber-500 font-semibold">
                <StarIcon className="w-3.5 h-3.5 fill-current" />
                <span>{profile.rating.toFixed(1)}</span>
                <span className="text-muted-foreground font-normal">({profile.reviewCount} đánh giá)</span>
              </div>
              <span>•</span>
              <span>Năm thành lập: <strong className="text-foreground">{profile.establishedYear}</strong></span>
            </div>
          </div>
        </div>
      </div>

      {/* 4 Key Highlight Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-3.5">
        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground font-medium truncate">
              Tỷ lệ đỗ Visa Mỹ
            </span>
            <div className="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-600 flex items-center justify-center shrink-0">
              <FlameIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="text-lg sm:text-2xl font-bold tracking-tight text-foreground truncate">
              {profile.visaSuccessRate}%
            </div>
            <div className="text-[11px] text-muted-foreground truncate mt-0.5">
              Hồ sơ phỏng vấn ĐSQ
            </div>
          </div>
        </Card>

        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground font-medium truncate">
              Học bổng đạt được
            </span>
            <div className="w-8 h-8 rounded-lg bg-amber-500/10 text-amber-600 flex items-center justify-center shrink-0">
              <AwardIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="text-lg sm:text-2xl font-bold tracking-tight text-foreground truncate" title={scholarshipStat.amount}>
              {scholarshipStat.amount}
            </div>
            <div
              className="text-[11px] text-muted-foreground truncate mt-0.5"
              title={scholarshipStat.note || "Kỳ tuyển sinh gần nhất"}
            >
              {scholarshipStat.note || "Kỳ tuyển sinh gần nhất"}
            </div>
          </div>
        </Card>

        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground font-medium truncate">
              Quy mô nhân sự
            </span>
            <div className="w-8 h-8 rounded-lg bg-blue-500/10 text-blue-600 flex items-center justify-center shrink-0">
              <UsersIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="text-lg sm:text-2xl font-bold tracking-tight text-foreground truncate">
              {profile.staffCount}{" "}
              <span className="text-xs sm:text-sm font-normal text-muted-foreground">chuyên viên</span>
            </div>
            <div className="text-[11px] text-muted-foreground truncate mt-0.5">
              Cố vấn & Giảng viên
            </div>
          </div>
        </Card>

        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground font-medium truncate">
              Pháp lý cơ sở
            </span>
            <div className="w-8 h-8 rounded-lg bg-purple-500/10 text-purple-600 flex items-center justify-center shrink-0">
              <ShieldCheckIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="text-base sm:text-lg font-bold tracking-tight text-foreground truncate" title={profile.licenseNumber}>
              {profile.licenseNumber}
            </div>
            <div className="text-[11px] text-muted-foreground truncate mt-0.5" title={profile.licenseIssuedBy}>
              {profile.licenseIssuedBy}
            </div>
          </div>
        </Card>
      </div>

      {/* Main Tabs Navigation */}
      <Tabs defaultValue="tong-quan" className="space-y-4">
        <TabsList className="grid w-full grid-cols-2 sm:grid-cols-4 max-w-2xl h-11 p-1 rounded-xl border-2 border-border/90 bg-muted/90 shadow-2xs">
          <TabsTrigger value="tong-quan" className="text-xs sm:text-sm font-medium">Tổng quan & Cơ sở</TabsTrigger>
          <TabsTrigger value="dich-vu" className="text-xs sm:text-sm font-medium">Dịch vụ & Khóa học</TabsTrigger>
          <TabsTrigger value="thanh-tich" className="text-xs sm:text-sm font-medium">Thành tích đỗ đạt</TabsTrigger>
          <TabsTrigger value="danh-gia" className="text-xs sm:text-sm font-medium">Đánh giá ({profile.reviewCount})</TabsTrigger>
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
              <p>
                {center.description ??
                  `${center.name} là tổ chức giáo dục chuyên sâu về định hướng lộ trình du học Hoa Kỳ, luyện thi chuẩn hóa và đào tạo kỹ năng học thuật cho học sinh Việt Nam. Với hơn 14 năm kinh nghiệm đồng hành, trung tâm đã hỗ trợ hàng ngàn học sinh trúng tuyển thành công vào các trường Đại học danh tiếng tại Mỹ.`}
              </p>

              {/* Chứng nhận quốc tế */}
              <div className="pt-2 border-t">
                <div className="text-xs font-semibold text-foreground mb-2 uppercase tracking-wider">
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
            </CardContent>
          </Card>

          {/* Vị trí & Liên hệ */}
          <div className="grid sm:grid-cols-2 gap-4">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base flex items-center gap-2">
                  <MapPinIcon className="w-4 h-4 text-brand" />
                  Địa chỉ & Pháp lý cơ sở
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div>
                  <span className="font-medium text-foreground">Địa chỉ:</span>{" "}
                  <span className="text-muted-foreground">{center.address ?? "Đang cập nhật địa chỉ"}</span>
                </div>
                <div>
                  <span className="font-medium text-foreground">Khu vực:</span>{" "}
                  <span className="text-muted-foreground">
                    {center.district ? `${center.district}, ${center.city ?? "Việt Nam"}` : (center.city ?? "Toàn quốc")}
                  </span>
                </div>
                <div>
                  <span className="font-medium text-foreground">Số giấy phép:</span>{" "}
                  <span className="text-muted-foreground font-mono">{profile.licenseNumber}</span>
                </div>
                <div>
                  <span className="font-medium text-foreground">Cơ quan cấp phép:</span>{" "}
                  <span className="text-muted-foreground">{profile.licenseIssuedBy}</span>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base flex items-center gap-2">
                  <ClockIcon className="w-4 h-4 text-brand" />
                  Thời gian làm việc & Kênh tiếp nhận
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div>
                  <span className="font-semibold text-foreground">Giờ hoạt động:</span>{" "}
                  <span className="text-muted-foreground">{profile.operatingHours}</span>
                </div>
                <div className="pt-2 border-t text-xs text-muted-foreground flex items-start gap-2 bg-muted/40 p-2.5 rounded-lg">
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
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg flex items-center gap-2">
                <BookOpenIcon className="w-5 h-5 text-brand" />
                Khóa đào tạo Tiếng Anh & Luyện thi Chuẩn hóa
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-3">
                {profile.courses.map((course, idx) => (
                  <div
                    key={idx}
                    className="p-4 rounded-lg border bg-gradient-to-b from-card to-muted/20 flex flex-col justify-between space-y-3"
                  >
                    <div className="space-y-1.5">
                      <div className="font-semibold text-foreground text-sm">{course.name}</div>
                      <div className="text-xs text-muted-foreground">{course.target}</div>
                      <div className="text-xs text-brand font-medium bg-brand/5 p-2 rounded border border-brand/15">
                        ⭐ {course.highlight}
                      </div>
                    </div>
                    <div className="pt-2 border-t text-xs text-muted-foreground space-y-0.5">
                      <div>Thời lượng: <strong>{course.duration}</strong></div>
                      <div>Hình thức: <strong>{course.format}</strong></div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* TAB 3: THÀNH TÍCH ĐỖ ĐẠT */}
        <TabsContent value="thanh-tich" className="space-y-6 pt-2">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg flex items-center gap-2">
                <AwardIcon className="w-5 h-5 text-amber-500" />
                Học sinh trúng tuyển tiêu biểu năm 2025 - 2026
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid sm:grid-cols-3 gap-3.5">
                {profile.successStories.map((story, i) => (
                  <div key={i} className="p-4 rounded-lg border bg-card shadow-sm space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="font-semibold text-foreground text-sm">{story.studentName}</span>
                      <Badge variant="secondary" className="text-[10px]">
                        {story.year}
                      </Badge>
                    </div>
                    <div className="text-xs text-muted-foreground">{story.highSchool}</div>
                    <div className="pt-2 border-t">
                      <div className="text-xs text-brand font-bold">{story.university}</div>
                      <div className="text-xs text-muted-foreground">Ngành: {story.major}</div>
                      <div className="text-xs font-semibold text-emerald-600 mt-1">
                        Học bổng: {story.scholarship}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* TAB 4: ĐÁNH GIÁ & NHẬN XÉT */}
        <TabsContent value="danh-gia" className="space-y-6 pt-2">
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-foreground">
              Đánh giá từ phụ huynh & học sinh ({profile.reviews.length} nhận xét)
            </h3>
            {profile.reviews.map((rev) => (
              <Card key={rev.id}>
                <CardHeader className="pb-2 pt-4 px-4 sm:px-6">
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-sm text-foreground">{rev.reviewerName}</span>
                        {rev.verified && (
                          <Badge variant="outline" className="text-emerald-600 border-emerald-300 text-[10px] py-0">
                            ✓ Xác thực
                          </Badge>
                        )}
                        <span className="text-xs text-muted-foreground">({rev.date})</span>
                      </div>
                      {rev.targetSchool && (
                        <div className="text-xs text-brand font-medium mt-0.5">{rev.targetSchool}</div>
                      )}
                    </div>
                    <div className="flex text-amber-500">
                      {[...Array(rev.rating)].map((_, i) => (
                        <StarIcon key={i} className="w-3.5 h-3.5 fill-current" />
                      ))}
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="px-4 sm:px-6 pb-4 text-xs sm:text-sm text-muted-foreground leading-relaxed">
                  {rev.comment}
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>

      {/* Confirmation Dialog for Verify / Unverify / Hide / Show */}
      <Dialog
        open={confirmAction !== null}
        onOpenChange={(open) => {
          if (!open && !isSubmitting) setConfirmAction(null);
        }}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              {confirmAction === "verify" ? (
                <>
                  <ShieldCheckIcon className="w-5 h-5 text-emerald-600" />
                  <span>Xác minh trung tâm tư vấn</span>
                </>
              ) : confirmAction === "unverify" ? (
                <>
                  <ShieldAlertIcon className="w-5 h-5 text-rose-600" />
                  <span>Hủy xác minh trung tâm tư vấn</span>
                </>
              ) : confirmAction === "hide" ? (
                <>
                  <EyeOffIcon className="w-5 h-5 text-rose-600" />
                  <span>Ẩn trung tâm tư vấn</span>
                </>
              ) : (
                <>
                  <EyeIcon className="w-5 h-5 text-emerald-600" />
                  <span>Hiện lại trung tâm tư vấn</span>
                </>
              )}
            </DialogTitle>
            <DialogDescription className="pt-2 text-sm leading-relaxed">
              {confirmAction === "verify" ? (
                <>
                  Bạn có chắc chắn muốn xác minh trung tâm{" "}
                  <strong className="text-foreground">{center.name}</strong>?
                  <br />
                  Sau khi xác minh, trung tâm sẽ được gắn huy hiệu &quot;Đã xác minh&quot; và đủ điều kiện xuất hiện trong danh bạ đề xuất cho học sinh, phụ huynh.
                </>
              ) : confirmAction === "unverify" ? (
                <>
                  Bạn có chắc chắn muốn hủy trạng thái xác minh của trung tâm{" "}
                  <strong className="text-foreground">{center.name}</strong>?
                  <br />
                  Trung tâm sẽ chuyển về trạng thái Chưa xác minh (&quot;unverified&quot;) và không còn được hiển thị huy hiệu xác minh trên toàn hệ thống.
                </>
              ) : confirmAction === "hide" ? (
                <>
                  Bạn có chắc chắn muốn ẩn trung tâm{" "}
                  <strong className="text-foreground">{center.name}</strong>?
                  <br />
                  Trung tâm sẽ bị ẩn khỏi danh bạ công khai và thuật toán gợi ý của AI sẽ không đề xuất trung tâm này cho học sinh.
                </>
              ) : (
                <>
                  Bạn có chắc chắn muốn kích hoạt và hiển thị lại trung tâm{" "}
                  <strong className="text-foreground">{center.name}</strong>?
                  <br />
                  Trung tâm sẽ xuất hiện trở lại trên danh bạ công khai và đủ điều kiện để AI gợi ý.
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="mt-4 gap-2 sm:gap-0">
            <Button
              variant="outline"
              onClick={() => setConfirmAction(null)}
              disabled={isSubmitting}
            >
              Hủy bỏ
            </Button>
            <Button
              variant={
                confirmAction === "verify" || confirmAction === "show"
                  ? "default"
                  : "destructive"
              }
              onClick={handleConfirmAction}
              disabled={isSubmitting}
              className={
                confirmAction === "verify" || confirmAction === "show"
                  ? "bg-emerald-600 hover:bg-emerald-700 text-white"
                  : undefined
              }
            >
              {isSubmitting ? (
                <>
                  <Spinner className="w-4 h-4 mr-1.5" />
                  Đang xử lý...
                </>
              ) : confirmAction === "verify" ? (
                <>
                  <CheckCircle2Icon className="w-4 h-4 mr-1.5" />
                  Xác nhận xác minh
                </>
              ) : confirmAction === "unverify" ? (
                <>
                  <ShieldAlertIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận hủy xác minh
                </>
              ) : confirmAction === "hide" ? (
                <>
                  <EyeOffIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận ẩn
                </>
              ) : (
                <>
                  <EyeIcon className="w-4 h-4 mr-1.5" />
                  Xác nhận hiện
                </>
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
