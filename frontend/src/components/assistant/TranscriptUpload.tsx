"use client";

import { useRef, useState } from "react";
import { UploadIcon } from "lucide-react";
import { profileFlowApi } from "@/lib/profile-flow-api";
import { calculateLinearGpa } from "@/lib/profile-flow-options";
import {
  TRANSCRIPT_ACCEPT,
  TRANSCRIPT_EXTENSIONS,
  TRANSCRIPT_MAX_BYTES,
  getInitialAcademicYears,
  mergeExtractedTerms,
} from "@/components/flow/Step2Transcript";

/** D2: tải học bạ giống bước 2 của form. Điểm đọc được ghi vào nháp (chế độ học bạ), rồi gửi "GPA x thang 10" cho chat */
export function TranscriptUpload({ onPick }: { onPick?: (text: string) => void }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const upload = async (file: File) => {
    if (!onPick) return;
    const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
    if (!TRANSCRIPT_EXTENSIONS.includes(ext)) return setNotice("Chỉ nhận tệp PDF, PNG, JPG hoặc JPEG.");
    if (file.size > TRANSCRIPT_MAX_BYTES) return setNotice("Tệp quá lớn. Bảng điểm tối đa 10MB.");

    setUploading(true);
    setNotice(null);
    try {
      const res = await profileFlowApi.uploadDraftTranscript([file]);
      const terms = (res.extractedTerms ?? []).filter((t) => t.scores?.length > 0);
      const state = terms.length > 0 ? await profileFlowApi.getFlowState() : null;
      if (!state) {
        setNotice(res.ocrMessage || "Mình chưa đọc được điểm từ tệp này. Bạn gửi ảnh rõ hơn hoặc gõ GPA vào ô chat nhé.");
        return;
      }
      // Gộp điểm vào các năm học như form, tính GPA rồi lưu nháp để form hiện đúng bảng điểm
      const { draft } = state;
      const current = draft.step2?.academicYears?.length
        ? draft.step2.academicYears
        : getInitialAcademicYears(draft.step1?.currentEducationLevel, draft.step1?.currentGrade);
      const academicYears = mergeExtractedTerms(current, terms, 0);
      const gpa = calculateLinearGpa(academicYears);
      if (gpa.gpa10 == null) {
        setNotice("Mình chưa tính được GPA từ bảng điểm này. Bạn gõ GPA vào ô chat nhé.");
        return;
      }
      await profileFlowApi.saveDraft(state.currentStep, state.completedSteps, {
        ...draft,
        step2: {
          ...draft.step2,
          gpaMode: "transcript",
          academicYears,
          manualGpa10Scale: gpa.gpa10,
          calculatedGpa4Scale: gpa.gpa4,
        },
      });
      onPick(`GPA ${gpa.gpa10} thang 10`);
    } catch (err: unknown) {
      setNotice(err instanceof Error ? err.message : "Tải học bạ thất bại, bạn thử lại nhé.");
    } finally {
      setUploading(false);
      if (inputRef.current) inputRef.current.value = "";
    }
  };

  return (
    <div className="space-y-1">
      <input
        ref={inputRef}
        type="file"
        accept={TRANSCRIPT_ACCEPT}
        className="hidden"
        onChange={(e) => e.target.files?.[0] && void upload(e.target.files[0])}
      />
      <button
        type="button"
        disabled={!onPick || uploading}
        onClick={() => inputRef.current?.click()}
        className="inline-flex h-7 cursor-pointer items-center gap-1 rounded-full border border-brand/40 px-3 text-xs font-medium text-brand transition-colors hover:bg-brand-wash disabled:cursor-not-allowed disabled:opacity-50"
      >
        <UploadIcon className="size-3.5" />
        {uploading ? "Đang đọc học bạ…" : "Tải học bạ (PDF, PNG, JPG, tối đa 10MB)"}
      </button>
      {notice && <p className="text-[11px] text-warn">{notice}</p>}
    </div>
  );
}
