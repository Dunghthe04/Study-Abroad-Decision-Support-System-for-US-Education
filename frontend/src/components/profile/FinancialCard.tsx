"use client";

// [USAS-364] Component khai báo thông tin tài chính du học với inline validation

import { useState, useEffect } from "react";
import { getFinancialProfile, saveFinancialProfile } from "@/lib/profile-api";
import type { FinancialProfileDto } from "@/types/profile";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { InputGroup, InputGroupAddon, InputGroupInput, InputGroupText } from "@/components/ui/input-group";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";

const FUNDING_OPTIONS = [
  { value: "family_support", label: "Gia đình hỗ trợ toàn bộ/một phần" },
  { value: "personal_savings", label: "Tiết kiệm cá nhân" },
  { value: "bank_loan", label: "Vay vốn du học ngân hàng" },
  { value: "scholarship", label: "Cần học bổng để trang trải chi phí" },
  { value: "other", label: "Nguồn khác" },
];

export function FinancialCard() {
  const [profile, setProfile] = useState<FinancialProfileDto | null>(null);
  const [budget, setBudget] = useState<string>("30000");
  const [fundingSource, setFundingSource] = useState<string>("family_support");
  const [needScholarship, setNeedScholarship] = useState<boolean>(true);
  const [maxTuition, setMaxTuition] = useState<string>("");
  const [notes, setNotes] = useState<string>("");

  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        setLoading(true);
        const data = await getFinancialProfile();
        if (data) {
          setProfile(data);
          setBudget(data.annualBudget.toString());
          setFundingSource(data.fundingSource);
          setNeedScholarship(data.needScholarship);
          setMaxTuition(data.maxExpectedTuition ? data.maxExpectedTuition.toString() : "");
          setNotes(data.notes || "");
        }
      } catch {
        // Có thể chưa đăng nhập hoặc chưa có profile
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    const budgetNum = parseFloat(budget);
    if (isNaN(budgetNum) || budgetNum < 0) {
      setError("Ngân sách hàng năm phải là số lớn hơn hoặc bằng 0.");
      return;
    }

    if (budgetNum > 10000000) {
      setError("Ngân sách hàng năm không vượt quá 10,000,000 USD.");
      return;
    }

    if (!fundingSource) {
      setError("Vui lòng chọn nguồn tài chính dự kiến.");
      return;
    }

    let maxTuitionNum: number | null = null;
    if (maxTuition.trim() !== "") {
      maxTuitionNum = parseFloat(maxTuition);
      if (isNaN(maxTuitionNum) || maxTuitionNum < 0) {
        setError("Học phí mong muốn tối đa phải là số không âm.");
        return;
      }
    }

    try {
      setSaving(true);
      const updated = await saveFinancialProfile({
        annualBudget: budgetNum,
        fundingSource,
        needScholarship,
        maxExpectedTuition: maxTuitionNum,
        currency: "USD",
        notes: notes.trim() || null,
      });
      setProfile(updated);
      setSuccess("Đã lưu thông tin tài chính thành công!");
      setTimeout(() => setSuccess(null), 4000);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Có lỗi xảy ra khi lưu thông tin tài chính.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <Card>
        <CardContent className="space-y-4">
          <Skeleton className="h-6 w-1/3" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>1. Khả năng Tài chính Du học</CardTitle>
        <CardDescription>
          Thông tin giúp hệ thống AI so sánh chi phí trường và gợi ý gói học bổng phù hợp (USAS-364).
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {success && (
          <Alert variant="success">
            <AlertDescription>{success}</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="financial-budget">
              Ngân sách gia đình có thể chi trả mỗi năm (USD/năm) *
            </Label>
            <InputGroup>
              <InputGroupAddon>
                <InputGroupText>$</InputGroupText>
              </InputGroupAddon>
              <InputGroupInput
                id="financial-budget"
                type="number"
                min="0"
                max="10000000"
                step="500"
                required
                value={budget}
                onChange={(e) => setBudget(e.target.value)}
                placeholder="30000"
              />
            </InputGroup>
            <p className="text-body-s">
              Ví dụ: $20,000 – $40,000 (Trung bình các trường ĐH công lập Mỹ khoảng $25,000–$45,000/năm gồm học phí và ăn ở).
            </p>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="financial-funding-source">
              Nguồn tài chính chính *
            </Label>
            <NativeSelect
              id="financial-funding-source"
              className="w-full"
              value={fundingSource}
              onChange={(e) => setFundingSource(e.target.value)}
            >
              {FUNDING_OPTIONS.map((opt) => (
                <NativeSelectOption key={opt.value} value={opt.value}>
                  {opt.label}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>

          <div className="flex items-center gap-3">
            <Checkbox
              id="needScholarship"
              checked={needScholarship}
              onCheckedChange={(checked) => setNeedScholarship(checked)}
            />
            <Label htmlFor="needScholarship">
              Cần học bổng hoặc hỗ trợ tài chính để đủ điều kiện du học
            </Label>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="financial-max-tuition">
              Học phí mong muốn tối đa (USD/năm - tùy chọn)
            </Label>
            <InputGroup>
              <InputGroupAddon>
                <InputGroupText>$</InputGroupText>
              </InputGroupAddon>
              <InputGroupInput
                id="financial-max-tuition"
                type="number"
                min="0"
                max="10000000"
                step="500"
                value={maxTuition}
                onChange={(e) => setMaxTuition(e.target.value)}
                placeholder="20000"
              />
            </InputGroup>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="financial-notes">
              Ghi chú thêm về hoàn cảnh tài chính
            </Label>
            <Textarea
              id="financial-notes"
              rows={2}
              value={notes}
              maxLength={1000}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Ví dụ: Có thể chứng minh sổ tiết kiệm 1 tỷ đồng, có người thân bảo lãnh tại bang California..."
            />
          </div>

          <div className="flex justify-end pt-2">
            <Button type="submit" size="lg" disabled={saving}>
              {saving ? "Đang lưu..." : profile ? "Cập nhật hồ sơ tài chính" : "Lưu hồ sơ tài chính"}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
