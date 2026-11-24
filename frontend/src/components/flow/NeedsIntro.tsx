"use client";

import { useEffect, useId, useState } from "react";
import { ClipboardCheckIcon, GraduationCapIcon, PencilLineIcon, RouteIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { EDIT_ONLY, FORM_GOAL_OPTIONS, toggleGoal, type FormGoal } from "@/lib/form-goals";
import { CHOICE_CARD, CheckIndicator } from "./FormKit";

interface NeedsIntroProps {
  /** start: màn đầu tiên khi vào form; edit: sửa nhu cầu giữa chừng (Esc để quay lại) */
  mode: "start" | "edit";
  initialGoals: FormGoal[];
  /** Bước đang làm dở của nháp (null = chưa có nháp) */
  draftStep: number | null;
  /** "Chỉ sửa hồ sơ" chỉ có nghĩa khi đã có hồ sơ */
  hasCommittedProfile: boolean;
  onConfirm: (goals: FormGoal[]) => void;
  onCancel?: () => void;
}

export function NeedsIntro({ mode, initialGoals, draftStep, hasCommittedProfile, onConfirm, onCancel }: NeedsIntroProps) {
  const [selected, setSelected] = useState<FormGoal[]>(initialGoals);
  const [error, setError] = useState(false);
  const labelId = useId();
  const options = FORM_GOAL_OPTIONS.filter((o) => o.value !== EDIT_ONLY || hasCommittedProfile || selected.includes(EDIT_ONLY));

  const lede =
    mode === "start" && draftStep
      ? `Bạn đang làm dở ở bước ${draftStep}. Chọn nhu cầu để làm tiếp.`
      : "Chọn một hoặc nhiều việc bạn cần. Bạn đổi lại được bất cứ lúc nào.";
  const cta = mode === "edit" ? "Tiếp tục" : draftStep ? "Làm tiếp" : "Bắt đầu";

  const confirm = () => {
    if (selected.length === 0) {
      setError(true);
      return;
    }
    onConfirm(selected);
  };

  // Sửa nhu cầu: không có nút "Huỷ" riêng — bấm lưu mà không đổi gì là giữ nguyên; Esc để quay lại
  useEffect(() => {
    if (mode !== "edit" || !onCancel) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onCancel();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [mode, onCancel]);

  return (
    <div
      data-flow-motion
      className="flex min-h-[calc(100dvh-var(--header-h))] items-center justify-center bg-(--surface-2) px-4 py-12"
    >
      <div className="w-full max-w-[720px] space-y-8">
        <div className="space-y-3">
          <h1 id={labelId} className="font-heading text-4xl leading-tight text-foreground sm:text-5xl">
            Nhu cầu của bạn là
          </h1>
          <p className="text-base text-ink-2 sm:text-lg">{lede}</p>
        </div>
        <div role="group" aria-labelledby={labelId} className="space-y-3">
          {options.map((opt) => {
            const isSelected = selected.includes(opt.value);
            const { icon: Icon, tint } = GOAL_ICONS[opt.value];
            return (
              <button
                key={opt.value}
                type="button"
                role="checkbox"
                aria-checked={isSelected}
                onClick={() => {
                  setSelected((prev) => toggleGoal(prev, opt.value));
                  setError(false);
                }}
                // Thẻ đã chọn: nền xanh nhạt và ô tích, không thêm viền
                className={cn(CHOICE_CARD, "group flex w-full items-center gap-5 p-5 aria-checked:border-transparent aria-checked:ring-0 sm:p-6")}
              >
                <span aria-hidden className={cn("flex size-12 shrink-0 items-center justify-center rounded-xl group-aria-checked:bg-surface", tint)}>
                  <Icon className="size-6" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block text-lg font-semibold text-foreground">{opt.label}</span>
                  <span className="mt-0.5 block text-sm text-ink-2 sm:text-base">{opt.description}</span>
                </span>
                <CheckIndicator checked={isSelected} />
              </button>
            );
          })}
        </div>
        <div className="space-y-3">
          {error && (
            <p role="alert" className="text-sm font-medium text-destructive">
              Chọn ít nhất một nhu cầu để tiếp tục.
            </p>
          )}
          <Button type="button" size="lg" onClick={confirm} className="h-14 w-full text-base">
            {cta}
          </Button>
        </div>
      </div>
    </div>
  );
}

// Mỗi nhu cầu một biểu tượng và một sắc màu riêng (nền nhạt + chữ đậm cùng sắc) để quét nhanh
const GOAL_ICONS: Record<FormGoal, { icon: typeof ClipboardCheckIcon; tint: string }> = {
  danh_gia: { icon: ClipboardCheckIcon, tint: "bg-brand-wash text-brand" },
  lo_trinh: { icon: RouteIcon, tint: "bg-ok-wash text-ok" },
  goi_y_truong: { icon: GraduationCapIcon, tint: "bg-gold-wash text-gold" },
  chi_sua: { icon: PencilLineIcon, tint: "bg-reach-wash text-reach" },
};
