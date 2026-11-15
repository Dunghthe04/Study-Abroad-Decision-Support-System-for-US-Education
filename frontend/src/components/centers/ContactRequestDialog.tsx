"use client";

// [UC-42] Gửi yêu cầu liên hệ trung tâm. Giao diện theo ConsultationBookingModal (Dương),
// nhưng gọi API thật: học sinh tự gửi, hoặc phụ huynh chọn con đã liên kết rồi gửi thay.
// Server vẫn kiểm tra lại tuổi, liên kết và giới hạn mỗi lần gửi; form chỉ ẩn nút và giải thích.

import { useState } from "react";
import Link from "next/link";
import { Button, buttonVariants } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { CheckCircle2Icon, MessageSquareIcon, ShieldAlertIcon, SparklesIcon } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { AGE_FEATURES, useAgeBlock } from "@/lib/age-features";
import { ApiError, parentLinksApi } from "@/lib/api";
import { getLeadRuleError, leadApi } from "@/lib/lead-api";
import { STUDY_LEVELS, studyLevelLabel } from "@/lib/study-levels";
import type { ParentLinkDto } from "@/types/api";
import { LEAD_SHARED_FIELDS, LEAD_SHARED_FIELD_INFO, type LeadSharedField } from "@/types/lead";

interface ContactRequestDialogProps {
  centerId: string;
  centerName: string;
  isVerified: boolean;
  /** Nút nhỏ (viền) dùng dưới thẻ trung tâm trong chat; mặc định là nút lớn ở trang chi tiết trung tâm. */
  compact?: boolean;
}

const SELECT_CLASS =
  "flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring";

const DEFAULT_SHARED_FIELDS = LEAD_SHARED_FIELDS.filter((f) => LEAD_SHARED_FIELD_INFO[f].defaultChecked);

function formatRetryAt(iso: string): string {
  return new Date(iso).toLocaleString("vi-VN", { dateStyle: "short", timeStyle: "short" });
}

/** Lỗi 429 hiện ngày được gửi lại; lỗi 400/403 hiện message server trả về. */
function toErrorMessage(error: unknown): string {
  const rule = getLeadRuleError(error);
  if (rule) {
    if (rule.code === "center_cooldown" && rule.retryAt) {
      return `Bạn đã gửi yêu cầu cho trung tâm này trong 7 ngày gần đây. Có thể gửi lại từ ${formatRetryAt(rule.retryAt)}.`;
    }
    return rule.message;
  }
  if (error instanceof ApiError) return error.message;
  return "Không gửi được yêu cầu. Vui lòng thử lại.";
}

export function ContactRequestDialog({ centerId, centerName, isVerified, compact = false }: ContactRequestDialogProps) {
  const { user, isLoading } = useAuth();
  const ageBlock = useAgeBlock(AGE_FEATURES.shareWithCenter);

  const [open, setOpen] = useState(false);
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Phụ huynh: danh sách con đã liên kết (tải khi mở form)
  const [children, setChildren] = useState<ParentLinkDto[] | null>(null);
  const [childrenError, setChildrenError] = useState<string | null>(null);
  const [studentUserId, setStudentUserId] = useState("");

  // Form states
  const [sharedFields, setSharedFields] = useState<LeadSharedField[]>(DEFAULT_SHARED_FIELDS);
  const [phone, setPhone] = useState("");
  const [studyLevel, setStudyLevel] = useState("undergraduate");
  const [note, setNote] = useState("");
  const [consentAccepted, setConsentAccepted] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);

  // Trung tâm chưa xác minh thì không nhận yêu cầu (server cũng chặn center_not_verified)
  if (!isVerified || isLoading) return null;

  // Chưa đăng nhập: dẫn đi đăng nhập
  if (!user) {
    return (
      <Link
        href="/login"
        className={
          compact
            ? buttonVariants({ variant: "outline", size: "sm" })
            : buttonVariants({ size: "lg", className: "gap-2 bg-brand text-white hover:bg-brand/90 font-medium" })
        }
      >
        {!compact && <MessageSquareIcon className="w-4 h-4 text-white" />}
        {compact ? "Đăng nhập để liên hệ" : "Đăng nhập để gửi yêu cầu liên hệ"}
      </Link>
    );
  }

  // Chỉ học sinh và phụ huynh gửi được
  if (user.role !== "student" && user.role !== "parent") return null;

  const isParent = user.role === "parent";
  const isMinorStudent = !isParent && user.ageGroup === "minor";
  const activeChildren = (children ?? []).filter((c) => c.status === "active");

  const loadChildren = async () => {
    setChildrenError(null);
    try {
      const res = await parentLinksApi.getMine();
      const active = res.data.filter((c) => c.status === "active");
      setChildren(res.data);
      setStudentUserId((current) => current || (active.length === 1 ? active[0].studentUserId : ""));
    } catch (err) {
      setChildrenError(err instanceof ApiError ? err.message : "Không tải được danh sách con đã liên kết.");
    }
  };

  const handleOpen = () => {
    setOpen(true);
    setError(null);
    if (isParent && children === null) void loadChildren();
  };

  const toggleField = (field: LeadSharedField, checked: boolean) => {
    if (LEAD_SHARED_FIELD_INFO[field].locked) return;
    setSharedFields((current) =>
      checked ? LEAD_SHARED_FIELDS.filter((f) => f === field || current.includes(f)) : current.filter((f) => f !== field),
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await leadApi.create({
        studyCenterId: centerId,
        studyLevel: studyLevel || null,
        contactPhone: phone.trim() || null,
        message: note.trim() || null,
        studentUserId: isParent ? studentUserId : null,
        sharedFields,
        consentAccepted,
        acknowledgedCenterContact: isMinorStudent ? acknowledged : false,
      });
      setIsSubmitted(true);
    } catch (err) {
      setError(toErrorMessage(err));
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setIsSubmitted(false);
    setOpen(false);
    setError(null);
    setSharedFields(DEFAULT_SHARED_FIELDS);
    setPhone("");
    setNote("");
    setConsentAccepted(false);
    setAcknowledged(false);
  };

  const canSubmit =
    !loading &&
    consentAccepted &&
    (!isMinorStudent || acknowledged) &&
    (!isParent || studentUserId !== "");

  const renderBody = () => {
    // Học sinh dưới 16: không tự gửi được (server trả age_restricted)
    if (ageBlock) {
      return (
        <div className="py-4 text-center space-y-3">
          <div className="w-12 h-12 bg-amber-500/10 text-amber-600 rounded-full flex items-center justify-center mx-auto">
            <ShieldAlertIcon className="w-6 h-6" />
          </div>
          <DialogHeader>
            <DialogTitle className="text-lg text-center">Chức năng dành cho học sinh từ 16 tuổi</DialogTitle>
            <DialogDescription className="text-center text-sm pt-1">
              Nhờ phụ huynh gửi bằng tài khoản phụ huynh. Phụ huynh đã liên kết với bạn có thể chọn bạn và gửi yêu cầu cho <strong>{centerName}</strong>.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button type="button" variant="outline" className="w-full" onClick={() => setOpen(false)}>
              Đóng
            </Button>
          </DialogFooter>
        </div>
      );
    }

    // Phụ huynh chưa có con liên kết: hướng dẫn đi liên kết trước
    if (isParent && children !== null && activeChildren.length === 0) {
      return (
        <div className="py-4 text-center space-y-3">
          <DialogHeader>
            <DialogTitle className="text-lg text-center">Chưa có con đã liên kết</DialogTitle>
            <DialogDescription className="text-center text-sm pt-1">
              Phụ huynh gửi yêu cầu thay con, nên cần liên kết với tài khoản của con trước. Vào trang tài khoản để xem lời mời liên kết.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Đóng
            </Button>
            <Link href="/account" className={buttonVariants()}>
              Quản lý liên kết
            </Link>
          </DialogFooter>
        </div>
      );
    }

    return (
      <form onSubmit={handleSubmit}>
        <DialogHeader>
          <div className="flex items-center gap-2 text-brand text-xs font-semibold">
            <SparklesIcon className="w-3.5 h-3.5" />
            Kết nối trực tiếp trung tâm
          </div>
          <DialogTitle className="text-xl">Gửi yêu cầu liên hệ</DialogTitle>
          <DialogDescription className="text-xs">
            <strong>{centerName}</strong> sẽ nhận các thông tin bạn chọn bên dưới và liên hệ lại để tư vấn.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3.5 py-4 max-h-[60vh] overflow-y-auto pr-1">
          {isParent && (
            <div className="space-y-1.5">
              <Label htmlFor="cr-child" className="text-xs font-medium">
                Gửi thay cho con <span className="text-destructive">*</span>
              </Label>
              {children === null && !childrenError ? (
                <p className="text-xs text-muted-foreground">Đang tải danh sách con đã liên kết...</p>
              ) : childrenError ? (
                <p className="text-xs text-destructive">{childrenError}</p>
              ) : (
                <select
                  id="cr-child"
                  className={SELECT_CLASS}
                  value={studentUserId}
                  onChange={(e) => setStudentUserId(e.target.value)}
                  required
                >
                  <option value="" disabled>
                    Chọn con
                  </option>
                  {activeChildren.map((c) => (
                    <option key={c.id} value={c.studentUserId}>
                      {c.studentName}
                      {c.studentAge !== null ? ` (${c.studentAge} tuổi)` : ""}
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="cr-phone" className="text-xs font-medium">
                Số điện thoại
              </Label>
              <Input
                id="cr-phone"
                type="tel"
                placeholder="0988 123 456"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                maxLength={20}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="cr-level" className="text-xs font-medium">
                Bậc học mong muốn
              </Label>
              <select id="cr-level" className={SELECT_CLASS} value={studyLevel} onChange={(e) => setStudyLevel(e.target.value)}>
                {STUDY_LEVELS.map((level) => (
                  <option key={level} value={level}>
                    {studyLevelLabel(level)}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="cr-note" className="text-xs font-medium">
              Lời nhắn cho trung tâm
            </Label>
            <Textarea
              id="cr-note"
              placeholder="Ví dụ: Em muốn nộp ngành CS kỳ Thu 2027, cần tư vấn chọn trường và bài luận..."
              rows={2}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={2000}
            />
          </div>

          <div className="space-y-1.5">
            <p className="text-xs font-medium">
              Thông tin {isParent ? "của con " : ""}chia sẻ cho trung tâm
            </p>
            <div className="grid gap-2">
              {LEAD_SHARED_FIELDS.map((field) => {
                const info = LEAD_SHARED_FIELD_INFO[field];
                return (
                  <Label
                    key={field}
                    className="items-start border rounded-md p-2 hover:bg-muted/50 cursor-pointer font-normal"
                  >
                    <Checkbox
                      className="mt-0.5"
                      checked={sharedFields.includes(field)}
                      disabled={info.locked}
                      onCheckedChange={(checked) => toggleField(field, checked)}
                    />
                    <span className="space-y-0.5">
                      <span className="block text-xs font-medium">
                        {info.label}
                        {info.locked && <span className="text-muted-foreground font-normal"> (bắt buộc)</span>}
                      </span>
                      <span className="block text-xs text-muted-foreground">{info.description}</span>
                    </span>
                  </Label>
                );
              })}
            </div>
          </div>

          <Label className="items-start text-xs font-normal leading-snug cursor-pointer">
            <Checkbox className="mt-0.5" checked={consentAccepted} onCheckedChange={setConsentAccepted} />
            <span>
              Tôi đồng ý chia sẻ các thông tin đã chọn cho trung tâm <span className="text-destructive">*</span>
            </span>
          </Label>

          {isMinorStudent && (
            <Label className="items-start text-xs font-normal leading-snug cursor-pointer">
              <Checkbox className="mt-0.5" checked={acknowledged} onCheckedChange={setAcknowledged} />
              <span>
                Tôi hiểu trung tâm sẽ nhận thông tin này và liên hệ lại với tôi <span className="text-destructive">*</span>
              </span>
            </Label>
          )}

          {error && (
            <p role="alert" className="text-xs text-destructive border border-destructive/30 bg-destructive/5 rounded-md p-2">
              {error}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => setOpen(false)}>
            Hủy
          </Button>
          <Button type="submit" disabled={!canSubmit}>
            {loading ? "Đang gửi..." : "Gửi yêu cầu"}
          </Button>
        </DialogFooter>
      </form>
    );
  };

  return (
    <>
      {compact ? (
        <Button type="button" variant="outline" size="sm" onClick={handleOpen}>
          Liên hệ
        </Button>
      ) : (
        <Button size="lg" onClick={handleOpen} className="gap-2 bg-brand text-white hover:bg-brand/90 font-medium ">
          <MessageSquareIcon className="w-4 h-4 text-white" />
          Gửi yêu cầu liên hệ
        </Button>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          {isSubmitted ? (
            <div className="py-6 text-center space-y-4">
              <div className="w-14 h-14 bg-emerald-500/10 text-emerald-600 rounded-full flex items-center justify-center mx-auto">
                <CheckCircle2Icon className="w-8 h-8" />
              </div>
              <DialogHeader>
                <DialogTitle className="text-xl text-center">Gửi yêu cầu thành công!</DialogTitle>
                <DialogDescription className="text-center text-sm pt-2">
                  Hệ thống đã chuyển thông tin đã chọn tới <strong>{centerName}</strong>. Trung tâm sẽ gọi điện thoại hoặc gửi email để liên hệ lại.
                </DialogDescription>
              </DialogHeader>
              <div className="pt-4">
                <Button onClick={handleReset} className="w-full">
                  Hoàn tất
                </Button>
              </div>
            </div>
          ) : (
            renderBody()
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
