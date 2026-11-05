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
import { ConsultationBookingModal } from "./ConsultationBookingModal";
import { cn } from "cn";

interface StudyCenterDetailViewProps {
  center: StudyCenter;
}

export function StudyCenterDetailView({ center }: StudyCenterDetailViewProps) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";

  return (
    <div className="space-y-6 pb-12">
      {/* Back button & Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <Link
          href="/centers"
          className="inline-flex items-center gap-1.5 hover:text-foreground transition-colors font-medium"
        >
          <ArrowLeftIcon className="w-4 h-4" />
          Quay lại Danh bạ trung tâm
        </Link>
        <span>/</span>
        <span className="text-foreground truncate font-medium">{center.name}</span>
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
          {/* Top row: Avatar & Action Buttons */}
          <div className="flex flex-col sm:flex-row items-start sm:items-end justify-between gap-4 -mt-12 sm:-mt-14">
            {/* Logo / Avatar */}
            <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-xl overflow-hidden border-4 border-background bg-card shadow-md shrink-0 flex items-center justify-center">
              <img
                src={profile.logoImage}
                alt={center.name}
                className="w-full h-full object-cover"
              />
            </div>

            {/* CTA Buttons */}
            <div className="flex flex-wrap items-center gap-2 w-full sm:w-auto shrink-0 pb-1">
              <ConsultationBookingModal centerName={center.name} />
              {center.phone && (
                <a
                  href={`tel:${center.phone}`}
                  className={cn(buttonVariants({ variant: "outline", size: "lg" }), "gap-2 inline-flex items-center")}
                >
                  <PhoneIcon className="w-4 h-4 text-emerald-600" />
                  {center.phone}
                </a>
              )}
            </div>
          </div>

          {/* Title & Info Block - Cleanly placed on card background */}
          <div className="mt-4 space-y-2">
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
                {center.name}
              </h1>
              {isVerified ? (
                <Badge className="bg-emerald-600 hover:bg-emerald-600/90 text-white gap-1 text-xs px-2.5 py-0.5">
                  <CheckCircle2Icon className="w-3.5 h-3.5" />
                  Đã xác minh Sở GD&ĐT
                </Badge>
              ) : (
                <Badge variant="outline" className="text-muted-foreground text-xs px-2.5 py-0.5">
                  Chờ xác minh
                </Badge>
              )}
            </div>

            <div className="flex flex-wrap items-center gap-2 text-xs sm:text-sm text-muted-foreground">
              <Badge variant="secondary" className="font-medium bg-brand/10 text-brand border-brand/20">
                {profile.typeLabel}
              </Badge>
              {center.city && (
                <span className="inline-flex items-center gap-1 font-medium text-foreground">
                  <MapPinIcon className="w-3.5 h-3.5 text-brand" />
                  {center.city}
                </span>
              )}
              <span>•</span>
              <div className="inline-flex items-center gap-1 text-amber-500 font-semibold">
                <StarIcon className="w-3.5 h-3.5 fill-current" />
                <span>{profile.rating.toFixed(1)}</span>
                <span className="text-muted-foreground font-normal">({profile.reviewCount} đánh giá)</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* 4 Key Highlight Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3.5">
        <Card className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-emerald-500/10 text-emerald-600 flex items-center justify-center shrink-0">
            <FlameIcon className="w-5 h-5" />
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Tỷ lệ đỗ Visa Mỹ</div>
            <div className="text-lg font-bold text-foreground">{profile.visaSuccessRate}%</div>
          </div>
        </Card>

        <Card className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-amber-500/10 text-amber-600 flex items-center justify-center shrink-0">
            <AwardIcon className="w-5 h-5" />
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Tổng học bổng đạt được</div>
            <div className="text-sm font-bold text-foreground truncate">{profile.totalScholarshipsWon}</div>
          </div>
        </Card>

        <Card className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-blue-500/10 text-blue-600 flex items-center justify-center shrink-0">
            <UsersIcon className="w-5 h-5" />
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Quy mô nhân sự</div>
            <div className="text-sm font-bold text-foreground">{profile.staffCount} chuyên viên & GV</div>
          </div>
        </Card>

        <Card className="p-4 flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-purple-500/10 text-purple-600 flex items-center justify-center shrink-0">
            <ShieldCheckIcon className="w-5 h-5" />
          </div>
          <div>
            <div className="text-xs text-muted-foreground">Pháp lý hoạt động</div>
            <div className="text-sm font-bold text-foreground truncate">{profile.licenseNumber}</div>
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

          {/* Gallery hình ảnh thực tế */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-lg">Không gian & Cơ sở vật chất</CardTitle>
              <CardDescription className="text-xs">
                Môi trường đào tạo, phòng phỏng vấn thử và không gian tự học chuẩn quốc tế
              </CardDescription>
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

          {/* Vị trí & Liên hệ */}
          <div className="grid sm:grid-cols-2 gap-4">
            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base flex items-center gap-2">
                  <MapPinIcon className="w-4 h-4 text-brand" />
                  Địa chỉ & Vị trí văn phòng
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div>
                  <span className="font-medium text-foreground">Địa chỉ:</span>{" "}
                  <span className="text-muted-foreground">{center.address ?? "Đang cập nhật địa chỉ"}</span>
                </div>
                <div>
                  <span className="font-medium text-foreground">Khu vực:</span>{" "}
                  <span className="text-muted-foreground">{center.city ?? "Toàn quốc"}</span>
                </div>
                <div>
                  <span className="font-medium text-foreground">Giấy phép cơ sở:</span>{" "}
                  <span className="text-muted-foreground">{profile.licenseIssuedBy}</span>
                </div>
                {center.address && (
                  <div className="pt-2">
                    <a
                      href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${center.name} ${center.address}`)}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={cn(buttonVariants({ variant: "secondary", size: "sm" }), "gap-1.5 text-xs inline-flex items-center")}
                    >
                      <GlobeIcon className="w-3.5 h-3.5" />
                      Mở Google Maps chỉ đường
                      <ExternalLinkIcon className="w-3 h-3 ml-1" />
                    </a>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-3">
                <CardTitle className="text-base flex items-center gap-2">
                  <ClockIcon className="w-4 h-4 text-brand" />
                  Thông tin kết nối & Giờ làm việc
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-xs sm:text-sm">
                <div>
                  <span className="font-medium text-foreground">Giờ hoạt động:</span>{" "}
                  <span className="text-muted-foreground">{profile.operatingHours}</span>
                </div>
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
                    <span className="font-medium text-foreground">Website chính thức:</span>{" "}
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
                    <div>
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

          {/* Khóa học Luyện thi Tiếng Anh / SAT */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="text-lg flex items-center gap-2">
                    <BookOpenIcon className="w-5 h-5 text-brand" />
                    Khóa đào tạo Tiếng Anh & Luyện thi Chuẩn hóa
                  </CardTitle>
                  <CardDescription className="text-xs">
                    Chương trình học tập trung mục tiêu đầu ra cho lộ trình du học
                  </CardDescription>
                </div>
                <Badge variant="outline" className="border-brand/40 text-brand">
                  Cam kết đầu ra
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-3">
                {profile.courses.map((course, idx) => (
                  <div
                    key={idx}
                    className="p-4 rounded-lg border bg-gradient-to-b from-card to-muted/20 flex flex-col justify-between"
                  >
                    <div className="space-y-2">
                      <div className="font-semibold text-foreground text-sm leading-snug">{course.name}</div>
                      <div className="text-xs text-muted-foreground">{course.target}</div>
                      <div className="text-xs text-brand font-medium bg-brand/5 p-2 rounded border border-brand/15">
                        ⭐ {course.highlight}
                      </div>
                    </div>
                    <div className="pt-3 mt-3 border-t text-xs text-muted-foreground space-y-1">
                      <div>
                        <strong>Thời lượng:</strong> {course.duration}
                      </div>
                      <div>
                        <strong>Hình thức:</strong> {course.format}
                      </div>
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
                Gương mặt trúng tuyển tiêu biểu năm 2025 - 2026
              </CardTitle>
              <CardDescription className="text-xs">
                Các học sinh đạt học bổng cao vào các trường Đại học danh tiếng tại Hoa Kỳ
              </CardDescription>
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
          {/* Tiêu chí đánh giá */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="text-lg">Điểm đánh giá cộng đồng</CardTitle>
                  <CardDescription className="text-xs">
                    Dựa trên {profile.reviewCount} lượt đánh giá từ học sinh và phụ huynh đã làm việc với trung tâm
                  </CardDescription>
                </div>
                <div className="text-right">
                  <div className="text-2xl font-black text-foreground flex items-center gap-1">
                    <StarIcon className="w-5 h-5 text-amber-500 fill-current" />
                    {profile.rating.toFixed(1)} / 5.0
                  </div>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-center">
                <div className="p-3 rounded-lg border bg-muted/30">
                  <div className="text-xs text-muted-foreground">Chất lượng tư vấn</div>
                  <div className="text-lg font-bold text-foreground mt-0.5">5.0 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border bg-muted/30">
                  <div className="text-xs text-muted-foreground">Độ tận tâm & tốc độ</div>
                  <div className="text-lg font-bold text-foreground mt-0.5">4.9 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border bg-muted/30">
                  <div className="text-xs text-muted-foreground">Minh bạch chi phí</div>
                  <div className="text-lg font-bold text-foreground mt-0.5">4.8 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border bg-muted/30">
                  <div className="text-xs text-muted-foreground">Kết quả Visa & Học bổng</div>
                  <div className="text-lg font-bold text-foreground mt-0.5">5.0 ⭐</div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Danh sách bình luận */}
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-foreground">Nhận xét từ học viên đã xác thực</h3>
            {profile.reviews.map((rev) => (
              <Card key={rev.id}>
                <CardHeader className="pb-2 pt-4 px-4 sm:px-6">
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-sm text-foreground">{rev.reviewerName}</span>
                        {rev.verified && (
                          <Badge variant="outline" className="text-emerald-600 border-emerald-300 text-[10px] py-0">
                            ✓ Học viên xác thực
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
