"use client";

import Link from "next/link";
import {
  ArrowLeftIcon,
  CheckCircle2Icon,
  MapPinIcon,
  PhoneIcon,
  GlobeIcon,
  ClockIcon,
  StarIcon,
  ShieldCheckIcon,
  AwardIcon,
  UsersIcon,
  GraduationCapIcon,
  BookOpenIcon,
  ExternalLinkIcon,
  Building2Icon,
  FlameIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerServiceLabel } from "@/lib/center-services";
import { studyLevelLabel } from "@/lib/study-levels";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "cn";

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

export function AdminCenterDetailView({ center }: AdminCenterDetailViewProps) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";
  const scholarshipStat = parseScholarshipStat(profile.totalScholarshipsWon);

  return (
    <div className="space-y-6 pb-12">
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
        </div>

        <div className="flex items-center gap-2">
          {isVerified ? (
            <Badge className="bg-emerald-600 text-white gap-1 text-xs px-2.5 py-1">
              <CheckCircle2Icon className="w-3.5 h-3.5" />
              Đã xác minh Sở GD&ĐT
            </Badge>
          ) : (
            <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-xs px-2.5 py-1">
              Chờ duyệt hồ sơ
            </Badge>
          )}

          <Link
            href={`/centers/${center.id}`}
            target="_blank"
            rel="noopener noreferrer"
            className={cn(buttonVariants({ variant: "outline", size: "sm" }), "gap-1.5 text-xs")}
          >
            <span>Xem trang công khai (Học sinh)</span>
            <ExternalLinkIcon className="w-3.5 h-3.5" />
          </Link>
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

            {/* Contact Chips */}
            <div className="flex flex-wrap items-center gap-2 shrink-0 pb-1">
              {center.phone && (
                <a
                  href={`tel:${center.phone}`}
                  className={cn(buttonVariants({ variant: "outline", size: "sm" }), "gap-1.5 text-xs")}
                >
                  <PhoneIcon className="w-3.5 h-3.5 text-emerald-600" />
                  {center.phone}
                </a>
              )}
              {center.website && (
                <a
                  href={center.website}
                  target="_blank"
                  rel="noopener noreferrer"
                  className={cn(buttonVariants({ variant: "outline", size: "sm" }), "gap-1.5 text-xs")}
                >
                  <GlobeIcon className="w-3.5 h-3.5 text-brand" />
                  Website
                </a>
              )}
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
        <TabsList className="grid w-full grid-cols-4 max-w-xl h-10 p-1">
          <TabsTrigger value="tong-quan" className="text-xs sm:text-sm">Tổng quan & Cơ sở</TabsTrigger>
          <TabsTrigger value="dich-vu" className="text-xs sm:text-sm">Dịch vụ & Khóa học</TabsTrigger>
          <TabsTrigger value="thanh-tich" className="text-xs sm:text-sm">Thành tích đỗ đạt</TabsTrigger>
          <TabsTrigger value="danh-gia" className="text-xs sm:text-sm">Đánh giá ({profile.reviewCount})</TabsTrigger>
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
                  Kênh kết nối & Giờ làm việc
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div>
                  <span className="font-medium text-foreground">Giờ hoạt động:</span>{" "}
                  <span className="text-muted-foreground">{profile.operatingHours}</span>
                </div>
                {center.phone && (
                  <div>
                    <span className="font-medium text-foreground">Hotline:</span>{" "}
                    <span className="text-foreground font-mono">{center.phone}</span>
                  </div>
                )}
                {center.email && (
                  <div>
                    <span className="font-medium text-foreground">Email:</span>{" "}
                    <a href={`mailto:${center.email}`} className="text-brand hover:underline">
                      {center.email}
                    </a>
                  </div>
                )}
                {center.website && (
                  <div>
                    <span className="font-medium text-foreground">Website:</span>{" "}
                    <a
                      href={center.website}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-brand hover:underline inline-flex items-center gap-1"
                    >
                      {center.website}
                      <ExternalLinkIcon className="w-3 h-3" />
                    </a>
                  </div>
                )}
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
    </div>
  );
}
