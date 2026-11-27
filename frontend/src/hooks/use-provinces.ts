import { useEffect, useState } from "react";
import { dashboardApi } from "@/lib/dashboard-api";
import { FALLBACK_PROVINCES } from "@/lib/vietnam-provinces";

/** Danh sách 34 tỉnh/thành lấy từ backend (VietnamProvinces). Đang tải hoặc API lỗi thì dùng bản dự phòng. */
export function useProvinces(): string[] {
  const [provinces, setProvinces] = useState<string[]>(FALLBACK_PROVINCES);

  useEffect(() => {
    let alive = true;
    dashboardApi
      .getProvinces()
      .then((list) => {
        if (alive && list?.length) setProvinces(list);
      })
      .catch(() => {});
    return () => {
      alive = false;
    };
  }, []);

  return provinces;
}
