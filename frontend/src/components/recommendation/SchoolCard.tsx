"use client";

import { ExternalLinkIcon, MapPinIcon, MessageSquareTextIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { PickedSchool, ScoredSchool } from "@/types/recommendation";
import { flagText, formatUsd, groupLabel, groupVariant, siteHost, whyText } from "@/lib/recommendation-labels";

interface SchoolCardProps {
    picked: PickedSchool; // vị trí trong khối + mã lý do (lượt chọn)
    school: ScoredSchool | undefined; // số liệu đã chấm (lượt chấm)
    onExplain: (code: string) => void;
}

// Một thẻ trường. Chỉ hiển thị số đã lưu, không tính lại gì.
export function SchoolCard({ picked, school, onExplain }: SchoolCardProps) {
    const name = school?.name ?? picked.code;
    const control =
        school?.control === "public" ? "Công lập" : school?.control === "private" ? "Tư thục" : null;
    const website = school?.website ?? null;
    const host = siteHost(website);
    return (
        <div className="rounded-2xl border border-line bg-surface p-5 shadow-2xs transition-colors hover:border-brand/40">
            <div className="flex items-start justify-between gap-3">
                <div className="min-w-0 space-y-1">
                    <h3 className="truncate text-sm font-bold text-foreground">{name}</h3>
                    <div className="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
                        {school?.state && (
                            <span className="inline-flex items-center gap-1">
                                <MapPinIcon className="size-3" />
                                {school.state}
                            </span>
                        )}
                        {control && <span>· {control}</span>}
                        <span>· {formatUsd(school?.cost)}/năm</span>
                    </div>
                    {website && host && (
                      <a
                          href={website}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-1 text-xs text-brand underline-offset-4 hover:underline"
                        >
                          {host}
                          <ExternalLinkIcon className="size-3" />
                        </a>
                      )}
                </div>
                {school && <Badge variant={groupVariant(school.g2)}>{groupLabel(school.g2)}</Badge>}
            </div>

            {school?.outcome === "AidBlock" && school.gap != null && (
                <p className="mt-3 text-xs font-medium text-warn">
                    Vượt ngân sách khoảng {formatUsd(school.gap)} mỗi năm
                </p>
            )}

            {picked.why.length > 0 && (
                <ul className="mt-3 space-y-1 text-xs text-ink-2">
                    {picked.why.map((code) => (
                        <li key={code}>• {whyText(code)}</li>
                    ))}
                </ul>
            )}

            {school && school.flags.length > 0 && (
                <p className="mt-2 text-[11px] text-ink-3">{school.flags.map(flagText).join(" · ")}</p>
            )}

            <div className="mt-4 flex items-center justify-between border-t border-line/60 pt-3">
                <span className="text-[11px] text-ink-3">
                    {picked.x != null ? `Điểm xếp hạng ${picked.x.toFixed(3)}` : ""}
                </span>
                <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onExplain(picked.code)}
                    className="h-8 gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
                >
                    <MessageSquareTextIcon className="size-3.5" />
                    Xem tư vấn
                </Button>
            </div>
        </div>
    );
}
