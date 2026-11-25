"use client";

import React from "react";
import Link from "next/link";
import type { AcademicProfileResponse } from "@/types/api";
import { AccountSectionHeader } from "@/components/account/AccountSectionHeader";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyTitle,
} from "@/components/ui/empty";
import { Skeleton } from "@/components/ui/skeleton";
import { BookOpenIcon, CheckIcon, PencilIcon, RocketIcon } from "lucide-react";
import { ProfileReviewCards, normalizeProfileDetails } from "@/components/flow/ProfileReviewCards";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { editProfileHref } from "@/lib/form-goals";
import { useFetched } from "@/lib/use-fetched";

// Nhãn bậc học
const targetLevelLabels: Record<string, string> = {
  community_college: "Cao đẳng cộng đồng 2+2 (Community College)",
  undergraduate: "Đại học 4 năm (Undergraduate)",
  master: "Thạc sĩ (Master)",
  phd: "Tiến sĩ (PhD)",
};

// Backend tính isComplete giống luồng 6 bước: trường học không bắt buộc, chế độ GPA tổng không có bảng điểm theo kỳ
export function hasCompleteAcademicProfile(profile: AcademicProfileResponse | null): boolean {
  return Boolean(profile?.isComplete);
}

interface AcademicSummaryCardProps {
  profile: AcademicProfileResponse | null;
  isLoading: boolean;
  /** Phụ huynh xem hồ sơ con: ẩn nút sửa/phân tích, đổi tiêu đề sang tên con */
  readOnly?: boolean;
  title?: string;
}

// Tóm tắt bảng điểm & hồ sơ học thuật cho học sinh / phụ huynh
export function AcademicSummaryCard({ profile, isLoading, readOnly = false, title }: AcademicSummaryCardProps) {
  // Nhãn bang, ngành, mức ngân sách, chứng chỉ để hiện đủ 5 bước giống bước 6
  const metadata = useFetched("profile-metadata", () => profileFlowApi.getMetadata());
  const details = normalizeProfileDetails(profile?.details);

  const renderHeader = (opts: {
    heading: string;
    description?: string;
    badge?: React.ReactNode;
    actions?: React.ReactNode;
  }) => {
    if (!readOnly) {
      return (
        <AccountSectionHeader
          title={opts.heading}
          description={opts.description}
          badge={opts.badge}
          actions={opts.actions}
        />
      );
    }
    return (
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div className="space-y-0.5">
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="font-heading text-lg font-semibold text-ink">{opts.heading}</h2>
            {opts.badge}
          </div>
          {opts.description && <p className="text-xs text-ink-2">{opts.description}</p>}
        </div>
      </div>
    );
  };

  if (isLoading) {
    return (
      <div>
        {!readOnly && (
          <AccountSectionHeader
            title="Học bạ"
            description="Tổng hợp bảng điểm, chứng chỉ, ngân sách và hoạt động ngoại khóa của bạn."
          />
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          <Skeleton className="h-36 rounded-2xl" />
          <Skeleton className="h-36 rounded-2xl" />
          <Skeleton className="h-32 rounded-2xl" />
          <Skeleton className="h-32 rounded-2xl" />
        </div>
      </div>
    );
  }

  if (readOnly && !hasCompleteAcademicProfile(profile)) {
    return (
      <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
        <div className="flex items-center gap-2.5">
          <BookOpenIcon className="size-5 text-brand" aria-hidden />
          <h2 className="font-heading text-lg font-semibold text-ink">{title ?? "Học bạ"}</h2>
        </div>
        <p className="mt-1.5 text-sm text-ink-2">Con chưa hoàn thành hồ sơ 6 bước.</p>
      </div>
    );
  }

  if (!hasCompleteAcademicProfile(profile)) {
    return (
      <div>
        <AccountSectionHeader
          title="Học bạ"
          description="Hoàn thành hồ sơ 6 bước để hệ thống lưu trữ bảng điểm và phân tích cơ hội trúng tuyển."
          badge={<Badge variant="warn">Chưa hoàn tất</Badge>}
        />
        <Empty className="py-16">
          <EmptyHeader>
            <EmptyTitle className="text-base font-semibold text-ink">
              Bảng điểm và hồ sơ chưa được thiết lập
            </EmptyTitle>
            <EmptyDescription>
              Bạn chưa hoàn thành hồ sơ 6 bước (GPA, chứng chỉ, ngành dự định, ngân sách...). Hãy hoàn thành để hệ thống lưu trữ và tư vấn chính xác hơn.
            </EmptyDescription>
          </EmptyHeader>
          <EmptyContent>
            <Link href="/analysis" className={buttonVariants({ size: "lg" })}>
              <RocketIcon data-icon="inline-start" aria-hidden />
              Điền bảng điểm ngay
            </Link>
          </EmptyContent>
        </Empty>
      </div>
    );
  }

  // Danh sách chứng chỉ có điểm
  const activeCertificates: { name: string; score: string | number }[] = [];
  if (profile?.ielts != null) activeCertificates.push({ name: "IELTS", score: profile.ielts });
  if (profile?.toefl != null) activeCertificates.push({ name: "TOEFL iBT", score: profile.toefl });
  if (profile?.duolingo != null) activeCertificates.push({ name: "Duolingo", score: profile.duolingo });
  if (profile?.sat != null) activeCertificates.push({ name: "SAT", score: profile.sat });
  if (profile?.act != null) activeCertificates.push({ name: "ACT", score: profile.act });
  if (profile?.gre != null) activeCertificates.push({ name: "GRE", score: profile.gre });
  if (profile?.gmat != null) activeCertificates.push({ name: "GMAT", score: profile.gmat });

  const levelText = profile?.targetLevel
    ? targetLevelLabels[profile.targetLevel] || profile.targetLevel
    : "Đại học";
  const gradeText = profile?.currentGrade || "Lớp 11";

  return (
    <div>
      {renderHeader({
        heading: title ?? "Học bạ",
        description: `${levelText} · ${gradeText}`,
        badge: (
          <Badge variant="ok">
            <CheckIcon aria-hidden />
            Đã cập nhật
          </Badge>
        ),
        actions: !readOnly ? (
          <>
            <Link
              href={editProfileHref("/account#academic")}
              className={buttonVariants({ variant: "outline", size: "lg", className: "bg-surface" })}
            >
              <PencilIcon data-icon="inline-start" aria-hidden />
              Sửa hồ sơ
            </Link>
            <Link href="/analysis" className={buttonVariants({ size: "lg" })}>
              <RocketIcon data-icon="inline-start" aria-hidden />
              Phân tích năng lực
            </Link>
          </>
        ) : undefined,
      })}

      {details ? (
        /* Đủ 5 bước của hồ sơ đã gửi, hiển thị trực tiếp trên nền trang (không lồng thẻ) */
        <ProfileReviewCards draft={details} metadata={metadata.data} />
      ) : (
        <div className="space-y-4">
          {/* Chi tiết học vấn & GPA theo thang trường */}
          <div className="grid grid-cols-1 divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface sm:grid-cols-3 sm:divide-x sm:divide-y-0">
            <div className="p-5">
              <span className="block text-label">Trường học</span>
              <span className="mt-1 block truncate font-semibold text-ink">
                {profile?.currentSchool?.trim() || "Chưa nhập"}
              </span>
              <span className="text-xs text-ink-2">TN: {profile?.graduationYear || "Chưa rõ"}</span>
            </div>

            <div className="p-5">
              <span className="block text-label">Khối lớp / Bậc học</span>
              <span className="mt-1 block truncate font-semibold text-ink">
                {profile?.currentGrade || "Chưa nhập"}
              </span>
              <span className="text-xs text-ink-2">
                {profile?.targetLevel ? targetLevelLabels[profile.targetLevel] || profile.targetLevel : ""}
              </span>
            </div>

            <div className="p-5">
              <span className="block text-label">Điểm trung bình (GPA)</span>
              <span className="mt-1 block text-lg text-numeric text-ink">
                {profile?.overallGpa != null ? profile.overallGpa.toFixed(2) : "Chưa nhập"}{" "}
                {/* overallGpa luôn là GPA thang 4 server đã quy đổi; gradeScale là thang học sinh nhập */}
                <span className="text-xs font-normal text-ink-2">(Thang 4.0)</span>
              </span>
              {profile?.overallGpa != null && profile.gradeScale === "4" && (
                <span className="block text-xs text-ink-2">GPA thang 10: Chưa có dữ liệu</span>
              )}
              {profile?.overallGpa != null && profile.gradeScale !== "4" && (
                <span className="block text-xs text-ink-2">
                  Quy đổi tham khảo từ {profile.gradeScale === "letter" ? "thang chữ" : `thang ${profile.gradeScale}`} (không thay cho đánh giá chính thức của trường)
                </span>
              )}
            </div>
          </div>

          {/* Tiến độ các học kỳ & chứng chỉ */}
          <div className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            <div className="p-5">
              {profile?.terms && profile.terms.length > 0 ? (
                <>
                  <span className="mb-2.5 block text-label">
                    Tiến độ bảng điểm ({profile.terms.length} kỳ đã nhập)
                  </span>
                  <div className="flex flex-wrap gap-2">
                    {profile.terms.map((term, idx) => (
                      <Badge key={idx} variant="secondary">
                        <span>{term.termName}</span>
                        <span>({term.scores.length} môn)</span>
                      </Badge>
                    ))}
                  </div>
                </>
              ) : (
                <>
                  <span className="mb-1.5 block text-label">Bảng điểm</span>
                  <p className="text-xs text-ink-2">Đã nhập GPA tổng, không nhập điểm theo từng học kỳ.</p>
                </>
              )}
            </div>

            <div className="p-5">
              <span className="mb-2.5 block text-label">Chứng chỉ chuẩn hóa & Ngoại ngữ</span>
              {activeCertificates.length > 0 ? (
                <div className="flex flex-wrap gap-2">
                  {activeCertificates.map((c, idx) => (
                    <Badge key={idx} variant="brand">
                      <span>{c.name}:</span>
                      <span className="text-numeric">{c.score}</span>
                    </Badge>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-ink-2">
                  Chưa nhập chứng chỉ nào (IELTS, TOEFL, SAT, GRE...). Bạn có thể cập nhật thêm khi có điểm.
                </p>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
