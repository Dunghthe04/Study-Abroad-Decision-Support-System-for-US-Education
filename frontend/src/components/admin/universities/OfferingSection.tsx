"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/contexts/AuthContext";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { findNotNumbers, numberOrNull, textOrNull, toInputValue, todayIso } from "@/lib/admin-form";
import { AdminFormError, getAdminMajors, updateAdminOffering } from "@/lib/admin-university-api";
import {
  ACT_POLICY_OPTIONS,
  AID_POLICY_OPTIONS,
  CAMPUS_SETTING_OPTIONS,
  GPA_SCALE_OPTIONS,
  IMPORTANCE_OPTIONS,
  INTL_HOUSING_OPTIONS,
  SAT_POLICY_OPTIONS,
  SOURCE_RELIABILITY_OPTIONS,
  aidPolicyLabel,
  campusSettingLabel,
  formatFraction,
  gpaScaleLabel,
  importanceLabel,
  intlHousingLabel,
  formatLastEdited,
  formatNumber,
  formatRetrievedAt,
  formatSatRange,
  formatUsd,
  actPolicyLabel,
  parseDeadlines,
  satPolicyLabel,
  sourceReliabilityLabel,
} from "@/lib/admin-university-display";
import { studyLevelLabel } from "@/lib/study-levels";
import type {
  AdminFieldErrors,
  AdminMajorOption,
  AdminUniversityOffering,
} from "@/types/admin-university";
import { BlockBody, EditableTable, TableFrame, type TableEditState, type TableRow } from "./EditableTable";
import { OptionSelect } from "./OptionSelect";

export type OfferingTable = "cost" | "requirements" | "stats" | "aid" | "campus" | "majors" | "source";

interface OfferingSectionProps {
  universityId: string;
  offering: AdminUniversityOffering;
  /** Bảng đang sửa của bậc học này; null nếu không sửa bảng nào của bậc học này. */
  editingTable: OfferingTable | null;
  /** false khi đang sửa một bảng khác trong panel. */
  canEdit: boolean;
  onStartEdit: (table: OfferingTable) => void;
  onStopEdit: () => void;
  onSaved: (offering: AdminUniversityOffering) => void;
}

/** Các ô số, khóa trùng tên trường dữ liệu gửi lên API. */
const NUMBER_FIELDS = [
  "tuitionUsd",
  "livingUsd",
  "feesUsd",
  "otherExpensesUsd",
  "healthInsuranceUsd",
  "applicationFeeUsd",
  "tuitionInStateUsd",
  "actSubmitPct",
  "minGpa4",
  "avgGpa4",
  "minIelts",
  "minToefl",
  "minDuolingo",
  "sat25",
  "sat75",
  "act25",
  "act75",
  "applicantsTotal",
  "admittedTotal",
  "intlApplicants",
  "intlAdmitted",
  "intlAcceptanceRate",
  "satSubmitPct",
  "gpaSubmitPct",
  "gpaPct40",
  "gpaPct375To399",
  "gpaPct350To374",
  "gpaPct325To349",
  "gpaPct300To324",
  "gpaPct250To299",
  "gpaPctBelow250",
  "avgGpaWeighted",
  "retentionRate1Yr",
  "gradRate6Yr",
  "studentFacultyRatio",
  "intlAidAvgUsd",
  "intlAidRecipients",
  "fyOnCampusPct",
  "ugOnCampusPct",
  "housingOnlyUsd",
  "housingDepositUsd",
] as const;

type NumberField = (typeof NUMBER_FIELDS)[number];

const NUMBER_LABELS: Record<NumberField, string> = {
  tuitionUsd: "Học phí (USD/năm)",
  livingUsd: "Sinh hoạt (USD/năm)",
  feesUsd: "Các khoản phí (USD/năm)",
  otherExpensesUsd: "Chi phí khác (USD/năm)",
  healthInsuranceUsd: "Bảo hiểm y tế (USD/năm)",
  applicationFeeUsd: "Phí nộp hồ sơ (USD, một lần)",
  tuitionInStateUsd: "Học phí in-state (USD/năm, chỉ tham khảo)",
  actSubmitPct: "Tỉ lệ nộp ACT (0–1)",
  minGpa4: "GPA tối thiểu (thang 4)",
  avgGpa4: "GPA TB trúng tuyển",
  minIelts: "IELTS tối thiểu",
  minToefl: "TOEFL iBT tối thiểu",
  minDuolingo: "Duolingo tối thiểu",
  sat25: "SAT mốc 25%",
  sat75: "SAT mốc 75%",
  act25: "ACT mốc 25%",
  act75: "ACT mốc 75%",
  applicantsTotal: "Số hồ sơ nộp",
  admittedTotal: "Số hồ sơ đỗ",
  intlApplicants: "Số hồ sơ nộp – du học sinh",
  intlAdmitted: "Số hồ sơ đỗ – du học sinh",
  intlAcceptanceRate: "Tỉ lệ nhận du học sinh (0–1)",
  satSubmitPct: "Tỉ lệ nộp SAT (0–1)",
  gpaSubmitPct: "Tỉ lệ nộp GPA (0–1)",
  gpaPct40: "Tỉ lệ GPA 4.0 (0–1)",
  gpaPct375To399: "Tỉ lệ GPA 3.75–3.99 (0–1)",
  gpaPct350To374: "Tỉ lệ GPA 3.50–3.74 (0–1)",
  gpaPct325To349: "Tỉ lệ GPA 3.25–3.49 (0–1)",
  gpaPct300To324: "Tỉ lệ GPA 3.00–3.24 (0–1)",
  gpaPct250To299: "Tỉ lệ GPA 2.50–2.99 (0–1)",
  gpaPctBelow250: "Tỉ lệ GPA dưới 2.50 (0–1)",
  avgGpaWeighted: "GPA TB có trọng số (thang 5)",
  retentionRate1Yr: "Tỉ lệ giữ chân năm 1 (0–1)",
  gradRate6Yr: "Tỉ lệ tốt nghiệp 6 năm (0–1)",
  studentFacultyRatio: "Tỉ lệ SV/giảng viên (x:1)",
  intlAidAvgUsd: "Hỗ trợ TB mỗi du học sinh (USD)",
  intlAidRecipients: "Số du học sinh được hỗ trợ",
  fyOnCampusPct: "Tỉ lệ năm nhất ở KTX (0–1)",
  ugOnCampusPct: "Tỉ lệ SV đại học ở KTX (0–1)",
  housingOnlyUsd: "Tiền phòng KTX (USD/năm, chưa gồm ăn)",
  housingDepositUsd: "Tiền cọc KTX (USD)",
};

const COST_FIELDS: NumberField[] = [
  "tuitionUsd",
  "livingUsd",
  "feesUsd",
  "otherExpensesUsd",
  "healthInsuranceUsd",
  "applicationFeeUsd",
  "tuitionInStateUsd",
];
const REQUIREMENT_FIELDS: NumberField[] = ["minGpa4", "avgGpa4", "minIelts", "minToefl", "minDuolingo"];
const TEST_FIELDS: NumberField[] = ["sat25", "sat75", "act25", "act75", "actSubmitPct"];
const STATS_FIELDS: NumberField[] = [
  "applicantsTotal",
  "admittedTotal",
  "intlApplicants",
  "intlAdmitted",
  "intlAcceptanceRate",
  "satSubmitPct",
  "gpaSubmitPct",
  "avgGpaWeighted",
  "gpaPct40",
  "gpaPct375To399",
  "gpaPct350To374",
  "gpaPct325To349",
  "gpaPct300To324",
  "gpaPct250To299",
  "gpaPctBelow250",
  "retentionRate1Yr",
  "gradRate6Yr",
  "studentFacultyRatio",
];
const AID_FIELDS: NumberField[] = ["intlAidAvgUsd", "intlAidRecipients"];
const CAMPUS_FIELDS: NumberField[] = ["fyOnCampusPct", "ugOnCampusPct", "housingOnlyUsd", "housingDepositUsd"];

/** Các ô chữ/chọn mới, khóa trùng tên trường dữ liệu gửi lên API. */
const TEXT_FIELDS = [
  "gpaScaleType",
  "c7GpaImportance",
  "c7TestImportance",
  "c7EcImportance",
  "intlAidPolicy",
  "campusSetting",
  "housingTypes",
  "activities",
] as const;
type TextField = (typeof TEXT_FIELDS)[number];

interface FormState {
  numbers: Record<NumberField, string>;
  texts: Record<TextField, string>;
  /** "true" | "false" | "" (chưa rõ) */
  intlHousing: string;
  academicYear: string;
  satPolicy: string;
  actPolicy: string;
  sourceUrl: string;
  retrievedAt: string;
  sourceReliability: string;
  notes: string;
  /** majorId -> isStem; ngành không có trong map là trường không đào tạo. */
  majors: Record<string, boolean>;
}

function toFormState(offering: AdminUniversityOffering): FormState {
  const numbers = {} as Record<NumberField, string>;
  for (const field of NUMBER_FIELDS) {
    numbers[field] = toInputValue(offering[field]);
  }
  const texts = {} as Record<TextField, string>;
  for (const field of TEXT_FIELDS) {
    texts[field] = toInputValue(offering[field]);
  }
  const majors: Record<string, boolean> = {};
  for (const item of offering.majorItems) {
    majors[item.majorId] = item.isStem;
  }
  return {
    numbers,
    texts,
    intlHousing: offering.intlHousing === null ? "" : String(offering.intlHousing),
    academicYear: toInputValue(offering.academicYear),
    satPolicy: toInputValue(offering.satPolicy),
    actPolicy: toInputValue(offering.actPolicy),
    sourceUrl: toInputValue(offering.sourceUrl),
    retrievedAt: toInputValue(offering.retrievedAt),
    sourceReliability: toInputValue(offering.sourceReliability),
    notes: toInputValue(offering.notes),
    majors,
  };
}

const INVALID_MESSAGE = "Dữ liệu chưa hợp lệ, xem các ô được đánh dấu.";

export function OfferingSection({
  universityId,
  offering,
  editingTable,
  canEdit,
  onStartEdit,
  onStopEdit,
  onSaved,
}: OfferingSectionProps) {
  // [UC-66] Chỉ Moderator sửa dữ liệu trường; Admin chỉ xem
  const { user } = useAuth();
  const readOnly = user?.role !== "moderator";
  const [form, setForm] = useState<FormState>(() => toFormState(offering));
  const [fieldErrors, setFieldErrors] = useState<AdminFieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [majorOptions, setMajorOptions] = useState<AdminMajorOption[] | null>(null);
  const [majorLoadError, setMajorLoadError] = useState<string | null>(null);

  const isEditingMajors = editingTable === "majors";

  useEffect(() => {
    if (!isEditingMajors || majorOptions) {
      return;
    }
    let cancelled = false;
    getAdminMajors()
      .then((loaded) => {
        if (!cancelled) setMajorOptions(loaded);
      })
      .catch((err: unknown) => {
        if (!cancelled) setMajorLoadError(err instanceof Error ? err.message : "Không thể tải danh mục ngành.");
      });
    return () => {
      cancelled = true;
    };
  }, [isEditingMajors, majorOptions]);

  const hasChanges = JSON.stringify(form) !== JSON.stringify(toFormState(offering));

  const handleEdit = (table: OfferingTable) => {
    setForm(toFormState(offering));
    setFieldErrors({});
    setError(null);
    onStartEdit(table);
  };

  const setNumber = (field: NumberField, value: string) => {
    setForm({ ...form, numbers: { ...form.numbers, [field]: value } });
  };

  const setText = (field: TextField, value: string) => {
    setForm({ ...form, texts: { ...form.texts, [field]: value } });
  };

  const toggleMajor = (majorId: string, included: boolean) => {
    const majors = { ...form.majors };
    if (included) {
      majors[majorId] = false;
    } else {
      delete majors[majorId];
    }
    setForm({ ...form, majors });
  };

  const toggleStem = (majorId: string, isStem: boolean) => {
    setForm({ ...form, majors: { ...form.majors, [majorId]: isStem } });
  };

  const handleSave = async () => {
    const numbers = {} as Record<NumberField, number | null>;
    for (const field of NUMBER_FIELDS) {
      numbers[field] = numberOrNull(form.numbers[field]);
    }
    const notNumbers = findNotNumbers(numbers);
    if (notNumbers.length > 0) {
      const errors: AdminFieldErrors = {};
      for (const field of notNumbers) {
        errors[field] = "Vui lòng nhập số.";
      }
      setFieldErrors(errors);
      setError(INVALID_MESSAGE);
      return;
    }

    // SAT cần đủ cả 2 mốc (công thức gợi ý dùng cả hai); báo sớm trước khi gửi.
    if ((numbers.sat25 === null) !== (numbers.sat75 === null)) {
      setFieldErrors({ sat25: "Nhập đủ cả SAT 25% và 75%, hoặc để trống cả hai." });
      setError(INVALID_MESSAGE);
      return;
    }

    const majors = Object.entries(form.majors).map(([majorId, isStem]) => ({ majorId, isStem }));
    const texts = {} as Record<TextField, string | null>;
    for (const field of TEXT_FIELDS) {
      texts[field] = textOrNull(form.texts[field]);
    }

    setIsSaving(true);
    setError(null);
    setFieldErrors({});
    try {
      const saved = await updateAdminOffering(universityId, offering.id, {
        ...numbers,
        academicYear: textOrNull(form.academicYear),
        satPolicy: textOrNull(form.satPolicy),
        actPolicy: textOrNull(form.actPolicy),
        sourceUrl: textOrNull(form.sourceUrl),
        retrievedAt: textOrNull(form.retrievedAt),
        sourceReliability: textOrNull(form.sourceReliability),
        notes: textOrNull(form.notes),
        ...texts,
        intlHousing: form.intlHousing === "" ? null : form.intlHousing === "true",
        majors,
      });
      toast.add({ type: "success", title: "Đã lưu bậc học", description: studyLevelLabel(saved.studyLevel) });
      onSaved(saved);
    } catch (err) {
      if (err instanceof AdminFormError) {
        const missingSource = editingTable !== "source" && (err.fieldErrors.sourceUrl || err.fieldErrors.retrievedAt);
        setError(missingSource ? "Bậc học này chưa có nguồn hoặc ngày lấy hợp lệ. Sửa bảng Nguồn dữ liệu trước." : err.message);
        setFieldErrors(err.fieldErrors);
      } else {
        setError("Không thể lưu. Vui lòng thử lại sau.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const editStateFor = (table: OfferingTable): TableEditState => ({
    isEditing: editingTable === table,
    canEdit,
    readOnly,
    isSaving,
    hasChanges,
    error: editingTable === table ? error : null,
    onEdit: () => handleEdit(table),
    onCancel: onStopEdit,
    onSave: handleSave,
  });

  const numberRow = (field: NumberField, autoFocus = false): TableRow => ({
    label: NUMBER_LABELS[field],
    inputId: `offering-${offering.id}-${field}`,
    error: fieldErrors[field],
    value: (
      <Input
        id={`offering-${offering.id}-${field}`}
        inputMode="decimal"
        value={form.numbers[field]}
        onChange={(e) => setNumber(field, e.target.value)}
        aria-invalid={Boolean(fieldErrors[field]) || undefined}
        disabled={isSaving}
        autoFocus={autoFocus}
      />
    ),
  });

  const costRows = (): TableRow[] => {
    if (editingTable !== "cost") {
      const hasCost =
        offering.tuitionUsd !== null ||
        offering.livingUsd !== null ||
        offering.feesUsd !== null ||
        offering.otherExpensesUsd !== null;
      const totalCost =
        (offering.tuitionUsd ?? 0) +
        (offering.livingUsd ?? 0) +
        (offering.feesUsd ?? 0) +
        (offering.otherExpensesUsd ?? 0);
      return [
        { label: "Năm học", value: offering.academicYear ?? "—" },
        { label: "Học phí", value: formatUsd(offering.tuitionUsd) },
        { label: "Sinh hoạt", value: formatUsd(offering.livingUsd) },
        { label: "Các khoản phí", value: formatUsd(offering.feesUsd) },
        { label: "Chi phí khác", value: formatUsd(offering.otherExpensesUsd) },
        { label: "Bảo hiểm y tế", value: formatUsd(offering.healthInsuranceUsd) },
        { label: "Phí nộp hồ sơ (một lần)", value: formatUsd(offering.applicationFeeUsd) },
        { label: "Học phí in-state (tham khảo)", value: formatUsd(offering.tuitionInStateUsd) },
        { label: "Tổng", value: hasCost ? formatUsd(totalCost) : "—" },
      ];
    }

    const yearInputId = `offering-${offering.id}-year`;
    const yearRow: TableRow = {
      label: "Năm học",
      inputId: yearInputId,
      error: fieldErrors.academicYear,
      value: (
        <Input
          id={yearInputId}
          value={form.academicYear}
          onChange={(e) => setForm({ ...form, academicYear: e.target.value })}
          aria-invalid={Boolean(fieldErrors.academicYear) || undefined}
          placeholder="2026-27"
          disabled={isSaving}
          autoFocus
        />
      ),
    };
    const rows = [yearRow];
    for (const field of COST_FIELDS) {
      rows.push(numberRow(field));
    }
    return rows;
  };

  const requirementRows = (): TableRow[] => {
    if (editingTable !== "requirements") {
      return [
        { label: "GPA tối thiểu (thang 4)", value: formatNumber(offering.minGpa4) },
        { label: "GPA TB trúng tuyển", value: formatNumber(offering.avgGpa4) },
        { label: "IELTS tối thiểu", value: formatNumber(offering.minIelts) },
        { label: "TOEFL tối thiểu", value: formatNumber(offering.minToefl) },
        { label: "Duolingo tối thiểu", value: formatNumber(offering.minDuolingo) },
        { label: "SAT 25% – 75%", value: formatSatRange(offering.sat25, offering.sat75) },
        { label: "ACT 25% – 75%", value: formatSatRange(offering.act25, offering.act75) },
        { label: "Chính sách SAT", value: satPolicyLabel(offering.satPolicy) },
        { label: "Chính sách ACT", value: actPolicyLabel(offering.actPolicy) },
        { label: "Tỉ lệ nộp ACT", value: formatNumber(offering.actSubmitPct) },
      ];
    }

    const rows: TableRow[] = [];
    REQUIREMENT_FIELDS.forEach((field, index) => {
      rows.push(numberRow(field, index === 0));
    });
    for (const field of TEST_FIELDS) {
      rows.push(numberRow(field));
    }
    const policyInputId = `offering-${offering.id}-sat-policy`;
    rows.push({
      label: "Chính sách SAT",
      inputId: policyInputId,
      error: fieldErrors.satPolicy,
      value: (
        <OptionSelect
          id={policyInputId}
          value={form.satPolicy}
          options={SAT_POLICY_OPTIONS}
          onChange={(value) => setForm({ ...form, satPolicy: value })}
          invalid={Boolean(fieldErrors.satPolicy)}
          disabled={isSaving}
        />
      ),
    });
    const actPolicyInputId = `offering-${offering.id}-act-policy`;
    rows.push({
      label: "Chính sách ACT",
      inputId: actPolicyInputId,
      error: fieldErrors.actPolicy,
      value: (
        <OptionSelect
          id={actPolicyInputId}
          value={form.actPolicy}
          options={ACT_POLICY_OPTIONS}
          onChange={(value) => setForm({ ...form, actPolicy: value })}
          invalid={Boolean(fieldErrors.actPolicy)}
          disabled={isSaving}
        />
      ),
    });
    return rows;
  };

  const selectRow = (
    field: TextField,
    label: string,
    options: { value: string; label: string }[],
    shown: string,
  ): TableRow => {
    const id = `offering-${offering.id}-${field}`;
    if (!editingTable) return { label, value: shown };
    return {
      label,
      inputId: id,
      error: fieldErrors[field],
      value: (
        <OptionSelect
          id={id}
          value={form.texts[field]}
          options={options}
          onChange={(value) => setText(field, value)}
          invalid={Boolean(fieldErrors[field])}
          disabled={isSaving}
        />
      ),
    };
  };

  const textAreaRow = (field: TextField, label: string, max: number): TableRow => ({
    label,
    inputId: `offering-${offering.id}-${field}`,
    error: fieldErrors[field],
    value: (
      <Textarea
        id={`offering-${offering.id}-${field}`}
        value={form.texts[field]}
        onChange={(e) => setText(field, e.target.value)}
        aria-invalid={Boolean(fieldErrors[field]) || undefined}
        maxLength={max}
        disabled={isSaving}
      />
    ),
  });

  const statsRows = (): TableRow[] => {
    if (editingTable !== "stats") {
      const gpaBands: [string, number | null][] = [
        ["GPA 4.0", offering.gpaPct40],
        ["GPA 3.75–3.99", offering.gpaPct375To399],
        ["GPA 3.50–3.74", offering.gpaPct350To374],
        ["GPA 3.25–3.49", offering.gpaPct325To349],
        ["GPA 3.00–3.24", offering.gpaPct300To324],
        ["GPA 2.50–2.99", offering.gpaPct250To299],
        ["GPA dưới 2.50", offering.gpaPctBelow250],
      ];
      return [
        { label: "Hồ sơ nộp / đỗ", value: `${formatNumber(offering.applicantsTotal)} / ${formatNumber(offering.admittedTotal)}` },
        { label: "Hồ sơ nộp / đỗ – du học sinh", value: `${formatNumber(offering.intlApplicants)} / ${formatNumber(offering.intlAdmitted)}` },
        { label: "Tỉ lệ nhận du học sinh", value: formatFraction(offering.intlAcceptanceRate) },
        { label: "Tỉ lệ nộp SAT / GPA", value: `${formatFraction(offering.satSubmitPct)} / ${formatFraction(offering.gpaSubmitPct)}` },
        { label: "GPA TB có trọng số", value: formatNumber(offering.avgGpaWeighted) },
        { label: "Thang GPA trường báo cáo", value: gpaScaleLabel(offering.gpaScaleType) },
        ...gpaBands.map(([label, value]) => ({ label, value: formatFraction(value) })),
        { label: "Coi trọng GPA", value: importanceLabel(offering.c7GpaImportance) },
        { label: "Coi trọng điểm thi", value: importanceLabel(offering.c7TestImportance) },
        { label: "Coi trọng ngoại khóa", value: importanceLabel(offering.c7EcImportance) },
        { label: "Giữ chân năm 1", value: formatFraction(offering.retentionRate1Yr) },
        { label: "Tốt nghiệp 6 năm", value: formatFraction(offering.gradRate6Yr) },
        { label: "SV / giảng viên", value: offering.studentFacultyRatio === null ? "—" : `${offering.studentFacultyRatio}:1` },
      ];
    }
    const rows: TableRow[] = [];
    STATS_FIELDS.forEach((field, index) => rows.push(numberRow(field, index === 0)));
    rows.push(selectRow("gpaScaleType", "Thang GPA trường báo cáo", GPA_SCALE_OPTIONS, ""));
    rows.push(selectRow("c7GpaImportance", "Coi trọng GPA", IMPORTANCE_OPTIONS, ""));
    rows.push(selectRow("c7TestImportance", "Coi trọng điểm thi", IMPORTANCE_OPTIONS, ""));
    rows.push(selectRow("c7EcImportance", "Coi trọng ngoại khóa", IMPORTANCE_OPTIONS, ""));
    return rows;
  };

  const aidRows = (): TableRow[] => {
    if (editingTable !== "aid") {
      return [
        { label: "Chính sách hỗ trợ", value: aidPolicyLabel(offering.intlAidPolicy) },
        { label: "Hỗ trợ TB mỗi du học sinh", value: formatUsd(offering.intlAidAvgUsd) },
        { label: "Số du học sinh được hỗ trợ", value: formatNumber(offering.intlAidRecipients) },
      ];
    }
    return [
      selectRow("intlAidPolicy", "Chính sách hỗ trợ", AID_POLICY_OPTIONS, ""),
      ...AID_FIELDS.map((field) => numberRow(field)),
    ];
  };

  const campusRows = (): TableRow[] => {
    if (editingTable !== "campus") {
      return [
        { label: "Bối cảnh khuôn viên", value: campusSettingLabel(offering.campusSetting) },
        { label: "Năm nhất ở KTX", value: formatFraction(offering.fyOnCampusPct) },
        { label: "SV đại học ở KTX", value: formatFraction(offering.ugOnCampusPct) },
        { label: "Nhà ở cho du học sinh", value: intlHousingLabel(offering.intlHousing) },
        { label: "Tiền phòng KTX", value: formatUsd(offering.housingOnlyUsd) },
        { label: "Tiền cọc KTX", value: formatUsd(offering.housingDepositUsd) },
        { label: "Loại nhà ở", value: offering.housingTypes ?? "—" },
        { label: "Hoạt động sinh viên", value: offering.activities ?? "—" },
      ];
    }
    const housingId = `offering-${offering.id}-intlHousing`;
    return [
      selectRow("campusSetting", "Bối cảnh khuôn viên", CAMPUS_SETTING_OPTIONS, ""),
      ...CAMPUS_FIELDS.map((field) => numberRow(field)),
      {
        label: "Nhà ở cho du học sinh",
        inputId: housingId,
        error: fieldErrors.intlHousing,
        value: (
          <OptionSelect
            id={housingId}
            value={form.intlHousing}
            options={INTL_HOUSING_OPTIONS}
            onChange={(value) => setForm({ ...form, intlHousing: value })}
            disabled={isSaving}
          />
        ),
      },
      textAreaRow("housingTypes", "Loại nhà ở (cách nhau bằng |)", 2000),
      textAreaRow("activities", "Hoạt động sinh viên (cách nhau bằng |)", 4000),
    ];
  };

  const sourceRows = (): TableRow[] => {
    const lastEditedRow: TableRow = {
      label: "Sửa lần cuối",
      value: formatLastEdited(offering.updatedAt, offering.lastEditedBy),
    };

    if (editingTable !== "source") {
      const sourceLink = offering.sourceUrl ? (
        <a
          href={offering.sourceUrl}
          target="_blank"
          rel="noreferrer"
          className="text-brand underline-offset-2 hover:underline"
        >
          Mở liên kết
        </a>
      ) : (
        "—"
      );
      return [
        { label: "Nguồn", value: sourceLink },
        { label: "Ngày lấy", value: formatRetrievedAt(offering.retrievedAt) },
        { label: "Mức tin cậy", value: sourceReliabilityLabel(offering.sourceReliability) },
        { label: "Ghi chú", value: offering.notes ?? "—" },
        lastEditedRow,
      ];
    }

    const idPrefix = `offering-${offering.id}`;
    return [
      {
        label: "Liên kết nguồn *",
        inputId: `${idPrefix}-source`,
        error: fieldErrors.sourceUrl,
        value: (
          <Input
            id={`${idPrefix}-source`}
            value={form.sourceUrl}
            onChange={(e) => setForm({ ...form, sourceUrl: e.target.value })}
            aria-invalid={Boolean(fieldErrors.sourceUrl) || undefined}
            placeholder="https://"
            disabled={isSaving}
            autoFocus
          />
        ),
      },
      {
        label: "Ngày lấy dữ liệu *",
        inputId: `${idPrefix}-retrieved`,
        error: fieldErrors.retrievedAt,
        value: (
          <Input
            id={`${idPrefix}-retrieved`}
            type="date"
            max={todayIso()}
            value={form.retrievedAt}
            onChange={(e) => setForm({ ...form, retrievedAt: e.target.value })}
            aria-invalid={Boolean(fieldErrors.retrievedAt) || undefined}
            disabled={isSaving}
          />
        ),
      },
      {
        label: "Mức tin cậy",
        inputId: `${idPrefix}-reliability`,
        error: fieldErrors.sourceReliability,
        value: (
          <OptionSelect
            id={`${idPrefix}-reliability`}
            value={form.sourceReliability}
            options={SOURCE_RELIABILITY_OPTIONS}
            onChange={(value) => setForm({ ...form, sourceReliability: value })}
            invalid={Boolean(fieldErrors.sourceReliability)}
            disabled={isSaving}
          />
        ),
      },
      {
        label: "Ghi chú",
        inputId: `${idPrefix}-notes`,
        error: fieldErrors.notes,
        value: (
          <Textarea
            id={`${idPrefix}-notes`}
            value={form.notes}
            onChange={(e) => setForm({ ...form, notes: e.target.value })}
            aria-invalid={Boolean(fieldErrors.notes) || undefined}
            maxLength={2000}
            disabled={isSaving}
          />
        ),
      },
      lastEditedRow,
    ];
  };

  const deadlines = parseDeadlines(offering.deadlinesJson);
  let deadlineRows: TableRow[] = [{ label: "Chưa có dữ liệu", value: "—" }];
  if (deadlines.length > 0) {
    deadlineRows = deadlines.map((deadline) => ({ label: deadline.round, value: deadline.date }));
  }

  const majorCount = isEditingMajors ? Object.keys(form.majors).length : offering.majors.length;

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-base font-semibold">{studyLevelLabel(offering.studyLevel)}</h3>
        {offering.academicYear && <Badge variant="outline">{offering.academicYear}</Badge>}
      </div>

      <EditableTable title="Chi phí mỗi năm" rows={costRows()} edit={editStateFor("cost")} />
      <EditableTable title="Điều kiện" rows={requirementRows()} edit={editStateFor("requirements")} />
      <EditableTable title="Hạn nộp hồ sơ" rows={deadlineRows} />
      <EditableTable title="Tuyển sinh & thống kê" rows={statsRows()} edit={editStateFor("stats")} />
      <EditableTable title="Hỗ trợ tài chính cho du học sinh" rows={aidRows()} edit={editStateFor("aid")} />
      <EditableTable title="Khuôn viên & nhà ở" rows={campusRows()} edit={editStateFor("campus")} />

      <TableFrame title={`Ngành (${majorCount})`} edit={editStateFor("majors")}>
        <BlockBody>
          {isEditingMajors ? (
            <MajorChecklist
              offering={offering}
              options={majorOptions}
              loadError={majorLoadError}
              selected={form.majors}
              fieldError={fieldErrors.majors}
              disabled={isSaving}
              onToggleMajor={toggleMajor}
              onToggleStem={toggleStem}
            />
          ) : (
            <MajorBadges offering={offering} />
          )}
        </BlockBody>
      </TableFrame>

      <EditableTable title="Nguồn dữ liệu" rows={sourceRows()} edit={editStateFor("source")} />
    </section>
  );
}

function MajorBadges({ offering }: { offering: AdminUniversityOffering }) {
  const unverifiedCount = offering.majorItems.filter((item) => !item.isVerified).length;

  return (
    <>
      {offering.majors.length === 0 ? (
        <span className="text-muted-foreground">—</span>
      ) : (
        <div className="flex flex-wrap gap-1">
          {offering.majors.map((major) => (
            <Badge key={major} variant={offering.stemMajors.includes(major) ? "brand" : "outline"}>
              {major}
            </Badge>
          ))}
        </div>
      )}
      {offering.stemMajors.length > 0 && (
        <p className="mt-2 text-xs text-muted-foreground">Ngành tô đậm là ngành STEM.</p>
      )}
      {unverifiedCount > 0 && (
        <p className="mt-1 text-xs text-muted-foreground">
          {unverifiedCount} ngành lấy từ College Scorecard, chưa xác minh với CDS.
        </p>
      )}
    </>
  );
}

function MajorChecklist({
  offering,
  options,
  loadError,
  selected,
  fieldError,
  disabled,
  onToggleMajor,
  onToggleStem,
}: {
  offering: AdminUniversityOffering;
  options: AdminMajorOption[] | null;
  loadError: string | null;
  selected: Record<string, boolean>;
  fieldError: string | undefined;
  disabled: boolean;
  onToggleMajor: (majorId: string, included: boolean) => void;
  onToggleStem: (majorId: string, isStem: boolean) => void;
}) {
  const unverifiedMajorIds = new Set(
    offering.majorItems.filter((item) => !item.isVerified).map((item) => item.majorId),
  );

  if (loadError) {
    return (
      <Alert variant="destructive">
        <AlertDescription>{loadError}</AlertDescription>
      </Alert>
    );
  }

  if (!options) {
    return (
      <div className="flex items-center gap-2 text-muted-foreground">
        <Spinner />
        Đang tải danh mục ngành...
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {fieldError && <p className="text-xs text-destructive">{fieldError}</p>}
      <div className="-mx-3 max-h-80 overflow-y-auto">
        {options.map((major) => {
          const included = major.id in selected;
          return (
            <div
              key={major.id}
              className="flex items-center gap-3 border-b border-border px-3 py-1.5 last:border-b-0"
            >
              <label className="flex min-w-0 flex-1 items-center gap-2">
                <Checkbox
                  checked={included}
                  onCheckedChange={(checked) => onToggleMajor(major.id, checked === true)}
                  disabled={disabled}
                />
                <span className="min-w-0">
                  {major.name}
                  {major.cipCode && <span className="ml-1 text-xs text-muted-foreground">CIP {major.cipCode}</span>}
                </span>
                {included && unverifiedMajorIds.has(major.id) && <Badge variant="outline">Chưa xác minh</Badge>}
              </label>
              <label className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <Checkbox
                  checked={selected[major.id] === true}
                  onCheckedChange={(checked) => onToggleStem(major.id, checked === true)}
                  disabled={disabled || !included}
                />
                STEM
              </label>
            </div>
          );
        })}
      </div>
    </div>
  );
}
