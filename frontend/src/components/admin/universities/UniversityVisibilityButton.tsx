"use client";

import { useState } from "react";
import { EyeIcon, EyeOffIcon } from "lucide-react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { AdminFormError, setAdminUniversityVisibility } from "@/lib/admin-university-api";
import type { AdminUniversityDetail } from "@/types/admin-university";

interface UniversityVisibilityButtonProps {
  university: AdminUniversityDetail;
  onChanged: (university: AdminUniversityDetail) => void;
}

/** [UC-67] Ẩn trường cần xác nhận; hiện lại thì làm ngay. */
export function UniversityVisibilityButton({ university, onChanged }: UniversityVisibilityButtonProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);
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
      setConfirmOpen(false);
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

  if (!university.isActive) {
    return (
      <Button type="button" variant="outline" size="sm" onClick={() => changeVisibility(true)} disabled={isSaving}>
        {isSaving ? <Spinner /> : <EyeIcon />}
        Hiện lại
      </Button>
    );
  }

  return (
    <>
      <Button type="button" variant="outline" size="sm" onClick={() => setConfirmOpen(true)} disabled={isSaving}>
        <EyeOffIcon />
        Ẩn trường
      </Button>
      <AlertDialog open={confirmOpen} onOpenChange={(open) => !isSaving && setConfirmOpen(open)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ẩn {university.name}?</AlertDialogTitle>
            <AlertDialogDescription>
              Trường sẽ không còn xuất hiện trong tìm kiếm và gợi ý cho học sinh. Bạn có thể hiện lại bất cứ lúc nào.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isSaving}>Hủy</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => changeVisibility(false)} disabled={isSaving}>
              {isSaving && <Spinner />}
              Ẩn trường
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
