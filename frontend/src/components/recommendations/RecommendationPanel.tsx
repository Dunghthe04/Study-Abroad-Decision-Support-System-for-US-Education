"use client";

// Gợi ý trường: nút "Lọc trường theo hồ sơ" và bảng trường chia 3 nhóm Thách thức / Mục tiêu / Vừa sức

import { Fragment, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api";
import { createRecommendation, getLatestRecommendation } from "@/lib/recommendation-api";
import type { Category, RecommendationItem, RecommendationResult, StudentSummary } from "@/types/recommendation";
import {
  AlertCircleIcon,
  CheckIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  ExternalLinkIcon,
  InfoIcon,
  SparklesIcon,
  TriangleAlertIcon,
  XIcon,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

const GROUPS: { category: Category; label: string; hint: string; badge: "ok" | "brand" | "warn" | "neutral" }[] = [
  { category: "reach", label: "Thách thức", hint: "Hồ sơ thấp hơn mặt bằng trúng tuyển", badge: "warn" },
  { category: "match", label: "Mục tiêu", hint: "Hồ sơ ngang mặt bằng trúng tuyển", badge: "brand" },
  { category: "safety", label: "Vừa sức", hint: "Hồ sơ cao hơn mặt bằng trúng tuyển", badge: "ok" },
  { category: "insufficient_data", label: "Chưa đủ dữ liệu", hint: "Trường chưa công bố đủ GPA/SAT để xếp nhóm", badge: "neutral" },
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
      <Card>
        <CardContent className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="space-y-2 text-sm text-muted-foreground">
            {result ? (
              <>
                <div>
                  Kết quả lúc <span className="font-medium text-foreground">{new Date(result.createdAt).toLocaleString("vi-VN")}</span>
                </div>
                <ProfileSummary student={result.student} ecScore={ecScore} />
              </>
            ) : loadingLatest ? (
              "Đang tải kết quả lần trước…"
            ) : (
              "Chưa có kết quả. Bấm nút để lọc trường theo hồ sơ của bạn."
            )}
          </div>
          <Button type="button" onClick={handleRun} disabled={running} size="lg">
            {running && <Spinner />}
            {running ? "Đang lọc trường…" : result ? "Lọc lại theo hồ sơ" : "Lọc trường theo hồ sơ"}
          </Button>
        </CardContent>
      </Card>

      {running && (
        <Alert variant="info">
          <InfoIcon />
          <AlertDescription>
            Hệ thống đang chấm điểm và AI đang viết nhận xét cho từng trường, có thể mất vài phút. Vui lòng không đóng trang.
          </AlertDescription>
        </Alert>
      )}

      {noProfile && (
        <Alert variant="warning">
          <TriangleAlertIcon />
          <AlertDescription>
            <p>
              Bạn chưa có hồ sơ học sinh.{" "}
              <Link href="/profile/academic" className="underline">
                Tạo hồ sơ học thuật
              </Link>{" "}
              trước khi lọc trường.
            </p>
          </AlertDescription>
        </Alert>
      )}

      {error && (
        <Alert variant="destructive">
          <AlertCircleIcon />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {result && result.warnings.length > 0 && (
        <Alert variant="warning">
          <TriangleAlertIcon />
          <AlertDescription>
            <ul className="space-y-1">
              {result.warnings.map((w) => (
                <li key={w}>• {w}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
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
    <Badge variant="outline" className="h-auto whitespace-normal">
      {label}: <span className="font-semibold">{value}</span>
    </Badge>
  );
}

function SchoolGroup({ group, items }: { group: (typeof GROUPS)[number]; items: RecommendationItem[] }) {
  const [open, setOpen] = useState<string | null>(null);

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>{group.label}</h2>
        </CardTitle>
        <CardDescription>{group.hint}</CardDescription>
        <CardAction>
          <Badge variant={group.badge}>{items.length} trường</Badge>
        </CardAction>
      </CardHeader>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>#</TableHead>
            <TableHead>Trường</TableHead>
            <TableHead>Chi phí/năm</TableHead>
            <TableHead>Lý do</TableHead>
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((i) => {
            const expanded = open === i.offeringId;
            return (
              <Fragment key={i.offeringId}>
                <TableRow>
                  <TableCell className="align-top">{i.rank}</TableCell>
                  <TableCell className="align-top whitespace-normal">
                    <div className="font-medium">{i.name}</div>
                    <div className="text-body-s">{[i.school?.city, i.state].filter(Boolean).join(", ") || "–"}</div>
                  </TableCell>
                  <TableCell className="align-top">
                    {i.costUnknown || i.totalCostUsd == null ? <span className="text-muted-foreground">Chưa có dữ liệu</span> : usd(i.totalCostUsd)}
                  </TableCell>
                  <TableCell className="align-top whitespace-normal">
                    <Reason item={i} />
                  </TableCell>
                  <TableCell className="text-right align-top">
                    <Button
                      type="button"
                      variant="link"
                      size="xs"
                      onClick={() => setOpen(expanded ? null : i.offeringId)}
                    >
                      {expanded ? "Thu gọn" : "Chi tiết"}
                      {expanded ? <ChevronUpIcon aria-hidden="true" /> : <ChevronDownIcon aria-hidden="true" />}
                    </Button>
                  </TableCell>
                </TableRow>
                {expanded && (
                  <TableRow>
                    <TableCell />
                    <TableCell colSpan={4} className="whitespace-normal">
                      <SchoolDetails item={i} />
                    </TableCell>
                  </TableRow>
                )}
              </Fragment>
            );
          })}
        </TableBody>
      </Table>
    </Card>
  );
}

//Lý do: vì sao thuộc nhóm + ưu điểm + nhược điểm. Kết quả cũ chưa có 3 phần này thì hiện câu giải thích cũ
function Reason({ item }: { item: RecommendationItem }) {
  if (!item.categoryReason) return <>{item.reason}</>;

  return (
    <div className="space-y-2">
      <p className="font-medium">{item.categoryReason}</p>
      {!!item.strengths?.length && (
        <div>
          <div className="text-label text-ok">Ưu điểm</div>
          <ul className="mt-0.5 space-y-0.5">
            {item.strengths.map((s) => (
              <li key={s} className="flex gap-1.5">
                <CheckIcon className="mt-0.5 size-4 shrink-0 text-ok" aria-hidden="true" />
                {s}
              </li>
            ))}
          </ul>
        </div>
      )}
      {!!item.weaknesses?.length && (
        <div>
          <div className="text-label text-risk">Nhược điểm</div>
          <ul className="mt-0.5 space-y-0.5">
            {item.weaknesses.map((w) => (
              <li key={w} className="flex gap-1.5">
                <XIcon className="mt-0.5 size-4 shrink-0 text-risk" aria-hidden="true" />
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
          <div key={k} className="flex justify-between gap-3 border-b py-1">
            <dt className="text-muted-foreground">{k}</dt>
            <dd className={v === na ? "text-muted-foreground" : "text-numeric"}>{v}</dd>
          </div>
        ))}
      </dl>
      {s?.website && (
        <a href={websiteUrl(s.website)} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 text-xs text-primary hover:underline">
          Website của trường
          <ExternalLinkIcon className="size-3.5" aria-hidden="true" />
        </a>
      )}
      {item.aiExplained && item.categoryReason && (
        <Alert variant="info">
          <SparklesIcon />
          <AlertDescription>
            <p>
              <span className="font-semibold">Nhận xét của AI: </span>
              {item.reason}
            </p>
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}

// Dữ liệu website có khi thiếu "https://"
function websiteUrl(site: string) {
  return /^https?:\/\//i.test(site) ? site : `https://${site}`;
}
