"use client";

import { useCallback, useEffect, useState } from "react";
import { ListRestartIcon, PlusIcon, RotateCcwIcon, SaveIcon, Trash2Icon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import {
  getAdminGradeScale,
  saveGradeScale,
  type GradeScaleSetting,
} from "@/lib/admin-grade-scale-api";
import { setGradeScaleRules } from "@/lib/profile-flow-options";
import type { GradeScaleConfig } from "@/types/academic";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { toast } from "@/components/ui/toast";
import { formatTime, GradeScaleHistoryDialog } from "./GradeScaleHistoryDialog";

// Một dòng đang sửa: giữ chữ người nhập (ô số để trống hoặc gõ dở vẫn hiện đúng), lưu mới đổi sang số
interface RowDraft {
  key: number;
  minScore: string;
  gpa4: string;
  letterGrade: string;
}

let nextKey = 0;

function toRows(config: GradeScaleConfig): RowDraft[] {
  return [...config.rules]
    .sort((a, b) => b.minScore - a.minScore)
    .map((r) => ({ key: nextKey++, minScore: String(r.minScore), gpa4: String(r.gpa4), letterGrade: r.letterGrade ?? "" }));
}

function parseNumber(text: string): number | null {
  const value = Number(text.trim().replace(",", "."));
  return text.trim() === "" || Number.isNaN(value) ? null : value;
}

// Khoảng điểm hiện bên cạnh: từ mốc này tới mốc kế trên − 0.01 (mốc cao nhất tới 10), giống cách server tính MaxScore
function rangeLabels(rows: RowDraft[]): Map<number, string> {
  const sorted = rows
    .map((r) => ({ key: r.key, min: parseNumber(r.minScore) }))
    .filter((r): r is { key: number; min: number } => r.min !== null)
    .sort((a, b) => a.min - b.min);
  const labels = new Map<number, string>();
  sorted.forEach((r, i) => {
    const max = i + 1 < sorted.length ? sorted[i + 1].min - 0.01 : 10;
    labels.set(r.key, `${r.min.toFixed(2)} – ${Math.max(max, r.min).toFixed(2)}`);
  });
  return labels;
}

/**
 * [UC-68] Bảng quy đổi điểm thang 10 sang GPA thang 4 (grade_scale.10).
 * Moderator sửa và lưu; admin chỉ xem bảng và lịch sử. Lưu xong server tính lại GPA các hồ sơ lấy từ học bạ.
 */
export function GradeScaleSettings() {
  const { user } = useAuth();
  const canEdit = user?.role === "moderator";

  const [setting, setSetting] = useState<GradeScaleSetting | null>(null);
  const [rows, setRows] = useState<RowDraft[]>([]);
  const [sourceName, setSourceName] = useState("");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const [conflict, setConflict] = useState<GradeScaleSetting | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const showSetting = useCallback((value: GradeScaleSetting) => {
    setSetting(value);
    setRows(toRows(value.config));
    setSourceName(value.config.sourceName);
    setErrors([]);
    setConflict(null);
  }, []);

  useEffect(() => {
    let cancelled = false;
    getAdminGradeScale()
      .then((value) => {
        if (!cancelled) showSetting(value);
      })
      .catch(() => {
        if (!cancelled) setLoadError("Không tải được bảng quy đổi. Vui lòng thử lại sau.");
      });
    return () => {
      cancelled = true;
    };
  }, [showSetting]);

  const updateRow = (key: number, field: keyof Omit<RowDraft, "key">, value: string) =>
    setRows((prev) => prev.map((r) => (r.key === key ? { ...r, [field]: value } : r)));

  const addRow = () => setRows((prev) => [...prev, { key: nextKey++, minScore: "", gpa4: "", letterGrade: "" }]);

  const removeRow = (key: number) => setRows((prev) => prev.filter((r) => r.key !== key));

  // Điền mốc mặc định (server gửi kèm) vào bảng đang sửa; chưa lưu, mod vẫn bấm "Lưu bảng" để đi đường lưu thường
  // (tính lại GPA hồ sơ học bạ + ghi lịch sử)
  const applyDefault = () => {
    if (!setting) return;
    setRows(toRows(setting.defaultConfig));
    setSourceName(setting.defaultConfig.sourceName);
    setErrors([]);
  };

  // Chỉ kiểm ô bỏ trống / không phải số; các luật còn lại (0–10, 0–4, trùng mốc, GPA giảm dần) server kiểm và trả lỗi
  const checkInput = (): string[] => {
    const problems: string[] = [];
    if (rows.length === 0) problems.push("Bảng phải có ít nhất một mốc điểm.");
    rows.forEach((r, i) => {
      if (parseNumber(r.minScore) === null) problems.push(`Dòng ${i + 1}: mốc điểm phải là số.`);
      if (parseNumber(r.gpa4) === null) problems.push(`Dòng ${i + 1}: GPA phải là số.`);
    });
    return problems;
  };

  const askSave = () => {
    const problems = checkInput();
    setErrors(problems);
    if (problems.length === 0) setConfirmOpen(true);
  };

  const save = async () => {
    if (!setting) return;
    setSaving(true);
    try {
      const rules = rows.map((r) => ({
        minScore: parseNumber(r.minScore) ?? 0,
        gpa4: parseNumber(r.gpa4) ?? 0,
        letterGrade: r.letterGrade.trim(),
      }));
      const result = await saveGradeScale(rules, sourceName.trim(), setting.version);
      if (result.status === "ok") {
        showSetting(result.setting);
        setGradeScaleRules(result.setting.config.rules); // trang khác trong tab này tính GPA theo bảng mới luôn
        toast.add({
          type: "success",
          priority: "high",
          timeout: 6000,
          title: "Đã lưu bảng quy đổi",
          description:
            result.recomputedProfiles > 0
              ? `Đã tính lại GPA cho ${result.recomputedProfiles} hồ sơ lấy GPA từ học bạ.`
              : "Không có hồ sơ nào cần tính lại GPA.",
        });
      } else if (result.status === "conflict") {
        setErrors(result.errors);
        setConflict(result.current);
      } else {
        setErrors(result.errors);
      }
    } catch {
      setErrors(["Không lưu được bảng quy đổi. Vui lòng thử lại sau."]);
    } finally {
      setSaving(false);
      setConfirmOpen(false);
    }
  };

  if (loadError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{loadError}</AlertDescription>
      </Alert>
    );
  }

  if (!setting) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Spinner /> Đang tải bảng quy đổi...
      </div>
    );
  }

  const ranges = rangeLabels(rows);

  return (
    <div className="flex flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle>
            <h2 className="text-h3">Bảng quy đổi điểm thang 10 sang GPA thang 4</h2>
          </CardTitle>
          <CardDescription>
            Chỉ điểm trung bình tổng mới quy sang thang 4; điểm từng môn giữ nguyên thang 10. Điểm nằm giữa hai mốc thì
            nội suy tuyến tính (mốc 6 → 3.0 và 8 → 3.5 thì 7 → 3.25). Điểm chữ không bắt buộc.
            {canEdit
              ? " Lưu bảng mới sẽ tính lại GPA của các hồ sơ lấy GPA từ học bạ; hồ sơ tự nhập GPA giữ nguyên."
              : " Quản trị viên chỉ xem, Moderator là người sửa bảng."}
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center gap-2">
            {setting.isDefault ? (
              <Badge variant="secondary">Đang dùng bảng mặc định, chưa lưu vào hệ thống</Badge>
            ) : (
              setting.version && <Badge variant="outline">Sửa lần cuối: {formatTime(setting.version)}</Badge>
            )}
            {!canEdit && <Badge variant="outline">Chỉ xem</Badge>}
            <span className="ml-auto">
              <GradeScaleHistoryDialog />
            </span>
          </div>

          <label className="flex max-w-xl flex-col gap-1 text-sm">
            <span className="font-medium">Nguồn bảng</span>
            {canEdit ? (
              <Input value={sourceName} onChange={(e) => setSourceName(e.target.value)} maxLength={200} />
            ) : (
              <span className="text-muted-foreground">{sourceName}</span>
            )}
          </label>

          {conflict && (
            <Alert variant="warning">
              <AlertTitle>Bảng vừa được người khác sửa</AlertTitle>
              <AlertDescription className="flex flex-col items-start gap-2">
                Thay đổi của bạn chưa được lưu. Tải bảng mới nhất rồi sửa lại.
                <Button size="sm" variant="outline" onClick={() => showSetting(conflict)}>
                  <RotateCcwIcon /> Tải bảng mới nhất
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

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Từ điểm (thang 10)</TableHead>
                <TableHead>Khoảng điểm</TableHead>
                <TableHead>GPA thang 4</TableHead>
                <TableHead>Điểm chữ</TableHead>
                {canEdit && <TableHead className="w-12" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={r.key}>
                  <TableCell>
                    {canEdit ? (
                      <Input
                        inputMode="decimal"
                        aria-label="Từ điểm"
                        className="w-24"
                        value={r.minScore}
                        onChange={(e) => updateRow(r.key, "minScore", e.target.value)}
                      />
                    ) : (
                      r.minScore
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{ranges.get(r.key) ?? "—"}</TableCell>
                  <TableCell>
                    {canEdit ? (
                      <Input
                        inputMode="decimal"
                        aria-label="GPA thang 4"
                        className="w-24"
                        value={r.gpa4}
                        onChange={(e) => updateRow(r.key, "gpa4", e.target.value)}
                      />
                    ) : (
                      r.gpa4
                    )}
                  </TableCell>
                  <TableCell>
                    {canEdit ? (
                      <Input
                        aria-label="Điểm chữ"
                        className="w-20"
                        maxLength={4}
                        value={r.letterGrade}
                        onChange={(e) => updateRow(r.key, "letterGrade", e.target.value)}
                      />
                    ) : (
                      r.letterGrade
                    )}
                  </TableCell>
                  {canEdit && (
                    <TableCell>
                      <Button
                        size="icon-sm"
                        variant="ghost"
                        aria-label={`Xóa mốc ${r.minScore}`}
                        onClick={() => removeRow(r.key)}
                      >
                        <Trash2Icon />
                      </Button>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {canEdit && (
            <div className="flex flex-wrap gap-2">
              <Button variant="outline" onClick={addRow}>
                <PlusIcon /> Thêm mốc
              </Button>
              <Button variant="outline" onClick={applyDefault} disabled={saving}>
                <ListRestartIcon /> Dùng bảng mặc định (EducationUSA THPT)
              </Button>
              <Button variant="ghost" onClick={() => showSetting(setting)} disabled={saving}>
                <RotateCcwIcon /> Bỏ thay đổi
              </Button>
              <Button onClick={askSave} disabled={saving}>
                {saving ? <Spinner /> : <SaveIcon />} Lưu bảng
              </Button>
            </div>
          )}
        </CardContent>
      </Card>


      <AlertDialog open={confirmOpen} onOpenChange={(open) => !saving && setConfirmOpen(open)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Lưu bảng quy đổi mới?</AlertDialogTitle>
            <AlertDialogDescription>
              Từ lúc lưu, mọi chỗ tính GPA (form hồ sơ, đọc học bạ, chat tư vấn) dùng bảng này. GPA của các hồ sơ lấy
              từ học bạ sẽ được tính lại; hồ sơ tự nhập GPA giữ nguyên.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={saving}>Hủy</AlertDialogCancel>
            <AlertDialogAction onClick={() => void save()} disabled={saving}>
              {saving && <Spinner />} Lưu
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
