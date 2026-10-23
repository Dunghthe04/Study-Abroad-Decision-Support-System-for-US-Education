"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api";
import { createRecommendation, getLatestRecommendation, getSchoolAdvice } from "@/lib/recommendation-api";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { calculateLinearGpa, countMonths } from "@/lib/profile-flow-options";
import type {
  ImpactLevel,
  LegacyCertificationDraft,
  ProfileFlowDraft,
  ProfileMetadataResponse,
} from "@/types/profile-flow";
import type {
  RecommendationBlock,
  RecommendationItem,
  RecommendationResult,
  SchoolAdvice,
  StudentSummary,
} from "@/types/recommendation";
import {
  AlertCircleIcon,
  CheckIcon,
  ChevronUpIcon,
  ExternalLinkIcon,
  RefreshCwIcon,
  SparklesIcon,
  TriangleAlertIcon,
  XIcon,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAssistant, useAssistantPageContext } from "@/components/assistant/AssistantProvider";
import { cn } from "@/lib/utils";

export type FlowCategory = "reach" | "match" | "safety" | "financial_reach" | "insufficient_data";

interface TierMeta {
  key: FlowCategory;
  label: string;
  badgeVariant: "destructive" | "brand" | "ok" | "warn" | "secondary";
  hint: string;
  badgeText: string;
}

const TIERS: TierMeta[] = [
  {
    key: "reach",
    label: "Khó vào",
    badgeVariant: "destructive",
    badgeText: "Mục tiêu cao",
    hint: "Hồ sơ hiện tại thấp hơn mức thường nhận. Nên xem như mục tiêu cao.",
  },
  {
    key: "match",
    label: "Vừa sức",
    badgeVariant: "brand",
    badgeText: "Trọng tâm",
    hint: "Hồ sơ nằm trong mức thường nhận. Nhóm nên tập trung nhất.",
  },
  {
    key: "safety",
    label: "An toàn hơn",
    badgeVariant: "ok",
    badgeText: "Vững chắc",
    hint: "Hồ sơ cao hơn mức thường nhận, khả năng đậu cao.",
  },
  {
    key: "financial_reach",
    label: "Chỉ đi được nếu có học bổng lớn",
    badgeVariant: "warn",
    badgeText: "Cần học bổng lớn",
    hint: "Chi phí vượt ngân sách của bạn nếu không có học bổng lớn.",
  },
  {
    key: "insufficient_data",
    label: "Chưa đủ dữ liệu",
    badgeVariant: "secondary",
    badgeText: "Chưa đủ dữ liệu",
    hint: "Trường chưa công bố đủ dữ liệu tuyển sinh GPA/SAT để xếp nhóm.",
  },
];

type PriorityPreset = "balanced" | "low_cost" | "high_scholarship" | "reputation";

interface PriorityWeights {
  cost: number;
  scholarship: number;
  quality: number;
}

// 4 nút = 4 bộ mức quan tâm trong GOI_Y_TRUONG_TONG_QUAN: Cân bằng [2,2,2], Chi phí thấp [3,1,1],
// Học bổng nhiều [1,3,1], Danh tiếng [1,1,3]; trọng số = mức / tổng mức (cùng công thức SchoolPicker ở backend)
const PRESET_WEIGHTS: Record<PriorityPreset, PriorityWeights> = {
  balanced: { cost: 1 / 3, scholarship: 1 / 3, quality: 1 / 3 },
  low_cost: { cost: 0.6, scholarship: 0.2, quality: 0.2 },
  high_scholarship: { cost: 0.2, scholarship: 0.6, quality: 0.2 },
  reputation: { cost: 0.2, scholarship: 0.2, quality: 0.6 },
};

const PRESET_CHIPS: [PriorityPreset, string][] = [
  ["balanced", "Cân bằng"],
  ["low_cost", "Chi phí thấp"],
  ["high_scholarship", "Học bổng nhiều"],
  ["reputation", "Danh tiếng"],
];

const TIER_TONE: Record<FlowCategory, string> = {
  reach: "bg-risk-wash text-risk",
  match: "bg-brand-wash text-brand",
  safety: "bg-ok-wash text-ok",
  financial_reach: "bg-warn-wash text-warn",
  insufficient_data: "bg-muted text-muted-foreground",
};

const RESULT_SUGGESTIONS = [
  "Hồ sơ của em mạnh ở đâu?",
  "Nên nộp bao nhiêu trường mỗi nhóm?",
  "Làm sao để lên nhóm Vừa sức?",
];

const SAT_POLICY: Record<string, string> = {
  required: "bắt buộc",
  optional: "không bắt buộc",
  not_accepted: "không xét",
};

// Nhãn khả năng trúng tuyển, hiện trong khối "Chỉ đi được nếu có học bổng lớn"
function academicLabel(item: RecommendationItem): string {
  if (item.farReach) return "Khó vào xa";
  switch (item.category) {
    case "reach":
      return "Khó vào";
    case "match":
      return "Vừa sức";
    case "safety":
      return "An toàn hơn";
    default:
      return "Chưa đủ dữ liệu";
  }
}

const money = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 0,
});
const usd = (v: number | null | undefined) => (v == null ? null : money.format(v));

// Mức ngân sách: khoảng min–max, "Dưới max", "Trên min" (mức cao nhất không có trần) hoặc số cũ annualBudgetUsd
function budgetLabel(s: StudentSummary): string | null {
  const min = s.budgetMinUsd && s.budgetMinUsd > 0 ? s.budgetMinUsd : null;
  const max = s.budgetMaxUsd ?? null;
  if (min && max) return `${usd(min)} – ${usd(max)}`;
  if (max) return `Dưới ${usd(max)}`;
  if (min) return `Trên ${usd(min)}`;
  return s.annualBudgetUsd ? usd(s.annualBudgetUsd) : null;
}

// Quy TOEFL / Duolingo về mức IELTS tương đương để xếp mức tiếng Anh
function ieltsEquivalent(s: StudentSummary): number | null {
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
function assessProfile(s: StudentSummary, ecTier: number | null, activitiesCount: number) {
  const g4 = s.gpa4;
  const sat = s.sat;
  const ie = ieltsEquivalent(s);
  const budget = s.budgetMaxUsd ?? s.annualBudgetUsd ?? s.budgetMinUsd ?? null;

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

// Mức học bổng từ điểm hỗ trợ tài chính (0–1)
function aidLabel(aidFit: number | null | undefined): string {
  if (aidFit == null) return "—";
  return aidFit >= 0.6 ? "Nhiều" : aidFit >= 0.3 ? "Vừa" : "Ít";
}

// Độ phù hợp theo ưu tiên đang chọn (điểm X × 100)
function fitLabel(fit: number | null): string {
  if (fit == null) return "—";
  return fit > 55 ? "Cao" : fit > 40 ? "Vừa" : "Thấp";
}

const clamp = (x: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, x));
const round2 = (x: number) => Math.round(x * 100) / 100;

const IMPACT_RANK: Record<ImpactLevel, number> = { school: 1, district: 2, province: 3, national: 4, international: 5 };
const ROLE_WEIGHT = { founder: 1, leader: 1, vice_leader: 0.75, member: 0.5 } as const;

// Bậc ngoại khóa 1–5 theo công thức ecTier của bản demo (backend chấm bằng AI nhưng không trả bậc về)
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
  student: StudentSummary;
  gpa10: number | null;
  states: string[];
  ecTier: number;
  activitiesCount: number;
}

// Backend không trả hồ sơ kèm kết quả gợi ý → dựng lại từ nháp 6 bước (cùng nguồn backend dùng khi lưu hồ sơ)
function profileFromDraft(draft: ProfileFlowDraft, meta: ProfileMetadataResponse): FlowProfile {
  const linear = calculateLinearGpa(draft.step2?.academicYears ?? []);
  const manual10 = draft.step2?.manualGpa10Scale ?? null;
  const gpa10 = manual10 ?? linear.gpa10;
  const gpa4 = draft.step2?.calculatedGpa4Scale ?? (manual10 != null ? round2(manual10 * 0.4) : linear.gpa4);

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

// Bảng z → điểm vị trí của SchoolScorer.cs
const Z_TABLE: [number, number][] = [
  [-2, 2], [-1.5, 7], [-1, 16], [-0.5, 31], [0, 50], [0.5, 69], [1, 84], [1.5, 93], [2, 98],
];
function zToPosition(z: number): number {
  if (z <= Z_TABLE[0][0]) return Z_TABLE[0][1];
  for (let i = 0; i < Z_TABLE.length - 1; i++) {
    const [z1, p1] = Z_TABLE[i];
    const [z2, p2] = Z_TABLE[i + 1];
    if (z <= z2) return p1 + ((p2 - p1) * (z - z1)) / (z2 - z1);
  }
  return Z_TABLE[Z_TABLE.length - 1][1];
}

// Điểm S gần đúng theo SchoolScorer.cs (FE không có GPA theo khoảng và trọng số riêng từng trường)
function approxGroup(item: RecommendationItem, gpa4: number | null, sat: number | null): number | null {
  const s = item.school;
  const pg = gpa4 != null && s?.avgGpa4 != null ? zToPosition((gpa4 - s.avgGpa4) / 0.3) : null;
  const ps =
    sat != null && s?.satPolicy !== "not_accepted" && s?.sat25 != null && s?.sat75 != null && s.sat75 > s.sat25
      ? clamp(25 + (50 * (sat - s.sat25)) / (s.sat75 - s.sat25), 0, 100)
      : null;
  const a = pg == null ? ps : ps == null ? pg : 0.55 * pg + 0.45 * ps;
  if (a == null) return null;
  const total = a + (item.ecAdjustment ?? 0);
  return total < 25 ? 0 : total > 75 ? 2 : 1;
}

const ACADEMIC: RecommendationItem["category"][] = ["reach", "match", "safety"];
const BUDGET_TOLERANCE = 1.1; // HardFilter: vượt ngân sách × 1,1 mới vào khối học bổng

interface SimValues {
  gpa10?: number;
  sat?: number;
  budget?: number;
}

export function RecommendationPanel() {
  const router = useRouter();
  const { openPanel } = useAssistant();
  const [result, setResult] = useState<RecommendationResult | null>(null);
  const [loadingLatest, setLoadingLatest] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [noProfile, setNoProfile] = useState(false);

  // AHP Priority Presets State
  const [activePreset, setActivePreset] = useState<PriorityPreset>("balanced");

  // Expanded items
  const [expandedId, setExpandedId] = useState<string | null>(null);

  // Hồ sơ dựng từ nháp 6 bước + giá trị đang thử ở hàng "Thử thay đổi hồ sơ" (chỉ tính trên máy)
  const [flowProfile, setFlowProfile] = useState<FlowProfile | null>(null);
  const [sim, setSim] = useState<SimValues>({});

  const handleRun = useCallback(async () => {
    setError(null);
    setNoProfile(false);
    setRunning(true);
    try {
      setResult(await createRecommendation());
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) router.push("/login");
      else if (err instanceof ApiError && err.status === 404) setNoProfile(true);
      else setError(err instanceof Error ? err.message : "Không lọc được trường, hãy thử lại.");
    } finally {
      setRunning(false);
    }
  }, [router]);

  // Chưa lọc lần nào thì tự lọc luôn, khỏi bắt người dùng tìm nút.
  // Vừa gửi hồ sơ 6 bước (?run=1) thì luôn lọc lại theo hồ sơ mới, không hiện kết quả cũ đã lưu.
  const autoRan = useRef(false);
  useEffect(() => {
    async function load() {
      if (new URLSearchParams(window.location.search).get("run") === "1") {
        router.replace("/recommendations"); // bỏ ?run=1 để tải lại trang không lọc lại lần nữa
        setLoadingLatest(false);
        if (!autoRan.current) {
          autoRan.current = true;
          void handleRun();
        }
        return;
      }
      let latest: RecommendationResult | null = null;
      try {
        latest = await getLatestRecommendation();
        setResult(latest);
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) {
          router.push("/login");
          return;
        }
      } finally {
        setLoadingLatest(false);
      }
      if (!latest && !autoRan.current) {
        autoRan.current = true;
        void handleRun();
      }
    }
    load();
  }, [router, handleRun]);

  // Có kết quả thì đọc nháp 6 bước một lần để đánh giá hồ sơ; lỗi thì ẩn khối đánh giá
  const hasResult = !!result;
  useEffect(() => {
    if (!hasResult) return;
    let cancelled = false;
    Promise.all([profileFlowApi.getFlowState(), profileFlowApi.getMetadata()])
      .then(([state, meta]) => {
        if (!cancelled && state?.draft) setFlowProfile(profileFromDraft(state.draft, meta));
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [hasResult]);

  const realStudent = result?.student ?? flowProfile?.student ?? null;
  const realBudget = realStudent ? (realStudent.budgetMaxUsd ?? realStudent.annualBudgetUsd ?? realStudent.budgetMinUsd ?? null) : null;
  const simulating = Object.keys(sim).length > 0;
  const student = useMemo<StudentSummary | null>(() => {
    if (!realStudent || !simulating) return realStudent;
    return {
      ...realStudent,
      // Dịch GPA thang 4 đúng bằng phần chênh thang 10 (× 0,4) để khớp cách quy đổi của hồ sơ thật
      gpa4:
        sim.gpa10 == null
          ? realStudent.gpa4
          : clamp(round2((realStudent.gpa4 ?? 0) + (sim.gpa10 - (flowProfile?.gpa10 ?? sim.gpa10)) * 0.4), 0, 4),
      sat: sim.sat ?? realStudent.sat,
      ...(sim.budget != null && { annualBudgetUsd: sim.budget, budgetMinUsd: null, budgetMaxUsd: sim.budget }),
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

  // Calculate current active weights
  const currentWeights = PRESET_WEIGHTS[activePreset];

  // Chia khối theo "block" backend trả về (backend đã chấm và cắt hạn mức 3/5/4, khối học bổng 5).
  // Đổi nút ưu tiên chỉ sắp lại thứ tự trong khối bằng X = w·[F, H, Q], cùng công thức SchoolPicker; không đổi khối.
  const tieredItems = useMemo(() => {
    const categorized: Record<
      FlowCategory,
      Array<RecommendationItem & { fitScore: number | null; movedFrom?: FlowCategory }>
    > = {
      reach: [],
      match: [],
      safety: [],
      financial_reach: [],
      insufficient_data: [],
    };
    if (!result?.items) return categorized;

    const rankScore = (item: RecommendationItem): number | null => {
      const parts = (
        [
          [currentWeights.cost, item.costFit],
          [currentWeights.scholarship, item.aidFit],
          [currentWeights.quality, item.quality],
        ] as const
      ).filter((part): part is readonly [number, number] => part[1] != null);
      const weightSum = parts.reduce((s, [w]) => s + w, 0);
      if (parts.length === 0 || weightSum === 0) return item.rankScore ?? null;
      return parts.reduce((s, [w, v]) => s + w * v, 0) / weightSum; // tiêu chí thiếu thì chia lại trọng số
    };

    const blockToTier: Record<RecommendationBlock, FlowCategory> = {
      reach: "reach",
      match: "match",
      safety: "safety",
      aid: "financial_reach",
      other: "insufficient_data",
    };

    // Mô phỏng: nhóm học thuật dịch đúng số bậc mà điểm S gần đúng dịch giữa hồ sơ thật và hồ sơ thử,
    // nên không chỉnh gì thì giữ nguyên nhóm backend đã chấm
    const academicChanged = sim.gpa10 != null || sim.sat != null;
    const simulate = (item: RecommendationItem): RecommendationItem => {
      let next = item;
      const baseIdx = ACADEMIC.indexOf(item.category);
      if (academicChanged && student && realStudent && baseIdx >= 0 && (item.school?.acceptanceRate ?? 0) < 0.99) {
        const before = approxGroup(item, realStudent.gpa4, realStudent.sat);
        const after = approxGroup(item, student.gpa4, student.sat);
        if (before != null && after != null && before !== after) {
          next = { ...next, category: ACADEMIC[clamp(baseIdx + after - before, 0, 2)], farReach: false };
        }
      }
      const cost = item.costUnknown ? null : item.totalCostUsd;
      if (sim.budget != null && cost != null) {
        const budget = sim.budget;
        const over = cost > budget * BUDGET_TOLERANCE;
        next = {
          ...next,
          costFit: clamp((budget - cost) / budget, 0, 1),
          overBudgetUsd: cost > budget ? cost - budget : null,
          block: next.block === "other" ? "other" : over ? "aid" : next.block === "aid" ? "match" : next.block,
        };
      }
      return next;
    };
    const tierOf = (item: RecommendationItem): FlowCategory => {
      if (!item.block) return item.category; // kết quả cũ (trước công thức mới) không có block
      if (item.block === "aid" || item.block === "other") return blockToTier[item.block];
      return item.category; // khối chính đi theo nhóm học thuật (đã mô phỏng nếu có)
    };

    for (const original of result.items) {
      const item = simulating ? simulate(original) : original;
      const tier = tierOf(item);
      const before = tierOf(original);
      const x = rankScore(item);
      categorized[tier].push({
        ...item,
        fitScore: x == null ? null : Math.round(x * 100),
        movedFrom: simulating && tier !== before ? before : undefined,
      });
    }

    // 3 khối chính: X giảm dần theo nút đang chọn. Khối học bổng giữ thứ tự backend (vượt ít tiền nhất trước).
    for (const key of ["reach", "match", "safety"] as const) {
      categorized[key].sort((p, q) => (q.fitScore ?? -1) - (p.fitScore ?? -1) || p.rank - q.rank);
    }

    return categorized;
  }, [result, currentWeights, sim, simulating, student, realStudent]);

  const moved = TIERS.flatMap((t) =>
    tieredItems[t.key].filter((i) => i.movedFrom).map((i) => ({ name: i.name, from: i.movedFrom!, to: t.key }))
  );
  const tierName = (k: FlowCategory) => TIERS.find((t) => t.key === k)?.label ?? k;
  const simLabel = [
    sim.gpa10 != null && `Điểm TB ${sim.gpa10}`,
    sim.sat != null && `SAT ${sim.sat}`,
    sim.budget != null && `ngân sách ${sim.budget / 1000}k`,
  ]
    .filter(Boolean)
    .join(", ");

  const firstReach = tieredItems.reach[0];
  const shownCount = TIERS.reduce((n, t) => n + tieredItems[t.key].length, 0);
  useAssistantPageContext({
    label: "Trang đề xuất",
    suggestions: firstReach ? [`Vì sao ${firstReach.name} Khó vào?`, ...RESULT_SUGGESTIONS] : RESULT_SUGGESTIONS,
    greeting: result
      ? `Đây là ${shownCount} trường hợp với hồ sơ của bạn. Bạn có thể hỏi mình, ví dụ:`
      : undefined,
  });

  // Có kết quả thì mở trợ lý bên phải một lần như bản demo (màn hẹp thì khung phủ kín nên không tự mở)
  const autoOpened = useRef(false);
  useEffect(() => {
    if (!result || autoOpened.current) return;
    autoOpened.current = true;
    if (window.matchMedia("(min-width: 900px)").matches) openPanel();
  }, [result, openPanel]);

  const handlePrint = () => {
    if (typeof window !== "undefined") {
      window.print();
    }
  };

  if (loadingLatest) {
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center space-y-3">
        <Spinner />
        <p className="text-sm text-muted-foreground">Đang tải kết quả gợi ý trường gần nhất...</p>
      </div>
    );
  }

  const ecTier = flowProfile?.ecTier ?? null;
  // BE chưa điền activitiesCount (luôn 0) → lấy số hoạt động trong nháp hoặc phần ngoại khóa đã chấm
  const activitiesCount =
    flowProfile?.activitiesCount || student?.activitiesCount || result?.extracurricular?.activities?.length || 0;
  const assessment = student ? assessProfile(student, ecTier, activitiesCount) : null;
  const filterText = realStudent
    ? [
        !result?.studyLevel || result.studyLevel === "undergraduate" ? "Đại học" : result.studyLevel,
        realStudent.major || "Mọi ngành",
        flowProfile?.states.length ? flowProfile.states.join(", ") : "Mọi bang",
        budgetLabel(realStudent) ? `${budgetLabel(realStudent)}/năm` : "Chưa đặt ngân sách",
      ].join(" · ")
    : null;
  const shownGpa10 = sim.gpa10 ?? flowProfile?.gpa10 ?? null;
  const shownSat = sim.sat ?? realStudent?.sat ?? null;
  const shownBudget = sim.budget ?? realBudget;

  return (
    <div className="w-full space-y-4">
      {/* Thẻ đầu trang: hồ sơ đã gửi + đánh giá + ưu tiên */}
      <section className="space-y-5 rounded-xl border border-line bg-surface p-[18px] sm:p-7">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 space-y-1.5">
            <div className="text-[11.5px] font-bold tracking-[0.07em] text-brand uppercase">Hồ sơ đã gửi</div>
            <h1 className="text-[clamp(26px,3vw,32px)] leading-tight font-semibold text-foreground">
              Gợi ý trường cho hồ sơ của bạn
            </h1>
            {filterText && <p className="text-[13.5px] text-muted-foreground">Bộ lọc từ hồ sơ: {filterText}</p>}
          </div>
          <div className="flex flex-wrap gap-2 print:hidden">
            <Link href="/analysis?edit=1" className={buttonVariants({ variant: "outline", className: "h-[42px] px-4" })}>
              Sửa hồ sơ
            </Link>
            {result && (
              <Button type="button" onClick={handlePrint} className="h-[42px] px-4">
                Tải báo cáo PDF
              </Button>
            )}
          </div>
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

        {result && (
          <>
            <hr className="border-line print:hidden" />
            <div className="space-y-2 print:hidden">
              <div className="text-[15px] font-semibold text-foreground">Bạn ưu tiên điều gì?</div>
              <div className="flex flex-wrap items-center gap-2">
                {PRESET_CHIPS.map(([key, label]) => (
                  <button
                    key={key}
                    type="button"
                    aria-pressed={activePreset === key}
                    onClick={() => setActivePreset(key)}
                    className={cn(
                      "min-h-[38px] cursor-pointer rounded-full border px-[15px] text-sm font-medium transition-colors",
                      activePreset === key
                        ? "border-foreground bg-foreground text-surface"
                        : "border-line bg-surface text-foreground hover:border-brand"
                    )}
                  >
                    {label}
                  </button>
                ))}
              </div>
              <p className="text-[12.5px] text-muted-foreground">
                Danh sách sắp lại ngay. Trường không đổi nhóm, chỉ đổi thứ tự trong từng nhóm.
              </p>
            </div>
          </>
        )}

        {result && realStudent && (
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
            <p>
              Hồ sơ thật chưa đổi.{" "}
              {moved.length > 0
                ? moved.map((m) => `${m.name} ${tierName(m.from)} → ${tierName(m.to)}`).join("; ")
                : "Không trường nào đổi nhóm."}
            </p>
            <p className="text-[12px] opacity-80">
              Chia nhóm lại là ước lượng gần đúng tính trên máy bạn, không gửi lên AI.
            </p>
          </div>
          <Button type="button" variant="outline" onClick={() => setSim({})} className="h-9 px-3.5">
            Thoát mô phỏng
          </Button>
        </div>
      )}

      {running && (
        <Alert className="border-brand/30 bg-brand-wash/30">
          <SparklesIcon className="size-4 text-brand" />
          <AlertTitle className="text-sm font-semibold text-brand">Đang phân tích và gợi ý trường...</AlertTitle>
          <AlertDescription className="text-xs text-muted-foreground">
            Hệ thống đang chạy AHP & SAW và AI đang viết nhận xét cho từng trường. Vui lòng không đóng trang.
          </AlertDescription>
        </Alert>
      )}

      {noProfile && (
        <Alert className="border-warn/40 bg-warn-wash/40">
          <TriangleAlertIcon className="size-4 text-warn" />
          <AlertTitle className="text-sm font-semibold text-warn">Chưa có hồ sơ học sinh</AlertTitle>
          <AlertDescription className="text-xs text-muted-foreground">
            Bạn chưa hoàn thiện hồ sơ. Hãy{" "}
            <Link href="/analysis" className="font-semibold text-brand underline">
              điền form phân tích 6 bước
            </Link>{" "}
            để nhận gợi ý trường.
          </AlertDescription>
        </Alert>
      )}

      {error && (
        <Alert variant="destructive">
          <AlertCircleIcon className="size-4" />
          <AlertTitle>Lỗi phân tích</AlertTitle>
          <AlertDescription className="text-xs">{error}</AlertDescription>
        </Alert>
      )}

      {!result && !running && !noProfile && (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-line px-6 py-10 text-center">
          <SparklesIcon className="size-6 text-brand" />
          <div className="font-semibold">Chưa có kết quả gợi ý</div>
          <p className="max-w-md text-xs text-muted-foreground">
            Bấm nút dưới đây để lọc trường theo hồ sơ hiện tại. Mất khoảng 1 phút vì AI viết nhận xét cho từng
            trường.
          </p>
          <Button type="button" onClick={handleRun} className="mt-1 gap-2">
            <RefreshCwIcon className="size-4" />
            Lọc trường theo hồ sơ
          </Button>
        </div>
      )}

      {result &&
        result.warnings.map((w) => (
          <p key={w} className="text-[13.5px] text-muted-foreground">
            {w}
          </p>
        ))}

      {/* Thẻ trường theo từng nhóm */}
      {result &&
        TIERS.map((tier) => {
          const items = tieredItems[tier.key] ?? [];
          if (items.length === 0) return null;
          return (
            <section key={tier.key} className="space-y-2.5">
              <div className="flex flex-wrap items-center gap-2.5 pt-1.5">
                <span
                  className={cn(
                    "inline-flex h-[26px] items-center rounded-[7px] px-2.5 text-[12.5px] font-bold whitespace-nowrap",
                    TIER_TONE[tier.key]
                  )}
                >
                  {tier.label}
                </span>
                <span className="text-[13.5px] text-muted-foreground">{tier.hint}</span>
              </div>

              {items.map((item, idx) => {
                const isExpanded = expandedId === item.offeringId;
                return (
                  <article
                    key={item.offeringId}
                    className="flex items-start gap-3.5 rounded-xl border border-line bg-surface px-[18px] py-4"
                  >
                    <span className="text-numeric inline-flex size-8 shrink-0 items-center justify-center rounded-lg bg-ground font-bold">
                      {idx + 1}
                    </span>
                    <div className="min-w-0 flex-1 space-y-2.5">
                      <div>
                        <b className="text-[16.5px] font-semibold text-foreground">{item.name}</b>
                        {item.movedFrom && (
                          <span className="ml-2 inline-flex h-[22px] items-center rounded-md bg-warn-wash px-2 align-middle text-[11.5px] font-semibold text-warn">
                            Mới vào nhóm · trước: {tierName(item.movedFrom)}
                          </span>
                        )}
                        <div className="text-[13.5px] text-muted-foreground">
                          {[item.school?.city, item.state].filter(Boolean).join(", ") || "Mỹ"} ·{" "}
                          {item.school?.control === "public"
                            ? "Công lập"
                            : item.school?.control === "private"
                              ? "Tư thục"
                              : "Đại học"}
                        </div>
                      </div>

                      <div className="grid grid-cols-[repeat(auto-fit,minmax(110px,1fr))] gap-2.5">
                        <Stat label="Chi phí / năm">
                          {item.costUnknown || item.totalCostUsd == null ? "Chưa công bố" : usd(item.totalCostUsd)}
                          {item.overBudgetUsd != null && (
                            <span className="block text-[11.5px] font-normal text-risk">
                              vượt {usd(item.overBudgetUsd)}
                            </span>
                          )}
                        </Stat>
                        <Stat label="Học bổng">{aidLabel(item.aidFit)}</Stat>
                        <Stat label="Tỉ lệ nhận">
                          {item.school?.acceptanceRate != null
                            ? `${Math.round(item.school.acceptanceRate * 100)}%`
                            : "—"}
                        </Stat>
                        <Stat label="Độ phù hợp">{fitLabel(item.fitScore)}</Stat>
                        {tier.key === "financial_reach" && <Stat label="Khả năng đỗ">{academicLabel(item)}</Stat>}
                      </div>

                      {isExpanded && (
                        <div className="space-y-3">
                          {(item.categoryReason || item.reason) && (
                            <p className="text-[13.5px] text-foreground">{item.categoryReason || item.reason}</p>
                          )}
                          <ReasonBullets item={item} limit={3} />
                          <div className="rounded-lg bg-ground/60 p-4">
                            <SchoolDetailsExtended item={item} resultId={result.id} />
                          </div>
                        </div>
                      )}

                      <div className="flex flex-wrap justify-end gap-4 text-[13.5px] print:hidden">
                        <button
                          type="button"
                          aria-expanded={isExpanded}
                          onClick={() => setExpandedId(isExpanded ? null : item.offeringId)}
                          className="cursor-pointer px-0.5 py-1 font-medium text-brand hover:text-brand-ink"
                        >
                          {isExpanded ? "Ẩn chi tiết" : "Chi tiết"}
                        </button>
                        <button
                          type="button"
                          onClick={() => openPanel(`Vì sao ${item.name} xếp nhóm ${tier.label}?`)}
                          className="cursor-pointer px-0.5 py-1 font-medium text-brand hover:text-brand-ink"
                        >
                          Hỏi về trường này
                        </button>
                      </div>
                    </div>
                  </article>
                );
              })}
            </section>
          );
        })}
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

function Stat({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col">
      <span className="text-xs text-muted-foreground">{label}</span>
      <b className="text-numeric text-sm font-semibold text-foreground">{children}</b>
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

function ReasonBullets({ item, limit }: { item: RecommendationItem; limit: number }) {
  const strengths = item.strengths?.slice(0, limit) ?? [];
  const weaknesses = item.weaknesses?.slice(0, limit) ?? [];
  if (strengths.length === 0 && weaknesses.length === 0) return null;
  return (
    <ul className="mt-1.5 space-y-0.5 text-xs">
      {strengths.map((st) => (
        <li key={`s-${st}`} className="flex items-start gap-1.5 text-foreground/80">
          <CheckIcon className="size-3.5 text-ok shrink-0 mt-0.5" aria-hidden="true" />
          <span>{st}</span>
        </li>
      ))}
      {weaknesses.map((w) => (
        <li key={`w-${w}`} className="flex items-start gap-1.5 text-foreground/80">
          <XIcon className="size-3.5 text-risk shrink-0 mt-0.5" aria-hidden="true" />
          <span>{w}</span>
        </li>
      ))}
    </ul>
  );
}

// Lời tư vấn AI đã tải, theo lần gợi ý + trường: đóng/mở lại "Chi tiết" không gọi AI lần nữa
const adviceCache = new Map<string, SchoolAdvice>();

// AI tư vấn 1 trường: tải khi mở "Chi tiết" (vài giây), lỗi thì cho thử lại
function SchoolAdviceBox({ offeringId, resultId }: { offeringId: string; resultId: string }) {
  const key = `${resultId}:${offeringId}`;
  const [advice, setAdvice] = useState<SchoolAdvice | null>(() => adviceCache.get(key) ?? null);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (adviceCache.has(key)) return;
    let cancelled = false;
    getSchoolAdvice(offeringId)
      .then((a) => {
        adviceCache.set(key, a);
        if (!cancelled) setAdvice(a);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(
          err instanceof ApiError && err.status === 404
            ? "Kết quả gợi ý đã thay đổi, hãy tải lại trang."
            : "AI tư vấn tạm thời chưa phản hồi.",
        );
      });
    return () => {
      cancelled = true;
    };
  }, [key, offeringId, attempt]);

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-line bg-surface p-3 text-muted-foreground">
        <AlertCircleIcon className="size-4 shrink-0" aria-hidden="true" />
        <span>{error}</span>
        <Button variant="ghost" size="sm" className="ml-auto h-7 text-xs" onClick={() => {
            setError(null);
            setAttempt((n) => n + 1);
          }}>
          Thử lại
        </Button>
      </div>
    );
  }

  if (!advice) {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-brand/20 bg-brand-wash/30 p-3 text-brand">
        <Spinner className="size-4" />
        <span>AI đang phân tích hồ sơ của bạn với trường này…</span>
      </div>
    );
  }

  return (
    <Alert className="border-brand/20 bg-brand-wash/30">
      <SparklesIcon className="size-4 text-brand" />
      <AlertTitle className="text-brand font-semibold text-xs">Tư vấn của AI</AlertTitle>
      <AlertDescription className="text-xs text-foreground/90 mt-1 space-y-2 leading-relaxed">
        <p>{advice.summary}</p>
        {advice.strengths.length > 0 && (
          <div>
            <div className="font-semibold text-ok">Điểm mạnh</div>
            <ul className="mt-0.5 space-y-0.5">
              {advice.strengths.map((st, i) => (
                <li key={i} className="flex items-start gap-1.5">
                  <CheckIcon className="size-3.5 text-ok shrink-0 mt-0.5" aria-hidden="true" />
                  <span>{st}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
        {advice.improvements.length > 0 && (
          <div>
            <div className="font-semibold text-brand">Nên làm gì</div>
            <ul className="mt-0.5 space-y-0.5">
              {advice.improvements.map((im, i) => (
                <li key={i} className="flex items-start gap-1.5">
                  <ChevronUpIcon className="size-3.5 text-brand shrink-0 mt-0.5" aria-hidden="true" />
                  <span>{im}</span>
                </li>
              ))}
            </ul>
          </div>
        )}
        <p className="text-[10px] text-muted-foreground">
          AI ({advice.model}) viết từ số liệu hệ thống đã chấm, chỉ mang tính tham khảo.
        </p>
      </AlertDescription>
    </Alert>
  );
}

// Chi tiết trường mở rộng
function SchoolDetailsExtended({ item, resultId }: { item: RecommendationItem; resultId: string }) {
  const s = item.school;
  const na = "Chưa công bố";
  const english = [
    s?.minIelts != null && `IELTS ${s.minIelts}`,
    s?.minToefl != null && `TOEFL ${s.minToefl}`,
    s?.minDuolingo != null && `Duolingo ${s.minDuolingo}`,
  ].filter(Boolean);

  const stats: [string, string][] = [
    ["GPA TB sinh viên", s?.avgGpa4 != null ? `${s.avgGpa4.toFixed(2)}/4.0` : na],
    [
      "SAT 25% – 75%",
      s?.sat25 != null && s?.sat75 != null
        ? `${s.sat25} – ${s.sat75}${s.satPolicy ? ` (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : ""}`
        : s?.satPolicy ? `${na} (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : na,
    ],
    ["Tiếng Anh tối thiểu", english.length > 0 ? english.join(" · ") : na],
    ["Học phí hàng năm", usd(s?.tuitionUsd) ?? na],
    ["Sinh hoạt phí", usd(s?.livingUsd) ?? na],
    ["Phí khác", usd(s?.feesUsd) ?? na],
    ["Tỷ lệ trúng tuyển", s?.acceptanceRate != null ? `${Math.round(s.acceptanceRate * 100)}%` : na],
    ["Sinh viên quốc tế", s?.internationalStudents != null ? s.internationalStudents.toLocaleString("vi-VN") : na],
  ];

  return (
    <div className="space-y-4 text-xs">
      <SchoolAdviceBox offeringId={item.offeringId} resultId={resultId} />

      {/* Câu so sánh soạn sẵn từ số liệu (luôn có, kể cả khi AI chưa phản hồi) */}
      {item.reason && (
        <Alert className="border-brand/20 bg-brand-wash/30">
          <SparklesIcon className="size-4 text-brand" />
          <AlertTitle className="text-brand font-semibold text-xs">
            {item.aiExplained ? "Nhận xét của AI" : "Tóm tắt so sánh"}
          </AlertTitle>
          <AlertDescription className="text-xs text-foreground/90 mt-1 leading-relaxed">
            {item.reason}
          </AlertDescription>
        </Alert>
      )}

      {/* Strengths & Weaknesses */}
      {!!(item.strengths?.length || item.weaknesses?.length) && (
        <div className="grid gap-3 sm:grid-cols-2 pt-1">
          {!!item.strengths?.length && (
            <div className="rounded-lg bg-ok-wash/30 border border-ok/20 p-3 space-y-1">
              <div className="text-[11px] font-bold text-ok uppercase tracking-wider">Ưu điểm</div>
              <ul className="space-y-1 text-xs">
                {item.strengths.map((st, i) => (
                  <li key={i} className="flex items-start gap-1.5">
                    <CheckIcon className="size-3.5 text-ok shrink-0 mt-0.5" />
                    <span>{st}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {!!item.weaknesses?.length && (
            <div className="rounded-lg bg-risk-wash/30 border border-risk/20 p-3 space-y-1">
              <div className="text-[11px] font-bold text-risk uppercase tracking-wider">Điểm cần lưu ý</div>
              <ul className="space-y-1 text-xs">
                {item.weaknesses.map((w, i) => (
                  <li key={i} className="flex items-start gap-1.5">
                    <XIcon className="size-3.5 text-risk shrink-0 mt-0.5" />
                    <span>{w}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Stats Grid */}
      <dl className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1 border-t border-line">
        {stats.map(([k, v]) => (
          <div key={k} className="rounded-lg bg-surface border border-line p-2">
            <dt className="text-muted-foreground text-[10px]">{k}</dt>
            <dd className={v === na ? "text-muted-foreground text-xs" : "font-semibold text-xs text-foreground"}>
              {v}
            </dd>
          </div>
        ))}
      </dl>

      {/* School Website Link */}
      {s?.website && (
        <div className="pt-1">
          <a
            href={s.website.startsWith("http") ? s.website : `https://${s.website}`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-xs text-brand hover:underline font-semibold"
          >
            Trang web trường: {s.website}
            <ExternalLinkIcon className="size-3.5" />
          </a>
        </div>
      )}
    </div>
  );
}
