"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { MonthPicker } from "@/components/ui/month-picker";
import { findCertType, newCertId, parseYearMonth, toYearMonth } from "@/lib/certification-utils";
import type {
  CertificationCategory,
  CertificationDraft,
  CertificationItem,
  CertificationTypeOption,
} from "@/types/profile-flow";
import { CertificationCard } from "./certifications/CertificationCard";
import { CertificationForm } from "./certifications/CertificationForm";

interface Step3CertificationsProps {
  data: CertificationDraft;
  /** Danh mục loại chứng chỉ từ metadata; rỗng khi chưa tải được */
  certificationTypes: CertificationTypeOption[];
  onUpdate: (updated: Partial<CertificationDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

interface SectionConfig {
  key: "english" | "tests";
  title: string;
  hint: string;
  addLabel: string;
  formTitle: string;
  categories: CertificationCategory[];
}

const SECTIONS: SectionConfig[] = [
  {
    key: "english",
    title: "Chứng chỉ tiếng Anh",
    hint: "Hầu hết trường Mỹ yêu cầu với học sinh quốc tế",
    addLabel: "Thêm chứng chỉ tiếng Anh",
    formTitle: "Thêm chứng chỉ tiếng Anh",
    categories: ["english"],
  },
  {
    key: "tests",
    title: "Bài thi chuẩn hóa và học thuật",
    hint: "SAT, ACT, AP, IB, A-Level... · Không bắt buộc",
    addLabel: "Thêm bài thi",
    formTitle: "Thêm bài thi",
    categories: ["standardized", "graduate"],
  },
];

// Mỗi lúc chỉ mở 1 form: thêm mới ở một mục, hoặc sửa một thẻ
type EditingState = { mode: "add"; section: SectionConfig["key"]; draft: CertificationItem } | { mode: "edit"; id: string };

export function Step3Certifications({ data, certificationTypes, onUpdate, onPrev, onNext }: Step3CertificationsProps) {
  const certifications = data.certifications ?? [];
  const [editing, setEditing] = useState<EditingState | null>(null);
  const hasEnglish = certifications.some((c) => c.category === "english");

  const now = new Date();
  const thisMonth = { year: now.getFullYear(), month: now.getMonth() + 1 };

  const startAdd = (section: SectionConfig) => {
    setEditing({
      mode: "add",
      section: section.key,
      draft: { id: newCertId(), category: section.categories[0], type: "" },
    });
  };

  const handleSave = (cert: CertificationItem) => {
    const exists = certifications.some((c) => c.id === cert.id);
    // Đã có chứng chỉ tiếng Anh thì bỏ cờ "sẽ thi sau"
    const englishAdded = cert.category === "english";
    onUpdate({
      certifications: exists
        ? certifications.map((c) => (c.id === cert.id ? cert : c))
        : [...certifications, cert],
      ...(englishAdded && data.englishPlanned ? { englishPlanned: false, englishPlannedDate: null } : {}),
    });
    setEditing(null);
  };

  const handleRemove = (id: string) => {
    onUpdate({ certifications: certifications.filter((c) => c.id !== id) });
    if (editing?.mode === "edit" && editing.id === id) setEditing(null);
  };

  const renderSection = (section: SectionConfig) => {
    const items = certifications.filter((c) => section.categories.includes(c.category));
    const isAddingHere = editing?.mode === "add" && editing.section === section.key;

    return (
      <section className="space-y-3">
        <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
          <h2 className="text-sm font-semibold text-foreground">{section.title}</h2>
          <span className="text-xs text-muted-foreground">{section.hint}</span>
        </div>

        {items.map((cert) =>
          editing?.mode === "edit" && editing.id === cert.id ? (
            <CertificationForm
              key={cert.id}
              title="Sửa chứng chỉ"
              initial={cert}
              types={certificationTypes}
              categories={section.categories}
              otherCategory={section.categories[0]}
              onSave={handleSave}
              onCancel={() => setEditing(null)}
            />
          ) : (
            <CertificationCard
              key={cert.id}
              cert={cert}
              type={findCertType(certificationTypes, cert.type)}
              disabled={editing !== null}
              onEdit={() => setEditing({ mode: "edit", id: cert.id })}
              onRemove={() => handleRemove(cert.id)}
            />
          )
        )}

        {isAddingHere ? (
          <CertificationForm
            key={editing.draft.id}
            title={section.formTitle}
            initial={editing.draft}
            types={certificationTypes}
            categories={section.categories}
            otherCategory={section.categories[0]}
            onSave={handleSave}
            onCancel={() => setEditing(null)}
          />
        ) : section.key === "english" && data.englishPlanned ? null : (
          <button
            type="button"
            onClick={() => startAdd(section)}
            disabled={editing !== null}
            className="flex w-full cursor-pointer items-center justify-center gap-1.5 rounded-xl border border-dashed border-line px-4 py-3 text-sm font-medium text-brand transition-colors hover:border-brand/50 hover:bg-brand-wash/40 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <PlusIcon className="size-4" />
            {section.addLabel}
          </button>
        )}
      </section>
    );
  };

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">Bước 3/6 · khoảng 1 phút</div>
        <h1 className="text-h1 text-foreground">Chứng chỉ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Thêm từng chứng chỉ bạn đã có. Chưa có cũng không sao, bạn có thể bổ sung sau.
        </p>
      </div>

      <div className="space-y-4">
        {renderSection(SECTIONS[0])}

        {/* Kế hoạch thi tiếng Anh — chỉ hỏi khi chưa có chứng chỉ tiếng Anh nào */}
        {!hasEnglish && (
        <div className="space-y-3 rounded-xl bg-ground/50 px-4 py-3">
          <Label className="flex cursor-pointer items-center gap-2.5 text-sm font-normal text-foreground">
            <Checkbox
              checked={data.englishPlanned}
              onCheckedChange={(checked) =>
                onUpdate({ englishPlanned: checked, englishPlannedDate: checked ? data.englishPlannedDate : null })
              }
            />
            Tôi chưa có chứng chỉ tiếng Anh, sẽ thi sau
          </Label>
          {data.englishPlanned && (
            <div className="max-w-xs space-y-1.5 pl-6.5">
              <Label htmlFor="englishPlannedDate" className="text-xs font-medium">
                Dự kiến thi (tháng/năm)
              </Label>
              <MonthPicker
                id="englishPlannedDate"
                placeholder="Tháng / năm"
                value={parseYearMonth(data.englishPlannedDate)}
                min={thisMonth}
                onChange={(m) => onUpdate({ englishPlannedDate: toYearMonth(m) })}
              />
            </div>
          )}
        </div>
        )}
      </div>

      <div className="border-t border-line pt-6">{renderSection(SECTIONS[1])}</div>

      {/* Bottom Actions — bước không bắt buộc, luôn đi tiếp được */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={onNext} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
