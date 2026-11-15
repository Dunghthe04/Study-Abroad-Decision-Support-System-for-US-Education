"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/components/ui/toast";
import { AdminFormError, setAdminUniversityVisibility } from "@/lib/admin-university-api";
import type { AdminUniversityDetail } from "@/types/admin-university";

interface UniversityVisibilitySwitchProps {
  university: AdminUniversityDetail;
  onChanged: (university: AdminUniversityDetail) => void;
}

/** [UC-67] Tắt (ẩn trường) cần xác nhận ngay bên dưới; bật lại thì làm ngay. */
export function UniversityVisibilitySwitch({ university, onChanged }: UniversityVisibilitySwitchProps) {
  const [confirmingHide, setConfirmingHide] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const changeVisibility = async (isActive: boolean) => {
    setIsSaving(true);
    try {
      const saved = await setAdminUniversityVisibility(university.id, isActive);
      toast.add({
        type: "success",
        title: isActive ? "Đã hiện lại trường" : "Đã ẩn trường",
        description: saved.name,
      });
      onChanged(saved);
      setConfirmingHide(false);
    } catch (err) {
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
    if (checked) {
      changeVisibility(true);
    } else {
      setConfirmingHide(true);
    }
  };

  const switchChecked = university.isActive && !confirmingHide;

  return (
    <div className="rounded-md border border-border">
      <label className="flex items-center justify-between gap-3 px-3 py-2">
        <span className="min-w-0">
          <span className="block text-sm font-medium">Hiển thị với học sinh</span>
          <span className="block text-xs text-muted-foreground">
            {university.isActive
              ? "Trường đang xuất hiện trong tìm kiếm và gợi ý."
              : "Trường đang bị ẩn khỏi tìm kiếm và gợi ý."}
          </span>
        </span>
        <span className="flex items-center gap-2">
          {isSaving && <Spinner />}
          <Switch
            checked={switchChecked}
            onCheckedChange={handleCheckedChange}
            disabled={isSaving || confirmingHide}
            aria-label="Hiển thị trường với học sinh"
          />
        </span>
      </label>

      {confirmingHide && (
        <div className="space-y-2 border-t border-border bg-muted px-3 py-2">
          <p className="text-sm">
            Ẩn <span className="font-medium">{university.name}</span>? Học sinh sẽ không còn thấy trường này. Bạn có thể
            bật lại bất cứ lúc nào.
          </p>
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setConfirmingHide(false)}
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
    </div>
  );
}
