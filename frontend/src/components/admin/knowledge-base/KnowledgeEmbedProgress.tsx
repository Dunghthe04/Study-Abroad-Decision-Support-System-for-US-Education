"use client";

import { useEffect, useState } from "react";
import { CircleAlertIcon, CircleCheckIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { adminKnowledgeBaseApi } from "@/lib/knowledge-base-api";
import type { KnowledgeEmbedStatus } from "@/types/knowledge-base";

const POLL_MS = 3000;
const MAX_POLLS = 200; // 10 phút; quá lâu thì dừng hỏi, moderator mở lại hộp để hỏi tiếp

const UNAVAILABLE: KnowledgeEmbedStatus = { available: false, pending: 0, running: false, done: 0, error: null, started: false };

// Chat chỉ thấy dòng đã có vector. Sau khi nhập file, advisor tạo vector ở nền: hộp này cho moderator biết đã xong chưa.
export function KnowledgeEmbedProgress() {
  const [status, setStatus] = useState<KnowledgeEmbedStatus | null>(null);
  const [starting, setStarting] = useState(false);
  const [round, setRound] = useState(0); // tăng để hỏi lại từ đầu (sau khi bấm tạo vector)

  useEffect(() => {
    let cancelled = false;
    let timer: number | undefined;
    const poll = async (attempt: number) => {
      const next = await adminKnowledgeBaseApi.embedStatus().catch(() => UNAVAILABLE);
      if (cancelled) return;
      setStatus(next);
      // Chỉ hỏi tiếp khi advisor đang chạy; chưa chạy mà còn dòng thiếu thì chờ moderator bấm nút
      if (next.available && next.running && attempt < MAX_POLLS) timer = window.setTimeout(() => void poll(attempt + 1), POLL_MS);
    };
    void poll(0);
    return () => {
      cancelled = true;
      if (timer !== undefined) window.clearTimeout(timer);
    };
  }, [round]);

  const start = async () => {
    setStarting(true);
    const next = await adminKnowledgeBaseApi.startEmbed().catch(() => UNAVAILABLE);
    setStarting(false);
    setStatus(next);
    if (next.available) setRound((current) => current + 1);
  };

  if (status === null) {
    return (
      <Alert variant="info">
        <Spinner />
        <AlertDescription>Đang kiểm tra việc tạo vector cho chat...</AlertDescription>
      </Alert>
    );
  }
  if (!status.available) {
    return (
      <Alert variant="warning">
        <CircleAlertIcon />
        <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
          <span>Chưa tạo được vector (advisor chưa chạy hoặc lỗi). Các dòng đã lưu nhưng chat chưa thấy cho tới khi vector được tạo.</span>
          <Button type="button" size="sm" variant="outline" disabled={starting} onClick={() => void start()}>
            {starting && <Spinner />} Thử lại
          </Button>
        </AlertDescription>
      </Alert>
    );
  }
  if (status.running) {
    return (
      <Alert variant="info">
        <Spinner />
        <AlertDescription>
          Đang tạo vector cho chat: còn {status.pending} dòng (đã xong {status.done}). Có thể đóng hộp này, việc vẫn chạy.
        </AlertDescription>
      </Alert>
    );
  }
  if (status.error !== null || status.pending > 0) {
    return (
      <Alert variant="warning">
        <CircleAlertIcon />
        <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
          <span>
            {status.error ?? "Còn dòng chưa có vector."} Chat chưa thấy {status.pending} dòng.
          </span>
          <Button type="button" size="sm" variant="outline" disabled={starting} onClick={() => void start()}>
            {starting && <Spinner />} Tạo vector
          </Button>
        </AlertDescription>
      </Alert>
    );
  }
  return (
    <Alert variant="success">
      <CircleCheckIcon />
      <AlertDescription>Chat đã thấy tất cả các dòng trong kho.</AlertDescription>
    </Alert>
  );
}
