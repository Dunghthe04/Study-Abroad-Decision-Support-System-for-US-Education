"use client";

import {
  AlertTriangleIcon,
  ExternalLinkIcon,
  FileTextIcon,
  MapPinIcon,
  ShieldCheckIcon,
} from "lucide-react";
import type { EnrichedCenterProfile } from "@/lib/center-details";
import {
  centerNeedsTrainingLicense,
  formatLegalDate,
  getCenterLegalMissingChecklist,
  LEGAL_COMPLIANCE_MODE_LABELS,
} from "@/lib/center-legal";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

interface CenterLegalInfoCardProps {
  profile: EnrichedCenterProfile;
  mode?: "admin" | "public";
  showMapsLink?: boolean;
}

export function CenterLegalInfoCard({
  profile,
  mode = "public",
  showMapsLink = false,
}: CenterLegalInfoCardProps) {
  const { center } = profile;
  const formattedIssuedDate = formatLegalDate(profile.licenseIssuedDate);
  const needsTrainingLicense = centerNeedsTrainingLicense(center.centerType);
  const missingItems = mode === "admin" ? getCenterLegalMissingChecklist(center) : [];
  const docUrls = profile.legalDocumentUrls ?? [];

  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="text-base flex items-center gap-2">
            {mode === "admin" ? (
              <ShieldCheckIcon className="w-4 h-4 text-brand" />
            ) : (
              <MapPinIcon className="w-4 h-4 text-brand" />
            )}
            {mode === "admin" ? "Hồ sơ pháp lý & Địa chỉ" : "Địa chỉ & Pháp lý hoạt động"}
          </CardTitle>
          <Badge variant="outline" className="text-[11px] font-normal">
            {LEGAL_COMPLIANCE_MODE_LABELS[profile.legalComplianceMode]}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-2.5 text-xs sm:text-sm">
        <div>
          <span className="font-medium text-foreground">Địa chỉ:</span>{" "}
          <span className="text-muted-foreground">
            {center.address ?? "Đang cập nhật địa chỉ"}
          </span>
        </div>
        <div>
          <span className="font-medium text-foreground">Khu vực:</span>{" "}
          <span className="text-muted-foreground">
            {center.district
              ? `${center.district}, ${center.city ?? "Việt Nam"}`
              : center.city ?? "Toàn quốc"}
          </span>
        </div>
        {profile.establishedYear && (
          <div>
            <span className="font-medium text-foreground">Năm thành lập:</span>{" "}
            <span className="text-muted-foreground">{profile.establishedYear}</span>
          </div>
        )}
        <div>
          <span className="font-medium text-foreground">Mã số thuế / ĐKKD:</span>{" "}
          <span className="text-muted-foreground font-mono">
            {profile.taxCode || "Chưa cập nhật"}
          </span>
        </div>
        <div>
          <span className="font-medium text-foreground">Người đại diện PL:</span>{" "}
          <span className="text-muted-foreground">
            {profile.legalRepresentative || "Chưa cập nhật"}
          </span>
        </div>
        <div>
          <span className="font-medium text-foreground">
            {profile.legalComplianceMode === "notified"
              ? "Mã hồ sơ thông báo:"
              : "Số GP tư vấn du học:"}
          </span>{" "}
          <span className="text-muted-foreground font-mono">{profile.licenseNumber}</span>
        </div>
        {(profile.trainingLicenseNumber || needsTrainingLicense) && (
          <div>
            <span className="font-medium text-foreground">GP đào tạo ngoại ngữ:</span>{" "}
            <span className="text-muted-foreground font-mono">
              {profile.trainingLicenseNumber || "Chưa bổ sung"}
            </span>
          </div>
        )}
        <div>
          <span className="font-medium text-foreground">Cơ quan cấp / tiếp nhận:</span>{" "}
          <span className="text-muted-foreground">{profile.licenseIssuedBy}</span>
          {formattedIssuedDate && (
            <span className="text-muted-foreground"> (ngày {formattedIssuedDate})</span>
          )}
        </div>
        {profile.certifiedCounselorCount != null && (
          <div>
            <span className="font-medium text-foreground">Tư vấn viên có chứng chỉ:</span>{" "}
            <span className="text-muted-foreground">
              {profile.certifiedCounselorCount}
              {center.staffCount ? ` / ${center.staffCount} nhân sự` : " nhân sự"}
            </span>
          </div>
        )}

        {docUrls.length > 0 && (
          <div className="pt-2 border-t space-y-1.5">
            <div className="text-xs font-semibold text-foreground flex items-center gap-1.5">
              <FileTextIcon className="w-3.5 h-3.5 text-brand" />
              Tài liệu pháp lý đính kèm ({docUrls.length})
            </div>
            <ul className="space-y-1">
              {docUrls.map((url, idx) => (
                <li key={url}>
                  <a
                    href={url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1 text-xs text-brand hover:underline break-all"
                  >
                    Tài liệu minh chứng #{idx + 1}
                    <ExternalLinkIcon className="w-3 h-3 shrink-0" />
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}

        {mode === "admin" && profile.verificationNote && (
          <div className="pt-2 border-t text-xs bg-muted/40 p-2.5 rounded-lg">
            <span className="font-semibold text-foreground">Ghi chú kiểm duyệt:</span>{" "}
            <span className="text-muted-foreground">{profile.verificationNote}</span>
          </div>
        )}

        {mode === "admin" && missingItems.length > 0 && (
          <div className="pt-2 border-t">
            <div className="rounded-lg border border-amber-300 bg-amber-50/70 dark:bg-amber-950/30 p-2.5 text-xs text-amber-800 dark:text-amber-200 space-y-1">
              <div className="font-semibold flex items-center gap-1.5">
                <AlertTriangleIcon className="w-3.5 h-3.5 shrink-0" />
                Hồ sơ còn thiếu {missingItems.length} hạng mục thực tế:
              </div>
              <div className="leading-relaxed">{missingItems.join(", ")}.</div>
            </div>
          </div>
        )}

        {showMapsLink && (center.address || (center.latitude && center.longitude)) && (
          <div className="pt-2">
            <a
              href={
                center.latitude && center.longitude
                  ? `https://www.google.com/maps/search/?api=1&query=${center.latitude},${center.longitude}`
                  : `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(
                      `${center.name} ${center.address}`
                    )}`
              }
              target="_blank"
              rel="noopener noreferrer"
              className={cn(
                buttonVariants({ variant: "secondary", size: "sm" }),
                "gap-1.5 text-xs inline-flex items-center"
              )}
            >
              <MapPinIcon className="w-3.5 h-3.5 text-brand" />
              Mở Google Maps chỉ đường
              <ExternalLinkIcon className="w-3 h-3 ml-1" />
            </a>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
