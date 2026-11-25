"use client";

import { useState } from "react";
import { PlayIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  DEFAULT_STABLE_BELOW, DEFAULT_WATCH_UP_TO, KIND_TITLES, paramValue, STEP_OPTIONS, validateRequest,
} from "@/lib/sensitivity-format";
import type { SensitivityKind, SensitivityParameterInfo, SensitivityRequest } from "@/types/sensitivity";

interface RunFormProps {
  parameters: SensitivityParameterInfo[] | null;
  running: boolean;
  onRun: (request: SensitivityRequest) => void;
}

const KINDS: SensitivityKind[] = ["grouping", "fit_weight"];

/** Chọn tham số, mức nhích và ngưỡng nhãn rồi bấm Chạy. Mặc định chọn hết 13 tham số và cả hai mức. */
export function RunForm({ parameters, running, onRun }: RunFormProps) {
  // null: chưa đụng tới, coi như chọn hết
  const [picked, setPicked] = useState<Set<string> | null>(null);
  const [steps, setSteps] = useState<Set<number>>(() => new Set(STEP_OPTIONS.map((o) => o.value)));
  const [stableBelowPct, setStableBelowPct] = useState(String(DEFAULT_STABLE_BELOW * 100));
  const [watchUpToPct, setWatchUpToPct] = useState(String(DEFAULT_WATCH_UP_TO * 100));
  const [error, setError] = useState<string | null>(null);

  const all = parameters ?? [];
  const selected = picked ?? new Set(all.map((p) => p.key));

  const toggle = (key: string, on: boolean) => {
    const next = new Set(selected);
    if (on) next.add(key);
    else next.delete(key);
    setPicked(next);
  };
  const toggleKind = (kind: SensitivityKind, on: boolean) => {
    const next = new Set(selected);
    for (const p of all.filter((x) => x.kind === kind)) {
      if (on) next.add(p.key);
      else next.delete(p.key);
    }
    setPicked(next);
  };
  const toggleStep = (value: number, on: boolean) => {
    const next = new Set(steps);
    if (on) next.add(value);
    else next.delete(value);
    setSteps(next);
  };

  const submit = () => {
    // Giữ thứ tự theo danh mục để bảng kết quả luôn cùng thứ tự
    const keys = all.filter((p) => selected.has(p.key)).map((p) => p.key);
    const stepList = [...steps].sort((a, b) => a - b);
    const stable = Number(stableBelowPct.replace(",", "."));
    const watch = Number(watchUpToPct.replace(",", "."));
    const problem = validateRequest(keys, stepList, stable, watch);
    setError(problem);
    if (problem) return;
    onRun({ parameterKeys: keys, steps: stepList, stableBelow: stable / 100, watchUpTo: watch / 100 });
  };

  const only20 = steps.size > 0 && !steps.has(0.1);

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="text-base font-semibold text-ink">Chạy phân tích mới</h2>
        </CardTitle>
        <CardDescription>
          Nhích từng tham số lên và xuống theo phần trăm của giá trị hiện tại, chạy lại 324 hồ sơ giả lập qua đúng thuật toán gợi ý
          thật và đếm bao nhiêu kết quả thay đổi. Không dùng dữ liệu học sinh thật, không gọi AI, không đổi tham số đang chạy.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        <div className="grid gap-4 lg:grid-cols-2">
          {KINDS.map((kind) => {
            const items = all.filter((p) => p.kind === kind);
            const allOn = items.length > 0 && items.every((p) => selected.has(p.key));
            return (
              <fieldset key={kind} className="rounded-lg border border-line p-3">
                <legend className="px-1 text-sm font-medium text-ink">{KIND_TITLES[kind].title}</legend>
                <p className="mb-2 text-xs text-ink-3">Đo bằng {KIND_TITLES[kind].measure}.</p>
                <label className="mb-1 flex items-center gap-2 text-xs font-medium text-ink-2">
                  <Checkbox checked={allOn} onCheckedChange={(on) => toggleKind(kind, on === true)} />
                  Chọn tất cả
                </label>
                <ul className="flex flex-col gap-1.5">
                  {items.map((p) => (
                    <li key={p.key}>
                      <label className="flex items-center gap-2 text-sm text-ink">
                        <Checkbox checked={selected.has(p.key)} onCheckedChange={(on) => toggle(p.key, on === true)} />
                        <span className="flex-1">{p.name}</span>
                        <span className="tabular text-xs text-ink-3">đang dùng {paramValue(p.current)}</span>
                      </label>
                    </li>
                  ))}
                </ul>
              </fieldset>
            );
          })}
        </div>

        <div className="flex flex-wrap items-end gap-6">
          <fieldset className="flex flex-col gap-1.5">
            <legend className="mb-1 text-xs font-medium text-ink-2">Mức nhích</legend>
            <div className="flex gap-4">
              {STEP_OPTIONS.map((o) => (
                <label key={o.value} className="flex items-center gap-2 text-sm text-ink">
                  <Checkbox checked={steps.has(o.value)} onCheckedChange={(on) => toggleStep(o.value, on === true)} />
                  {o.label}
                </label>
              ))}
            </div>
          </fieldset>
          <label className="flex flex-col gap-1 text-xs font-medium text-ink-2">
            Ổn định khi thay đổi dưới (%)
            <Input inputMode="decimal" value={stableBelowPct} onChange={(e) => setStableBelowPct(e.target.value)} className="h-8 w-28" />
          </label>
          <label className="flex flex-col gap-1 text-xs font-medium text-ink-2">
            Nhạy khi thay đổi trên (%)
            <Input inputMode="decimal" value={watchUpToPct} onChange={(e) => setWatchUpToPct(e.target.value)} className="h-8 w-28" />
          </label>
          <Button type="button" onClick={submit} loading={running} disabled={!parameters}>
            <PlayIcon data-icon="inline-start" />
            Chạy phân tích
          </Button>
        </div>

        <p className="text-xs text-ink-3">
          Đang chọn {selected.size}/{all.length} tham số. Nhãn tính trên mức ±10%: dưới ngưỡng Ổn định là Ổn định, đến ngưỡng Nhạy là Cần
          theo dõi, trên nữa là Nhạy.{only20 ? " Chỉ chọn ±20% thì có số liệu nhưng không gắn nhãn." : ""}
        </p>
        {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      </CardContent>
    </Card>
  );
}
