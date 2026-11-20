"use client";

import React, { useEffect, useRef, useState } from "react";
import {
  ShieldCheckIcon,
  CheckCircle2Icon,
  AlertCircleIcon,
  ClockIcon,
  UploadCloudIcon,
  FileTextIcon,
  XIcon,
  InfoIcon
} from "lucide-react";
import { centerPortalApi, ApiError } from "@/lib/api";
import type {
  StudyCenter,
  CenterVerificationStatusDto,
  VerificationDocumentUploadDto,
} from "@/types/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { CenterContactCard } from "@/components/center/CenterContactCard";

export function CenterVerificationView() {
  const [center, setCenter] = useState<StudyCenter | null>(null);
  const [status, setStatus] = useState<CenterVerificationStatusDto | null>(null);
  const [loading, setLoading] = useState(true);

  // Form submission state
  const [legalName, setLegalName] = useState("");
  const [taxCode, setTaxCode] = useState("");
  const [address, setAddress] = useState("");
  const [legalRepresentative, setLegalRepresentative] = useState("");
  const [website, setWebsite] = useState("");

  // Uploaded documents
  const [uploadedDocs, setUploadedDocs] = useState<VerificationDocumentUploadDto[]>([]);
  const [uploadingType, setUploadingType] = useState<string | null>(null);
  const fileInputRefs = useRef<Record<string, HTMLInputElement | null>>({});

  const [formError, setFormError] = useState<string | null>(null);
  const [formSuccess, setFormSuccess] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [dismissedVerified, setDismissedVerified] = useState(false);

  // Tự động xóa thông báo nộp hồ sơ thành công sau 5 giây
  useEffect(() => {
    if (!formSuccess) return;
    const t = setTimeout(() => setFormSuccess(null), 5000);
    return () => clearTimeout(t);
  }, [formSuccess]);

  useEffect(() => {
    // Đọc sau một tick (không setState đồng bộ trong effect); sessionStorage có thể không khả dụng
    void Promise.resolve().then(() => {
      try {
        if (sessionStorage.getItem("center_verified_banner_dismissed") === "true") {
          setDismissedVerified(true);
        }
      } catch {}
    });
  }, []);

  const fetchStatus = async () => {
    try {
      const [c, s] = await Promise.all([
        centerPortalApi.getProfile(),
        centerPortalApi.getVerificationStatus(),
      ]);
      setCenter(c);
      setStatus(s);

      // Pre-fill fields if already in center profile or previous request
      if (s.currentRequest) {
        setLegalName(s.currentRequest.legalName || "");
        setTaxCode(s.currentRequest.taxCode || "");
        setAddress(s.currentRequest.address || "");
        setLegalRepresentative(s.currentRequest.legalRepresentative || "");
        setWebsite(s.currentRequest.website || "");
      } else if (c) {
        setLegalName(c.name || "");
        setTaxCode(c.taxCode || "");
        setAddress(c.address || "");
        setLegalRepresentative(c.legalRepresentative || "");
        setWebsite(c.website || "");
      }
    } catch (e) {
      console.error("Failed to load verification status", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    let active = true;
    queueMicrotask(() => {
      if (active) {
        void fetchStatus();
      }
    });
    return () => {
      active = false;
    };
  }, []);

  // Tự động ẩn banner "đã xác minh" sau 8 giây
  useEffect(() => {
    if (status?.verificationStatus === "verified" && !dismissedVerified) {
      const timer = setTimeout(() => {
        setDismissedVerified(true);
        try {
          sessionStorage.setItem("center_verified_banner_dismissed", "true");
        } catch {}
      }, 8000);
      return () => clearTimeout(timer);
    }
  }, [status, dismissedVerified]);

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>, docType: string) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setFormError(null);
    setUploadingType(docType);
    try {
      const doc = await centerPortalApi.uploadDocument(file, docType);
      setUploadedDocs((prev) => [
        ...prev.filter((d) => d.documentType !== docType),
        doc,
      ]);
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError("Không thể tải lên tệp tài liệu. Vui lòng kiểm tra lại định dạng (.pdf, .jpg, .png) và dung lượng <= 10MB.");
      }
    } finally {
      setUploadingType(null);
      e.target.value = "";
    }
  };

  // [UC-71] Người đăng ký khác người đại diện pháp luật thì bắt buộc giấy ủy quyền
  const normName = (v?: string | null) =>
    (v ?? "").trim().toLowerCase().replace(/đ/g, "d").normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/\s+/g, " ");
  const needsPowerOfAttorney =
    !!center?.registrantName?.trim() && normName(center.registrantName) !== normName(legalRepresentative);

  const handleRemoveDoc = (docType: string) => {
    setUploadedDocs((prev) => prev.filter((d) => d.documentType !== docType));
  };

  const handleSubmitVerification = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    setFormSuccess(null);

    // [UC-91] Phải xác thực cả email và số điện thoại trước khi nộp hồ sơ
    if (!status?.isEmailVerified || !status?.isPhoneVerified) {
      setFormError("Bạn phải xác thực email và số điện thoại bằng mã OTP trước khi nộp hồ sơ.");
      return;
    }

    const hasBizReg = uploadedDocs.some((d) => d.documentType === "business_registration");
    if (!hasBizReg) {
      setFormError("Bản scan hoặc ảnh Giấy chứng nhận đăng ký doanh nghiệp là tài liệu bắt buộc.");
      return;
    }

    if (needsPowerOfAttorney && !uploadedDocs.some((d) => d.documentType === "power_of_attorney")) {
      setFormError("Người đăng ký không phải người đại diện theo pháp luật nên bắt buộc nộp Giấy ủy quyền người thực hiện thủ tục.");
      return;
    }

    setIsSubmitting(true);
    try {
      await centerPortalApi.submitVerification({
        legalName: legalName.trim(),
        taxCode: taxCode.trim(),
        address: address.trim(),
        legalRepresentative: legalRepresentative.trim(),
        website: website.trim() || undefined,
        documents: uploadedDocs,
      });

      setFormSuccess("Hồ sơ xác minh đã được nộp thành công! Moderator sẽ đối chiếu và duyệt trong thời gian sớm nhất.");
      await fetchStatus();
    } catch (err) {
      if (err instanceof ApiError) {
        setFormError(err.message);
      } else {
        setFormError("Nộp hồ sơ không thành công. Vui lòng kiểm tra lại thông tin và thử lại.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center gap-2 text-ink-2 text-sm">
        <Spinner />
        <span>Đang tải thông tin xác minh...</span>
      </div>
    );
  }

  const isVerified = status?.verificationStatus === "verified";
  const isPending = status?.verificationStatus === "pending_review";
  const isRejected = status?.verificationStatus === "rejected";

  return (
    <div className="max-w-4xl mx-auto space-y-8">
      <div>
        <h1 className="text-2xl font-bold text-ink flex items-center gap-2.5">
          <ShieldCheckIcon className="h-7 w-7 text-brand" />
          Xác minh pháp lý trung tâm tư vấn
        </h1>
        <p className="text-sm text-ink-2 mt-1">
          Quy trình KYC & đối chiếu thông tin doanh nghiệp theo Nghị định quản lý dịch vụ tư vấn du học.
        </p>
      </div>

      {/* Banner Current Request Status */}
      {isVerified && !dismissedVerified && (
        <Alert className="relative border-emerald-500/30 bg-emerald-500/5 text-emerald-800 dark:text-emerald-200 pr-9 transition-all duration-300">
          <CheckCircle2Icon className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
          <AlertDescription className="ml-2">
            <strong>Đã xác minh:</strong> Trung tâm của bạn đã hoàn thành đối chiếu pháp lý và được cấp dấu xác minh thông tin đăng ký doanh nghiệp.
          </AlertDescription>
          <Button
            type="button"
            variant="ghost"
            size="icon-xs"
            onClick={() => {
              setDismissedVerified(true);
              try {
                sessionStorage.setItem("center_verified_banner_dismissed", "true");
              } catch {}
            }}
            className="absolute top-2.5 right-2.5 hover:bg-emerald-500/10 dark:hover:bg-emerald-500/10 text-emerald-700 hover:text-emerald-700 dark:text-emerald-300 dark:hover:text-emerald-300"
            title="Đóng thông báo"
            aria-label="Đóng thông báo"
          >
            <XIcon className="h-4 w-4" />
          </Button>
        </Alert>
      )}

      {isPending && (
        <Alert className="border-sky-500/30 bg-sky-500/5 text-sky-900 dark:text-sky-200">
          <ClockIcon className="h-5 w-5 text-sky-600 dark:text-sky-400" />
          <AlertDescription className="ml-2">
            <strong>Hồ sơ đang chờ duyệt:</strong> Moderator đang tiến hành đối chiếu hồ sơ của bạn với Cổng Đăng ký Doanh nghiệp Quốc gia (dangkykinhdoanh.gov.vn). Trong thời gian này thông tin pháp lý được khóa để đảm bảo tính toàn vẹn.
          </AlertDescription>
        </Alert>
      )}

      {isRejected && (
        <Alert variant="destructive">
          <AlertCircleIcon className="h-5 w-5" />
          <AlertDescription className="ml-2">
            <strong>
              {status?.currentRequest?.status === "changes_requested"
                ? "Moderator yêu cầu bổ sung hồ sơ"
                : "Hồ sơ bị từ chối duyệt"}
              {status?.rejectionReason ? ":" : "."}
            </strong>{" "}
            {/* Nội dung phản hồi do moderator nhập, không tự đặt sẵn */}
            {status?.rejectionReason && <span className="whitespace-pre-line">{status.rejectionReason} </span>}
            Vui lòng cập nhật lại thông tin và nộp lại hồ sơ dưới đây.
          </AlertDescription>
        </Alert>
      )}

      {/* Nộp hồ sơ pháp lý (UC-71) */}
      <div className="rounded-xl border border-line bg-surface p-6 space-y-6">
        <div className="flex items-center justify-between border-b border-line pb-4">
          <div>
            <h2 className="text-base font-semibold text-ink">
              Nộp hồ sơ pháp lý & Giấy tờ doanh nghiệp
            </h2>
            <p className="text-xs text-ink-3 mt-0.5">
              Thông tin sẽ được đối chiếu trực tiếp với Cổng thông tin Quốc gia về đăng ký doanh nghiệp.
            </p>
          </div>
          <span className="text-xs text-ink-3 font-mono">UC-71</span>
        </div>

        {formSuccess && (
          <Alert className="relative border-emerald-500/30 bg-emerald-500/5 text-emerald-800 dark:text-emerald-200 pr-9 transition-all duration-300">
            <CheckCircle2Icon className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
            <AlertDescription className="ml-2">{formSuccess}</AlertDescription>
            <Button
              type="button"
              variant="ghost"
              size="icon-xs"
              onClick={() => setFormSuccess(null)}
              className="absolute top-2.5 right-2.5 hover:bg-emerald-500/10 dark:hover:bg-emerald-500/10 text-emerald-700 hover:text-emerald-700 dark:text-emerald-300 dark:hover:text-emerald-300"
              title="Đóng thông báo"
              aria-label="Đóng thông báo"
            >
              <XIcon className="h-4 w-4" />
            </Button>
          </Alert>
        )}

        {formError && (
          <Alert variant="destructive">
            <AlertCircleIcon className="h-5 w-5" />
            <AlertDescription className="ml-2">{formError}</AlertDescription>
          </Alert>
        )}

        {/* [UC-91] Chưa xác thực đủ email và số điện thoại thì xác thực ngay tại đây trước khi nộp */}
        {status && (!status.isEmailVerified || !status.isPhoneVerified) && (
          <div className="space-y-3">
            <Alert>
              <InfoIcon className="h-5 w-5" />
              <AlertDescription className="ml-2">
                Cần xác thực cả email và số điện thoại bằng mã OTP trước khi nộp hồ sơ. Việc xác thực không tự cấp dấu xác minh.
              </AlertDescription>
            </Alert>
            <CenterContactCard
              phone={center?.phone}
              email={center?.email}
              emailVerified={status.isEmailVerified}
              phoneVerified={status.isPhoneVerified}
              onChanged={() => {
                void fetchStatus();
              }}
            />
          </div>
        )}

        <form onSubmit={handleSubmitVerification} className="space-y-6">
          <FieldGroup>
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field>
                <FieldLabel htmlFor="legalName">Tên pháp lý theo ĐKKD (*)</FieldLabel>
                <Input
                  id="legalName"
                  required
                  maxLength={256}
                  disabled={isPending || isVerified}
                  value={legalName}
                  onChange={(e) => setLegalName(e.target.value)}
                  placeholder="Công ty TNHH Tư vấn Du học..."
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="taxCode">Mã số doanh nghiệp / Mã số thuế (*)</FieldLabel>
                <Input
                  id="taxCode"
                  required
                  maxLength={32}
                  disabled={isPending || isVerified}
                  value={taxCode}
                  onChange={(e) => setTaxCode(e.target.value)}
                  placeholder="0101234567"
                />
                <FieldDescription>Gồm 10 hoặc 13 chữ số, duy nhất trên toàn hệ thống (BR-C4)</FieldDescription>
              </Field>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <Field>
                <FieldLabel htmlFor="legalRep">Người đại diện theo pháp luật (*)</FieldLabel>
                <Input
                  id="legalRep"
                  required
                  maxLength={150}
                  disabled={isPending || isVerified}
                  value={legalRepresentative}
                  onChange={(e) => setLegalRepresentative(e.target.value)}
                  placeholder="Họ tên Giám đốc / Đại diện pháp luật"
                />
              </Field>

              <Field>
                <FieldLabel htmlFor="website">Website hoặc Fanpage chính thức</FieldLabel>
                <Input
                  id="website"
                  type="url"
                  maxLength={512}
                  disabled={isPending || isVerified}
                  value={website}
                  onChange={(e) => setWebsite(e.target.value)}
                  placeholder="https://center.edu.vn"
                />
              </Field>
            </div>

            <Field>
              <FieldLabel htmlFor="address">Địa chỉ trụ sở chính theo ĐKKD (*)</FieldLabel>
              <Input
                id="address"
                required
                maxLength={512}
                disabled={isPending || isVerified}
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành phố"
              />
            </Field>

            {/* Document Uploads Section */}
            <div className="space-y-4 pt-4 border-t border-line">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-sm font-semibold text-ink">
                    Tài liệu minh chứng đính kèm
                  </h3>
                  <p className="text-xs text-ink-3">
                    Định dạng chấp nhận: .pdf, .jpg, .jpeg, .png (Tối đa 10MB mỗi tệp)
                  </p>
                </div>
              </div>

              {/* Upload Item: Business Registration */}
              <div className="rounded-lg border border-line bg-surface-2 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <FileTextIcon className="h-4 w-4 text-brand" />
                    <span className="text-sm font-medium text-ink">
                      Giấy chứng nhận đăng ký doanh nghiệp (ĐKKD) (*)
                    </span>
                    <Badge variant="outline" className="text-[10px] text-brand border-brand/30">
                      Bắt buộc
                    </Badge>
                  </div>
                  <p className="text-xs text-ink-3">
                    Ảnh chụp hoặc bản scan rõ nét thể hiện tên, MST, địa chỉ và người đại diện.
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  {uploadedDocs.find((d) => d.documentType === "business_registration") ? (
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-emerald-600 truncate max-w-[150px]">
                        ✓ {uploadedDocs.find((d) => d.documentType === "business_registration")?.fileName}
                      </span>
                      {!isPending && !isVerified && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => handleRemoveDoc("business_registration")}
                          className="text-ink-3 hover:text-rose-600"
                          aria-label="Xóa tệp đã tải lên"
                          title="Xóa tệp"
                        >
                          <XIcon className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  ) : (
                    <>
                      <input
                        ref={(el) => {
                          fileInputRefs.current["business_registration"] = el;
                        }}
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png"
                        disabled={isPending || isVerified || uploadingType === "business_registration"}
                        onChange={(e) => handleFileUpload(e, "business_registration")}
                        className="hidden"
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        loading={uploadingType === "business_registration"}
                        disabled={isPending || isVerified}
                        onClick={() => fileInputRefs.current["business_registration"]?.click()}
                        className="border-line bg-surface text-xs font-medium text-ink hover:bg-surface-3 hover:text-ink dark:border-line dark:bg-surface dark:hover:bg-surface-3"
                      >
                        <UploadCloudIcon className="h-4 w-4 text-ink-2" />
                        Tải tệp lên
                      </Button>
                    </>
                  )}
                </div>
              </div>

              {/* Upload Item: Power of Attorney */}
              <div className="rounded-lg border border-line bg-surface-2 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <FileTextIcon className="h-4 w-4 text-ink-2" />
                    <span className="text-sm font-medium text-ink">
                      Giấy ủy quyền người thực hiện thủ tục
                    </span>
                    <span className="text-[10px] text-ink-3">{needsPowerOfAttorney ? "(Bắt buộc)" : "(Tùy chọn)"}</span>
                  </div>
                  <p className="text-xs text-ink-3">
                    Nộp nếu người đăng ký tài khoản không phải người đại diện theo pháp luật trên ĐKKD.
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  {uploadedDocs.find((d) => d.documentType === "power_of_attorney") ? (
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-emerald-600 truncate max-w-[150px]">
                        ✓ {uploadedDocs.find((d) => d.documentType === "power_of_attorney")?.fileName}
                      </span>
                      {!isPending && !isVerified && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => handleRemoveDoc("power_of_attorney")}
                          className="text-ink-3 hover:text-rose-600"
                          aria-label="Xóa tệp đã tải lên"
                          title="Xóa tệp"
                        >
                          <XIcon className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  ) : (
                    <>
                      <input
                        ref={(el) => {
                          fileInputRefs.current["power_of_attorney"] = el;
                        }}
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png"
                        disabled={isPending || isVerified || uploadingType === "power_of_attorney"}
                        onChange={(e) => handleFileUpload(e, "power_of_attorney")}
                        className="hidden"
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        loading={uploadingType === "power_of_attorney"}
                        disabled={isPending || isVerified}
                        onClick={() => fileInputRefs.current["power_of_attorney"]?.click()}
                        className="border-line bg-surface text-xs font-medium text-ink hover:bg-surface-3 hover:text-ink dark:border-line dark:bg-surface dark:hover:bg-surface-3"
                      >
                        <UploadCloudIcon className="h-4 w-4 text-ink-2" />
                        Tải tệp lên
                      </Button>
                    </>
                  )}
                </div>
              </div>

              {/* Upload Item: Partnership Contract */}
              <div className="rounded-lg border border-line bg-surface-2 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <FileTextIcon className="h-4 w-4 text-ink-2" />
                    <span className="text-sm font-medium text-ink">
                      Hợp đồng đại lý / Thư xác nhận đối tác trường
                    </span>
                    <span className="text-[10px] text-ink-3">(Tùy chọn)</span>
                  </div>
                  <p className="text-xs text-ink-3">
                    Bắt buộc nếu trung tâm muốn hiển thị chứng nhận là đối tác chính thức của các đại học Mỹ (BR-C8).
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  {uploadedDocs.find((d) => d.documentType === "partnership_contract") ? (
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-emerald-600 truncate max-w-[150px]">
                        ✓ {uploadedDocs.find((d) => d.documentType === "partnership_contract")?.fileName}
                      </span>
                      {!isPending && !isVerified && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => handleRemoveDoc("partnership_contract")}
                          className="text-ink-3 hover:text-rose-600"
                          aria-label="Xóa tệp đã tải lên"
                          title="Xóa tệp"
                        >
                          <XIcon className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  ) : (
                    <>
                      <input
                        ref={(el) => {
                          fileInputRefs.current["partnership_contract"] = el;
                        }}
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png"
                        disabled={isPending || isVerified || uploadingType === "partnership_contract"}
                        onChange={(e) => handleFileUpload(e, "partnership_contract")}
                        className="hidden"
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        loading={uploadingType === "partnership_contract"}
                        disabled={isPending || isVerified}
                        onClick={() => fileInputRefs.current["partnership_contract"]?.click()}
                        className="border-line bg-surface text-xs font-medium text-ink hover:bg-surface-3 hover:text-ink dark:border-line dark:bg-surface dark:hover:bg-surface-3"
                      >
                        <UploadCloudIcon className="h-4 w-4 text-ink-2" />
                        Tải tệp lên
                      </Button>
                    </>
                  )}
                </div>
              </div>

              {/* Upload Item: Consulting License */}
              <div className="rounded-lg border border-line bg-surface-2 p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <FileTextIcon className="h-4 w-4 text-ink-2" />
                    <span className="text-sm font-medium text-ink">
                      Giấy chứng nhận hoạt động tư vấn du học (cũ)
                    </span>
                    <span className="text-[10px] text-ink-3">(Tùy chọn)</span>
                  </div>
                  <p className="text-xs text-ink-3">
                    Giấy phép đã được Sở GD&amp;ĐT cấp trước đây (BR-C7).
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  {uploadedDocs.find((d) => d.documentType === "consulting_license") ? (
                    <div className="flex items-center gap-2">
                      <span className="text-xs font-medium text-emerald-600 truncate max-w-[150px]">
                        ✓ {uploadedDocs.find((d) => d.documentType === "consulting_license")?.fileName}
                      </span>
                      {!isPending && !isVerified && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon-xs"
                          onClick={() => handleRemoveDoc("consulting_license")}
                          className="text-ink-3 hover:text-rose-600"
                          aria-label="Xóa tệp đã tải lên"
                          title="Xóa tệp"
                        >
                          <XIcon className="h-4 w-4" />
                        </Button>
                      )}
                    </div>
                  ) : (
                    <>
                      <input
                        ref={(el) => {
                          fileInputRefs.current["consulting_license"] = el;
                        }}
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png"
                        disabled={isPending || isVerified || uploadingType === "consulting_license"}
                        onChange={(e) => handleFileUpload(e, "consulting_license")}
                        className="hidden"
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        loading={uploadingType === "consulting_license"}
                        disabled={isPending || isVerified}
                        onClick={() => fileInputRefs.current["consulting_license"]?.click()}
                        className="border-line bg-surface text-xs font-medium text-ink hover:bg-surface-3 hover:text-ink dark:border-line dark:bg-surface dark:hover:bg-surface-3"
                      >
                        <UploadCloudIcon className="h-4 w-4 text-ink-2" />
                        Tải tệp lên
                      </Button>
                    </>
                  )}
                </div>
              </div>
            </div>

            {/* Privacy note per BR-C6 */}
            <div className="rounded-lg border border-line bg-surface-2 p-3 text-xs text-ink-3 flex items-start gap-2">
              <InfoIcon className="h-4 w-4 text-brand shrink-0 mt-0.5" />
              <span>
                <strong>Bảo mật thông tin (BR-C6 &amp; BR-C5):</strong> Hệ thống không yêu cầu bản sao CCCD của người đại diện. Mọi tệp tài liệu tải lên được mã hóa lưu trữ riêng biệt, chỉ Moderator được phân công mới có quyền mở xem và mọi lượt truy cập đều được ghi nhật ký kiểm toán.
              </span>
            </div>

            {!isPending && !isVerified && (
              <Button
                type="submit"
                size="lg"
                loading={isSubmitting}
                disabled={!status?.isEmailVerified || !status?.isPhoneVerified}
                className="w-full sm:w-auto"
              >
                Nộp hồ sơ xác minh pháp lý
              </Button>
            )}
          </FieldGroup>
        </form>
      </div>
    </div>
  );
}
