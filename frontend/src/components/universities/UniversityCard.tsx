"use client";

import Link from "next/link";
import Image from "next/image";
import { Building2Icon } from "lucide-react";
import { AddTargetSchoolButton } from "@/components/target-schools/AddTargetSchoolButton";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  MISSING_UNIVERSITY_DATA,
  schoolControlLabel,
  universityMoney,
  universityPercent,
  universityUrl,
} from "@/lib/university-display";
import { useAuth } from "@/contexts/AuthContext";
import type { UniversityListItem } from "@/types/university";
import { cn } from "@/lib/utils";

interface UniversityCardProps {
  /** Cách xếp đang chọn: xếp theo tổng chi phí thì thẻ hiện tổng chi phí để con số khớp thứ tự (không thì học phí) */
  sortBy?: string;
  university: UniversityListItem;
}

const presentOrNull = (s: string) => (s === MISSING_UNIVERSITY_DATA ? null : s);

function englishRequirement(university: UniversityListItem): string | null {
  if (university.minIelts) return `IELTS ${Number(university.minIelts).toFixed(1)}+`;
  if (university.minToefl) return `TOEFL ${university.minToefl}+`;
  if (university.minDuolingo) return `DET ${university.minDuolingo}+`;
  return null;
}

function testPolicyLabel(policy: string | null): string | null {
  if (policy === "optional") return "Không bắt buộc";
  if (policy === "required") return "Bắt buộc";
  if (policy === "not_accepted") return "Không nhận điểm";
  return null;
}

function StatItem({ label, value, emphasize = false }: { label: string; value: string | null; emphasize?: boolean }) {
  const missing = value == null;
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd
        className={cn(
          "truncate text-sm font-semibold",
          missing ? "font-normal text-muted-foreground/40" : emphasize ? "text-brand" : "text-foreground",
        )}
      >
        {value ?? "-"}
      </dd>
    </div>
  );
}

export function UniversityCard({ university, sortBy }: UniversityCardProps) {
  const { user } = useAuth();
  // Học sinh lưu trường vào mục tiêu; phụ huynh gợi ý cho con. Khách, trung tâm và quản trị không có nút này
  const canSave = user?.role === "student" || user?.role === "parent";
  const logoUrl = universityUrl(university.logoUrl);
  const coverUrl = universityUrl(university.coverUrl);
  const detailHref = `/university/${encodeURIComponent(university.code)}?from=universities`;

  const location = [university.city, university.stateName ?? university.state].filter(Boolean).join(", ");
  const controlLabel = university.control ? schoolControlLabel(university.control) : null;
  const subtitleParts = [location, controlLabel].filter(Boolean);

  const byTotalCost = sortBy === "total_cost_asc" || sortBy === "total_cost_desc";
  const byTuition = sortBy === "tuition_asc" || sortBy === "tuition_desc";
  const tuition = presentOrNull(universityMoney(university.tuitionUsd));
  const totalCost = presentOrNull(universityMoney(university.totalCostUsd));
  const acceptance = presentOrNull(universityPercent(university.acceptanceRate));
  const english = englishRequirement(university);
  const satPolicy = testPolicyLabel(university.satPolicy);
  const actPolicy = testPolicyLabel(university.actPolicy);
  const hasBadges = university.bestRank != null || university.isIvy;

  return (
    <div className="relative h-full">
    {/* Nút lưu nằm ngoài <Link> để không lồng điều khiển trong liên kết */}
    {canSave && (
      <div className="absolute top-3 right-3 z-10">
        <AddTargetSchoolButton
          iconOnly
          universityId={university.id}
          universityCode={university.code}
          universityName={university.name}
          universityCity={university.city}
          universityState={university.stateName ?? university.state}
        />
      </div>
    )}
    <Link
      href={detailHref}
      className="group block h-full rounded-xl outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
    >
      <Card className="h-full gap-0 py-0 transition-[box-shadow,ring-color] duration-150 group-hover:ring-brand/45 group-hover:shadow-sm">
        <div className="relative aspect-[4/3] w-full overflow-hidden bg-muted">
          {coverUrl ? (
            <Image
              src={coverUrl}
              alt={university.name}
              fill
              sizes="(max-width: 768px) 100vw, (max-width: 1280px) 50vw, 33vw"
              className="object-cover"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center bg-brand-wash">
              <Building2Icon className="size-10 text-brand/30" />
            </div>
          )}

          <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent" />

          <div className="absolute bottom-3 left-3 right-3 flex items-end gap-3">
            <div className="relative flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white">
              {logoUrl ? (
                <Image src={logoUrl} alt={`Logo ${university.name}`} fill sizes="64px" className="object-contain p-1.5" />
              ) : (
                <Building2Icon className="size-7 text-brand" />
              )}
            </div>
            <div className="min-w-0">
              <span
                className="line-clamp-2 font-heading text-lg font-bold leading-snug text-white group-hover:underline"
                title={university.name}
              >
                {university.name}
              </span>
              {subtitleParts.length > 0 && (
                <p className="truncate text-xs text-white/85">{subtitleParts.join(" · ")}</p>
              )}
            </div>
          </div>
        </div>

        <CardContent className="flex flex-1 flex-col gap-3 py-4">
          {hasBadges && (
            <div className="flex flex-wrap gap-1.5">
              {university.bestRank != null && (
                <Badge variant="brand" title={university.bestRankList ?? undefined}>
                  #{university.bestRank}
                  {university.bestRankList && <span className="font-normal">{university.bestRankList}</span>}
                </Badge>
              )}
              {university.isIvy && <Badge variant="brand">Ivy League</Badge>}
            </div>
          )}

          <dl className="grid grid-cols-2 gap-x-4 gap-y-2">
            <StatItem label="Học phí/năm" value={tuition} emphasize={!byTotalCost && byTuition} />
            <StatItem label="Tổng chi phí/năm" value={totalCost} emphasize={byTotalCost} />
            <StatItem label="Tỷ lệ nhận" value={acceptance} />
            <StatItem label="Yêu cầu tiếng Anh" value={english} />
            <StatItem label="SAT" value={satPolicy} />
            <StatItem label="ACT" value={actPolicy} />
          </dl>
        </CardContent>
      </Card>
    </Link>
    </div>
  );
}
