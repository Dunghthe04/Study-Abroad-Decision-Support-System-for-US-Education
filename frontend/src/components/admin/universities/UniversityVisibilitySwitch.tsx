"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useAuth } from "@/contexts/AuthContext";
import { toast } from "@/components/ui/toast";
import { AdminFormError, setAdminUniversityVisibility } from "@/lib/admin-university-api";
import type { AdminUniversityDetail } from "@/types/admin-university";

interface UniversityVisibilitySwitchProps {
  university: AdminUniversityDetail;
  onChanged: (university: AdminUniversityDetail) => void;
}

/** [UC-63] Cả Ẩn (kèm lý do) và Hiện đều yêu cầu xác nhận trước khi lưu. */
export function UniversityVisibilitySwitch({ university, onChanged }: UniversityVisibilitySwitchProps) {
  // [UC-63] Chỉ Moderator ẩn hoặc hiện trường; Admin chỉ xem trạng thái
  const { user } = useAuth();
  const canChange = user?.role === "moderator";
  const [confirming, setConfirming] = useState<"hide" | "show" | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);

  const changeVisibility = async (isActive: boolean) => {
    const trimmed = reason.trim();
    if (!isActive && !trimmed) {
      setReasonError("Vui lòng nhập lý do ẩn.");
      return;
    }
    setIsSaving(true);
    setReasonError(null);
    try {
      const saved = await setAdminUniversityVisibility(university.id, isActive, isActive ? null : trimmed);
      toast.add({
        type: "success",
        title: isActive ? "Đã hiện lại trường" : "Đã ẩn trường",
        description: saved.name,
      });
      onChanged(saved);
      setConfirming(null);
      setReason("");
    } catch (err) {
      if (err instanceof AdminFormError && err.fieldErrors.reason) {
        setReasonError(err.fieldErrors.reason);
        return;
      }
      toast.add({
        type: "error",
        title: "Không đổi được trạng thái hiển thị",
        description: err instanceof AdminFormError ? err.message : "Vui lòng thử lại sau.",
      });
    } finally {
      setIsSaving(false);
    }
  };

  const handleCheckedChange = (checked: boolean) => {
    setReasonError(null);
    setConfirming(checked ? "show" : "hide");
  };

  const switchChecked = confirming === "show" ? true : confirming === "hide" ? false : university.isActive;

  return (
    <div className="rounded-md border border-border">
      <label className="flex items-center justify-between gap-3 px-3 py-2">
        <span className="min-w-0">
          <span className="block text-sm font-medium">Hiển thị với học sinh</span>
          <span className="block text-xs text-muted-foreground">
            {university.isActive
              ? "Trường đang xuất hiện trong tìm kiếm và gợi ý."
              : "Trường đang bị ẩn khỏi tìm kiếm và gợi ý."}
            {!canChange && " Chỉ Moderator đổi được trạng thái này."}
          </span>
        </span>
        <span className="flex items-center gap-2">
          {isSaving && <Spinner />}
          <Switch
            checked={switchChecked}
            onCheckedChange={handleCheckedChange}
            disabled={!canChange || isSaving || confirming !== null}
            aria-label="Hiển thị trường với học sinh"
          />
        </span>
      </label>

      {confirming === "hide" && (
        <div className="space-y-2 border-t border-border bg-muted px-3 py-2">
          <p className="text-sm">
            Ẩn <span className="font-medium">{university.name}</span>? Học sinh sẽ không còn thấy trường này. Bạn có thể
            bật lại bất cứ lúc nào.
          </p>
          <Textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Lý do ẩn (bắt buộc, ghi vào nhật ký)"
            maxLength={500}
            disabled={isSaving}
            aria-label="Lý do ẩn trường"
            aria-invalid={Boolean(reasonError) || undefined}
          />
          {reasonError && <p className="text-xs text-destructive">{reasonError}</p>}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                setConfirming(null);
                setReasonError(null);
              }}
              disabled={isSaving}
            >
              Hủy
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              onClick={() => changeVisibility(false)}
              disabled={isSaving}
            >
              {isSaving && <Spinner />}
              Ẩn trường
            </Button>
          </div>
        </div>
      )}

      {confirming === "show" && (
        <div className="space-y-2 border-t border-border bg-muted px-3 py-2">
          <p className="text-sm">
            Hiện lại <span className="font-medium">{university.name}</span>? Trường sẽ xuất hiện trở lại trong tìm kiếm và gợi ý.
          </p>
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setConfirming(null)}
              disabled={isSaving}
            >
              Hủy
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={() => changeVisibility(true)}
              disabled={isSaving}
            >
              {isSaving && <Spinner />}
              Xác nhận hiện trường
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
