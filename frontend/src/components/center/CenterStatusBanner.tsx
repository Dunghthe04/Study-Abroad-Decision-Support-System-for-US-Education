"use client";

import React, { useEffect, useState } from "react";
import {
  AlertTriangleIcon,
  CheckCircle2Icon,
  ClockIcon,
  ShieldAlertIcon,
  XCircleIcon,
  type LucideIcon,
} from "lucide-react";
import { centerPortalApi } from "@/lib/api";
import type { CenterVerificationStatusDto } from "@/types/api";
import { cn } from "@/lib/utils";

interface BannerTone {
  icon: LucideIcon;
  title: string;
  detail?: string;
  className: string;
  iconClassName: string;
}

function toneFor(status: CenterVerificationStatusDto): BannerTone {
  switch (status.verificationStatus?.toLowerCase()) {
    case "verified":
      return {
        icon: CheckCircle2Icon,
        title: "Đã xác minh thông tin đăng ký doanh nghiệp",
        detail: "Trung tâm đang hiển thị trên danh bạ và nhận được yêu cầu tư vấn.",
        className: "border-ok/25 bg-ok-wash text-ok",
        iconClassName: "text-ok",
      };
    case "pending_review":
      return {
        icon: ClockIcon,
        title: "Hồ sơ đang chờ duyệt",
        detail: "Moderator đang đối chiếu thông tin pháp lý với Cổng ĐKKD quốc gia.",
        className: "border-brand/20 bg-brand-wash text-brand-ink",
        iconClassName: "text-brand",
      };
    case "rejected": {
      const needsChanges = status.currentRequest?.status === "changes_requested";
      return {
        icon: XCircleIcon,
        title: needsChanges ? "Hồ sơ cần bổ sung" : "Hồ sơ xác minh chưa được duyệt",
        // Lý do do moderator nhập; không tự thêm nội dung mặc định
        detail: status.rejectionReason ? `${needsChanges ? "Yêu cầu" : "Lý do"}: ${status.rejectionReason}` : undefined,
        className: "border-risk/25 bg-risk-wash text-risk",
        iconClassName: "text-risk",
      };
    }
    case "suspended":
      return {
        icon: ShieldAlertIcon,
        title: "Tài khoản trung tâm đang bị tạm ngừng",
        detail: status.rejectionReason
          ? `Lý do: ${status.rejectionReason}`
          : "Vui lòng liên hệ ban quản trị để được hỗ trợ.",
        className: "border-line bg-ground text-ink-2",
        iconClassName: "text-ink-3",
      };
    default:
      return {
        icon: AlertTriangleIcon,
        title: "Chưa xác minh",
        detail: "Trung tâm chưa hiển thị trên danh bạ và chưa nhận được yêu cầu tư vấn từ học sinh.",
        className: "border-warn/25 bg-warn-wash text-warn",
        iconClassName: "text-warn",
      };
  }
}

/**
 * Banner trạng thái xác minh dùng chung cho cổng trung tâm. Chỉ thông báo, không có nút:
 * lối đi tới xác minh nằm ở thanh tab và nút chính ở trang tổng quan.
 */
export function CenterStatusBanner() {
  const [status, setStatus] = useState<CenterVerificationStatusDto | null>(null);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const res = await centerPortalApi.getVerificationStatus();
        if (active) setStatus(res);
      } catch {
        // trung tâm chưa cấu hình hoặc chưa đăng nhập
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  if (!status) return null;

  const { icon: Icon, title, detail, className, iconClassName } = toneFor(status);

  return (
    <div className={cn("border-b px-4 py-2.5 text-sm", className)} role="status">
      <div className="container mx-auto flex items-center gap-2">
        <Icon className={cn("h-4 w-4 shrink-0", iconClassName)} />
        <span className="font-medium">{title}</span>
        {detail && <span className="hidden sm:inline text-xs opacity-90">— {detail}</span>}
      </div>
    </div>
  );
}
