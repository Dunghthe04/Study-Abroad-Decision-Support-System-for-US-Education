"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MonthPicker } from "@/components/ui/month-picker";
import { findCertType, newCertId, parseYearMonth, toYearMonth } from "@/lib/certification-utils";
import { cn } from "@/lib/utils";
import type {
  CertificationCategory,
  CertificationDraft,
  CertificationItem,
  CertificationTypeOption,
} from "@/types/profile-flow";
import { CertificationCard } from "./certifications/CertificationCard";
import { CertificationForm } from "./certifications/CertificationForm";
import { RequiredMark } from "./RequiredMark";

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
    hint: "SAT, ACT, AP, IB, A-Level...",
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
  const hasStandardized = certifications.some((c) => ["standardized", "graduate"].includes(c.category));
  // Mỗi mục phải có 1 trong 2: chứng chỉ đã thi (có điểm) hoặc tích "chưa thi, sẽ thi sau".
  // Chỉ báo đỏ sau khi đã bấm Tiếp tục; điền xong mục nào thì mục đó tự hết đỏ
  const [triedNext, setTriedNext] = useState(false);
  const englishMissing = triedNext && !hasEnglish && !data.englishPlanned;
  const testsMissing = triedNext && !hasStandardized && !data.standardizedPlanned;

  const englishTypes = certificationTypes.filter((t) => t.category === "english");
  const standardizedTypes = certificationTypes.filter((t) => ["standardized", "graduate"].includes(t.category));

  const selectedEnglishType = englishTypes.find((t) => t.id === data.englishPlannedTestType);
  const selectedStandardizedType = standardizedTypes.find((t) => t.id === data.standardizedPlannedTestType);

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
    const englishAdded = cert.category === "english";
    const standardizedAdded = ["standardized", "graduate"].includes(cert.category);
    onUpdate({
      certifications: exists
        ? certifications.map((c) => (c.id === cert.id ? cert : c))
        : [...certifications, cert],
      ...(englishAdded && data.englishPlanned
        ? {
            englishPlanned: false,
            englishPlannedDate: null,
            englishPlannedTestType: null,
            englishPlannedTargetScore: null,
          }
        : {}),
      ...(standardizedAdded && data.standardizedPlanned
        ? {
            standardizedPlanned: false,
            standardizedPlannedDate: null,
            standardizedPlannedTestType: null,
            standardizedPlannedTargetScore: null,
          }
        : {}),
    });
    setEditing(null);
  };

  const handleRemove = (id: string) => {
    onUpdate({ certifications: certifications.filter((c) => c.id !== id) });
    if (editing?.mode === "edit" && editing.id === id) setEditing(null);
  };

  const handleNextClick = () => {
    if ((!hasEnglish && !data.englishPlanned) || (!hasStandardized && !data.standardizedPlanned)) {
      setTriedNext(true);
      return;
    }
    onNext();
  };

  const renderSection = (section: SectionConfig) => {
    const items = certifications.filter((c) => section.categories.includes(c.category));
    const isAddingHere = editing?.mode === "add" && editing.section === section.key;

    return (
      <section className="space-y-3">
        <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
          <h2 className="text-sm font-semibold text-foreground">
            {section.title} <RequiredMark />
          </h2>
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
        ) : (
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
        <div className="text-xs font-semibold text-brand">Bước 3/6 · khoảng 1 phút</div>
        <h1 className="text-h1 text-foreground">Chứng chỉ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Mỗi mục thêm chứng chỉ bạn đã thi, hoặc tích “chưa thi, sẽ thi sau” nếu chưa có.
        </p>
      </div>

      <div className={cn("space-y-4", englishMissing && "rounded-xl border border-destructive p-4")}>
        {renderSection(SECTIONS[0])}

        {/* Kế hoạch thi tiếng Anh — chỉ hỏi khi chưa có chứng chỉ tiếng Anh nào */}
        {!hasEnglish && (
          <div className="space-y-3 rounded-xl bg-ground/50 p-4 border border-line/60">
            <Label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-foreground">
              <Checkbox
                checked={data.englishPlanned}
                onCheckedChange={(checked) =>
                  onUpdate({
                    englishPlanned: Boolean(checked),
                    englishPlannedDate: checked ? data.englishPlannedDate : null,
                    englishPlannedTestType: checked ? data.englishPlannedTestType : null,
                    englishPlannedTargetScore: checked ? data.englishPlannedTargetScore : null,
                  })
                }
              />
              Tôi chưa có chứng chỉ tiếng Anh, sẽ thi sau
            </Label>
            {data.englishPlanned && (
              <div className="grid gap-3 sm:grid-cols-3 pl-6.5 pt-1 animate-in fade-in-50 duration-150">
                <div className="space-y-1.5">
                  <Label htmlFor="englishPlannedTestType" className="text-xs font-medium text-muted-foreground">
                    Bài thi dự kiến (tùy chọn)
                  </Label>
                  <select
                    id="englishPlannedTestType"
                    value={data.englishPlannedTestType ?? ""}
                    onChange={(e) => onUpdate({ englishPlannedTestType: e.target.value || null })}
                    className="flex h-9 w-full rounded-md border border-line bg-surface px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-brand text-foreground"
                  >
                    <option value="">Chưa chọn bài thi</option>
                    {englishTypes.map((opt) => (
                      <option key={opt.id} value={opt.id}>
                        {opt.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="englishPlannedTargetScore" className="text-xs font-medium text-muted-foreground">
                    Điểm mục tiêu (tùy chọn)
                  </Label>
                  <Input
                    id="englishPlannedTargetScore"
                    type="number"
                    step={selectedEnglishType?.step ?? "any"}
                    placeholder={
                      selectedEnglishType && selectedEnglishType.min != null && selectedEnglishType.max != null
                        ? `${selectedEnglishType.min}–${selectedEnglishType.max}`
                        : "VD: 6.5"
                    }
                    value={data.englishPlannedTargetScore ?? ""}
                    onChange={(e) => {
                      const val = e.target.value.trim() === "" ? null : Number(e.target.value);
                      onUpdate({ englishPlannedTargetScore: val });
                    }}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="englishPlannedDate" className="text-xs font-medium text-muted-foreground">
                    Dự kiến thi (tùy chọn)
                  </Label>
                  <MonthPicker
                    id="englishPlannedDate"
                    placeholder="Tháng / năm"
                    value={parseYearMonth(data.englishPlannedDate)}
                    min={thisMonth}
                    onChange={(m) => onUpdate({ englishPlannedDate: toYearMonth(m) })}
                  />
                </div>
              </div>
            )}
          </div>
        )}
        {englishMissing && (
          <p className="text-xs font-medium text-destructive">
            Thêm chứng chỉ tiếng Anh đã thi hoặc tích “chưa có, sẽ thi sau”.
          </p>
        )}
      </div>

      <div
        className={cn(
          "space-y-4",
          testsMissing ? "rounded-xl border border-destructive p-4" : "border-t border-line pt-6"
        )}
      >
        {renderSection(SECTIONS[1])}

        {/* Kế hoạch thi bài thi chuẩn hóa — chỉ hỏi khi chưa có bài thi chuẩn hóa nào */}
        {!hasStandardized && (
          <div className="space-y-3 rounded-xl bg-ground/50 p-4 border border-line/60">
            <Label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-foreground">
              <Checkbox
                checked={data.standardizedPlanned}
                onCheckedChange={(checked) =>
                  onUpdate({
                    standardizedPlanned: Boolean(checked),
                    standardizedPlannedDate: checked ? data.standardizedPlannedDate : null,
                    standardizedPlannedTestType: checked ? data.standardizedPlannedTestType : null,
                    standardizedPlannedTargetScore: checked ? data.standardizedPlannedTargetScore : null,
                  })
                }
              />
              Tôi chưa thi bài thi chuẩn hóa, sẽ thi sau
            </Label>
            {data.standardizedPlanned && (
              <div className="grid gap-3 sm:grid-cols-3 pl-6.5 pt-1 animate-in fade-in-50 duration-150">
                <div className="space-y-1.5">
                  <Label htmlFor="standardizedPlannedTestType" className="text-xs font-medium text-muted-foreground">
                    Bài thi dự kiến (tùy chọn)
                  </Label>
                  <select
                    id="standardizedPlannedTestType"
                    value={data.standardizedPlannedTestType ?? ""}
                    onChange={(e) => onUpdate({ standardizedPlannedTestType: e.target.value || null })}
                    className="flex h-9 w-full rounded-md border border-line bg-surface px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-brand text-foreground"
                  >
                    <option value="">Chưa chọn bài thi</option>
                    {standardizedTypes.map((opt) => (
                      <option key={opt.id} value={opt.id}>
                        {opt.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="standardizedPlannedTargetScore" className="text-xs font-medium text-muted-foreground">
                    Điểm mục tiêu (tùy chọn)
                  </Label>
                  <Input
                    id="standardizedPlannedTargetScore"
                    type="number"
                    step={selectedStandardizedType?.step ?? "any"}
                    placeholder={
                      selectedStandardizedType && selectedStandardizedType.min != null && selectedStandardizedType.max != null
                        ? `${selectedStandardizedType.min}–${selectedStandardizedType.max}`
                        : "VD: 1450"
                    }
                    value={data.standardizedPlannedTargetScore ?? ""}
                    onChange={(e) => {
                      const val = e.target.value.trim() === "" ? null : Number(e.target.value);
                      onUpdate({ standardizedPlannedTargetScore: val });
                    }}
                  />
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="standardizedPlannedDate" className="text-xs font-medium text-muted-foreground">
                    Dự kiến thi (tùy chọn)
                  </Label>
                  <MonthPicker
                    id="standardizedPlannedDate"
                    placeholder="Tháng / năm"
                    value={parseYearMonth(data.standardizedPlannedDate)}
                    min={thisMonth}
                    onChange={(m) => onUpdate({ standardizedPlannedDate: toYearMonth(m) })}
                  />
                </div>
              </div>
            )}
          </div>
        )}
        {testsMissing && (
          <p className="text-xs font-medium text-destructive">
            Thêm bài thi đã có điểm hoặc tích “chưa thi, sẽ thi sau”.
          </p>
        )}
      </div>

      {/* Bottom Actions — mỗi mục phải có điểm hoặc tích chưa thi mới đi tiếp được */}
      <div className="flex justify-between pt-4 border-t border-line">
        <Button variant="outline" onClick={onPrev} size="lg" className="gap-1.5">
          <ArrowLeftIcon className="size-4" />
          Quay lại
        </Button>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </div>
    </div>
  );
}
