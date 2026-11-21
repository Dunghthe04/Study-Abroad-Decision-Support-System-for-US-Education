"use client";

import React, { useEffect, useState } from "react";
import { CheckCircle2Icon, MailIcon, PhoneIcon, XIcon } from "lucide-react";
import { centerPortalApi, ApiError } from "@/lib/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";

type Kind = "phone" | "email";

const COPY: Record<Kind, { label: string; placeholder: string; inputType: string; where: string }> = {
  phone: {
    label: "Số điện thoại",
    placeholder: "Số điện thoại mới (VD: 0912345678)",
    inputType: "tel",
    where: "số điện thoại mới",
  },
  email: {
    label: "Email liên lạc",
    placeholder: "Email mới",
    inputType: "email",
    where: "email mới",
  },
};

interface RowProps {
  kind: Kind;
  current: string | null | undefined;
  verified: boolean;
  onChanged: () => void;
}

/**
 * Một dòng thông tin liên lạc: xem giá trị hiện tại, đổi bằng OTP gửi tới giá trị mới.
 * [UC-91] Số điện thoại: đổi số qua OTP SMS gửi tới số mới; số hiện tại chưa xác thực thì xác thực bằng OTP SMS.
 */
function ContactRow({ kind, current, verified, onChanged }: RowProps) {
  const copy = COPY[kind];
  const Icon = kind === "phone" ? PhoneIcon : MailIcon;

  // "verify": xác thực số điện thoại hiện tại (UC-91), các bước còn lại là đổi giá trị
  const [step, setStep] = useState<"view" | "input" | "otp" | "verify">("view");
  const [newValue, setNewValue] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [info, setInfo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (cooldown <= 0) return;
    const t = setTimeout(() => setCooldown((c) => c - 1), 1000);
    return () => clearTimeout(t);
  }, [cooldown]);

  // Tự động xóa thông báo kết quả cập nhật số/email sau 5 giây
  useEffect(() => {
    if (!info) return;
    const t = setTimeout(() => setInfo(null), 5000);
    return () => clearTimeout(t);
  }, [info]);

  const reset = () => {
    setStep("view");
    setNewValue("");
    setCode("");
    setError(null);
    setCooldown(0);
  };

  const fail = (err: unknown, fallback: string) =>
    setError(err instanceof ApiError ? err.message : fallback);

  const sendCode = async () => {
    const value = newValue.trim();
    if (!value) {
      setError(`Vui lòng nhập ${copy.where}.`);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res =
        kind === "phone"
          ? await centerPortalApi.requestPhoneChange(value)
          : await centerPortalApi.requestEmailChange(value);
      setInfo(res.message);
      setStep("otp");
      setCooldown(60);
    } catch (err) {
      fail(err, "Không gửi được mã OTP. Vui lòng thử lại.");
    } finally {
      setBusy(false);
    }
  };

  // [UC-91] Gửi OTP SMS tới số hiện tại; gửi lại có đếm ngược 60 giây theo giới hạn của máy chủ
  const sendVerifyCode = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await centerPortalApi.requestPhoneVerification();
      setInfo(res.message);
      setStep("verify");
      setCooldown(60);
    } catch (err) {
      setStep("verify");
      fail(err, "Không gửi được mã OTP qua SMS. Vui lòng thử lại.");
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    if (!code.trim()) {
      setError("Vui lòng nhập mã OTP.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res =
        step === "verify"
          ? await centerPortalApi.verifyPhone(code.trim())
          : kind === "phone"
            ? await centerPortalApi.confirmPhoneChange(newValue.trim(), code.trim())
            : await centerPortalApi.confirmEmailChange(newValue.trim(), code.trim());
      setInfo(res.message);
      reset();
      onChanged();
    } catch (err) {
      fail(err, "Mã OTP không đúng hoặc đã hết hạn.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <Icon className="h-4 w-4 shrink-0 text-ink-3" />
          <div className="min-w-0">
            <p className="text-xs text-ink-3">{copy.label}</p>
            <p className="text-sm font-medium text-ink truncate">{current || "—"}</p>
          </div>
          {current && verified && (
            <Badge variant="ok" className="gap-1 shrink-0">
              <CheckCircle2Icon className="h-3 w-3" />
              Đã xác thực
            </Badge>
          )}
          {kind === "phone" && current && !verified && (
            <Badge variant="outline" className="shrink-0">
              Chưa xác thực
            </Badge>
          )}
        </div>
        {step === "view" && kind === "phone" && current && !verified && (
          <Button type="button" size="sm" onClick={sendVerifyCode} disabled={busy}>
            {busy && <Spinner data-icon="inline-start" />}
            Xác thực qua SMS
          </Button>
        )}
        {step === "view" && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              setInfo(null);
              setStep("input");
            }}
          >
            Đổi {kind === "phone" ? "số" : "email"}
          </Button>
        )}
      </div>

      {info && step === "view" && (
        <Alert className="relative border-ok/25 bg-ok-wash text-ok pr-9 transition-all duration-300">
          <CheckCircle2Icon className="h-4 w-4" />
          <AlertDescription className="ml-2">{info}</AlertDescription>
          <button
            type="button"
            onClick={() => setInfo(null)}
            className="absolute top-2.5 right-2.5 p-1 rounded hover:bg-ok/10 text-ok transition-colors"
            title="Đóng thông báo"
            aria-label="Đóng thông báo"
          >
            <XIcon className="h-3.5 w-3.5" />
          </button>
        </Alert>
      )}

      {step !== "view" && (
        <div className="rounded-lg border border-line bg-surface-2/50 p-4 space-y-3">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {step !== "verify" && (
          <div className="flex flex-col gap-2 sm:flex-row">
            <Input
              type={copy.inputType}
              value={newValue}
              onChange={(e) => setNewValue(e.target.value)}
              disabled={busy || step === "otp"}
              placeholder={copy.placeholder}
              aria-label={copy.placeholder}
              className="sm:max-w-xs"
            />
            {step === "input" ? (
              <Button type="button" size="sm" onClick={sendCode} disabled={busy}>
                {busy && <Spinner data-icon="inline-start" />}
                Gửi mã OTP
              </Button>
            ) : (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={sendCode}
                disabled={busy || cooldown > 0}
              >
                {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : "Gửi lại mã"}
              </Button>
            )}
          </div>
          )}

          {step === "verify" && (
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-xs text-ink-2">Mã OTP gửi qua SMS tới số {current}.</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={sendVerifyCode}
                disabled={busy || cooldown > 0}
              >
                {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : "Gửi lại mã"}
              </Button>
            </div>
          )}

          {(step === "otp" || step === "verify") && (
            <div className="space-y-2">
              {info && <p className="text-xs text-ink-2">{info}</p>}
              <div className="flex flex-col gap-2 sm:flex-row">
                <Input
                  inputMode="numeric"
                  maxLength={6}
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
                  disabled={busy}
                  placeholder="Mã OTP 6 số"
                  aria-label="Mã OTP"
                  className="sm:max-w-[10rem] font-mono tracking-widest"
                />
                <Button type="button" size="sm" onClick={confirm} disabled={busy || code.length < 6}>
                  {busy && <Spinner data-icon="inline-start" />}
                  {step === "verify" ? "Xác thực" : "Xác nhận đổi"}
                </Button>
              </div>
            </div>
          )}

          <Button type="button" variant="ghost" size="sm" onClick={reset} disabled={busy}>
            Hủy
          </Button>
        </div>
      )}
    </div>
  );
}

interface CardProps {
  phone: string | null | undefined;
  email: string | null | undefined;
  emailVerified: boolean;
  phoneVerified: boolean;
  onChanged: () => void;
}

export function CenterContactCard({ phone, email, emailVerified, phoneVerified, onChanged }: CardProps) {
  return (
    <div className="rounded-xl border border-line bg-surface p-6 space-y-5">
      <div>
        <h2 className="text-base font-semibold text-ink">Thông tin liên lạc</h2>
        <p className="text-xs text-ink-3 mt-1">
          Email và số điện thoại cần được xác thực bằng mã OTP trước khi nộp hồ sơ xác minh. Khi đổi, mã OTP được gửi tới email hoặc số điện thoại mới và chỉ lưu sau khi bạn nhập đúng mã. Email đăng nhập không bị thay đổi.
        </p>
      </div>
      <ContactRow kind="phone" current={phone} verified={phoneVerified} onChanged={onChanged} />
      <div className="border-t border-line" />
      <ContactRow kind="email" current={email} verified={emailVerified} onChanged={onChanged} />
    </div>
  );
}
