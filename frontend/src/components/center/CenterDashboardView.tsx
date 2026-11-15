"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
  Building2Icon,
  ShieldCheckIcon,
  CheckCircle2Icon,
  ArrowRightIcon,
  ExternalLinkIcon,
  PhoneIcon,
  MailIcon
} from "lucide-react";
import { centerPortalApi } from "@/lib/api";
import type { StudyCenter, CenterVerificationStatusDto } from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { CenterStatusBadge } from "@/components/centers/CenterBadges";
import { buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

export function CenterDashboardView() {
  const [center, setCenter] = useState<StudyCenter | null>(null);
  const [status, setStatus] = useState<CenterVerificationStatusDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const [c, s] = await Promise.all([
          centerPortalApi.getProfile(),
          centerPortalApi.getVerificationStatus(),
        ]);
        if (active) {
          setCenter(c);
          setStatus(s);
        }
      } catch (e) {
        console.error("Failed to load center dashboard", e);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center gap-2 text-ink-2 text-sm">
        <Spinner />
        <span>Đang tải thông tin trung tâm...</span>
      </div>
    );
  }

  const isVerified = status?.verificationStatus === "verified";
  const isPending = status?.verificationStatus === "pending_review";
  const isRejected = status?.verificationStatus === "rejected";
  const isSuspended = status?.verificationStatus === "suspended";

  return (
    <div className="space-y-6">
      {/* Header Profile Summary */}
      <div className="rounded-xl border border-line bg-surface p-6 shadow-none">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-start gap-4">
            <div className="h-14 w-14 rounded-xl border border-line bg-surface-2 flex items-center justify-center text-brand shrink-0">
              {center?.logoUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={center.logoUrl}
                  alt={center.name}
                  className="h-full w-full rounded-xl object-cover"
                />
              ) : (
                <Building2Icon className="h-7 w-7" />
              )}
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <h1 className="text-xl font-bold text-ink">
                  {center?.name || "Trung tâm tư vấn"}
                </h1>
                <CenterStatusBadge status={status?.verificationStatus} />
              </div>
              <p className="text-xs text-ink-3 mt-1">
                Mã đơn vị: <span className="font-mono text-ink-2 font-medium">{center?.code || "CTR_---"}</span>
                {center?.city && ` • ${center.city}`}
              </p>
            </div>
          </div>

          {/* Lối vào duy nhất tới xác minh pháp lý; các mục khác đã có ở thanh tab */}
          {!isVerified && !isSuspended && (
            <Link
              href="/center/verification"
              className={cn(buttonVariants({ size: "sm" }), "gap-1.5")}
            >
              {isRejected ? "Cập nhật & nộp lại" : isPending ? "Xem tiến độ" : "Xác minh pháp lý"}
              <ArrowRightIcon className="h-3.5 w-3.5" />
            </Link>
          )}
        </div>
      </div>

      {/* Grid 3 cards: Status & Checklist */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Card 1: 5-Item Checklist (BR-C3) */}
        <div className="md:col-span-2 rounded-xl border border-line bg-surface p-6 space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold text-ink flex items-center gap-2">
              <ShieldCheckIcon className="h-5 w-5 text-brand" />
              Lộ trình cấp dấu xác minh (4 điều kiện bắt buộc)
            </h2>
          </div>

          <div className="divide-y divide-line rounded-lg border border-line bg-surface-2/50 text-sm">
            {/* Điều kiện 1: Email */}
            <div className="p-3.5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="h-6 w-6 rounded-full bg-ok-wash text-ok flex items-center justify-center shrink-0">
                  <CheckCircle2Icon className="h-4 w-4" />
                </div>
                <div>
                  <p className="font-medium text-ink">1. Xác thực Email liên hệ</p>
                  <p className="text-xs text-ink-3">Đã kích hoạt bằng mã OTP qua hộp thư</p>
                </div>
              </div>
              <Badge variant="ok" className="text-xs">
                Hoàn thành
              </Badge>
            </div>

            {/* Điều kiện 3: Tên & MST khớp Cổng ĐKDN */}
            <div className="p-3.5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className={`h-6 w-6 rounded-full flex items-center justify-center shrink-0 ${isVerified ? "bg-ok-wash text-ok" : "bg-surface-3 text-ink-3"}`}>
                  {isVerified ? <CheckCircle2Icon className="h-4 w-4" /> : <span className="text-xs font-mono">2</span>}
                </div>
                <div>
                  <p className="font-medium text-ink">2. Khớp tên & MST trên Cổng ĐKKD Quốc gia</p>
                  <p className="text-xs text-ink-3">Moderator đối chiếu với Cổng dangkykinhdoanh.gov.vn</p>
                </div>
              </div>
              <span className="text-xs text-ink-3">
                {isVerified ? "Đã đối chiếu" : isPending ? "Đang kiểm tra" : "Chưa gửi"}
              </span>
            </div>

            {/* Điều kiện 4: Doanh nghiệp hoạt động bình thường */}
            <div className="p-3.5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className={`h-6 w-6 rounded-full flex items-center justify-center shrink-0 ${isVerified ? "bg-ok-wash text-ok" : "bg-surface-3 text-ink-3"}`}>
                  {isVerified ? <CheckCircle2Icon className="h-4 w-4" /> : <span className="text-xs font-mono">3</span>}
                </div>
                <div>
                  <p className="font-medium text-ink">3. Doanh nghiệp đang hoạt động</p>
                  <p className="text-xs text-ink-3">Trạng thái pháp lý còn hiệu lực tại thời điểm xét duyệt</p>
                </div>
              </div>
              <span className="text-xs text-ink-3">
                {isVerified ? "Đã xác nhận" : isPending ? "Đang kiểm tra" : "Chưa gửi"}
              </span>
            </div>

            {/* Điều kiện 5: Người đại diện / ủy quyền */}
            <div className="p-3.5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className={`h-6 w-6 rounded-full flex items-center justify-center shrink-0 ${isVerified ? "bg-ok-wash text-ok" : "bg-surface-3 text-ink-3"}`}>
                  {isVerified ? <CheckCircle2Icon className="h-4 w-4" /> : <span className="text-xs font-mono">4</span>}
                </div>
                <div>
                  <p className="font-medium text-ink">4. Người đăng ký là đại diện hoặc có ủy quyền</p>
                  <p className="text-xs text-ink-3">Kiểm tra qua ĐKKD hoặc Giấy ủy quyền đính kèm</p>
                </div>
              </div>
              <span className="text-xs text-ink-3">
                {isVerified ? "Hợp lệ" : isPending ? "Đang kiểm tra" : "Chưa gửi"}
              </span>
            </div>
          </div>
        </div>

        {/* Card 2: Quick Contact & Status Summary */}
        <div className="rounded-xl border border-line bg-surface p-6 space-y-4">
          <div className="space-y-4">
            <h3 className="text-base font-semibold text-ink">Thông tin liên lạc</h3>

            <div className="space-y-3 text-sm">
              <div className="flex items-center gap-2.5 text-ink-2">
                <MailIcon className="h-4 w-4 text-ink-3" />
                <span className="truncate">{center?.email || "—"}</span>
              </div>
              <div className="flex items-center gap-2.5 text-ink-2">
                <PhoneIcon className="h-4 w-4 text-ink-3" />
                <span>{center?.phone || "—"}</span>
              </div>
              {center?.website && (
                <div className="flex items-center gap-2.5 text-brand">
                  <ExternalLinkIcon className="h-4 w-4 shrink-0" />
                  <a
                    href={center.website}
                    target="_blank"
                    rel="noreferrer"
                    className="truncate hover:underline"
                  >
                    {center.website}
                  </a>
                </div>
              )}
            </div>

            <div className="border-t border-line pt-4 space-y-2">
              <p className="text-xs text-ink-3 font-medium uppercase tracking-wider">
                Quyền lợi khi hoàn tất xác minh
              </p>
              <ul className="text-xs text-ink-2 space-y-1.5 list-disc pl-4">
                <li>Hiển thị dấu xác minh và được xếp ưu tiên trên danh bạ trung tâm</li>
                <li>Nhận trực tiếp yêu cầu tư vấn từ học sinh và phụ huynh</li>
                <li>Được trợ lý AI gợi ý cho học sinh phù hợp</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
