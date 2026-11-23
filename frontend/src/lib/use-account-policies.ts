"use client";

import { useState } from "react";
import { getCurrentPolicy } from "@/lib/policy-api";
import { useFetched } from "@/lib/use-fetched";
import type { AcceptedPolicy, PublicPolicy } from "@/types/policy";

export interface AccountPolicies {
  terms: PublicPolicy | null;
  privacy: PublicPolicy | null;
}

/**
 * [UC-104] Điều khoản sử dụng và Chính sách bảo mật đang hiệu lực, cho form đăng ký.
 * Form gửi kèm đúng phiên bản người dùng đã được hiển thị (acceptedPolicies) để server ghi bằng chứng đồng ý.
 * `ready` chỉ true khi đã tải xong và không lỗi: chưa tải được thì không cho đăng ký, vì không biết người dùng đang đồng ý bản nào.
 * Loại nào chưa công bố thì không có trong acceptedPolicies (không có gì để đồng ý).
 */
export function useAccountPolicies() {
  const [attempt, setAttempt] = useState(0);
  const { data, error, loading } = useFetched<AccountPolicies>(`account-policies:${attempt}`, async () => {
    const [terms, privacy] = await Promise.all([getCurrentPolicy("terms_of_use"), getCurrentPolicy("privacy_policy")]);
    return { terms, privacy };
  });

  const acceptedPolicies: AcceptedPolicy[] = [];
  if (data?.terms) acceptedPolicies.push({ kind: "terms_of_use", version: data.terms.version });
  if (data?.privacy) acceptedPolicies.push({ kind: "privacy_policy", version: data.privacy.version });

  return {
    policies: data,
    acceptedPolicies,
    ready: !loading && !error && data !== null,
    loading,
    error,
    /** Tải lại (sau khi server báo bản vừa đổi, hoặc người dùng bấm Thử lại). */
    reload: () => setAttempt((n) => n + 1),
  };
}
