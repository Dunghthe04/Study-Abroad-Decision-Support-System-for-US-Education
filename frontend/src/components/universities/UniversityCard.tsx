"use client";

import Link from "next/link";
import Image from "next/image";
import { Building2Icon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardFooter } from "@/components/ui/card";
import { AddTargetSchoolButton } from "@/components/target-schools/AddTargetSchoolButton";
import {
  schoolControlLabel,
  universityMoney,
  universityPercent,
  universityUrl,
} from "@/lib/university-display";
import type { UniversityListItem } from "@/types/university";
import { cn } from "@/lib/utils";

interface UniversityCardProps {
  university: UniversityListItem;
}

function englishRequirement(university: UniversityListItem): string | null {
  if (university.minIelts) return `IELTS ${university.minIelts}+`;
  if (university.minToefl) return `TOEFL ${university.minToefl}+`;
  if (university.minDuolingo) return `DET ${university.minDuolingo}+`;
  return null;
}

function satPolicyLabel(policy: string | null): string | null {
  if (policy === "optional") return "Tùy chọn";
  if (policy === "required") return "Bắt buộc";
  if (policy === "not_accepted") return "Không xét";
  return null;
}

function StatItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="truncate text-sm font-semibold text-foreground">{value}</dd>
    </div>
  );
}

export function UniversityCard({ university }: UniversityCardProps) {
  const logoUrl = universityUrl(university.logoUrl);
  const coverUrl = universityUrl(university.coverUrl);
  const detailHref = `/university/${encodeURIComponent(university.code)}?from=universities`;

  const location = [university.city, university.stateName ?? university.state].filter(Boolean).join(", ");
  const controlLabel = university.control ? schoolControlLabel(university.control) : null;
  const subtitleParts = [location, controlLabel].filter(Boolean);

  const tuition = university.tuitionUsd != null ? universityMoney(university.tuitionUsd) : "Chưa có";
  const acceptance = university.acceptanceRate != null ? universityPercent(university.acceptanceRate) : "Chưa có";
  const english = englishRequirement(university) ?? "Chưa có";
  const satPolicy = satPolicyLabel(university.satPolicy) ?? "Chưa có";
  const hasBadges = university.bestRank != null || university.isIvy;

  return (
    <Card className="group gap-0 py-0">
      <div className="relative aspect-[4/3] w-full overflow-hidden bg-muted">
        {coverUrl ? (
          <Image
            src={coverUrl}
            alt={university.name}
            fill
            sizes="(max-width: 768px) 100vw, (max-width: 1280px) 50vw, 33vw"
            className="object-cover transition-transform duration-300 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-brand-wash">
            <Building2Icon className="size-10 text-brand/30" />
          </div>
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent" />

        <div className="absolute bottom-3 left-3 right-3 flex items-end gap-3">
          <div className="relative flex size-11 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-white">
            {logoUrl ? (
              <Image src={logoUrl} alt={`Logo ${university.name}`} fill sizes="44px" className="object-contain p-1" />
            ) : (
              <Building2Icon className="size-5 text-brand" />
            )}
          </div>
          <div className="min-w-0">
            <Link
              href={detailHref}
              className="line-clamp-2 font-heading text-lg font-bold leading-snug text-white hover:underline"
              title={university.name}
            >
              {university.name}
            </Link>
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
          <StatItem label="Học phí/năm" value={tuition} />
          <StatItem label="Tỷ lệ nhận" value={acceptance} />
          <StatItem label="Yêu cầu tiếng Anh" value={english} />
          <StatItem label="SAT/ACT" value={satPolicy} />
        </dl>
      </CardContent>

      <CardFooter className="grid grid-cols-2 gap-2 bg-transparent py-3">
        <Link href={detailHref} className={cn(buttonVariants({ variant: "default" }), "w-full")}>
          Xem chi tiết
        </Link>
        <AddTargetSchoolButton
          universityId={university.id}
          universityCode={university.code}
          universityName={university.name}
          universityCity={university.city}
          universityState={university.state}
          variant="outline"
          className="w-full"
        />
      </CardFooter>
    </Card>
  );
}
