"use client";

// Trạng thái của trình mô phỏng hồ sơ: giá trị giả định, gọi API có chống rung, kết quả mới nhất.

import { useCallback, useEffect, useRef, useState } from "react";
import { recommendationApi } from "@/lib/recommendation-api";
import { overridesFromValues } from "@/lib/simulation-link";
import type { Pref, SimulationOverrides, SimulationResponse } from "@/types/recommendation";

const DEBOUNCE_MS = 300;

export interface ProfileSimulation {
  result: SimulationResponse | null; // null cho tới khi có phản hồi đầu tiên
  overrides: SimulationOverrides;
  loading: boolean;
  error: string | null;
  isSimulating: boolean; // có giá trị giả định đang áp dụng
  set: (patch: SimulationOverrides) => void; // đặt hoặc xóa (null) các giá trị giả định
  reset: (keys: (keyof SimulationOverrides)[]) => void; // bỏ giả định của một nhóm thanh kéo
  resetAll: () => void;
  replace: (next: SimulationOverrides) => void; // thay toàn bộ giá trị giả định (chat vừa thử một kịch bản mới)
}

// Giá trị giả định đang xem được nhớ trong tab (sessionStorage) để tải lại trang không bị mất.
// Khóa gồm học sinh và lần gợi ý đã lưu: gợi ý lại hoặc đổi hồ sơ thì khóa đổi, giả định cũ tự bỏ. Lỗi bộ nhớ thì bỏ qua.
function readStored(key: string): SimulationOverrides {
  try {
    const raw = window.sessionStorage.getItem(key);
    return raw ? overridesFromValues(JSON.parse(raw) as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function writeStored(key: string, overrides: SimulationOverrides): void {
  try {
    const has = Object.values(overrides).some((v) => v !== undefined && v !== null);
    if (has) window.sessionStorage.setItem(key, JSON.stringify(overrides));
    else window.sessionStorage.removeItem(key);
  } catch {
    // bộ nhớ đầy hoặc bị chặn: vẫn dùng được, chỉ không nhớ qua lần tải lại
  }
}

// basePref: ưu tiên của lần gợi ý đã lưu, dùng làm mặc định để mô phỏng không đổi thứ tự khi người dùng chưa đụng tới thanh ưu tiên.
// initial: giá trị giả định có sẵn từ đầu (chat vừa thử một kịch bản); chỉ đọc lúc dựng lần đầu, và được ưu tiên hơn giá trị đã nhớ.
// storageKey: nơi nhớ giả định (null cho tới khi biết lần gợi ý đang xem).
export function useProfileSimulation(
  enabledNow: boolean,
  basePref: Pref | null,
  initial?: SimulationOverrides,
  storageKey?: string | null,
): ProfileSimulation {
  const hasInitial = initial !== undefined && Object.keys(initial).length > 0;
  const [overrides, setOverrides] = useState<SimulationOverrides>(initial ?? {});
  // Chỉ mô phỏng sau khi đã khôi phục giá trị nhớ, để không gọi API hai lần (rỗng rồi mới tới giá trị nhớ)
  const [restored, setRestored] = useState(!storageKey || hasInitial);
  const enabled = enabledNow && restored;

  // Khôi phục ngay khi biết khóa (chỉ một lần): chỉnh state trong lúc dựng, cách React khuyên thay cho setState trong effect
  if (storageKey && !restored) {
    const stored = readStored(storageKey);
    if (Object.keys(stored).length > 0) setOverrides(stored);
    setRestored(true);
  }

  useEffect(() => {
    if (storageKey && restored) writeStored(storageKey, overrides);
  }, [storageKey, restored, overrides]);

  const [result, setResult] = useState<SimulationResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const seq = useRef(0); // chỉ nhận phản hồi của lần gọi mới nhất

  useEffect(() => {
    if (!enabled) return;
    const mine = ++seq.current;
    // Lần đầu (chưa có hồ sơ thật) gọi ngay để lấy giá trị hiện tại; sau đó chờ người dùng dừng kéo
    const timer = setTimeout(
      () => {
        setLoading(true);
        recommendationApi
          .simulate({ ...overrides, pref: overrides.pref ?? basePref })
          .then((res) => {
            if (seq.current !== mine) return;
            setResult(res);
            setError(null);
          })
          .catch((err: unknown) => {
            if (seq.current !== mine) return;
            setError(err instanceof Error ? err.message : "Chưa mô phỏng được.");
          })
          .finally(() => {
            if (seq.current === mine) setLoading(false);
          });
      },
      result === null ? 0 : DEBOUNCE_MS,
    );
    return () => clearTimeout(timer);
    // result chỉ dùng để quyết định có chờ hay không, không được làm chạy lại hiệu ứng
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, overrides, basePref]);

  const set = useCallback((patch: SimulationOverrides) => setOverrides((cur) => ({ ...cur, ...patch })), []);
  const reset = useCallback(
    (keys: (keyof SimulationOverrides)[]) =>
      setOverrides((cur) => {
        const next = { ...cur };
        keys.forEach((key) => delete next[key]);
        return next;
      }),
    [],
  );
  const resetAll = useCallback(() => setOverrides({}), []);
  const replace = useCallback((next: SimulationOverrides) => setOverrides(next), []);

  // Chỉ tính các giá trị người dùng đã kéo; ưu tiên mặc định (basePref) không tính
  const isSimulating = Object.values(overrides).some((v) => v !== undefined && v !== null);
  return { result, overrides, loading, error, isSimulating, set, reset, resetAll, replace };
}
