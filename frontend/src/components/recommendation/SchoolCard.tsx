"use client";

import { useStateNames } from "@/lib/us-state-names";
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
    student?: { sat: number | null; gpa: number | null }; // SAT và GPA của học sinh trong lượt này, để đặt cạnh khoảng của trường
}

// Trường đã ở nhóm An toàn thì không cần hỏi "cần gì để vào"
const GOAL_GROUPS = ["R", "RF", "M", "NA"];

// Một trường dạng hàng ngang như CollegeVine: tên và nơi ở bên trái, các chỉ số ở giữa, nhóm và nút bên phải.
// Chỉ hiển thị số đã tính, không tính lại gì.
export function SchoolCard({ picked, school, change, onExplain, onGoal, student }: SchoolCardProps) {
    const stateName = useStateNames();
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
                "flex flex-col overflow-hidden rounded-2xl border border-line bg-surface shadow-2xs transition-colors hover:border-brand/40 @2xl:flex-row",
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
                                {stateName(school.state)}
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
                    <div className="grid grid-cols-2 gap-x-4 gap-y-3 @4xl:grid-cols-4">
                        <CostStat school={school} />
                        <AcceptStat school={school} />
                        <SatStat school={school} studentSat={student?.sat ?? null} />
                        <GpaStat pg={school?.pg ?? null} studentGpa={student?.gpa ?? null} topShare={school?.gpaTopShare ?? null} />
                    </div>

                    {keyReasons(picked.why, school).length > 0 && (
                        <ul className="space-y-1 text-xs text-ink-2">
                            {keyReasons(picked.why, school).map((reason) => (
                                <li key={reason}>• {reason}</li>
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
                            Vì sao nhóm này?
                        </Button>
                    )}
                </div>
            </div>
        </div>
        </div>
    );
}

// ---------------------------------------------------------------- các chỉ số trên thẻ
// Nói bằng số học sinh hiểu được (khoảng điểm của trường, số của bạn), không dùng phần trăm trừu tượng dễ hiểu nhầm là xác suất đỗ

function Stat({ label, value, note, tone = "muted" }: { label: string; value: ReactNode; note?: ReactNode; tone?: "muted" | "ok" | "warn" }) {
    return (
        <div className="min-w-0 space-y-0.5">
            <p className="text-[11px] font-medium text-ink-2">{label}</p>
            <p className="text-lg leading-tight font-bold text-foreground">{value}</p>
            {note && (
                <p className={cn("text-[11px]", tone === "ok" ? "text-ok" : tone === "warn" ? "font-medium text-warn" : "text-ink-3")}>
                    {note}
                </p>
            )}
        </div>
    );
}

function CostStat({ school }: { school: ScoredSchool | undefined }) {
    if (school?.cost == null) return <Stat label="Chi phí mỗi năm" value={<span className="text-sm text-ink-3">Chưa có số liệu</span>} />;
    const over = school.outcome === "AidBlock";
    return (
        <Stat
            label="Chi phí mỗi năm"
            value={formatUsd(school.cost)}
            note={over && school.gap != null ? `Vượt ngân sách khoảng ${formatUsd(school.gap)}` : "Trong ngân sách của bạn"}
            tone={over ? "warn" : "ok"}
        />
    );
}

function AcceptStat({ school }: { school: ScoredSchool | undefined }) {
    const rate = school?.acceptRate;
    if (rate == null) return <Stat label="Tỷ lệ nhận" value={<span className="text-sm text-ink-3">Chưa có số liệu</span>} />;
    const intl = school?.intlAcceptRate;
    return (
        <Stat
            label="Tỷ lệ nhận"
            value={pct(rate)}
            note={intl != null ? `Du học sinh: ${pct(intl)}` : "Số hồ sơ được nhận"}
            tone={intl != null && rate > 0 && intl / rate < 0.5 ? "warn" : "muted"}
        />
    );
}

const pct = (fraction: number) => `${Math.round(fraction * 1000) / 10}%`;

// SAT: số của bạn, khoảng của trường và một thanh nhỏ cho thấy bạn đứng ở đâu. Trường chưa có khoảng hợp lệ thì nói chưa có
function SatStat({ school, studentSat }: { school: ScoredSchool | undefined; studentSat: number | null }) {
    const p25 = school?.sat25 ?? null;
    const p75 = school?.sat75 ?? null;
    if (p25 == null || p75 == null) {
        return <Stat label="SAT" value={<span className="text-sm text-ink-3">Chưa có số liệu</span>} note={studentSat != null ? `Bạn: ${studentSat}` : undefined} />;
    }
    if (studentSat == null) {
        return <Stat label="SAT của trường" value={`${p25}–${p75}`} note="Bạn chưa có điểm SAT" />;
    }
    const where = studentSat < p25 ? "Dưới mức thường của trường" : studentSat > p75 ? "Trên mức thường của trường" : "Trong mức thường của trường";
    return (
        <div className="min-w-0 space-y-1">
            <p className="text-[11px] font-medium text-ink-2">SAT: bạn {studentSat}</p>
            <p className="text-sm font-bold text-foreground">Trường {p25}–{p75}</p>
            <SatBar student={studentSat} p25={p25} p75={p75} />
            <p className={cn("text-[11px]", studentSat < p25 ? "font-medium text-warn" : "text-ink-3")}>{where}</p>
        </div>
    );
}

// Thanh từ (mốc 25% − 150) đến (mốc 75% + 150): đoạn đậm là khoảng của trường, chấm là điểm của bạn
function SatBar({ student, p25, p75 }: { student: number; p25: number; p75: number }) {
    const lo = Math.max(400, p25 - 150);
    const hi = Math.min(1600, p75 + 150);
    const at = (v: number) => `${Math.min(100, Math.max(0, ((v - lo) / (hi - lo)) * 100))}%`;
    return (
        <div
            className="relative h-2 w-full max-w-[160px] rounded-full bg-line"
            role="img"
            aria-label={`SAT của bạn ${student}, khoảng của trường ${p25} đến ${p75}`}
        >
            <div className="absolute inset-y-0 rounded-full bg-brand/40" style={{ left: at(p25), width: `calc(${at(p75)} - ${at(p25)})` }} />
            <div className="absolute top-1/2 size-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-surface bg-brand shadow" style={{ left: at(student) }} />
        </div>
    );
}

// GPA: vị trí so với sinh viên của trường nói bằng lời (cùng ngưỡng 25% và 75% với các lý do bên dưới)
function GpaStat({ pg, studentGpa, topShare }: { pg: number | null; studentGpa: number | null; topShare: number | null }) {
    const label = studentGpa != null ? `GPA: bạn ${studentGpa.toFixed(2)}` : "GPA";
    if (pg == null) return <Stat label={label} value={<span className="text-sm text-ink-3">Trường chưa có số liệu</span>} />;
    // GPA tối đa 4,0 nhưng nhiều trường có phần lớn sinh viên trúng tuyển cũng 4,0: nói rõ để học sinh khỏi thắc mắc vì sao "4,0 mà chỉ ở giữa"
    const crowded = studentGpa != null && studentGpa >= 4 && topShare != null && topShare >= 0.4;
    const text = crowded
        ? "Ngang đa số sinh viên"
        : pg >= 75 ? "Cao hơn đa số sinh viên" : pg >= 25 ? "Ở khoảng giữa của trường" : "Thấp hơn đa số sinh viên";
    const note = crowded ? `${pct(topShare)} sinh viên trúng tuyển cũng có GPA 4.0` : pg < 25 ? "Điểm yếu so với trường này" : "So với sinh viên đã trúng tuyển";
    return (
        <div className="min-w-0 space-y-1">
            <p className="text-[11px] font-medium text-ink-2">{label}</p>
            <p className="text-sm font-bold text-foreground">{text}</p>
            <PositionBar at={pg} label={`${label}, ${text}`} />
            <p className={cn("text-[11px]", pg < 25 ? "font-medium text-warn" : "text-ink-3")}>{note}</p>
        </div>
    );
}

// Thanh vị trí 0–100 trong số sinh viên trúng tuyển: đoạn đậm là nhóm đa số (25–75), chấm là bạn. Cùng kiểu với thanh SAT
function PositionBar({ at, label }: { at: number; label: string }) {
    const x = (v: number) => `${Math.min(100, Math.max(0, v))}%`;
    return (
        <div className="relative h-2 w-full max-w-[160px] rounded-full bg-line" role="img" aria-label={label}>
            <div className="absolute inset-y-0 rounded-full bg-brand/40" style={{ left: x(25), width: "50%" }} />
            <div className="absolute top-1/2 size-3 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-surface bg-brand shadow" style={{ left: x(at) }} />
        </div>
    );
}

// Lý do ngắn trên thẻ: chỉ điều QUYẾT ĐỊNH nhóm của trường và các cảnh báo, tối đa 2 dòng, có số cụ thể.
// Phần tốt (GPA, SAT, chi phí ổn) đã nằm ở các chỉ số bên trên; giải thích đầy đủ có ở nút "Vì sao xếp nhóm này?"
const KEY_REASON_ORDER = ["LOW_RATE", "INTL_LOW", "RATE_CAP", "WEAK_CAP", "NEED_AID", "GPA_LOW", "SAT_BELOW", "EC_MINUS"];

function keyReasons(why: string[], school: ScoredSchool | undefined): string[] {
    const rate = school?.acceptRate ?? null;
    const intl = school?.intlAcceptRate ?? null;
    const text = (code: string): string => {
        if (code === "LOW_RATE" && rate != null) return `Trường chỉ nhận ${pct(rate)} hồ sơ nên luôn là Khó vào, điểm cao đến đâu cũng vậy`;
        if (code === "INTL_LOW" && rate != null && intl != null)
            return `Trường nhận du học sinh ít: ${pct(intl)} so với ${pct(rate)} chung, nên bị hạ một nhóm`;
        if (code === "NEED_AID") return "Vượt ngân sách, chỉ đi được nếu có học bổng lớn";
        return whyText(code);
    };
    return KEY_REASON_ORDER.filter((code) => why.includes(code)).slice(0, 2).map(text);
}
