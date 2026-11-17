"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { setAdminScholarshipVisibility } from "@/lib/admin-scholarship-api";
import { AdminFormError } from "@/lib/admin-university-api";
import type { AdminScholarship } from "@/types/admin-scholarship";

interface ScholarshipVisibilityToggleProps {
  scholarship: AdminScholarship;
  canChange: boolean;
  onChanged: (scholarship: AdminScholarship) => void;
}

/** [UC-109] Ẩn học bổng cần lý do (ghi vào nhật ký); hiện lại thì không. */
export function ScholarshipVisibilityToggle({ scholarship, canChange, onChanged }: ScholarshipVisibilityToggleProps) {
  const [confirmingHide, setConfirmingHide] = useState(false);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const change = async (isActive: boolean) => {
    const trimmed = reason.trim();
    if (!isActive && !trimmed) {
      setReasonError("Vui lòng nhập lý do ẩn.");
      return;
    }
    setIsSaving(true);
    setReasonError(null);
    try {
      const saved = await setAdminScholarshipVisibility(scholarship.id, isActive, isActive ? null : trimmed);
      toast.add({ type: "success", title: isActive ? "Đã hiện lại học bổng" : "Đã ẩn học bổng", description: saved.name });
      setConfirmingHide(false);
      setReason("");
      onChanged(saved);
    } catch (err) {
      if (err instanceof AdminFormError && err.fieldErrors.reason) {
        setReasonError(err.fieldErrors.reason);
      } else {
        toast.add({
          type: "error",
          title: "Không đổi được trạng thái hiển thị",
          description: err instanceof AdminFormError ? err.message : "Vui lòng thử lại sau.",
        });
      }
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-end gap-2">
        {isSaving && <Spinner />}
        <Switch
          checked={scholarship.isActive && !confirmingHide}
          onCheckedChange={(checked) => (checked ? change(true) : setConfirmingHide(true))}
          disabled={!canChange || isSaving || confirmingHide}
          aria-label={`Hiển thị học bổng ${scholarship.name}`}
        />
      </div>
      {confirmingHide && (
        <div className="space-y-2 text-left">
          <Textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Lý do ẩn (bắt buộc, ghi vào nhật ký)"
            maxLength={500}
            disabled={isSaving}
            aria-label="Lý do ẩn học bổng"
            aria-invalid={Boolean(reasonError) || undefined}
          />
          {reasonError && <p className="text-xs text-destructive">{reasonError}</p>}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="xs"
              disabled={isSaving}
              onClick={() => {
                setConfirmingHide(false);
                setReasonError(null);
              }}
            >
              Hủy
            </Button>
            <Button type="button" variant="destructive" size="xs" disabled={isSaving} onClick={() => change(false)}>
              Ẩn học bổng
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
