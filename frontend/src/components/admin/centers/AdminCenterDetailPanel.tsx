"use client";

import Link from "next/link";
import {
  MapPinIcon,
  PhoneIcon,
  MailIcon,
  GlobeIcon,
  CheckCircle2Icon,
  ExternalLinkIcon,
  ShieldCheckIcon,
  AwardIcon,
  FlameIcon,
  UsersIcon,
  ClockIcon,
  ChevronRightIcon,
} from "lucide-react";
import type { StudyCenter } from "@/types/api";
import { getEnrichedCenterProfile } from "@/lib/center-details";
import { centerServiceLabel } from "@/lib/center-services";
import { studyLevelLabel } from "@/lib/study-levels";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { DetailPanel } from "@/components/admin/shared/DetailPanel";
import { cn } from "cn";

interface AdminCenterDetailPanelProps {
  center: StudyCenter;
  onClose: () => void;
}

export function AdminCenterDetailPanel({ center, onClose }: AdminCenterDetailPanelProps) {
  const profile = getEnrichedCenterProfile(center);
  const isVerified = center.verificationStatus === "verified" || profile.center.verificationStatus === "verified";

  return (
    <DetailPanel
      title={center.name}
      description={`Mã cơ sở: ${center.code}`}
      onClose={onClose}
    >
      <div className="space-y-5">
        {/* Banner & Logo Preview */}
        <div className="relative rounded-lg overflow-hidden border bg-muted aspect-[3/1]">
          {profile.coverImage && (
            <img
              src={profile.coverImage}
              alt=""
              className="w-full h-full object-cover"
            />
          )}
          <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
          <div className="absolute bottom-2.5 left-3 flex items-center gap-2.5">
            <div className="w-10 h-10 rounded-lg overflow-hidden border-2 border-background bg-card shadow shrink-0 flex items-center justify-center">
              <img
                src={profile.logoImage}
                alt={center.name}
                className="w-full h-full object-cover"
              />
            </div>
            <div className="text-white min-w-0">
              <div className="text-xs font-semibold truncate leading-tight drop-shadow">{center.name}</div>
              <div className="text-[10px] text-white/80">{profile.typeLabel}</div>
            </div>
          </div>
        </div>

        {/* Verification Status & Quick Actions */}
        <div className="flex flex-wrap items-center justify-between gap-2 p-3 rounded-lg border bg-muted/40">
          <div className="space-y-0.5">
            <div className="text-xs text-muted-foreground font-medium">Trạng thái xác thực</div>
            {isVerified ? (
              <Badge className="bg-emerald-600 text-white gap-1 text-xs">
                <CheckCircle2Icon className="w-3 h-3" />
                Đã xác minh Sở GD&ĐT
              </Badge>
            ) : (
              <Badge variant="outline" className="text-amber-600 border-amber-300 bg-amber-50 dark:bg-amber-950/40 text-xs">
                Chờ kiểm duyệt hồ sơ
              </Badge>
            )}
          </div>

          <div className="flex items-center gap-2">
            <Link
              href={`/admin/centers/${center.id}`}
              className={cn(buttonVariants({ variant: "default", size: "sm" }), "gap-1 text-xs")}
            >
              Xem trang quản trị
              <ChevronRightIcon className="w-3.5 h-3.5" />
            </Link>
          </div>
        </div>

        {/* Quick KPI stats */}
        <div className="grid grid-cols-2 gap-2 text-xs">
          <div className="p-2.5 rounded-lg border bg-card space-y-1">
            <div className="text-muted-foreground flex items-center gap-1">
              <FlameIcon className="w-3.5 h-3.5 text-emerald-600" />
              Tỷ lệ Visa
            </div>
            <div className="font-bold text-foreground text-sm">{profile.visaSuccessRate}%</div>
          </div>

          <div className="p-2.5 rounded-lg border bg-card space-y-1">
            <div className="text-muted-foreground flex items-center gap-1">
              <AwardIcon className="w-3.5 h-3.5 text-amber-600" />
              Tổng học bổng
            </div>
            <div className="font-bold text-foreground text-sm truncate" title={profile.totalScholarshipsWon}>
              {profile.totalScholarshipsWon.split("(")[0].trim()}
            </div>
          </div>

          <div className="p-2.5 rounded-lg border bg-card space-y-1">
            <div className="text-muted-foreground flex items-center gap-1">
              <UsersIcon className="w-3.5 h-3.5 text-blue-600" />
              Nhân sự
            </div>
            <div className="font-bold text-foreground text-sm">{profile.staffCount} chuyên viên</div>
          </div>

          <div className="p-2.5 rounded-lg border bg-card space-y-1">
            <div className="text-muted-foreground flex items-center gap-1">
              <ShieldCheckIcon className="w-3.5 h-3.5 text-purple-600" />
              Pháp lý
            </div>
            <div className="font-bold text-foreground text-sm truncate" title={profile.licenseNumber}>
              {profile.licenseNumber}
            </div>
          </div>
        </div>

        {/* Contact details */}
        <div className="space-y-2 text-xs">
          <div className="font-semibold text-foreground text-xs uppercase tracking-wider text-muted-foreground">
            Thông tin liên hệ & Địa điểm
          </div>
          <div className="space-y-1.5 p-3 rounded-lg border bg-card text-muted-foreground">
            <div className="flex items-start gap-2 text-foreground">
              <MapPinIcon className="w-3.5 h-3.5 text-brand shrink-0 mt-0.5" />
              <span>{center.address ?? "Chưa cập nhật địa chỉ"}</span>
            </div>
            <div className="pl-5.5 text-[11px] text-muted-foreground">
              Khu vực: {center.district ? `${center.district}, ${center.city ?? "Việt Nam"}` : (center.city ?? "Toàn quốc")}
            </div>

            {center.phone && (
              <div className="flex items-center gap-2 pt-1 border-t">
                <PhoneIcon className="w-3.5 h-3.5 text-emerald-600 shrink-0" />
                <a href={`tel:${center.phone}`} className="text-foreground hover:underline font-mono">
                  {center.phone}
                </a>
              </div>
            )}

            {center.email && (
              <div className="flex items-center gap-2">
                <MailIcon className="w-3.5 h-3.5 text-blue-600 shrink-0" />
                <a href={`mailto:${center.email}`} className="text-brand hover:underline">
                  {center.email}
                </a>
              </div>
            )}

            {center.website && (
              <div className="flex items-center gap-2">
                <GlobeIcon className="w-3.5 h-3.5 text-brand shrink-0" />
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

            <div className="flex items-center gap-2 pt-1 border-t text-[11px]">
              <ClockIcon className="w-3.5 h-3.5 text-amber-600 shrink-0" />
              <span>{profile.operatingHours}</span>
            </div>
          </div>
        </div>

        {/* Services & Study Levels */}
        {center.services.length > 0 && (
          <div className="space-y-2 text-xs">
            <div className="font-semibold text-foreground text-xs uppercase tracking-wider text-muted-foreground">
              Dịch vụ tư vấn ({center.services.length})
            </div>
            <div className="flex flex-wrap gap-1.5">
              {center.services.map((svc) => (
                <Badge key={svc} variant="secondary" className="text-[11px] font-normal py-0.5">
                  {centerServiceLabel(svc)}
                </Badge>
              ))}
            </div>
          </div>
        )}

        {center.studyLevels.length > 0 && (
          <div className="space-y-2 text-xs">
            <div className="font-semibold text-foreground text-xs uppercase tracking-wider text-muted-foreground">
              Bậc học đào tạo ({center.studyLevels.length})
            </div>
            <div className="flex flex-wrap gap-1.5">
              {center.studyLevels.map((lvl) => (
                <Badge key={lvl} variant="outline" className="text-[11px] font-normal py-0.5">
                  {studyLevelLabel(lvl)}
                </Badge>
              ))}
            </div>
          </div>
        )}

        {/* Public view preview link */}
        <div className="pt-2 border-t">
          <Link
            href={`/centers/${center.id}`}
            target="_blank"
            rel="noopener noreferrer"
            className={cn(buttonVariants({ variant: "outline", size: "sm" }), "w-full gap-1.5 text-xs text-muted-foreground hover:text-foreground")}
          >
            <span>Xem giao diện công khai (Học sinh & Phụ huynh)</span>
            <ExternalLinkIcon className="w-3.5 h-3.5" />
          </Link>
        </div>
      </div>
    </DetailPanel>
  );
}
