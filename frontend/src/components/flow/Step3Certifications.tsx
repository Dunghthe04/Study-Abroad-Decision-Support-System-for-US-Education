"use client";

import { useState } from "react";
import { ArrowLeftIcon, ArrowRightIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { findCertType, newCertId, validatePlanConflicts } from "@/lib/certification-utils";
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
    addLabel: "Thêm chứng chỉ hoặc dự định thi tiếng Anh",
    formTitle: "Thêm chứng chỉ tiếng Anh",
    categories: ["english"],
  },
  {
    key: "tests",
    title: "Bài thi chuẩn hóa và học thuật",
    hint: "SAT, ACT, AP, IB, A-Level...",
    addLabel: "Thêm bài thi hoặc dự định thi",
    formTitle: "Thêm bài thi",
    categories: ["standardized", "graduate"],
  },
];

// Mỗi lúc chỉ mở 1 form: thêm mới ở một mục, hoặc sửa một thẻ.
// Sửa có thể mở với bản khác thẻ (dự định quá hạn bấm "Đã thi rồi" / "Đổi tháng")
type EditingState =
  | { mode: "add"; section: SectionConfig["key"]; draft: CertificationItem }
  | { mode: "edit"; id: string; initial?: CertificationItem };

export function Step3Certifications({ data, certificationTypes, onUpdate, onPrev, onNext }: Step3CertificationsProps) {
  const certifications = data.certifications ?? [];
  const [editing, setEditing] = useState<EditingState | null>(null);
  // Thẻ đã thi và thẻ dự định thi đều tính là đã khai mục đó
  const hasEnglish = certifications.some((c) => c.category === "english");
  const hasStandardized = certifications.some((c) => ["standardized", "graduate"].includes(c.category));
  // Mỗi mục phải có 1 trong 2: thẻ (đã thi hoặc dự định thi) hoặc tích "chưa thi và chưa có dự định thi".
  // Chỉ báo đỏ sau khi đã bấm Tiếp tục; điền xong mục nào thì mục đó tự hết đỏ
  const [triedNext, setTriedNext] = useState(false);
  const englishMissing = triedNext && !hasEnglish && !data.englishPlanned;
  const testsMissing = triedNext && !hasStandardized && !data.standardizedPlanned;

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
    // Có thẻ thì mục đó không còn "chưa thi và chưa có dự định thi"
    onUpdate({
      certifications: exists
        ? certifications.map((c) => (c.id === cert.id ? cert : c))
        : [...certifications, cert],
      ...(englishAdded && data.englishPlanned ? { englishPlanned: false } : {}),
      ...(standardizedAdded && data.standardizedPlanned ? { standardizedPlanned: false } : {}),
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

  const checkConflict = (cert: CertificationItem) =>
    validatePlanConflicts(cert, certifications, findCertType(certificationTypes, cert.type));

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

        {items.map((cert) => {
          if (editing?.mode === "edit" && editing.id === cert.id) {
            const initial = editing.initial ?? cert;
            return (
              <CertificationForm
                key={cert.id}
                title={initial.isTarget ? "Sửa dự định thi" : "Sửa chứng chỉ"}
                initial={initial}
                types={certificationTypes}
                categories={section.categories}
                otherCategory={section.categories[0]}
                onSave={handleSave}
                onCancel={() => setEditing(null)}
                checkConflict={checkConflict}
              />
            );
          }
          return (
            <CertificationCard
              key={cert.id}
              cert={cert}
              type={findCertType(certificationTypes, cert.type)}
              disabled={editing !== null}
              onEdit={() => setEditing({ mode: "edit", id: cert.id })}
              onRemove={() => handleRemove(cert.id)}
              onTaken={() =>
                setEditing({
                  mode: "edit",
                  id: cert.id,
                  // tháng dự kiến đã qua thường là tháng đã thi; điểm mục tiêu không phải điểm thật nên bỏ
                  initial: { ...cert, isTarget: false, overall: null, scaleText: null, sections: null },
                })
              }
              onChangeMonth={() => setEditing({ mode: "edit", id: cert.id, initial: { ...cert, testDate: null } })}
            />
          );
        })}

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
            checkConflict={checkConflict}
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

  // Tick "chưa thi và chưa có dự định thi" — chỉ hỏi khi mục chưa có thẻ nào
  const renderNoPlanTick = (checked: boolean, onChange: (value: boolean) => void, label: string) => (
    <Label className="flex cursor-pointer items-center gap-2.5 rounded-xl border border-line/60 bg-ground/50 p-4 text-sm font-medium text-foreground">
      <Checkbox checked={checked} onCheckedChange={(value) => onChange(Boolean(value))} />
      {label}
    </Label>
  );

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand">Bước 3/6 · khoảng 1 phút</div>
        <h1 className="text-h1 text-foreground">Chứng chỉ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Mỗi mục thêm chứng chỉ đã thi và các bài định thi (thi lần đầu hoặc thi lại để nâng điểm). Chưa thi và
          chưa định thi thì tích ô cuối mục.
        </p>
      </div>

      <div className={cn("space-y-4", englishMissing && "rounded-xl border border-destructive p-4")}>
        {renderSection(SECTIONS[0])}
        {!hasEnglish &&
          renderNoPlanTick(
            Boolean(data.englishPlanned),
            (value) => onUpdate({ englishPlanned: value }),
            "Tôi chưa thi tiếng Anh và chưa có dự định thi"
          )}
        {englishMissing && (
          <p className="text-xs font-medium text-destructive">
            Thêm chứng chỉ tiếng Anh, dự định thi, hoặc tích “chưa thi và chưa có dự định thi”.
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
        {!hasStandardized &&
          renderNoPlanTick(
            Boolean(data.standardizedPlanned),
            (value) => onUpdate({ standardizedPlanned: value }),
            "Tôi chưa thi bài thi chuẩn hóa và chưa có dự định thi"
          )}
        {testsMissing && (
          <p className="text-xs font-medium text-destructive">
            Thêm bài thi đã có điểm, dự định thi, hoặc tích “chưa thi và chưa có dự định thi”.
          </p>
        )}
      </div>

      {/* Bottom Actions — mỗi mục phải có thẻ hoặc tích "chưa thi và chưa có dự định thi" mới đi tiếp được */}
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
