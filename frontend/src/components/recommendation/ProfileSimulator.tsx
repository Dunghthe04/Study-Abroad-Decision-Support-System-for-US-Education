"use client";

import type { ReactNode } from "react";
import { ChevronDownIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Slider } from "@/components/ui/slider";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import type { ProfileSimulation } from "@/lib/use-profile-simulation";
import type { Pref, ScholarshipNeed, SimulationOverrides } from "@/types/recommendation";

type NumericKey = "gpa" | "sat" | "act" | "ielts" | "budgetUsd" | "ecTier";

const NUMBER = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

const SCHOLARSHIP_OPTIONS: { value: ScholarshipNeed; label: string }[] = [
  { value: "optional", label: "Không cần" },
  { value: "preferred", label: "Nên có" },
  { value: "mandatory", label: "Bắt buộc" },
];

const PREF_ROWS: { index: 0 | 1 | 2; label: string }[] = [
  { index: 0, label: "Chi phí thấp" },
  { index: 1, label: "Học bổng nhiều" },
  { index: 2, label: "Danh tiếng" },
];

interface ProfileSimulatorProps {
  sim: ProfileSimulation;
  basePref: Pref; // ưu tiên của lần gợi ý đã lưu
}

// Trình mô phỏng hồ sơ: kéo GPA, SAT, ngân sách... để xem danh sách trường đổi thế nào. Không đổi hồ sơ thật.
export function ProfileSimulator({ sim, basePref }: ProfileSimulatorProps) {
  const profile = sim.result?.profile ?? null;
  const o = sim.overrides;

  // Đặt một giá trị số; bằng giá trị hồ sơ thật thì coi như bỏ giả định
  const setNumber = (key: NumericKey, base: number | null, value: number) =>
    sim.set({ [key]: value === base ? null : value } as SimulationOverrides);

  const setPref = (index: 0 | 1 | 2, value: number) => {
    const next = [...(o.pref ?? basePref)] as Pref;
    next[index] = value;
    sim.set({ pref: next.every((v, i) => v === basePref[i]) ? null : next });
  };

  const prefNow = o.pref ?? basePref;
  const scholarshipNow = (o.scholarshipNeed ?? profile?.scholarshipNeed ?? null) as ScholarshipNeed | null;

  return (
    <aside className="overflow-hidden rounded-2xl border border-line bg-surface lg:sticky lg:top-4 lg:max-h-[calc(100vh-2rem)] lg:overflow-y-auto">
      <div className="space-y-1 px-4 pt-4 pb-3">
        <div className="flex items-center justify-between gap-2">
          <h2 className="text-base font-bold text-foreground">Mô phỏng hồ sơ</h2>
          {sim.loading && <Spinner className="size-4 text-brand" />}
        </div>
        <p className="text-xs text-muted-foreground">
          Kéo để xem danh sách trường đổi thế nào. Đây chỉ là giả định, hồ sơ thật của bạn không thay đổi.
        </p>
        {sim.error && <p className="text-xs text-risk">{sim.error}</p>}
      </div>

      {!profile ? (
        <p className="px-4 pb-6 text-xs text-muted-foreground">
          {sim.error ? "Chưa tải được hồ sơ để mô phỏng." : "Đang tải hồ sơ..."}
        </p>
      ) : (
        <>
          <Section title="GPA" onReset={() => sim.reset(["gpa"])} changed={o.gpa != null}>
            <SliderRow
              label="GPA (thang 4)"
              base={profile.gpa}
              value={o.gpa ?? null}
              min={0}
              max={4}
              step={0.1}
              fallback={3}
              onChange={(v) => setNumber("gpa", profile.gpa, v)}
            />
          </Section>

          <Section title="SAT / ACT" onReset={() => sim.reset(["sat", "act"])} changed={o.sat != null || o.act != null}>
            <SliderRow
              label="SAT (tổng)"
              base={profile.sat}
              value={o.sat ?? null}
              min={400}
              max={1600}
              step={10}
              fallback={1000}
              onChange={(v) => setNumber("sat", profile.sat, v)}
            />
            <SliderRow
              label="ACT"
              base={profile.act}
              value={o.act ?? null}
              min={1}
              max={36}
              step={1}
              fallback={24}
              onChange={(v) => setNumber("act", profile.act, v)}
            />
          </Section>

          <Section title="Tiếng Anh" onReset={() => sim.reset(["ielts"])} changed={o.ielts != null}>
            <SliderRow
              label="IELTS"
              base={profile.ielts}
              value={o.ielts ?? null}
              min={0}
              max={9}
              step={0.5}
              fallback={6.5}
              onChange={(v) => setNumber("ielts", profile.ielts, v)}
            />
          </Section>

          <Section
            title="Tài chính"
            onReset={() => sim.reset(["budgetUsd", "scholarshipNeed"])}
            changed={o.budgetUsd != null || o.scholarshipNeed != null}
          >
            <SliderRow
              label="Ngân sách mỗi năm (USD)"
              base={profile.budgetUsd}
              value={o.budgetUsd ?? null}
              min={0}
              max={120000}
              step={1000}
              fallback={40000}
              onChange={(v) => setNumber("budgetUsd", profile.budgetUsd, v)}
            />
            <div className="space-y-2">
              <p className="text-xs font-semibold text-foreground">Cần học bổng</p>
              <div className="flex flex-wrap gap-2">
                {SCHOLARSHIP_OPTIONS.map((opt) => (
                  <Button
                    key={opt.value}
                    size="sm"
                    variant={scholarshipNow === opt.value ? "default" : "outline"}
                    onClick={() =>
                      sim.set({ scholarshipNeed: opt.value === profile.scholarshipNeed ? null : opt.value })
                    }
                  >
                    {opt.label}
                  </Button>
                ))}
              </div>
            </div>
          </Section>

          <Section title="Ngoại khóa" onReset={() => sim.reset(["ecTier"])} changed={o.ecTier != null}>
            <SliderRow
              label="Bậc ngoại khóa (1 đến 5)"
              base={profile.ecTier}
              value={o.ecTier ?? null}
              min={1}
              max={5}
              step={1}
              fallback={3}
              onChange={(v) => setNumber("ecTier", profile.ecTier, v)}
            />
          </Section>

          <Section title="Ưu tiên của bạn" onReset={() => sim.reset(["pref"])} changed={o.pref != null}>
            <p className="text-[11px] text-muted-foreground">
              Chỉ đổi thứ tự các trường trong cùng một nhóm, không đổi nhóm.
            </p>
            {PREF_ROWS.map((row) => (
              <SliderRow
                key={row.index}
                label={row.label}
                base={basePref[row.index]}
                value={prefNow[row.index] === basePref[row.index] ? null : prefNow[row.index]}
                min={0}
                max={4}
                step={1}
                fallback={2}
                colorDelta={false}
                onChange={(v) => setPref(row.index, v)}
              />
            ))}
          </Section>

          <div className="sticky bottom-0 border-t border-line bg-surface px-4 py-3 text-center">
            <Button variant="ghost" size="sm" disabled={!sim.isSimulating} onClick={sim.resetAll}>
              Đặt lại mọi thay đổi
            </Button>
          </div>
        </>
      )}
    </aside>
  );
}

// Một nhóm thanh kéo, có thể thu gọn. Thanh tiêu đề màu thương hiệu như bản demo CollegeVine.
function Section({
  title,
  changed,
  onReset,
  children,
}: {
  title: string;
  changed: boolean;
  onReset: () => void;
  children: ReactNode;
}) {
  return (
    <details open className="group border-t border-line">
      <summary className="flex cursor-pointer list-none items-center justify-between bg-brand px-4 py-2.5 text-sm font-semibold text-white [&::-webkit-details-marker]:hidden">
        <span>
          {title}
          {changed && <span className="ml-2 rounded-full bg-white/20 px-2 py-0.5 text-[10px] font-medium">đã đổi</span>}
        </span>
        <ChevronDownIcon className="size-4 transition-transform group-open:rotate-180" />
      </summary>
      <div className="space-y-4 px-4 py-4">
        {children}
        <button
          type="button"
          onClick={onReset}
          disabled={!changed}
          className="text-xs text-brand underline-offset-4 hover:underline disabled:cursor-default disabled:text-ink-3 disabled:no-underline"
        >
          Đặt lại phần này
        </button>
      </div>
    </details>
  );
}

interface SliderRowProps {
  label: string;
  base: number | null; // giá trị hồ sơ thật
  value: number | null; // giá trị giả định (null = chưa đổi)
  min: number;
  max: number;
  step: number;
  fallback: number; // vị trí ban đầu của thanh khi hồ sơ chưa có giá trị
  colorDelta?: boolean; // tô xanh khi tăng, đỏ khi giảm
  onChange: (value: number) => void;
}

// Một thanh kéo kiểu "HỒ SƠ x · GIẢ ĐỊNH y +z"
function SliderRow({ label, base, value, min, max, step, fallback, colorDelta = true, onChange }: SliderRowProps) {
  const current = value ?? base ?? fallback;
  const delta = value != null && base != null && value !== base ? value - base : null;

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-foreground">{label}</p>
      <div className="flex items-end justify-between gap-3">
        <div>
          <p className="text-[10px] font-medium tracking-wide text-ink-3 uppercase">Hồ sơ</p>
          <p className="text-lg font-semibold text-brand">{base != null ? NUMBER.format(base) : "Chưa có"}</p>
        </div>
        <div className="text-right">
          <p className="text-[10px] font-medium tracking-wide text-ink-3 uppercase">Giả định</p>
          <p className="text-lg font-semibold text-foreground">
            {NUMBER.format(current)}
            {delta != null && (
              <span
                className={cn(
                  "ml-1.5 text-sm font-medium",
                  colorDelta ? (delta > 0 ? "text-ok" : "text-risk") : "text-ink-2",
                )}
              >
                {delta > 0 ? "+" : "−"}
                {NUMBER.format(Math.abs(delta))}
              </span>
            )}
          </p>
        </div>
      </div>
      <Slider
        value={[current]}
        min={min}
        max={max}
        step={step}
        aria-label={label}
        onValueChange={(v) => onChange(Array.isArray(v) ? v[0] : v)}
      />
      <div className="flex justify-between text-[10px] text-ink-3">
        <span>{NUMBER.format(min)}</span>
        <span>{NUMBER.format(max)}</span>
      </div>
    </div>
  );
}
