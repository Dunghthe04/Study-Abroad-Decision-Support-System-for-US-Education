"use client";

import { useState } from "react";
import { PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { AdminFormError, createAdminOffering } from "@/lib/admin-university-api";
import { studyLevelLabel } from "@/lib/study-levels";
import type { AdminUniversityOffering } from "@/types/admin-university";
import { OptionSelect } from "./OptionSelect";

interface AddOfferingControlProps {
  universityId: string;
  existingLevels: string[];
  disabled: boolean;
  onCreated: (offering: AdminUniversityOffering) => void;
}

/** [UC-65] Thêm bậc học trống; số liệu nhập tiếp ở các bảng của bậc học đó. */
export function AddOfferingControl({ universityId, existingLevels, disabled, onCreated }: AddOfferingControlProps) {
  const { user } = useAuth();
  const [level, setLevel] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  // UC-61: chỉ hỗ trợ thêm bậc đại học (undergraduate)
  const options = ["undergraduate"].filter((l) => !existingLevels.includes(l)).map((l) => ({
    value: l,
    label: studyLevelLabel(l),
  }));

  if (user?.role !== "moderator" || options.length === 0) {
    return null;
  }

  const handleAdd = async () => {
    if (!level) return;
    setIsSaving(true);
    try {
      const created = await createAdminOffering(universityId, level);
      toast.add({ type: "success", title: "Đã thêm bậc học", description: studyLevelLabel(created.studyLevel) });
      setLevel("");
      onCreated(created);
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không thêm được bậc học",
        description: err instanceof AdminFormError ? (err.fieldErrors.studyLevel ?? err.message) : "Vui lòng thử lại sau.",
      });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex items-center gap-2">
      <OptionSelect
        value={level}
        options={[{ value: "", label: "Chọn bậc học để thêm" }, ...options]}
        onChange={setLevel}
        disabled={disabled || isSaving}
        size="sm"
        aria-label="Bậc học cần thêm"
      />
      <Button type="button" size="sm" onClick={handleAdd} loading={isSaving} disabled={disabled || !level}>
        <PlusIcon />
        Thêm bậc học
      </Button>
    </div>
  );
}
