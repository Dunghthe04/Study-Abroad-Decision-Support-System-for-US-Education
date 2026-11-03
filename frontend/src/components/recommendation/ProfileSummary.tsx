"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { Button, buttonVariants } from "@/components/ui/button";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { countMonths, draftGpa } from "@/lib/profile-flow-options";
import { cn } from "@/lib/utils";
import type {
  ImpactLevel,
  LegacyCertificationDraft,
  ProfileFlowDraft,
  ProfileMetadataResponse,
} from "@/types/profile-flow";
import type { Recommendation } from "@/types/recommendation";

// Điểm số của hồ sơ dùng cho khối đánh giá (dựng từ nháp 6 bước)
interface StudentProfile {
  major: string;
  gpa4: number | null;
  sat: number | null;
  ielts: number | null;
  toefl: number | null;
  duolingo: number | null;
  budgetMinUsd: number | null;
  budgetMaxUsd: number | null;
}

const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});
const usd = (v: number) => money.format(v);

// Mức ngân sách: khoảng min–max, "Dưới max" hoặc "Trên min" (mức cao nhất không có trần)
function budgetLabel(s: StudentProfile): string | null {
  const min = s.budgetMinUsd && s.budgetMinUsd > 0 ? s.budgetMinUsd : null;
  const max = s.budgetMaxUsd ?? null;
  if (min && max) return `${usd(min)} – ${usd(max)}`;
  if (max) return `Dưới ${usd(max)}`;
  if (min) return `Trên ${usd(min)}`;
  return null;
}

// Quy TOEFL / Duolingo về mức IELTS tương đương để xếp mức tiếng Anh
function ieltsEquivalent(s: StudentProfile): number | null {
  if (s.ielts) return s.ielts;
  if (s.toefl) return s.toefl >= 110 ? 8 : s.toefl >= 94 ? 7 : s.toefl >= 79 ? 6 : s.toefl >= 72 ? 5.5 : 5;
  if (s.duolingo) return s.duolingo >= 135 ? 7.5 : s.duolingo >= 120 ? 7 : s.duolingo >= 105 ? 6 : s.duolingo >= 95 ? 5.5 : 5;
  return null;
}

interface ProfileLevel {
  label: string;
  value: string;
  detail?: string | null;
}

const ROUTE_LABEL = {
  direct: "Đại học thẳng",
  pathway: "Năm dự bị (pathway)",
  cc_2plus2: "Cao đẳng cộng đồng 2+2",
} as const;

// Đánh giá hồ sơ theo ngưỡng của bản demo: mức từng mặt, điểm mạnh, điểm cần cải thiện, lộ trình
function assessProfile(s: StudentProfile, ecTier: number | null, activitiesCount: number) {
  const g4 = s.gpa4;
  const sat = s.sat;
  const ie = ieltsEquivalent(s);
  const budget = s.budgetMaxUsd ?? s.budgetMinUsd ?? null;

  const academic = g4 == null ? "Chưa có" : g4 >= 3.5 ? "Mạnh" : g4 >= 3.0 ? "Trung bình" : "Yếu";
  const satLv = sat == null ? "Chưa thi" : sat >= 1400 ? "Mạnh" : sat >= 1200 ? "Trung bình" : "Yếu";
  const english = ie == null ? "Chưa có" : ie >= 7 ? "Đạt tốt" : ie >= 6 ? "Đạt" : ie >= 5.5 ? "Gần đạt" : "Chưa đạt";
  const finance = budget == null ? "Chưa đặt" : budget >= 60000 ? "Đủ" : budget >= 35000 ? "Vừa đủ" : "Thiếu";

  const strengths: string[] = [];
  const weak: string[] = [];
  if (academic === "Mạnh") strengths.push(`Điểm trung bình quy đổi ${g4} trên thang 4 ở mức mạnh.`);
  if (academic === "Trung bình")
    weak.push(
      `Điểm trung bình quy đổi ${g4} trên thang 4 ở mức trung bình. Giữ hoặc tăng điểm học kỳ tới sẽ mở thêm trường.`
    );
  if (academic === "Yếu") weak.push(`Điểm trung bình quy đổi ${g4} trên thang 4 còn thấp so với đa số trường.`);
  if (satLv === "Mạnh") strengths.push(`SAT ${sat} ở mức mạnh.`);
  if (satLv === "Trung bình")
    weak.push(`SAT ${sat} ở mức trung bình. Thi lại lên khoảng 1400 giúp nhiều trường chuyển nhóm.`);
  if (satLv === "Chưa thi") weak.push("Chưa có SAT. Hệ thống ưu tiên trường không bắt buộc SAT.");
  if (english === "Đạt tốt" || english === "Đạt")
    strengths.push(`Tiếng Anh đạt yêu cầu của phần lớn trường (tương đương IELTS ${ie}).`);
  if (english === "Gần đạt" || english === "Chưa đạt")
    weak.push("Tiếng Anh chưa đạt mức nhiều trường yêu cầu. Nên thi lại hoặc cân nhắc năm dự bị.");
  if (english === "Chưa có") weak.push("Chưa có chứng chỉ tiếng Anh. Cần thi trước hạn nộp ít nhất 2 tháng.");
  if (ecTier != null && ecTier >= 4) strengths.push("Hoạt động ngoại khóa có vai trò và phạm vi tốt.");
  if (ecTier == null || ecTier <= 2)
    weak.push("Hoạt động ngoại khóa còn ít. Một hoạt động có vai trò rõ sẽ giúp hồ sơ.");
  if (finance === "Thiếu") weak.push("Ngân sách dưới 35.000 USD mỗi năm, lựa chọn trường sẽ hẹp.");

  const route: keyof typeof ROUTE_LABEL =
    english === "Chưa đạt" ? "pathway" : finance === "Thiếu" || academic === "Yếu" ? "cc_2plus2" : "direct";

  const englishDetail = s.ielts
    ? `IELTS ${s.ielts}`
    : s.toefl
      ? `TOEFL ${s.toefl}`
      : s.duolingo
        ? `Duolingo ${s.duolingo}`
        : null;

  const levels: ProfileLevel[] = [
    { label: "Học lực", value: academic, detail: g4 != null ? `GPA ${g4.toFixed(2)} / 4` : null },
    { label: "SAT", value: satLv, detail: sat != null ? String(sat) : null },
    { label: "Tiếng Anh", value: english, detail: englishDetail },
    { label: "Tài chính", value: finance, detail: budgetLabel(s) },
    {
      label: "Ngoại khóa",
      value: ecTier != null ? `Bậc ${ecTier}/5` : "Chưa có",
      detail: activitiesCount > 0 ? `${activitiesCount} hoạt động` : null,
    },
    { label: "Lộ trình", value: ROUTE_LABEL[route] },
  ];

  return { levels, strengths, weak };
}

const clamp = (x: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, x));
const round2 = (x: number) => Math.round(x * 100) / 100;

const IMPACT_RANK: Record<ImpactLevel, number> = { school: 1, district: 2, province: 3, national: 4, international: 5 };
const ROLE_WEIGHT = { founder: 1, leader: 1, vice_leader: 0.75, member: 0.5 } as const;

// Bậc ngoại khóa 1–5 theo công thức ecTier của bản demo; chỉ dùng khi lần gợi ý không có bậc
function ecTierOf(activities: ProfileFlowDraft["step5"]["activities"]): number {
  const acts = activities
    .filter((a) => a.category === "activity")
    .map((a) => {
      const level = a.impactLevel ? IMPACT_RANK[a.impactLevel] : 1;
      const role = a.role ? ROLE_WEIGHT[a.role] : 0.5;
      const months =
        (a.startDate ? countMonths(a.startDate, a.endDate ?? null, !!a.isOngoing) : null) ?? a.durationMonths ?? 3;
      return (0.4 * level) / 5 + 0.35 * role + 0.25 * Math.min(months / 24, 1);
    })
    .sort((p, q) => q - p)
    .slice(0, 4);
  const bonus = Math.min(
    0.5,
    activities
      .filter((a) => a.category === "achievement")
      .slice(0, 3)
      .reduce((s, a) => s + 0.05 * (a.impactLevel ? IMPACT_RANK[a.impactLevel] : 1), 0)
  );
  const m = (acts.reduce((s, x) => s + x, 0) + bonus) / 4;
  return m < 0.1 ? 1 : m < 0.25 ? 2 : m < 0.45 ? 3 : m < 0.65 ? 4 : 5;
}

interface FlowProfile {
  student: StudentProfile;
  gpa10: number | null;
  states: string[];
  ecTier: number;
  activitiesCount: number;
}

// Lần gợi ý không trả điểm số hồ sơ → dựng lại từ nháp 6 bước (cùng nguồn backend dùng khi lưu hồ sơ)
function profileFromDraft(draft: ProfileFlowDraft, meta: ProfileMetadataResponse): FlowProfile {
  const { gpa10, gpa4 } = draftGpa(draft.step2);

  // Mỗi loại chứng chỉ có profileField (ielts/toefl/duolingo/sat); lấy điểm cao nhất như backend
  const scores: Record<string, number> = {};
  const put = (field: string, v: number | null | undefined) => {
    if (v != null && v > (scores[field] ?? -1)) scores[field] = v;
  };
  for (const c of draft.step3?.certifications ?? []) {
    const field = meta.certificationTypes.find((t) => t.id === c.type)?.profileField;
    if (field) put(field.toLowerCase(), c.overall);
  }
  const legacy = draft.step3 as Partial<LegacyCertificationDraft> | undefined;
  if (legacy?.englishTestType && legacy.englishTestType !== "none")
    put(legacy.englishTestType.toLowerCase(), legacy.englishOverallScore);
  for (const t of legacy?.otherTests ?? []) if (t.testType === "SAT") put("sat", t.score);

  const tier = meta.budgetTiers.find((t) => t.key === draft.step4?.annualBudgetTier);
  const activities = draft.step5?.activities ?? [];
  const stateName = new Map(meta.states.map((s) => [s.code, s.name]));

  return {
    student: {
      major: draft.step1?.targetMajor ?? "",
      gpa4,
      sat: scores.sat ?? null,
      ielts: scores.ielts ?? null,
      toefl: scores.toefl ?? null,
      duolingo: scores.duolingo ?? null,
      budgetMinUsd: tier?.minUsd ?? null,
      budgetMaxUsd: tier?.maxUsd ?? null,
    },
    gpa10,
    states: (draft.step1?.targetStates ?? []).map((c) => stateName.get(c) ?? c),
    ecTier: ecTierOf(activities),
    activitiesCount: activities.filter((a) => a.category === "activity").length,
  };
}

interface SimValues {
  gpa10?: number;
  sat?: number;
  budget?: number;
}

interface ProfileSummaryProps {
  rec: Recommendation | null;
}

// Thẻ đầu trang gợi ý: bộ lọc, đánh giá hồ sơ, hàng "Thử thay đổi hồ sơ".
// Mô phỏng chỉ tính lại khối đánh giá trên máy, không gọi backend hay AI và không đổi danh sách trường.
export function ProfileSummary({ rec }: ProfileSummaryProps) {
  const [flowProfile, setFlowProfile] = useState<FlowProfile | null>(null);
  const [sim, setSim] = useState<SimValues>({});

  // Đọc nháp 6 bước một lần; lỗi thì ẩn khối đánh giá
  useEffect(() => {
    let cancelled = false;
    Promise.all([profileFlowApi.getFlowState(), profileFlowApi.getMetadata()])
      .then(([state, meta]) => {
        if (!cancelled && state?.draft) setFlowProfile(profileFromDraft(state.draft, meta));
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  const realStudent = flowProfile?.student ?? null;
  const realBudget = realStudent
    ? (realStudent.budgetMaxUsd ?? realStudent.budgetMinUsd ?? rec?.criteria.budget ?? null)
    : null;
  const simulating = Object.keys(sim).length > 0;
  const student = useMemo<StudentProfile | null>(() => {
    if (!realStudent || !simulating) return realStudent;
    return {
      ...realStudent,
      // Dịch GPA thang 4 đúng bằng phần chênh thang 10 (× 0,4) để khớp cách quy đổi của hồ sơ thật
      gpa4:
        sim.gpa10 == null
          ? realStudent.gpa4
          : clamp(round2((realStudent.gpa4 ?? 0) + (sim.gpa10 - (flowProfile?.gpa10 ?? sim.gpa10)) * 0.4), 0, 4),
      sat: sim.sat ?? realStudent.sat,
      ...(sim.budget != null && { budgetMinUsd: null, budgetMaxUsd: sim.budget }),
    };
  }, [realStudent, simulating, sim, flowProfile]);

  // Bấm −/+ như bản demo; về đúng giá trị thật thì bỏ khỏi mô phỏng
  const bump = (key: keyof SimValues, dir: 1 | -1) => {
    const real = { gpa10: flowProfile?.gpa10 ?? null, sat: realStudent?.sat ?? null, budget: realBudget }[key];
    const cur = sim[key] ?? real;
    const step = { gpa10: 0.1, sat: 10, budget: 5000 }[key];
    const [lo, hi] = { gpa10: [0, 10], sat: [400, 1600], budget: [5000, 200000] }[key];
    const fallback = { gpa10: 7, sat: 1200, budget: 40000 }[key];
    const raw = cur == null ? fallback : cur + dir * step;
    const v = clamp(key === "gpa10" ? Math.round(raw * 10) / 10 : round2(raw), lo, hi); // điểm TB giữ 1 chữ số lẻ như ô hiển thị
    setSim((prev) => {
      const next = { ...prev, [key]: v };
      if (real != null && Math.abs(real - v) < 1e-9) delete next[key];
      return next;
    });
  };

  const simLabel = [
    sim.gpa10 != null && `Điểm TB ${sim.gpa10}`,
    sim.sat != null && `SAT ${sim.sat}`,
    sim.budget != null && `ngân sách ${sim.budget / 1000}k`,
  ]
    .filter(Boolean)
    .join(", ");

  // Bậc ngoại khóa: ưu tiên bậc backend đã chấm trong lần gợi ý, không có thì tính từ nháp
  const ecTier = rec?.criteria.tier ?? flowProfile?.ecTier ?? null;
  const assessment = student ? assessProfile(student, ecTier, flowProfile?.activitiesCount ?? 0) : null;
  const major = rec?.criteria.major || realStudent?.major || "Mọi ngành";
  const states = flowProfile?.states.length ? flowProfile.states : (rec?.criteria.states ?? []);
  const budgetText = realStudent && budgetLabel(realStudent);
  const filterText = [
    !rec?.studyLevel || rec.studyLevel === "undergraduate" ? "Đại học" : rec.studyLevel,
    major,
    states.length ? states.join(", ") : "Mọi bang",
    budgetText
      ? `${budgetText}/năm`
      : rec?.criteria.budget != null
        ? `${usd(rec.criteria.budget)}/năm`
        : "Chưa đặt ngân sách",
  ].join(" · ");
  const shownGpa10 = sim.gpa10 ?? flowProfile?.gpa10 ?? null;
  const shownSat = sim.sat ?? realStudent?.sat ?? null;
  const shownBudget = sim.budget ?? realBudget;

  if (!rec && !flowProfile) return null;

  return (
    <div className="space-y-4">
      <section className="space-y-5 rounded-xl border border-line bg-surface p-[18px] sm:p-7">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 space-y-1.5">
            <div className="text-[11.5px] font-bold tracking-[0.07em] text-brand uppercase">Hồ sơ đã gửi</div>
            <p className="text-[13.5px] text-muted-foreground">Bộ lọc từ hồ sơ: {filterText}</p>
          </div>
          <Link
            href="/analysis?edit=1"
            className={buttonVariants({ variant: "outline", className: "h-[42px] px-4 print:hidden" })}
          >
            Sửa hồ sơ
          </Link>
        </div>

        {assessment && (
          <div className="space-y-2.5">
            <div className="text-[15px] font-semibold text-foreground">Đánh giá hồ sơ</div>
            <div className="grid grid-cols-[repeat(auto-fit,minmax(110px,1fr))] gap-3">
              {assessment.levels.map((lv) => (
                <Level key={lv.label} {...lv} />
              ))}
            </div>
            {(assessment.strengths.length > 0 || assessment.weak.length > 0) && (
              <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-3.5 text-sm">
                {assessment.strengths.length > 0 && (
                  <div>
                    <b className="font-semibold">Điểm mạnh</b>
                    <ul className="list-disc space-y-0.5 pl-[18px] text-[13.5px]">
                      {assessment.strengths.map((s) => (
                        <li key={s}>{s}</li>
                      ))}
                    </ul>
                  </div>
                )}
                {assessment.weak.length > 0 && (
                  <div>
                    <b className="font-semibold">Cần cải thiện</b>
                    <ul className="list-disc space-y-0.5 pl-[18px] text-[13.5px]">
                      {assessment.weak.map((s) => (
                        <li key={s}>{s}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        {realStudent && (
          <>
            <hr className="border-line print:hidden" />
            <div className="flex flex-wrap items-end justify-between gap-3 print:hidden">
              <div className="space-y-2">
                <div className="text-[15px] font-semibold text-foreground">Thử thay đổi hồ sơ</div>
                <div className="flex flex-wrap gap-3">
                  {flowProfile?.gpa10 != null && (
                    <Stepper
                      label="Điểm TB (thang 10)"
                      value={shownGpa10 != null ? shownGpa10.toFixed(1) : "—"}
                      changed={sim.gpa10 != null}
                      onBump={(d) => bump("gpa10", d)}
                    />
                  )}
                  <Stepper
                    label="SAT"
                    value={shownSat != null ? String(shownSat) : "Chưa thi"}
                    changed={sim.sat != null}
                    onBump={(d) => bump("sat", d)}
                  />
                  <Stepper
                    label="Ngân sách / năm"
                    value={shownBudget != null ? `${Math.round(shownBudget / 1000)}k` : "Chưa đặt"}
                    changed={sim.budget != null}
                    onBump={(d) => bump("budget", d)}
                  />
                </div>
              </div>
              <p className="text-[12.5px] text-muted-foreground">Chỉnh ở đây hoặc hỏi trong khung chat</p>
            </div>
          </>
        )}
      </section>

      {simulating && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-warn/40 bg-warn-wash px-[18px] py-3 print:hidden">
          <div className="min-w-0 space-y-0.5 text-[13.5px] text-warn">
            <b className="font-semibold">Đang xem mô phỏng: {simLabel}</b>
            <p>Hồ sơ thật chưa đổi. Khối đánh giá ở trên tính lại trên máy bạn, danh sách trường giữ nguyên.</p>
          </div>
          <Button type="button" variant="outline" onClick={() => setSim({})} className="h-9 px-3.5">
            Thoát mô phỏng
          </Button>
        </div>
      )}
    </div>
  );
}

// Một ô trong lưới "Đánh giá hồ sơ"
function Level({ label, value, detail }: ProfileLevel) {
  return (
    <div className="flex min-w-0 flex-col gap-0.5 rounded-[10px] bg-ground px-3 py-2.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <b className="text-[14.5px] font-semibold text-foreground">{value}</b>
      {detail && <span className="text-numeric truncate text-xs text-muted-foreground">{detail}</span>}
    </div>
  );
}

// Nút −/+ của hàng "Thử thay đổi hồ sơ"; đang khác hồ sơ thật thì tô màu mô phỏng
function Stepper({
  label,
  value,
  changed,
  onBump,
}: {
  label: string;
  value: string;
  changed: boolean;
  onBump: (dir: 1 | -1) => void;
}) {
  const btn =
    "flex w-[34px] cursor-pointer items-center justify-center text-lg text-muted-foreground transition-colors hover:text-foreground";
  return (
    <div className="space-y-1">
      <div className="text-[12.5px] text-muted-foreground">{label}</div>
      <div
        className={cn(
          "flex h-10 items-stretch overflow-hidden rounded-[9px] border",
          changed ? "border-warn/50 bg-warn-wash" : "border-line bg-surface"
        )}
      >
        <button type="button" aria-label={`Giảm ${label}`} onClick={() => onBump(-1)} className={btn}>
          −
        </button>
        <span
          className={cn(
            "text-numeric flex min-w-[64px] items-center justify-center px-1 text-sm font-semibold",
            changed ? "text-warn" : "text-foreground"
          )}
        >
          {value}
        </span>
        <button type="button" aria-label={`Tăng ${label}`} onClick={() => onBump(1)} className={btn}>
          +
        </button>
      </div>
    </div>
  );
}
