"use client";

// UC-76: màn so sánh danh sách của con với danh sách theo ưu tiên cha mẹ. Chỉ hiển thị, dùng chung cho phụ huynh và con.
// Mỗi nhóm là một bảng 2 cột: hạng trong danh sách của con | hạng theo cha mẹ, kèm ô chi phí so với ngân sách.

import { Fragment, useState, type ReactNode } from "react";
import Link from "next/link";
import { AlertTriangleIcon, ChevronDownIcon, HandshakeIcon, MapPinIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { SchoolPhoto } from "@/components/recommendation/SchoolPhoto";
import { formatPct, formatUsd } from "@/lib/recommendation-labels";
import type {
  CompareGroup,
  CompareItems,
  CompareLabel,
  ComparedSchool,
  PreferenceCompareDto,
} from "@/lib/parent-preference-api";
import type { ScoredSchool } from "@/types/recommendation";
import { useStateNames } from "@/lib/us-state-names";
import { cn } from "@/lib/utils";

// Cùng tên với thanh ưu tiên bên trình mô phỏng
export const PREF_NAMES = ["Chi phí thấp", "Học bổng nhiều", "Danh tiếng"];
export const PREF_LEVELS = ["Không quan tâm", "Ít quan tâm", "Bình thường", "Quan tâm nhiều", "Quan tâm nhất"];

// Thứ tự khối như trang Gợi ý trường
const GROUPS: { key: CompareGroup; title: string }[] = [
  { key: "reach", title: "Khó vào" },
  { key: "match", title: "Vừa sức" },
  { key: "safety", title: "An toàn" },
  { key: "aid", title: "Chỉ đi được nếu có học bổng lớn" },
  { key: "other", title: "Khác" },
];
const GROUP_TITLE = Object.fromEntries(GROUPS.map((g) => [g.key, g.title])) as Record<CompareGroup, string>;

const VND = new Intl.NumberFormat("vi-VN");

type Variant = "ok" | "brand" | "neutral" | "warn" | "risk";

const AID_POLICY: Record<string, string> = {
  none: "Không cấp cho du học sinh",
  need_only: "Theo hoàn cảnh tài chính",
  merit_only: "Theo thành tích",
  need_and_merit: "Theo hoàn cảnh và thành tích",
};

// "18/11/2026 lúc 17:34"
const formatDate = (iso: string) => {
  const d = new Date(iso);
  return `${d.toLocaleDateString("vi-VN")} lúc ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
};

/** parentName: "Bố", "Mẹ" hoặc "Bạn" (phụ huynh tự xem), dùng trong tiêu đề cột. forChild: con đang xem, gọi con là "bạn". */
export function PreferenceComparison({
  data,
  parentName = "Cha mẹ",
  forChild = false,
}: {
  data: PreferenceCompareDto;
  parentName?: string;
  forChild?: boolean;
}) {
  const you = forChild ? "bạn" : "con";
  const You = forChild ? "Bạn" : "Con";

  if (!data.studentHasRun || !data.comparison) {
    return <Box>{data.message ?? "Con chưa nhận gợi ý trường, chưa so được."}</Box>;
  }

  return <Views data={data} parentName={parentName} forChild={forChild} you={you} You={You} />;
}

// Hai chế độ: chỉ danh sách theo cha mẹ (thẻ có ảnh, xếp theo hạng của cha mẹ) hoặc so sánh với con (bảng 2 cột)
function Views({
  data,
  parentName,
  forChild,
  you,
  You,
}: {
  data: PreferenceCompareDto;
  parentName: string;
  forChild: boolean;
  you: string;
  You: string;
}) {
  const [view, setView] = useState<"parent" | "compare">("parent");
  const c = data.comparison!;
  // "ngân sách của bạn" khi phụ huynh tự xem, "ngân sách của Bố" khi con xem
  const parentOwner = parentName === "Bạn" ? "bạn" : parentName;
  const budget = data.parentBudgetUsd;
  const parentListTitle = parentName === "Bạn" ? "Danh sách của bạn" : `Danh sách theo ${parentName}`;

  return (
    <div className="space-y-4">
      <div role="tablist" aria-label="Cách xem" className="inline-flex rounded-xl border border-line bg-surface p-1">
        {(
          [
            ["parent", parentListTitle],
            ["compare", `So sánh với ${you}`],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={view === key}
            onClick={() => setView(key)}
            className={cn(
              "cursor-pointer rounded-lg px-3 py-1.5 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default",
              view === key ? "bg-brand text-white" : "text-ink-2 hover:text-foreground",
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {data.studentRunAt && (
        <p className="text-xs text-muted-foreground">
          Theo lần nhận gợi ý của {you} ngày {formatDate(data.studentRunAt)}. {You} chạy lại gợi ý thì danh sách theo cha mẹ
          được tính lại theo hồ sơ mới.
        </p>
      )}

      {data.profileChangedAfterRun && data.studentRunAt && (
        <div className="flex gap-2 rounded-2xl border border-warn/30 bg-warn-wash p-3 text-sm text-warn" role="status">
          <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            {You} đã sửa hồ sơ sau lần nhận gợi ý ngày {formatDate(data.studentRunAt)},{" "}
            {forChild ? "bấm" : "nhắc con bấm"} Nhận gợi ý lại để so cho đúng.
          </span>
        </div>
      )}

      <BudgetBox data={data} parentName={parentName} you={you} />

      {view === "parent" ? (
        <>
          <p className="text-xs text-muted-foreground">
            {budget != null
              ? `Chi phí so với ngân sách của ${parentOwner}: ${formatUsd(budget)}/năm.`
              : `Cha mẹ chưa đặt ngân sách, chi phí so với ngân sách của ${you}.`}{" "}
            Bấm vào một trường để xem chi tiết.
          </p>
          <ParentList data={data} />
        </>
      ) : (
        <>
          <PrefTable studentPref={data.studentPref} parentPref={data.parentPref} parentName={parentName} youName={You} />

          <ul className="grid gap-2 sm:grid-cols-3">
            <Count label="Cả hai cùng có" value={c.counts.both} />
            <Count label={`Chỉ ${you} có`} value={c.counts.studentOnly} />
            <Count label="Chỉ cha mẹ có" value={c.counts.parentOnly} />
          </ul>

          {c.sameResult && (
            <div className="flex gap-2 rounded-2xl border border-ok/30 bg-ok-wash p-4 text-sm text-ok">
              <HandshakeIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <div className="space-y-1">
                <p className="font-semibold">Hai bên đồng thuận</p>
                {c.sameNote && <p>{c.sameNote}</p>}
              </div>
            </div>
          )}

          <p className="text-xs text-muted-foreground">
            {budget != null
              ? `Cột chi phí so với ngân sách của ${parentOwner}: ${formatUsd(budget)}/năm.`
              : `Cha mẹ chưa đặt ngân sách, cột chi phí so với ngân sách của ${you}.`}{" "}
            Bấm vào một trường để xem chi tiết.
          </p>

          <CompareTables data={data} youName={You} parentName={parentName} />
        </>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- ngân sách hai bên

// Ngân sách cha mẹ nhập: số đồng và số USD quy theo tỷ giá. Ngân sách của con không hiện vì form chỉ chọn một khoảng.
// Khoảng ngân sách trong hồ sơ con: "20.000 – 40.000 USD/năm", chỉ có mức trên hoặc mức dưới thì ghi "tới" / "từ"
function studentBudgetText(min: number | null | undefined, max: number | null | undefined): string | null {
  if (min != null && max != null) return min === max ? `${formatUsd(max)}/năm` : `${formatUsd(min)} – ${formatUsd(max)}/năm`;
  if (max != null) return `Tới ${formatUsd(max)}/năm`;
  if (min != null) return `Từ ${formatUsd(min)}/năm`;
  return null;
}

// [UC-76, LNK-019/022/033] Ngân sách hai bên cạnh nhau và câu nêu chỗ khác. Cha mẹ nhập một con số chốt (VND),
// con chọn một mức trong hồ sơ (khoảng USD): nằm trong khoảng là giống, thấp hơn mức dưới / cao hơn mức trên là khác.
function budgetDiff(
  parentUsd: number | null,
  min: number | null | undefined,
  max: number | null | undefined,
  parentOwner: string,
  you: string,
): { same: boolean; text: string } | null {
  if (parentUsd == null || (min == null && max == null)) return null;
  if (min != null && parentUsd < min)
    return { same: false, text: `Ngân sách của ${parentOwner} thấp hơn mức ${you} chọn trong hồ sơ.` };
  if (max != null && parentUsd > max)
    return { same: false, text: `Ngân sách của ${parentOwner} cao hơn mức ${you} chọn trong hồ sơ.` };
  return { same: true, text: `Ngân sách của ${parentOwner} nằm trong mức ${you} chọn, hai bên không khác.` };
}

function BudgetBox({ data, parentName, you }: { data: PreferenceCompareDto; parentName: string; you: string }) {
  const self = parentName === "Bạn";
  const parentOwner = self ? "bạn" : parentName;
  const pb = data.parentBudgetUsd;
  const vnd = data.parentBudgetVnd;
  const rate = data.vndPerUsd;
  const studentText = studentBudgetText(data.studentBudgetMinUsd, data.studentBudgetMaxUsd);
  const diff = budgetDiff(pb, data.studentBudgetMinUsd, data.studentBudgetMaxUsd, parentOwner, you);

  return (
    <section aria-label="Ngân sách hai bên" className="space-y-3 rounded-2xl border border-line bg-surface p-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <p className="text-xs font-semibold text-muted-foreground">Ngân sách trong hồ sơ của {you}</p>
          <p className="text-lg font-bold text-foreground">{studentText ?? "Chưa nhập"}</p>
        </div>
        <div className="space-y-1">
          <p className="text-xs font-semibold text-muted-foreground">
            {self ? "Ngân sách bạn nhập" : `Ngân sách ${parentName} nhập`}
          </p>
          {pb != null && vnd != null ? (
            <>
              <p className="text-lg font-bold text-foreground">
                {VND.format(vnd)} đồng/năm ≈ {formatUsd(pb)}/năm
              </p>
              <p className="text-xs text-muted-foreground">
                {rate != null && `Theo tỷ giá ${VND.format(Math.round(rate))} VND/USD ${data.isPreview ? "hôm nay" : "lúc lưu"}. `}
                Con số chốt, không có mức dư: trường vượt dù ít là sang nhóm Cần học bổng, trường không có học bổng cho du
                học sinh thì bị loại.
              </p>
            </>
          ) : (
            <>
              <p className="text-lg font-bold text-foreground">Chưa nhập</p>
              <p className="text-xs text-muted-foreground">Danh sách theo cha mẹ dùng ngân sách của {you}.</p>
            </>
          )}
        </div>
      </div>
      {diff && (
        <p
          role="status"
          className={cn(
            "rounded-xl px-3 py-2 text-sm",
            diff.same ? "bg-muted text-ink-2" : "border border-warn/30 bg-warn-wash font-medium text-warn",
          )}
        >
          {diff.text}
        </p>
      )}
    </section>
  );
}

// ---------------------------------------------------------------- danh sách riêng theo cha mẹ

// Chỉ các trường cha mẹ có, chia nhóm như trang Gợi ý, trong nhóm xếp theo hạng của cha mẹ (không theo thứ tự của con).
// Mỗi trường một thẻ có ảnh (cùng ảnh với thẻ trang Gợi ý), bấm "Xem chi tiết" thì mở số liệu.
function ParentList({ data }: { data: PreferenceCompareDto }) {
  const [open, setOpen] = useState<string | null>(null);
  const stateName = useStateNames();
  const schools = data.comparison?.schools ?? [];
  const sections = GROUPS.map((g) => ({
    ...g,
    rows: schools
      .filter((s) => s.parentGroup === g.key)
      .sort((a, b) => (a.parentRank ?? 999) - (b.parentRank ?? 999)),
  })).filter((g) => g.rows.length > 0);

  if (sections.length === 0)
    return <Box>Danh sách theo cha mẹ trống: không trường nào hợp hồ sơ của con với ngân sách này.</Box>;

  return (
    <div className="space-y-5">
      {sections.map((g) => (
        <section key={g.key} className="space-y-2" aria-labelledby={`par-${g.key}`}>
          <h3 id={`par-${g.key}`} className="text-h3 text-foreground">
            {g.title} <span className="text-sm font-normal text-muted-foreground">({g.rows.length})</span>
          </h3>
          <ul className="space-y-3">
            {g.rows.map((s) => {
              const info = schoolInfo(data, s.code);
              const name = s.name ?? info?.name ?? s.code;
              const href = `/university/${encodeURIComponent(s.code)}?from=recommendations`;
              const isOpen = open === s.code;
              return (
                <li key={s.code} className="@container">
                  <div className="flex flex-col overflow-hidden rounded-xl border border-line bg-surface transition-colors hover:border-brand/40 @2xl:flex-row">
                    <SchoolPhoto name={name} coverUrl={info?.coverUrl} logoUrl={info?.logoUrl} href={href} />
                    <div className="min-w-0 flex-1 space-y-2 p-4">
                      <div className="flex items-start gap-2">
                        {s.parentRank != null && <Badge variant="brand">#{s.parentRank}</Badge>}
                        <h4 className="text-base font-bold text-foreground">
                          <Link href={href} className="hover:text-brand hover:underline">
                            {name}
                          </Link>
                        </h4>
                      </div>
                      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
                        {info?.state && (
                          <span className="inline-flex items-center gap-1">
                            <MapPinIcon className="size-3" aria-hidden />
                            {stateName(info.state)}
                          </span>
                        )}
                        {info?.control && <span>{info.control === "public" ? "Công lập" : "Tư thục"}</span>}
                        {info?.acceptRate != null && <span>Tỷ lệ nhận {formatPct(info.acceptRate)}</span>}
                      </div>
                      <CostBadge info={info} budget={data.parentBudgetUsd} />
                      <button
                        type="button"
                        aria-expanded={isOpen}
                        onClick={() => setOpen((cur) => (cur === s.code ? null : s.code))}
                        className="flex cursor-pointer items-center gap-1 rounded text-sm font-semibold text-brand focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
                      >
                        <ChevronDownIcon className={cn("size-4 transition-transform", isOpen && "rotate-180")} aria-hidden />
                        {isOpen ? "Ẩn chi tiết" : "Xem chi tiết"}
                      </button>
                      {isOpen && (
                        <div className="border-t border-line pt-3">
                          <Detail school={s} info={info} />
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

// Ảnh nhỏ cạnh tên trường trong bảng so sánh: logo, thiếu logo thì ảnh bìa, thiếu cả hai thì chữ cái đầu
function SchoolThumb({ name, info }: { name: string; info: Info }) {
  const [failed, setFailed] = useState(false);
  const url = info?.logoUrl ?? info?.coverUrl ?? null;
  return (
    <span className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-line bg-brand-wash">
      {url && !failed ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={url}
          alt=""
          loading="lazy"
          className={info?.logoUrl ? "size-full object-contain p-1" : "size-full object-cover"}
          onError={() => setFailed(true)}
        />
      ) : (
        <span aria-hidden className="text-xs font-bold text-brand/60">
          {name.slice(0, 2).toUpperCase()}
        </span>
      )}
    </span>
  );
}

// 3 mức ưu tiên của con và của cha mẹ cạnh nhau
function PrefTable({
  studentPref,
  parentPref,
  parentName,
  youName,
}: {
  studentPref: number[] | null;
  parentPref: number[];
  parentName: string;
  youName: string;
}) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-xs text-muted-foreground">
            <th className="p-3 font-medium">Ưu tiên</th>
            <th className="p-3 font-medium">{youName}</th>
            <th className="p-3 font-medium">{parentName}</th>
          </tr>
        </thead>
        <tbody>
          {PREF_NAMES.map((name, i) => {
            const mine = studentPref?.[i];
            const theirs = parentPref[i];
            return (
              <tr key={name} className="border-b border-line last:border-0">
                <td className="p-3 font-medium text-foreground">{name}</td>
                <td className="p-3 text-ink-2">{mine != null ? PREF_LEVELS[mine] : "Chưa rõ"}</td>
                <td className={mine != null && mine !== theirs ? "p-3 font-semibold text-brand" : "p-3 text-ink-2"}>
                  {PREF_LEVELS[theirs] ?? theirs}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

// ---------------------------------------------------------------- bảng 2 cột theo nhóm

// Mỗi trường một hàng, đặt theo nhóm của con; trường chỉ cha mẹ có thì đặt theo nhóm của cha mẹ.
// Trong nhóm: các trường của con theo hạng của con trước, rồi các trường cha mẹ thêm theo hạng của cha mẹ.
function CompareTables({ data, youName, parentName }: { data: PreferenceCompareDto; youName: string; parentName: string }) {
  const [open, setOpen] = useState<string | null>(null);
  const schools = data.comparison?.schools ?? [];
  const sections = GROUPS.map((g) => ({
    ...g,
    rows: schools
      .filter((s) => (s.studentGroup ?? s.parentGroup) === g.key)
      .sort((a, b) => order(a) - order(b)),
  })).filter((g) => g.rows.length > 0);

  if (sections.length === 0) return <Box>Cả hai danh sách đều trống.</Box>;

  const toggle = (code: string) => setOpen((cur) => (cur === code ? null : code));

  return (
    <div className="space-y-5">
      {sections.map((g) => (
        <section key={g.key} className="space-y-2" aria-labelledby={`cmp-${g.key}`}>
          <h3 id={`cmp-${g.key}`} className="text-h3 text-foreground">
            {g.title} <span className="text-sm font-normal text-muted-foreground">({g.rows.length})</span>
          </h3>

          {/* Màn rộng: bảng */}
          <div className="hidden overflow-hidden rounded-2xl border border-line bg-surface sm:block">
            <table className="w-full table-fixed text-sm">
              <thead>
                <tr className="border-b border-line text-left text-xs text-muted-foreground">
                  <th className="w-[34%] p-3 font-medium">Trường</th>
                  <th className="w-[9%] p-3 font-medium">{youName}</th>
                  <th className="w-[17%] p-3 font-medium">{parentName}</th>
                  <th className="w-[20%] p-3 font-medium">Chi phí</th>
                  <th className="p-3 font-medium">Lý do</th>
                </tr>
              </thead>
              <tbody>
                {g.rows.map((s) => {
                  const isOpen = open === s.code;
                  const info = schoolInfo(data, s.code);
                  return (
                    <Fragment key={s.code}>
                      <tr
                        className={cn(
                          "cursor-pointer border-b border-line align-top last:border-0 hover:bg-ground",
                          isOpen && "bg-ground",
                        )}
                        onClick={() => toggle(s.code)}
                      >
                        <td className="p-3">
                          <button
                            type="button"
                            className="flex cursor-pointer items-center gap-2 rounded text-left font-semibold text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
                            aria-expanded={isOpen}
                            onClick={(e) => {
                              e.stopPropagation();
                              toggle(s.code);
                            }}
                          >
                            <ChevronDownIcon
                              className={cn("size-4 shrink-0 text-ink-3 transition-transform", isOpen && "rotate-180")}
                              aria-hidden
                            />
                            <SchoolThumb name={s.name ?? s.code} info={info} />
                            <span>{s.name ?? s.code}</span>
                          </button>
                        </td>
                        <td className="p-3 text-ink-2">{s.studentRank != null ? `#${s.studentRank}` : "—"}</td>
                        <td className="p-3">
                          <ParentPlace school={s} />
                        </td>
                        <td className="p-3">
                          <CostBadge info={info} budget={data.parentBudgetUsd} />
                        </td>
                        <td className="p-3 text-xs text-ink-2">
                          <span className="line-clamp-2">{s.reason ?? "—"}</span>
                        </td>
                      </tr>
                      {isOpen && (
                        <tr className="border-b border-line bg-ground last:border-0">
                          <td colSpan={5} className="px-3 pb-4 pt-1">
                            <Detail school={s} info={info} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Điện thoại: mỗi trường một thẻ nhỏ */}
          <ul className="space-y-2 sm:hidden">
            {g.rows.map((s) => {
              const isOpen = open === s.code;
              const info = schoolInfo(data, s.code);
              return (
                <li key={s.code} className="rounded-2xl border border-line bg-surface">
                  <button
                    type="button"
                    className="w-full cursor-pointer space-y-1.5 rounded-2xl p-3 text-left focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 disabled:cursor-default"
                    aria-expanded={isOpen}
                    onClick={() => toggle(s.code)}
                  >
                    <span className="flex items-center justify-between gap-2">
                      <span className="flex items-center gap-2 text-sm font-semibold text-foreground">
                        <SchoolThumb name={s.name ?? s.code} info={info} />
                        {s.name ?? s.code}
                      </span>
                      <ChevronDownIcon
                        className={cn("mt-0.5 size-4 shrink-0 text-ink-3 transition-transform", isOpen && "rotate-180")}
                        aria-hidden
                      />
                    </span>
                    <span className="flex flex-wrap items-center gap-2 text-xs text-ink-2">
                      <span>
                        {youName} {s.studentRank != null ? `#${s.studentRank}` : "—"}
                      </span>
                      <span aria-hidden>→</span>
                      <span>{parentName}</span>
                      <ParentPlace school={s} />
                    </span>
                    <CostBadge info={info} budget={data.parentBudgetUsd} />
                    {s.reason && <span className="block text-xs text-ink-2">{s.reason}</span>}
                  </button>
                  {isOpen && (
                    <div className="border-t border-line p-3">
                      <Detail school={s} info={info} />
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

const order = (s: ComparedSchool) => (s.studentRank != null ? s.studentRank : 100 + (s.parentRank ?? 99));

// Ô của cha mẹ: cùng nhóm thì hạng kèm mũi tên, khác nhóm thì "→ nhóm #hạng", con có mà cha mẹ không có thì "Bỏ"
function ParentPlace({ school: s }: { school: ComparedSchool }) {
  if (s.parentGroup == null) {
    return s.label === ("dropped_over_budget" satisfies CompareLabel) ? (
      <Badge variant="risk">Bỏ, vượt ngân sách</Badge>
    ) : (
      <Badge variant="risk">Bỏ</Badge>
    );
  }
  const rank = s.parentRank != null ? `#${s.parentRank}` : "";
  if (s.studentGroup == null) return <Badge variant="ok">+ Thêm {rank}</Badge>;
  if (s.studentGroup !== s.parentGroup) {
    return (
      <Badge variant={s.parentGroup === "aid" ? "warn" : "brand"}>
        → {GROUP_TITLE[s.parentGroup]} {rank}
      </Badge>
    );
  }
  const moved = s.studentRank != null && s.parentRank != null ? s.studentRank - s.parentRank : 0;
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className="text-ink-2">{rank}</span>
      {moved > 0 && <Badge variant="brand">↑ {moved}</Badge>}
      {moved < 0 && <Badge variant="neutral">↓ {-moved}</Badge>}
      {moved === 0 && <Badge variant="ok">Giữ nguyên</Badge>}
    </span>
  );
}

type Info = ScoredSchool | undefined;

// Số liệu của trường: ưu tiên bản trong danh sách theo cha mẹ (gap, outcome tính theo ngân sách cha mẹ)
function schoolInfo(data: PreferenceCompareDto, code: string): Info {
  return find(data.parentItems, code) ?? find(data.studentItems, code);
}

function find(items: CompareItems | null | undefined, code: string): Info {
  const same = (c: string) => c.toLowerCase() === code.toLowerCase();
  return items?.scored?.find((x) => same(x.code));
}

// Ngân sách cha mẹ nhập không có mức dư: trong ngân sách, hoặc vượt là cần học bổng.
// Cha mẹ chưa nhập thì theo ngân sách của con (có mức dư 10%): trong ngân sách / vượt trong mức dư / cần học bổng lớn.
function costStatus(info: Info, budget: number | null): { text: string; variant: Variant } {
  const cost = info?.cost ?? null;
  if (cost == null) return { text: "Chưa có số liệu chi phí", variant: "neutral" };
  const total = `${formatUsd(cost)}/năm`;
  if (budget != null) {
    if (cost <= budget) return { text: `✓ ${total}, trong ngân sách`, variant: "ok" };
    return { text: `${total}, vượt ${formatUsd(cost - budget)}, cần học bổng`, variant: "risk" };
  }
  if (info?.outcome === "AidBlock") return { text: `${total}, cần học bổng lớn`, variant: "risk" };
  if (info?.gap != null && info.gap > 0)
    return { text: `⚠ ${total}, vượt ${formatUsd(info.gap)} (trong mức dư 10%)`, variant: "warn" };
  return { text: total, variant: "neutral" };
}

function CostBadge({ info, budget }: { info: Info; budget: number | null }) {
  const st = costStatus(info, budget);
  return (
    <Badge variant={st.variant} className="h-auto whitespace-normal text-left">
      {st.text}
    </Badge>
  );
}

// Chi tiết gọn khi bấm vào hàng
function Detail({ school: s, info }: { school: ComparedSchool; info: Info }) {
  const sat = info?.sat25 != null && info?.sat75 != null ? `${info.sat25}–${info.sat75}` : "Chưa có số liệu";
  const aid =
    info?.aidPolicy === "none"
      ? AID_POLICY.none
      : [info?.aidPolicy ? AID_POLICY[info.aidPolicy] : null, info?.aidAvg ? `trung bình ${formatUsd(info.aidAvg)}/năm` : null]
          .filter(Boolean)
          .join(", ") || "Chưa có số liệu";
  return (
    <div className="space-y-3">
      <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
        <Fact label="Tổng chi phí" value={info?.cost != null ? `${formatUsd(info.cost)}/năm` : "Chưa có số liệu"} />
        <Fact label="Tỷ lệ nhận" value={info?.acceptRate != null ? formatPct(info.acceptRate) : "Chưa có số liệu"} />
        <Fact label="SAT 25–75%" value={sat} />
        <Fact label="Học bổng du học sinh" value={aid} />
      </dl>
      {s.reason && <p className="text-sm text-ink-2">{s.reason}</p>}
      <Link
        href={`/university/${encodeURIComponent(s.code)}?from=recommendations`}
        className="inline-block text-sm font-semibold text-brand underline-offset-4 hover:underline"
      >
        Xem trang trường
      </Link>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-foreground">{value}</dd>
    </div>
  );
}

function Count({ label, value }: { label: string; value: number }) {
  return (
    <li className="rounded-2xl border border-line bg-surface p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-h3 text-foreground">{value}</p>
    </li>
  );
}

function Box({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">{children}</div>
  );
}
