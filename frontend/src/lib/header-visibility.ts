import { useSyncExternalStore } from "react";

// Ẩn/hiện thanh trên cùng (trang phân tích), nhớ trong trình duyệt
const KEY = "usas:header-hidden";
const listeners = new Set<() => void>();

const subscribe = (cb: () => void) => {
  listeners.add(cb);
  return () => listeners.delete(cb);
};

const read = () => {
  try {
    return localStorage.getItem(KEY) === "1";
  } catch {
    return false;
  }
};

export function useHeaderHidden() {
  return useSyncExternalStore(subscribe, read, () => false);
}

export function setHeaderHidden(hidden: boolean) {
  try {
    localStorage.setItem(KEY, hidden ? "1" : "0");
  } catch {}
  listeners.forEach((l) => l());
}
