"use client";

import React, { useState } from "react";
import { CakeIcon } from "lucide-react";
import { birthDateRequestsApi, ApiError } from "@/lib/api";
import { emailError } from "@/lib/auth-validation";
import type { BirthDateChangeDto, BirthDateEvidenceType, UserDto } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
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

const MAX_REASON_LENGTH = 500;
const MAX_EVIDENCE_BYTES = 5 * 1024 * 1024;
const CHILD_MAX_AGE_EXCLUSIVE = 16;
const ADULT_AGE = 18;
const STUDENT_MIN_AGE = 13;
const MAX_AGE = 100;
const EVIDENCE_ACCEPT = "image/jpeg,image/png,application/pdf,.jpg,.jpeg,.png,.pdf";

export const EVIDENCE_TYPE_LABELS: Record<BirthDateEvidenceType, string> = {
  birth_certificate: "Giấy khai sinh",
  citizen_id: "Căn cước công dân",
  passport: "Hộ chiếu",
  student_card: "Thẻ học sinh",
};

const EVIDENCE_TYPE_ITEMS = (Object.keys(EVIDENCE_TYPE_LABELS) as BirthDateEvidenceType[]).map((type) => ({
  value: type,
  label: EVIDENCE_TYPE_LABELS[type],
}));

type FieldErrors = Partial<Record<"birthDate" | "reason" | "evidenceType" | "evidence" | "representativeEmail", string>>;

/** Hôm nay theo giờ Việt Nam (UTC+7), dạng yyyy-MM-dd. */
function todayInVietnam(): string {
  return new Date(Date.now() + 7 * 3600 * 1000).toISOString().slice(0, 10);
}

/** Tuổi tròn tại ngày `today` (cả hai dạng yyyy-MM-dd), giống AgePolicy.GetAge ở server. */
export function ageOn(birthDate: string, today: string): number | null {
  const b = /^(\d{4})-(\d{2})-(\d{2})$/.exec(birthDate);
  const t = /^(\d{4})-(\d{2})-(\d{2})$/.exec(today);
  if (!b || !t) return null;
  let age = Number(t[1]) - Number(b[1]);
  if (t[2] + t[3] < b[2] + b[3]) age -= 1;
  return age;
}

/** Tuổi lên từ dưới mốc tới từ mốc trở lên; tuổi cũ không rõ thì chỉ xét tuổi mới (như server). */
function crossesUp(oldAge: number | null, newAge: number, threshold: number): boolean {
  return newAge >= threshold && (oldAge === null || oldAge < threshold);
}

interface BirthDateRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  user: UserDto;
  /** Gửi xong (hoặc server báo đã có yêu cầu chờ): đưa yêu cầu về cho thẻ Thông tin cá nhân hiện. */
  onSubmitted: (request: BirthDateChangeDto, message: string) => void;
}

/**
 * [UC-105 3b / UC-107] Học sinh, Phụ huynh gửi yêu cầu sửa ngày sinh.
 * Kiểm vượt mốc 16 / 18 ở trình duyệt chỉ để hiện ô minh chứng và email người đại diện đúng lúc; server kiểm lại.
 */
export function BirthDateRequestDialog({ open, onOpenChange, user, onSubmitted }: BirthDateRequestDialogProps) {
  const [birthDate, setBirthDate] = useState("");
  const [reason, setReason] = useState("");
  const [evidenceType, setEvidenceType] = useState<BirthDateEvidenceType | "">("");
  const [evidence, setEvidence] = useState<File | null>(null);
  const [representativeEmail, setRepresentativeEmail] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  // Đổi key để xóa tệp đã chọn trong ô input file khi mở lại
  const [fileInputKey, setFileInputKey] = useState(0);

  const today = todayInVietnam();
  const oldAge = user.dateOfBirth ? ageOn(user.dateOfBirth, today) : null;
  const newAge = birthDate ? ageOn(birthDate, today) : null;
  const evidenceRequired =
    newAge !== null &&
    (crossesUp(oldAge, newAge, CHILD_MAX_AGE_EXCLUSIVE) || crossesUp(oldAge, newAge, ADULT_AGE));
  const needsRepresentative =
    user.role === "student" &&
    newAge !== null &&
    newAge < CHILD_MAX_AGE_EXCLUSIVE &&
    user.parentConsentStatus !== "granted";

  const reset = () => {
    setBirthDate("");
    setReason("");
    setEvidenceType("");
    setEvidence(null);
    setRepresentativeEmail("");
    setFieldErrors({});
    setError(null);
    setFileInputKey((k) => k + 1);
  };

  const handleOpenChange = (next: boolean) => {
    if (submitting) return;
    if (!next) reset();
    onOpenChange(next);
  };

  const clearFieldError = (key: keyof FieldErrors) =>
    setFieldErrors((prev) => (prev[key] ? { ...prev, [key]: undefined } : prev));

  const validate = (): FieldErrors => {
    const errors: FieldErrors = {};
    if (!birthDate || newAge === null) errors.birthDate = "Vui lòng nhập ngày sinh mới hợp lệ.";
    else if (birthDate > today) errors.birthDate = "Ngày sinh không được ở tương lai.";
    else if (birthDate === user.dateOfBirth) errors.birthDate = "Ngày sinh mới trùng với ngày sinh hiện tại.";
    else if (newAge > MAX_AGE) errors.birthDate = `Tuổi không được lớn hơn ${MAX_AGE}.`;
    else if (user.role === "parent" && newAge < ADULT_AGE) errors.birthDate = `Tài khoản phụ huynh phải từ ${ADULT_AGE} tuổi.`;
    else if (user.role === "student" && newAge < STUDENT_MIN_AGE) errors.birthDate = `Học sinh phải từ ${STUDENT_MIN_AGE} tuổi.`;

    const trimmedReason = reason.trim();
    if (!trimmedReason) errors.reason = "Vui lòng nhập lý do sửa ngày sinh.";
    else if (trimmedReason.length > MAX_REASON_LENGTH) errors.reason = `Lý do tối đa ${MAX_REASON_LENGTH} ký tự.`;

    if (evidenceRequired) {
      if (!evidence) errors.evidence = "Vui lòng tải lên minh chứng ngày sinh.";
      else if (evidence.size > MAX_EVIDENCE_BYTES) errors.evidence = "Tệp minh chứng tối đa 5 MB.";
      if (!evidenceType) errors.evidenceType = "Vui lòng chọn loại giấy tờ minh chứng.";
    }

    if (needsRepresentative) {
      const emailMessage = emailError(representativeEmail, "Email người đại diện");
      if (emailMessage) errors.representativeEmail = emailMessage;
      else if (representativeEmail.trim().toLowerCase() === user.email.toLowerCase())
        errors.representativeEmail = "Email người đại diện phải khác email của bạn.";
    }
    return errors;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    const errors = validate();
    setFieldErrors(errors);
    if (Object.values(errors).some(Boolean)) return;

    setSubmitting(true);
    try {
      const res = await birthDateRequestsApi.submit({
        birthDate,
        reason: reason.trim(),
        evidenceType: evidenceRequired && evidenceType ? evidenceType : null,
        evidence: evidenceRequired ? evidence : null,
        representativeEmail: needsRepresentative ? representativeEmail : null,
      });
      reset();
      onSubmitted(res.data, res.message || "Đã gửi yêu cầu sửa ngày sinh.");
      onOpenChange(false);
    } catch (err) {
      // E5: đã có yêu cầu chờ duyệt (gửi ở tab khác) thì hiện lại yêu cầu đó
      const pending = err instanceof ApiError && err.status === 409
        ? (err.body as { data?: BirthDateChangeDto } | null)?.data
        : undefined;
      if (pending) {
        reset();
        onSubmitted(pending, err instanceof ApiError ? err.message : "Bạn đã có yêu cầu đang chờ duyệt.");
        onOpenChange(false);
        return;
      }
      setError(
        err instanceof ApiError
          ? err.message || "Không gửi được yêu cầu."
          : "Có lỗi kết nối máy chủ. Vui lòng thử lại sau.",
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange} disablePointerDismissal={submitting}>
      <DialogContent variant="form" size="md">
        <form onSubmit={handleSubmit} noValidate className="flex min-h-0 flex-1 flex-col gap-6">
          <DialogHeader>
            <DialogIcon tone="brand">
              <CakeIcon aria-hidden />
            </DialogIcon>
            <DialogTitle>Yêu cầu sửa ngày sinh</DialogTitle>
            <DialogDescription>
              Administrator sẽ xem xét yêu cầu. Ngày sinh chỉ đổi khi yêu cầu được duyệt.
            </DialogDescription>
          </DialogHeader>

          <DialogBody className="space-y-4">
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}

            <FieldGroup className="gap-4">
            <Field data-invalid={!!fieldErrors.birthDate || undefined}>
              <FieldLabel htmlFor="birth-request-date">
                Ngày sinh mới <span className="text-destructive">*</span>
              </FieldLabel>
              <Input
                id="birth-request-date"
                type="date"
                value={birthDate}
                max={today}
                aria-invalid={!!fieldErrors.birthDate || undefined}
                aria-describedby={fieldErrors.birthDate ? "birth-request-date-error" : undefined}
                onChange={(e) => {
                  setBirthDate(e.target.value);
                  clearFieldError("birthDate");
                }}
                disabled={submitting}
                className="h-10"
              />
              {newAge !== null && !fieldErrors.birthDate && birthDate <= today && (
                <FieldDescription>Tuổi theo ngày sinh mới: {newAge}.</FieldDescription>
              )}
              {fieldErrors.birthDate && <FieldError id="birth-request-date-error">{fieldErrors.birthDate}</FieldError>}
            </Field>

            <Field data-invalid={!!fieldErrors.reason || undefined}>
              <FieldLabel htmlFor="birth-request-reason">
                Lý do <span className="text-destructive">*</span>
              </FieldLabel>
              <Textarea
                id="birth-request-reason"
                value={reason}
                maxLength={MAX_REASON_LENGTH}
                rows={3}
                placeholder="Ví dụ: Khai nhầm năm sinh khi đăng ký"
                aria-invalid={!!fieldErrors.reason || undefined}
                aria-describedby={fieldErrors.reason ? "birth-request-reason-error" : undefined}
                onChange={(e) => {
                  setReason(e.target.value);
                  clearFieldError("reason");
                }}
                disabled={submitting}
              />
              {fieldErrors.reason && <FieldError id="birth-request-reason-error">{fieldErrors.reason}</FieldError>}
            </Field>

            {evidenceRequired && (
              <>
                <Alert variant="warning">
                  <AlertDescription>
                    Ngày sinh mới làm tuổi vượt mốc {CHILD_MAX_AGE_EXCLUSIVE} hoặc {ADULT_AGE}, cần tải lên minh chứng.
                    Bạn có thể che số định danh, chỉ cần rõ họ tên và ngày sinh.
                  </AlertDescription>
                </Alert>

                <Field data-invalid={!!fieldErrors.evidenceType || undefined}>
                  <FieldLabel htmlFor="birth-request-evidence-type">
                    Loại giấy tờ <span className="text-destructive">*</span>
                  </FieldLabel>
                  <Select
                    value={evidenceType || null}
                    onValueChange={(v) => {
                      setEvidenceType((v ?? "") as BirthDateEvidenceType | "");
                      clearFieldError("evidenceType");
                    }}
                    disabled={submitting}
                    items={EVIDENCE_TYPE_ITEMS}
                  >
                    <SelectTrigger
                      id="birth-request-evidence-type"
                      className="w-full"
                      aria-invalid={!!fieldErrors.evidenceType || undefined}
                    >
                      <SelectValue placeholder="Chọn loại giấy tờ" />
                    </SelectTrigger>
                    <SelectContent>
                      {EVIDENCE_TYPE_ITEMS.map((item) => (
                        <SelectItem key={item.value} value={item.value}>
                          {item.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {fieldErrors.evidenceType && <FieldError>{fieldErrors.evidenceType}</FieldError>}
                </Field>

                <Field data-invalid={!!fieldErrors.evidence || undefined}>
                  <FieldLabel htmlFor="birth-request-evidence">
                    Tệp minh chứng <span className="text-destructive">*</span>
                  </FieldLabel>
                  <Input
                    key={fileInputKey}
                    id="birth-request-evidence"
                    type="file"
                    accept={EVIDENCE_ACCEPT}
                    aria-invalid={!!fieldErrors.evidence || undefined}
                    onChange={(e) => {
                      setEvidence(e.target.files?.[0] ?? null);
                      clearFieldError("evidence");
                    }}
                    disabled={submitting}
                    className="h-10"
                  />
                  <FieldDescription>Ảnh JPG, PNG hoặc PDF, tối đa 5 MB.</FieldDescription>
                  {fieldErrors.evidence && <FieldError>{fieldErrors.evidence}</FieldError>}
                </Field>
              </>
            )}

            {needsRepresentative && (
              <Field data-invalid={!!fieldErrors.representativeEmail || undefined}>
                <FieldLabel htmlFor="birth-request-representative">
                  Email người đại diện <span className="text-destructive">*</span>
                </FieldLabel>
                <Input
                  id="birth-request-representative"
                  type="email"
                  autoComplete="off"
                  value={representativeEmail}
                  placeholder="Email cha mẹ hoặc người giám hộ"
                  aria-invalid={!!fieldErrors.representativeEmail || undefined}
                  onChange={(e) => {
                    setRepresentativeEmail(e.target.value);
                    clearFieldError("representativeEmail");
                  }}
                  disabled={submitting}
                  className="h-10"
                />
                <FieldDescription>
                  Ngày sinh mới làm bạn dưới {CHILD_MAX_AGE_EXCLUSIVE} tuổi. Khi yêu cầu được duyệt, hệ thống gửi email
                  mời người đại diện đồng ý cho tài khoản.
                </FieldDescription>
                {fieldErrors.representativeEmail && <FieldError>{fieldErrors.representativeEmail}</FieldError>}
              </Field>
            )}
            </FieldGroup>
          </DialogBody>

          <DialogFooter layout="pair">
            <DialogCancel disabled={submitting}>Đóng</DialogCancel>
            <DialogAction type="submit" tone="primary" loading={submitting}>
              Gửi yêu cầu
            </DialogAction>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
