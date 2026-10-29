"use client";

import { useEffect, useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { getAdminUniversityOfferings } from "@/lib/admin-university-api";
import {
  formatNumber,
  formatRetrievedAt,
  formatSatRange,
  formatUsd,
  parseDeadlines,
  satPolicyLabel,
} from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import type {
  AdminUniversityListItem,
  AdminUniversityOffering,
} from "@/types/admin-university";

interface OfferingsPanelProps {
  university: AdminUniversityListItem | null;
  onClose: () => void;
}

interface LoadResult {
  universityId: string;
  offerings: AdminUniversityOffering[] | null;
  error: string | null;
}

const FRAME = "overflow-hidden rounded-md border border-border";
const LABEL_CELL =
  "w-1/2 border-t border-r border-border bg-muted px-3 py-1.5 text-left text-xs font-normal text-muted-foreground";
const VALUE_CELL =
  "border-t border-border px-3 py-1.5 text-right text-sm font-medium";

interface DataRow {
  label: string;
  value: React.ReactNode;
}

function DataTable({ title, rows }: { title: string; rows: DataRow[] }) {
  return (
    <div className={FRAME}>
      <table className="w-full border-separate border-spacing-0">
        <caption className="bg-muted px-3 py-1.5 text-left text-xs font-semibold tracking-wide text-foreground uppercase">
          {title}
        </caption>
        <tbody>
          {rows.map((row) => (
            <tr key={row.label}>
              <th scope="row" className={LABEL_CELL}>
                {row.label}
              </th>
              <td className={VALUE_CELL}>{row.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function BlockTable({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className={FRAME}>
      <div className="bg-muted px-3 py-1.5 text-xs font-semibold tracking-wide text-foreground uppercase">
        {title}
      </div>
      <div className="border-t border-border px-3 py-2 text-sm">{children}</div>
    </div>
  );
}

function OfferingCard({ offering }: { offering: AdminUniversityOffering }) {
  const deadlines = parseDeadlines(offering.deadlinesJson);
  const totalCost =
    (offering.tuitionUsd ?? 0) +
    (offering.livingUsd ?? 0) +
    (offering.feesUsd ?? 0) +
    (offering.otherExpensesUsd ?? 0);
  const hasCost =
    offering.tuitionUsd !== null ||
    offering.livingUsd !== null ||
    offering.feesUsd !== null ||
    offering.otherExpensesUsd !== null;

  const costRows: DataRow[] = [
    { label: "Học phí", value: formatUsd(offering.tuitionUsd) },
    { label: "Sinh hoạt", value: formatUsd(offering.livingUsd) },
    { label: "Các khoản phí", value: formatUsd(offering.feesUsd) },
    { label: "Chi phí khác", value: formatUsd(offering.otherExpensesUsd) },
    { label: "Tổng", value: hasCost ? formatUsd(totalCost) : "—" },
  ];

  const requirementRows: DataRow[] = [
    { label: "GPA tối thiểu (thang 4)", value: formatNumber(offering.minGpa4) },
    { label: "GPA TB trúng tuyển", value: formatNumber(offering.avgGpa4) },
    { label: "IELTS tối thiểu", value: formatNumber(offering.minIelts) },
    { label: "TOEFL tối thiểu", value: formatNumber(offering.minToefl) },
    { label: "Duolingo tối thiểu", value: formatNumber(offering.minDuolingo) },
    {
      label: "SAT 25% – 75%",
      value: formatSatRange(offering.sat25, offering.sat75),
    },
    { label: "Chính sách SAT", value: satPolicyLabel(offering.satPolicy) },
  ];

  const deadlineRows: DataRow[] =
    deadlines.length === 0
      ? [{ label: "Chưa có dữ liệu", value: "—" }]
      : deadlines.map((deadline) => ({
          label: deadline.round,
          value: deadline.date,
        }));

  const sourceRows: DataRow[] = [
    { label: "Ngày lấy", value: formatRetrievedAt(offering.retrievedAt) },
    {
      label: "Nguồn",
      value: offering.sourceUrl ? (
        <a
          href={offering.sourceUrl}
          target="_blank"
          rel="noreferrer"
          className="text-brand underline-offset-2 hover:underline"
        >
          Mở liên kết
        </a>
      ) : (
        "—"
      ),
    },
  ];

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-base font-semibold">
          {studyLevelLabel(offering.studyLevel)}
        </h3>
        {offering.academicYear && (
          <Badge variant="outline">{offering.academicYear}</Badge>
        )}
      </div>

      <DataTable title="Chi phí mỗi năm" rows={costRows} />
      <DataTable title="Điều kiện" rows={requirementRows} />
      <DataTable title="Hạn nộp hồ sơ" rows={deadlineRows} />

      <BlockTable title={`Ngành (${offering.majors.length})`}>
        {offering.majors.length === 0 ? (
          <span className="text-muted-foreground">—</span>
        ) : (
          <div className="flex flex-wrap gap-1">
            {offering.majors.map((major) => (
              <Badge
                key={major}
                variant={
                  offering.stemMajors.includes(major) ? "brand" : "outline"
                }
              >
                {major}
              </Badge>
            ))}
          </div>
        )}
        {offering.stemMajors.length > 0 && (
          <p className="mt-2 text-xs text-muted-foreground">
            Ngành tô đậm là ngành STEM.
          </p>
        )}
      </BlockTable>

      <DataTable title="Nguồn dữ liệu" rows={sourceRows} />
      {offering.notes && (
        <p className="text-xs text-muted-foreground">{offering.notes}</p>
      )}
    </section>
  );
}

export function OfferingsPanel({ university, onClose }: OfferingsPanelProps) {
  const [result, setResult] = useState<LoadResult | null>(null);
  const universityId = university?.id ?? null;
  const isLoading =
    universityId !== null && result?.universityId !== universityId;
  const offerings =
    result?.universityId === universityId ? result.offerings : null;
  const error = result?.universityId === universityId ? result.error : null;

  useEffect(() => {
    if (!universityId) {
      return;
    }
    let cancelled = false;

    getAdminUniversityOfferings(universityId)
      .then((loaded) => {
        if (!cancelled) {
          setResult({ universityId, offerings: loaded, error: null });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          const message =
            err instanceof Error
              ? err.message
              : "Không thể tải danh sách bậc học.";
          setResult({ universityId, offerings: null, error: message });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [universityId]);

  if (!university) {
    return null;
  }

  return (
    <aside
      aria-label={`Bậc học của ${university.name}`}
      className="w-full shrink-0 rounded-xl bg-card p-4 ring-1 ring-border lg:h-full lg:min-h-0 lg:w-[26rem] lg:overflow-y-auto xl:w-[30rem]"
    >
      <div className="space-y-5">
        <header className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <h2 className="text-lg font-semibold leading-tight">
              {university.name}
            </h2>
            <p className="text-sm text-muted-foreground">
              Mã {university.code}. Danh sách bậc học (offering) của trường.
            </p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="Đóng"
            onClick={onClose}
          >
            <XIcon />
          </Button>
        </header>

        {isLoading && (
          <div className="flex items-center gap-2 text-muted-foreground">
            <Spinner />
            Đang tải bậc học...
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {offerings && offerings.length === 0 && (
          <p className="text-muted-foreground">
            Trường này chưa có bậc học nào.
          </p>
        )}

        {offerings?.map((offering) => (
          <OfferingCard key={offering.id} offering={offering} />
        ))}
      </div>
    </aside>
  );
}
