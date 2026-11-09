"use client";

import React, { useState, useEffect } from "react";
import { BookmarkIcon, BookmarkCheckIcon, Loader2, Sparkles, Trash2Icon, CalendarIcon, AlertCircleIcon } from "lucide-react";
import { targetSchoolsApi, ApiError } from "@/lib/api";
import type { AddTargetSchoolRequest, TargetSchoolDto } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel, FieldDescription } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

export interface AddTargetSchoolButtonProps {
  universityId?: string;
  universityCode?: string;
  universityName: string;
  universityCity?: string | null;
  universityState?: string | null;
  defaultCategory?: "reach" | "match" | "safety" | string;
  className?: string;
  variant?: "default" | "outline" | "secondary" | "ghost";
  size?: "default" | "sm" | "lg" | "icon";
  onSuccess?: (targetSchool: TargetSchoolDto) => void;
  onRemove?: (targetSchoolId: string) => void;
}

const CATEGORY_OPTIONS = [
  { value: "match", label: "Vừa sức (Match)", desc: "Điểm và hồ sơ nằm trong khoảng tuyển sinh trung bình" },
  { value: "reach", label: "Mục tiêu cao / Với (Reach)", desc: "Trường có tính cạnh tranh cao hoặc yêu cầu cao hơn hồ sơ hiện tại" },
  { value: "safety", label: "An toàn (Safety)", desc: "Hồ sơ vượt trội so với yêu cầu đầu vào thông thường" },
];

const APPLICATION_ROUNDS = [
  { value: "Early Action", label: "Early Action (EA)" },
  { value: "Early Decision", label: "Early Decision (ED)" },
  { value: "Regular Decision", label: "Regular Decision (RD)" },
  { value: "Rolling", label: "Rolling Admission" },
];

export function AddTargetSchoolButton({
  universityId,
  universityCode,
  universityName,
  universityCity,
  universityState,
  defaultCategory = "match",
  className,
  variant = "outline",
  size = "default",
  onSuccess,
  onRemove,
}: AddTargetSchoolButtonProps) {
  const { user } = useAuth();
  const [isOpenAdd, setIsOpenAdd] = useState(false);
  const [isOpenManage, setIsOpenManage] = useState(false);
  const [savedTarget, setSavedTarget] = useState<TargetSchoolDto | null>(null);
  const [isChecking, setIsChecking] = useState(() => Boolean(user && (universityCode || universityId)));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [category, setCategory] = useState(defaultCategory);
  const [round, setRound] = useState("");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  // [UC-32] State chọn và đổi vòng nộp hồ sơ
  const [isEditingRound, setIsEditingRound] = useState(false);
  const [selectedEditRound, setSelectedEditRound] = useState("");
  const [isUpdatingRound, setIsUpdatingRound] = useState(false);

  const locationStr = [universityCity, universityState].filter(Boolean).join(", ");

  // Kiểm tra xem trường này đã được lưu vào danh sách mục tiêu chưa
  useEffect(() => {
    if (!user || (!universityCode && !universityId)) {
      return;
    }

    let cancelled = false;

    targetSchoolsApi
      .check({ universityCode, universityId })
      .then((res) => {
        if (!cancelled) setSavedTarget(res);
      })
      .catch(() => {
        if (!cancelled) setSavedTarget(null);
      })
      .finally(() => {
        if (!cancelled) setIsChecking(false);
      });

    return () => {
      cancelled = true;
    };
  }, [user, universityCode, universityId]);

  const handleClickButton = () => {
    if (!user) {
      toast.add({
        type: "warning",
        title: "Cần đăng nhập",
        description: "Vui lòng đăng nhập bằng tài khoản Học sinh hoặc Phụ huynh để quản lý trường mục tiêu.",
      });
      return;
    }

    setError(null);
    setConfirmDelete(false);

    if (savedTarget) {
      // Đã có trong danh sách -> Mở modal xem thông tin và tùy chọn xóa (UC-31)
      setIsOpenManage(true);
    } else {
      // Chưa có -> Mở modal thêm (UC-30)
      setIsOpenAdd(true);
    }
  };

  // [UC-30] Thêm trường mục tiêu
  const handleSubmitAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      const payload: AddTargetSchoolRequest = {
        universityId: universityId || undefined,
        universityCode: universityCode || undefined,
        category: category || null,
        applicationRound: round || null,
        notes: notes.trim() || null,
      };

      const result = await targetSchoolsApi.add(payload);

      setSavedTarget(result);
      setIsOpenAdd(false);

      toast.add({
        type: "success",
        title: "Đã lưu vào danh sách mục tiêu",
        description: `${universityName} đã được thêm vào mục tiêu theo dõi của bạn.`,
      });

      onSuccess?.(result);
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        const msg = typeof err.body === "string" ? err.body : err.message;
        setError(msg || "Không thể thêm trường vào mục tiêu.");
      } else {
        setError("Đã xảy ra lỗi khi kết nối máy chủ. Vui lòng thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // [UC-31] Xóa trường khỏi danh sách mục tiêu
  const handleDeleteTargetSchool = async () => {
    if (!savedTarget) return;

    setIsDeleting(true);
    setError(null);

    try {
      await targetSchoolsApi.remove(savedTarget.id);

      const targetId = savedTarget.id;
      setSavedTarget(null);
      setIsOpenManage(false);
      setConfirmDelete(false);

      toast.add({
        type: "info",
        title: "Đã xóa khỏi danh sách mục tiêu",
        description: `${universityName} đã được xóa khỏi danh sách trường mục tiêu của bạn.`,
      });

      onRemove?.(targetId);
    } catch {
      setError("Không thể xóa trường mục tiêu. Vui lòng thử lại sau.");
    } finally {
      setIsDeleting(false);
    }
  };

  // [UC-32] Cập nhật vòng nộp hồ sơ
  const handleUpdateRound = async (newRound: string) => {
    if (!savedTarget || !newRound) return;

    setIsUpdatingRound(true);
    setError(null);

    try {
      const updated = await targetSchoolsApi.updateRound(savedTarget.id, {
        applicationRound: newRound,
        autoResolveDeadline: true,
      });

      setSavedTarget(updated);
      setIsEditingRound(false);

      toast.add({
        type: "success",
        title: "Đã cập nhật vòng nộp",
        description: `Đã chọn đợt nộp ${newRound} cho ${universityName}.`,
      });

      onSuccess?.(updated);
    } catch {
      setError("Không thể cập nhật vòng nộp hồ sơ. Vui lòng thử lại.");
    } finally {
      setIsUpdatingRound(false);
    }
  };

  const getCategoryLabel = (cat?: string | null) => {
    switch (cat?.toLowerCase()) {
      case "reach":
        return { label: "Mục tiêu cao (Reach)", color: "destructive" as const };
      case "match":
        return { label: "Vừa sức (Match)", color: "brand" as const };
      case "safety":
        return { label: "An toàn (Safety)", color: "default" as const };
      default:
        return { label: "Chưa phân loại", color: "outline" as const };
    }
  };

  return (
    <>
      <Button
        variant={savedTarget ? "secondary" : variant}
        size={size}
        onClick={handleClickButton}
        disabled={isChecking}
        className={cn("gap-2", savedTarget && "border-emerald-200 bg-emerald-50 text-emerald-800 hover:bg-emerald-100 dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300", className)}
      >
        {isChecking ? (
          <>
            <Loader2 className="size-4 animate-spin text-muted-foreground" />
            <span>Đang kiểm tra...</span>
          </>
        ) : savedTarget ? (
          <>
            <BookmarkCheckIcon className="size-4 text-emerald-600 dark:text-emerald-400" />
            <span>Đã trong mục tiêu</span>
          </>
        ) : (
          <>
            <BookmarkIcon className="size-4 text-brand" />
            <span>Lưu mục tiêu</span>
          </>
        )}
      </Button>

      {/* Modal [UC-30] Thêm trường mục tiêu */}
      <Dialog open={isOpenAdd} onOpenChange={setIsOpenAdd}>
        <DialogContent className="max-w-md">
          <form onSubmit={handleSubmitAdd} className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-lg">
                <Sparkles className="size-5 text-brand" />
                Lưu vào danh sách mục tiêu
              </DialogTitle>
              <DialogDescription>
                Thêm <span className="font-semibold text-foreground">{universityName}</span>
                {locationStr && ` (${locationStr})`} vào kế hoạch nộp hồ sơ của bạn.
              </DialogDescription>
            </DialogHeader>

            {error && (
              <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                {error}
              </div>
            )}

            <div className="space-y-3">
              <Field>
                <FieldLabel>Phân loại mục tiêu</FieldLabel>
                <div className="grid grid-cols-1 gap-2">
                  {CATEGORY_OPTIONS.map((opt) => (
                    <label
                      key={opt.value}
                      className={cn(
                        "flex cursor-pointer flex-col rounded-lg border p-2.5 transition-colors",
                        category === opt.value
                          ? "border-brand bg-brand/5 text-foreground"
                          : "border-border hover:bg-muted/50"
                      )}
                    >
                      <div className="flex items-center gap-2">
                        <input
                          type="radio"
                          name="category"
                          value={opt.value}
                          checked={category === opt.value}
                          onChange={(e) => setCategory(e.target.value)}
                          className="size-4 text-brand"
                        />
                        <span className="font-medium text-sm">{opt.label}</span>
                      </div>
                      <span className="mt-1 pl-6 text-xs text-muted-foreground">{opt.desc}</span>
                    </label>
                  ))}
                </div>
              </Field>

              <Field>
                <FieldLabel>Vòng nộp dự kiến (Tùy chọn)</FieldLabel>
                <select
                  value={round}
                  onChange={(e) => setRound(e.target.value)}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-sm focus:border-brand focus:outline-none focus:ring-1 focus:ring-brand"
                >
                  <option value="">-- Chưa chọn vòng nộp --</option>
                  {APPLICATION_ROUNDS.map((r) => (
                    <option key={r.value} value={r.value}>
                      {r.label}
                    </option>
                  ))}
                </select>
                <FieldDescription>
                  Hệ thống sẽ tự động xác định hạn nộp hồ sơ từ dữ liệu tuyển sinh của trường.
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel>Ghi chú cá nhân (Tùy chọn)</FieldLabel>
                <Input
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="Ví dụ: Ngành Computer Science, cần xin hỗ trợ tài chính..."
                  maxLength={1000}
                />
              </Field>
            </div>

            <DialogFooter className="gap-2 sm:gap-0">
              <Button
                type="button"
                variant="ghost"
                onClick={() => setIsOpenAdd(false)}
                disabled={isSubmitting}
              >
                Hủy
              </Button>
              <Button type="submit" disabled={isSubmitting} className="gap-2">
                {isSubmitting && <Loader2 className="size-4 animate-spin" />}
                Xác nhận lưu
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Modal [UC-31] Quản lý & Xóa trường mục tiêu */}
      <Dialog open={isOpenManage} onOpenChange={setIsOpenManage}>
        <DialogContent className="max-w-md">
          <div className="space-y-4">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2 text-lg">
                <BookmarkCheckIcon className="size-5 text-emerald-600" />
                Thông tin trường mục tiêu
              </DialogTitle>
              <DialogDescription>
                <span className="font-semibold text-foreground">{universityName}</span> đang nằm trong danh sách mục tiêu của bạn.
              </DialogDescription>
            </DialogHeader>

            {error && (
              <div className="rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                {error}
              </div>
            )}

            {savedTarget && (
              <div className="space-y-3 rounded-lg border border-line bg-muted/30 p-3.5 text-sm">
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Phân loại:</span>
                  <Badge variant={getCategoryLabel(savedTarget.category).color}>
                    {getCategoryLabel(savedTarget.category).label}
                  </Badge>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">Vòng nộp:</span>
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-foreground">
                      {savedTarget.applicationRound || "Chưa xác định"}
                    </span>
                    {!isEditingRound && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => {
                          setSelectedEditRound(savedTarget.applicationRound || "Regular Decision");
                          setIsEditingRound(true);
                        }}
                        className="text-xs text-brand h-6 px-1.5 hover:underline"
                      >
                        Đổi vòng nộp
                      </Button>
                    )}
                  </div>
                </div>

                {isEditingRound && (
                  <div className="rounded-md border border-brand/30 bg-brand/5 p-2.5 space-y-2 mt-1">
                    <span className="text-xs font-semibold text-foreground block">
                      [UC-32] Chọn vòng nộp hồ sơ:
                    </span>
                    <div className="grid grid-cols-2 gap-1.5">
                      {APPLICATION_ROUNDS.map((r) => (
                        <Button
                          key={r.value}
                          type="button"
                          size="xs"
                          variant={selectedEditRound === r.value ? "default" : "outline"}
                          onClick={() => setSelectedEditRound(r.value)}
                          className="text-xs justify-start h-7"
                          disabled={isUpdatingRound}
                        >
                          {r.label}
                        </Button>
                      ))}
                    </div>
                    <div className="flex items-center justify-end gap-1.5 pt-1">
                      <Button
                        type="button"
                        size="xs"
                        variant="ghost"
                        onClick={() => setIsEditingRound(false)}
                        disabled={isUpdatingRound}
                        className="h-6 text-xs"
                      >
                        Hủy
                      </Button>
                      <Button
                        type="button"
                        size="xs"
                        onClick={() => handleUpdateRound(selectedEditRound)}
                        disabled={isUpdatingRound || !selectedEditRound}
                        className="h-6 text-xs bg-brand text-white hover:bg-brand/90 gap-1"
                      >
                        {isUpdatingRound && <Loader2 className="size-3 animate-spin" />}
                        Lưu vòng nộp
                      </Button>
                    </div>
                  </div>
                )}

                {savedTarget.deadline && (
                  <div className="flex items-center justify-between">
                    <span className="inline-flex items-center gap-1.5 text-muted-foreground">
                      <CalendarIcon className="size-3.5" /> Hạn nộp:
                    </span>
                    <span className="font-medium text-foreground">
                      {new Date(savedTarget.deadline).toLocaleDateString("vi-VN", {
                        day: "2-digit",
                        month: "2-digit",
                        year: "numeric",
                      })}
                    </span>
                  </div>
                )}

                {savedTarget.notes && (
                  <div className="border-t border-border/50 pt-2 text-xs">
                    <span className="font-medium text-muted-foreground">Ghi chú:</span>
                    <p className="mt-0.5 text-foreground italic">{savedTarget.notes}</p>
                  </div>
                )}
              </div>
            )}

            {confirmDelete ? (
              <div className="rounded-lg border border-destructive/20 bg-destructive/5 p-3.5 space-y-2">
                <div className="flex items-start gap-2 text-destructive text-sm font-medium">
                  <AlertCircleIcon className="size-4 shrink-0 mt-0.5" />
                  <span>Xác nhận xóa trường này khỏi danh sách mục tiêu?</span>
                </div>
                <p className="text-xs text-muted-foreground pl-6">
                  Trường này sẽ được gỡ khỏi kế hoạch theo dõi và dòng thời gian nộp đơn của bạn.
                </p>
                <div className="flex items-center justify-end gap-2 pt-2">
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => setConfirmDelete(false)}
                    disabled={isDeleting}
                  >
                    Hủy
                  </Button>
                  <Button
                    size="sm"
                    variant="destructive"
                    onClick={handleDeleteTargetSchool}
                    disabled={isDeleting}
                    className="gap-1.5"
                  >
                    {isDeleting && <Loader2 className="size-3.5 animate-spin" />}
                    <Trash2Icon className="size-3.5" />
                    Đồng ý xóa
                  </Button>
                </div>
              </div>
            ) : (
              <DialogFooter className="flex items-center justify-between sm:justify-between w-full">
                <Button
                  type="button"
                  variant="destructive"
                  size="sm"
                  onClick={() => setConfirmDelete(true)}
                  className="gap-1.5"
                >
                  <Trash2Icon className="size-4" />
                  Xóa khỏi mục tiêu
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setIsOpenManage(false)}
                >
                  Đóng
                </Button>
              </DialogFooter>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
