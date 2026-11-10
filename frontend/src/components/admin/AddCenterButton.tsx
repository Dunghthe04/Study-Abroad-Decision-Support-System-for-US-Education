"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { PlusIcon, UserPlusIcon } from "lucide-react";
import { studyCentersApi, ApiError } from "@/lib/api";
import { STUDY_LEVELS, STUDY_LEVEL_LABELS } from "@/lib/study-levels";
import { useAuth } from "@/contexts/AuthContext";
import { CreateCenterAccountButton } from "./centers/CreateCenterAccountButton";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { toast } from "@/components/ui/toast";
import { cn } from "@/lib/utils";

const COMMON_SERVICES = [
  { key: "CHON_TRUONG", label: "Tư vấn chọn trường" },
  { key: "HO_SO", label: "Làm hồ sơ du học" },
  { key: "LUYEN_PHONG_VAN", label: "Luyện phỏng vấn visa" },
  { key: "HOC_BONG", label: "Hỗ trợ săn học bổng" },
  { key: "VIET_LUAN", label: "Hướng dẫn viết luận" },
  { key: "CHUNG_CHI", label: "Luyện thi chứng chỉ (SAT, IELTS...)" },
];

export interface AddCenterButtonProps {
  className?: string;
  size?: "default" | "sm" | "lg" | "xs" | "icon" | "icon-xs" | "icon-sm" | "icon-lg";
}

export function AddCenterButton({ className, size = "sm" }: AddCenterButtonProps = {}) {
  const { user } = useAuth();
  const router = useRouter();

  const [isOpen, setIsOpen] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [city, setCity] = useState("");
  const [district, setDistrict] = useState("");
  const [address, setAddress] = useState("");
  const [website, setWebsite] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [centerType, setCenterType] = useState("study_abroad");
  const [licenseNumber, setLicenseNumber] = useState("");
  const [establishedYear, setEstablishedYear] = useState<string>("");
  const [staffCount, setStaffCount] = useState<string>("");
  const [visaSuccessRate, setVisaSuccessRate] = useState<string>("");
  const [logoUrl, setLogoUrl] = useState("");
  const [accreditationsInput, setAccreditationsInput] = useState("");
  const [description, setDescription] = useState("");
  const [selectedLevels, setSelectedLevels] = useState<string[]>([]);
  const [selectedServices, setSelectedServices] = useState<string[]>([]);

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);

  // Admin hoặc Moderator có quyền thêm trung tâm vào danh bạ (UC-71)
  const canAdd = user?.role === "admin" || user?.role === "moderator";
  if (!canAdd) return null;

  const handleToggleLevel = (lvl: string) => {
    setSelectedLevels((prev) =>
      prev.includes(lvl) ? prev.filter((x) => x !== lvl) : [...prev, lvl]
    );
  };

  const handleToggleService = (svc: string) => {
    setSelectedServices((prev) =>
      prev.includes(svc) ? prev.filter((x) => x !== svc) : [...prev, svc]
    );
  };

  const handleOpen = () => {
    setCode("");
    setName("");
    setCity("");
    setDistrict("");
    setAddress("");
    setWebsite("");
    setPhone("");
    setEmail("");
    setCenterType("study_abroad");
    setLicenseNumber("");
    setEstablishedYear("");
    setStaffCount("");
    setVisaSuccessRate("");
    setLogoUrl("");
    setAccreditationsInput("");
    setDescription("");
    setSelectedLevels([]);
    setSelectedServices([]);
    setError(null);
    setIsOpen(true);
  };

  const handleClose = () => {
    if (isSubmitting) return;
    setIsOpen(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedCode = code.trim();
    const trimmedName = name.trim();

    if (!trimmedCode) {
      setError("Vui lòng nhập mã trung tâm (ví dụ: CTY_012).");
      return;
    }
    if (!trimmedName) {
      setError("Vui lòng nhập tên trung tâm.");
      return;
    }

    setError(null);
    setIsSubmitting(true);

    try {
      const parsedEstYear = establishedYear ? parseInt(establishedYear, 10) : undefined;
      const parsedStaff = staffCount ? parseInt(staffCount, 10) : undefined;
      const parsedVisaRate = visaSuccessRate ? parseFloat(visaSuccessRate) : undefined;
      const parsedAccreditations = accreditationsInput
        ? accreditationsInput
            .split(/[,;\n]/)
            .map((s) => s.trim())
            .filter(Boolean)
        : undefined;

      await studyCentersApi.create({
        code: trimmedCode,
        name: trimmedName,
        city: city.trim() || null,
        district: district.trim() || null,
        address: address.trim() || null,
        website: website.trim() || null,
        phone: phone.trim() || null,
        email: email.trim() || null,
        centerType: centerType.trim() || "study_abroad",
        licenseNumber: licenseNumber.trim() || null,
        establishedYear: isNaN(parsedEstYear as number) ? null : parsedEstYear,
        staffCount: isNaN(parsedStaff as number) ? null : parsedStaff,
        visaSuccessRate: isNaN(parsedVisaRate as number) ? null : parsedVisaRate,
        logoUrl: logoUrl.trim() || null,
        accreditations: parsedAccreditations,
        description: description.trim() || null,
        studyLevels: selectedLevels,
        services: selectedServices,
      });

      toast.add({
        type: "success",
        priority: "high",
        timeout: 6000,
        title: "Đã thêm trung tâm tư vấn",
        description: `${trimmedName} (${trimmedCode})`,
      });

      setIsOpen(false);
      router.refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không thể tạo trung tâm tư vấn.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <Button
        type="button"
        size={size}
        onClick={handleOpen}
        className={cn("gap-1.5 bg-brand text-white hover:bg-brand/90", className)}
      >
        <PlusIcon className="size-4" aria-hidden />
        Thêm trung tâm
      </Button>

      <Dialog open={isOpen} onOpenChange={(open) => !open && handleClose()}>
        <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto p-6">
          <DialogHeader>
            <DialogTitle>Thêm trung tâm tư vấn vào danh bạ</DialogTitle>
            <DialogDescription>
              Nhập thông tin chi tiết trung tâm tư vấn du học để phục vụ luồng gợi ý và quản lý danh bạ.
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}


            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field>
                <FieldLabel htmlFor="center-code">Mã trung tâm *</FieldLabel>
                <Input
                  id="center-code"
                  placeholder="Ví dụ: CTY_012, IVY_PREP"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  disabled={isSubmitting}
                  required
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="center-name">Tên trung tâm *</FieldLabel>
                <Input
                  id="center-name"
                  placeholder="Ví dụ: Trung tâm Tư vấn Du học ABC"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  disabled={isSubmitting}
                  required
                />
              </Field>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field>
                <FieldLabel htmlFor="center-type">Mô hình hoạt động</FieldLabel>
                <select
                  id="center-type"
                  value={centerType}
                  onChange={(e) => setCenterType(e.target.value)}
                  disabled={isSubmitting}
                  className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <option value="study_abroad">Tư vấn Du học</option>
                  <option value="test_prep">Luyện thi chuẩn hóa (SAT/IELTS)</option>
                  <option value="hybrid">Toàn diện (Du học & Luyện thi)</option>
                </select>
              </Field>

              <Field>
                <FieldLabel htmlFor="center-logo">Link ảnh Logo (URL)</FieldLabel>
                <Input
                  id="center-logo"
                  placeholder="https://example.com/logo.png"
                  value={logoUrl}
                  onChange={(e) => setLogoUrl(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field>
                <FieldLabel htmlFor="center-city">Tỉnh / Thành phố</FieldLabel>
                <Input
                  id="center-city"
                  placeholder="Ví dụ: Hà Nội, TP.HCM, Đà Nẵng"
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="center-district">Quận / Huyện</FieldLabel>
                <Input
                  id="center-district"
                  placeholder="Ví dụ: Cầu Giấy, Quận 1, Hải Châu..."
                  value={district}
                  onChange={(e) => setDistrict(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            <Field>
              <FieldLabel htmlFor="center-address">Địa chỉ chi tiết</FieldLabel>
              <Input
                id="center-address"
                placeholder="Số nhà, đường, phường/xã..."
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                disabled={isSubmitting}
              />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <Field>
                <FieldLabel htmlFor="center-phone">Số điện thoại</FieldLabel>
                <Input
                  id="center-phone"
                  placeholder="Ví dụ: 0912345678"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="center-email">Email liên hệ</FieldLabel>
                <Input
                  id="center-email"
                  type="email"
                  placeholder="contact@example.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="center-website">Website</FieldLabel>
                <Input
                  id="center-website"
                  placeholder="https://example.com"
                  value={website}
                  onChange={(e) => setWebsite(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            {/* Khối Pháp lý & Năng lực */}
            <div className="rounded-lg border bg-muted/20 p-3.5 space-y-3">
              <div className="text-xs font-semibold text-foregroundr">
                Hồ sơ pháp lý & Năng lực đối soát (Xác minh)
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <Field>
                  <FieldLabel htmlFor="center-license">Số giấy phép Sở GD&ĐT</FieldLabel>
                  <Input
                    id="center-license"
                    placeholder="Ví dụ: GPHĐ-GD-2010/HN-088"
                    value={licenseNumber}
                    onChange={(e) => setLicenseNumber(e.target.value)}
                    disabled={isSubmitting}
                  />
                </Field>

                <Field>
                  <FieldLabel htmlFor="center-year">Năm thành lập</FieldLabel>
                  <Input
                    id="center-year"
                    type="number"
                    placeholder="Ví dụ: 2010"
                    value={establishedYear}
                    onChange={(e) => setEstablishedYear(e.target.value)}
                    disabled={isSubmitting}
                  />
                </Field>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <Field>
                  <FieldLabel htmlFor="center-staff">Quy mô nhân sự (cố vấn)</FieldLabel>
                  <Input
                    id="center-staff"
                    type="number"
                    placeholder="Ví dụ: 35"
                    value={staffCount}
                    onChange={(e) => setStaffCount(e.target.value)}
                    disabled={isSubmitting}
                  />
                </Field>

                <Field>
                  <FieldLabel htmlFor="center-visa">Tỷ lệ đỗ Visa Mỹ (%)</FieldLabel>
                  <Input
                    id="center-visa"
                    type="number"
                    step="0.1"
                    placeholder="Ví dụ: 98.5"
                    value={visaSuccessRate}
                    onChange={(e) => setVisaSuccessRate(e.target.value)}
                    disabled={isSubmitting}
                  />
                </Field>
              </div>

              <Field>
                <FieldLabel htmlFor="center-accreditations">Chứng nhận / Kiểm định quốc tế (ngăn cách bằng dấu phẩy)</FieldLabel>
                <Input
                  id="center-accreditations"
                  placeholder="Ví dụ: AIRC, ICEF, PIER, British Council"
                  value={accreditationsInput}
                  onChange={(e) => setAccreditationsInput(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            <Field>
              <FieldLabel>Bậc học tư vấn</FieldLabel>
              <div className="flex flex-wrap gap-2 pt-1">
                {STUDY_LEVELS.map((lvl) => {
                  const isChecked = selectedLevels.includes(lvl);
                  return (
                    <Button
                      key={lvl}
                      type="button"
                      size="sm"
                      variant={isChecked ? "default" : "outline"}
                      onClick={() => handleToggleLevel(lvl)}
                      disabled={isSubmitting}
                    >
                      {STUDY_LEVEL_LABELS[lvl]}
                    </Button>
                  );
                })}
              </div>
            </Field>

            <Field>
              <FieldLabel>Dịch vụ cung cấp</FieldLabel>
              <div className="flex flex-wrap gap-2 pt-1">
                {COMMON_SERVICES.map((svc) => {
                  const isChecked = selectedServices.includes(svc.key);
                  return (
                    <Button
                      key={svc.key}
                      type="button"
                      size="sm"
                      variant={isChecked ? "default" : "outline"}
                      onClick={() => handleToggleService(svc.key)}
                      disabled={isSubmitting}
                    >
                      {svc.label}
                    </Button>
                  );
                })}
              </div>
            </Field>

            <Field>
              <FieldLabel htmlFor="center-desc">Mô tả ngắn</FieldLabel>
              <Input
                id="center-desc"
                placeholder="Giới thiệu thêm về kinh nghiệm, thế mạnh của trung tâm..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={isSubmitting}
              />
            </Field>

            <DialogFooter className="pt-2 flex flex-col sm:flex-row sm:items-center justify-between gap-2">
              {user?.role === "admin" || user?.role === "moderator" ? (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setIsCreateAccountOpen(true)}
                  disabled={isSubmitting}
                  className="gap-1.5 text-xs text-brand border-brand/30 hover:bg-brand/5 mr-auto"
                >
                  <UserPlusIcon className="size-3.5" />
                  Tạo tài khoản cho trung tâm
                </Button>
              ) : <div />}
              <div className="flex items-center gap-2 self-end sm:self-auto">
                <Button type="button" variant="outline" onClick={handleClose} disabled={isSubmitting}>
                  Hủy
                </Button>
                <Button type="submit" disabled={isSubmitting} className="bg-brand text-white hover:bg-brand/90">
                  {isSubmitting ? <Spinner className="size-4" /> : "Thêm vào danh bạ"}
                </Button>
              </div>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <CreateCenterAccountButton
        hideTriggerButton
        isOpenControlled={isCreateAccountOpen}
        onOpenChangeControlled={setIsCreateAccountOpen}
        initialName=""
        initialEmail={email}
        initialPhone={phone}
      />
    </>
  );
}
