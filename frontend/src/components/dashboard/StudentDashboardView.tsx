"use client";

import { CalendarClockIcon, CheckCircle2Icon, ClipboardCheckIcon, DatabaseIcon, GraduationCapIcon, XCircleIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import { Progress } from "@/components/ui/progress";
import type { StudentDashboard } from "@/types/dashboard";
import { KpiCard } from "./KpiCard";

export interface DashboardLinks {
  /** null: không hiện link sửa hồ sơ (phụ huynh chỉ xem) */
  profile: string | null;
  recommendations: string;
  targetSchools: string;
}

const STUDENT_LINKS: DashboardLinks = {
  profile: "/profile/academic",
  recommendations: "/recommendations",
  targetSchools: "/target-schools",
};

// Cùng tên và màu nhóm với màn kết quả gợi ý / lịch sử gợi ý
const GROUPS = [
  { key: "reach", label: "Khó vào", tone: "text-reach" },
  { key: "match", label: "Vừa sức", tone: "text-brand" },
  { key: "safety", label: "An toàn hơn", tone: "text-ok" },
  { key: "aid", label: "Cần học bổng", tone: "text-warn" },
] as const;

function EmptyNote({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed border-line bg-ground p-4 text-center">
      <p className="text-sm font-medium text-foreground">{title}</p>
      <p className="mt-1 text-xs text-muted-foreground">{children}</p>
    </div>
  );
}

interface StudentDashboardViewProps {
  dashboard: StudentDashboard;
  /** Phụ huynh xem con thì link trỏ sang các tab của con, không sang trang của chính phụ huynh */
  links?: DashboardLinks;
}

/** UC-94: 4 thẻ chỉ số của học sinh. Trang /dashboard và tab Tổng quan của phụ huynh dùng chung. */
export function StudentDashboardView({ dashboard, links = STUDENT_LINKS }: StudentDashboardViewProps) {
  const { recommendation, profileProgress, upcomingDeadlines, dataCompleteness } = dashboard;
  const progressPct = profileProgress.totalSteps > 0
    ? Math.round((profileProgress.completedSteps / profileProgress.totalSteps) * 100)
    : 0;
  const dataFull = dataCompleteness.filledCount >= dataCompleteness.totalCount;

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      {/* 1. Tiến độ hồ sơ */}
      <KpiCard
        title="Tiến độ hồ sơ"
        icon={<ClipboardCheckIcon />}
        badge={profileProgress.hasCommittedProfile
          ? { text: "Đã lưu hồ sơ", variant: "ok" }
          : { text: "Chưa lưu", variant: "warn" }}
        explanation={profileProgress.explanation}
        action={links.profile
          ? { label: profileProgress.hasCommittedProfile ? "Xem hồ sơ" : "Tiếp tục nhập hồ sơ", href: links.profile }
          : undefined}
      >
        <div className="space-y-3">
          <div className="flex items-baseline justify-between gap-2">
            <p>
              <span className="text-numeric text-3xl font-bold text-foreground">{profileProgress.completedSteps}</span>
              <span className="text-sm text-muted-foreground"> / {profileProgress.totalSteps} bước đã xong</span>
            </p>
            <span className="text-numeric text-sm font-semibold text-foreground">{progressPct}%</span>
          </div>
          <Progress value={progressPct} aria-label="Tiến độ hồ sơ" />
        </div>
      </KpiCard>

      {/* 2. Mức đầy đủ dữ liệu */}
      <KpiCard
        title="Dữ liệu cho gợi ý"
        icon={<DatabaseIcon />}
        badge={{ text: `${dataCompleteness.filledCount}/${dataCompleteness.totalCount} mục`, variant: dataFull ? "ok" : "warn" }}
        explanation={dataCompleteness.explanation}
        action={dataFull || !links.profile ? undefined : { label: "Bổ sung hồ sơ", href: links.profile }}
      >
        <ul className="divide-y divide-line">
          {dataCompleteness.items.map((item) => (
            <li key={item.key} className="flex items-center justify-between gap-2 py-1.5 text-sm">
              <span className="text-foreground">{item.label}</span>
              {item.isFilled ? (
                <span className="inline-flex items-center gap-1 text-ok">
                  <CheckCircle2Icon className="size-4" aria-hidden /> Đã có
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-muted-foreground">
                  <XCircleIcon className="size-4" aria-hidden /> Chưa có
                </span>
              )}
            </li>
          ))}
        </ul>
      </KpiCard>

      {/* 3. Gợi ý mới nhất */}
      <KpiCard
        title="Lần gợi ý mới nhất"
        icon={<GraduationCapIcon />}
        badge={recommendation.hasRun ? { text: `${recommendation.total} trường`, variant: "brand" } : { text: "Chưa chạy", variant: "neutral" }}
        explanation={recommendation.explanation}
        action={{ label: recommendation.hasRun ? "Xem kết quả gợi ý" : "Chạy gợi ý trường", href: links.recommendations }}
      >
        {recommendation.hasRun ? (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {GROUPS.map((g) => (
              <div key={g.key} className="rounded-lg border border-line bg-ground p-2.5 text-center">
                <div className="text-label">{g.label}</div>
                <div className={`text-numeric text-2xl font-bold ${g.tone}`}>{recommendation[g.key]}</div>
              </div>
            ))}
          </div>
        ) : (
          <EmptyNote title="Chưa có kết quả gợi ý">
            Hoàn thiện hồ sơ rồi bấm &quot;Gợi ý trường&quot; để có danh sách theo từng nhóm.
          </EmptyNote>
        )}
      </KpiCard>

      {/* 4. Hạn nộp sắp tới */}
      <KpiCard
        title="Hạn nộp sắp tới"
        icon={<CalendarClockIcon />}
        badge={{ text: `${upcomingDeadlines.totalTargetSchools} trường mục tiêu`, variant: "neutral" }}
        explanation={upcomingDeadlines.explanation}
        action={{ label: "Mở trường mục tiêu", href: links.targetSchools }}
      >
        {upcomingDeadlines.upcoming.length > 0 ? (
          <div className="space-y-2">
            <ul className="divide-y divide-line">
              {upcomingDeadlines.upcoming.map((s) => {
                const days = s.daysRemaining ?? 0;
                const variant = days <= 7 ? "risk" : days <= 30 ? "warn" : "neutral";
                return (
                  <li key={s.targetSchoolId} className="flex items-center justify-between gap-3 py-2 first:pt-0">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-foreground">{s.universityName}</p>
                      <p className="text-xs text-muted-foreground">
                        {s.applicationRound || "Chưa chọn vòng"}
                        {s.deadline && ` · ${new Date(s.deadline).toLocaleDateString("vi-VN")}`}
                      </p>
                    </div>
                    <Badge variant={variant}>{days === 0 ? "Hôm nay" : `Còn ${days} ngày`}</Badge>
                  </li>
                );
              })}
            </ul>
            {upcomingDeadlines.undecidedRoundCount > 0 && (
              <p className="text-xs text-warn">
                {upcomingDeadlines.undecidedRoundCount} trường chưa chọn vòng nộp nên chưa có hạn.
              </p>
            )}
          </div>
        ) : (
          <EmptyNote title="Không có hạn nộp sắp tới">
            {upcomingDeadlines.totalTargetSchools === 0
              ? "Bạn chưa thêm trường nào vào danh sách mục tiêu."
              : "Các trường chưa chọn vòng nộp, hoặc hạn nộp đã qua."}
          </EmptyNote>
        )}
      </KpiCard>
    </div>
  );
}
