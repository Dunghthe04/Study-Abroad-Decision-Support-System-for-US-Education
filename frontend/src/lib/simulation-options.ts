"use client";

// Danh sách bang và ngành cho khung Mô phỏng (UC-77). Lấy từ metadata của luồng hồ sơ, tải một lần cho cả phiên.
// Lỗi thì trả danh sách rỗng và lần sau thử lại; khi đó các ô bang, ngành tắt, các thanh kéo khác vẫn dùng được.

import { useEffect, useState } from "react";
import { profileFlowApi } from "@/lib/profile-flow-api";
import type { MajorOption, UsStateOption } from "@/types/profile-flow";

export interface SimulationOptions {
  states: UsStateOption[];
  majors: MajorOption[];
}

const EMPTY: SimulationOptions = { states: [], majors: [] };

let cache: Promise<SimulationOptions> | null = null;
function load(): Promise<SimulationOptions> {
  cache ??= profileFlowApi
    .getMetadata()
    .then((meta) => ({ states: meta.states, majors: meta.majors }))
    .catch(() => {
      cache = null;
      return EMPTY;
    });
  return cache;
}

export function useSimulationOptions(): SimulationOptions {
  const [options, setOptions] = useState<SimulationOptions>(EMPTY);

  useEffect(() => {
    let alive = true;
    void load().then((loaded) => alive && setOptions(loaded));
    return () => {
      alive = false;
    };
  }, []);

  return options;
}
