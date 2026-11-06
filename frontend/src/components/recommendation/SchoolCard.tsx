"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { ExternalLinkIcon, MapPinIcon, MessageSquareTextIcon, TargetIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { PickedSchool, SchoolChange, ScoredSchool } from "@/types/recommendation";
import { SchoolPhoto } from "./SchoolPhoto";
import {
    flagText,
    formatUsd,
    groupLabel,
    groupVariant,
    siteHost,
    statusLabel,
    whyText,
} from "@/lib/recommendation-labels";

interface SchoolCardProps {
    picked: PickedSchool; // vị trí trong khối + mã lý do
    school: ScoredSchool | undefined; // số liệu đã chấm
    change?: SchoolChange; // có khi đang mô phỏng và trường đổi nhóm so với hồ sơ thật
    onExplain?: (code: string) => void; // bỏ trống khi đang mô phỏng (lời tư vấn chỉ có cho kết quả đã lưu)
    onGoal?: (code: string) => void; // "Cần gì để vào?": luôn tính từ hồ sơ thật, kể cả khi đang xem giả định
    highlighted?: boolean; // đây là trường đang được ghim ở đầu trang (khung "Cần gì để vào?"): viền đỏ để nhận ra
}

// Trường đã ở nhóm An toàn thì không cần hỏi "cần gì để vào"
const GOAL_GROUPS = ["R", "RF", "M", "NA"];

// "35%" từ vị trí 0–100 của học sinh trong số sinh viên đã nhập học; thiếu số thì "–"
const percent = (value: number | null | undefined) => (value == null ? "–" : `${Math.round(value)}%`);

// Một trường dạng hàng ngang như CollegeVine: tên và nơi ở bên trái, các chỉ số ở giữa, nhóm và nút bên phải.
// Chỉ hiển thị số đã tính, không tính lại gì.
export function SchoolCard({ picked, school, change, onExplain, onGoal, highlighted = false }: SchoolCardProps) {
    const name = school?.name ?? picked.code;
    const control =
        school?.control === "public" ? "Công lập" : school?.control === "private" ? "Tư thục" : null;
    const website = school?.website ?? null;
    const host = siteHost(website);

    // Khung ngoài chỉ làm "container": một phần tử không đổi bố cục theo chính bề rộng của nó được,
    // nên thẻ bên trong dùng các lớp @2xl, @4xl... theo bề rộng của khung này (trang có cột mô phỏng và trợ lý nên thẻ hẹp hơn màn hình nhiều)
    return (
        <div className="@container">
        <div
            className={cn(
                "flex flex-col overflow-hidden rounded-2xl border bg-surface shadow-2xs transition-colors @2xl:flex-row",
                highlighted ? "border-risk ring-2 ring-risk/40" : "border-line hover:border-brand/40",
            )}
        >
            {/* Ảnh trường */}
            <SchoolPhoto
                name={name}
                coverUrl={school?.coverUrl}
                logoUrl={school?.logoUrl}
                href={`/university/${encodeURIComponent(picked.code)}?from=recommendations`}
            />

            <div className="grid min-w-0 flex-1 content-start gap-x-8 gap-y-4 p-5 @7xl:grid-cols-[minmax(0,1fr)_minmax(0,1.7fr)_auto]">
                {/* Tên, nơi ở, trang chủ */}
                <div className="min-w-0 space-y-1.5">
                    <h3 className="text-base font-bold text-foreground"><Link href={`/university/${encodeURIComponent(picked.code)}?from=recommendations`} className="hover:text-brand hover:underline">{name}</Link></h3>
                    <div className="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground">
                        {school?.state && (
                            <span className="inline-flex items-center gap-1">
                                <MapPinIcon className="size-3" />
                                {school.state}
                            </span>
                        )}
                        {control && <span>· {control}</span>}
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
                    {change && (
                        <p className="rounded-md bg-brand-wash px-2.5 py-1.5 text-xs font-semibold text-brand">
                            Đổi so với hồ sơ thật: {statusLabel(change.from)} → {statusLabel(change.to)}
                        </p>
                    )}
                </div>

                {/* Chỉ số và lý do */}
                <div className="min-w-0 space-y-3">
                    <div className="grid grid-cols-2 gap-x-4 gap-y-3 @lg:grid-cols-3 @4xl:grid-cols-5">
                        <Stat label="Chi phí / năm" value={school?.cost != null ? formatUsd(school.cost) : "Chưa có số liệu"} small={school?.cost == null} />
                        <Stat
                            label="Tỷ lệ nhận"
                            value={percent(school?.acceptRate == null ? null : school.acceptRate * 100)}
                            hint="số hồ sơ được nhận"
                        />
                        <Stat label="GPA của bạn" value={percent(school?.pg)} hint="cao hơn sinh viên của trường" />
                        <Stat label="SAT của bạn" value={percent(school?.ps)} hint="cao hơn sinh viên của trường" />
                        <Stat label="Điểm phù hợp" value={picked.x != null ? picked.x.toFixed(2) : "–"} hint="xếp thứ tự trong nhóm" />
                    </div>

                    {school?.outcome === "AidBlock" && school.gap != null && (
                        <p className="text-xs font-medium text-warn">
                            Vượt ngân sách khoảng {formatUsd(school.gap)} mỗi năm
                        </p>
                    )}

                    {picked.why.length > 0 && (
                        <ul className="grid gap-x-6 gap-y-1 text-xs text-ink-2 @xl:grid-cols-2">
                            {picked.why.map((code) => (
                                <li key={code}>• {whyText(code)}</li>
                            ))}
                        </ul>
                    )}

                    {school && school.flags.length > 0 && (
                        <p className="text-[11px] text-ink-3">{school.flags.map(flagText).join(" · ")}</p>
                    )}
                </div>

                {/* Nhóm và nút */}
                <div className="flex flex-wrap items-center justify-between gap-3 @7xl:flex-col @7xl:items-end">
                    {school && <Badge variant={groupVariant(school.g2)}>{groupLabel(school.g2)}</Badge>}
                    {onGoal && school && GOAL_GROUPS.includes(school.g2) && (
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onGoal(picked.code)}
                            className="gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
                        >
                            <TargetIcon className="size-3.5" />
                            Cần gì để vào?
                        </Button>
                    )}
                    {onExplain && (
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onExplain(picked.code)}
                            className="gap-1 text-xs font-semibold text-brand hover:bg-brand-wash"
                        >
                            <MessageSquareTextIcon className="size-3.5" />
                            Xem tư vấn
                        </Button>
                    )}
                </div>
            </div>
        </div>
        </div>
    );
}

function Stat({ label, value, hint, small = false }: { label: string; value: ReactNode; hint?: string; small?: boolean }) {
    return (
        <div className="min-w-0">
            <p className={small ? "text-sm font-semibold text-ink-3" : "text-lg font-bold text-foreground"}>{value}</p>
            <p className="text-[11px] font-medium text-ink-2">{label}</p>
            {hint && <p className="text-[10px] text-ink-3">{hint}</p>}
        </div>
    );
}
