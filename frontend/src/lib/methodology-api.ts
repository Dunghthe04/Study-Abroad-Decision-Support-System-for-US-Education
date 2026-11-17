import { apiFetch } from "@/lib/api";
import type { MethodologyParameters } from "@/types/methodology";

// Công khai, không cần đăng nhập (như trang /methodology)
export async function getMethodologyParameters(): Promise<MethodologyParameters> {
  return await apiFetch<MethodologyParameters>("/api/v1/methodology/parameters", {
    signal: AbortSignal.timeout(10_000),
  });
}
