"use client";

import React, { useEffect, useState } from "react";
import { CheckCircle2Icon, MailIcon, PhoneIcon } from "lucide-react";
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

/** Một dòng thông tin liên lạc: xem giá trị hiện tại, đổi bằng OTP gửi tới giá trị mới. */
function ContactRow({ kind, current, verified, onChanged }: RowProps) {
  const copy = COPY[kind];
  const Icon = kind === "phone" ? PhoneIcon : MailIcon;

  const [step, setStep] = useState<"view" | "input" | "otp">("view");
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
      if (kind === "phone") {
        // Số điện thoại không cần OTP: lưu trực tiếp
        const res = await centerPortalApi.changePhone(value);
        setInfo(res.message);
        reset();
        onChanged();
        return;
      }
      const res = await centerPortalApi.requestEmailChange(value);
      setInfo(res.message);
      setStep("otp");
      setCooldown(60);
    } catch (err) {
      fail(err, kind === "phone" ? "Không lưu được số điện thoại. Vui lòng thử lại." : "Không gửi được mã OTP. Vui lòng thử lại.");
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
      const res = await centerPortalApi.confirmEmailChange(newValue.trim(), code.trim());
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
          {kind === "email" && current && verified && (
            <Badge variant="ok" className="gap-1 shrink-0">
              <CheckCircle2Icon className="h-3 w-3" />
              Đã xác thực
            </Badge>
          )}
        </div>
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
        <Alert className="border-ok/25 bg-ok-wash text-ok">
          <CheckCircle2Icon className="h-4 w-4" />
          <AlertDescription className="ml-2">{info}</AlertDescription>
        </Alert>
      )}

      {step !== "view" && (
        <div className="rounded-lg border border-line bg-surface-2/50 p-4 space-y-3">
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

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
                {kind === "phone" ? "Lưu số mới" : "Gửi mã OTP"}
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

          {step === "otp" && (
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
                  Xác nhận đổi
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
  onChanged: () => void;
}

export function CenterContactCard({ phone, email, emailVerified, onChanged }: CardProps) {
  return (
    <div className="rounded-xl border border-line bg-surface p-6 space-y-5">
      <div>
        <h2 className="text-base font-semibold text-ink">Thông tin liên lạc</h2>
        <p className="text-xs text-ink-3 mt-1">
          Số điện thoại lưu trực tiếp. Khi đổi email, mã OTP được gửi tới email mới và chỉ lưu sau khi bạn nhập đúng mã. Email đăng nhập không bị thay đổi.
        </p>
      </div>
      <ContactRow kind="phone" current={phone} verified={false} onChanged={onChanged} />
      <div className="border-t border-line" />
      <ContactRow kind="email" current={email} verified={emailVerified} onChanged={onChanged} />
    </div>
  );
}
