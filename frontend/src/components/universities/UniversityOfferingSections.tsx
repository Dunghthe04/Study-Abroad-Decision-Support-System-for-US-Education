"use client";

import { useSyncExternalStore } from "react";
import Image from "next/image";
import {
  BuildingIcon,
  FileTextIcon,
  GraduationCapIcon,
  HandCoinsIcon,
  HeartHandshakeIcon,
  HomeIcon,
  UsersIcon,
  WalletIcon,
} from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import type { UniversityOffering, UniversitySource } from "@/types/university";
import {
  activityLabel,
  annualPublishedCost,
  deadlineRemainingText,
  gpaScaleTypeLabel,
  housingTypeLabel,
  MISSING_UNIVERSITY_DATA,
  schoolAidPolicy,
  schoolImportanceClass,
  schoolImportanceLabel,
  scoreRange,
  universityDeadlines,
  universityMoney,
  universityNumber,
} from "@/lib/university-display";
import {
  DetailRows,
  DetailSection,
  GaugeDataItem,
  GridDataItem,
  RangeTrack,
} from "./UniversityDetailParts";
import { findFieldSource, SourceTooltip } from "./UniversitySourceTooltip";

export function UniversityCosts({ offering, sources }: { offering: UniversityOffering; sources: UniversitySource[] }) {
  const costSourceByKey: Record<string, UniversitySource | undefined> = {
    tuition: findFieldSource(sources, "tuition_usd"),
    living: findFieldSource(sources, "living_usd"),
    fees: findFieldSource(sources, "fees_usd"),
    other: findFieldSource(sources, "other_expenses_usd"),
  };
  const total = annualPublishedCost(offering);
  const tuition = offering.tuitionUsd;
  const living = offering.livingUsd;
  const fees = offering.feesUsd;
  const other = offering.otherExpensesUsd;

  const costBreakdown = [
    {
      key: "tuition",
      label: "Học phí",
      amount: tuition,
      colorClass: "bg-brand",
      indicatorClass: "bg-brand",
      note: null,
      subNote: null,
    },
    {
      key: "living",
      label: "Sinh hoạt phí (ăn & ở)",
      amount: living,
      colorClass: "bg-brand/65",
      indicatorClass: "bg-brand/65",
      note: offering.housingOnlyUsd != null
        ? `Trong đó tiền phòng ở KTX: ${universityMoney(offering.housingOnlyUsd)}/năm`
        : null,
      subNote: offering.housingDepositUsd != null
        ? `Tiền cọc phòng ở: ${universityMoney(offering.housingDepositUsd)}`
        : null,
    },
    {
      key: "fees",
      label: "Phí bắt buộc",
      amount: fees,
      colorClass: "bg-brand/35",
      indicatorClass: "bg-brand/35",
      note: null,
      subNote: null,
    },
    {
      key: "other",
      label: "Chi phí khác (sách vở, đi lại)",
      amount: other,
      colorClass: "bg-ink-3/35",
      indicatorClass: "bg-ink-3/35",
      note: null,
      subNote: null,
    },
  ].filter((item): item is typeof item & { amount: number } => item.amount != null && item.amount > 0);

  const breakdownSum = costBreakdown.reduce((sum, item) => sum + item.amount, 0);
  const displayTotal = total ?? (breakdownSum > 0 ? breakdownSum : null);

  const hasAid = Boolean(
    offering.intlAidPolicy ||
    offering.intlAidAvgUsd != null ||
    offering.intlAidRecipients != null
  );

  return (
    <DetailSection
      id="costs"
      title="Chi phí & hỗ trợ tài chính"
      description={
        offering.academicYear
          ? `Số liệu chi phí trong hệ thống · năm học ${offering.academicYear} · USD/năm`
          : "Số liệu chi phí trong hệ thống · USD/năm"
      }
    >
      <div className="space-y-6">
        {displayTotal !== null ? (
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="flex flex-col sm:flex-row sm:items-baseline sm:justify-between gap-2 border-b border-line pb-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wider text-ink-3">
                  Tổng chi phí công bố ước tính (COA)
                </p>
                <div className="mt-1.5 flex items-baseline gap-2">
                  <span className="text-3xl sm:text-4xl font-extrabold tracking-tight text-brand">
                    <SourceTooltip source={findFieldSource(sources, "coa_total_usd", "tuition_usd")}>
                      {universityMoney(displayTotal)}
                    </SourceTooltip>
                  </span>
                  <span className="text-sm font-medium text-ink-2">/ năm học</span>
                </div>
              </div>
              {offering.academicYear && (
                <span className="text-xs text-ink-3">Áp dụng năm học {offering.academicYear}</span>
              )}
            </div>

            {breakdownSum > 0 && (
              <div className="mt-5 space-y-3">
                <div
                  className="flex h-3 w-full overflow-hidden rounded-full bg-line"
                  role="img"
                  aria-label={`Biểu đồ tỷ trọng chi phí: ${costBreakdown.map((i) => `${i.label} ${universityMoney(i.amount)}`).join(", ")}`}
                >
                  {costBreakdown.map((item) => {
                    const pct = (item.amount / breakdownSum) * 100;
                    return (
                      <div
                        key={item.key}
                        className={cn("h-full transition-all", item.colorClass)}
                        style={{ width: `${pct}%` }}
                        title={`${item.label}: ${universityMoney(item.amount)} (${pct.toFixed(1)}%)`}
                      />
                    );
                  })}
                </div>

                <div className="divide-y divide-line/70 pt-2">
                  {costBreakdown.map((item) => {
                    const pct = (item.amount / breakdownSum) * 100;
                    return (
                      <div
                        key={item.key}
                        className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-1 last:pb-0"
                      >
                        <div className="flex items-start gap-2.5 min-w-0">
                          <span
                            className={cn("mt-1.5 size-2.5 shrink-0 rounded-full", item.indicatorClass)}
                            aria-hidden="true"
                          />
                          <div className="min-w-0">
                            <p className="text-sm font-medium text-ink">{item.label}</p>
                            {item.note && <p className="text-xs text-ink-2 mt-0.5">{item.note}</p>}
                            {item.subNote && <p className="text-xs text-ink-3 mt-0.5">{item.subNote}</p>}
                          </div>
                        </div>
                        <div className="text-right shrink-0">
                          <span className="text-sm font-bold text-ink">
                            <SourceTooltip source={costSourceByKey[item.key]}>{universityMoney(item.amount)}</SourceTooltip>
                          </span>
                          <span className="ml-2 text-xs text-ink-3">({pct.toFixed(0)}%)</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        ) : (
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
              <GridDataItem icon={GraduationCapIcon} label="Học phí" value={<SourceTooltip source={costSourceByKey.tuition}>{universityMoney(offering.tuitionUsd)}</SourceTooltip>} />
              <GridDataItem icon={HomeIcon} label="Sinh hoạt phí" value={<SourceTooltip source={costSourceByKey.living}>{universityMoney(offering.livingUsd)}</SourceTooltip>} />
              <GridDataItem icon={FileTextIcon} label="Phí bắt buộc" value={<SourceTooltip source={costSourceByKey.fees}>{universityMoney(offering.feesUsd)}</SourceTooltip>} />
              <GridDataItem icon={WalletIcon} label="Chi phí khác" value={<SourceTooltip source={costSourceByKey.other}>{universityMoney(offering.otherExpensesUsd)}</SourceTooltip>} />
            </div>
          </div>
        )}

        {hasAid && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Hỗ trợ cho sinh viên quốc tế</h3>
            <div className="overflow-hidden rounded-xl border border-line bg-surface">
              <div className="p-5 sm:p-6">
                <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
                  {offering.intlAidPolicy && (
                    <GridDataItem
                      icon={HeartHandshakeIcon}
                      label="Chính sách hỗ trợ"
                      value={<SourceTooltip source={findFieldSource(sources, "intl_aid_policy")}>{schoolAidPolicy(offering.intlAidPolicy)}</SourceTooltip>}
                    />
                  )}
                  {offering.intlAidAvgUsd != null && (
                    <GridDataItem
                      icon={HandCoinsIcon}
                      label="Mức hỗ trợ trung bình"
                      value={<SourceTooltip source={findFieldSource(sources, "intl_aid_avg_usd")}>{universityMoney(offering.intlAidAvgUsd)}</SourceTooltip>}
                    />
                  )}
                  {offering.intlAidRecipients != null && (
                    <GridDataItem
                      icon={UsersIcon}
                      label="Số sinh viên nhận hỗ trợ"
                      value={<SourceTooltip source={findFieldSource(sources, "intl_aid_recipients")}>{universityNumber(offering.intlAidRecipients)}</SourceTooltip>}
                    />
                  )}
                </div>
              </div>
              {displayTotal != null && offering.intlAidAvgUsd != null && (
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-brand px-5 py-4 sm:px-6 text-white">
                  <div>
                    <p className="text-base font-semibold leading-tight text-white">
                      Chi phí ước tính sau hỗ trợ
                    </p>
                    <p className="mt-1 text-sm text-white/75">Chưa gồm học bổng bên ngoài</p>
                  </div>
                  <div className="flex items-baseline gap-1.5 shrink-0">
                    <span className="text-2xl sm:text-3xl font-semibold tracking-tight tabular-nums text-white">
                      {universityMoney(Math.max(0, displayTotal - offering.intlAidAvgUsd))}
                    </span>
                    <span className="text-sm font-normal text-white/80">/ năm</span>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </DetailSection>
  );
}

export function UniversityAdmissions({ offering, sources }: { offering: UniversityOffering; sources: UniversitySource[] }) {
  const clientMounted = useSyncExternalStore(
    () => () => {},
    () => true,
    () => false
  );

  const deadlines = universityDeadlines(offering.deadlinesJson);

  const hasSat = offering.sat25 != null || offering.sat75 != null;
  const hasAct = offering.act25 != null || offering.act75 != null;
  const hasGpa4 = offering.avgGpa4 != null || offering.minGpa4 != null;
  const hasGpaWeighted = offering.avgGpaWeighted != null;
  const hasAnyScore = hasSat || hasAct || hasGpa4 || hasGpaWeighted;

  const isSatRequired = offering.satPolicy === "required";

  const englishRequirements = [
    {
      id: "ielts",
      sourceField: "min_ielts",
      label: "IELTS tối thiểu",
      name: "IELTS",
      value: offering.minIelts,
      min: 0,
      max: 9,
      scaleLabel: "Thang 0–9",
      logo: "/certs/ielts.png",
      logoWidth: 440,
      logoHeight: 142,
    },
    {
      id: "toefl",
      sourceField: "min_toefl",
      label: "TOEFL iBT tối thiểu",
      name: "TOEFL iBT",
      value: offering.minToefl,
      min: 0,
      max: 120,
      scaleLabel: "Thang 0–120",
      logo: "/certs/toefl.png",
      logoWidth: 738,
      logoHeight: 144,
    },
    {
      id: "duolingo",
      sourceField: "min_duolingo",
      label: "Duolingo tối thiểu",
      name: "Duolingo",
      value: offering.minDuolingo,
      min: 10,
      max: 160,
      scaleLabel: "Thang 10–160",
      logo: "/certs/duolingo.png",
      logoWidth: 160,
      logoHeight: 160,
    },
  ].filter((item): item is typeof item & { value: number } => item.value != null);

  const importanceFactors = [
    { label: "Điểm trung bình (GPA)", value: offering.c7GpaImportance },
    { label: "Điểm thi chuẩn hóa (SAT/ACT)", value: offering.c7TestImportance },
    { label: "Hoạt động ngoại khóa", value: offering.c7EcImportance },
  ].filter((item) => item.value != null);

  return (
    <DetailSection
      id="admissions"
      title="Tuyển sinh"
    >
      <div className="space-y-6">
        {hasAnyScore && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Khoảng điểm & học lực trúng tuyển</h3>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {hasSat && (
                <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-semibold text-ink-2 truncate">SAT (khoảng 25% – 75%)</span>
                    {isSatRequired && (
                      <Badge className="border-transparent bg-risk-wash text-risk text-[11px] font-medium py-0.5 px-2 shrink-0">
                        Bắt buộc
                      </Badge>
                    )}
                  </div>
                  <div className="flex items-baseline justify-between">
                    <span className="text-lg font-bold text-ink">
                      <SourceTooltip source={findFieldSource(sources, "sat25", "sat75")}>
                        {scoreRange(offering.sat25, offering.sat75)}
                      </SourceTooltip>
                    </span>
                  </div>
                  <RangeTrack
                    min={400}
                    max={1600}
                    low={offering.sat25}
                    high={offering.sat75}
                    ariaLabel={`SAT khoảng 25%–75%: ${scoreRange(offering.sat25, offering.sat75)}`}
                  />
                </div>
              )}

              {hasAct && (
                <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-semibold text-ink-2 truncate">ACT (khoảng 25% – 75%)</span>
                    {isSatRequired && (
                      <Badge className="border-transparent bg-risk-wash text-risk text-[11px] font-medium py-0.5 px-2 shrink-0">
                        Bắt buộc
                      </Badge>
                    )}
                  </div>
                  <div className="flex items-baseline justify-between">
                    <span className="text-lg font-bold text-ink">
                      <SourceTooltip source={findFieldSource(sources, "act25", "act75")}>
                        {scoreRange(offering.act25, offering.act75)}
                      </SourceTooltip>
                    </span>
                  </div>
                  <RangeTrack
                    min={1}
                    max={36}
                    low={offering.act25}
                    high={offering.act75}
                    ariaLabel={`ACT khoảng 25%–75%: ${scoreRange(offering.act25, offering.act75)}`}
                  />
                </div>
              )}

              {hasGpa4 && (
                <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-ink-2">GPA chuẩn (thang 4.0)</span>
                  </div>
                  <div className="flex items-baseline justify-between">
                    <div className="flex flex-wrap items-baseline gap-2">
                      {offering.avgGpa4 != null && (
                        <span className="text-lg font-bold text-ink">
                          <SourceTooltip source={findFieldSource(sources, "avg_gpa4")}>
                            TB: {universityNumber(offering.avgGpa4)}
                          </SourceTooltip>
                        </span>
                      )}
                      {offering.minGpa4 != null && (
                        <span className="text-xs text-ink-2">
                          (Tối thiểu: {universityNumber(offering.minGpa4)})
                        </span>
                      )}
                    </div>
                  </div>
                  <RangeTrack
                    min={0}
                    max={4}
                    low={offering.minGpa4}
                    high={offering.avgGpa4}
                    point={offering.avgGpa4 ?? offering.minGpa4}
                    ariaLabel={`GPA thang 4.0: ${offering.avgGpa4 ? `Trung bình ${offering.avgGpa4}` : `Tối thiểu ${offering.minGpa4}`}`}
                  />
                </div>
              )}

              {hasGpaWeighted && (
                <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-ink-2">GPA có trọng số (Weighted)</span>
                    <span className="text-xs text-ink-3">{gpaScaleTypeLabel(offering.gpaScaleType)}</span>
                  </div>
                  <div className="flex items-baseline justify-between">
                    <span className="text-lg font-bold text-ink">
                      <SourceTooltip source={findFieldSource(sources, "avg_gpa_weighted")}>
                        {universityNumber(offering.avgGpaWeighted)}
                      </SourceTooltip>
                    </span>
                    <span className="text-xs text-ink-3">Thang điểm có tính điểm thưởng AP/IB</span>
                  </div>
                  <RangeTrack
                    min={0}
                    max={5}
                    point={offering.avgGpaWeighted}
                    ariaLabel={`GPA có trọng số: ${offering.avgGpaWeighted}`}
                  />
                </div>
              )}
            </div>
          </div>
        )}

        {englishRequirements.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Yêu cầu chứng chỉ tiếng Anh tối thiểu</h3>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
              {englishRequirements.map((item) => (
                <div
                  key={item.id}
                  className="flex flex-col justify-between rounded-xl border border-line bg-surface p-4"
                >
                  <div className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-xs text-ink-3">{item.label}</p>
                      <p className="mt-1 text-2xl font-bold tracking-tight text-ink">
                        <SourceTooltip source={findFieldSource(sources, item.sourceField)}>
                          {universityNumber(item.value)}
                        </SourceTooltip>
                      </p>
                    </div>
                    <div className="flex h-10 w-28 shrink-0 items-center justify-end">
                      <Image
                        src={item.logo}
                        alt={item.name}
                        width={item.logoWidth}
                        height={item.logoHeight}
                        className="h-8 sm:h-9 w-auto max-w-full object-contain"
                      />
                    </div>
                  </div>

                  <div className="mt-3.5">
                    <RangeTrack
                      min={item.min}
                      max={item.max}
                      point={item.value}
                      fillToMax
                      ariaLabel={`${item.name} yêu cầu tối thiểu ${universityNumber(item.value)}`}
                    />
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {importanceFactors.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Mức độ coi trọng các yếu tố tuyển sinh (CDS C7)</h3>
            <div className="divide-y divide-line/70 rounded-xl border border-line bg-surface px-5 sm:px-6">
              {importanceFactors.map((item) => (
                <div key={item.label} className="flex items-center justify-between py-3">
                  <span className="text-sm text-ink">{item.label}</span>
                  <span className={cn("text-sm", schoolImportanceClass(item.value))}>
                    {schoolImportanceLabel(item.value)}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="space-y-3">
          <h3 className="text-sm font-semibold text-ink">Các đợt nộp hồ sơ</h3>
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            {deadlines.length ? (
              <div className="grid gap-x-8 gap-y-5 sm:grid-cols-2 md:grid-cols-3">
                {deadlines.map((deadline) => {
                  const remaining = clientMounted ? deadlineRemainingText(deadline.rawDate) : null;
                  const isPassed = remaining?.isPassed ?? false;
                  return (
                    <div key={deadline.round} className="space-y-1">
                      <p className={cn("text-xs", isPassed ? "text-ink-3" : "text-ink-2")}>
                        {deadline.label} ({deadline.round})
                      </p>
                      <p className={cn("text-xl font-bold tracking-tight", isPassed ? "text-ink-3 line-through decoration-ink-3/40" : "text-ink")}>
                        {deadline.date}
                      </p>
                      {remaining && (
                        <p className={cn("text-xs font-medium", isPassed ? "text-ink-3" : "text-brand")}>
                          {remaining.label}
                        </p>
                      )}
                    </div>
                  );
                })}
              </div>
            ) : (
              <p className="text-sm text-ink-3">{MISSING_UNIVERSITY_DATA}</p>
            )}
          </div>
        </div>
      </div>
    </DetailSection>
  );
}

export function UniversityCampusLife({ offering, sources }: { offering: UniversityOffering; sources: UniversitySource[] }) {
  const hasOnCampus = offering.fyOnCampusPct != null || offering.ugOnCampusPct != null;
  const hasHousingCost = offering.housingOnlyUsd != null || offering.housingDepositUsd != null;
  const housingList = offering.housingTypes
    ? offering.housingTypes.split("|").map((s) => s.trim()).filter(Boolean)
    : [];
  const activitiesList = offering.activities
    ? offering.activities.split("|").map((s) => s.trim()).filter(Boolean)
    : [];

  const hasAnyData =
    hasOnCampus ||
    hasHousingCost ||
    housingList.length > 0 ||
    offering.intlHousing != null ||
    activitiesList.length > 0;

  if (!hasAnyData) return null;

  return (
    <DetailSection id="campus" title="Khuôn viên & nhà ở sinh viên">
      <div className="space-y-6">
        {offering.intlHousing != null && (
          <Alert variant={offering.intlHousing ? "success" : "info"}>
            <BuildingIcon />
            <AlertTitle>Khu nhà ở cho sinh viên quốc tế</AlertTitle>
            <AlertDescription>
              {offering.intlHousing
                ? "Trường có ký túc xá hoặc khu nhà ở được bố trí riêng/ưu tiên cho sinh viên quốc tế."
                : "Trường không có khu ký túc xá riêng cho sinh viên quốc tế (sinh viên quốc tế ở chung khu KTX thông thường)."}
            </AlertDescription>
          </Alert>
        )}

        {hasOnCampus && (
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
              {offering.fyOnCampusPct != null && (
                <GaugeDataItem
                  label="SV năm nhất ở KTX"
                  value={offering.fyOnCampusPct}
                  wrapValue={(value) => <SourceTooltip source={findFieldSource(sources, "fy_on_campus_pct")}>{value}</SourceTooltip>}
                  subtitle="Sinh viên năm 1 nội trú"
                />
              )}
              {offering.ugOnCampusPct != null && (
                <GaugeDataItem
                  label="SV đại học ở KTX"
                  value={offering.ugOnCampusPct}
                  wrapValue={(value) => <SourceTooltip source={findFieldSource(sources, "ug_on_campus_pct")}>{value}</SourceTooltip>}
                  subtitle="Toàn bộ sinh viên đại học"
                />
              )}
            </div>
          </div>
        )}

        {hasHousingCost && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Chi phí nhà ở</h3>
            <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2">
                {offering.housingOnlyUsd != null && (
                  <GridDataItem
                    icon={HomeIcon}
                    label="Tiền phòng ở KTX"
                    value={<SourceTooltip source={findFieldSource(sources, "housing_only_usd")}>{`${universityMoney(offering.housingOnlyUsd)} / năm`}</SourceTooltip>}
                  />
                )}
                {offering.housingDepositUsd != null && (
                  <GridDataItem
                    icon={WalletIcon}
                    label="Tiền đặt cọc phòng"
                    value={<SourceTooltip source={findFieldSource(sources, "housing_deposit_usd")}>{universityMoney(offering.housingDepositUsd)}</SourceTooltip>}
                  />
                )}
              </div>
            </div>
          </div>
        )}

        {housingList.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Các loại hình nhà ở có sẵn ({housingList.length})</h3>
            <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <ul className="grid grid-cols-[repeat(auto-fill,minmax(15rem,1fr))] gap-x-8 gap-y-3">
                {housingList.map((item) => (
                  <li key={item} className="text-sm text-ink">
                    {housingTypeLabel(item)}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}

        {activitiesList.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Câu lạc bộ & hoạt động sinh viên ({activitiesList.length})</h3>
            <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <ul className="grid grid-cols-[repeat(auto-fill,minmax(15rem,1fr))] gap-x-8 gap-y-3">
                {activitiesList.map((item) => (
                  <li key={item} className="text-sm text-ink">
                    {activityLabel(item)}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        )}
      </div>
    </DetailSection>
  );
}

export function UniversityStatistics({
  offering,
  internationalStudentCount,
  sources,
}: {
  offering: UniversityOffering;
  internationalStudentCount: number | null;
  sources: UniversitySource[];
}) {
  const percentageStats = [
    {
      key: "retention",
      sourceField: "retention_rate_1yr",
      label: "Tỷ lệ tiếp tục học năm hai",
      value: offering.retentionRate1Yr,
      subtitle: "SV năm 1 quay lại học tiếp",
    },
    {
      key: "graduation",
      sourceField: "grad_rate_6yr",
      label: "Tỷ lệ tốt nghiệp trong 6 năm",
      value: offering.gradRate6Yr,
      subtitle: "Chuẩn CDS B / College Scorecard",
    },
    {
      key: "intlAcceptance",
      sourceField: "intl_acceptance_rate",
      label: "Tỷ lệ nhận hồ sơ quốc tế",
      value: offering.intlAcceptanceRate,
      subtitle: "Chấp thuận sinh viên quốc tế",
    },
    {
      key: "satSubmit",
      sourceField: "sat_submit_pct",
      label: "Tỷ lệ nộp điểm SAT",
      value: offering.satSubmitPct,
      subtitle: "Tân sinh viên nộp SAT",
    },
    {
      key: "gpaSubmit",
      sourceField: "gpa_submit_pct",
      label: "Tỷ lệ nộp GPA cấp 3",
      value: offering.gpaSubmitPct,
      subtitle: "Tân sinh viên có điểm GPA",
    },
  ].filter((item): item is typeof item & { value: number } => item.value != null);

  const countRows: { label: string; value: string; sourceField?: string }[] = [
    { label: "Tổng hồ sơ nộp", value: universityNumber(offering.applicantsTotal), sourceField: "applicants_total" },
    { label: "Tổng hồ sơ được nhận", value: universityNumber(offering.admittedTotal), sourceField: "admitted_total" },
    { label: "Hồ sơ quốc tế nộp", value: universityNumber(offering.intlApplicants), sourceField: "intl_applicants" },
    { label: "Hồ sơ quốc tế được nhận", value: universityNumber(offering.intlAdmitted), sourceField: "intl_admitted" },
    { label: "Số sinh viên quốc tế đang theo học", value: universityNumber(internationalStudentCount), sourceField: "international_student_count" },
    { label: "Tỷ lệ SV / giảng viên", value: offering.studentFacultyRatio != null ? `${offering.studentFacultyRatio} : 1` : MISSING_UNIVERSITY_DATA },
  ].filter((r) => r.value !== MISSING_UNIVERSITY_DATA);

  return (
    <DetailSection
      id="statistics"
      title="Thống kê sinh viên & tuyển sinh"
    >
      <div className="space-y-6">
        {percentageStats.length > 0 && (
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
              {percentageStats.map((item) => (
                <GaugeDataItem
                  key={item.key}
                  label={item.label}
                  value={item.value}
                  subtitle={item.subtitle}
                  wrapValue={(value) => <SourceTooltip source={findFieldSource(sources, item.sourceField)}>{value}</SourceTooltip>}
                />
              ))}
            </div>
          </div>
        )}

        {countRows.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Số lượng hồ sơ & tuyển sinh</h3>
            <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
              <DetailRows rows={countRows.map((row) => ({ label: row.label, value: row.sourceField ? <SourceTooltip source={findFieldSource(sources, row.sourceField)}>{row.value}</SourceTooltip> : row.value }))} />
            </div>
          </div>
        )}
      </div>
    </DetailSection>
  );
}

export function UniversityMajors({ offering, sources }: { offering: UniversityOffering; sources: UniversitySource[] }) {
  const majorsFieldSource = findFieldSource(sources, "majors");
  const majorsSource: UniversitySource | undefined = majorsFieldSource ?? (offering.sourceUrl
    ? {
        fieldName: "majors",
        fieldValue: null,
        sourceUrl: offering.sourceUrl,
        sourceSection: null,
        status: "unverified",
        retrievedAt: offering.retrievedAt,
        dataYear: offering.academicYear,
        appliedToSnapshot: true,
        normalization: null,
      }
    : undefined);
  return (
    <DetailSection
      id="majors"
      title="Ngành học"
      description={`Nhóm ngành có trong dữ liệu hiện tại của bậc học đang chọn (${offering.majors.length} nhóm ngành).`}
    >
      <div className="space-y-4">
        <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
          {offering.majors.length ? (
            <div className="grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
              {offering.majors.map((major) => (
                <div
                  key={`${major.cipCode}-${major.name}`}
                  className="flex items-center justify-between gap-2 py-1.5"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-ink">{major.name}</p>
                    {major.cipCode && <p className="text-xs text-ink-3">CIP {major.cipCode}</p>}
                  </div>
                  {major.isStem && <Badge variant="secondary" className="shrink-0 text-xs">STEM</Badge>}
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-ink-3">{MISSING_UNIVERSITY_DATA}</p>
          )}
        </div>
        {majorsSource && (
          <p className="text-xs text-ink-3">
            <SourceTooltip source={majorsSource}>Nguồn dữ liệu ngành học</SourceTooltip>
          </p>
        )}
      </div>
    </DetailSection>
  );
}
