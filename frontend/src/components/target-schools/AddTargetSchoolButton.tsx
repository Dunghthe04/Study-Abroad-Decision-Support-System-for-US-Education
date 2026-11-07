"use client";

import React, { useState } from "react";
import { BookmarkIcon, BookmarkCheckIcon, Loader2, Sparkles } from "lucide-react";
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
}: AddTargetSchoolButtonProps) {
  const { user } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [isAdded, setIsAdded] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [category, setCategory] = useState(defaultCategory);
  const [round, setRound] = useState("");
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  const locationStr = [universityCity, universityState].filter(Boolean).join(", ");

  const handleOpen = () => {
    if (!user) {
      toast.add({
        type: "warning",
        title: "Cần đăng nhập",
        description: "Vui lòng đăng nhập bằng tài khoản Học sinh hoặc Phụ huynh để lưu trường mục tiêu.",
      });
      return;
    }
    setError(null);
    setIsOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
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

      setIsAdded(true);
      setIsOpen(false);

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

  return (
    <>
      <Button
        variant={isAdded ? "secondary" : variant}
        size={size}
        onClick={handleOpen}
        disabled={isAdded}
        className={cn("gap-2", className)}
      >
        {isAdded ? (
          <>
            <BookmarkCheckIcon className="size-4 text-emerald-600" />
            <span>Đã trong mục tiêu</span>
          </>
        ) : (
          <>
            <BookmarkIcon className="size-4 text-brand" />
            <span>Thêm vào mục tiêu</span>
          </>
        )}
      </Button>

      <Dialog open={isOpen} onOpenChange={setIsOpen}>
        <DialogContent className="max-w-md">
          <form onSubmit={handleSubmit} className="space-y-4">
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
                  Bạn có thể thay đổi hoặc cập nhật hạn nộp hồ sơ chi tiết sau.
                </FieldDescription>
              </Field>

              <Field>
                <FieldLabel>Ghi chú cá nhân (Tùy chọn)</FieldLabel>
                <Input
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="Ví dụ: Ngành Computer Science, đã liên hệ cựu sinh viên..."
                  maxLength={1000}
                />
              </Field>
            </div>

            <DialogFooter className="gap-2 sm:gap-0">
              <Button
                type="button"
                variant="ghost"
                onClick={() => setIsOpen(false)}
                disabled={isSubmitting}
              >
                Hủy
              </Button>
              <Button type="submit" disabled={isSubmitting} className="gap-2">
                {isSubmitting && <Loader2 className="size-4 animate-spin" />}
                Xác nhận thêm
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
