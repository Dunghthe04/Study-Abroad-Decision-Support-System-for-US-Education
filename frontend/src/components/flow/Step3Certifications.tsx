"use client";

import { ArrowLeftIcon, ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MonthPicker, type MonthValue } from "@/components/ui/month-picker";
import { cn } from "@/lib/utils";
import type { CertificationDraft, EnglishTestType, OtherTestDraft } from "@/types/profile-flow";

interface Step3CertificationsProps {
  data: CertificationDraft;
  onUpdate: (updated: Partial<CertificationDraft>) => void;
  onPrev: () => void;
  onNext: () => void;
}

const englishTests: { key: EnglishTestType; label: string; max: number; step: string; placeholder: string }[] = [
  { key: "IELTS", label: "IELTS", max: 9.0, step: "0.5", placeholder: "6.5" },
  { key: "TOEFL", label: "TOEFL iBT", max: 120, step: "1", placeholder: "90" },
  { key: "Duolingo", label: "Duolingo", max: 160, step: "5", placeholder: "115" },
  { key: "none", label: "Chưa có", max: 0, step: "1", placeholder: "" },
];

type StandardizedTest = OtherTestDraft["testType"];
const standardizedTests: (StandardizedTest | "Chưa có")[] = ["SAT", "ACT", "AP", "Chưa có"];

/** Ngày thi lưu dạng "yyyy-MM". */
function parseTestMonth(value?: string | null): MonthValue | null {
  const m = /^(\d{4})-(\d{2})/.exec(value ?? "");
  return m ? { year: Number(m[1]), month: Number(m[2]) } : null;
}

export function Step3Certifications({ data, onUpdate, onPrev, onNext }: Step3CertificationsProps) {
  const currentEnglish = data.englishTestType ?? "none";
  const otherTests = data.otherTests ?? [];

  const handleSelectEnglish = (key: EnglishTestType) => {
    if (key === "none") {
      onUpdate({ englishTestType: "none", englishOverallScore: null, englishTestDate: null });
    } else if (key !== currentEnglish) {
      // Đổi loại chứng chỉ thì điểm cũ không còn đúng thang
      onUpdate({ englishTestType: key, englishOverallScore: null });
    }
  };

  const toggleStandardizedTest = (test: StandardizedTest | "Chưa có") => {
    if (test === "Chưa có") {
      onUpdate({ otherTests: [] });
      return;
    }

    const exists = otherTests.some((t) => t.testType === test);
    if (exists) {
      onUpdate({ otherTests: otherTests.filter((t) => t.testType !== test) });
    } else {
      onUpdate({ otherTests: [...otherTests, { testType: test, score: null }] });
    }
  };

  const updateOtherTestScore = (testType: StandardizedTest, scoreStr: string) => {
    const scoreVal = scoreStr.trim() === "" ? null : Number(scoreStr);
    const updated = otherTests.map((t) => {
      if (t.testType !== testType) return t;
      return { ...t, score: scoreVal };
    });
    onUpdate({ otherTests: updated });
  };

  const satTest = otherTests.find((t) => t.testType === "SAT");
  const actTest = otherTests.find((t) => t.testType === "ACT");
  const apTest = otherTests.find((t) => t.testType === "AP");

  return (
    <div className="space-y-8 animate-in fade-in-50 duration-200">
      {/* Step Header */}
      <div className="space-y-2 border-b border-line pb-4">
        <div className="text-xs font-semibold text-brand tracking-wide uppercase">
          Bước 3/6 · khoảng 1 phút
        </div>
        <h1 className="text-h1 text-foreground">Chứng chỉ của bạn</h1>
        <p className="text-body text-muted-foreground">
          Chưa có cũng không sao, bạn chọn &quot;Chưa có&quot; rồi đi tiếp.
        </p>
      </div>

      {/* Question 1: English Certification */}
      <section className="space-y-4">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Bạn đã có chứng chỉ tiếng Anh chưa?
          </Label>
        </div>

        <div className="flex flex-wrap gap-2.5">
          {englishTests.map((t) => {
            const isSelected = currentEnglish === t.key;
            return (
              <button
                key={t.key}
                type="button"
                onClick={() => handleSelectEnglish(t.key)}
                className={cn(
                  "rounded-xl border px-4 py-2.5 text-sm font-semibold transition-all cursor-pointer",
                  isSelected
                    ? "border-brand bg-brand text-white shadow-xs"
                    : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                )}
              >
                {t.label}
              </button>
            );
          })}
        </div>

        {/* English Score inputs if test selected */}
        {currentEnglish !== "none" && (
          <div className="grid gap-4 sm:grid-cols-2 rounded-xl border border-line bg-ground/50 p-4">
            <div className="space-y-1.5">
              <Label htmlFor="englishOverallScore" className="text-xs font-medium text-foreground">
                Điểm tổng ({currentEnglish})
              </Label>
              <Input
                id="englishOverallScore"
                type="number"
                step={englishTests.find((x) => x.key === currentEnglish)?.step ?? "1"}
                min={0}
                max={englishTests.find((x) => x.key === currentEnglish)?.max}
                placeholder={
                  englishTests.find((x) => x.key === currentEnglish)?.placeholder ?? "Điểm"
                }
                value={data.englishOverallScore ?? ""}
                onChange={(e) =>
                  onUpdate({
                    englishOverallScore: e.target.value ? Number(e.target.value) : null,
                  })
                }
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="englishTestDate" className="text-xs font-medium text-foreground">
                Ngày thi (hoặc tháng/năm thi)
              </Label>
              <MonthPicker
                id="englishTestDate"
                placeholder="Chọn tháng thi"
                value={parseTestMonth(data.englishTestDate)}
                max={{ year: new Date().getFullYear(), month: new Date().getMonth() + 1 }}
                onChange={(m) => onUpdate({ englishTestDate: `${m.year}-${String(m.month).padStart(2, "0")}` })}
              />
            </div>
          </div>
        )}
      </section>

      {/* Question 2: Standardized Tests */}
      <section className="space-y-4 pt-2">
        <div>
          <Label className="text-sm font-semibold text-foreground">
            Bạn có bài thi nào khác không?
          </Label>
          <span className="text-xs text-muted-foreground ml-2">Chọn được nhiều bài</span>
        </div>

        <div className="flex flex-wrap gap-2.5">
          {standardizedTests.map((t) => {
            const isSelected =
              t === "Chưa có" ? otherTests.length === 0 : otherTests.some((x) => x.testType === t);
            return (
              <button
                key={t}
                type="button"
                onClick={() => toggleStandardizedTest(t)}
                className={cn(
                  "rounded-xl border px-4 py-2.5 text-sm font-semibold transition-all cursor-pointer",
                  isSelected
                    ? "border-brand bg-brand text-white shadow-xs"
                    : "border-line bg-surface text-muted-foreground hover:border-brand/40 hover:text-foreground"
                )}
              >
                {t}
              </button>
            );
          })}
        </div>

        {/* Detailed inputs for selected tests */}
        {satTest && (
          <div className="space-y-1.5 rounded-xl border border-line bg-ground/50 p-4 max-w-sm">
            <Label htmlFor="satScore" className="text-xs font-medium text-foreground">
              Tổng điểm SAT (400 – 1600)
            </Label>
            <Input
              id="satScore"
              type="number"
              min={400}
              max={1600}
              placeholder="VD: 1350"
              value={satTest.score ?? ""}
              onChange={(e) => updateOtherTestScore("SAT", e.target.value)}
            />
          </div>
        )}

        {actTest && (
          <div className="space-y-1.5 rounded-xl border border-line bg-ground/50 p-4 max-w-sm">
            <Label htmlFor="actScore" className="text-xs font-medium text-foreground">
              Điểm ACT Composite (1 – 36)
            </Label>
            <Input
              id="actScore"
              type="number"
              min={1}
              max={36}
              placeholder="VD: 30"
              value={actTest.score ?? ""}
              onChange={(e) => updateOtherTestScore("ACT", e.target.value)}
            />
          </div>
        )}

        {apTest && (
          <div className="space-y-1.5 rounded-xl border border-line bg-ground/50 p-4 max-w-sm">
            <Label htmlFor="apScore" className="text-xs font-medium text-foreground">
              Số môn thi AP (hoặc điểm trung bình AP 1–5)
            </Label>
            <Input
              id="apScore"
              type="number"
              min={1}
              max={20}
              placeholder="VD: 3 môn (Điểm 4–5)"
              value={apTest.score ?? ""}
              onChange={(e) => updateOtherTestScore("AP", e.target.value)}
            />
          </div>
        )}
      </section>

      {/* Bottom Actions */}
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
