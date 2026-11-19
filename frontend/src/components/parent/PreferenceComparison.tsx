"use client";

// UC-76: màn so sánh danh sách của con với danh sách theo ưu tiên cha mẹ. Chỉ hiển thị, dùng chung cho phụ huynh và con.

import { useState, type ReactNode } from "react";
import { AlertTriangleIcon, HandshakeIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import type { CompareGroup, CompareLabel, ComparedSchool, PreferenceCompareDto } from "@/lib/parent-preference-api";

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

type Variant = "ok" | "brand" | "neutral" | "warn" | "risk";

const LABEL: Record<CompareLabel, { text: string; variant: Variant }> = {
  both: { text: "Cha mẹ cũng chọn", variant: "ok" },
  parent_higher: { text: "Cha mẹ xếp cao hơn", variant: "brand" },
  parent_lower: { text: "Cha mẹ xếp thấp hơn", variant: "neutral" },
  moved_to_aid: { text: "Cha mẹ chuyển sang Cần học bổng lớn", variant: "warn" },
  moved_out_of_aid: { text: "Theo cha mẹ không cần học bổng lớn", variant: "brand" },
  group_changed: { text: "Khác nhóm", variant: "neutral" },
  parent_drops: { text: "Cha mẹ sẽ bỏ", variant: "risk" },
  dropped_over_budget: { text: "Bị loại vì vượt ngân sách cha mẹ", variant: "risk" },
  parent_adds: { text: "Cha mẹ sẽ thêm", variant: "ok" },
};

// "18/11/2026 lúc 17:34"
const formatDate = (iso: string) => {
  const d = new Date(iso);
  return `${d.toLocaleDateString("vi-VN")} lúc ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
};

type View = "student" | "parent";

/** parentName: "Bố", "Mẹ" hoặc tên cha/mẹ, dùng trong tiêu đề cột ưu tiên. forChild: con đang xem, gọi con là "bạn". */
export function PreferenceComparison({
  data,
  parentName = "Cha mẹ",
  forChild = false,
}: {
  data: PreferenceCompareDto;
  parentName?: string;
  forChild?: boolean;
}) {
  const [view, setView] = useState<View>("student");
  const you = forChild ? "bạn" : "con";

  if (!data.studentHasRun || !data.comparison) {
    return <Box>{data.message ?? "Con chưa nhận gợi ý trường, chưa so được."}</Box>;
  }

  const c = data.comparison;
  const added = c.schools.filter((s) => s.studentGroup == null);

  return (
    <div className="space-y-4">
      {data.studentRunAt && (
        <p className="text-xs text-muted-foreground">
          Theo lần nhận gợi ý của {you} ngày {formatDate(data.studentRunAt)}. {forChild ? "Bạn" : "Con"} chạy lại gợi ý thì
          danh sách theo cha mẹ được tính lại theo hồ sơ mới.
        </p>
      )}

      {data.profileChangedAfterRun && data.studentRunAt && (
        <div className="flex gap-2 rounded-2xl border border-warn/30 bg-warn-wash p-3 text-sm text-warn" role="status">
          <AlertTriangleIcon className="mt-0.5 size-4 shrink-0" aria-hidden />
          <span>
            {forChild ? "Bạn" : "Con"} đã sửa hồ sơ sau lần nhận gợi ý ngày {formatDate(data.studentRunAt)},{" "}
            {forChild ? "bấm" : "nhắc con bấm"} Nhận gợi ý lại để so cho đúng.
          </span>
        </div>
      )}

      <PrefTable studentPref={data.studentPref} parentPref={data.parentPref} parentName={parentName} youName={forChild ? "Bạn" : "Con"} />

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

      <ToggleGroup
        value={[view]}
        onValueChange={(v) => {
          const next = (Array.isArray(v) ? v[0] : v) as View | undefined;
          if (next) setView(next);
        }}
        variant="outline"
        aria-label="Cách xem"
      >
        <ToggleGroupItem value="student">So với {you}</ToggleGroupItem>
        <ToggleGroupItem value="parent">Danh sách theo cha mẹ</ToggleGroupItem>
      </ToggleGroup>

      {view === "student" ? (
        <div className="space-y-4">
          <Grouped
            schools={c.schools}
            groupOf={(s) => s.studentGroup}
            rankOf={(s) => s.studentRank}
            badges={(s) => <Badge variant={LABEL[s.label].variant}>{LABEL[s.label].text}</Badge>}
            empty={`Danh sách của ${you} trống.`}
          />
          {added.length > 0 && (
            <section className="space-y-2 rounded-2xl border border-ok/30 p-4">
              <h3 className="text-sm font-semibold text-foreground">Cha mẹ sẽ thêm</h3>
              <ul className="divide-y divide-line">
                {added.map((s) => (
                  <SchoolRow key={s.code} school={s} rank={s.parentRank}>
                    {s.parentGroup && <Badge variant="neutral">{GROUP_TITLE[s.parentGroup]}</Badge>}
                  </SchoolRow>
                ))}
              </ul>
            </section>
          )}
        </div>
      ) : (
        <Grouped
          schools={c.schools}
          groupOf={(s) => s.parentGroup}
          rankOf={(s) => s.parentRank}
          badges={(s) =>
            s.studentGroup == null ? (
              <Badge variant="ok">{forChild ? "Bạn" : "Con"} chưa có</Badge>
            ) : s.studentGroup !== s.parentGroup ? (
              <Badge variant="warn">{forChild ? "Bạn" : "Con"} để ở nhóm {GROUP_TITLE[s.studentGroup]}</Badge>
            ) : s.studentRank !== s.parentRank && s.studentRank != null ? (
              <Badge variant="neutral">{forChild ? "Bạn" : "Con"} xếp thứ {s.studentRank}</Badge>
            ) : null
          }
          empty="Danh sách theo cha mẹ trống."
        />
      )}
    </div>
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

// Danh sách theo nhóm Khó / Vừa / An toàn..., trong nhóm theo hạng
function Grouped({
  schools,
  groupOf,
  rankOf,
  badges,
  empty,
}: {
  schools: ComparedSchool[];
  groupOf: (s: ComparedSchool) => CompareGroup | null;
  rankOf: (s: ComparedSchool) => number | null;
  badges: (s: ComparedSchool) => ReactNode;
  empty: string;
}) {
  const sections = GROUPS.map((g) => ({
    ...g,
    items: schools.filter((s) => groupOf(s) === g.key).sort((a, b) => (rankOf(a) ?? 99) - (rankOf(b) ?? 99)),
  })).filter((g) => g.items.length > 0);

  if (sections.length === 0) return <Box>{empty}</Box>;
  return (
    <div className="space-y-4">
      {sections.map((g) => (
        <section key={g.key} className="space-y-2">
          <h3 className="text-sm font-semibold text-foreground">{g.title}</h3>
          <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
            {g.items.map((s) => (
              <SchoolRow key={s.code} school={s} rank={rankOf(s)}>
                {badges(s)}
              </SchoolRow>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function SchoolRow({ school: s, rank, children }: { school: ComparedSchool; rank: number | null; children?: ReactNode }) {
  return (
    <li className="space-y-1 p-3">
      <div className="flex flex-wrap items-center gap-2">
        {rank != null && <span className="text-xs text-muted-foreground">#{rank}</span>}
        <span className="text-sm font-semibold text-foreground">{s.name ?? s.code}</span>
        {children}
      </div>
      {s.reason && <p className="text-xs text-ink-2">{s.reason}</p>}
    </li>
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
