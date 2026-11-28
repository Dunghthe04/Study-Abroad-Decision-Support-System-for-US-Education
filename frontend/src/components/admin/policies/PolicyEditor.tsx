"use client";

import { useState } from "react";
import { SaveIcon, SendIcon, Trash2Icon, UploadIcon } from "lucide-react";
import { PolicyContent } from "@/components/policy/PolicyContent";
import { Segmented } from "@/components/admin/ai-usage/parts";
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogIcon, AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { ApiError } from "@/lib/api";
import { adminPoliciesApi } from "@/lib/admin-policies-api";
import { MAX_SUMMARY, MAX_TITLE, formatDateTime, validateDraft } from "@/lib/policy-format";
import type { PolicyDocument } from "@/types/policy";

interface PolicyEditorProps {
  draft: PolicyDocument;
  /** Phiên bản đang hiệu lực mà bản nháp sẽ thay (null nếu chưa có). */
  currentVersion: string | null;
  onPublished: (published: PolicyDocument) => void;
  onDiscarded: () => void;
}

type Confirm = "publish" | "discard" | null;

const messageOf = (error: unknown) =>
  error instanceof ApiError || error instanceof Error ? error.message : "Đã có lỗi xảy ra. Bạn thử lại nhé.";

/** Soạn bản nháp: sửa, xem trước, lưu, công bố, bỏ. Dùng key={draft.id} ở nơi gọi để mỗi bản nháp bắt đầu với dữ liệu của nó. */
export function PolicyEditor({ draft, currentVersion, onPublished, onDiscarded }: PolicyEditorProps) {
  const [saved, setSaved] = useState(draft);
  const [title, setTitle] = useState(draft.title);
  const [content, setContent] = useState(draft.content);
  const [summary, setSummary] = useState(draft.changeSummary ?? "");
  const [reconsent, setReconsent] = useState(draft.requiresReconsent);
  const [mode, setMode] = useState<"edit" | "preview">("edit");
  const [confirm, setConfirm] = useState<Confirm>(null);
  const [busy, setBusy] = useState<"save" | "publish" | "discard" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const dirty = title !== saved.title || content !== saved.content || summary !== (saved.changeSummary ?? "") || reconsent !== saved.requiresReconsent;
  const request = { title, content, changeSummary: summary.trim() || null, requiresReconsent: reconsent };

  const save = async (): Promise<PolicyDocument | null> => {
    const problem = validateDraft({ title, content, changeSummary: summary }, false);
    if (problem) { setError(problem); return null; }
    const result = await adminPoliciesApi.updateDraft(saved.id, request);
    setSaved(result);
    return result;
  };

  const onSave = async () => {
    setBusy("save"); setError(null); setNotice(null);
    try {
      if (await save()) setNotice("Đã lưu bản nháp.");
    } catch (e) {
      setError(messageOf(e));
    } finally {
      setBusy(null);
    }
  };

  const askPublish = () => {
    const problem = validateDraft({ title, content, changeSummary: summary }, true);
    setError(problem);
    setNotice(null);
    if (!problem) setConfirm("publish");
  };

  // Công bố luôn lưu nội dung đang soạn trước, rồi mới công bố đúng bản vừa lưu
  const doPublish = async () => {
    setBusy("publish"); setError(null);
    try {
      if (dirty && !(await save())) { setConfirm(null); return; }
      const published = await adminPoliciesApi.publish(saved.id);
      setConfirm(null);
      onPublished(published);
    } catch (e) {
      setConfirm(null);
      setError(messageOf(e));
    } finally {
      setBusy(null);
    }
  };

  const doDiscard = async () => {
    setBusy("discard"); setError(null);
    try {
      await adminPoliciesApi.discardDraft(saved.id);
      setConfirm(null);
      onDiscarded();
    } catch (e) {
      setConfirm(null);
      setError(messageOf(e));
    } finally {
      setBusy(null);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2 className="flex flex-wrap items-center gap-2 text-base font-semibold text-ink">
            Bản nháp đang soạn
            <Badge variant="warn">Nháp</Badge>
            {dirty ? <Badge variant="neutral">Chưa lưu</Badge> : null}
          </h2>
        </CardTitle>
        <CardDescription>
          Tạo ngày {formatDateTime(saved.createdAt)}. Hệ thống đã chép nội dung của bản đang hiệu lực
          {currentVersion ? ` (phiên bản ${currentVersion})` : ""}; bạn chỉ cần sửa chỗ cần đổi. Bản đang hiệu lực vẫn hiện cho người dùng cho tới khi bạn công bố.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Segmented
          label="Chế độ"
          value={mode}
          options={[{ id: "edit", label: "Soạn" }, { id: "preview", label: "Xem trước" }]}
          onChange={setMode}
        />

        {mode === "edit" ? (
          <div className="flex flex-col gap-4">
            <label className="flex flex-col gap-1 text-sm font-medium text-ink">
              Tiêu đề
              <Input value={title} maxLength={MAX_TITLE} onChange={(e) => setTitle(e.target.value)} disabled={busy !== null} />
            </label>
            <label className="flex flex-col gap-1 text-sm font-medium text-ink">
              Nội dung
              <Textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                rows={18}
                disabled={busy !== null}
                className="font-mono text-xs leading-relaxed"
                spellCheck
              />
              <span className="text-xs font-normal text-ink-3">
                Gõ theo kiểu Markdown: <code>## Tiêu đề mục</code> · <code>- gạch đầu dòng</code> · <code>1. danh sách đánh số</code> ·{" "}
                <code>**in đậm**</code> · <code>[chữ](https://...)</code>. Bấm Xem trước để thấy người dùng sẽ đọc thế nào. {content.length.toLocaleString("vi-VN")} ký tự.
              </span>
            </label>
            <label className="flex flex-col gap-1 text-sm font-medium text-ink">
              Tóm tắt thay đổi <span className="font-normal text-ink-3">(bắt buộc khi công bố, người dùng sẽ thấy dòng này)</span>
              <Input
                value={summary}
                maxLength={MAX_SUMMARY}
                onChange={(e) => setSummary(e.target.value)}
                placeholder="Ví dụ: Thêm mục 5.3 về việc đánh giá chất lượng AI"
                disabled={busy !== null}
              />
            </label>
            <label className="flex items-start gap-2 text-sm text-ink">
              <Checkbox checked={reconsent} onCheckedChange={(on) => setReconsent(on === true)} disabled={busy !== null} className="mt-0.5" />
              <span>
                <span className="font-medium">Yêu cầu người dùng đồng ý lại</span>
                <span className="block text-xs text-ink-3">
                  Chọn khi thay đổi quan trọng (thêm điều khoản, đổi mục đích dùng dữ liệu). Sửa lỗi chính tả thì không chọn.
                </span>
              </span>
            </label>
          </div>
        ) : (
          <div className="rounded-lg border border-line p-5">
            <h3 className="mb-3 font-heading text-2xl font-bold text-ink">{title || "(chưa có tiêu đề)"}</h3>
            {content.trim() ? <PolicyContent content={content} /> : <p className="text-sm text-ink-3">Chưa có nội dung.</p>}
          </div>
        )}

        {error ? <Alert variant="destructive"><AlertDescription>{error}</AlertDescription></Alert> : null}
        {notice ? <Alert><AlertDescription>{notice}</AlertDescription></Alert> : null}

        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" variant="outline" onClick={() => void onSave()} disabled={!dirty || busy !== null} loading={busy === "save"}>
            <SaveIcon data-icon="inline-start" />
            Lưu nháp
          </Button>
          <Button type="button" onClick={askPublish} disabled={busy !== null}>
            <SendIcon data-icon="inline-start" />
            Công bố
          </Button>
          <Button type="button" variant="ghost" onClick={() => setConfirm("discard")} disabled={busy !== null} className="ml-auto text-destructive">
            <Trash2Icon data-icon="inline-start" />
            Bỏ bản nháp
          </Button>
        </div>
      </CardContent>

      <AlertDialog open={confirm !== null} onOpenChange={(open) => !open && busy === null && setConfirm(null)}>
        <AlertDialogContent variant="action">
          <AlertDialogHeader>
            <AlertDialogIcon tone={confirm === "discard" ? "destructive" : "brand"}>
              {confirm === "discard" ? <Trash2Icon /> : <UploadIcon />}
            </AlertDialogIcon>
            <AlertDialogTitle>{confirm === "publish" ? "Công bố bản này?" : "Bỏ bản nháp này?"}</AlertDialogTitle>
            <AlertDialogDescription>
              {confirm === "publish" ? (
                <>
                  Bản này sẽ có hiệu lực ngay và <strong>bị khóa, không sửa được nữa</strong>
                  {currentVersion ? `; bản ${currentVersion} chuyển sang hết hiệu lực (vẫn giữ để tra cứu)` : ""}.
                  {reconsent ? " Người dùng sẽ được hỏi đồng ý lại." : " Người dùng không bị hỏi đồng ý lại."}
                  {dirty ? " Nội dung đang soạn sẽ được lưu trước khi công bố." : ""}
                </>
              ) : (
                "Nội dung đang soạn sẽ bị xóa hẳn. Bản đang hiệu lực không bị ảnh hưởng."
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel disabled={busy !== null}>Hủy</AlertDialogCancel>
            <AlertDialogAction
              tone={confirm === "discard" ? "destructive" : "primary"}
              onClick={() => void (confirm === "publish" ? doPublish() : doDiscard())}
              loading={busy !== null}
            >
              {confirm === "publish" ? "Công bố" : "Bỏ bản nháp"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}
