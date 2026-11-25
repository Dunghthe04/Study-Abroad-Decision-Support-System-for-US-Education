"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/AuthContext";
import { ListRestartIcon, RotateCcwIcon, SaveIcon } from "lucide-react";
import {
  getAdminCostParameters,
  saveCostParameters,
  type CostParameters,
  type CostParametersSetting,
} from "@/lib/admin-cost-parameters-api";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { formatTime } from "./GradeScaleHistoryDialog";

interface Draft {
  visaIntegrityFeeUsd: string;
  annualEscalationPct: string;
  bankSellSpreadPct: string;
  undergraduateYears: string;
}

function formatPct(rate: number): string {
  return String(Math.round(rate * 10_000) / 100);
}

function toDraft(values: CostParameters): Draft {
  return {
    visaIntegrityFeeUsd: String(values.visaIntegrityFeeUsd),
    annualEscalationPct: formatPct(values.annualEscalationRate),
    bankSellSpreadPct: formatPct(values.bankSellRateSpread),
    undergraduateYears: String(values.undergraduateYears),
  };
}

function parseNumber(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() === "" || !Number.isFinite(value) ? null : value;
}

/**
 * [UC-26 / UC-83] Quản lý 4 tham số ước tính chi phí lưu trong app_settings (cost.parameters).
 * Moderator sửa và lưu; Administrator chỉ xem. Lưu xong làm mới bảng căn cứ tham số ngay.
 */
export function CostParametersSettings() {
  const { user } = useAuth();
  const canEdit = user?.role === "moderator";
  const router = useRouter();
  const [setting, setSetting] = useState<CostParametersSetting | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [conflict, setConflict] = useState<CostParametersSetting | null>(null);
  const [saving, setSaving] = useState(false);

  const showSetting = useCallback((value: CostParametersSetting) => {
    setSetting(value);
    setDraft(toDraft(value.values));
    setErrors([]);
    setConflict(null);
  }, []);

  useEffect(() => {
    let active = true;
    getAdminCostParameters()
      .then((value) => { if (active) showSetting(value); })
      .catch(() => { if (active) setLoadError("Không tải được tham số ước tính chi phí."); });
    return () => { active = false; };
  }, [showSetting]);

  const handleSave = async () => {
    if (!setting || !draft) return;
    const fee = parseNumber(draft.visaIntegrityFeeUsd);
    const escalationPct = parseNumber(draft.annualEscalationPct);
    const spreadPct = parseNumber(draft.bankSellSpreadPct);
    const years = parseNumber(draft.undergraduateYears);

    const problems: string[] = [];
    if (fee === null) problems.push("Phí Visa Integrity Fee phải là số.");
    if (escalationPct === null) problems.push("Tỷ lệ tăng chi phí hằng năm phải là số.");
    if (spreadPct === null) problems.push("Biên độ giá bán ra ngân hàng phải là số.");
    if (years === null || !Number.isInteger(years)) problems.push("Số năm học bậc đại học phải là số nguyên.");
    if (problems.length > 0) {
      setErrors(problems);
      return;
    }

    setSaving(true);
    setErrors([]);
    setConflict(null);
    try {
      const result = await saveCostParameters(
        {
          visaIntegrityFeeUsd: fee!,
          annualEscalationRate: Math.round(escalationPct! * 100) / 10_000,
          bankSellRateSpread: Math.round(spreadPct! * 100) / 10_000,
          undergraduateYears: years!,
        },
        setting.version,
      );
      if (result.status === "ok") {
        showSetting(result.setting);
        router.refresh();
        toast.add({
          type: "success",
          priority: "high",
          timeout: 5000,
          title: "Đã lưu tham số ước tính chi phí",
          description: "Ước tính chi phí (UC-26) và trang Căn cứ tham số (UC-83) sẽ dùng giá trị mới ngay.",
        });
      } else if (result.status === "conflict") {
        setErrors(result.errors);
        setConflict(result.current);
      } else {
        setErrors(result.errors);
      }
    } catch {
      setErrors(["Không lưu được tham số ước tính chi phí. Vui lòng thử lại sau."]);
    } finally {
      setSaving(false);
    }
  };

  if (loadError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{loadError}</AlertDescription>
      </Alert>
    );
  }

  if (!setting || !draft) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Spinner /> Đang tải tham số ước tính chi phí...
      </div>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="text-h3">Tham số ước tính chi phí (UC-26)</h2>
        </CardTitle>
        <CardDescription>
          Các tham số dùng khi ước tính chi phí trường (UC-26) và hiển thị công khai ở trang Căn cứ tham số (UC-83).
          Khi chưa lưu trong hệ thống, hệ thống dùng giá trị mặc định.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center gap-2">
          {setting.isDefault ? (
            <Badge variant="secondary">Đang dùng giá trị mặc định, chưa lưu vào hệ thống</Badge>
          ) : (
            setting.version && <Badge variant="outline">Sửa lần cuối: {formatTime(setting.version)}</Badge>
          )}
          {!canEdit && <Badge variant="outline">Chỉ xem (Moderator mới sửa được)</Badge>}
        </div>

        {conflict && (
          <Alert variant="warning">
            <AlertTitle>Tham số vừa được người khác sửa</AlertTitle>
            <AlertDescription className="flex flex-col items-start gap-2">
              Thay đổi của bạn chưa được lưu. Tải giá trị mới nhất rồi sửa lại.
              <Button size="sm" variant="outline" onClick={() => showSetting(conflict)}>
                <RotateCcwIcon /> Tải giá trị mới nhất
              </Button>
            </AlertDescription>
          </Alert>
        )}

        {errors.length > 0 && !conflict && (
          <Alert variant="destructive">
            <AlertTitle>Chưa lưu được</AlertTitle>
            <AlertDescription>
              <ul className="list-disc pl-4">
                {errors.map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            </AlertDescription>
          </Alert>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">Phí Visa Integrity Fee (USD)</span>
            <Input
              inputMode="decimal"
              value={draft.visaIntegrityFeeUsd}
              onChange={(e) => setDraft({ ...draft, visaIntegrityFeeUsd: e.target.value })}
              disabled={saving || !canEdit}
            />
            <span className="text-xs text-muted-foreground">Mặc định: ${setting.defaults.visaIntegrityFeeUsd}</span>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">Tỷ lệ tăng chi phí hằng năm (%)</span>
            <Input
              inputMode="decimal"
              value={draft.annualEscalationPct}
              onChange={(e) => setDraft({ ...draft, annualEscalationPct: e.target.value })}
              disabled={saving || !canEdit}
            />
            <span className="text-xs text-muted-foreground">Mặc định: {formatPct(setting.defaults.annualEscalationRate)}%</span>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">Biên độ giá bán ra ngân hàng (%)</span>
            <Input
              inputMode="decimal"
              value={draft.bankSellSpreadPct}
              onChange={(e) => setDraft({ ...draft, bankSellSpreadPct: e.target.value })}
              disabled={saving || !canEdit}
            />
            <span className="text-xs text-muted-foreground">Mặc định: {formatPct(setting.defaults.bankSellRateSpread)}%</span>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">Số năm học bậc đại học (năm)</span>
            <Input
              inputMode="numeric"
              value={draft.undergraduateYears}
              onChange={(e) => setDraft({ ...draft, undergraduateYears: e.target.value })}
              disabled={saving || !canEdit}
            />
            <span className="text-xs text-muted-foreground">Mặc định: {setting.defaults.undergraduateYears} năm</span>
          </label>
        </div>

        {canEdit && (
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setDraft(toDraft(setting.defaults))} disabled={saving}>
            <ListRestartIcon /> Dùng giá trị mặc định
          </Button>
          <Button variant="ghost" onClick={() => showSetting(setting)} disabled={saving}>
            <RotateCcwIcon /> Bỏ thay đổi
          </Button>
          <Button onClick={() => void handleSave()} loading={saving}>
            <SaveIcon /> Lưu tham số
          </Button>
        </div>
        )}
      </CardContent>
    </Card>
  );
}
