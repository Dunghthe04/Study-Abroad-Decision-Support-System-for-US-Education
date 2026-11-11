"use client";

import type { ReactNode } from "react";
import { ChevronDownIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Slider } from "@/components/ui/slider";
import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";
import type { ProfileSimulation } from "@/lib/use-profile-simulation";
import type { Pref, ScholarshipNeed, SimulationOverrides } from "@/types/recommendation";

type NumericKey = "gpa" | "sat" | "act" | "ielts" | "toefl" | "duolingo" | "budgetUsd" | "ecTier";

const NUMBER = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

const SCHOLARSHIP_OPTIONS: { value: ScholarshipNeed; label: string }[] = [
  { value: "optional", label: "Không cần" },
  { value: "preferred", label: "Nên có" },
  { value: "mandatory", label: "Bắt buộc" },
];

// Tên từng mức ưu tiên (0 đến 4), để học sinh không phải đoán các con số
const PREF_LEVELS = ["Không quan tâm", "Ít quan tâm", "Bình thường", "Quan tâm nhiều", "Quan tâm nhất"];

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
    <aside className="w-full shrink-0 overflow-hidden border-b border-line bg-surface lg:sticky lg:top-(--header-h) lg:h-[calc(100vh-var(--header-h))] lg:w-[320px] lg:overflow-y-auto lg:border-r lg:border-b-0">
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

          <Section
            title="Tiếng Anh"
            onReset={() => sim.reset(["ielts", "toefl", "duolingo"])}
            changed={o.ielts != null || o.toefl != null || o.duolingo != null}
          >
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
            <SliderRow
              label="TOEFL iBT"
              base={profile.toefl ?? null}
              value={o.toefl ?? null}
              min={0}
              max={120}
              step={1}
              fallback={90}
              onChange={(v) => setNumber("toefl", profile.toefl ?? null, v)}
            />
            <SliderRow
              label="Duolingo"
              base={profile.duolingo ?? null}
              value={o.duolingo ?? null}
              min={10}
              max={160}
              step={5}
              fallback={115}
              onChange={(v) => setNumber("duolingo", profile.duolingo ?? null, v)}
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

          <Section title="Ngoại khóa" changed={false}>
            <ExtracurricularSummary
              tier={profile.ecTier}
              activities={profile.activityCount ?? 0}
              awards={profile.awardCount ?? 0}
            />
          </Section>

          <Section title="Ưu tiên của bạn" onReset={() => sim.reset(["pref"])} changed={o.pref != null}>
            <p className="text-xs text-muted-foreground">
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
                levels={PREF_LEVELS}
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
// Tên mức ngoại khóa (bậc 1 đến 5) bằng chữ, để học sinh không phải biết "bậc" là gì
const EC_LEVELS = ["Chưa nổi bật", "Cơ bản", "Trung bình", "Mạnh", "Rất mạnh"];

// Ngoại khóa chỉ đọc: mức hiện tại, số hoạt động và giải thưởng đã khai, nút sang trang khai báo.
// Mở tab mới để các giá trị giả định đang chỉnh ở đây không bị mất.
function ExtracurricularSummary({ tier, activities, awards }: { tier: number; activities: number; awards: number }) {
  const declared = activities + awards > 0;
  const level = Math.min(Math.max(tier, 1), 5);
  return (
    <div className="space-y-3">
      {declared ? (
        <>
          <div>
            <p className="text-xs font-medium text-ink-3">Mức hiện tại</p>
            <p className="text-lg font-semibold text-brand">{EC_LEVELS[level - 1]}</p>
            <div aria-hidden className="mt-1 flex gap-1">
              {EC_LEVELS.map((name, i) => (
                <span key={name} className={cn("size-2 rounded-full", i < level ? "bg-brand" : "bg-line")} />
              ))}
            </div>
          </div>
          <p className="text-sm text-ink-2">
            Bạn đã khai {activities} hoạt động, {awards} giải thưởng.
          </p>
        </>
      ) : (
        <p className="text-sm text-ink-2">Bạn chưa khai hoạt động ngoại khóa hay giải thưởng nào.</p>
      )}
      <Button
        variant="outline"
        size="sm"
        nativeButton={false}
        role="link"
        className="text-xs font-semibold text-brand hover:bg-brand-wash"
        render={<a href="/analysis?edit=1" target="_blank" rel="noopener noreferrer" />}
      >
        {declared ? "+ Thêm hoạt động hoặc giải thưởng" : "+ Khai báo ngoại khóa"}
      </Button>
    </div>
  );
}

function Section({
  title,
  changed,
  onReset,
  children,
}: {
  title: string;
  changed: boolean;
  onReset?: () => void; // bỏ trống thì không có nút đặt lại (mục chỉ đọc)
  children: ReactNode;
}) {
  return (
    <details open className="group border-t border-line">
      <summary className="flex cursor-pointer list-none items-center justify-between bg-brand px-4 py-2.5 text-sm font-semibold text-white [&::-webkit-details-marker]:hidden">
        <span>
          {title}
          {changed && <span className="ml-2 rounded-full bg-white/20 px-2 py-0.5 text-xs font-medium">đã đổi</span>}
        </span>
        <ChevronDownIcon className="size-4 transition-transform group-open:rotate-180" />
      </summary>
      <div className="space-y-4 px-4 py-4">
        {children}
        {onReset && (
          <button
            type="button"
            onClick={onReset}
            disabled={!changed}
            className="text-xs text-brand underline-offset-4 hover:underline disabled:cursor-default disabled:text-ink-3 disabled:no-underline"
          >
            Đặt lại phần này
          </button>
        )}
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
  levels?: string[]; // tên từng mức (theo chỉ số 0, 1, 2...) khi thang là mức độ chứ không phải điểm số
  onChange: (value: number) => void;
}

// Một thanh kéo kiểu "HỒ SƠ x · GIẢ ĐỊNH y +z"
function SliderRow({ label, base, value, min, max, step, fallback, colorDelta = true, levels, onChange }: SliderRowProps) {
  const current = value ?? base ?? fallback;
  const delta = value != null && base != null && value !== base ? value - base : null;

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-foreground">{label}</p>
      <div className="flex items-end justify-between gap-3">
        <div>
          <p className="text-[13px] font-medium text-ink-3">Hồ sơ</p>
          <p className={cn("font-semibold text-brand", levels ? "text-base" : "text-lg")}>
            {base != null ? (levels ? levels[base] : NUMBER.format(base)) : "Chưa có"}
          </p>
        </div>
        <div className="text-right">
          <p className="text-[13px] font-medium text-ink-3">Giả định</p>
          <p className={cn("font-semibold text-foreground", levels ? "text-base" : "text-lg")}>
            {levels ? levels[current] : NUMBER.format(current)}
            {delta != null && (
              <span
                className={cn(
                  "ml-1.5 text-sm font-medium",
                  colorDelta ? (delta > 0 ? "text-ok" : "text-risk") : "text-ink-2",
                )}
              >
                {delta > 0 ? "+" : "−"}
                {NUMBER.format(Math.abs(delta))}
                {levels && " mức"}
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
        getAriaValueText={(_text, v) => (levels ? (levels[v] ?? NUMBER.format(v)) : NUMBER.format(v))}
        onValueChange={(v) => onChange(Array.isArray(v) ? v[0] : v)}
      />
      {levels && (
        // Một vạch cho mỗi mức, canh với vị trí núm kéo (lề 7px bằng nửa bề rộng núm)
        <div aria-hidden className="-mt-1 flex justify-between px-[7px]">
          {levels.map((name, i) => (
            <span key={name} className={cn("h-1.5 w-px", i === current ? "bg-brand" : "bg-ink-3/60")} />
          ))}
        </div>
      )}
      <div className="flex justify-between text-xs text-ink-3">
        <span>{levels ? levels[min] : NUMBER.format(min)}</span>
        <span>{levels ? levels[max] : NUMBER.format(max)}</span>
      </div>
    </div>
  );
}
