"use client";

// Gợi ý trường: nút "Lọc trường theo hồ sơ" và bảng trường chia 3 nhóm Thử sức / Vừa sức / An toàn

import { Fragment, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api";
import { createRecommendation, getLatestRecommendation } from "@/lib/recommendation-api";
import type { Category, RecommendationItem, RecommendationResult, StudentSummary } from "@/types/recommendation";

const GROUPS: { category: Category; label: string; hint: string; color: string }[] = [
  { category: "reach", label: "Thử sức", hint: "Hồ sơ thấp hơn mặt bằng trúng tuyển", color: "border-orange-300 bg-orange-50 text-orange-800" },
  { category: "match", label: "Vừa sức", hint: "Hồ sơ ngang mặt bằng trúng tuyển", color: "border-blue-300 bg-blue-50 text-blue-800" },
  { category: "safety", label: "An toàn", hint: "Hồ sơ cao hơn mặt bằng trúng tuyển", color: "border-green-300 bg-green-50 text-green-800" },
  { category: "insufficient_data", label: "Chưa đủ dữ liệu", hint: "Trường chưa công bố đủ GPA/SAT để xếp nhóm", color: "border-slate-300 bg-slate-50 text-slate-700" },
];

const SAT_POLICY: Record<string, string> = { required: "bắt buộc", optional: "không bắt buộc", not_accepted: "không xét" };

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 });
const usd = (v: number | null | undefined) => (v == null ? null : money.format(v));

export function RecommendationPanel() {
  const router = useRouter();
  const [result, setResult] = useState<RecommendationResult | null>(null);
  const [loadingLatest, setLoadingLatest] = useState(true);
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [noProfile, setNoProfile] = useState(false);

  useEffect(() => {
    async function load() {
      try {
        setResult(await getLatestRecommendation());
      } catch (err) {
        if (err instanceof ApiError && err.status === 401) router.push("/login");
        // lỗi khác: vẫn cho bấm nút lọc
      } finally {
        setLoadingLatest(false);
      }
    }
    load();
  }, [router]);

  async function handleRun() {
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
  }

  const ecScore = result?.student?.extracurricularScore ?? result?.extracurricular?.score ?? null;

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-2 text-sm text-slate-600">
          {result ? (
            <>
              <div>
                Kết quả lúc <span className="font-medium text-slate-800">{new Date(result.createdAt).toLocaleString("vi-VN")}</span>
              </div>
              <ProfileSummary student={result.student} ecScore={ecScore} />
            </>
          ) : loadingLatest ? (
            "Đang tải kết quả lần trước…"
          ) : (
            "Chưa có kết quả. Bấm nút để lọc trường theo hồ sơ của bạn."
          )}
        </div>
        <button
          type="button"
          onClick={handleRun}
          disabled={running}
          className="inline-flex shrink-0 items-center justify-center gap-2 rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-blue-300"
        >
          {running && <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/40 border-t-white" />}
          {running ? "Đang lọc trường…" : result ? "Lọc lại theo hồ sơ" : "Lọc trường theo hồ sơ"}
        </button>
      </div>

      {running && (
        <p className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
          Hệ thống đang chấm điểm và AI đang viết nhận xét cho từng trường, có thể mất vài phút. Vui lòng không đóng trang.
        </p>
      )}

      {noProfile && (
        <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          Bạn chưa có hồ sơ học sinh.{" "}
          <Link href="/profile/academic" className="font-semibold underline">
            Tạo hồ sơ học thuật
          </Link>{" "}
          trước khi lọc trường.
        </p>
      )}

      {error && <p className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</p>}

      {result && result.warnings.length > 0 && (
        <ul className="space-y-1 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          {result.warnings.map((w) => (
            <li key={w}>• {w}</li>
          ))}
        </ul>
      )}

      {result &&
        GROUPS.map((g) => {
          const items = result.items.filter((i) => i.category === g.category);
          return items.length > 0 ? <SchoolGroup key={g.category} group={g} items={items} /> : null;
        })}
    </div>
  );
}

//Hồ sơ dùng cho lần lọc: Học thuật (GPA, SAT, tiếng Anh) · Ngân sách · Ngoại khóa
function ProfileSummary({ student, ecScore }: { student?: StudentSummary | null; ecScore: number | null }) {
  const academic = student
    ? [
        student.gpa4 != null && `GPA ${student.gpa4.toFixed(2)}/4`,
        student.sat != null && `SAT ${student.sat}`,
        student.ielts != null && `IELTS ${student.ielts}`,
        student.toefl != null && `TOEFL ${student.toefl}`,
        student.duolingo != null && `Duolingo ${student.duolingo}`,
      ].filter(Boolean)
    : [];

  return (
    <div className="flex flex-wrap gap-2">
      {student && (
        <Chip label="Học thuật" value={academic.length > 0 ? academic.join(" · ") : "chưa có điểm"} />
      )}
      {student?.annualBudgetUsd != null && <Chip label="Ngân sách" value={`${usd(student.annualBudgetUsd)}/năm`} />}
      {ecScore != null && <Chip label="Ngoại khóa" value={`${ecScore}/4`} />}
      {student?.major && <Chip label="Ngành" value={student.major} />}
    </div>
  );
}

function Chip({ label, value }: { label: string; value: string }) {
  return (
    <span className="rounded-md border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs text-slate-600">
      {label}: <span className="font-semibold text-slate-800">{value}</span>
    </span>
  );
}

function SchoolGroup({ group, items }: { group: (typeof GROUPS)[number]; items: RecommendationItem[] }) {
  const [open, setOpen] = useState<string | null>(null);

  return (
    <section className="overflow-hidden rounded-xl border border-slate-200 bg-white">
      <div className={`flex items-baseline gap-3 border-b px-5 py-3 ${group.color}`}>
        <h2 className="text-base font-bold">{group.label}</h2>
        <span className="text-xs">
          {group.hint} · {items.length} trường
        </span>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-2">#</th>
              <th className="px-4 py-2">Trường</th>
              <th className="px-4 py-2 whitespace-nowrap">Chi phí/năm</th>
              <th className="px-4 py-2">Lý do</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {items.map((i) => {
              const expanded = open === i.offeringId;
              return (
                <Fragment key={i.offeringId}>
                  <tr className="align-top">
                    <td className="px-4 py-3 text-slate-500">{i.rank}</td>
                    <td className="px-4 py-3">
                      <div className="font-medium text-slate-900">{i.name}</div>
                      <div className="text-xs text-slate-500">{[i.school?.city, i.state].filter(Boolean).join(", ") || "–"}</div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-slate-700">
                      {i.costUnknown || i.totalCostUsd == null ? <span className="text-slate-400">Chưa có dữ liệu</span> : usd(i.totalCostUsd)}
                    </td>
                    <td className="px-4 py-3 text-slate-700">
                      <Reason item={i} />
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        onClick={() => setOpen(expanded ? null : i.offeringId)}
                        className="whitespace-nowrap text-xs font-semibold text-blue-600 hover:underline"
                      >
                        {expanded ? "Thu gọn ▴" : "Chi tiết ▾"}
                      </button>
                    </td>
                  </tr>
                  {expanded && (
                    <tr className="bg-slate-50/70">
                      <td />
                      <td colSpan={4} className="px-4 pb-4 pt-1">
                        <SchoolDetails item={i} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

//Lý do: vì sao thuộc nhóm + ưu điểm + nhược điểm. Kết quả cũ chưa có 3 phần này thì hiện câu giải thích cũ
function Reason({ item }: { item: RecommendationItem }) {
  if (!item.categoryReason) return <>{item.reason}</>;

  return (
    <div className="space-y-2">
      <p className="font-medium text-slate-800">{item.categoryReason}</p>
      {!!item.strengths?.length && (
        <div>
          <div className="text-xs font-semibold uppercase text-green-700">Ưu điểm</div>
          <ul className="mt-0.5 space-y-0.5">
            {item.strengths.map((s) => (
              <li key={s} className="flex gap-1.5">
                <span className="text-green-600">✓</span>
                {s}
              </li>
            ))}
          </ul>
        </div>
      )}
      {!!item.weaknesses?.length && (
        <div>
          <div className="text-xs font-semibold uppercase text-red-600">Nhược điểm</div>
          <ul className="mt-0.5 space-y-0.5">
            {item.weaknesses.map((w) => (
              <li key={w} className="flex gap-1.5">
                <span className="text-red-500">✗</span>
                {w}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

//Thông tin chi tiết của trường; số liệu trường chưa công bố thì ghi "Chưa công bố"
function SchoolDetails({ item }: { item: RecommendationItem }) {
  const s = item.school;
  const na = "Chưa công bố";
  const english = [
    s?.minIelts != null && `IELTS ${s.minIelts}`,
    s?.minToefl != null && `TOEFL ${s.minToefl}`,
    s?.minDuolingo != null && `Duolingo ${s.minDuolingo}`,
  ].filter(Boolean);

  const rows: [string, string][] = [
    ["GPA trung bình trúng tuyển", s?.avgGpa4 != null ? `${s.avgGpa4.toFixed(2)}/4` : na],
    [
      "SAT 25–75%",
      s?.sat25 != null && s?.sat75 != null
        ? `${s.sat25}–${s.sat75}${s.satPolicy ? ` (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : ""}`
        : s?.satPolicy ? `${na} (${SAT_POLICY[s.satPolicy] ?? s.satPolicy})` : na,
    ],
    ["Tiếng Anh tối thiểu", english.length > 0 ? english.join(" · ") : na],
    ["Học phí", usd(s?.tuitionUsd) ?? na],
    ["Sinh hoạt phí", usd(s?.livingUsd) ?? na],
    ["Phí khác", usd(s?.feesUsd) ?? na],
    ["Loại trường", s?.control === "public" ? "Công lập" : s?.control === "private" ? "Tư thục" : na],
    ["Tỷ lệ nhận", s?.acceptanceRate != null ? `${Math.round(s.acceptanceRate * 100)}%` : na],
    ["Sinh viên quốc tế", s?.internationalStudents != null ? s.internationalStudents.toLocaleString("vi-VN") : na],
  ];

  return (
    <div className="space-y-3">
      <dl className="grid grid-cols-1 gap-x-6 gap-y-1.5 text-xs sm:grid-cols-2 lg:grid-cols-3">
        {rows.map(([k, v]) => (
          <div key={k} className="flex justify-between gap-3 border-b border-slate-200/70 py-1">
            <dt className="text-slate-500">{k}</dt>
            <dd className={v === na ? "text-slate-400" : "font-medium text-slate-800"}>{v}</dd>
          </div>
        ))}
      </dl>
      {s?.website && (
        <a href={websiteUrl(s.website)} target="_blank" rel="noopener noreferrer" className="inline-block text-xs font-semibold text-blue-600 hover:underline">
          Website của trường ↗
        </a>
      )}
      {item.aiExplained && item.categoryReason && (
        <div className="rounded-lg border border-violet-200 bg-violet-50 p-3 text-xs text-violet-900">
          <span className="font-semibold">Nhận xét của AI: </span>
          {item.reason}
        </div>
      )}
    </div>
  );
}

// Dữ liệu website có khi thiếu "https://"
function websiteUrl(site: string) {
  return /^https?:\/\//i.test(site) ? site : `https://${site}`;
}
