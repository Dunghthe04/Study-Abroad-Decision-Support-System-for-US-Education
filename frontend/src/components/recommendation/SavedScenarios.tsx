"use client";

// [UC-86] Kịch bản what-if đã lưu: danh sách trên trang Gợi ý trường, mở lại, đổi tên, xóa.
// Chỉ lưu khi học sinh bấm "Lưu kịch bản" (không lưu mỗi lần kéo thanh). Tối đa 20 kịch bản, backend kiểm tra.

import { useCallback, useEffect, useState } from "react";
import { BookmarkIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { scenarioApi } from "@/lib/scenario-api";
import { describeSimulation } from "@/lib/simulation-link";
import type { GroupSummary, SavedScenario, SimulationOverrides } from "@/types/recommendation";

const MAX_TITLE = 256; // khớp ScenarioService.MaxTitleLength

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback);

export interface SavedScenariosStore {
  items: SavedScenario[];
  error: string | null;
  save: (title: string, overrides: SimulationOverrides) => Promise<void>; // lỗi thì ném ra để form lưu hiện
  rename: (id: string, title: string) => Promise<void>;
  remove: (id: string) => Promise<void>;
}

// Danh sách kịch bản của chính mình (hoặc của con, khi phụ huynh truyền studentId). enabled=false thì chưa tải.
export function useSavedScenarios(enabled: boolean, studentId?: string): SavedScenariosStore {
  const [items, setItems] = useState<SavedScenario[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled) return;
    let alive = true; // đổi học sinh giữa chừng thì bỏ phản hồi cũ
    // Chỉ gọi setState trong .then/.catch, không gọi ngay trong effect
    scenarioApi
      .list(studentId)
      .then((list) => {
        if (!alive) return;
        setItems(list);
        setError(null);
      })
      .catch((err: unknown) => {
        if (alive) setError(errorText(err, "Chưa tải được kịch bản đã lưu."));
      });
    return () => {
      alive = false;
    };
  }, [enabled, studentId]);

  const save = useCallback(async (title: string, overrides: SimulationOverrides) => {
    const saved = await scenarioApi.save(title, overrides);
    setItems((cur) => [saved, ...cur]); // mới nhất lên đầu, giống backend
  }, []);

  const rename = useCallback(async (id: string, title: string) => {
    const renamed = await scenarioApi.rename(id, title);
    setItems((cur) => cur.map((s) => (s.id === id ? renamed : s)));
  }, []);

  const remove = useCallback(async (id: string) => {
    await scenarioApi.remove(id);
    setItems((cur) => cur.filter((s) => s.id !== id));
  }, []);

  return { items, error, save, rename, remove };
}

// Giá trị giả định của một kịch bản đã lưu, dạng thanh kéo dùng được (bỏ các trường null)
export function scenarioOverrides(s: SavedScenario): SimulationOverrides {
  const all: SimulationOverrides = { ...s.overrides, pref: s.pref };
  return Object.fromEntries(Object.entries(all).filter(([, v]) => v !== null && v !== undefined)) as SimulationOverrides;
}

// Nút "Lưu kịch bản" trong dải "Đang xem giả định": bấm thì mở ô đặt tên, tên mặc định là mô tả giả định
export function SaveScenarioButton({ overrides, onSave }: { overrides: SimulationOverrides; onSave: (title: string) => Promise<void> }) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const start = () => {
    setTitle(describeSimulation(overrides).slice(0, MAX_TITLE));
    setError(null);
    setDone(false);
    setOpen(true);
  };

  const submit = async () => {
    if (!title.trim()) return setError("Hãy đặt tên cho kịch bản.");
    setBusy(true);
    try {
      await onSave(title.trim());
      setOpen(false);
      setDone(true);
    } catch (err) {
      setError(errorText(err, "Chưa lưu được kịch bản."));
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <Button variant="outline" size="sm" onClick={start} className="gap-1">
        <BookmarkIcon className="size-3.5" />
        {done ? "Đã lưu, lưu thêm" : "Lưu kịch bản"}
      </Button>
    );
  }

  return (
    <form
      className="flex w-full flex-wrap items-center gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      <Input
        value={title}
        maxLength={MAX_TITLE}
        onChange={(e) => setTitle(e.target.value)}
        aria-label="Tên kịch bản"
        autoFocus
        className="min-w-48 flex-1 bg-background"
      />
      <Button type="submit" size="sm" disabled={busy}>
        {busy ? "Đang lưu..." : "Lưu"}
      </Button>
      <Button type="button" variant="ghost" size="sm" onClick={() => setOpen(false)} disabled={busy}>
        Hủy
      </Button>
      {error && <p className="w-full text-xs text-destructive">{error}</p>}
    </form>
  );
}

const DATE = new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" });

// "Khó vào 10 → 7 · Vừa sức 12 → 14 · An toàn 8 → 9": kết quả lúc bấm lưu
function snapshotText(before: GroupSummary | null, after: GroupSummary | null): string | null {
  if (!before || !after) return null;
  const part = (label: string, a: number, b: number) => (a === b ? `${label} ${a}` : `${label} ${a} → ${b}`);
  return [
    part("Khó vào", before.reach, after.reach),
    part("Vừa sức", before.match, after.match),
    part("An toàn", before.safety, after.safety),
  ].join(" · ");
}

interface SavedScenariosProps {
  store: SavedScenariosStore;
  onOpen: (scenario: SavedScenario) => void;
  readOnly?: boolean; // phụ huynh xem: không đổi tên, không xóa
}

// Danh sách kịch bản đã lưu. Không có kịch bản nào thì ẩn hẳn (nút lưu nằm ở dải "Đang xem giả định").
export function SavedScenarios({ store, onOpen, readOnly = false }: SavedScenariosProps) {
  if (store.error) return <p className="text-xs text-destructive">{store.error}</p>;
  if (store.items.length === 0) return null;

  return (
    <details className="rounded-2xl border border-line bg-surface p-4" open>
      <summary className="cursor-pointer text-sm font-semibold text-foreground">
        Kịch bản đã lưu ({store.items.length})
      </summary>
      <p className="mt-1 text-xs text-muted-foreground">
        Số trường mỗi nhóm là kết quả lúc lưu. Bấm &quot;Mở&quot; để tính lại theo hồ sơ hiện tại.
      </p>
      <ul className="mt-3 divide-y divide-line">
        {store.items.map((s) => (
          <ScenarioRow key={s.id} scenario={s} store={store} onOpen={onOpen} readOnly={readOnly} />
        ))}
      </ul>
    </details>
  );
}

function ScenarioRow({ scenario, store, onOpen, readOnly }: { scenario: SavedScenario } & Required<SavedScenariosProps>) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(scenario.title);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (action: () => Promise<void>, fallback: string) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      return true;
    } catch (err) {
      setError(errorText(err, fallback));
      return false;
    } finally {
      setBusy(false);
    }
  };

  const rename = async () => {
    if (!title.trim()) return setError("Hãy đặt tên cho kịch bản.");
    if (await run(() => store.rename(scenario.id, title.trim()), "Chưa đổi được tên.")) setEditing(false);
  };

  const remove = () => {
    if (!window.confirm(`Xóa kịch bản "${scenario.title}"? Không khôi phục lại được.`)) return;
    void run(() => store.remove(scenario.id), "Chưa xóa được kịch bản.");
  };

  const snapshot = snapshotText(scenario.baseline, scenario.simulated);
  const values = describeSimulation(scenarioOverrides(scenario));

  return (
    <li className="space-y-1 py-3 first:pt-0 last:pb-0">
      {editing ? (
        <form
          className="flex flex-wrap items-center gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            void rename();
          }}
        >
          <Input
            value={title}
            maxLength={MAX_TITLE}
            onChange={(e) => setTitle(e.target.value)}
            aria-label="Tên mới của kịch bản"
            autoFocus
            className="min-w-48 flex-1"
          />
          <Button type="submit" size="sm" disabled={busy}>
            Lưu tên
          </Button>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={busy}
            onClick={() => {
              setTitle(scenario.title);
              setEditing(false);
              setError(null);
            }}
          >
            Hủy
          </Button>
        </form>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-foreground">{scenario.title}</p>
            <p className="text-[11px] text-ink-3">
              Lưu lúc {DATE.format(new Date(scenario.createdAt))}
              {scenario.updatedAt && ` · đổi tên lúc ${DATE.format(new Date(scenario.updatedAt))}`}
            </p>
          </div>
          <div className="flex items-center gap-1">
            <Button size="sm" variant="outline" onClick={() => onOpen(scenario)} disabled={busy}>
              Mở
            </Button>
            {!readOnly && (
              <>
                <Button size="icon-sm" variant="ghost" onClick={() => setEditing(true)} disabled={busy} aria-label="Đổi tên">
                  <PencilIcon className="size-3.5" />
                </Button>
                <Button size="icon-sm" variant="ghost" onClick={remove} disabled={busy} aria-label="Xóa">
                  <Trash2Icon className="size-3.5 text-destructive" />
                </Button>
              </>
            )}
          </div>
        </div>
      )}
      {values && <p className="text-xs text-muted-foreground">{values}</p>}
      {snapshot && <p className="text-xs text-muted-foreground">Lúc lưu: {snapshot}</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
    </li>
  );
}
