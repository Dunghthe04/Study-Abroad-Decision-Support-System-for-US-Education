"use client";

import { useState } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { ComparisonPanel } from "@/components/admin/sensitivity/ComparisonPanel";
import { RunForm } from "@/components/admin/sensitivity/RunForm";
import { RunHistory } from "@/components/admin/sensitivity/RunHistory";
import { RunResult } from "@/components/admin/sensitivity/RunResult";
import { ApiError } from "@/lib/api";
import { adminSensitivityApi } from "@/lib/sensitivity-api";
import { useFetched } from "@/lib/use-fetched";
import type { SensitivityRequest, SensitivityRunDetail } from "@/types/sensitivity";

const messageOf = (error: unknown) =>
  error instanceof ApiError || error instanceof Error ? error.message : "Đã có lỗi xảy ra. Bạn thử lại nhé.";

/**
 * Trang admin: phân tích độ nhạy tham số thuật toán gợi ý (UC-85). Chỉ admin.
 * Thứ tự: chạy mới → kết quả lần đang xem → so sánh (khi bấm) → lịch sử.
 */
export function SensitivityView() {
  const parameters = useFetched("sensitivity-parameters", () => adminSensitivityApi.parameters());
  const [historyVersion, setHistoryVersion] = useState(0);
  const history = useFetched(`sensitivity-runs|${historyVersion}`, () => adminSensitivityApi.list());

  // Chưa chọn thì xem lần chạy mới nhất
  const [pickedId, setPickedId] = useState<string | null>(null);
  const selectedId = pickedId ?? history.data?.[0]?.id ?? null;
  // Kết quả vừa chạy: hiện ngay, không đợi tải lại
  const [justRun, setJustRun] = useState<SensitivityRunDetail | null>(null);
  const detail = useFetched(`sensitivity-run|${selectedId ?? ""}`, () =>
    selectedId ? adminSensitivityApi.get(selectedId) : Promise.resolve(null));
  const shown = justRun && justRun.summary.id === selectedId ? justRun : detail.data?.summary.id === selectedId ? detail.data : null;

  // So sánh chỉ hiện khi đúng lần đang xem; withId null là so với lần ngay trước
  const [compare, setCompare] = useState<{ id: string; withId: string | null } | null>(null);
  const activeCompare = compare && compare.id === selectedId ? compare : null;
  const comparison = useFetched(
    `sensitivity-compare|${activeCompare?.id ?? ""}|${activeCompare?.withId ?? ""}`,
    () => (activeCompare ? adminSensitivityApi.compare(activeCompare.id, activeCompare.withId) : Promise.resolve(null)),
  );

  const [running, setRunning] = useState(false);
  const [runError, setRunError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string | null>(null);

  const run = async (request: SensitivityRequest) => {
    setRunning(true);
    setRunError(null);
    try {
      const result = await adminSensitivityApi.run(request);
      setJustRun(result);
      setPickedId(result.summary.id);
      setCompare(null);
      setHistoryVersion((v) => v + 1);
    } catch (error) {
      setRunError(messageOf(error));
    } finally {
      setRunning(false);
    }
  };

  const download = async () => {
    if (!selectedId) return;
    setDownloading(true);
    setDownloadError(null);
    try {
      await adminSensitivityApi.downloadCsv(selectedId);
    } catch (error) {
      setDownloadError(messageOf(error));
    } finally {
      setDownloading(false);
    }
  };

  const select = (id: string) => {
    setPickedId(id);
    setCompare(null);
    setDownloadError(null);
  };

  return (
    <div className="flex flex-col gap-4">
      <header>
        <h1 className="text-h2 text-ink">Phân tích độ nhạy tham số</h1>
        <p className="mt-1 max-w-3xl text-sm text-ink-2">
          Thuật toán gợi ý trường do nhóm tự xây dựng nên các con số như ngưỡng 0,75 không có chuẩn chung. Trang này cho biết nếu một con
          số lệch đi 10% hoặc 20% thì kết quả gợi ý thay đổi bao nhiêu, để biết tham số nào cần theo dõi kỹ.
        </p>
      </header>

      {parameters.error ? <Alert variant="destructive"><AlertDescription>{parameters.error}</AlertDescription></Alert> : null}
      <RunForm parameters={parameters.data} running={running} onRun={(request) => void run(request)} />
      {runError ? <Alert variant="destructive"><AlertDescription>{runError}</AlertDescription></Alert> : null}

      {detail.error && !shown ? <Alert variant="destructive"><AlertDescription>{detail.error}</AlertDescription></Alert> : null}
      {downloadError ? <Alert variant="destructive"><AlertDescription>{downloadError}</AlertDescription></Alert> : null}
      {shown ? (
        <RunResult
          run={shown}
          loading={false}
          downloading={downloading}
          onCompare={() => setCompare({ id: shown.summary.id, withId: null })}
          onDownload={() => void download()}
        />
      ) : null}

      {activeCompare ? (
        <ComparisonPanel
          comparison={comparison.data}
          loading={comparison.loading}
          error={comparison.error}
          onClose={() => setCompare(null)}
        />
      ) : null}

      {history.error ? <Alert variant="destructive"><AlertDescription>{history.error}</AlertDescription></Alert> : null}
      <RunHistory
        runs={history.data}
        loading={history.loading}
        selectedId={selectedId}
        onSelect={select}
        onCompareWith={(id) => selectedId && setCompare({ id: selectedId, withId: id })}
      />
    </div>
  );
}
