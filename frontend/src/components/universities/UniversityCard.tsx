"use client";

import Link from "next/link";
import Image from "next/image";
import { Building2Icon, ExternalLinkIcon, GraduationCapIcon, MapPinIcon, TrophyIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
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

export function UniversityCard({ university }: UniversityCardProps) {
  const logoUrl = universityUrl(university.logoUrl);
  const coverUrl = universityUrl(university.coverUrl);
  const location = [university.city, university.stateName ?? university.state].filter(Boolean).join(", ");

  const englishReq = university.minIelts
    ? `IELTS ${university.minIelts}+`
    : university.minToefl
      ? `TOEFL ${university.minToefl}+`
      : university.minDuolingo
        ? `DET ${university.minDuolingo}+`
        : null;

  return (
    <Card className="group relative flex flex-col overflow-hidden transition-all duration-200 border-line hover:border-brand/40 bg-card">
      <div className="relative aspect-[16/9] w-full overflow-hidden bg-muted">
        {coverUrl ? (
          <Image
            src={coverUrl}
            alt={university.name}
            fill
            sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
            className="object-cover transition-transform duration-300 group-hover:scale-105"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-brand-wash">
            <span className="font-heading text-4xl font-black tracking-wider text-brand/20 select-none">
              {university.code}
            </span>
          </div>
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-black/20" />

        <div className="absolute left-3 top-3 flex flex-wrap gap-1.5">
          {university.bestRank != null && (
            <Badge className="bg-amber-500/90 text-white font-semibold border-none gap-1">
              <TrophyIcon className="size-3" />
              <span>#{university.bestRank}</span>
              {university.bestRankPublisher && (
                <span className="text-xs opacity-90 hidden sm:inline">({university.bestRankPublisher})</span>
              )}
            </Badge>
          )}
          {university.isIvy && (
            <Badge className="bg-emerald-700/90 text-white font-semibold border-none ">
              Ivy League
            </Badge>
          )}
          {university.control && (
            <Badge variant="secondary" className="bg-black/40 text-white border-white/20">
              {schoolControlLabel(university.control)}
            </Badge>
          )}
        </div>

        <div className="absolute right-3 top-3">
          <AddTargetSchoolButton
            universityId={university.id}
            universityCode={university.code}
            universityName={university.name}
            universityCity={university.city}
            universityState={university.state}
            variant="secondary"
            size="sm"
            className="h-8 bg-black/40 text-white hover:bg-black/60 border-white/20"
          />
        </div>

        <div className="absolute bottom-3 left-3 flex items-center gap-2.5">
          <div className="relative flex size-12 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white p-1 ring-2 ring-white/80">
            {logoUrl ? (
              <Image
                src={logoUrl}
                alt={`${university.name} logo`}
                fill
                sizes="48px"
                className="object-contain p-0.5"
              />
            ) : (
              <Building2Icon className="size-6 text-brand" />
            )}
          </div>
          <div className="min-w-0 text-white">
            <span className="text-xs font-semiboldr text-white/80">
              {university.code}
            </span>
            {location && (
              <p className="flex items-center gap-1 text-xs text-white/90 truncate">
                <MapPinIcon className="size-3 shrink-0" />
                <span className="truncate">{location}</span>
              </p>
            )}
          </div>
        </div>
      </div>

      <CardContent className="flex flex-1 flex-col justify-between p-4 space-y-4">
        <div className="space-y-2">
          <Link
            href={`/university/${encodeURIComponent(university.code)}?from=universities`}
            className="line-clamp-2 font-heading text-lg font-bold text-foreground transition-colors hover:text-brand"
            title={university.name}
          >
            {university.name}
          </Link>

          <div className="grid grid-cols-2 gap-2 rounded-lg bg-muted/40 p-2.5 text-xs">
            <div>
              <span className="text-muted-foreground block text-xs">Học phí/năm:</span>
              <span className="font-semibold text-foreground">
                {university.tuitionUsd != null ? universityMoney(university.tuitionUsd) : "Chưa có"}
              </span>
            </div>
            <div>
              <span className="text-muted-foreground block text-xs">Tỷ lệ nhận:</span>
              <span className="font-semibold text-foreground">
                {university.acceptanceRate != null ? universityPercent(university.acceptanceRate) : "Chưa có"}
              </span>
            </div>
            {englishReq && (
              <div>
                <span className="text-muted-foreground block text-xs">Yêu cầu tiếng Anh:</span>
                <span className="font-medium text-foreground">{englishReq}</span>
              </div>
            )}
            {university.satPolicy && (
              <div>
                <span className="text-muted-foreground block text-xs">SAT/ACT:</span>
                <span className="font-medium text-foreground capitalize">
                  {university.satPolicy === "optional"
                    ? "Tùy chọn"
                    : university.satPolicy === "required"
                      ? "Bắt buộc"
                      : "Không xét"}
                </span>
              </div>
            )}
          </div>

          {university.majors && university.majors.length > 0 && (
            <div className="space-y-1 pt-1">
              <span className="text-xs font-medium text-muted-foreground flex items-center gap-1">
                <GraduationCapIcon className="size-3" />
                Ngành nổi bật:
              </span>
              <div className="flex flex-wrap gap-1">
                {university.majors.slice(0, 3).map((major) => (
                  <Badge
                    key={major}
                    variant="outline"
                    className="text-xs font-normal py-0 px-2 max-w-[200px] truncate"
                  >
                    {major}
                  </Badge>
                ))}
                {university.majors.length > 3 && (
                  <span className="text-xs text-muted-foreground self-center">
                    +{university.majors.length - 3}
                  </span>
                )}
              </div>
            </div>
          )}
        </div>

        <div className="pt-2 border-t border-line/60">
          <Link
            href={`/university/${encodeURIComponent(university.code)}?from=universities`}
            className={cn(
              buttonVariants({ variant: "outline" }),
              "w-full justify-between group-hover:border-brand/60"
            )}
          >
            <span>Xem chi tiết trường</span>
            <ExternalLinkIcon className="size-3.5 text-muted-foreground group-hover:text-brand" />
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
