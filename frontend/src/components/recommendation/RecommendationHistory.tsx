"use client";

// [UC-87] Lịch sử gợi ý: danh sách các lần chạy trước, xem lại một lần, chọn hai lần để so sánh.
// Chỉ đọc: không chạy lại, không sửa gì.

import { useEffect, useState } from "react";
import Link from "next/link";
import { AlertCircleIcon, ArrowLeftIcon, ArrowRightIcon, GitCompareIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { recommendationHistoryApi } from "@/lib/recommendation-history-api";
import { cn } from "@/lib/utils";
import type { Recommendation, ScoredSchool } from "@/types/recommendation";
import {
  changeKind,
  type HistoryGroup,
  type RecommendationRunSummary,
  type RunChangeKind,
  type RunComparison,
} from "@/types/recommendation-history";
import { SchoolCard } from "./SchoolCard";

// Cùng tên nhóm với trang Gợi ý trường
const GROUPS: { key: HistoryGroup; title: string; short: string }[] = [
  { key: "reach", title: "Khó vào", short: "Khó vào" },
  { key: "match", title: "Vừa sức", short: "Vừa sức" },
  { key: "safety", title: "An toàn hơn", short: "An toàn" },
  { key: "aid", title: "Chỉ đi được nếu có học bổng lớn", short: "Cần học bổng" },
];
const GROUP_SHORT = Object.fromEntries(GROUPS.map((g) => [g.key, g.short])) as Record<HistoryGroup, string>;

// Cùng tên với thanh ưu tiên bên trình mô phỏng
const PREF_NAMES = ["Chi phí thấp", "Học bổng nhiều", "Danh tiếng"];
const PREF_LEVELS = ["Không quan tâm", "Ít quan tâm", "Bình thường", "Quan tâm nhiều", "Quan tâm nhất"];

const CHANGE_LABEL: Record<RunChangeKind, { text: string; variant: "ok" | "risk" | "brand" | "neutral" }> = {
  new: { text: "Mới vào", variant: "ok" },
  dropped: { text: "Rơi ra", variant: "risk" },
  group: { text: "Đổi nhóm", variant: "brand" },
  rank: { text: "Đổi hạng", variant: "neutral" },
};

const formatTime = (iso: string) =>
  new Date(iso).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });

function describePref(pref: RecommendationRunSummary["pref"]) {
  if (!pref) return "Ưu tiên mặc định";
  if (pref.every((n) => n === 2)) return "Ưu tiên cân bằng";
  return pref.map((n, i) => `${PREF_NAMES[i]}: ${PREF_LEVELS[n] ?? n}`).join(" · ");
}

function errorText(err: unknown, fallback: string) {
  return err instanceof Error ? err.message : fallback;
}

export function RecommendationHistory() {
  const [runs, setRuns] = useState<RecommendationRunSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [comparison, setComparison] = useState<RunComparison | null>(null);
  const [viewing, setViewing] = useState<Recommendation | null>(null);
  const [busy, setBusy] = useState<string | null>(null); // "compare" hoặc id lần đang mở

  useEffect(() => {
    recommendationHistoryApi
      .list()
      .then(setRuns)
      .catch((err: unknown) => {
        setRuns([]);
        setError(errorText(err, "Chưa tải được lịch sử gợi ý."));
      });
  }, []);

  const toggle = (id: string) =>
    setSelected((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : prev.length < 2 ? [...prev, id] : prev));

  const compare = async () => {
    if (!runs || selected.length !== 2) return;
    // Lần cũ hơn là "trước", lần mới hơn là "sau"
    const [older, newer] = runs
      .filter((r) => selected.includes(r.id))
      .sort((a, b) => a.createdAt.localeCompare(b.createdAt));
    setBusy("compare");
    setError(null);
    try {
      setComparison(await recommendationHistoryApi.compare(older.id, newer.id));
    } catch (err) {
      setError(errorText(err, "Chưa so sánh được hai lần gợi ý."));
    } finally {
      setBusy(null);
    }
  };

  const open = async (id: string) => {
    setBusy(id);
    setError(null);
    try {
      setViewing(await recommendationHistoryApi.get(id));
    } catch (err) {
      setError(errorText(err, "Chưa mở được lần gợi ý này."));
    } finally {
      setBusy(null);
    }
  };

  if (runs === null) {
    return (
      <div className="flex min-h-[50vh] flex-col items-center justify-center gap-3 text-center">
        <Spinner className="size-8 text-brand" />
        <p className="text-sm text-muted-foreground">Đang tải lịch sử gợi ý...</p>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-6 lg:px-8">
      <div className="space-y-2">
        <Link
          href="/recommendations"
          className="inline-flex items-center gap-1 text-xs font-semibold text-brand underline-offset-4 hover:underline"
        >
          <ArrowLeftIcon className="size-3.5" />
          Về trang Gợi ý trường
        </Link>
        <h1 className="text-h1 text-foreground">Lịch sử gợi ý</h1>
        <p className="text-sm text-muted-foreground">
          50 lần gợi ý gần nhất, gồm cả các lần đổi nút ưu tiên. Chọn hai lần để xem trường nào đổi nhóm hoặc đổi
          thứ hạng.
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertCircleIcon className="size-4" />
          <AlertTitle>Có lỗi</AlertTitle>
          <AlertDescription className="text-xs">{error}</AlertDescription>
        </Alert>
      )}

      {runs.length === 0 && !error && (
        <div className="rounded-2xl border border-line bg-surface p-8 text-center text-sm text-muted-foreground">
          Bạn chưa có lần gợi ý nào.{" "}
          <Link href="/recommendations" className="font-semibold text-brand underline-offset-4 hover:underline">
            Nhận gợi ý
          </Link>{" "}
          trước.
        </div>
      )}

      {runs.length > 0 && (
        <section className="space-y-3">
          <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface">
            {runs.map((run, index) => {
              const checked = selected.includes(run.id);
              const full = !checked && selected.length >= 2;
              return (
                <li key={run.id} className={cn("flex items-start gap-3 p-4", checked && "bg-brand-wash/40")}>
                  <Checkbox
                    className="mt-1"
                    checked={checked}
                    disabled={full}
                    onCheckedChange={() => toggle(run.id)}
                    aria-label={`Chọn lần gợi ý lúc ${formatTime(run.createdAt)}`}
                  />
                  <div className="min-w-0 flex-1 space-y-1.5">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-foreground">{formatTime(run.createdAt)}</span>
                      {index === 0 && <Badge variant="brand">Mới nhất</Badge>}
                      {run.major && <span className="text-xs text-muted-foreground">Ngành: {run.major}</span>}
                    </div>
                    <p className="text-xs text-muted-foreground">{describePref(run.pref)}</p>
                    <div className="flex flex-wrap gap-1.5">
                      {GROUPS.map((g) => (
                        <Badge key={g.key} variant="neutral">
                          {g.short}: {run.counts[g.key]}
                        </Badge>
                      ))}
                    </div>
                  </div>
                  <Button variant="outline" size="sm" disabled={busy !== null} onClick={() => open(run.id)}>
                    {busy === run.id ? <Spinner className="size-3.5" /> : "Xem"}
                  </Button>
                </li>
              );
            })}
          </ul>

          {/* Thanh chọn dính đáy màn hình: chọn hai lần ở xa nhau vẫn bấm So sánh được ngay, không phải cuộn lên */}
          <div className="sticky bottom-4 z-10 flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-surface/95 px-4 py-3 shadow-lg backdrop-blur">
            <p className="text-xs text-muted-foreground">
              {selected.length === 2 ? "Đã chọn 2/2" : `Chọn thêm ${2 - selected.length} lần để so sánh`}
              {selected.length > 0 && (
                <button type="button" className="ml-2 font-semibold text-brand hover:underline" onClick={() => setSelected([])}>
                  Bỏ chọn
                </button>
              )}
            </p>
            <Button size="sm" className="gap-1" disabled={selected.length !== 2 || busy !== null} onClick={compare}>
              {busy === "compare" ? <Spinner className="size-3.5" /> : <GitCompareIcon className="size-3.5" />}
              So sánh
            </Button>
          </div>
        </section>
      )}

      {/* Xem lại một lần và kết quả so sánh đều hiện dạng popup; dài thì cuộn bên trong popup */}
      <Dialog open={viewing !== null} onOpenChange={(isOpen) => !isOpen && setViewing(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-3xl">
          {viewing && <RunBody rec={viewing} />}
        </DialogContent>
      </Dialog>

      <Dialog open={comparison !== null} onOpenChange={(isOpen) => !isOpen && setComparison(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-3xl">
          {comparison && <ComparisonBody comparison={comparison} />}
        </DialogContent>
      </Dialog>
    </div>
  );
}

// Vị trí một trường trong một lần: "Vừa sức #2", hoặc gạch ngang nếu không có
function Place({ group, rank }: { group: HistoryGroup | null; rank: number | null }) {
  if (!group) return <span className="text-ink-3">—</span>;
  return (
    <span className="whitespace-nowrap">
      {GROUP_SHORT[group]} <span className="text-muted-foreground">#{rank}</span>
    </span>
  );
}

function ComparisonBody({ comparison }: { comparison: RunComparison }) {
  const { from, to, changes } = comparison;
  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle>So sánh hai lần gợi ý</DialogTitle>
        <DialogDescription className="text-xs">
          Lần trước {formatTime(from.createdAt)} → lần sau {formatTime(to.createdAt)}
        </DialogDescription>
      </DialogHeader>

      {changes.length === 0 ? (
        <p className="py-4 text-center text-sm text-muted-foreground">
          Hai lần gợi ý có cùng danh sách trường, cùng nhóm và cùng thứ hạng.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="text-xs text-muted-foreground">
              <tr className="border-b border-line">
                <th className="py-2 pr-3 font-medium">Trường</th>
                <th className="py-2 pr-3 font-medium">Lần trước</th>
                <th className="w-6 py-2" aria-hidden />
                <th className="py-2 pr-3 font-medium">Lần sau</th>
                <th className="py-2 font-medium">Thay đổi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {changes.map((c) => {
                const label = CHANGE_LABEL[changeKind(c)];
                return (
                  <tr key={c.code}>
                    <td className="py-2 pr-3">
                      <Link
                        href={`/university/${encodeURIComponent(c.code)}?from=recommendations`}
                        className="font-semibold text-foreground underline-offset-4 hover:underline"
                      >
                        {c.name}
                      </Link>
                    </td>
                    <td className="py-2 pr-3">
                      <Place group={c.fromGroup} rank={c.fromRank} />
                    </td>
                    <td className="py-2 text-ink-3">
                      <ArrowRightIcon className="size-3.5" />
                    </td>
                    <td className="py-2 pr-3">
                      <Place group={c.toGroup} rank={c.toRank} />
                    </td>
                    <td className="py-2">
                      <Badge variant={label.variant}>{label.text}</Badge>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <p className="text-xs text-ink-3">Chỉ liệt kê trường có thay đổi. Thứ hạng tính trong từng nhóm.</p>
    </>
  );
}

// Xem lại một lần chạy: các nhóm và thẻ trường như trang Gợi ý trường, nhưng chỉ xem (không hỏi trợ lý)
function RunBody({ rec }: { rec: Recommendation }) {
  const { criteria, items } = rec;
  const byCode = new Map<string, ScoredSchool>(items.scored.map((s) => [s.code, s]));
  const student = { sat: criteria.studentSat ?? null, gpa: criteria.studentGpa ?? null };
  const empty = GROUPS.every((g) => items[g.key].length === 0);

  return (
    <div className="space-y-6">
      <DialogHeader className="pr-8">
        <DialogTitle>Lần gợi ý lúc {formatTime(rec.createdAt)}</DialogTitle>
        <DialogDescription className="text-xs">{describePref(criteria.pref ?? null)}</DialogDescription>
      </DialogHeader>

      {empty && (
        <p className="py-4 text-center text-sm text-muted-foreground">Lần này không có trường nào qua bộ lọc.</p>
      )}

      {GROUPS.map((g) => {
        const list = items[g.key];
        if (list.length === 0) return null;
        return (
          <div key={g.key} className="space-y-3">
            <h3 className="text-h3 text-foreground">{g.title}</h3>
            {list.map((p) => (
              <SchoolCard key={p.code} picked={p} school={byCode.get(p.code)} student={student} />
            ))}
          </div>
        );
      })}
    </div>
  );
}
