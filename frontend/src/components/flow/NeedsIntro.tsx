"use client";

import { useId, useState } from "react";
import { ArrowRightIcon, CheckIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { EDIT_ONLY, FORM_GOAL_OPTIONS, toggleGoal, type FormGoal } from "@/lib/form-goals";
import { CHOICE_CARD } from "./StepChrome";

interface NeedsIntroProps {
  /** start: màn đầu tiên khi vào form; edit: sửa nhu cầu giữa chừng (có "Huỷ") */
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
  const cta = mode === "edit" ? "Lưu nhu cầu" : draftStep ? "Làm tiếp" : "Bắt đầu";

  const confirm = () => {
    if (selected.length === 0) {
      setError(true);
      return;
    }
    onConfirm(selected);
  };

  return (
    <div
      data-flow-motion
      className="flex min-h-[calc(100dvh-var(--header-h))] items-center justify-center bg-surface px-4 py-12"
    >
      <div className="flow-stagger w-full max-w-[560px] space-y-6">
        <h1 id={labelId} className="text-h1 text-foreground">
          Nhu cầu của bạn
        </h1>
        <p className="text-body text-ink-2">{lede}</p>
        <div role="group" aria-labelledby={labelId} className="flow-stagger grid gap-3 sm:grid-cols-2">
          {options.map((opt) => {
            const isSelected = selected.includes(opt.value);
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
                className={cn(CHOICE_CARD, "flex items-start gap-3")}
              >
                <span
                  aria-hidden
                  className={cn(
                    "mt-0.5 flex size-4 shrink-0 items-center justify-center rounded border",
                    isSelected ? "border-brand bg-brand text-brand-foreground" : "border-line"
                  )}
                >
                  {isSelected && <CheckIcon className="size-3" />}
                </span>
                <span>
                  <span className="block text-sm font-medium text-foreground">{opt.label}</span>
                  <span className="block text-xs text-ink-2">{opt.description}</span>
                </span>
              </button>
            );
          })}
        </div>
        <div className="space-y-3">
          {error && (
            <p role="alert" className="text-xs font-medium text-destructive">
              Chọn ít nhất một nhu cầu để tiếp tục.
            </p>
          )}
          <div className="flex items-center gap-3">
            <Button type="button" size="lg" onClick={confirm} className="gap-1.5">
              {cta}
              <ArrowRightIcon className="size-4" />
            </Button>
            {mode === "edit" && onCancel && (
              <Button type="button" variant="ghost" size="lg" onClick={onCancel}>
                Huỷ
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
