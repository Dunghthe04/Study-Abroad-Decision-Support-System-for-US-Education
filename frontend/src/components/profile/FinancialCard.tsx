"use client";

// [USAS-364] Component khai báo thông tin tài chính du học với inline validation

import { useState, useEffect } from "react";
import { getFinancialProfile, saveFinancialProfile } from "@/lib/profile-api";
import type { FinancialProfileDto } from "@/types/profile";

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
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="animate-pulse space-y-4">
          <div className="h-6 w-1/3 rounded bg-slate-200"></div>
          <div className="h-10 w-full rounded bg-slate-100"></div>
          <div className="h-10 w-full rounded bg-slate-100"></div>
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="mb-4 border-b border-slate-100 pb-3">
        <h2 className="text-lg font-semibold text-slate-900">1. Khả năng Tài chính Du học</h2>
        <p className="text-xs text-slate-500">
          Thông tin giúp hệ thống AI so sánh chi phí trường và gợi ý gói học bổng phù hợp (USAS-364).
        </p>
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-rose-50 p-3 text-sm text-rose-700 border border-rose-200">
          {error}
        </div>
      )}

      {success && (
        <div className="mb-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-700 border border-emerald-200">
          {success}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-slate-700">
            Ngân sách gia đình có thể chi trả mỗi năm (USD/năm) *
          </label>
          <div className="relative mt-1">
            <span className="absolute inset-y-0 left-0 flex items-center pl-3 text-slate-400 font-semibold">$</span>
            <input
              type="number"
              min="0"
              max="10000000"
              step="500"
              required
              value={budget}
              onChange={(e) => setBudget(e.target.value)}
              placeholder="30000"
              className="w-full rounded-lg border border-slate-300 py-2 pl-8 pr-4 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <span className="text-xs text-slate-500 mt-1 block">
            Ví dụ: $20,000 – $40,000 (Trung bình các trường ĐH công lập Mỹ khoảng $25,000–$45,000/năm gồm học phí và ăn ở).
          </span>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700">
            Nguồn tài chính chính *
          </label>
          <select
            value={fundingSource}
            onChange={(e) => setFundingSource(e.target.value)}
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
          >
            {FUNDING_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        <div className="flex items-center gap-3 pt-1">
          <input
            type="checkbox"
            id="needScholarship"
            checked={needScholarship}
            onChange={(e) => setNeedScholarship(e.target.checked)}
            className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
          />
          <label htmlFor="needScholarship" className="text-sm font-medium text-slate-800">
            Cần học bổng hoặc hỗ trợ tài chính để đủ điều kiện du học
          </label>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700">
            Học phí mong muốn tối đa (USD/năm - tùy chọn)
          </label>
          <div className="relative mt-1">
            <span className="absolute inset-y-0 left-0 flex items-center pl-3 text-slate-400 font-semibold">$</span>
            <input
              type="number"
              min="0"
              max="10000000"
              step="500"
              value={maxTuition}
              onChange={(e) => setMaxTuition(e.target.value)}
              placeholder="20000"
              className="w-full rounded-lg border border-slate-300 py-2 pl-8 pr-4 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-slate-700">
            Ghi chú thêm về hoàn cảnh tài chính
          </label>
          <textarea
            rows={2}
            value={notes}
            maxLength={1000}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Ví dụ: Có thể chứng minh sổ tiết kiệm 1 tỷ đồng, có người thân bảo lãnh tại bang California..."
            className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 text-sm"
          />
        </div>

        <div className="pt-2 flex justify-end">
          <button
            type="submit"
            disabled={saving}
            className="rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white shadow-xs hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {saving ? "Đang lưu..." : profile ? "Cập nhật hồ sơ tài chính" : "Lưu hồ sơ tài chính"}
          </button>
        </div>
      </form>
    </div>
  );
}
