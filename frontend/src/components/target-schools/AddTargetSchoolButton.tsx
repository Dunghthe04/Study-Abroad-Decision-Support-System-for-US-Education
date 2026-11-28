"use client";

import React, { useState, useEffect } from "react";
import { BookmarkIcon, BookmarkCheckIcon, BookmarkPlusIcon, Loader2, CalendarIcon, AlertCircleIcon } from "lucide-react";
import { targetSchoolsApi, ApiError } from "@/lib/api";
import type { AddTargetSchoolRequest, TargetSchoolDto } from "@/types/api";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { groupLabel } from "@/lib/recommendation-labels";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";
import { SuggestToChildrenButton } from "@/components/parent/SuggestToChildrenButton";

export interface AddTargetSchoolButtonProps {
  universityId?: string;
  universityCode?: string;
  universityName: string;
  universityCity?: string | null;
  universityState?: string | null;
  deadlinesJson?: string | null;
  supportedRounds?: string[] | null;
  defaultCategory?: "reach" | "match" | "safety" | string;
  className?: string;
  variant?: "default" | "outline" | "secondary" | "ghost";
  size?: "default" | "sm" | "lg" | "icon";
  /** Chỉ hiện biểu tượng (thẻ trường trong danh sách): đã lưu thì biểu tượng đặc */
  iconOnly?: boolean;
  onSuccess?: (targetSchool: TargetSchoolDto) => void;
  onRemove?: (targetSchoolId: string) => void;
}

const CATEGORY_OPTIONS = [
  {
    value: "match",
    title: groupLabel("M"),
    englishTerm: "Match",
    badgeVariant: "brand" as const,
    desc: "Điểm và hồ sơ nằm trong khoảng tuyển sinh trung bình",
  },
  {
    value: "reach",
    title: groupLabel("R"),
    englishTerm: "Reach",
    badgeVariant: "reach" as const,
    desc: "Trường có tính cạnh tranh cao hoặc yêu cầu cao hơn hồ sơ hiện tại",
  },
  {
    value: "safety",
    title: groupLabel("S"),
    englishTerm: "Safety",
    badgeVariant: "ok" as const,
    desc: "Hồ sơ vượt trội so với yêu cầu đầu vào thông thường",
  },
];

const APPLICATION_ROUNDS = [
  { value: "none", label: "-- Chưa chọn vòng nộp --" },
  { value: "Early Decision", label: "Early Decision (ED)" },
  { value: "Early Decision II", label: "Early Decision II (ED II)" },
  { value: "Early Action", label: "Early Action (EA)" },
  { value: "Restrictive Early Action", label: "Restrictive / Single-Choice Early Action (REA/SCEA)" },
  { value: "Regular Decision", label: "Regular Decision (RD)" },
  { value: "Rolling", label: "Rolling Admission" },
];

function isBindingRound(roundValue?: string | null): boolean {
  return roundValue === "Early Decision" || roundValue === "Early Decision II";
}

function isRestrictiveRound(roundValue?: string | null): boolean {
  return roundValue === "Restrictive Early Action";
}

function parseRoundsFromDeadlinesJson(deadlinesJson?: string | null): string[] {
  if (!deadlinesJson) return [];
  try {
    const parsed = JSON.parse(deadlinesJson) as Record<string, unknown>;
    if (!parsed || typeof parsed !== "object") return [];
    const keys = new Set<string>();
    for (const [k, v] of Object.entries(parsed)) {
      if (v == null || (typeof v === "string" && !v.trim())) continue;
      const tokens = k.toUpperCase().split(/[^A-Z0-9]+/).filter(Boolean);
      if (
        tokens.includes("ED2") ||
        tokens.includes("EDII") ||
        (tokens.includes("EARLY") && tokens.includes("DECISION") && (tokens.includes("2") || tokens.includes("II"))) ||
        (tokens.includes("ED") && (tokens.includes("2") || tokens.includes("II")))
      ) {
        keys.add("ED2");
      } else if (
        tokens.includes("ED") ||
        tokens.includes("ED1") ||
        tokens.includes("EDI") ||
        (tokens.includes("EARLY") && tokens.includes("DECISION"))
      ) {
        keys.add("ED");
      } else if (
        tokens.includes("REA") ||
        tokens.includes("SCEA") ||
        (tokens.includes("RESTRICTIVE") && tokens.includes("EARLY") && tokens.includes("ACTION")) ||
        (tokens.includes("SINGLE") && tokens.includes("CHOICE") && tokens.includes("EARLY") && tokens.includes("ACTION"))
      ) {
        keys.add("REA");
      } else if (
        tokens.includes("EA") ||
        tokens.includes("EA1") ||
        tokens.includes("EA2") ||
        (tokens.includes("EARLY") && tokens.includes("ACTION"))
      ) {
        keys.add("EA");
      } else if (tokens.includes("RD") || tokens.includes("REGULAR")) {
        keys.add("RD");
      } else if (tokens.some((t) => t === "ROLLING" || t.startsWith("ROLLING"))) {
        keys.add("ROLLING");
      }
    }
    const ordered: string[] = [];
    if (keys.has("ED")) ordered.push("Early Decision");
    if (keys.has("ED2")) ordered.push("Early Decision II");
    if (keys.has("EA")) ordered.push("Early Action");
    if (keys.has("REA")) ordered.push("Restrictive Early Action");
    if (keys.has("RD")) ordered.push("Regular Decision");
    if (keys.has("ROLLING")) ordered.push("Rolling");
    return ordered;
  } catch {
    return [];
  }
}

function filterRoundOptions(supported?: string[] | null) {
  if (!supported || supported.length === 0) return APPLICATION_ROUNDS;
  const set = new Set(supported);
  const filtered = APPLICATION_ROUNDS.filter((r) => r.value === "none" || set.has(r.value));
  return filtered.length > 1 ? filtered : APPLICATION_ROUNDS;
}

// Phụ huynh không có danh sách mục tiêu riêng: nút đổi thành "Gợi ý cho con" (hỏi gợi ý cho con nào).
// 1.5f: đang làm thay con managed thì lưu thẳng vào danh sách của con như học sinh
export function AddTargetSchoolButton(props: AddTargetSchoolButtonProps) {
  const { user, acting } = useAuth();
  if (user?.role === "parent" && !acting) return <SuggestToChildrenButton {...props} />;
  return <StudentTargetSchoolButton {...props} />;
}

function StudentTargetSchoolButton({
  universityId,
  universityCode,
  universityName,
  universityCity,
  universityState,
  deadlinesJson,
  supportedRounds,
  defaultCategory = "match",
  className,
  variant = "outline",
  size = "default",
  iconOnly = false,
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
  const [recommendedCategory, setRecommendedCategory] = useState<string | null>(null);
  const [userPickedCategory, setUserPickedCategory] = useState(false);
  const [availableRounds, setAvailableRounds] = useState<string[]>(() =>
    supportedRounds && supportedRounds.length > 0
      ? supportedRounds
      : parseRoundsFromDeadlinesJson(deadlinesJson)
  );
  const [isScholarshipMandatory, setIsScholarshipMandatory] = useState(false);
  const [existingEdSchoolName, setExistingEdSchoolName] = useState<string | null>(null);
  const [existingEd2SchoolName, setExistingEd2SchoolName] = useState<string | null>(null);
  const [round, setRound] = useState("");
  const [confirmBindingAdd, setConfirmBindingAdd] = useState(false);
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<string | null>(null);

  // [UC-32] State chọn và đổi vòng nộp hồ sơ
  const [isEditingRound, setIsEditingRound] = useState(false);
  const [selectedEditRound, setSelectedEditRound] = useState("");
  const [confirmBindingEdit, setConfirmBindingEdit] = useState(false);
  const [isUpdatingRound, setIsUpdatingRound] = useState(false);

  // [UC-33] State đổi nhóm phân loại (reach / match / safety)
  const [isEditingCategory, setIsEditingCategory] = useState(false);
  const [selectedEditCategory, setSelectedEditCategory] = useState("match");
  const [isUpdatingCategory, setIsUpdatingCategory] = useState(false);

  const locationStr = [universityCity, universityState].filter(Boolean).join(", ");
  const addRoundOptions = filterRoundOptions(availableRounds);
  const manageRoundOptions = filterRoundOptions(
    savedTarget?.availableRounds && savedTarget.availableRounds.length > 0
      ? savedTarget.availableRounds
      : availableRounds
  ).filter((r) => r.value !== "none");

  // Kiểm tra xem trường này đã được lưu vào danh sách mục tiêu chưa
  useEffect(() => {
    if (!user || (!universityCode && !universityId)) {
      return;
    }

    let cancelled = false;

    targetSchoolsApi
      .check({ universityCode, universityId })
      .then((res) => {
        if (!cancelled) {
          if (res && res.id) {
            setSavedTarget(res);
          } else {
            setSavedTarget(null);
          }
        }
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

  // Khi mở modal Thêm hoặc Quản lý (UC-30 / UC-32 / UC-33): nạp nhóm gợi ý, vòng nộp, cờ học bổng bắt buộc và trường giữ ED/ED II
  useEffect(() => {
    if ((!isOpenAdd && !isOpenManage) || !user || (!universityCode && !universityId)) return;
    let cancelled = false;
    targetSchoolsApi
      .getDefaults({ universityCode, universityId })
      .then((res) => {
        if (cancelled) return;
        if (res.availableRounds && res.availableRounds.length > 0) {
          setAvailableRounds(res.availableRounds);
        }
        setIsScholarshipMandatory(Boolean(res.isScholarshipMandatory));
        setExistingEdSchoolName(res.existingEdSchoolName ?? null);
        setExistingEd2SchoolName(res.existingEd2SchoolName ?? null);
        if (isOpenAdd) {
          if (res.recommendedCategory) {
            setRecommendedCategory(res.recommendedCategory);
            setCategory(res.recommendedCategory);
            setUserPickedCategory(false);
          } else {
            setRecommendedCategory(null);
            setUserPickedCategory(true);
          }
        }
      })
      .catch(() => {
        // Giữ giá trị mặc định nếu không tải được
      });
    return () => {
      cancelled = true;
    };
  }, [isOpenAdd, isOpenManage, user, universityCode, universityId]);

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
    setIsEditingRound(false);
    setIsEditingCategory(false);

    if (savedTarget && savedTarget.id) {
      // Đã có trong danh sách -> Mở modal xem thông tin và tùy chọn xóa (UC-31)
      setConfirmBindingEdit(false);
      setIsOpenManage(true);
    } else {
      // Chưa có -> Mở modal thêm (UC-30)
      setRound("");
      setConfirmBindingAdd(false);
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
        isUserCategory: recommendedCategory ? userPickedCategory : true,
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
    if (!savedTarget && !universityCode && !universityId) return;

    setIsDeleting(true);
    setError(null);

    try {
      if (savedTarget?.id) {
        await targetSchoolsApi.remove(savedTarget.id);
      } else if (universityCode) {
        await targetSchoolsApi.removeByUniversity(universityCode);
      } else if (universityId) {
        await targetSchoolsApi.removeByUniversity(universityId);
      } else {
        throw new Error("Không tìm thấy thông tin trường để xóa.");
      }

      const targetId = savedTarget?.id;
      setSavedTarget(null);
      setIsOpenManage(false);
      setConfirmDelete(false);

      toast.add({
        type: "info",
        title: "Đã xóa khỏi danh sách mục tiêu",
        description: `${universityName} đã được xóa khỏi danh sách trường mục tiêu của bạn.`,
      });

      if (targetId) {
        onRemove?.(targetId);
      }
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
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        setError(err.message || "Không thể cập nhật vòng nộp hồ sơ.");
      } else {
        setError("Không thể cập nhật vòng nộp hồ sơ. Vui lòng thử lại.");
      }
    } finally {
      setIsUpdatingRound(false);
    }
  };

  // [UC-33] Đổi nhóm phân loại (reach / match / safety)
  const handleUpdateCategory = async (newCategory: string) => {
    if (!savedTarget || !newCategory) return;

    setIsUpdatingCategory(true);
    setError(null);

    try {
      const updated = await targetSchoolsApi.updateCategory(savedTarget.id, { category: newCategory });
      setSavedTarget(updated);
      setIsEditingCategory(false);

      toast.add({
        type: "success",
        title: "Đã cập nhật nhóm phân loại",
        description: `Đã chuyển ${universityName} sang nhóm ${getCategoryLabel(newCategory).label}.`,
      });

      onSuccess?.(updated);
    } catch {
      setError("Không thể cập nhật nhóm phân loại. Vui lòng thử lại.");
    } finally {
      setIsUpdatingCategory(false);
    }
  };

  const getCategoryLabel = (cat?: string | null) => {
    switch (cat?.toLowerCase()) {
      case "reach":
        return { label: groupLabel("R"), color: "reach" as const };
      case "match":
        return { label: groupLabel("M"), color: "brand" as const };
      case "safety":
        return { label: groupLabel("S"), color: "ok" as const };
      default:
        return { label: "Chưa phân loại", color: "neutral" as const };
    }
  };

  const isSaved = Boolean(savedTarget && savedTarget.id);

  return (
    <>
      {iconOnly ? (
        <Button
          variant="ghost"
          size="icon"
          onClick={() => { if (!isChecking) handleClickButton(); }}
          aria-label={isSaved ? `${universityName}: đã trong mục tiêu, bấm để xem hoặc bỏ` : `Lưu ${universityName} vào mục tiêu`}
          aria-pressed={isSaved}
          title={isSaved ? "Đã trong mục tiêu" : "Lưu mục tiêu"}
          className={cn(
            "size-9 rounded-full border-0 bg-transparent text-white shadow-none drop-shadow-[0_1px_3px_rgba(0,0,0,0.6)] hover:bg-black/25 hover:text-white focus-visible:ring-white/70",
            className,
          )}
        >
          <BookmarkIcon className={cn("size-[22px]", isSaved && "fill-current")} aria-hidden />
        </Button>
      ) : (
      <Button
        variant={isSaved ? "secondary" : variant}
        size={size}
        onClick={handleClickButton}
        disabled={isChecking}
        className={cn(
          "gap-2 hover:bg-brand-wash hover:text-brand-ink hover:border-brand/40 transition-colors",
          isSaved && "border-ok/25 bg-ok-wash text-ok hover:bg-ok-wash/80 hover:text-ok",
          className
        )}
      >
        {isChecking ? (
          <>
            <Loader2 className="size-4 animate-spin text-muted-foreground" />
            <span>Đang kiểm tra...</span>
          </>
        ) : isSaved ? (
          <>
            <BookmarkCheckIcon className="size-4 text-ok" />
            <span>Đã trong mục tiêu</span>
          </>
        ) : (
          <>
            <BookmarkIcon className="size-4 text-brand" />
            <span>Lưu mục tiêu</span>
          </>
        )}
      </Button>
      )}

      {/* Modal [UC-30] Thêm trường mục tiêu */}
      <Dialog
        open={isOpenAdd}
        onOpenChange={(next) => !isSubmitting && setIsOpenAdd(next)}
        disablePointerDismissal={isSubmitting}
      >
        <DialogContent variant="form" size="md">
          <form onSubmit={handleSubmitAdd} noValidate className="flex min-h-0 flex-1 flex-col gap-6">
            <DialogHeader>
              <DialogIcon tone="brand">
                <BookmarkPlusIcon />
              </DialogIcon>
              <DialogTitle>Lưu vào danh sách mục tiêu</DialogTitle>
              <DialogDescription>
                Thêm <span className="font-semibold text-foreground">{universityName}</span>
                {locationStr && ` (${locationStr})`} vào kế hoạch nộp hồ sơ của bạn.
              </DialogDescription>
            </DialogHeader>

            <DialogBody className="space-y-4">
              {error && (
                <div className="rounded-md border border-risk/25 bg-risk-wash p-3 text-sm text-risk">
                  {error}
                </div>
              )}

              <div className="space-y-3">
                <Field>
                  <FieldLabel className="text-sm font-semibold">Phân loại mục tiêu</FieldLabel>
                  <RadioGroup
                    value={category}
                    onValueChange={(val) => {
                      const next = String(val);
                      setCategory(next);
                      setUserPickedCategory(next !== recommendedCategory);
                    }}
                    className="gap-2 pt-1"
                  >
                    {CATEGORY_OPTIONS.map((opt) => (
                      <label
                        key={opt.value}
                        htmlFor={`cat-${opt.value}`}
                        className={cn(
                          "flex cursor-pointer flex-col rounded-xl border p-3 transition-colors",
                          category === opt.value
                            ? "border-brand bg-brand/5 text-foreground"
                            : "border-border hover:bg-muted/50"
                        )}
                      >
                        <div className="flex items-center justify-between gap-2">
                          <div className="flex items-center gap-2.5">
                            <RadioGroupItem id={`cat-${opt.value}`} value={opt.value} />
                            <span className="font-semibold text-sm" title={opt.englishTerm}>{opt.title}</span>
                          </div>
                          {recommendedCategory === opt.value && (
                            <Badge variant="outline" className="text-[11px] font-medium">
                              Theo gợi ý gần nhất
                            </Badge>
                          )}
                        </div>
                        <p className="mt-1 pl-6.5 text-xs text-muted-foreground">{opt.desc}</p>
                      </label>
                    ))}
                  </RadioGroup>
                </Field>

                <Field>
                  <FieldLabel className="text-sm font-semibold">Vòng nộp dự kiến (Tùy chọn)</FieldLabel>
                  <Select
                    value={round || "none"}
                    onValueChange={(val) => {
                      if (val !== null) {
                        setRound(val === "none" ? "" : String(val));
                        setConfirmBindingAdd(false);
                      }
                    }}
                    items={addRoundOptions}
                  >
                    <SelectTrigger className="w-full text-sm">
                      <SelectValue placeholder="-- Chưa chọn vòng nộp --" />
                    </SelectTrigger>
                    <SelectContent>
                      {addRoundOptions.map((r) => (
                        <SelectItem key={r.value} value={r.value}>
                          {r.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>

                  {/* [UC-32 3a/E1] Cảnh báo ràng buộc Early Decision / Early Decision II */}
                  {isBindingRound(round) && (
                    <div className="mt-2 rounded-lg border border-warn/40 bg-warn-wash/60 p-3 text-xs space-y-2 text-foreground">
                      <p className="font-semibold text-warn">
                        Cảnh báo vòng ràng buộc ({round})
                      </p>
                      <p>
                        Đây là vòng nộp ràng buộc: nếu trúng tuyển, bạn phải nhập học tại trường này và rút toàn bộ hồ sơ đã nộp ở các trường khác.
                      </p>
                      {isScholarshipMandatory && (
                        <p className="font-medium text-risk">
                          Lưu ý rủi ro tài chính: Hồ sơ của bạn đang chọn nhu cầu học bổng &ldquo;Bắt buộc&rdquo;. Nộp {round} có thể gây áp lực tài chính nếu gói hỗ trợ của trường không đủ trang trải.
                        </p>
                      )}
                      {round === "Early Decision" && existingEdSchoolName && existingEdSchoolName !== universityName && (
                        <p className="font-semibold text-risk">
                          Bạn đã chọn Early Decision cho {existingEdSchoolName}. Mỗi học sinh chỉ được chọn tối đa một trường ở vòng này.
                        </p>
                      )}
                      {round === "Early Decision II" && existingEd2SchoolName && existingEd2SchoolName !== universityName && (
                        <p className="font-semibold text-risk">
                          Bạn đã chọn Early Decision II cho {existingEd2SchoolName}. Mỗi học sinh chỉ được chọn tối đa một trường ở vòng này.
                        </p>
                      )}
                      <label className="flex items-start gap-2 cursor-pointer pt-1">
                        <Checkbox
                          checked={confirmBindingAdd}
                          onCheckedChange={(c) => setConfirmBindingAdd(c === true)}
                          className="mt-0.5"
                        />
                        <span>Tôi đã hiểu quy định ràng buộc của {round} và xác nhận lưu vòng nộp này.</span>
                      </label>
                    </div>
                  )}

                  {/* [UC-32 3b] Cảnh báo Restrictive / Single-Choice Early Action */}
                  {isRestrictiveRound(round) && (
                    <div className="mt-2 rounded-lg border border-brand/30 bg-brand-wash/50 p-3 text-xs space-y-1 text-foreground">
                      <p className="font-semibold text-brand">Lưu ý vòng nộp sớm có giới hạn (REA / SCEA)</p>
                      <p>
                        Vòng Restrictive / Single-Choice Early Action không bắt buộc nhập học nhưng thường giới hạn việc nộp sớm (EA/ED) sang các trường tư thục khác theo quy định riêng của trường.
                      </p>
                    </div>
                  )}
                </Field>

                <Field>
                  <FieldLabel className="text-sm font-semibold">Ghi chú cá nhân (Tùy chọn)</FieldLabel>
                  <Textarea
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    placeholder="Ví dụ: Ngành Computer Science, cần xin hỗ trợ tài chính..."
                    maxLength={1000}
                    rows={3}
                    className="resize-none text-sm"
                  />
                </Field>
              </div>
            </DialogBody>

            <DialogFooter layout="pair">
              <DialogCancel disabled={isSubmitting}>Hủy</DialogCancel>
              <DialogAction
                type="submit"
                tone="primary"
                loading={isSubmitting}
                disabled={isBindingRound(round) && !confirmBindingAdd}
              >
                Lưu
              </DialogAction>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Modal [UC-31] Quản lý & Xóa trường mục tiêu */}
      <Dialog
        open={isOpenManage}
        onOpenChange={(next) => !(isDeleting || isUpdatingRound || isUpdatingCategory) && setIsOpenManage(next)}
        disablePointerDismissal={isDeleting || isUpdatingRound || isUpdatingCategory}
      >
        <DialogContent variant="form" size="md">
          <DialogHeader>
            <DialogIcon tone="brand">
              <BookmarkCheckIcon />
            </DialogIcon>
            <DialogTitle>Thông tin trường mục tiêu</DialogTitle>
            <DialogDescription>
              <span className="font-semibold text-foreground">{universityName}</span> đang nằm trong danh sách mục tiêu của bạn.
            </DialogDescription>
          </DialogHeader>

          <DialogBody className="space-y-4">
            {error && (
              <div className="rounded-md border border-risk/25 bg-risk-wash p-3 text-sm text-risk">
                {error}
              </div>
            )}

            {savedTarget && (
              <div className="space-y-3 rounded-lg border border-line bg-muted/30 p-3.5 text-sm">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-muted-foreground">Phân loại:</span>
                  <div className="flex flex-wrap items-center justify-end gap-1.5">
                    <Badge variant={getCategoryLabel(savedTarget.category).color}>
                      {getCategoryLabel(savedTarget.category).label}
                    </Badge>
                    {savedTarget.isCategoryUserSelected && (
                      <Badge variant="outline" className="text-[11px]">
                        Do bạn chọn
                      </Badge>
                    )}
                    {!isEditingCategory && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => {
                          setSelectedEditCategory(savedTarget.category || "match");
                          setIsEditingCategory(true);
                        }}
                        className="text-xs text-brand h-6 px-1.5 hover:underline"
                      >
                        Đổi nhóm
                      </Button>
                    )}
                  </div>
                </div>

                {isEditingCategory && (
                  <div className="rounded-md border border-brand/30 bg-brand/5 p-2.5 space-y-2 mt-1">
                    <span className="text-xs font-semibold text-foreground block">
                      Chọn nhóm phân loại
                    </span>
                    <div className="grid grid-cols-3 gap-1.5">
                      {CATEGORY_OPTIONS.map((opt) => (
                        <Button
                          key={opt.value}
                          type="button"
                          size="xs"
                          variant={selectedEditCategory === opt.value ? "default" : "outline"}
                          onClick={() => setSelectedEditCategory(opt.value)}
                          className="text-xs justify-center h-7"
                          disabled={isUpdatingCategory}
                        >
                          {opt.title}
                        </Button>
                      ))}
                    </div>
                    <div className="flex items-center justify-end gap-1.5 pt-1">
                      <Button
                        type="button"
                        size="xs"
                        variant="ghost"
                        onClick={() => setIsEditingCategory(false)}
                        disabled={isUpdatingCategory}
                        className="h-6 text-xs"
                      >
                        Hủy
                      </Button>
                      <Button
                        type="button"
                        size="xs"
                        onClick={() => handleUpdateCategory(selectedEditCategory)}
                        disabled={!selectedEditCategory}
                        loading={isUpdatingCategory}
                        className="h-6 gap-1 text-xs"
                      >
                        Lưu nhóm
                      </Button>
                    </div>
                  </div>
                )}

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
                          const defaultRound = savedTarget.applicationRound && manageRoundOptions.some((r) => r.value === savedTarget.applicationRound)
                            ? savedTarget.applicationRound
                            : (manageRoundOptions[0]?.value || "Regular Decision");
                          setSelectedEditRound(defaultRound);
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
                      Chọn vòng nộp hồ sơ
                    </span>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                      {manageRoundOptions.map((r) => (
                        <Button
                          key={r.value}
                          type="button"
                          size="xs"
                          variant={selectedEditRound === r.value ? "default" : "outline"}
                          onClick={() => {
                            setSelectedEditRound(r.value);
                            setConfirmBindingEdit(false);
                          }}
                          className="text-xs justify-start h-7"
                          disabled={isUpdatingRound}
                        >
                          {r.label}
                        </Button>
                      ))}
                    </div>

                    {/* [UC-32 3a/E1] Cảnh báo vòng ràng buộc khi đổi vòng nộp */}
                    {isBindingRound(selectedEditRound) && (
                      <div className="rounded-md border border-warn/40 bg-warn-wash/60 p-2.5 text-xs space-y-1.5 text-foreground">
                        <p className="font-semibold text-warn">Cảnh báo vòng ràng buộc ({selectedEditRound})</p>
                        <p>
                          Trúng tuyển vòng này bắt buộc phải nhập học và rút các hồ sơ khác.
                        </p>
                        {isScholarshipMandatory && (
                          <p className="font-medium text-risk">
                            Rủi ro tài chính: Hồ sơ của bạn cần học bổng &ldquo;Bắt buộc&rdquo; — cân nhắc kỹ trước khi nộp {selectedEditRound}.
                          </p>
                        )}
                        {selectedEditRound === "Early Decision" && existingEdSchoolName && existingEdSchoolName !== universityName && (
                          <p className="font-semibold text-risk">
                            Bạn đã chọn Early Decision cho {existingEdSchoolName} (tối đa 1 trường).
                          </p>
                        )}
                        {selectedEditRound === "Early Decision II" && existingEd2SchoolName && existingEd2SchoolName !== universityName && (
                          <p className="font-semibold text-risk">
                            Bạn đã chọn Early Decision II cho {existingEd2SchoolName} (tối đa 1 trường).
                          </p>
                        )}
                        <label className="flex items-start gap-2 cursor-pointer pt-0.5">
                          <Checkbox
                            checked={confirmBindingEdit}
                            onCheckedChange={(c) => setConfirmBindingEdit(c === true)}
                            className="mt-0.5"
                          />
                          <span>Tôi đã hiểu cam kết ràng buộc và xác nhận chọn vòng này.</span>
                        </label>
                      </div>
                    )}

                    {/* [UC-32 3b] Cảnh báo REA / SCEA */}
                    {isRestrictiveRound(selectedEditRound) && (
                      <div className="rounded-md border border-brand/30 bg-brand-wash/50 p-2.5 text-xs text-foreground">
                        <span className="font-semibold text-brand">Lưu ý REA/SCEA: </span>
                        Vòng nộp sớm có giới hạn nộp sớm vào các trường khác theo quy định của trường.
                      </div>
                    )}

                    <div className="flex items-center justify-end gap-1.5 pt-1">
                      <Button
                        type="button"
                        size="xs"
                        variant="ghost"
                        onClick={() => {
                          setIsEditingRound(false);
                          setConfirmBindingEdit(false);
                        }}
                        disabled={isUpdatingRound}
                        className="h-6 text-xs"
                      >
                        Hủy
                      </Button>
                      <Button
                        type="button"
                        size="xs"
                        onClick={() => handleUpdateRound(selectedEditRound)}
                        disabled={!selectedEditRound || (isBindingRound(selectedEditRound) && !confirmBindingEdit)}
                        loading={isUpdatingRound}
                        className="h-6 gap-1 text-xs"
                      >
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

            {confirmDelete && (
              <div className="rounded-lg border border-destructive/20 bg-destructive/5 p-3.5 space-y-2">
                <div className="flex items-start gap-2 text-destructive text-sm font-medium">
                  <AlertCircleIcon className="size-4 shrink-0 mt-0.5" />
                  <span>Xác nhận xóa trường này khỏi danh sách mục tiêu?</span>
                </div>
                <p className="text-xs text-muted-foreground pl-6">
                  Trường này sẽ được gỡ khỏi kế hoạch theo dõi và dòng thời gian nộp đơn của bạn.
                </p>
              </div>
            )}
          </DialogBody>

          {confirmDelete ? (
            <DialogFooter layout="pair">
              <DialogCancel asClose={false} onClick={() => setConfirmDelete(false)} disabled={isDeleting}>
                Hủy
              </DialogCancel>
              <DialogAction
                type="button"
                tone="destructive"
                onClick={handleDeleteTargetSchool}
                loading={isDeleting}
              >
                Đồng ý xóa
              </DialogAction>
            </DialogFooter>
          ) : (
            <DialogFooter layout="pair">
              <DialogCancel disabled={isDeleting || isUpdatingRound || isUpdatingCategory}>
                Đóng
              </DialogCancel>
              <DialogAction
                type="button"
                tone="destructive-tint"
                onClick={() => setConfirmDelete(true)}
                disabled={isUpdatingRound || isUpdatingCategory}
              >
                Xóa khỏi mục tiêu
              </DialogAction>
            </DialogFooter>
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
