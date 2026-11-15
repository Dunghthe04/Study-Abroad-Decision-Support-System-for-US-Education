"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
  Building2Icon,
  CheckCircle2Icon,
  AlertCircleIcon,
  DollarSignIcon,
  LockIcon
} from "lucide-react";
import { centerPortalApi, ApiError } from "@/lib/api";
import type { StudyCenter } from "@/types/api";
import { Button } from "@/components/ui/button";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { CenterContactCard } from "@/components/center/CenterContactCard";
import {
  centerNeedsTrainingLicense,
  LEGAL_COMPLIANCE_MODE_DESCRIPTIONS,
  LEGAL_COMPLIANCE_MODE_LABELS,
  normalizeComplianceMode,
  type LegalComplianceMode,
} from "@/lib/center-legal";

const SERVICE_OPTIONS = [
  { key: "CHON_TRUONG", label: "Tư vấn chọn trường & ngành học" },
  { key: "HOC_BONG", label: "Tư vấn & săn học bổng du học Mỹ" },
  { key: "HO_SO_VISA", label: "Luyện phỏng vấn & xin Visa F-1" },
  { key: "LUYEN_THI", label: "Đào tạo chứng chỉ (SAT, ACT, IELTS, TOEFL)" },
  { key: "DINH_CU_VIEC_LAM", label: "Định hướng OPT & cơ hội việc làm sau tốt nghiệp" },
];

const STUDY_LEVEL_OPTIONS = [
  { key: "DaiHoc", label: "Đại học (Undergraduate)" },
  { key: "ThacSi", label: "Thạc sĩ (Master's)" },
  { key: "TienSi", label: "Tiến sĩ (PhD)" },
  { key: "PhoThong", label: "Trung học phổ thông (High School)" },
  { key: "CaoDangCongDong", label: "Cao đẳng cộng đồng (Community College)" },
];

export function CenterProfileView() {
  const [center, setCenter] = useState<StudyCenter | null>(null);
  const [loading, setLoading] = useState(true);

  // Form states
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [website, setWebsite] = useState("");
  const [address, setAddress] = useState("");
  const [city, setCity] = useState("");
  const [district, setDistrict] = useState("");
  const [services, setServices] = useState<string[]>([]);
  const [studyLevels, setStudyLevels] = useState<string[]>([]);
  const [centerType, setCenterType] = useState("study_abroad");
  const [establishedYear, setEstablishedYear] = useState<string>("");
  const [staffCount, setStaffCount] = useState<string>("");
  const [feeMin, setFeeMin] = useState<string>("");
  const [feeMax, setFeeMax] = useState<string>("");
  const [feeDescription, setFeeDescription] = useState("");
  const [logoUrl, setLogoUrl] = useState("");
  const [coverImageUrl, setCoverImageUrl] = useState("");

  // Thông tin pháp lý (khóa khi đã xác minh)
  const [taxCode, setTaxCode] = useState("");
  const [legalRepresentative, setLegalRepresentative] = useState("");
  const [complianceMode, setComplianceMode] = useState<LegalComplianceMode>("licensed");
  const [licenseNumber, setLicenseNumber] = useState("");
  const [licenseIssuedBy, setLicenseIssuedBy] = useState("");
  const [licenseIssuedDate, setLicenseIssuedDate] = useState("");
  const [trainingLicenseNumber, setTrainingLicenseNumber] = useState("");
  const [certifiedCount, setCertifiedCount] = useState("");

  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let active = true;
    void (async () => {
      try {
        const c = await centerPortalApi.getProfile();
        if (active) {
          setCenter(c);
          setName(c.name || "");
          setDescription(c.description || "");
          setWebsite(c.website || "");
          setAddress(c.address || "");
          setCity(c.city || "");
          setDistrict(c.district || "");
          setServices(c.services || []);
          setStudyLevels(c.studyLevels || []);
          setCenterType(c.centerType || "study_abroad");
          setEstablishedYear(c.establishedYear ? String(c.establishedYear) : "");
          setStaffCount(c.staffCount ? String(c.staffCount) : "");
          setFeeMin(c.feeMin ? String(c.feeMin) : "");
          setFeeMax(c.feeMax ? String(c.feeMax) : "");
          setFeeDescription(c.feeDescription || "");
          setLogoUrl(c.logoUrl || "");
          setCoverImageUrl(c.coverImageUrl || "");
          setTaxCode(c.taxCode || "");
          setLegalRepresentative(c.legalRepresentative || "");
          setComplianceMode(normalizeComplianceMode(c.legalComplianceMode));
          setLicenseNumber(c.licenseNumber || "");
          setLicenseIssuedBy(c.licenseIssuedBy || "");
          setLicenseIssuedDate(c.licenseIssuedDate || "");
          setTrainingLicenseNumber(c.trainingLicenseNumber || "");
          setCertifiedCount(c.certifiedCounselorCount != null ? String(c.certifiedCounselorCount) : "");
        }
      } catch (e) {
        console.error("Failed to load center profile", e);
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, []);

  const toggleService = (key: string) => {
    setServices((prev) =>
      prev.includes(key) ? prev.filter((s) => s !== key) : [...prev, key]
    );
  };

  const toggleLevel = (key: string) => {
    setStudyLevels((prev) =>
      prev.includes(key) ? prev.filter((l) => l !== key) : [...prev, key]
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    const fMin = feeMin ? Number(feeMin) : null;
    const fMax = feeMax ? Number(feeMax) : null;

    if (fMin != null && fMax != null && fMin > fMax) {
      setError("Mức phí tối thiểu không được lớn hơn mức phí tối đa.");
      return;
    }

    const fCertified = certifiedCount.trim() ? Number(certifiedCount) : null;
    const fStaff = staffCount ? Number(staffCount) : null;
    if (!legalLocked) {
      if (fCertified != null && (!Number.isInteger(fCertified) || fCertified < 0)) {
        setError("Số tư vấn viên có chứng chỉ phải là số nguyên không âm.");
        return;
      }
      if (fCertified != null && fStaff != null && fCertified > fStaff) {
        setError("Số tư vấn viên có chứng chỉ không được vượt quá quy mô nhân sự.");
        return;
      }
    }

    // Đã xác minh: không gửi tên, loại hình và các trường pháp lý (server cũng chặn)
    const legalPayload = legalLocked
      ? {}
      : {
          name: name.trim(),
          centerType,
          taxCode: taxCode.trim(),
          legalRepresentative: legalRepresentative.trim(),
          legalComplianceMode: complianceMode,
          licenseNumber: licenseNumber.trim(),
          licenseIssuedBy: licenseIssuedBy.trim(),
          licenseIssuedDate: licenseIssuedDate || null,
          trainingLicenseNumber: trainingLicenseNumber.trim(),
          certifiedCounselorCount: fCertified,
        };

    setIsSubmitting(true);
    try {
      const updated = await centerPortalApi.updateProfile({
        ...legalPayload,
        description: description.trim() || null,
        website: website.trim() || null,
        address: address.trim() || null,
        city: city.trim() || null,
        district: district.trim() || null,
        services,
        studyLevels,
        establishedYear: establishedYear ? Number(establishedYear) : null,
        staffCount: staffCount ? Number(staffCount) : null,
        feeMin: fMin,
        feeMax: fMax,
        feeDescription: feeDescription.trim() || null,
        logoUrl: logoUrl.trim() || null,
        coverImageUrl: coverImageUrl.trim() || null,
      });

      setCenter(updated);
      setSuccess("Hồ sơ trung tâm đã được lưu và cập nhật thành công!");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Không thể lưu thông tin hồ sơ. Vui lòng kiểm tra lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  // Đã xác minh: chỉ sửa được thông tin hiển thị, không sửa được thông tin pháp lý
  const legalLocked = center?.verificationStatus === "verified";

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center gap-2 text-ink-2 text-sm">
        <Spinner />
        <span>Đang tải thông tin hồ sơ...</span>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-ink flex items-center gap-2.5">
          <Building2Icon className="h-7 w-7 text-brand" />
          Hồ sơ công bố trung tâm
        </h1>
        <p className="text-sm text-ink-2 mt-1">
          Quản lý thông tin giới thiệu, dịch vụ, bậc học và mức phí hiển thị tới học sinh và phụ huynh.
        </p>
      </div>

      {success && (
        <Alert className="border-emerald-500/30 bg-emerald-500/5 text-emerald-800 dark:text-emerald-200">
          <CheckCircle2Icon className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
          <AlertDescription className="ml-2">{success}</AlertDescription>
        </Alert>
      )}

      {error && (
        <Alert variant="destructive">
          <AlertCircleIcon className="h-5 w-5" />
          <AlertDescription className="ml-2">{error}</AlertDescription>
        </Alert>
      )}

      <CenterContactCard
        phone={center?.phone}
        email={center?.email}
        emailVerified={Boolean(center?.emailVerifiedAt)}
        onChanged={() => {
          void centerPortalApi.getProfile().then(setCenter).catch(() => undefined);
        }}
      />

      {/* Editable Marketing & Services Form */}
      <div className="rounded-xl border border-line bg-surface p-6 space-y-6">
        <h2 className="text-base font-semibold text-ink border-b border-line pb-3">
          Thông tin hiển thị công khai
        </h2>

        <form onSubmit={handleSubmit} className="space-y-6">
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="centerName">Tên hiển thị trung tâm (*)</FieldLabel>
              <Input
                id="centerName"
                required
                maxLength={200}
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={legalLocked}
                placeholder="Tên thương hiệu trung tâm..."
              />
              {legalLocked && (
                <p className="text-xs text-ink-3">
                  Tên trung tâm đã khớp với đăng ký kinh doanh nên không thể sửa sau khi xác minh.
                </p>
              )}
            </Field>

            {/* Thông tin pháp lý: sửa được khi chưa xác minh, khóa khi đã xác minh */}
            <div className="rounded-lg border border-line bg-surface-2/50 p-4 space-y-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <LockIcon className="h-4 w-4 text-ink-3" />
                  <h3 className="text-sm font-semibold text-ink">Thông tin pháp lý</h3>
                </div>
                {legalLocked ? (
                  <Link href="/center/verification" className="text-xs font-semibold text-brand hover:underline">
                    Nộp lại xác minh nếu có thay đổi ĐKKD
                  </Link>
                ) : (
                  <span className="text-xs text-ink-3">Có thể chỉnh sửa cho đến khi được xác minh</span>
                )}
              </div>
              {legalLocked && (
                <p className="text-xs text-ink-3">
                  Trung tâm đã được xác minh: thông tin pháp lý được khóa để đảm bảo tính xác thực. Bạn vẫn sửa được các thông tin hiển thị bên dưới.
                </p>
              )}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <Field>
                  <FieldLabel htmlFor="taxCode">Mã số thuế / Mã số doanh nghiệp</FieldLabel>
                  <Input
                    id="taxCode"
                    inputMode="numeric"
                    maxLength={14}
                    value={taxCode}
                    onChange={(e) => setTaxCode(e.target.value)}
                    disabled={legalLocked}
                    placeholder="10 hoặc 13 chữ số"
                    className="font-mono"
                  />
                </Field>
                <Field>
                  <FieldLabel htmlFor="legalRep">Người đại diện theo pháp luật</FieldLabel>
                  <Input
                    id="legalRep"
                    maxLength={150}
                    value={legalRepresentative}
                    onChange={(e) => setLegalRepresentative(e.target.value)}
                    disabled={legalLocked}
                  />
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor="complianceMode">Hình thức tuân thủ pháp lý</FieldLabel>
                <select
                  id="complianceMode"
                  value={complianceMode}
                  onChange={(e) => setComplianceMode(e.target.value as LegalComplianceMode)}
                  disabled={legalLocked}
                  className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {(Object.keys(LEGAL_COMPLIANCE_MODE_LABELS) as LegalComplianceMode[]).map((m) => (
                    <option key={m} value={m}>
                      {LEGAL_COMPLIANCE_MODE_LABELS[m]}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-ink-3">{LEGAL_COMPLIANCE_MODE_DESCRIPTIONS[complianceMode]}</p>
              </Field>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <Field>
                  <FieldLabel htmlFor="licenseNumber">
                    {complianceMode === "notified" ? "Mã hồ sơ thông báo Sở GD&ĐT" : "Số giấy phép tư vấn du học"}
                  </FieldLabel>
                  <Input
                    id="licenseNumber"
                    maxLength={100}
                    value={licenseNumber}
                    onChange={(e) => setLicenseNumber(e.target.value)}
                    disabled={legalLocked}
                  />
                </Field>
                <Field>
                  <FieldLabel htmlFor="licenseIssuedBy">Cơ quan cấp / tiếp nhận</FieldLabel>
                  <Input
                    id="licenseIssuedBy"
                    maxLength={200}
                    value={licenseIssuedBy}
                    onChange={(e) => setLicenseIssuedBy(e.target.value)}
                    disabled={legalLocked}
                  />
                </Field>
                <Field>
                  <FieldLabel htmlFor="licenseIssuedDate">Ngày cấp / gửi thông báo</FieldLabel>
                  <Input
                    id="licenseIssuedDate"
                    type="date"
                    value={licenseIssuedDate}
                    onChange={(e) => setLicenseIssuedDate(e.target.value)}
                    disabled={legalLocked}
                  />
                </Field>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {centerNeedsTrainingLicense(centerType) && (
                  <Field>
                    <FieldLabel htmlFor="trainingLicense">Giấy phép hoạt động đào tạo ngoại ngữ</FieldLabel>
                    <Input
                      id="trainingLicense"
                      maxLength={100}
                      value={trainingLicenseNumber}
                      onChange={(e) => setTrainingLicenseNumber(e.target.value)}
                      disabled={legalLocked}
                    />
                  </Field>
                )}
                <Field>
                  <FieldLabel htmlFor="certifiedCount">Số tư vấn viên có chứng chỉ</FieldLabel>
                  <Input
                    id="certifiedCount"
                    type="number"
                    min={0}
                    value={certifiedCount}
                    onChange={(e) => setCertifiedCount(e.target.value)}
                    disabled={legalLocked}
                  />
                </Field>
              </div>
            </div>

            <Field>
              <FieldLabel htmlFor="description">Mô tả giới thiệu trung tâm</FieldLabel>
              <Textarea
                id="description"
                rows={4}
                maxLength={4000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Giới thiệu về kinh nghiệm, tỷ lệ đậu visa, đội ngũ tư vấn..."
              />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <Field>
                <FieldLabel htmlFor="city">Tỉnh / Thành phố</FieldLabel>
                <Input
                  id="city"
                  maxLength={128}
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                  placeholder="Hà Nội, TP.HCM..."
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="district">Quận / Huyện</FieldLabel>
                <Input
                  id="district"
                  maxLength={128}
                  value={district}
                  onChange={(e) => setDistrict(e.target.value)}
                  placeholder="Cầu Giấy, Quận 1..."
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="website">Website / Fanpage</FieldLabel>
                <Input
                  id="website"
                  type="url"
                  maxLength={512}
                  value={website}
                  onChange={(e) => setWebsite(e.target.value)}
                  placeholder="https://center.edu.vn"
                />
              </Field>
            </div>

            <Field>
              <FieldLabel htmlFor="address">Địa chỉ văn phòng tư vấn</FieldLabel>
              <Input
                id="address"
                maxLength={512}
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                placeholder="Địa chỉ học sinh đến tư vấn trực tiếp"
              />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field>
                <FieldLabel htmlFor="estYear">Năm thành lập</FieldLabel>
                <Input
                  id="estYear"
                  type="number"
                  min={1990}
                  max={2030}
                  value={establishedYear}
                  onChange={(e) => setEstablishedYear(e.target.value)}
                  placeholder="VD: 2015"
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="staffCount">Quy mô nhân sự tư vấn</FieldLabel>
                <Input
                  id="staffCount"
                  type="number"
                  min={1}
                  value={staffCount}
                  onChange={(e) => setStaffCount(e.target.value)}
                  placeholder="VD: 15"
                />
              </Field>
            </div>

            {/* Services Checklist */}
            <div className="space-y-2 pt-2 border-t border-line">
              <FieldLabel>Dịch vụ tư vấn cung cấp</FieldLabel>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                {SERVICE_OPTIONS.map((opt) => (
                  <label key={opt.key} className="flex items-center gap-2.5 text-sm text-ink-2 cursor-pointer p-2 rounded-lg hover:bg-surface-2 transition-colors">
                    <input
                      type="checkbox"
                      checked={services.includes(opt.key)}
                      onChange={() => toggleService(opt.key)}
                      className="h-4 w-4 rounded border-line text-brand focus:ring-brand"
                    />
                    <span>{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Study Levels Checklist */}
            <div className="space-y-2 pt-2 border-t border-line">
              <FieldLabel>Bậc học tư vấn</FieldLabel>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5">
                {STUDY_LEVEL_OPTIONS.map((opt) => (
                  <label key={opt.key} className="flex items-center gap-2.5 text-sm text-ink-2 cursor-pointer p-2 rounded-lg hover:bg-surface-2 transition-colors">
                    <input
                      type="checkbox"
                      checked={studyLevels.includes(opt.key)}
                      onChange={() => toggleLevel(opt.key)}
                      className="h-4 w-4 rounded border-line text-brand focus:ring-brand"
                    />
                    <span>{opt.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Fee Section (BR-C9) */}
            <div className="space-y-4 pt-4 border-t border-line">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-ink flex items-center gap-2">
                    <DollarSignIcon className="h-4 w-4 text-brand" />
                    Khoảng phí dịch vụ (VNĐ)
                  </h3>
                  <p className="text-xs text-brand font-medium">
                    * Nhãn bắt buộc: &quot;Do trung tâm công bố&quot; (BR-C9)
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <Field>
                  <FieldLabel htmlFor="feeMin">Mức phí từ (VNĐ)</FieldLabel>
                  <Input
                    id="feeMin"
                    type="number"
                    min={0}
                    step={1000000}
                    value={feeMin}
                    onChange={(e) => setFeeMin(e.target.value)}
                    placeholder="VD: 10000000"
                  />
                </Field>

                <Field>
                  <FieldLabel htmlFor="feeMax">Mức phí đến (VNĐ)</FieldLabel>
                  <Input
                    id="feeMax"
                    type="number"
                    min={0}
                    step={1000000}
                    value={feeMax}
                    onChange={(e) => setFeeMax(e.target.value)}
                    placeholder="VD: 50000000"
                  />
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor="feeDesc">Mô tả chi tiết gói phí</FieldLabel>
                <Input
                  id="feeDesc"
                  maxLength={500}
                  value={feeDescription}
                  onChange={(e) => setFeeDescription(e.target.value)}
                  placeholder="Gói cơ bản gồm chọn trường và dịch thuật hồ sơ..."
                />
              </Field>
            </div>

            <Button type="submit" size="lg" disabled={isSubmitting} className="w-full sm:w-auto">
              {isSubmitting && <Spinner data-icon="inline-start" />}
              {isSubmitting ? "Đang lưu thay đổi..." : "Lưu hồ sơ trung tâm"}
            </Button>
          </FieldGroup>
        </form>
      </div>
    </div>
  );
}
