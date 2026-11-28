"use client";

import { SlidersHorizontalIcon } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { CostBar } from "@/components/costs/CostBar";
import { Cell, CompareTable, CornerCell, HeadCell, RowHead } from "@/components/costs/CompareTable";
import { MissingValue } from "@/components/shared/missing-value";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { useAuth } from "@/contexts/AuthContext";
import { getCostEstimate } from "@/lib/cost-api";
import {
  budgetSourceLabel,
  COST_LABELS,
  gapText,
  rankCells,
  type CellRank,
  TUITION_MISSING_TEXT,
  tuitionMissing,
  usd,
  vnd,
} from "@/lib/cost-format";
import { httpLink } from "@/lib/university-display";
import { cn } from "@/lib/utils";
import type { CostEstimate, SchoolCost } from "@/types/cost";

const ANNUAL_KEYS = ["tuition", "living", "fees", "other", "insurance"];
const ONE_TIME_KEYS = ["visa", "sevis", "visa_integrity"];

/**
 * UC-26 (một trường) và UC-27 (2–4 trường) dùng chung: mỗi cột là một trường.
 * Khoản chưa có dữ liệu dùng MissingValue ("—" + tooltip "Chưa có dữ liệu"), không bao giờ tính là 0.
 * Từ 2 trường: mỗi hàng tô xanh ô thấp nhất, đỏ ô cao nhất. Hàng tổng chỉ xếp các trường đủ khoản,
 * vì tổng của trường thiếu khoản thấp giả tạo.
 */
export function CostEstimateView({
  codes,
  studyLevel,
  onLoaded,
}: {
  codes: string[];
  studyLevel?: string;
  onLoaded?: (schools: SchoolCost[]) => void;
}) {
  const { user, acting } = useAuth();
  const [parentOverride, setParentOverride] = useState<boolean | null>(null);
  const [scholarship, setScholarship] = useState("");
  const [budget, setBudget] = useState("");
  const [data, setData] = useState<CostEstimate | null>(null);
  const [error, setError] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);

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
        .then((d) => { setData(d); setError(false); onLoaded?.(d.schools); })
        .catch((e) => { if (!controller.signal.aborted) { setError(true); console.error(e); } });
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, studyLevel, parentMode, scholarship, budget]);

  if (codes.length === 0) return null;
  if (error) return <p className="text-center text-sm text-risk">Không tải được chi phí ước tính. Vui lòng thử lại sau.</p>;
  if (!data) {
    return (
      <p className="flex items-center justify-center gap-2 py-10 text-sm text-ink-2">
        <Spinner /> Đang tính chi phí...
      </p>
    );
  }

  // Giữ đúng thứ tự người dùng chọn (API trả theo tên)
  const { rate } = data;
  const schools = [...data.schools].sort((a, b) => codes.indexOf(a.code) - codes.indexOf(b.code));
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
  const compare = schools.length >= 2;

  const lineValue = (s: SchoolCost, k: string) => s.lines.find((l) => l.key === k)?.amountUsd ?? null;
  const lineRow = (k: string, sub: string) => (
    <ValueRow
      key={k}
      title={COST_LABELS[k]}
      sub={sub}
      schools={schools}
      ranks={compare ? rankCells(schools.map((s) => lineValue(s, k))) : []}
      cell={(s) => <Money value={lineValue(s, k)} rate={rate.vndPerUsd} />}
    />
  );
  // Hàng tổng: chỉ xếp hạng trường đủ khoản
  const totalRanks = (value: (s: SchoolCost) => number | null) =>
    compare ? rankCells(schools.map((s) => (tuitionMissing(s) || !s.isComplete ? null : value(s)))) : [];

  return (
    <div className="space-y-6">
      <div className="flex justify-end">
        <Dialog open={settingsOpen} onOpenChange={setSettingsOpen}>
          <DialogTrigger render={<Button variant="outline" size="sm" className="gap-2" />}>
            <SlidersHorizontalIcon aria-hidden className="size-4" />
            Ngân sách và học bổng
            {parentMode && <span className="size-1.5 rounded-full bg-brand" aria-label="Đang áp dụng" />}
          </DialogTrigger>
          <DialogContent variant="form" size="md">
            <DialogHeader>
              <DialogIcon tone="brand">
                <SlidersHorizontalIcon aria-hidden />
              </DialogIcon>
              <DialogTitle>Ngân sách và học bổng</DialogTitle>
              <DialogDescription>Nhập mức giả định để đối chiếu với chi phí các trường. Không lưu vào hồ sơ.</DialogDescription>
            </DialogHeader>
            <DialogBody className="space-y-5">
              <label className="flex items-center gap-3 text-sm font-medium text-ink">
                <Switch checked={parentMode} onCheckedChange={(checked) => setParentOverride(checked)} />
                Chế độ phụ huynh
              </label>
              {parentMode && (
                <div className="grid gap-4 sm:grid-cols-2">
                  <label className="space-y-1.5 text-sm">
                    <span className="font-medium text-ink">
                      Ngân sách mỗi năm (USD)
                      {firstBudget && firstBudget.source !== "input" ? <span className="block text-xs font-normal text-ink-2">Đang dùng ngân sách {budgetSource}</span> : null}
                    </span>
                    <Input type="number" min={0} placeholder="Để trống để dùng hồ sơ" value={budget} onChange={(e) => setBudget(e.target.value)} />
                  </label>
                  <label className="space-y-1.5 text-sm">
                    <span className="font-medium text-ink">Học bổng mỗi năm (USD)</span>
                    <Input type="number" min={0} placeholder="Giả định của bạn" value={scholarship} onChange={(e) => setScholarship(e.target.value)} />
                  </label>
                </div>
              )}
              <p className="border-t border-line pt-4 text-xs text-ink-2">
                Tỉ giá tham khảo <span className="font-medium text-ink tabular-nums">1 USD = {Math.round(rate.vndPerUsd).toLocaleString("vi-VN")} ₫</span> (đã cộng biên độ giá bán ra ngân hàng {spreadPct}%)
                {rate.asOf ? `, áp dụng ${new Date(rate.asOf).toLocaleDateString("vi-VN")}` : ""}, nguồn {rate.source}
                {rate.isFallback && " (không lấy được tỉ giá mới, đang dùng số tạm tính)"}. Số VND chỉ để tham khảo.
              </p>
            </DialogBody>
            <DialogFooter layout="single">
              <DialogCancel>Đóng</DialogCancel>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      {data.missingCodes.length > 0 && (
        <p className="text-sm text-risk">Không tìm thấy trường: {data.missingCodes.join(", ")}.</p>
      )}
      {data.academicYearsDiffer && (
        <div className="rounded-xl border border-warn/25 bg-warn-wash p-4 text-sm text-warn">
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
        <p className="text-center text-sm text-ink-2">
          Chưa có ngân sách nên chưa hiển thị nhận định trong hay vượt ngân sách.{" "}
          <Link href="/profile/financial" className="font-medium text-brand underline underline-offset-4 hover:opacity-80">
            Nhập ngân sách ở hồ sơ
          </Link>{" "}
          để đối chiếu với chi phí trường.
        </p>
      )}

      {schools.length > 0 && (
        <section aria-labelledby="cost-chart">
          <h2 id="cost-chart" className="mb-8 font-heading text-xl font-semibold text-ink sm:text-2xl">Chi phí mỗi năm{hasBudget ? " so với ngân sách" : ""}</h2>
          <CostBar schools={schools} scaleMax={scaleMax} />
        </section>
      )}

      <section aria-labelledby="cost-table" className="space-y-3">
        <h2 id="cost-table" className="font-heading text-xl font-semibold text-ink sm:text-2xl">Chi tiết từng khoản</h2>
        <CompareTable minWidth={compare ? 640 : 420}>
          <thead>
            <tr>
              <CornerCell>USD mỗi năm</CornerCell>
              {schools.map((s) => (
                <HeadCell key={s.code}>
                  <Link href={`/university/${s.code}`} className="hover:text-brand hover:underline">{s.name}</Link>
                  <span className="mt-0.5 block text-sm font-normal text-ink-2">
                    {s.academicYear ? `Năm học ${s.academicYear}` : <MissingValue />}
                  </span>
                </HeadCell>
              ))}
            </tr>
          </thead>
          <tbody>
            {ANNUAL_KEYS.map((k) => lineRow(k, "mỗi năm"))}
            <TotalRow title="Ước tính mỗi năm" sub="cộng các khoản trên" schools={schools} rate={rate.vndPerUsd} value={(s) => s.annualUsd} ranks={totalRanks((s) => s.annualUsd)} strong showIncomplete />
            {ONE_TIME_KEYS.map((k) => lineRow(k, "một lần, cộng vào năm đầu"))}
            <TotalRow title="Năm đầu" sub="gồm các khoản một lần" schools={schools} rate={rate.vndPerUsd} value={(s) => s.firstYearUsd} ranks={totalRanks((s) => s.firstYearUsd)} strong />
            <TotalRow
              title="Tổng cả khóa"
              sub={`tăng ${escalationPct}% mỗi năm`}
              schools={schools}
              rate={rate.vndPerUsd}
              value={(s) => s.totalUsd}
              ranks={totalRanks((s) => s.totalUsd)}
              strong
              years
              showIncomplete
            />
            {parentMode && schools.some((s) => s.scholarshipUsdPerYear > 0) && (
              <>
                <TotalRow
                  title="Còn thiếu mỗi năm"
                  sub="sau học bổng giả định"
                  schools={schools}
                  rate={rate.vndPerUsd}
                  value={(s) => s.annualAfterScholarshipUsd}
                  ranks={totalRanks((s) => s.annualAfterScholarshipUsd)}
                />
                <TotalRow
                  title="Tổng cả khóa"
                  sub="sau học bổng giả định"
                  schools={schools}
                  rate={rate.vndPerUsd}
                  value={(s) => s.totalAfterScholarshipUsd}
                  ranks={totalRanks((s) => s.totalAfterScholarshipUsd)}
                  years
                />
              </>
            )}
            {hasBudget && <BudgetRow title="So với ngân sách" sub={`mỗi năm, ngân sách ${budgetSource ?? ""}`.trim()} schools={schools} kind="annual" />}
            {hasBudget && parentMode && <BudgetRow title="So với ngân sách" sub="cả khóa" schools={schools} kind="total" />}
          </tbody>
        </CompareTable>
      </section>

      <ul className="space-y-1.5 text-xs text-ink-2">
        {schools.some((s) => !s.isComplete) && (
          <li>
            Khoản ghi «—» (Chưa có dữ liệu) không được tính là $0. Trường gắn nhãn «Chưa đủ khoản» tạm ẩn nhận định trong hay vượt ngân sách vì tổng thực tế có thể cao hơn{compare ? ", và không được tô màu ở các hàng tổng" : ""}.
          </li>
        )}
        <li>
          Tổng cả khóa {undergradYears} năm tính theo tỷ lệ tăng chi phí hằng năm {escalationPct}% (chi phí năm thứ k = chi phí năm đầu chưa gồm khoản một lần × (1 + {escalationPct}%)^(k − 1)), cộng các khoản thu một lần vào năm đầu.
        </li>
        <li>
          Số tiền VND quy đổi theo tỉ giá tham khảo 1 USD = {Math.round(rate.vndPerUsd).toLocaleString("vi-VN")} ₫ (đã cộng biên độ giá bán ra ngân hàng {spreadPct}%) và chỉ để tham khảo.{" "}
          <Link href="/methodology" className="font-medium text-brand underline underline-offset-4">Xem căn cứ tham số</Link>.
        </li>
        {schools.map((s) => {
          const sourceLink = httpLink(s.sourceUrl);
          return (
            <li key={s.code}>
              {s.name}: nguồn{" "}
              {sourceLink ? <a href={sourceLink.href} target="_blank" rel="noopener noreferrer" className="underline underline-offset-2">{sourceLink.host}</a> : <MissingValue />}
              {s.retrievedAt && `, lấy ngày ${new Date(s.retrievedAt).toLocaleDateString("vi-VN")}`}
            </li>
          );
        })}
      </ul>

      {parentMode && budgetCap && (
        <div className="text-center">
          <Link
            href={`/universities?maxTotalCostUsd=${Math.round(budgetCap)}`}
            className="inline-flex items-center rounded-lg border border-line bg-surface px-4 py-2 text-sm font-medium text-brand transition-colors hover:bg-brand-wash"
          >
            Xem các trường trong tầm ngân sách ({usd(budgetCap)}/năm)
          </Link>
        </div>
      )}
    </div>
  );
}

function ValueRow({ title, sub, schools, ranks, cell }: {
  title: string;
  sub?: string;
  schools: SchoolCost[];
  ranks: CellRank[];
  cell: (s: SchoolCost) => React.ReactNode;
}) {
  return (
    <tr>
      <RowHead title={title} sub={sub} />
      {schools.map((s, i) => <Cell key={s.code} rank={ranks[i]}>{cell(s)}</Cell>)}
    </tr>
  );
}

function TotalRow({ title, sub, schools, rate, value, ranks, strong, years, showIncomplete }: {
  title: string;
  sub?: string;
  schools: SchoolCost[];
  rate: number;
  value: (s: SchoolCost) => number | null;
  ranks: CellRank[];
  strong?: boolean;
  years?: boolean;
  showIncomplete?: boolean;
}) {
  return (
    <tr>
      <RowHead title={title} sub={sub} />
      {schools.map((s, i) => {
        const missingNames = s.missingKeys.filter((k) => k !== "tuition").map((k) => COST_LABELS[k] ?? k);
        return (
          <Cell key={s.code} rank={ranks[i]} className={cn(strong && "text-base")}>
            {tuitionMissing(s) ? (
              <span className="text-xs font-normal text-ink-2">{strong ? TUITION_MISSING_TEXT : <MissingValue />}</span>
            ) : (
              <Money value={value(s)} rate={rate} />
            )}
            {years && !tuitionMissing(s) && s.years === null && (
              <div className="text-xs font-normal text-ink-2">Bậc học này chưa có số năm cố định</div>
            )}
            {showIncomplete && !tuitionMissing(s) && !s.isComplete && (
              <div className="mt-1 space-y-0.5 text-xs font-normal text-warn">
                <span className="inline-block bg-warn-wash px-1.5 py-0.5 font-medium">Chưa đủ khoản</span>
                {missingNames.length > 0 && <div>Thiếu: {missingNames.join(", ")}</div>}
              </div>
            )}
          </Cell>
        );
      })}
    </tr>
  );
}

function BudgetRow({ title, sub, schools, kind }: { title: string; sub?: string; schools: SchoolCost[]; kind: "annual" | "total" }) {
  return (
    <tr>
      <RowHead title={title} sub={sub} />
      {schools.map((s) => {
        const gap = kind === "annual" ? (s.budget?.annualGapUsd ?? null) : (s.budget?.totalGapUsd ?? null);
        // Chỉ đánh dấu khi có nhận định thật: đủ khoản, có trần ngân sách, có số chênh
        const judged = !tuitionMissing(s) && s.isComplete && !s.budget?.unlimited && gap !== null;
        const tone = !judged ? "" : gap <= 0 ? "bg-ink/10 font-semibold" : kind === "annual" && s.budget?.withinTolerance ? "font-semibold text-warn" : "font-semibold text-risk";
        return (
          <Cell key={s.code} className={tone}>
            <BudgetCell school={s} kind={kind} />
          </Cell>
        );
      })}
    </tr>
  );
}

function BudgetCell({ school: s, kind }: { school: SchoolCost; kind: "annual" | "total" }) {
  if (tuitionMissing(s)) return <MissingValue />;
  // UC-26 E2: thiếu bất kỳ khoản bắt buộc nào → ẩn nhận định trong/vượt ngân sách
  if (!s.isComplete) {
    return <span className="text-xs font-normal text-ink-2">Chưa đủ khoản</span>;
  }
  if (s.budget?.unlimited) {
    return <span className="font-normal">Ngân sách trên 70.000 USD, không có trần</span>;
  }
  const gap = kind === "annual" ? (s.budget?.annualGapUsd ?? null) : (s.budget?.totalGapUsd ?? null);
  const text = gapText(gap);
  if (!text) return <MissingValue />;
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <span className="tabular-nums">{text}</span>
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
      <div className="text-ink">{usd(value)}</div>
      <div className="text-xs font-normal text-ink-2">{vnd(value, rate)}</div>
    </>
  );
}
