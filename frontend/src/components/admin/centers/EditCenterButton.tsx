"use client";

import React, { useState } from "react";
import { PencilIcon } from "lucide-react";
import { studyCentersApi, ApiError } from "@/lib/api";
import { STUDY_LEVELS, STUDY_LEVEL_LABELS } from "@/lib/study-levels";
import { useAuth } from "@/contexts/AuthContext";
import type { StudyCenter } from "@/types/api";
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
import { CenterLegalFieldsSection } from "@/components/admin/centers/CenterLegalFieldsSection";
import {
  createInitialLegalFormValues,
  parseAndValidateLegalFormValues,
  type LegalFormValues,
} from "@/lib/center-legal";
import { cn } from "@/lib/utils";

const COMMON_SERVICES = [
  { key: "CHON_TRUONG", label: "Tư vấn chọn trường" },
  { key: "HO_SO", label: "Làm hồ sơ du học" },
  { key: "LUYEN_PHONG_VAN", label: "Luyện phỏng vấn visa" },
  { key: "HOC_BONG", label: "Hỗ trợ săn học bổng" },
  { key: "VIET_LUAN", label: "Hướng dẫn viết luận" },
  { key: "CHUNG_CHI", label: "Luyện thi chứng chỉ (SAT, IELTS...)" },
];

export interface EditCenterButtonProps {
  center: StudyCenter;
  onSuccess?: (updated: StudyCenter) => void;
  className?: string;
  size?: "default" | "sm" | "lg" | "xs" | "icon" | "icon-xs" | "icon-sm" | "icon-lg";
  variant?: "default" | "outline" | "secondary" | "ghost" | "link";
  children?: React.ReactNode;
}

export function EditCenterButton({
  center,
  onSuccess,
  className,
  size = "sm",
  variant = "outline",
  children,
}: EditCenterButtonProps) {
  const { user } = useAuth();
  const [isOpen, setIsOpen] = useState(false);

  const [name, setName] = useState(center.name);
  const [city, setCity] = useState(center.city || "");
  const [district, setDistrict] = useState(center.district || "");
  const [address, setAddress] = useState(center.address || "");
  const [website, setWebsite] = useState(center.website || "");
  const [phone, setPhone] = useState(center.phone || "");
  const [email, setEmail] = useState(center.email || "");
  const [centerType, setCenterType] = useState(center.centerType || "study_abroad");
  const [logoUrl, setLogoUrl] = useState(center.logoUrl || "");
  const [coverImageUrl, setCoverImageUrl] = useState(center.coverImageUrl || "");
  const [legalValues, setLegalValues] = useState<LegalFormValues>(() =>
    createInitialLegalFormValues(center)
  );
  const [description, setDescription] = useState(center.description || "");
  const [selectedLevels, setSelectedLevels] = useState<string[]>(center.studyLevels || []);
  const [selectedServices, setSelectedServices] = useState<string[]>(center.services || []);

  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Chỉ Moderator hoặc Admin mới được sửa trung tâm
  const canEdit = user?.role === "admin" || user?.role === "moderator";
  if (!canEdit) return null;

  const handleOpen = () => {
    setName(center.name);
    setCity(center.city || "");
    setDistrict(center.district || "");
    setAddress(center.address || "");
    setWebsite(center.website || "");
    setPhone(center.phone || "");
    setEmail(center.email || "");
    setCenterType(center.centerType || "study_abroad");
    setLogoUrl(center.logoUrl || "");
    setCoverImageUrl(center.coverImageUrl || "");
    setLegalValues(createInitialLegalFormValues(center));
    setDescription(center.description || "");
    setSelectedLevels(center.studyLevels || []);
    setSelectedServices(center.services || []);
    setError(null);
    setIsOpen(true);
  };

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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      setError("Vui lòng nhập tên trung tâm.");
      return;
    }

    const { payload: legalPayload, error: legalError } =
      parseAndValidateLegalFormValues(legalValues);
    if (legalError || !legalPayload) {
      setError(legalError || "Thông tin pháp lý không hợp lệ.");
      return;
    }

    setError(null);
    setIsSubmitting(true);

    try {
      const updated = await studyCentersApi.update(center.id, {
        name: trimmedName,
        city: city.trim() || null,
        district: district.trim() || null,
        address: address.trim() || null,
        website: website.trim() || null,
        phone: phone.trim() || null,
        email: email.trim() || null,
        centerType: centerType.trim() || "study_abroad",
        logoUrl: logoUrl.trim() || null,
        coverImageUrl: coverImageUrl.trim() || null,
        ...legalPayload,
        description: description.trim() || null,
        studyLevels: selectedLevels,
        services: selectedServices,
      });

      toast.add({
        type: "success",
        title: "Đã cập nhật thông tin trung tâm",
        description: updated.name,
      });

      setIsOpen(false);
      onSuccess?.(updated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không thể cập nhật trung tâm tư vấn.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      <Button
        type="button"
        size={size}
        variant={variant}
        onClick={handleOpen}
        className={cn("gap-1.5", className)}
      >
        {children ?? (
          <>
            <PencilIcon className="size-3.5" />
            <span>Sửa thông tin</span>
          </>
        )}
      </Button>

      <Dialog open={isOpen} onOpenChange={(open) => !open && !isSubmitting && setIsOpen(false)}>
        <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto p-6">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <PencilIcon className="size-5 text-brand" />
              <span>Chỉnh sửa thông tin trung tâm tư vấn</span>
            </DialogTitle>
            <DialogDescription>
              Cập nhật thông tin nhận diện, pháp lý, tỷ lệ visa và phạm vi dịch vụ của trung tâm ({center.code}).
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
                <FieldLabel htmlFor="edit-center-code">Mã trung tâm (khảo sát)</FieldLabel>
                <Input
                  id="edit-center-code"
                  value={center.code}
                  disabled
                  className="bg-muted cursor-not-allowed font-mono text-xs"
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="edit-center-name">Tên trung tâm *</FieldLabel>
                <Input
                  id="edit-center-name"
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
                <FieldLabel htmlFor="edit-center-type">Mô hình hoạt động</FieldLabel>
                <select
                  id="edit-center-type"
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
                <FieldLabel htmlFor="edit-center-logo">Link ảnh Logo (URL)</FieldLabel>
                <Input
                  id="edit-center-logo"
                  placeholder="https://example.com/logo.png"
                  value={logoUrl}
                  onChange={(e) => setLogoUrl(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <Field>
                <FieldLabel htmlFor="edit-center-city">Tỉnh / Thành phố</FieldLabel>
                <Input
                  id="edit-center-city"
                  placeholder="Ví dụ: Hà Nội, TP.HCM, Đà Nẵng"
                  value={city}
                  onChange={(e) => setCity(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="edit-center-district">Quận / Huyện</FieldLabel>
                <Input
                  id="edit-center-district"
                  placeholder="Ví dụ: Cầu Giấy, Quận 1, Hải Châu..."
                  value={district}
                  onChange={(e) => setDistrict(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            <Field>
              <FieldLabel htmlFor="edit-center-address">Địa chỉ chi tiết</FieldLabel>
              <Input
                id="edit-center-address"
                placeholder="Số nhà, đường, phường/xã..."
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                disabled={isSubmitting}
              />
            </Field>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <Field>
                <FieldLabel htmlFor="edit-center-phone">Số điện thoại</FieldLabel>
                <Input
                  id="edit-center-phone"
                  placeholder="Ví dụ: 0912345678"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="edit-center-email">Email liên hệ</FieldLabel>
                <Input
                  id="edit-center-email"
                  type="email"
                  placeholder="contact@example.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="edit-center-website">Website</FieldLabel>
                <Input
                  id="edit-center-website"
                  placeholder="https://example.com"
                  value={website}
                  onChange={(e) => setWebsite(e.target.value)}
                  disabled={isSubmitting}
                />
              </Field>
            </div>

            {/* Khối Pháp lý & Năng lực */}
            <CenterLegalFieldsSection
              idPrefix="edit-center"
              centerType={centerType}
              values={legalValues}
              onChange={(patch) => setLegalValues((prev) => ({ ...prev, ...patch }))}
              disabled={isSubmitting}
            />

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
              <FieldLabel htmlFor="edit-center-desc">Mô tả ngắn</FieldLabel>
              <Input
                id="edit-center-desc"
                placeholder="Giới thiệu thêm về kinh nghiệm, thế mạnh của trung tâm..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                disabled={isSubmitting}
              />
            </Field>

            <DialogFooter className="pt-2 flex items-center justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => setIsOpen(false)} disabled={isSubmitting}>
                Hủy
              </Button>
              <Button type="submit" disabled={isSubmitting} className="bg-brand text-white hover:bg-brand/90">
                {isSubmitting ? <Spinner className="size-4 mr-1.5" /> : null}
                Lưu thay đổi
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
