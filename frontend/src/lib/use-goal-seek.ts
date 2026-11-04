"use client";

// Trạng thái của khung "Cần gì để vào trường này": trường đang xét, nhóm đích, kết quả mới nhất.

import { useCallback, useEffect, useRef, useState } from "react";
import { recommendationApi } from "@/lib/recommendation-api";
import type { GoalTarget, SchoolGoalResult } from "@/types/recommendation";

export interface GoalSeek {
  code: string | null; // trường đang xét; null là chưa mở khung
  target: GoalTarget | null; // null: để hệ thống chọn nhóm cao hơn một bậc
  result: SchoolGoalResult | null;
  loading: boolean;
  error: string | null;
  open: (code: string) => void;
  setTarget: (target: GoalTarget) => void;
  close: () => void;
}

export function useGoalSeek(): GoalSeek {
  const [code, setCode] = useState<string | null>(null);
  const [target, setTargetState] = useState<GoalTarget | null>(null);
  const [result, setResult] = useState<SchoolGoalResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0); // chỉ nhận phản hồi của lần gọi mới nhất

  useEffect(() => {
    if (!code) return;
    const mine = ++seq.current;
    // setState trong .then/.catch, không gọi ngay trong effect
    recommendationApi
      .goalSeek(code, target)
      .then((res) => {
        if (seq.current !== mine) return;
        setResult(res);
        setError(null);
      })
      .catch((err: unknown) => {
        if (seq.current !== mine) return;
        setError(err instanceof Error ? err.message : "Chưa tính được.");
      })
      .finally(() => {
        if (seq.current === mine) setLoading(false);
      });
  }, [code, target]);

  const open = useCallback((next: string) => {
    setCode(next);
    setTargetState(null); // trường mới thì quay về nhóm đích mặc định
    setResult(null);
    setError(null);
    setLoading(true);
  }, []);

  const setTarget = useCallback((next: GoalTarget) => {
    setTargetState(next);
    setLoading(true);
  }, []);

  const close = useCallback(() => {
    seq.current++; // bỏ phản hồi đang chờ
    setCode(null);
    setResult(null);
    setError(null);
    setLoading(false);
  }, []);

  return { code, target, result, loading, error, open, setTarget, close };
}
