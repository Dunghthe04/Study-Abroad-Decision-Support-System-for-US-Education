"use client";

import React, { useState } from "react";
import { REGEXP_ONLY_DIGITS } from "input-otp";
import { QRCodeSVG } from "qrcode.react";
import { CheckIcon, CopyIcon, DownloadIcon, KeyRoundIcon, ShieldCheckIcon } from "lucide-react";
import { ApiError, authApi } from "@/lib/api";
import type { TwoFactorChallengeResponse, TwoFactorLoginResponse } from "@/types/api";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { InputOTP, InputOTPGroup, InputOTPSlot } from "@/components/ui/input-otp";
import { NETWORK_ERROR } from "@/lib/auth-validation";

interface TwoFactorStepProps {
  challenge: TwoFactorChallengeResponse;
  /** Bước 2 xong, người dùng đã xem mã khôi phục (nếu có): đặt phiên và chuyển trang */
  onDone: (res: TwoFactorLoginResponse) => void;
  /** Phiếu hết hạn, đã dùng hoặc thử quá 5 lần, hoặc tài khoản vừa bị khóa: quay lại bước mật khẩu kèm câu báo */
  onRestart: (message: string, locked?: boolean) => void;
}

/**
 * [UC-03 3d] Bước 2 đăng nhập Moderator, Administrator.
 * Lần đầu: quét QR (hoặc nhập khóa) vào ứng dụng xác thực, nhập mã 6 số, nhận 10 mã khôi phục hiện một lần.
 * Các lần sau: nhập mã 6 số, hoặc một mã khôi phục khi mất điện thoại.
 */
export function TwoFactorStep({ challenge, onDone, onRestart }: TwoFactorStepProps) {
  const setup = challenge.setup;
  const [code, setCode] = useState("");
  const [useRecovery, setUseRecovery] = useState(false);
  const [recoveryCode, setRecoveryCode] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  // Thiết lập xong: giữ kết quả, chờ người dùng xác nhận đã lưu 10 mã rồi mới đặt phiên
  const [finished, setFinished] = useState<TwoFactorLoginResponse | null>(null);
  const [copied, setCopied] = useState<"secret" | "codes" | null>(null);

  const submit = async (otp: string) => {
    setError(null);
    if (useRecovery) {
      if (!recoveryCode.trim()) {
        setFieldError("Vui lòng nhập mã khôi phục.");
        return;
      }
    } else if (!/^\d{6}$/.test(otp)) {
      setFieldError("Vui lòng nhập đủ 6 chữ số.");
      return;
    }
    setFieldError(null);
    setIsSubmitting(true);
    try {
      const res = await authApi.loginTwoFactor(
        useRecovery
          ? { challenge: challenge.challenge, recoveryCode: recoveryCode.trim() }
          : { challenge: challenge.challenge, code: otp }
      );
      if (res.recoveryCodes && res.recoveryCodes.length > 0) {
        setFinished(res);
      } else {
        onDone(res);
      }
    } catch (err) {
      if (err instanceof ApiError) {
        const body = err.body as { code?: string } | null;
        if (err.status === 423) {
          onRestart(err.message || "Tài khoản tạm khóa 15 phút do nhập sai 5 lần liên tiếp.", true);
        } else if (body?.code === "two_factor_challenge_invalid") {
          onRestart(err.message || "Phiên xác thực đã hết hạn. Vui lòng đăng nhập lại.");
        } else {
          setError(err.message || "Mã xác thực không đúng.");
          setCode("");
        }
      } else {
        setError(NETWORK_ERROR);
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const copy = async (text: string, what: "secret" | "codes") => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(what);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      // Trình duyệt chặn clipboard: người dùng vẫn chọn chữ để sao chép tay
    }
  };

  if (finished?.recoveryCodes) {
    const codes = finished.recoveryCodes;
    const codesText = codes.join("\n");
    const download = () => {
      const blob = new Blob(
        [`Mã khôi phục USDSS (${finished.user.email})\nMỗi mã dùng được một lần.\n\n${codesText}\n`],
        { type: "text/plain;charset=utf-8" }
      );
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "usdss-ma-khoi-phuc.txt";
      a.click();
      URL.revokeObjectURL(url);
    };
    return (
      <div className="space-y-6">
        <div className="space-y-2">
          <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
            Lưu mã khôi phục
          </h1>
          <p className="text-sm text-ink-2">
            Xác thực hai lớp đã bật. Khi mất điện thoại, dùng một trong các mã dưới đây thay cho mã 6 số. Mỗi mã dùng được
            một lần và chỉ hiện lúc này, hãy lưu ở nơi an toàn.
          </p>
        </div>
        <ul
          className="grid grid-cols-2 gap-2 rounded-lg border border-input bg-surface-2 p-4 font-mono text-sm"
          aria-label="Mã khôi phục"
        >
          {codes.map((c) => (
            <li key={c} className="select-all text-center">
              {c}
            </li>
          ))}
        </ul>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => copy(codesText, "codes")}>
            {copied === "codes" ? <CheckIcon aria-hidden="true" /> : <CopyIcon aria-hidden="true" />}
            {copied === "codes" ? "Đã sao chép" : "Sao chép"}
          </Button>
          <Button type="button" variant="outline" size="sm" onClick={download}>
            <DownloadIcon aria-hidden="true" />
            Tải về
          </Button>
        </div>
        <Button type="button" size="xl" className="w-full" onClick={() => onDone(finished)}>
          Tôi đã lưu mã, tiếp tục
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink sm:text-3xl">
          {setup ? "Thiết lập xác thực hai lớp" : "Xác thực hai lớp"}
        </h1>
        <p className="text-sm text-ink-2">
          {setup
            ? "Tài khoản quản trị cần thêm một lớp bảo vệ. Mở ứng dụng xác thực trên điện thoại (Google Authenticator, Microsoft Authenticator…), quét mã QR rồi nhập mã 6 số ứng dụng hiện ra."
            : useRecovery
              ? "Nhập một mã khôi phục bạn đã lưu khi thiết lập. Mã đã dùng sẽ không dùng lại được."
              : "Nhập mã 6 số trong ứng dụng xác thực trên điện thoại của bạn."}
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {setup && (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-input p-4">
          <div className="rounded-md bg-white p-2">
            <QRCodeSVG value={setup.otpauthUri} size={176} aria-label="Mã QR thiết lập ứng dụng xác thực" />
          </div>
          <div className="w-full space-y-1 text-center">
            <p className="text-xs text-ink-2">Không quét được? Nhập khóa này vào ứng dụng:</p>
            <div className="flex items-center justify-center gap-2">
              <code className="select-all break-all font-mono text-sm text-ink">{setup.secret}</code>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Sao chép khóa"
                onClick={() => copy(setup.secret, "secret")}
              >
                {copied === "secret" ? <CheckIcon aria-hidden="true" /> : <CopyIcon aria-hidden="true" />}
              </Button>
            </div>
          </div>
        </div>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void submit(code);
        }}
        noValidate
        className="space-y-4"
      >
        {useRecovery ? (
          <Field data-invalid={Boolean(fieldError)}>
            <FieldLabel htmlFor="recovery-code">Mã khôi phục</FieldLabel>
            <Input
              id="recovery-code"
              autoComplete="off"
              autoFocus
              maxLength={20}
              placeholder="XXXXX-XXXXX"
              className="font-mono uppercase"
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? "two-factor-error" : undefined}
              value={recoveryCode}
              onChange={(e) => {
                setRecoveryCode(e.target.value);
                setFieldError(null);
              }}
            />
            {fieldError && <FieldError id="two-factor-error">{fieldError}</FieldError>}
          </Field>
        ) : (
          <Field data-invalid={Boolean(fieldError)}>
            <FieldLabel htmlFor="totp-code">Mã xác thực 6 số</FieldLabel>
            <InputOTP
              id="totp-code"
              maxLength={6}
              pattern={REGEXP_ONLY_DIGITS}
              inputMode="numeric"
              autoComplete="one-time-code"
              autoFocus
              value={code}
              onChange={(v) => {
                setCode(v);
                setFieldError(null);
              }}
              onComplete={(v: string) => void submit(v)}
              disabled={isSubmitting}
              aria-invalid={Boolean(fieldError)}
              aria-describedby={fieldError ? "two-factor-error" : undefined}
            >
              <InputOTPGroup>
                {Array.from({ length: 6 }, (_, i) => (
                  <InputOTPSlot key={i} index={i} className="size-11 text-lg" aria-invalid={Boolean(fieldError)} />
                ))}
              </InputOTPGroup>
            </InputOTP>
            {fieldError && <FieldError id="two-factor-error">{fieldError}</FieldError>}
          </Field>
        )}

        <Button type="submit" size="xl" className="w-full" disabled={isSubmitting} loading={isSubmitting}>
          <ShieldCheckIcon aria-hidden="true" />
          {isSubmitting ? "Đang xác thực..." : "Xác thực"}
        </Button>
      </form>

      <div className="space-y-2 text-center text-sm text-ink-2">
        {!setup && (
          <p>
            <button
              type="button"
              className="inline-flex items-center gap-1 font-semibold text-brand hover:underline"
              onClick={() => {
                setUseRecovery((v) => !v);
                setFieldError(null);
                setError(null);
              }}
            >
              <KeyRoundIcon className="size-3.5" aria-hidden="true" />
              {useRecovery ? "Dùng mã 6 số trong ứng dụng" : "Mất điện thoại? Dùng mã khôi phục"}
            </button>
          </p>
        )}
        <p>
          <button
            type="button"
            className="font-semibold text-brand hover:underline"
            onClick={() => onRestart("")}
          >
            Quay lại đăng nhập
          </button>
        </p>
      </div>
    </div>
  );
}
