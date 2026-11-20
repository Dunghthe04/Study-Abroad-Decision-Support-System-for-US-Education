"use client";

// Nút "Gợi ý cho con" thay cho "Lưu mục tiêu" khi người dùng là phụ huynh (phụ huynh không có danh sách mục tiêu riêng).
// Hộp thoại hỏi gợi ý trường cho con nào (từ 2 con có ô "Tất cả các con"), kèm lời nhắn chung.
// Con đã có trường này hoặc đang chờ xem gợi ý thì ô bị khóa, ghi rõ lý do.

import Link from "next/link";
import { useState } from "react";
import { SendIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api";
import { parentChildrenApi } from "@/lib/parent-children-api";
import { cn } from "@/lib/utils";

const MAX_NOTE = 1000; // cùng giới hạn với backend

type ChildOption = {
  studentId: string;
  name: string;
  /** Lý do không gợi ý được nữa; null là chọn được */
  blocked: string | null;
};

export function SuggestToChildrenButton({
  universityId,
  universityCode,
  universityName,
  variant = "outline",
  size = "default",
  className,
}: {
  universityId?: string;
  universityCode?: string;
  universityName: string;
  variant?: "default" | "outline" | "secondary" | "ghost";
  size?: "default" | "sm" | "lg" | "icon";
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const [children, setChildren] = useState<ChildOption[] | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [picked, setPicked] = useState<string[]>([]);
  const [note, setNote] = useState("");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const sameSchool = (t: { universityId: string; universityCode: string }) =>
    (universityId != null && t.universityId === universityId) ||
    (universityCode != null && t.universityCode.toLowerCase() === universityCode.toLowerCase());

  // Mỗi lần mở hộp thoại thì tải lại các con và trạng thái trường này ở từng con
  const load = async () => {
    setChildren(null);
    setLoadError(false);
    try {
      const list = await parentChildrenApi.listChildren();
      const options = await Promise.all(
        list.map(async (c): Promise<ChildOption> => {
          const targets = await parentChildrenApi.targetSchools(c.studentUserId);
          const found = targets.find(sameSchool);
          const blocked = !found
            ? null
            : found.status === "parent_suggested"
              ? "Đang chờ con xem"
              : "Đã có trong danh sách";
          return { studentId: c.studentUserId, name: c.studentName, blocked };
        }),
      );
      setChildren(options);
      const free = options.filter((o) => !o.blocked);
      setPicked(options.length === 1 && free.length === 1 ? [free[0].studentId] : []);
    } catch {
      setLoadError(true);
    }
  };

  const openDialog = () => {
    setNote("");
    setError(null);
    setPicked([]);
    setOpen(true);
    void load();
  };

  const free = (children ?? []).filter((c) => !c.blocked);
  const allPicked = free.length > 0 && free.every((c) => picked.includes(c.studentId));

  const toggle = (studentId: string, checked: boolean) =>
    setPicked((p) => (checked ? [...p, studentId] : p.filter((id) => id !== studentId)));

  const submit = async () => {
    if (picked.length === 0) return;
    setSending(true);
    setError(null);
    const body = {
      universityId: universityId || undefined,
      universityCode: universityId ? undefined : universityCode,
      note: note.trim() || undefined,
    };
    const done: string[] = [];
    const failed: string[] = [];
    for (const studentId of picked) {
      const name = children?.find((c) => c.studentId === studentId)?.name ?? "";
      try {
        await parentChildrenApi.suggestTargetSchool(studentId, body);
        done.push(name);
      } catch (err) {
        failed.push(err instanceof ApiError ? `${name}: ${err.message}` : name);
      }
    }
    setSending(false);
    if (done.length > 0) {
      toast.add({
        type: "success",
        title: `Đã gợi ý cho ${done.join(", ")}`,
        description: `Con sẽ thấy ${universityName} ở trang Trường mục tiêu.`,
      });
    }
    if (failed.length > 0) {
      setError(`Chưa gửi được: ${failed.join("; ")}`);
      void load();
    } else {
      setOpen(false);
    }
  };

  return (
    <>
      <Button
        variant={variant}
        size={size}
        onClick={openDialog}
        className={cn("gap-2 hover:border-brand/40 hover:bg-brand-wash hover:text-brand-ink", className)}
      >
        <SendIcon className="size-4 text-brand" />
        <span>Gợi ý cho con</span>
      </Button>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle className="text-lg font-bold">Gợi ý {universityName} cho con</DialogTitle>
            <DialogDescription>
              Trường bạn gợi ý chưa vào danh sách của con. Con sẽ chọn thêm vào danh sách hoặc bỏ qua.
            </DialogDescription>
          </DialogHeader>

          {loadError ? (
            <p className="text-sm text-destructive">Chưa tải được danh sách con. Vui lòng thử lại.</p>
          ) : children === null ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Spinner /> Đang tải danh sách con...
            </div>
          ) : children.length === 0 ? (
            <div className="space-y-3 text-sm">
              <p className="text-foreground">Bạn cần liên kết với con trước khi gợi ý trường.</p>
              <Button nativeButton={false} render={<Link href="/account" />} size="sm">
                Mở Tài khoản để liên kết
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="space-y-1 rounded-xl border border-line p-2">
                {children.length >= 2 && (
                  <label className="flex cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 text-sm font-semibold hover:bg-muted">
                    <Checkbox
                      checked={allPicked}
                      disabled={free.length === 0}
                      onCheckedChange={(checked) => setPicked(checked ? free.map((c) => c.studentId) : [])}
                    />
                    Tất cả các con
                  </label>
                )}
                {children.map((c) => (
                  <label
                    key={c.studentId}
                    className={cn(
                      "flex items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-sm",
                      c.blocked ? "text-muted-foreground" : "cursor-pointer hover:bg-muted",
                      children.length >= 2 && "pl-6",
                    )}
                  >
                    <span className="flex items-center gap-2.5">
                      <Checkbox
                        checked={picked.includes(c.studentId)}
                        disabled={Boolean(c.blocked)}
                        onCheckedChange={(checked) => toggle(c.studentId, checked)}
                      />
                      {c.name}
                    </span>
                    {c.blocked && <span className="text-xs">{c.blocked}</span>}
                  </label>
                ))}
              </div>

              <Textarea
                value={note}
                onChange={(e) => setNote(e.target.value.slice(0, MAX_NOTE))}
                placeholder="Lời nhắn cho con (không bắt buộc), ví dụ lý do bạn thích trường này"
                aria-label="Lời nhắn cho con"
                rows={3}
                className="resize-none text-sm"
              />
            </div>
          )}

          {error && <p className="text-xs text-destructive">{error}</p>}

          {children !== null && children.length > 0 && (
            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={sending}>
                Hủy
              </Button>
              <Button type="button" onClick={submit} disabled={sending || picked.length === 0} className="gap-2">
                {sending && <Spinner />}
                Gửi gợi ý
              </Button>
            </DialogFooter>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
