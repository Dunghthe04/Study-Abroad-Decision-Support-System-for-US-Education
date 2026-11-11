"use client";

import {
  FileTextIcon,
  GraduationCapIcon,
  HandCoinsIcon,
  HeartHandshakeIcon,
  HomeIcon,
  InfoIcon,
  UsersIcon,
  WalletIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";
import type { UniversityOffering } from "@/types/university";
import {
  activityLabel,
  annualPublishedCost,
  gpaScaleTypeLabel,
  housingTypeLabel,
  MISSING_UNIVERSITY_DATA,
  schoolAidPolicy,
  schoolImportanceClass,
  schoolImportanceLabel,
  schoolSatPolicy,
  scoreRange,
  universityDate,
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
  SourceLink,
} from "./UniversityDetailParts";

export function UniversityCosts({ offering }: { offering: UniversityOffering }) {
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
      {displayTotal !== null ? (
        <div className="space-y-6">
          <div className="rounded-xl border border-line bg-surface p-5 sm:p-6">
            <div className="flex flex-col sm:flex-row sm:items-baseline sm:justify-between gap-2 border-b border-line pb-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wider text-ink-3">
                  Tổng chi phí công bố ước tính (COA)
                </p>
                <div className="mt-1.5 flex items-baseline gap-2">
                  <span className="text-3xl sm:text-4xl font-extrabold tracking-tight text-brand">
                    {universityMoney(displayTotal)}
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
                          <span className="text-sm font-bold text-ink">{universityMoney(item.amount)}</span>
                          <span className="ml-2 text-xs text-ink-3">({pct.toFixed(0)}%)</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
          <GridDataItem icon={GraduationCapIcon} label="Học phí" value={universityMoney(offering.tuitionUsd)} />
          <GridDataItem icon={HomeIcon} label="Sinh hoạt phí" value={universityMoney(offering.livingUsd)} />
          <GridDataItem icon={FileTextIcon} label="Phí bắt buộc" value={universityMoney(offering.feesUsd)} />
          <GridDataItem icon={WalletIcon} label="Chi phí khác" value={universityMoney(offering.otherExpensesUsd)} />
        </div>
      )}

      {hasAid && (
        <div className="mt-8 border-t border-line pt-6">
          <h3 className="mb-4 text-sm font-semibold text-ink">Hỗ trợ cho sinh viên quốc tế</h3>
          <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
            {offering.intlAidPolicy && (
              <GridDataItem
                icon={HeartHandshakeIcon}
                label="Chính sách hỗ trợ"
                value={schoolAidPolicy(offering.intlAidPolicy)}
              />
            )}
            {offering.intlAidAvgUsd != null && (
              <GridDataItem
                icon={HandCoinsIcon}
                label="Mức hỗ trợ trung bình"
                value={universityMoney(offering.intlAidAvgUsd)}
              />
            )}
            {offering.intlAidRecipients != null && (
              <GridDataItem
                icon={UsersIcon}
                label="Số sinh viên nhận hỗ trợ"
                value={universityNumber(offering.intlAidRecipients)}
              />
            )}
          </div>
          {displayTotal != null && offering.intlAidAvgUsd != null && (
            <div className="mt-4 rounded-lg bg-brand-wash p-3.5 text-sm text-brand-ink">
              <span className="font-semibold">Chi phí ước tính sau hỗ trợ trung bình: </span>
              <span className="font-bold">
                {universityMoney(Math.max(0, displayTotal - offering.intlAidAvgUsd))} / năm
              </span>
              <span className="text-xs text-ink-2 ml-2">(chưa bao gồm các học bổng bên ngoài)</span>
            </div>
          )}
        </div>
      )}
    </DetailSection>
  );
}

export function UniversityAdmissions({ offering }: { offering: UniversityOffering }) {
  const deadlines = universityDeadlines(offering.deadlinesJson);

  const hasSat = offering.sat25 != null || offering.sat75 != null;
  const hasAct = offering.act25 != null || offering.act75 != null;
  const hasGpa4 = offering.avgGpa4 != null || offering.minGpa4 != null;
  const hasGpaWeighted = offering.avgGpaWeighted != null;
  const hasAnyScore = hasSat || hasAct || hasGpa4 || hasGpaWeighted;

  const englishRequirements = [
    { label: "IELTS tối thiểu", value: offering.minIelts },
    { label: "TOEFL iBT tối thiểu", value: offering.minToefl },
    { label: "Duolingo tối thiểu", value: offering.minDuolingo },
  ].filter((item) => item.value != null);

  const importanceFactors = [
    { label: "Điểm trung bình (GPA)", value: offering.c7GpaImportance },
    { label: "Điểm thi chuẩn hóa (SAT/ACT)", value: offering.c7TestImportance },
    { label: "Hoạt động ngoại khóa", value: offering.c7EcImportance },
  ].filter((item) => item.value != null);

  return (
    <DetailSection
      id="admissions"
      title="Tuyển sinh"
      description="Yêu cầu và thống kê đã được lưu trong hệ thống; không phải mức điểm bảo đảm trúng tuyển."
    >
      {offering.satPolicy && (
        <div className="mb-5">
          <Badge variant="secondary">{schoolSatPolicy(offering.satPolicy)}</Badge>
        </div>
      )}

      {hasAnyScore && (
        <div className="space-y-4">
          <h3 className="text-sm font-semibold text-ink">Khoảng điểm & học lực trúng tuyển</h3>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {hasSat && (
              <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-ink-2">SAT (khoảng 25% – 75%)</span>
                  <span className="text-xs text-ink-3">Thang 400 – 1600</span>
                </div>
                <div className="flex items-baseline justify-between">
                  <span className="text-lg font-bold text-ink">
                    {scoreRange(offering.sat25, offering.sat75)}
                  </span>
                </div>
                <RangeTrack
                  min={400}
                  max={1600}
                  low={offering.sat25}
                  high={offering.sat75}
                  ariaLabel={`SAT khoảng 25%–75%: ${scoreRange(offering.sat25, offering.sat75)} trên thang 400–1600`}
                />
              </div>
            )}

            {hasAct && (
              <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-ink-2">ACT (khoảng 25% – 75%)</span>
                  <span className="text-xs text-ink-3">Thang 1 – 36</span>
                </div>
                <div className="flex items-baseline justify-between">
                  <span className="text-lg font-bold text-ink">
                    {scoreRange(offering.act25, offering.act75)}
                  </span>
                </div>
                <RangeTrack
                  min={1}
                  max={36}
                  low={offering.act25}
                  high={offering.act75}
                  ariaLabel={`ACT khoảng 25%–75%: ${scoreRange(offering.act25, offering.act75)} trên thang 1–36`}
                />
              </div>
            )}

            {hasGpa4 && (
              <div className="rounded-xl border border-line bg-surface p-4 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold text-ink-2">GPA chuẩn (thang 4.0)</span>
                  <span className="text-xs text-ink-3">Thang 0 – 4.0</span>
                </div>
                <div className="flex items-baseline justify-between">
                  <div className="flex flex-wrap items-baseline gap-2">
                    {offering.avgGpa4 != null && (
                      <span className="text-lg font-bold text-ink">
                        TB: {universityNumber(offering.avgGpa4)}
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
                    {universityNumber(offering.avgGpaWeighted)}
                  </span>
                  <span className="text-xs text-ink-3">Thang điểm có tính điểm thưởng AP/IB</span>
                </div>
                <RangeTrack
                  min={0}
                  max={5}
                  point={offering.avgGpaWeighted}
                  ariaLabel={`GPA có trọng số: ${offering.avgGpaWeighted} trên thang 5.0`}
                />
              </div>
            )}
          </div>
        </div>
      )}

      {englishRequirements.length > 0 && (
        <div className="mt-7 space-y-3 border-t border-line pt-6">
          <h3 className="text-sm font-semibold text-ink">Yêu cầu chứng chỉ tiếng Anh tối thiểu</h3>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {englishRequirements.map((item) => (
              <div key={item.label} className="rounded-lg border border-line bg-surface p-3.5">
                <p className="text-xs text-ink-3">{item.label}</p>
                <p className="mt-1 text-lg font-bold text-ink">{universityNumber(item.value)}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      {importanceFactors.length > 0 && (
        <div className="mt-7 space-y-3 border-t border-line pt-6">
          <h3 className="text-sm font-semibold text-ink">Mức độ coi trọng các yếu tố tuyển sinh (CDS C7)</h3>
          <div className="divide-y divide-line/70 rounded-xl border border-line bg-surface px-4">
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

      <div className="mt-7 border-t border-line pt-6">
        <h3 className="mb-4 text-sm font-semibold text-ink">Các đợt nộp hồ sơ</h3>
        {deadlines.length ? (
          <div className="grid gap-x-8 gap-y-5 sm:grid-cols-2 md:grid-cols-3">
            {deadlines.map((deadline) => (
              <div key={deadline.round} className="space-y-1">
                <p className="text-xs text-ink-3">
                  {deadline.label} ({deadline.round})
                </p>
                <p className="text-xl font-bold tracking-tight text-ink">{deadline.date}</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-ink-3">{MISSING_UNIVERSITY_DATA}</p>
        )}
      </div>

      <Alert className="mt-6 border-line bg-ground text-ink">
        <InfoIcon className="text-brand shrink-0" />
        <AlertDescription className="text-xs leading-relaxed text-ink-2">
          Chính sách và hạn nộp có thể thay đổi theo từng năm. Hãy luôn xác nhận lại trên website chính thức của trường trước khi nộp hồ sơ.
        </AlertDescription>
      </Alert>
    </DetailSection>
  );
}

export function UniversityCampusLife({ offering }: { offering: UniversityOffering }) {
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
    <DetailSection
      id="campus"
      title="Khuôn viên & nhà ở sinh viên"
      description="Thông tin chỗ ở tại ký túc xá và hoạt động ngoại khóa dành cho sinh viên."
    >
      <div className="space-y-6">
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
          {offering.fyOnCampusPct != null && (
            <GaugeDataItem
              label="SV năm nhất ở KTX"
              value={offering.fyOnCampusPct}
              subtitle="Sinh viên năm 1 nội trú"
            />
          )}
          {offering.ugOnCampusPct != null && (
            <GaugeDataItem
              label="SV đại học ở KTX"
              value={offering.ugOnCampusPct}
              subtitle="Toàn bộ sinh viên đại học"
            />
          )}
          {offering.housingOnlyUsd != null && (
            <GridDataItem
              icon={HomeIcon}
              label="Tiền phòng ở KTX"
              value={`${universityMoney(offering.housingOnlyUsd)} / năm`}
            />
          )}
          {offering.housingDepositUsd != null && (
            <GridDataItem
              icon={WalletIcon}
              label="Tiền đặt cọc phòng"
              value={universityMoney(offering.housingDepositUsd)}
            />
          )}
        </div>

        {offering.intlHousing != null && (
          <div className="rounded-xl border border-line bg-surface p-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            <div>
              <p className="text-sm font-semibold text-ink">Khu nhà ở cho sinh viên quốc tế</p>
              <p className="text-xs text-ink-2 mt-0.5">
                {offering.intlHousing
                  ? "Trường có ký túc xá hoặc khu nhà ở được bố trí riêng/ưu tiên cho sinh viên quốc tế."
                  : "Trường không có khu ký túc xá riêng cho sinh viên quốc tế (sinh viên quốc tế ở chung khu KTX thông thường)."}
              </p>
            </div>
            <Badge
              variant={offering.intlHousing ? "default" : "secondary"}
              className={offering.intlHousing ? "!bg-ok !text-white shrink-0" : "shrink-0"}
            >
              {offering.intlHousing ? "Có hỗ trợ riêng" : "Khu ở chung"}
            </Badge>
          </div>
        )}

        {housingList.length > 0 && (
          <div className="space-y-3">
            <h3 className="text-sm font-semibold text-ink">Các loại hình nhà ở có sẵn ({housingList.length})</h3>
            <div className="flex flex-wrap gap-2">
              {housingList.map((item) => (
                <span
                  key={item}
                  className="inline-flex items-center rounded-md border border-line bg-surface px-2.5 py-1 text-xs font-medium text-ink"
                >
                  {housingTypeLabel(item)}
                </span>
              ))}
            </div>
          </div>
        )}

        {activitiesList.length > 0 && (
          <div className="space-y-3 border-t border-line pt-5">
            <h3 className="text-sm font-semibold text-ink">Câu lạc bộ & hoạt động sinh viên ({activitiesList.length})</h3>
            <div className="flex flex-wrap gap-2">
              {activitiesList.map((item) => (
                <span
                  key={item}
                  className="inline-flex items-center rounded-md bg-brand-wash px-2.5 py-1 text-xs font-medium text-brand-ink"
                >
                  {activityLabel(item)}
                </span>
              ))}
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
}: {
  offering: UniversityOffering;
  internationalStudentCount: number | null;
}) {
  const percentageStats = [
    {
      key: "retention",
      label: "Tỷ lệ tiếp tục học năm hai",
      value: offering.retentionRate1Yr,
      subtitle: "SV năm 1 quay lại học tiếp",
    },
    {
      key: "graduation",
      label: "Tỷ lệ tốt nghiệp trong 6 năm",
      value: offering.gradRate6Yr,
      subtitle: "Chuẩn CDS B / College Scorecard",
    },
    {
      key: "intlAcceptance",
      label: "Tỷ lệ nhận hồ sơ quốc tế",
      value: offering.intlAcceptanceRate,
      subtitle: "Chấp thuận sinh viên quốc tế",
    },
    {
      key: "satSubmit",
      label: "Tỷ lệ nộp điểm SAT",
      value: offering.satSubmitPct,
      subtitle: "Tân sinh viên nộp SAT",
    },
    {
      key: "gpaSubmit",
      label: "Tỷ lệ nộp GPA cấp 3",
      value: offering.gpaSubmitPct,
      subtitle: "Tân sinh viên có điểm GPA",
    },
  ].filter((item): item is typeof item & { value: number } => item.value != null);

  const countRows = [
    { label: "Tổng hồ sơ nộp", value: universityNumber(offering.applicantsTotal) },
    { label: "Tổng hồ sơ được nhận", value: universityNumber(offering.admittedTotal) },
    { label: "Hồ sơ quốc tế nộp", value: universityNumber(offering.intlApplicants) },
    { label: "Hồ sơ quốc tế được nhận", value: universityNumber(offering.intlAdmitted) },
    { label: "Số sinh viên quốc tế đang theo học", value: universityNumber(internationalStudentCount) },
  ].filter((r) => r.value !== MISSING_UNIVERSITY_DATA);

  return (
    <DetailSection
      id="statistics"
      title="Thống kê sinh viên & tuyển sinh"
      description="Các chỉ số có thể thuộc những kỳ dữ liệu khác nhau. Xem nguồn bên dưới để đối chiếu năm."
    >
      <div className="space-y-6">
        {percentageStats.length > 0 && (
          <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
            {percentageStats.map((item) => (
              <GaugeDataItem
                key={item.key}
                label={item.label}
                value={item.value}
                subtitle={item.subtitle}
              />
            ))}
          </div>
        )}

        {countRows.length > 0 && (
          <div className="border-t border-line pt-5">
            <h3 className="mb-4 text-sm font-semibold text-ink">Số lượng hồ sơ & tuyển sinh</h3>
            <DetailRows rows={countRows} />
          </div>
        )}
      </div>
    </DetailSection>
  );
}

export function UniversityMajors({ offering }: { offering: UniversityOffering }) {
  return (
    <DetailSection
      id="majors"
      title="Ngành học"
      description={`Nhóm ngành có trong dữ liệu hiện tại của bậc học đang chọn (${offering.majors.length} nhóm ngành).`}
    >
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
      {offering.sourceUrl && (
        <div className="mt-5 space-y-4 text-xs text-ink-3">
          <Separator />
          <p>
            Nguồn tổng quan của bậc học: <SourceLink url={offering.sourceUrl} /> · Ngày lấy:{" "}
            {universityDate(offering.retrievedAt)}
          </p>
        </div>
      )}
    </DetailSection>
  );
}
