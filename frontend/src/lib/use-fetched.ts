"use client";

import { useEffect, useState } from "react";
import { ApiError } from "@/lib/api";

export interface Fetched<T> {
  /** Dữ liệu của lần tải thành công gần nhất (giữ nguyên trong lúc tải lại để khung không nhảy). */
  data: T | null;
  /** Lỗi của lần tải ứng với key hiện tại. */
  error: string | null;
  /** key hiện tại chưa có kết quả. */
  loading: boolean;
}

function messageOf(error: unknown): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error) return error.message;
  return "Không tải được dữ liệu.";
}

/**
 * Tải dữ liệu theo `key`: đổi key thì tải lại, kết quả của key cũ bị bỏ. Không setState đồng bộ trong effect:
 * "đang tải" suy ra từ việc kết quả đang giữ chưa thuộc key hiện tại, nên bản cũ vẫn hiển thị (mờ) cho tới khi bản mới về.
 */
export function useFetched<T>(key: string, load: () => Promise<T>): Fetched<T> {
  const [result, setResult] = useState<{ key: string; data: T | null; error: string | null } | null>(null);

  useEffect(() => {
    let alive = true;
    load().then(
      (data) => {
        if (alive) setResult({ key, data, error: null });
      },
      (error: unknown) => {
        if (alive) setResult((previous) => ({ key, data: previous?.data ?? null, error: messageOf(error) }));
      },
    );
    return () => {
      alive = false;
    };
    // load là closure mới mỗi lần render; chỉ key quyết định có tải lại hay không
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  const current = result?.key === key;
  return { data: result?.data ?? null, error: current ? result.error : null, loading: !current };
}
