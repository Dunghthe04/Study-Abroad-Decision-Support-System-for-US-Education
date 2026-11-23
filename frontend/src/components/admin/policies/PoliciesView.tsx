"use client";

import { useState } from "react";
import { FilePlus2Icon } from "lucide-react";
import { Segmented } from "@/components/admin/ai-usage/parts";
import { PolicyEditor } from "@/components/admin/policies/PolicyEditor";
import { PolicyHistory } from "@/components/admin/policies/PolicyHistory";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ApiError } from "@/lib/api";
import { adminPoliciesApi } from "@/lib/admin-policies-api";
import { POLICY_KINDS, formatDateTime } from "@/lib/policy-format";
import { useFetched } from "@/lib/use-fetched";
import type { PolicyDocument, PolicyKind } from "@/types/policy";

const messageOf = (error: unknown) =>
  error instanceof ApiError || error instanceof Error ? error.message : "Đã có lỗi xảy ra. Bạn thử lại nhé.";

/**
 * Trang admin: quản lý chính sách và điều khoản (UC-103). Chỉ Administrator.
 * Mỗi loại có: bản đang hiệu lực, tối đa một bản nháp đang soạn, và lịch sử các phiên bản.
 * Quy trình: Soạn bản mới (chép bản đang hiệu lực) → sửa → xem trước → Công bố.
 */
export function PoliciesView() {
  const [kind, setKind] = useState<PolicyKind>("terms_of_use");
  const [version, setVersion] = useState(0);
  const history = useFetched(`policies|${kind}|${version}`, () => adminPoliciesApi.list(kind));
  const rows = history.data && history.data.every((r) => r.kind === kind) ? history.data : null;

  const published = rows?.find((r) => r.status === "published") ?? null;
  const draftRow = rows?.find((r) => r.status === "draft") ?? null;

  // Bản nháp đầy đủ (kèm toàn văn) để soạn. Chỉ tải khi loại này có nháp.
  const draft = useFetched(`policy-draft|${draftRow?.id ?? ""}|${version}`, () =>
    draftRow ? adminPoliciesApi.get(draftRow.id) : Promise.resolve(null));
  const draftDoc: PolicyDocument | null = draft.data && draftRow && draft.data.id === draftRow.id ? draft.data : null;

  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const refresh = () => setVersion((v) => v + 1);
  const changeKind = (next: PolicyKind) => { setKind(next); setNotice(null); setError(null); };

  const createDraft = async () => {
    setCreating(true); setError(null); setNotice(null);
    try {
      const result = await adminPoliciesApi.createDraft(kind);
      if (!result.created) setNotice("Loại này đã có bản nháp, đã mở lại bản nháp đó.");
      refresh();
    } catch (e) {
      setError(messageOf(e));
    } finally {
      setCreating(false);
    }
  };

  const kindLabel = POLICY_KINDS.find((k) => k.id === kind)!.label;

  return (
    <div className="flex flex-col gap-4">
      <header>
        <h1 className="text-h2 text-ink">Chính sách và điều khoản</h1>
        <p className="mt-1 max-w-3xl text-sm text-ink-2">
          Soạn, xem trước và công bố văn bản theo phiên bản. Bản đã công bố bị khóa, không sửa được; muốn đổi nội dung thì tạo bản mới. Trang
          Điều khoản (/terms) và Chính sách bảo mật (/privacy) luôn hiện bản đang hiệu lực.
        </p>
      </header>

      <Segmented label="Loại văn bản" value={kind} options={POLICY_KINDS} onChange={changeKind} />

      {history.error ? <Alert variant="destructive"><AlertDescription>{history.error}</AlertDescription></Alert> : null}
      {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
      {notice ? <Alert><AlertDescription>{notice}</AlertDescription></Alert> : null}

      <Card data-loading={history.loading ? "true" : "false"}>
        <CardHeader>
          <CardTitle>
            <h2 className="flex flex-wrap items-center gap-2 text-base font-semibold text-ink">
              {kindLabel}
              {published ? <Badge variant="ok">Đang hiệu lực: phiên bản {published.version}</Badge> : rows ? <Badge variant="neutral">Chưa công bố</Badge> : null}
            </h2>
          </CardTitle>
          <CardDescription>
            {published
              ? `Công bố ${formatDateTime(published.publishedAt)}. ${published.changeSummary ?? ""}`
              : rows ? "Loại này chưa có bản nào được công bố. Soạn bản đầu tiên bên dưới." : "Đang tải..."}
          </CardDescription>
        </CardHeader>
        {rows && !draftRow ? (
          <CardContent className="flex flex-col items-start gap-2">
            <Button type="button" onClick={() => void createDraft()} loading={creating}>
              <FilePlus2Icon data-icon="inline-start" />
              Soạn bản mới
            </Button>
            <p className="text-xs text-ink-3">
              {published
                ? "Hệ thống chép toàn văn bản đang hiệu lực thành bản nháp để bạn sửa chỗ cần đổi. Người dùng vẫn thấy bản hiện tại cho tới khi bạn công bố."
                : "Bắt đầu một bản nháp trống."}
            </p>
          </CardContent>
        ) : null}
      </Card>

      {draftRow ? (
        draftDoc ? (
          <PolicyEditor
            key={draftDoc.id}
            draft={draftDoc}
            currentVersion={published?.version ?? null}
            onPublished={(doc) => { setNotice(`Đã công bố ${kindLabel}, phiên bản ${doc.version}.`); setError(null); refresh(); }}
            onDiscarded={() => { setNotice("Đã bỏ bản nháp."); setError(null); refresh(); }}
          />
        ) : draft.error ? (
          <Alert variant="destructive"><AlertDescription>{draft.error}</AlertDescription></Alert>
        ) : (
          <Card><CardContent className="py-6 text-sm text-ink-2">Đang tải bản nháp...</CardContent></Card>
        )
      ) : null}

      <PolicyHistory rows={rows} loading={history.loading} />
    </div>
  );
}
