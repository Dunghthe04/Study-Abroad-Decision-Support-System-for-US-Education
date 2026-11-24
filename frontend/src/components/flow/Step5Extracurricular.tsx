"use client";

import { useState } from "react";
import { ArrowRightIcon, PlusIcon, Trash2Icon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { MonthPicker } from "@/components/ui/month-picker";
import {
  ActionList,
  ActionRow,
  ChoicePicker,
  FormBody,
  FormGroup,
  FieldRow,
  SegmentedControl,
  type Option,
} from "./FormKit";
import { StepHeader, StepNav } from "./StepChrome";
import {
  ACTIVITY_ROLE_OPTIONS,
  IMPACT_LEVEL_OPTIONS,
  IMPACT_TIER_DETAILS,
  countMonths,
  parseMonthYear,
} from "@/lib/profile-flow-options";
import type { ActivityDraft, ActivityRole, ImpactLevel } from "@/types/profile-flow";

interface Step5ExtracurricularProps {
  activities: ActivityDraft[];
  onUpdate: (activities: ActivityDraft[]) => void;
  onPrev: () => void;
  onNext: () => void;
}

const MAX_ITEMS = 10;

/** Thiếu gì so với điều kiện commit phía BE (ProfileFlowService.ValidateForCommit). */
export function getItemProblem(item: ActivityDraft): string | null {
  if (!item.name.trim()) return "chưa có tên";
  if (!item.impactLevel) return "chưa chọn phạm vi";
  if (item.category === "achievement") return item.year ? null : "chưa có năm đạt giải";
  if (!item.role) return "chưa chọn vai trò";
  if (!parseMonthYear(item.startDate)) return "chưa có tháng bắt đầu";
  if (!item.isOngoing && !parseMonthYear(item.endDate)) return "chưa có tháng kết thúc";
  return null;
}

type ItemErrors = Partial<Record<"name" | "impact" | "year" | "role" | "start" | "end" | "desc", string>>;

const monthIndex = (m: { year: number; month: number }) => m.year * 12 + m.month;

/** Kiểm tra từng ô của một mục; chạy khi bấm "Tiếp tục" */
function validateItem(item: ActivityDraft, now: Date): ItemErrors {
  const errors: ItemErrors = {};
  const currentYear = now.getFullYear();
  if (!item.name.trim()) errors.name = "Vui lòng nhập tên.";
  if (!item.impactLevel) errors.impact = "Vui lòng chọn phạm vi ảnh hưởng.";
  if ((item.description ?? "").trim().length > 500) errors.desc = "Mô tả tối đa 500 ký tự.";

  if (item.category === "achievement") {
    const year = item.year;
    if (year == null) errors.year = "Vui lòng nhập năm đạt giải.";
    else if (!Number.isInteger(year) || year > currentYear)
      errors.year = `Năm đạt giải không được lớn hơn năm hiện tại (${currentYear}).`;
    else if (year < 2000) errors.year = "Năm đạt giải không hợp lệ (từ năm 2000 trở lại đây).";
    return errors;
  }

  if (!item.role) errors.role = "Vui lòng chọn vai trò của bạn.";
  const start = parseMonthYear(item.startDate);
  if (!start) errors.start = "Vui lòng chọn tháng bắt đầu.";
  else if (monthIndex(start) > currentYear * 12 + now.getMonth() + 1) errors.start = "Tháng bắt đầu không được sau tháng hiện tại.";
  if (!item.isOngoing) {
    const end = parseMonthYear(item.endDate);
    if (!end) errors.end = "Vui lòng chọn tháng kết thúc, hoặc tick “Đến nay”.";
    else if (start && monthIndex(end) < monthIndex(start)) errors.end = "Tháng kết thúc phải sau hoặc bằng tháng bắt đầu.";
  }
  return errors;
}

const CATEGORY_OPTIONS: Option<"activity" | "achievement">[] = [
  { value: "activity", label: "Hoạt động" },
  { value: "achievement", label: "Giải thưởng" },
];

const formatMonthYear = (m: { year: number; month: number }) => `${String(m.month).padStart(2, "0")}/${m.year}`;

const newItemId = () =>
  typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString();


export function Step5Extracurricular({ activities, onUpdate, onPrev, onNext }: Step5ExtracurricularProps) {
  // Chỉ báo lỗi sau khi bấm Tiếp tục; sửa xong ô nào thì ô đó tự hết đỏ
  const [triedNext, setTriedNext] = useState(false);

  const now = new Date();
  const currentYear = now.getFullYear();
  const thisMonth = { year: currentYear, month: now.getMonth() + 1 };

  const patch = (index: number, val: Partial<ActivityDraft>) =>
    onUpdate(activities.map((a, i) => (i === index ? { ...a, ...val } : a)));

  const addItem = () =>
    onUpdate([
      ...activities,
      {
        id: newItemId(),
        name: "",
        category: "activity",
        impactLevel: null,
        role: null,
        startDate: null,
        endDate: null,
        isOngoing: false,
        durationMonths: null,
        year: null,
        description: null,
      },
    ]);

  const removeItem = (index: number) => onUpdate(activities.filter((_, i) => i !== index));

  const handleNextClick = () => {
    if (activities.some((a) => Object.keys(validateItem(a, now)).length > 0)) {
      setTriedNext(true);
      // Chờ lỗi hiện ra rồi cuộn tới lỗi đầu tiên
      requestAnimationFrame(() =>
        document.querySelector("#activities [role=alert]")?.scrollIntoView({ block: "center", behavior: "smooth" }),
      );
      return;
    }
    onNext();
  };

  const renderItem = (item: ActivityDraft, idx: number) => {
    const errors = triedNext ? validateItem(item, now) : {};
    const isActivity = item.category === "activity";
    const label = `${isActivity ? "Hoạt động" : "Giải thưởng"} ${idx + 1}`;
    const key = item.id ?? String(idx);
    const start = parseMonthYear(item.startDate);
    const end = parseMonthYear(item.endDate);
    const tier = item.impactLevel ? IMPACT_TIER_DETAILS[item.impactLevel] : null;

    const setDates = (startDate: string | null, endDate: string | null, isOngoing: boolean) =>
      patch(idx, {
        startDate,
        endDate: isOngoing ? null : endDate,
        isOngoing,
        durationMonths: startDate ? countMonths(startDate, isOngoing ? null : endDate, isOngoing) : null,
      });

    const changeCategory = (category: "activity" | "achievement") => {
      if (category === item.category) return;
      patch(
        idx,
        category === "achievement"
          ? { category, role: null, startDate: null, endDate: null, isOngoing: false, durationMonths: null, year: currentYear }
          : { category, year: null },
      );
    };

    return (
      <div key={key} className="@container">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-line bg-(--surface-2) px-4 py-2.5">
          <h3 className="min-w-0 flex-1 text-sm font-semibold text-ink">{label}</h3>
          <SegmentedControl
            options={CATEGORY_OPTIONS}
            value={item.category}
            onChange={changeCategory}
            ariaLabel={`${label}: hoạt động hay giải thưởng`}
          />
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={() => removeItem(idx)}
            aria-label={`Xóa ${label.toLowerCase()}`}
            className="text-ink-2"
          >
            <Trash2Icon className="size-4" />
          </Button>
        </div>

        <div className="divide-y divide-dashed divide-line px-4">
          <FieldRow
            label={isActivity ? "Tên hoạt động, câu lạc bộ hoặc dự án" : "Tên giải thưởng"}
            htmlFor={`${key}-name`}
            required
            error={errors.name}
          >
            <Input
              id={`${key}-name`}
              placeholder={isActivity ? "VD: CLB Robotics, Mùa Hè Xanh, Model UN" : "VD: Giải Nhì HSG Tin học cấp Tỉnh"}
              value={item.name}
              maxLength={200}
              aria-invalid={Boolean(errors.name) || undefined}
              onChange={(e) => patch(idx, { name: e.target.value })}
              onBlur={(e) => e.target.value !== e.target.value.trim() && patch(idx, { name: e.target.value.trim() })}
            />
          </FieldRow>

          {isActivity ? (
            <>
              <FieldRow label="Vai trò của bạn" required error={errors.role}>
                <ChoicePicker
                  id={`${key}-role`}
                  ariaLabel={`${label}: vai trò`}
                  options={ACTIVITY_ROLE_OPTIONS}
                  value={item.role}
                  onChange={(v) => patch(idx, { role: v as ActivityRole | null })}
                />
              </FieldRow>

              <FieldRow label="Từ (tháng/năm)" htmlFor={`${key}-start`} required error={errors.start}>
                <MonthPicker
                  id={`${key}-start`}
                  placeholder="Chọn tháng bắt đầu"
                  value={start}
                  max={thisMonth}
                  onChange={(m) => {
                    const startDate = formatMonthYear(m);
                    const keepEnd = end && monthIndex(end) >= monthIndex(m) ? item.endDate ?? null : null;
                    setDates(startDate, keepEnd, Boolean(item.isOngoing));
                  }}
                />
              </FieldRow>

              <FieldRow label="Đến (tháng/năm)" htmlFor={`${key}-end`} required={!item.isOngoing} error={errors.end}>
                <MonthPicker
                  id={`${key}-end`}
                  placeholder="Chọn tháng kết thúc"
                  value={end}
                  min={start}
                  max={thisMonth}
                  disabled={Boolean(item.isOngoing)}
                  disabledText="Đến nay (Hiện tại)"
                  onChange={(m) => setDates(item.startDate ?? null, formatMonthYear(m), false)}
                />
                <label className="flex w-fit cursor-pointer items-center gap-2 text-sm text-ink">
                  <input
                    type="checkbox"
                    checked={Boolean(item.isOngoing)}
                    onChange={(e) => setDates(item.startDate ?? null, item.endDate ?? null, e.target.checked)}
                    className="size-4 cursor-pointer rounded border-line accent-brand"
                  />
                  Đến nay
                </label>
              </FieldRow>
            </>
          ) : (
            <FieldRow label="Năm đạt giải" htmlFor={`${key}-year`} required error={errors.year}>
              <Input
                id={`${key}-year`}
                type="number"
                min={2000}
                max={currentYear}
                value={item.year ?? ""}
                aria-invalid={Boolean(errors.year) || undefined}
                onChange={(e) => patch(idx, { year: e.target.value === "" ? null : Number(e.target.value) })}
                className="sm:max-w-40"
              />
            </FieldRow>
          )}

          <FieldRow
            label="Phạm vi ảnh hưởng"
            required
            hint={tier ? undefined : "Chọn cấp lớn nhất mục này đạt tới. Phân vân giữa hai cấp thì chọn cấp cao hơn."}
            error={errors.impact}
          >
            <ChoicePicker
              id={`${key}-impact`}
              ariaLabel={`${label}: phạm vi ảnh hưởng`}
              options={IMPACT_LEVEL_OPTIONS}
              value={item.impactLevel}
              onChange={(v) => patch(idx, { impactLevel: v as ImpactLevel | null })}
            />
            {tier && (
              <div className="space-y-1.5 text-xs text-ink-2">
                <p>{tier.summary}</p>
                <ul className="list-inside list-disc space-y-0.5">
                  {(isActivity ? tier.activityCriteria : tier.awardCriteria).map((criterion, i) => (
                    <li key={i}>{criterion}</li>
                  ))}
                </ul>
              </div>
            )}
          </FieldRow>

          <FieldRow
            label="Mô tả thêm"
            htmlFor={`${key}-desc`}
            hint={`${(item.description ?? "").length}/500 ký tự. Nêu vai trò, kết quả cụ thể và con số nếu có.`}
            error={errors.desc}
          >
            <Textarea
              id={`${key}-desc`}
              rows={3}
              maxLength={500}
              placeholder={
                isActivity
                  ? "VD: Dẫn dắt 20 thành viên, tổ chức 2 sự kiện từ thiện, quyên góp 15 triệu đồng."
                  : "VD: Giải Nhì môn Tin học kỳ thi HSG cấp Tỉnh năm 2025, điểm 18.5/20."
              }
              value={item.description ?? ""}
              onChange={(e) => patch(idx, { description: e.target.value || null })}
              onBlur={(e) => e.target.value !== e.target.value.trim() && patch(idx, { description: e.target.value.trim() || null })}
              className="resize-none"
            />
          </FieldRow>
        </div>
      </div>
    );
  };

  return (
    <>
      <StepHeader title="Bạn đã làm gì ngoài giờ học?" />
      <FormBody>
        <FormGroup
          title="Hoạt động & giải thưởng"
          description="Câu lạc bộ, dự án, tình nguyện, giải thưởng: giúp hồ sơ của bạn rõ nét hơn. Không bắt buộc, có thể bỏ qua."
        >
          <div id="activities" className="scroll-mt-6 py-6">
            {activities.length === 0 && <p className="mb-3 text-sm text-ink-2">Bạn chưa thêm mục nào. Bước này có thể bỏ qua.</p>}
            <ActionList>
              {activities.map(renderItem)}
              {activities.length < MAX_ITEMS ? (
                <ActionRow icon={<PlusIcon />} onClick={addItem}>
                  Thêm hoạt động
                </ActionRow>
              ) : (
                <p className="px-4 py-3.5 text-sm text-ink-2">Đã đạt tối đa {MAX_ITEMS} mục.</p>
              )}
            </ActionList>
          </div>
        </FormGroup>
      </FormBody>

      <StepNav onPrev={onPrev}>
        <Button onClick={handleNextClick} size="lg" className="gap-1.5">
          Tiếp tục
          <ArrowRightIcon className="size-4" />
        </Button>
      </StepNav>
    </>
  );
}
