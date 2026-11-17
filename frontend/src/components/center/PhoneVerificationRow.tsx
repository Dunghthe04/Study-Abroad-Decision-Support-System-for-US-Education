"use client";

import { useEffect, useState } from "react";
import { CheckCircle2Icon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { ApiError, centerPortalApi } from "@/lib/api";

/** Khoảng cách tối thiểu giữa hai lần gửi mã, khớp với backend. */
const RESEND_COOLDOWN_SECONDS = 60;

interface PhoneVerificationRowProps {
  phone?: string | null;
  verified: boolean;
  /** Gọi sau khi xác thực thành công để tải lại hồ sơ trung tâm. */
  onChanged: () => void;
}

/**
 * [UC-91] Xác thực số điện thoại trung tâm: gửi OTP qua SMS tới số hiện tại, nhập mã, đúng thì số được ghi là đã xác thực.
 * Mã sai được nhập lại; sai quá số lần cho phép hoặc mã hết hạn thì phải xin mã mới. Đổi số điện thoại thì phải xác thực lại.
 */
export function PhoneVerificationRow({ phone, verified, onChanged }: PhoneVerificationRowProps) {
  const [step, setStep] = useState<"idle" | "otp">("idle");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [info, setInfo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = setTimeout(() => setCooldown((value) => value - 1), 1000);
    return () => clearTimeout(timer);
  }, [cooldown]);

  if (!phone) return null;

  if (verified) {
    return (
      <div className="flex items-center gap-1.5 text-xs text-ink-2">
        <Badge variant="ok" className="gap-1">
          <CheckCircle2Icon className="size-3" />
          Số điện thoại đã xác thực
        </Badge>
      </div>
    );
  }

  const errorText = (err: unknown) => (err instanceof ApiError ? err.message : "Không thể thực hiện. Vui lòng thử lại.");

  const requestCode = async () => {
    if (busy || cooldown > 0) return;
    setBusy(true);
    setError(null);
    setInfo(null);
    try {
      const res = await centerPortalApi.requestPhoneVerification();
      setInfo(res.message);
      setStep("otp");
      setCode("");
      setCooldown(RESEND_COOLDOWN_SECONDS);
    } catch (err: unknown) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    const trimmed = code.trim();
    if (busy || trimmed.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      await centerPortalApi.confirmPhoneVerification(trimmed);
      setStep("idle");
      setInfo(null);
      onChanged();
    } catch (err: unknown) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-line p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-sm">
          <Badge variant="warn">Số điện thoại chưa xác thực</Badge>
          <span className="text-ink-2">Cần xác thực trước khi nộp hồ sơ xác minh.</span>
        </div>
        {step === "idle" ? (
          <Button type="button" variant="outline" size="sm" onClick={() => void requestCode()} disabled={busy || cooldown > 0} className="gap-1.5">
            {busy ? <Spinner className="size-3.5" /> : null}
            {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : "Xác thực số điện thoại"}
          </Button>
        ) : null}
      </div>

      {info ? <p className="text-xs text-ink-2">{info}</p> : null}

      {error ? (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {step === "otp" ? (
        <div className="flex flex-wrap items-center gap-2">
          <Input
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={6}
            value={code}
            onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))}
            placeholder="Mã 6 chữ số"
            aria-label="Mã OTP gửi qua SMS"
            disabled={busy}
            className="w-36 tabular-nums"
          />
          <Button type="button" size="sm" onClick={() => void confirm()} disabled={busy || code.trim().length !== 6} className="gap-1.5">
            {busy ? <Spinner className="size-3.5" /> : null}
            Xác nhận
          </Button>
          <Button type="button" variant="ghost" size="sm" onClick={() => void requestCode()} disabled={busy || cooldown > 0}>
            {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : "Gửi lại mã"}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
