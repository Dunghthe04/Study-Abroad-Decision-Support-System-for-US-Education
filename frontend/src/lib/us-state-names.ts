"use client";

import { useEffect, useState } from "react";
import { profileFlowApi } from "@/lib/profile-flow-api";

// Bảng us_states (mã bang -> tên) tải một lần cho cả phiên rồi dùng lại; lỗi thì lần sau thử lại
let cache: Promise<Map<string, string>> | null = null;
function loadStateNames(): Promise<Map<string, string>> {
  cache ??= profileFlowApi
    .getMetadata()
    .then((meta) => new Map(meta.states.map((s) => [s.code.toUpperCase(), s.name])))
    .catch(() => {
      cache = null;
      return new Map<string, string>();
    });
  return cache;
}

/** Đổi mã bang ("FL") thành tên ("Florida"). Chưa tải xong hoặc không có trong bảng thì giữ nguyên mã. */
export function useStateNames(): (code: string | null | undefined) => string | null {
  const [names, setNames] = useState<Map<string, string> | null>(null);

  useEffect(() => {
    let alive = true;
    void loadStateNames().then((m) => alive && setNames(m));
    return () => {
      alive = false;
    };
  }, []);

  return (code) => (code ? (names?.get(code.toUpperCase()) ?? code) : null);
}
