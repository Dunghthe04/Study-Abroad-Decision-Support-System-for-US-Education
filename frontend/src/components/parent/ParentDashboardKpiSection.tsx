"use client";

import { useEffect, useState, type ReactNode } from "react";
import { PencilIcon, WalletIcon } from "lucide-react";
import { StudentDashboardView } from "@/components/dashboard/StudentDashboardView";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ApiError, dashboardApi } from "@/lib/api";
import { MAX_BUDGET_VND, MIN_BUDGET_VND, parentPreferenceApi } from "@/lib/parent-preference-api";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { useFetched } from "@/lib/use-fetched";
import type { ParentChildDashboard, ParentFinancialKpi } from "@/types/dashboard";

// Link trong thẻ của con → tab tương ứng trong trang Con của tôi; hồ sơ đã hiện ngay bên dưới nên không link
const childLinks = (studentId: string) => {
  const base = `/parent/children/${encodeURIComponent(studentId)}`;
  return { profile: null, recommendations: `${base}?tab=recommend`, targetSchools: `${base}?tab=targets` };
};

const usd = (n: number) => `$${Math.round(n).toLocaleString("en-US")}`;
const VND = new Intl.NumberFormat("vi-VN");
const digits = (text: string) => text.replace(/\D/g, "");

/** UC-94: phụ huynh (con liên kết thật) chưa nhập số tiền ở UC-76 → nhập ở đây. Chỉ lưu tiền, không lưu ưu tiên, không báo con. */
function BudgetDialog({ studentId, open, onOpenChange, onSaved }: {
  studentId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [text, setText] = useState("");
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  // Tỷ giá hôm nay cùng nguồn với form hồ sơ; chưa tải được thì không hiện dòng quy đổi
  const meta = useFetched(open ? "profile-metadata" : "closed", () =>
    open ? profileFlowApi.getMetadata() : Promise.resolve(null));
  const rate = meta.data?.vndPerUsd ?? null;

  const amount = digits(text) ? Number(digits(text)) : null;
  const rangeError = amount != null && (amount < MIN_BUDGET_VND || amount > MAX_BUDGET_VND)
    ? `Số tiền phải từ ${VND.format(MIN_BUDGET_VND)} đến ${VND.format(MAX_BUDGET_VND)} VND.`
    : null;
  const canSave = amount != null && !rangeError && !saving;

  const save = async () => {
    if (amount == null) return;
    setSaving(true);
    setServerError(null);
    try {
      await parentPreferenceApi.saveBudget(studentId, amount);
      setText("");
      onSaved();
    } catch (e) {
      setServerError(e instanceof ApiError ? e.message : "Không lưu được, thử lại sau.");
    } finally {
      setSaving(false);
    }
  };

  const error = rangeError ?? serverError;

  return (
    <Dialog open={open} onOpenChange={(o) => { if (!saving) { setServerError(null); onOpenChange(o); } }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Số tiền tối đa chi trả được mỗi năm</DialogTitle>
          <DialogDescription>
            Chỉ lưu số tiền để so với chi phí các trường. Số này cũng tự điền vào tab So sánh ưu tiên. Con không nhận thông báo.
          </DialogDescription>
        </DialogHeader>
        <Field data-invalid={error ? true : undefined}>
          <FieldLabel htmlFor="parent-budget-vnd">Số tiền mỗi năm (VND)</FieldLabel>
          <Input
            id="parent-budget-vnd"
            inputMode="numeric"
            placeholder="Ví dụ 1.300.000.000"
            value={text}
            onChange={(e) => { setServerError(null); setText(digits(e.target.value) ? VND.format(Number(digits(e.target.value))) : ""); }}
            onKeyDown={(e) => { if (e.key === "Enter" && canSave) void save(); }}
            aria-invalid={error ? true : undefined}
          />
          <FieldDescription>
            Từ 100 triệu đến 10 tỷ đồng.
            {amount != null && rate ? ` ≈ ${usd(amount / rate)}/năm theo tỷ giá hôm nay (${VND.format(Math.round(rate))} VND/USD).` : ""}
          </FieldDescription>
          {error && <FieldError>{error}</FieldError>}
        </Field>
        <DialogFooter>
          <Button type="button" variant="outline" disabled={saving} onClick={() => onOpenChange(false)}>Hủy</Button>
          <Button type="button" disabled={!canSave} onClick={() => void save()}>
            {saving && <Spinner className="size-4" />} Lưu số tiền
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function Stat({ label, value, sub, note }: { label: string; value: ReactNode; sub?: ReactNode; note: string }) {
  return (
    <Card size="sm">
      <CardContent className="space-y-1">
        <span className="block text-label">{label}</span>
        <div className="text-numeric text-2xl font-bold text-foreground">{value}</div>
        {sub && <div className="text-xs text-muted-foreground">{sub}</div>}
        <p className="pt-1 text-xs leading-relaxed text-muted-foreground">{note}</p>
      </CardContent>
    </Card>
  );
}

function FinancialCard({ fin, onEnterBudget }: { fin: ParentFinancialKpi; onEnterBudget?: () => void }) {
  const most = fin.mostShortfallSchool;
  const mostGap = most?.shortfall4Years ?? 0;

  return (
    <Card>
      <CardHeader className="border-b">
        <CardTitle className="flex items-center gap-2 text-base">
          <WalletIcon className="size-5 text-muted-foreground" aria-hidden />
          <h3>Chi phí so với ngân sách</h3>
        </CardTitle>
        <CardDescription className="space-y-2">
          <span className="block">{fin.budgetSourceLabel}</span>
          {onEnterBudget && (
            <Button type="button" size="sm" variant="outline" onClick={onEnterBudget}>
              <PencilIcon aria-hidden /> Nhập số tiền tối đa chi trả được/năm
            </Button>
          )}
        </CardDescription>
        <CardAction className="text-right">
          <span className="block text-label">Ngân sách/năm</span>
          <span className="text-numeric text-lg font-bold text-foreground">
            {fin.budgetPerYear != null ? usd(fin.budgetPerYear) : "Chưa có"}
          </span>
        </CardAction>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Stat
            label="Trường trong ngân sách"
            value={<>{fin.schoolsWithinBudgetCount}<span className="text-sm font-normal text-muted-foreground"> / {fin.schoolsTotalWithCostCount}</span></>}
            sub={fin.schoolsMissingCostCount > 0 ? `${fin.schoolsMissingCostCount} trường chưa có số liệu chi phí` : undefined}
            note={fin.withinBudgetExplanation}
          />
          <Stat
            label="Tổng chi phí 4 năm"
            value={usd(fin.totalCost4YearsAllSchools)}
            sub={`Cộng ${fin.schoolsTotalWithCostCount} trường có số liệu`}
            note={fin.totalCost4YearsExplanation}
          />
          <Stat
            label="Khoản còn thiếu (4 năm)"
            value={fin.budgetPerYear == null ? "—"
              : mostGap > 0 ? <span className="text-risk">{usd(mostGap)}</span>
              : <span className="text-ok">$0</span>}
            sub={fin.budgetPerYear == null ? "Chưa có ngân sách để so"
              : mostGap > 0 ? `Nhiều nhất: ${most!.universityName}`
              : "Không trường nào vượt ngân sách"}
            note={fin.shortfallExplanation}
          />
        </div>

        {fin.shortfallList.length > 0 && (
          <div className="space-y-2">
            <span className="block text-label">Từng trường mục tiêu ({fin.shortfallList.length})</span>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Trường</TableHead>
                  <TableHead className="text-right">1 năm</TableHead>
                  <TableHead className="text-right">4 năm</TableHead>
                  <TableHead className="text-right">Còn thiếu</TableHead>
                  <TableHead>Đánh giá</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {fin.shortfallList.map((s) => {
                  const within = s.hasCostData && fin.budgetPerYear != null && (s.totalCost1Year ?? 0) <= fin.budgetPerYear;
                  return (
                    <TableRow key={s.universityId}>
                      <TableCell className="max-w-56 truncate font-medium text-foreground">{s.universityName}</TableCell>
                      <TableCell className="text-numeric text-right">{s.totalCost1Year != null ? usd(s.totalCost1Year) : "—"}</TableCell>
                      <TableCell className="text-numeric text-right">{s.totalCost4Years != null ? usd(s.totalCost4Years) : "—"}</TableCell>
                      <TableCell className="text-numeric text-right">
                        {s.shortfall4Years == null ? "—" : s.shortfall4Years > 0
                          ? <span className="font-semibold text-risk">{usd(s.shortfall4Years)}</span>
                          : <span className="text-ok">$0</span>}
                      </TableCell>
                      <TableCell>
                        {!s.hasCostData ? <Badge variant="neutral">Chưa có số liệu</Badge>
                          : fin.budgetPerYear == null ? <Badge variant="neutral">Chưa có ngân sách</Badge>
                          : within ? <Badge variant="ok">Trong ngân sách</Badge>
                          : <Badge variant="risk">Vượt ngân sách</Badge>}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/** UC-94: phần tài chính + 4 thẻ chỉ số của con, đặt đầu tab Tổng quan trong ChildDashboard. */
export function ParentDashboardKpiSection({ studentId, childName }: { studentId: string; childName: string }) {
  // Kết quả gắn với studentId: đổi con thì coi như đang tải, không cần setState trong effect
  const [state, setState] = useState<{ id: string; data?: ParentChildDashboard; error?: string } | null>(null);
  const [reload, setReload] = useState(0);
  const [budgetOpen, setBudgetOpen] = useState(false);

  useEffect(() => {
    let alive = true;
    dashboardApi
      .getChildDashboard(studentId)
      .then((data) => { if (alive) setState({ id: studentId, data }); })
      .catch((err) => { if (alive) setState({ id: studentId, error: err?.message || "Chưa tải được chỉ số tổng quan." }); });
    return () => { alive = false; };
  }, [studentId, reload]);

  const isLoading = state?.id !== studentId;
  const data = isLoading ? null : state?.data ?? null;
  const error = isLoading ? null : state?.error ?? null;

  if (isLoading) {
    return (
      <div className="flex min-h-[20vh] items-center justify-center">
        <Spinner className="size-6 text-brand" />
      </div>
    );
  }

  if (error || !data) {
    // Không chặn phần bảng điểm bên dưới, chỉ báo một dòng
    return (
      <div className="rounded-2xl border border-line bg-surface p-6 text-center text-sm text-muted-foreground">
        Chưa tải được chỉ số tổng quan của {childName}. {error}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <section className="space-y-3">
        <h2 className="text-h3 text-foreground">Tài chính</h2>
        <FinancialCard
          fin={data.financialKpis}
          // Con liên kết thật chưa có số tiền của phụ huynh (UC-76): cho nhập ngay ở đây
          onEnterBudget={!data.isManaged && data.financialKpis.budgetSource !== "parent_preference" ? () => setBudgetOpen(true) : undefined}
        />
        {!data.isManaged && (
          <BudgetDialog
            studentId={studentId}
            open={budgetOpen}
            onOpenChange={setBudgetOpen}
            onSaved={() => { setBudgetOpen(false); setReload((k) => k + 1); }}
          />
        )}
      </section>
      <section className="space-y-3">
        <h2 className="text-h3 text-foreground">Tiến độ của {childName}</h2>
        <StudentDashboardView dashboard={data.studentKpis} links={childLinks(studentId)} />
      </section>
    </div>
  );
}
