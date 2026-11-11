"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { RefreshCwIcon } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { useAssistant } from "@/components/assistant/AssistantProvider";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { draftGpa } from "@/lib/profile-flow-options";
import { formatGpa, formatScore, formatUsd } from "@/lib/recommendation-labels";
import type {
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

const usd = formatUsd; // "20.000 USD", cùng định dạng vi-VN với thẻ trường và mô phỏng

// Mức ngân sách: khoảng min–max, "Dưới max" hoặc "Trên min" (mức cao nhất không có trần)
function budgetLabel(s: StudentProfile): string | null {
  const min = s.budgetMinUsd && s.budgetMinUsd > 0 ? s.budgetMinUsd : null;
  const max = s.budgetMaxUsd ?? null;
  if (min && max) return `${usd(min).replace(" USD", "")} – ${usd(max)}`;
  if (max) return `Dưới ${usd(max)}`;
  if (min) return `Trên ${usd(min)}`;
  return null;
}

interface ProfileFact {
  label: string;
  value: string;
}

// Các dữ kiện học sinh đã nhập, ngắn gọn để xếp thành một hàng nhãn nhỏ. Không phán xét mạnh hay yếu:
// việc đánh giá do trợ lý làm (một nguồn duy nhất)
function profileFacts(s: StudentProfile, activitiesCount: number): ProfileFact[] {
  const english = s.ielts ? `IELTS ${formatScore(s.ielts)}` : s.toefl ? `TOEFL ${formatScore(s.toefl)}` : s.duolingo ? `Duolingo ${formatScore(s.duolingo)}` : null;
  const budget = budgetLabel(s);
  return [
    { label: "GPA", value: s.gpa4 != null ? `${formatGpa(s.gpa4)}/4` : "chưa có" },
    { label: "SAT", value: s.sat != null ? String(s.sat) : "chưa thi" },
    { label: "Tiếng Anh", value: english ?? "chưa có" },
    { label: "Ngân sách", value: budget ? `${budget.replace(/ – /g, "–")}/năm` : "chưa đặt" },
    { label: "Ngoại khóa", value: activitiesCount > 0 ? `${activitiesCount} hoạt động` : "chưa khai" },
  ];
}

interface FlowProfile {
  student: StudentProfile;
  gpa10: number | null;
  states: string[];
  activitiesCount: number;
  /** tên tiếng Việt của ngành trong bảng majors (hồ sơ lưu tên Anh) */
  majorLabel: string | null;
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
    activitiesCount: activities.filter((a) => a.category === "activity").length,
    majorLabel:
      meta.majors.find((m) => m.value.toLowerCase() === draft.step1?.targetMajor?.toLowerCase())?.label ?? null,
  };
}

interface ProfileSummaryProps {
  rec: Recommendation | null;
  onRerun?: () => void; // chạy lại gợi ý từ hồ sơ hiện tại
}

// Thẻ đầu trang gợi ý: bộ lọc từ hồ sơ và khối đánh giá hồ sơ.
// Việc thử đổi GPA, SAT, ngân sách... nằm ở panel "Mô phỏng hồ sơ" (ProfileSimulator).
export function ProfileSummary({ rec, onRerun }: ProfileSummaryProps) {
  const { openPanel } = useAssistant();
  const [flowProfile, setFlowProfile] = useState<FlowProfile | null>(null);

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
  const student = realStudent;

  const facts = student ? profileFacts(student, flowProfile?.activitiesCount ?? 0) : null;
  const majorValue = rec?.criteria.major || realStudent?.major || "";
  const major = (majorValue && flowProfile?.majorLabel) || majorValue || "Mọi ngành";
  const states = flowProfile?.states.length ? flowProfile.states : (rec?.criteria.states ?? []);
  const filterText = [
    !rec?.studyLevel || rec.studyLevel === "undergraduate" ? "Đại học" : rec.studyLevel,
    major,
    states.length ? states.join(", ") : "Mọi bang",
  ].join(" · ");
  if (!rec && !flowProfile) return null;

  const small = buttonVariants({ variant: "outline", size: "sm", className: "h-8 gap-1.5 px-3 text-xs" });

  return (
    <section className="space-y-2.5 rounded-xl border border-line bg-surface px-4 py-3 sm:px-5">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5">
          <span className="text-xs font-bold text-brand">Hồ sơ của bạn</span>
          <span className="text-[13px] text-muted-foreground">
            {filterText}
            {majorValue && (
              <span
                className="ml-1 cursor-help text-ink-3"
                title="Gợi ý theo nhóm ngành: trường có đào tạo nhóm ngành bạn chọn, chưa chắc có đúng chuyên ngành hẹp."
              >
                ⓘ
              </span>
            )}
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <button
            type="button"
            // Cùng câu mặc định advisor dùng cho "đánh giá hồ sơ" (branches.DEFAULT_QUESTION): kết quả hiện trong chat
            onClick={() => openPanel("Đánh giá hồ sơ của em giúp em.")}
            className="text-xs font-semibold text-brand underline-offset-4 hover:underline"
          >
            Đánh giá hồ sơ
          </button>
          <Link href="/analysis?edit=1" className={small}>
            Sửa hồ sơ
          </Link>
          {onRerun && (
            <button
              type="button"
              onClick={onRerun}
              title="Tính lại danh sách theo hồ sơ hiện tại. Dùng sau khi bạn sửa hồ sơ."
              className={small}
            >
              <RefreshCwIcon className="size-3.5" />
              Gợi ý lại
            </button>
          )}
        </div>
      </div>

      {facts && (
        <div className="flex flex-wrap gap-2">
          {facts.map((fact) => (
            <span key={fact.label} className="inline-flex items-baseline gap-1.5 rounded-full bg-ground px-3 py-1 text-[13px]">
              <span className="text-xs text-muted-foreground">{fact.label}</span>
              <b className="font-semibold text-foreground">{fact.value}</b>
            </span>
          ))}
        </div>
      )}
    </section>
  );
}
