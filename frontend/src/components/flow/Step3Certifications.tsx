"use client";

import { useState } from "react";
import { ArrowRightIcon, PlusIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  findCertType,
  isPlanOverdue,
  newCertId,
  validateCertification,
  validatePlanConflicts,
} from "@/lib/certification-utils";
import type {
  CertificationCategory,
  CertificationDraft,
  CertificationItem,
  CertificationTypeOption,
} from "@/types/profile-flow";
import { CertificationCard } from "./certifications/CertificationCard";
import { ActionList, ActionRow, FormBody, FormGroup } from "./FormKit";
import { StepHeader, StepNav } from "./StepChrome";

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
  /** Tên nhóm, cũng là câu hỏi duy nhất của nhóm */
  title: string;
  hint: string;
  noneLabel: string;
  categories: CertificationCategory[];
}

const SECTIONS: SectionConfig[] = [
  {
    key: "english",
    title: "Chứng chỉ tiếng Anh",
    hint: "Hầu hết trường Mỹ yêu cầu với học sinh quốc tế",
    noneLabel: "Tôi chưa thi tiếng Anh và chưa có dự định thi",
    categories: ["english"],
  },
  {
    key: "tests",
    title: "Bài thi chuẩn hóa",
    hint: "SAT, ACT, AP, IB, A-Level...",
    noneLabel: "Tôi chưa thi bài thi chuẩn hóa và chưa có dự định thi",
    categories: ["standardized", "graduate"],
  },
];

const MISSING = "Thêm chứng chỉ, hoặc chọn “chưa thi và chưa có dự định thi”.";

export function Step3Certifications({ data, certificationTypes, onUpdate, onPrev, onNext }: Step3CertificationsProps) {
  const certifications = data.certifications ?? [];
  // Thẻ đã thi và thẻ dự định thi đều tính là đã khai mục đó
  const hasEnglish = certifications.some((c) => c.category === "english");
  const hasStandardized = certifications.some((c) => ["standardized", "graduate"].includes(c.category));
  // Mỗi mục phải có 1 trong 2: thẻ (đã thi hoặc dự định thi) hoặc chọn "chưa thi và chưa có dự định thi".
  // Chỉ báo lỗi sau khi đã bấm Tiếp tục; điền xong mục nào thì mục đó tự hết đỏ
  const [triedNext, setTriedNext] = useState(false);
  const missing = {
    english: triedNext && !hasEnglish && !data.englishPlanned,
    tests: triedNext && !hasStandardized && !data.standardizedPlanned,
  };

  // Kiểm tra từng thẻ như lúc bấm "Lưu" trước đây: luật theo metadata, rồi so với các thẻ khác
  const check = (cert: CertificationItem) => {
    const type = findCertType(certificationTypes, cert.type);
    const errors = validateCertification(cert, type);
    const conflict = Object.keys(errors).length === 0 ? validatePlanConflicts(cert, certifications, type) : null;
    return { errors, conflict };
  };

  const addCert = (section: SectionConfig) => {
    // Có thẻ thì mục đó không còn "chưa thi và chưa có dự định thi"
    onUpdate({
      certifications: [...certifications, { id: newCertId(), category: section.categories[0], type: "" }],
      ...(section.key === "english" && data.englishPlanned ? { englishPlanned: false } : {}),
      ...(section.key === "tests" && data.standardizedPlanned ? { standardizedPlanned: false } : {}),
    });
  };

  const handleChange = (cert: CertificationItem) =>
    onUpdate({ certifications: certifications.map((c) => (c.id === cert.id ? cert : c)) });

  const handleRemove = (id: string) => onUpdate({ certifications: certifications.filter((c) => c.id !== id) });

  const handleNextClick = () => {
    const incomplete = (!hasEnglish && !data.englishPlanned) || (!hasStandardized && !data.standardizedPlanned);
    const invalid = certifications.some((c) => {
      const { errors, conflict } = check(c);
      return Object.keys(errors).length > 0 || conflict;
    });
    if (incomplete || invalid) {
      setTriedNext(true);
      // Chờ lỗi hiện ra rồi cuộn tới lỗi đầu tiên
      requestAnimationFrame(() =>
        document
          .querySelector("#step3-english [role=alert], #step3-tests [role=alert]")
          ?.scrollIntoView({ block: "center", behavior: "smooth" }),
      );
      return;
    }
    onNext();
  };

  const renderSection = (section: SectionConfig) => {
    const items = certifications.filter((c) => section.categories.includes(c.category));
    const planned = section.key === "english" ? data.englishPlanned : data.standardizedPlanned;

    return (
      <FormGroup
        id={`step3-${section.key}`}
        title={section.title}
        description={section.hint}
        required
        error={missing[section.key] ? MISSING : null}
      >
        <div className="py-6">
        <ActionList>
          {items.map((cert, i) => {
            const { errors, conflict } = check(cert);
            return (
              <CertificationCard
                key={cert.id}
                index={i + 1}
                cert={cert}
                types={certificationTypes}
                categories={section.categories}
                otherCategory={section.categories[0]}
                // dự định đã qua tháng dự kiến luôn báo; lỗi khác chỉ báo sau khi bấm Tiếp tục
                errors={triedNext ? errors : isPlanOverdue(cert) && errors.testDate ? { testDate: errors.testDate } : {}}
                conflict={triedNext ? conflict : null}
                onChange={handleChange}
                onRemove={() => handleRemove(cert.id)}
              />
            );
          })}
          <ActionRow icon={<PlusIcon />} onClick={() => addCert(section)}>
            Thêm chứng chỉ
          </ActionRow>
          {items.length === 0 && (
            <ActionRow
              checked={Boolean(planned)}
              onClick={() => onUpdate(section.key === "english" ? { englishPlanned: !planned } : { standardizedPlanned: !planned })}
            >
              {section.noneLabel}
            </ActionRow>
          )}
        </ActionList>
        </div>
      </FormGroup>
    );
  };

  return (
    <>
      <StepHeader title="Chứng chỉ của bạn" />
      <FormBody>
        {renderSection(SECTIONS[0])}
        {renderSection(SECTIONS[1])}
      </FormBody>
      {/* mỗi mục phải có thẻ hợp lệ hoặc chọn "chưa thi và chưa có dự định thi" mới đi tiếp được */}
      <StepNav onPrev={onPrev}>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </>
  );
}
