import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { AMOUNT_TYPE_LABELS, APPLICATION_METHOD_LABELS, coverageLabel, KIND_LABELS } from "@/lib/admin-scholarship-display";
import { formatUsd } from "@/lib/admin-university-display";
import { httpLink } from "@/lib/university-display";
import type { ScholarshipItem } from "@/types/scholarship";

const LEVEL_LABELS: Record<string, string> = { undergraduate: "Đại học", graduate: "Sau đại học" };

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString("vi-VN") : "";
}

// Toàn phần thì ghi theo kiểu giá trị; còn lại ưu tiên số cố định, rồi khoảng thấp nhất–cao nhất
function amountText(s: ScholarshipItem): string {
  if (s.amountType === "full_tuition" || s.amountType === "full_ride") return AMOUNT_TYPE_LABELS[s.amountType];
  if (s.amountUsd !== null) return formatUsd(s.amountUsd);
  if (s.amountMinUsd !== null || s.amountMaxUsd !== null) {
    return `${s.amountMinUsd === null ? "?" : formatUsd(s.amountMinUsd)} – ${s.amountMaxUsd === null ? "?" : formatUsd(s.amountMaxUsd)}`;
  }
  return s.amountType ? (AMOUNT_TYPE_LABELS[s.amountType] ?? "Chưa rõ giá trị") : "Chưa rõ giá trị";
}

export function ScholarshipList({ items }: { items: ScholarshipItem[] }) {
  if (items.length === 0) {
    return <p className="rounded-xl border p-8 text-center text-muted-foreground">Không có học bổng phù hợp với bộ lọc.</p>;
  }
  return (
    <ul className="space-y-3">
      {items.map((s) => (
        <li key={s.id} className="rounded-xl border bg-card p-4">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <h2 className="font-semibold">{s.name}</h2>
              <p className="text-sm text-muted-foreground">
                {s.universityCode && s.universityName ? (
                  <Link href={`/university/${s.universityCode}`} className="hover:underline">{s.universityName}</Link>
                ) : (
                  s.provider
                )}
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">{LEVEL_LABELS[s.studyLevel] ?? s.studyLevel}</Badge>
              {s.kind && <Badge variant="secondary">{KIND_LABELS[s.kind] ?? s.kind}</Badge>}
              {s.vnEligible === true && <Badge>Mở cho du học sinh Việt Nam</Badge>}
              <Badge variant="outline">{amountText(s)} · {coverageLabel(s.coverageType)}</Badge>
            </div>
          </div>
          <dl className="mt-3 grid gap-x-6 gap-y-1 text-sm sm:grid-cols-3">
            <div><dt className="inline text-muted-foreground">Hạn nộp: </dt><dd className="inline">{s.deadline ? formatDate(s.deadline) : "Xét quanh năm / chưa rõ"}</dd></div>
            <div><dt className="inline text-muted-foreground">GPA tối thiểu: </dt><dd className="inline">{s.minGpa4 ?? "Không đòi hỏi"}</dd></div>
            <div><dt className="inline text-muted-foreground">IELTS tối thiểu: </dt><dd className="inline">{s.minIelts ?? "Không đòi hỏi"}</dd></div>
            {(s.renewable !== null || s.durationYears !== null) && (
              <div>
                <dt className="inline text-muted-foreground">Thời gian: </dt>
                <dd className="inline">
                  {[s.durationYears !== null ? `${s.durationYears} năm` : null, s.renewable === null ? null : s.renewable ? "có gia hạn" : "không gia hạn"].filter(Boolean).join(", ")}
                </dd>
              </div>
            )}
            {s.applicationMethod && (
              <div><dt className="inline text-muted-foreground">Cách nộp: </dt><dd className="inline">{APPLICATION_METHOD_LABELS[s.applicationMethod] ?? s.applicationMethod}</dd></div>
            )}
            {(() => {
              const applyLink = httpLink(s.applyUrl);
              return applyLink ? (
                <div><dt className="inline text-muted-foreground">Nộp hồ sơ: </dt><dd className="inline"><a href={applyLink.href} target="_blank" rel="noopener noreferrer" className="underline">{applyLink.host}</a></dd></div>
              ) : null;
            })()}
          </dl>
          {s.eligibilityNotes && <p className="mt-2 text-sm">{s.eligibilityNotes}</p>}
          <p className="mt-2 text-xs text-muted-foreground">
            Nguồn:{" "}
            {(() => {
              const sourceLink = httpLink(s.sourceUrl);
              return sourceLink ? <a href={sourceLink.href} target="_blank" rel="noopener noreferrer" className="underline">{sourceLink.host}</a> : "chưa có liên kết";
            })()}
            {s.retrievedAt && ` · lấy ngày ${formatDate(s.retrievedAt)}`}
          </p>
        </li>
      ))}
    </ul>
  );
}
