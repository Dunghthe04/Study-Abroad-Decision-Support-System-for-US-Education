"use client";

// [UC-86] Kịch bản what-if đã lưu: popup đặt tên khi lưu, popup danh sách để mở lại, đổi tên, xóa.
// Chỉ lưu khi học sinh bấm "Lưu kịch bản" (không lưu mỗi lần kéo thanh). Tối đa 20 kịch bản, backend kiểm tra.

import { useCallback, useEffect, useState } from "react";
import { BookmarkIcon, HistoryIcon, PencilIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { scenarioApi } from "@/lib/scenario-api";
import { describeSimulation } from "@/lib/simulation-link";
import type { GroupSummary, SavedScenario, SimulationOverrides } from "@/types/recommendation";

const MAX_TITLE = 256; // khớp ScenarioService.MaxTitleLength

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback);

export interface SavedScenariosStore {
  items: SavedScenario[];
  error: string | null;
  save: (title: string, overrides: SimulationOverrides) => Promise<void>; // lỗi thì ném ra để popup hiện
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

// Nút "Lưu kịch bản": bấm thì hiện popup bắt đặt tên, tên gợi ý sẵn là mô tả giả định
export function SaveScenarioButton({ overrides, onSave }: { overrides: SimulationOverrides; onSave: (title: string) => Promise<void> }) {
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const start = () => {
    setTitle(describeSimulation(overrides).slice(0, MAX_TITLE));
    setError(null);
    setOpen(true);
  };

  const submit = async () => {
    if (!title.trim()) return setError("Hãy đặt tên cho kịch bản.");
    setBusy(true);
    setError(null);
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

  return (
    <>
      <Button variant="outline" size="sm" onClick={start} className="gap-1">
        <BookmarkIcon className="size-3.5" />
        {done ? "Đã lưu, lưu thêm" : "Lưu kịch bản"}
      </Button>

      <Dialog open={open} onOpenChange={(next) => !busy && setOpen(next)}>
        <DialogContent>
          <form
            className="grid gap-4"
            onSubmit={(e) => {
              e.preventDefault();
              void submit();
            }}
          >
            <DialogHeader>
              <DialogTitle>Lưu kịch bản</DialogTitle>
              <DialogDescription>Đặt tên để lần sau mở lại đúng các giá trị đang giả định.</DialogDescription>
            </DialogHeader>
            <div className="space-y-1.5">
              <Input
                value={title}
                maxLength={MAX_TITLE}
                onChange={(e) => setTitle(e.target.value)}
                aria-label="Tên kịch bản"
                placeholder="Ví dụ: Tập trung nâng SAT"
                autoFocus
              />
              <p className="text-xs text-muted-foreground">Đang giả định: {describeSimulation(overrides)}</p>
              {error && <p className="text-xs text-destructive">{error}</p>}
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={busy}>
                Hủy
              </Button>
              <Button type="submit" disabled={busy}>
                {busy ? "Đang lưu..." : "Lưu"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
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

// Nút "Kịch bản đã lưu (n)": bấm thì hiện popup danh sách. Xóa thì hiện thêm popup xác nhận.
export function SavedScenariosButton({ store, onOpen, readOnly = false }: SavedScenariosProps) {
  const [open, setOpen] = useState(false);
  const [deleting, setDeleting] = useState<SavedScenario | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const openScenario = (s: SavedScenario) => {
    setOpen(false);
    onOpen(s);
  };

  const askDelete = (s: SavedScenario) => {
    setDeleteError(null);
    setDeleting(s);
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    setDeleteBusy(true);
    setDeleteError(null);
    try {
      await store.remove(deleting.id);
      setDeleting(null);
    } catch (err) {
      setDeleteError(errorText(err, "Chưa xóa được kịch bản."));
    } finally {
      setDeleteBusy(false);
    }
  };

  return (
    <>
      <Button variant="outline" size="sm" onClick={() => setOpen(true)} className="gap-1">
        <HistoryIcon className="size-3.5" />
        Kịch bản đã lưu ({store.items.length})
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Kịch bản đã lưu</DialogTitle>
            <DialogDescription>
              Số trường mỗi nhóm là kết quả lúc lưu. Bấm &quot;Mở&quot; để tính lại theo hồ sơ hiện tại.
            </DialogDescription>
          </DialogHeader>

          {store.error ? (
            <p className="text-sm text-destructive">{store.error}</p>
          ) : store.items.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">
              Chưa có kịch bản nào. Kéo các thanh mô phỏng rồi bấm &quot;Lưu kịch bản&quot;.
            </p>
          ) : (
            <ul className="-mx-4 max-h-[60vh] divide-y divide-line overflow-y-auto px-4">
              {store.items.map((s) => (
                <ScenarioRow
                  key={s.id}
                  scenario={s}
                  onRename={store.rename}
                  onOpen={openScenario}
                  onDelete={askDelete}
                  readOnly={readOnly}
                />
              ))}
            </ul>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={deleting !== null} onOpenChange={(next) => !next && !deleteBusy && setDeleting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Xóa kịch bản?</DialogTitle>
            <DialogDescription>
              Kịch bản &quot;{deleting?.title}&quot; sẽ bị xóa và không khôi phục lại được.
            </DialogDescription>
          </DialogHeader>
          {deleteError && <p className="text-xs text-destructive">{deleteError}</p>}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setDeleting(null)} disabled={deleteBusy}>
              Không
            </Button>
            <Button type="button" variant="destructive" onClick={() => void confirmDelete()} disabled={deleteBusy}>
              {deleteBusy ? "Đang xóa..." : "Xóa"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

interface ScenarioRowProps {
  scenario: SavedScenario;
  onRename: (id: string, title: string) => Promise<void>;
  onOpen: (scenario: SavedScenario) => void;
  onDelete: (scenario: SavedScenario) => void;
  readOnly: boolean;
}

function ScenarioRow({ scenario, onRename, onOpen, onDelete, readOnly }: ScenarioRowProps) {
  const [editing, setEditing] = useState(false);
  const [title, setTitle] = useState(scenario.title);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const rename = async () => {
    if (!title.trim()) return setError("Hãy đặt tên cho kịch bản.");
    setBusy(true);
    setError(null);
    try {
      await onRename(scenario.id, title.trim());
      setEditing(false);
    } catch (err) {
      setError(errorText(err, "Chưa đổi được tên."));
    } finally {
      setBusy(false);
    }
  };

  const cancelRename = () => {
    setTitle(scenario.title);
    setEditing(false);
    setError(null);
  };

  const snapshot = snapshotText(scenario.baseline, scenario.simulated);
  const values = describeSimulation(scenarioOverrides(scenario));

  return (
    <li className="space-y-1 py-3">
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
            className="min-w-40 flex-1"
          />
          <Button type="submit" size="sm" disabled={busy}>
            Lưu tên
          </Button>
          <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={cancelRename}>
            Hủy
          </Button>
        </form>
      ) : (
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-foreground">{scenario.title}</p>
            <p className="text-xs text-ink-3">
              Lưu lúc {DATE.format(new Date(scenario.createdAt))}
              {scenario.updatedAt && ` · đổi tên lúc ${DATE.format(new Date(scenario.updatedAt))}`}
            </p>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <Button size="sm" onClick={() => onOpen(scenario)}>
              Mở
            </Button>
            {!readOnly && (
              <>
                <Button size="icon-sm" variant="ghost" onClick={() => setEditing(true)} aria-label="Đổi tên">
                  <PencilIcon className="size-3.5" />
                </Button>
                <Button size="icon-sm" variant="ghost" onClick={() => onDelete(scenario)} aria-label="Xóa">
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
