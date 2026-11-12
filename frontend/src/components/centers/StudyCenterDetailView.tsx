"use client";

import Link from "next/link";
import {
  ArrowLeftIcon,
  CheckCircle2Icon,
  MapPinIcon,
  ClockIcon,
  StarIcon,
  ShieldCheckIcon,
  AwardIcon,
  UsersIcon,
  GraduationCapIcon,
  BookOpenIcon,
  Building2Icon,
  FlameIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
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

export function StudyCenterDetailView({ center }: StudyCenterDetailViewProps) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";
  const scholarshipStat = parseScholarshipStat(profile.totalScholarshipsWon);

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
          <div
            className="h-full w-full bg-cover bg-center"
            style={{ backgroundImage: `url(${profile.coverImage})` }}
          />
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/55 via-black/15 to-transparent" />
        </div>

        <div className="px-6 pb-6 pt-3">
          <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-end min-w-0">
              <div className="relative -mt-12 sm:-mt-16 flex size-20 sm:size-24 shrink-0 items-center justify-center overflow-hidden rounded-xl border-4 border-surface bg-surface">
                <img
                  src={profile.logoImage}
                  alt={center.name}
                  className="h-full w-full object-cover"
                />
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
                  <div className="inline-flex items-center gap-1 font-semibold text-gold tabular-nums">
                    <StarIcon className="size-3.5 fill-current" />
                    <span>{profile.rating.toFixed(1)}</span>
                    <span className="font-normal text-ink-2">({profile.reviewCount} đánh giá)</span>
                  </div>
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
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
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
              Hồ sơ phỏng vấn ĐSQ
            </div>
          </div>
        </Card>

        <Card className="p-3.5 sm:p-4 flex flex-col justify-between gap-3 min-w-0 border-line">
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-ink-2 font-medium truncate">
              Học bổng đạt được
            </span>
            <div className="w-8 h-8 rounded-lg bg-gold-wash text-gold flex items-center justify-center shrink-0">
              <AwardIcon className="w-4 h-4" />
            </div>
          </div>
          <div className="min-w-0">
            <div className="font-heading text-xl sm:text-2xl font-bold tracking-tight text-ink tabular-nums truncate" title={scholarshipStat.amount}>
              {scholarshipStat.amount}
            </div>
            <div
              className="text-xs text-ink-3 truncate mt-0.5"
              title={scholarshipStat.note || "Kỳ tuyển sinh gần nhất"}
            >
              {scholarshipStat.note || "Kỳ tuyển sinh gần nhất"}
            </div>
          </div>
        </Card>

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
              Cố vấn & Giảng viên
            </div>
          </div>
        </Card>

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
            <div className="text-base sm:text-lg font-bold tracking-tight text-foreground truncate" title={profile.licenseNumber}>
              {profile.licenseNumber}
            </div>
            <div className="text-xs text-muted-foreground truncate mt-0.5" title={profile.licenseIssuedBy}>
              {profile.taxCode ? `MST: ${profile.taxCode} · ${profile.licenseIssuedBy}` : profile.licenseIssuedBy}
            </div>
          </div>
        </Card>
      </div>

      {/* Main Tabs Navigation */}
      <Tabs defaultValue="tong-quan" className="space-y-4">
        <TabsList className="grid w-full grid-cols-2 sm:grid-cols-4 max-w-2xl h-11 p-1 rounded-xl border-2 border-border/90 bg-muted/90 ">
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
            <CenterLegalInfoCard profile={profile} mode="public" showMapsLink />

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
                <Badge variant="brand">
                  Cam kết đầu ra
                </Badge>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-3">
                {profile.courses.map((course, idx) => (
                  <div
                    key={idx}
                    className="p-4 rounded-lg border border-line bg-surface flex flex-col justify-between"
                  >
                    <div className="space-y-2">
                      <div className="font-semibold text-ink text-sm leading-snug">{course.name}</div>
                      <div className="text-xs text-ink-2">{course.target}</div>
                      <div className="text-xs text-brand-ink font-medium bg-brand-wash/70 p-2 rounded border border-brand/20">
                        ⭐ {course.highlight}
                      </div>
                    </div>
                    <div className="pt-3 mt-3 border-t border-line text-xs text-ink-2 space-y-1">
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
                <AwardIcon className="w-5 h-5 text-gold" />
                Gương mặt trúng tuyển tiêu biểu năm 2025 - 2026
              </CardTitle>
              <CardDescription className="text-xs">
                Các học sinh đạt học bổng cao vào các trường Đại học danh tiếng tại Hoa Kỳ
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid sm:grid-cols-3 gap-3.5">
                {profile.successStories.map((story, i) => (
                  <div key={i} className="p-4 rounded-lg border border-line bg-surface space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="font-semibold text-ink text-sm">{story.studentName}</span>
                      <Badge variant="neutral" className="text-xs">
                        {story.year}
                      </Badge>
                    </div>
                    <div className="text-xs text-ink-2">{story.highSchool}</div>
                    <div className="pt-2 border-t border-line">
                      <div className="text-xs text-brand font-bold">{story.university}</div>
                      <div className="text-xs text-ink-2">Ngành: {story.major}</div>
                      <div className="text-xs font-semibold text-ok mt-1">
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
                  <div className="font-heading text-2xl font-bold text-ink flex items-center gap-1 tabular-nums">
                    <StarIcon className="w-5 h-5 text-gold fill-current" />
                    {profile.rating.toFixed(1)} / 5.0
                  </div>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-center">
                <div className="p-3 rounded-lg border border-line bg-ground">
                  <div className="text-xs text-ink-2">Chất lượng tư vấn</div>
                  <div className="text-lg font-bold text-ink tabular-nums mt-0.5">5.0 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border border-line bg-ground">
                  <div className="text-xs text-ink-2">Độ tận tâm & tốc độ</div>
                  <div className="text-lg font-bold text-ink tabular-nums mt-0.5">4.9 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border border-line bg-ground">
                  <div className="text-xs text-ink-2">Minh bạch chi phí</div>
                  <div className="text-lg font-bold text-ink tabular-nums mt-0.5">4.8 ⭐</div>
                </div>
                <div className="p-3 rounded-lg border border-line bg-ground">
                  <div className="text-xs text-ink-2">Kết quả Visa & Học bổng</div>
                  <div className="text-lg font-bold text-ink tabular-nums mt-0.5">5.0 ⭐</div>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Danh sách bình luận */}
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Nhận xét từ học viên đã xác thực</h3>
            {profile.reviews.map((rev) => (
              <Card key={rev.id}>
                <CardHeader className="pb-2 pt-4 px-4 sm:px-6">
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-sm text-ink">{rev.reviewerName}</span>
                        {rev.verified && (
                          <Badge variant="ok" className="text-xs py-0">
                            ✓ Học viên xác thực
                          </Badge>
                        )}
                        <span className="text-xs text-ink-3">({rev.date})</span>
                      </div>
                      {rev.targetSchool && (
                        <div className="text-xs text-brand font-medium mt-0.5">{rev.targetSchool}</div>
                      )}
                    </div>
                    <div className="flex text-gold">
                      {[...Array(rev.rating)].map((_, i) => (
                        <StarIcon key={i} className="w-3.5 h-3.5 fill-current" />
                      ))}
                    </div>
                  </div>
                </CardHeader>
                <CardContent className="px-4 sm:px-6 pb-4 text-xs sm:text-sm text-ink-2 leading-relaxed">
                  {rev.comment}
                </CardContent>
              </Card>
            ))}
          </div>
        </TabsContent>
      </Tabs>
    </article>
  );
}
