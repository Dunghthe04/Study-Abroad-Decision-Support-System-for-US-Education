"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CostBar } from "@/components/costs/CostBar";
import { MissingValue } from "@/components/shared/missing-value";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { useAuth } from "@/contexts/AuthContext";
import { getCostEstimate } from "@/lib/cost-api";
import { budgetSourceLabel, COST_LABELS, gapText, TUITION_MISSING_TEXT, tuitionMissing, usd, vnd } from "@/lib/cost-format";
import { httpLink } from "@/lib/university-display";
import type { CostEstimate, SchoolCost } from "@/types/cost";

const LINE_KEYS = ["tuition", "living", "fees", "other", "insurance", "visa", "sevis", "visa_integrity"];

/**
 * UC-26 (một trường) và UC-27 (2–4 trường) dùng chung: mỗi cột là một trường.
 * Khoản chưa có dữ liệu dùng MissingValue ("—" + tooltip "Chưa có dữ liệu"), không bao giờ tính là 0.
 */
export function CostEstimateView({ codes, studyLevel }: { codes: string[]; studyLevel?: string }) {
  const { user, acting } = useAuth();
  const [parentOverride, setParentOverride] = useState<boolean | null>(null);
  const [scholarship, setScholarship] = useState("");
  const [budget, setBudget] = useState("");
  const [data, setData] = useState<CostEstimate | null>(null);
  const [error, setError] = useState(false);

  // Phụ huynh (kể cả đang làm thay con) mở sẵn chế độ phụ huynh
  const isParent = user?.role === "parent" || acting !== null;
  const parentMode = parentOverride ?? isParent;

  const key = codes.join(",");
  useEffect(() => {
    if (codes.length === 0) return;
    const controller = new AbortController();
    const timer = setTimeout(() => {
      getCostEstimate(
        {
          codes,
          studyLevel,
          scholarshipUsdPerYear: parentMode ? Number(scholarship) || undefined : undefined,
          budgetUsd: parentMode ? Number(budget) || undefined : undefined,
        },
        controller.signal,
      )
        .then((d) => { setData(d); setError(false); })
        .catch((e) => { if (!controller.signal.aborted) { setError(true); console.error(e); } });
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, studyLevel, parentMode, scholarship, budget]);

  if (codes.length === 0) return null;
  if (error) return <p className="text-sm text-destructive">Không tải được chi phí ước tính. Vui lòng thử lại sau.</p>;
  if (!data) return <p className="text-sm text-muted-foreground">Đang tính chi phí...</p>;

  const { rate, schools } = data;
  const escalationRate = data.annualEscalationRate ?? 0.04;
  const escalationPct = (escalationRate * 100).toLocaleString("vi-VN", { maximumFractionDigits: 2 });
  const spreadRate = data.bankSellRateSpread ?? 0.015;
  const spreadPct = (spreadRate * 100).toLocaleString("vi-VN", { maximumFractionDigits: 2 });
  const undergradYears = data.undergraduateYears ?? 4;

  const firstBudget = schools.find((s) => s.budget !== null)?.budget ?? null;
  const hasBudget = firstBudget !== null;
  const budgetCap = firstBudget?.annualBudgetUsd ?? null;
  const budgetSource = firstBudget ? budgetSourceLabel(firstBudget.source) : null;
  const scaleMax = Math.max(1, ...schools.map((s) => s.annualUsd ?? 0), ...schools.map((s) => s.budget?.annualBudgetUsd ?? 0));

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <label className="inline-flex items-center gap-2 text-sm">
          <input type="checkbox" checked={parentMode} onChange={(e) => setParentOverride(e.target.checked)} />
          Chế độ phụ huynh
        </label>
        <p className="text-xs text-muted-foreground">
          Tỉ giá tham khảo 1 USD = {Math.round(rate.vndPerUsd).toLocaleString("vi-VN")} ₫ (đã cộng biên độ giá bán ra ngân hàng {spreadPct}%)
          {rate.asOf ? `, áp dụng ${new Date(rate.asOf).toLocaleDateString("vi-VN")}` : ""}, nguồn {rate.source}
          {rate.isFallback && " (không lấy được tỉ giá mới, đang dùng số tạm tính)"}. Số VND chỉ để tham khảo.
        </p>
      </div>

      {parentMode && (
        <div className="grid gap-3 rounded-xl border bg-card p-4 sm:grid-cols-2">
          <label className="space-y-1 text-sm">
            <span>
              Ngân sách mỗi năm (USD)
              {firstBudget && firstBudget.source !== "input" ? ` (đang dùng ngân sách ${budgetSource})` : ""}
            </span>
            <Input type="number" min={0} placeholder="Để trống để dùng ngân sách trong hồ sơ" value={budget} onChange={(e) => setBudget(e.target.value)} />
          </label>
          <label className="space-y-1 text-sm">
            <span>Học bổng dự kiến mỗi năm (USD)</span>
            <Input type="number" min={0} placeholder="Giả định của bạn, không lưu" value={scholarship} onChange={(e) => setScholarship(e.target.value)} />
          </label>
        </div>
      )}

      {data.missingCodes.length > 0 && (
        <p className="text-sm text-destructive">Không tìm thấy trường: {data.missingCodes.join(", ")}.</p>
      )}
      {data.academicYearsDiffer && (
        <div className="rounded-lg border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-200">
          Các trường dùng số liệu của năm học khác nhau:{" "}
          {schools.map((s, idx) => (
            <span key={s.code}>
              {idx > 0 && "; "}
              <strong>{s.name}</strong> ({s.academicYear ?? "chưa rõ năm học"})
            </span>
          ))}
          . Cần cân nhắc khi so sánh.
        </div>
      )}
      {!hasBudget && (
        <p className="text-sm text-muted-foreground">
          Chưa có ngân sách nên chưa hiển thị nhận định trong hay vượt ngân sách.{" "}
          <Link href="/profile/financial" className="font-medium text-brand underline underline-offset-4 hover:opacity-80">
            Nhập ngân sách ở hồ sơ
          </Link>{" "}
          để đối chiếu với chi phí trường.
        </p>
      )}

      {schools.length > 0 && <CostBar schools={schools} scaleMax={scaleMax} />}

      <div className="overflow-x-auto">
        <table className="w-full min-w-[480px] border-collapse text-sm">
          <thead>
            <tr className="border-b text-left">
              <th className="py-2 pr-3 font-medium">Khoản mục (USD/năm)</th>
              {schools.map((s) => (
                <th key={s.code} className="px-3 py-2 font-medium">
                  <Link href={`/university/${s.code}`} className="hover:underline">{s.name}</Link>
                  <div className="text-xs font-normal text-muted-foreground">
                    {s.academicYear ? `Năm học ${s.academicYear}` : <MissingValue />}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {LINE_KEYS.map((k) => (
              <tr key={k} className="border-b">
                <td className="py-2 pr-3 text-muted-foreground">{COST_LABELS[k]}</td>
                {schools.map((s) => <Cell key={s.code} school={s} lineKey={k} rate={rate.vndPerUsd} />)}
              </tr>
            ))}
            <Row label="Ước tính mỗi năm" schools={schools} rate={rate.vndPerUsd} value={(s) => s.annualUsd} strong showIncomplete />
            <Row label="Năm đầu (gồm khoản một lần)" schools={schools} rate={rate.vndPerUsd} value={(s) => s.firstYearUsd} />
            <Row
              label={`Tổng cả khóa (tăng ${escalationPct}%/năm)`}
              schools={schools}
              rate={rate.vndPerUsd}
              value={(s) => s.totalUsd}
              years
              showIncomplete
            />
            {parentMode && schools.some((s) => s.scholarshipUsdPerYear > 0) && (
              <>
                <Row
                  label="Còn thiếu mỗi năm sau học bổng giả định"
                  schools={schools}
                  rate={rate.vndPerUsd}
                  value={(s) => s.annualAfterScholarshipUsd}
                />
                <Row
                  label="Tổng cả khóa sau học bổng giả định"
                  schools={schools}
                  rate={rate.vndPerUsd}
                  value={(s) => s.totalAfterScholarshipUsd}
                  years
                />
              </>
            )}
            {hasBudget && (
              <tr className="border-b">
                <td className="py-2 pr-3 font-medium">
                  So với ngân sách mỗi năm
                  {budgetSource && <div className="text-xs font-normal text-muted-foreground">Ngân sách {budgetSource}</div>}
                </td>
                {schools.map((s) => (
                  <td key={s.code} className="px-3 py-2">
                    <BudgetCell school={s} kind="annual" />
                  </td>
                ))}
              </tr>
            )}
            {hasBudget && parentMode && (
              <tr className="border-b">
                <td className="py-2 pr-3 font-medium">
                  So với ngân sách cả khóa
                  {budgetSource && <div className="text-xs font-normal text-muted-foreground">Ngân sách {budgetSource}</div>}
                </td>
                {schools.map((s) => (
                  <td key={s.code} className="px-3 py-2">
                    <BudgetCell school={s} kind="total" />
                  </td>
                ))}
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <ul className="space-y-1 text-xs text-muted-foreground">
        {schools.some((s) => !s.isComplete) && (
          <li>
            Khoản ghi «—» (Chưa có dữ liệu) không được tính là $0. Trường gắn nhãn «Chưa đủ khoản» tạm ẩn nhận định trong hay vượt ngân sách vì tổng thực tế có thể cao hơn.
          </li>
        )}
        <li>
          Tổng cả khóa {undergradYears} năm tính theo tỷ lệ tăng chi phí hằng năm {escalationPct}% (chi phí năm thứ k = chi phí năm đầu chưa gồm khoản một lần × (1 + {escalationPct}%)^(k − 1)), cộng các khoản thu một lần vào năm đầu.
        </li>
        <li>
          Số tiền VND quy đổi theo tỉ giá tham khảo (đã cộng biên độ giá bán ra ngân hàng {spreadPct}%) và chỉ để tham khảo.{" "}
          <Link href="/methodology" className="underline">Xem căn cứ tham số</Link>.
        </li>
        {schools.map((s) => {
          const sourceLink = httpLink(s.sourceUrl);
          return (
            <li key={s.code}>
              {s.name}: nguồn{" "}
              {sourceLink ? <a href={sourceLink.href} target="_blank" rel="noopener noreferrer" className="underline">{sourceLink.host}</a> : <MissingValue />}
              {s.retrievedAt && `, lấy ngày ${new Date(s.retrievedAt).toLocaleDateString("vi-VN")}`}
            </li>
          );
        })}
      </ul>

      {parentMode && budgetCap && (
        <Link href={`/universities?maxTotalCostUsd=${Math.round(budgetCap)}`} className="inline-block text-sm underline">
          Xem các trường trong tầm ngân sách ({usd(budgetCap)}/năm)
        </Link>
      )}
    </div>
  );
}

function BudgetCell({ school: s, kind }: { school: SchoolCost; kind: "annual" | "total" }) {
  if (tuitionMissing(s)) return <MissingValue />;
  // UC-26 E2: thiếu bất kỳ khoản bắt buộc nào → ẩn nhận định trong/vượt ngân sách
  if (!s.isComplete) {
    return <span className="text-xs text-muted-foreground">Chưa đủ khoản</span>;
  }
  if (s.budget?.unlimited) {
    return <span>Ngân sách trên 70.000 USD, không có trần</span>;
  }
  const gap = kind === "annual" ? (s.budget?.annualGapUsd ?? null) : (s.budget?.totalGapUsd ?? null);
  const text = gapText(gap);
  if (!text) return <MissingValue />;
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <span>{text}</span>
      {kind === "annual" && s.budget?.withinTolerance && (
        <Badge variant="warn" className="text-[11px]">Trong biên 10%</Badge>
      )}
    </div>
  );
}

function Money({ value, rate }: { value: number | null; rate: number }) {
  if (value === null) return <MissingValue />;
  return (
    <>
      <div>{usd(value)}</div>
      <div className="text-xs text-muted-foreground">{vnd(value, rate)}</div>
    </>
  );
}

function Cell({ school, lineKey, rate }: { school: SchoolCost; lineKey: string; rate: number }) {
  const line = school.lines.find((l) => l.key === lineKey);
  return <td className="px-3 py-2"><Money value={line?.amountUsd ?? null} rate={rate} /></td>;
}

function Row({ label, schools, rate, value, strong, years, showIncomplete }: {
  label: string;
  schools: SchoolCost[];
  rate: number;
  value: (s: SchoolCost) => number | null;
  strong?: boolean;
  years?: boolean;
  showIncomplete?: boolean;
}) {
  return (
    <tr className={`border-b ${strong ? "font-semibold" : ""}`}>
      <td className="py-2 pr-3">{label}</td>
      {schools.map((s) => {
        const missingNames = s.missingKeys.filter((k) => k !== "tuition").map((k) => COST_LABELS[k] ?? k);
        return (
          <td key={s.code} className="px-3 py-2">
            {tuitionMissing(s) ? (
              <span className="text-xs font-normal text-muted-foreground">{strong ? TUITION_MISSING_TEXT : <MissingValue />}</span>
            ) : (
              <Money value={value(s)} rate={rate} />
            )}
            {years && !tuitionMissing(s) && s.years === null && (
              <div className="text-xs font-normal text-muted-foreground">Bậc học này chưa có số năm cố định</div>
            )}
            {showIncomplete && !tuitionMissing(s) && !s.isComplete && (
              <div className="mt-1 space-y-0.5 text-xs font-normal text-amber-800 dark:text-amber-300">
                <span className="inline-block rounded bg-amber-100 px-1.5 py-0.5 font-medium dark:bg-amber-950/60">Chưa đủ khoản</span>
                {missingNames.length > 0 && <div>Thiếu: {missingNames.join(", ")}</div>}
              </div>
            )}
          </td>
        );
      })}
    </tr>
  );
}
