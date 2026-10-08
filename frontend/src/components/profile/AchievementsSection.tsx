"use client";

// [USAS-364] Component quản lý Giải thưởng, Nghiên cứu khoa học và Chứng chỉ

import { useState, useEffect } from "react";
import { getAchievements, addAchievement, deleteAchievement } from "@/lib/profile-api";
import type { StudentAchievementDto, CreateAchievementRequest } from "@/types/profile";

const CATEGORY_MAP: Record<string, { label: string; badge: string }> = {
  award: { label: "Giải thưởng / Học sinh giỏi", badge: "bg-amber-50 text-amber-800 border-amber-200" },
  research: { label: "Nghiên cứu khoa học", badge: "bg-cyan-50 text-cyan-800 border-cyan-200" },
  internship: { label: "Thực tập / Dự án thực tế", badge: "bg-emerald-50 text-emerald-800 border-emerald-200" },
  certification: { label: "Chứng chỉ chuyên môn", badge: "bg-blue-50 text-blue-800 border-blue-200" },
  other: { label: "Khác", badge: "bg-slate-100 text-slate-700 border-slate-200" },
};

export function AchievementsSection() {
  const [achievements, setAchievements] = useState<StudentAchievementDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showAddForm, setShowAddForm] = useState(false);

  const [category, setCategory] = useState("award");
  const [title, setTitle] = useState("");
  const [issuer, setIssuer] = useState("");
  const [issueDate, setIssueDate] = useState("");
  const [description, setDescription] = useState("");

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      try {
        const data = await getAchievements();
        setAchievements(data);
      } catch {
        // ignore
      } finally {
        setLoading(false);
      }
    }
    load();
  }, []);

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!title.trim()) {
      setError("Vui lòng nhập tiêu đề giải thưởng hoặc đề tài.");
      return;
    }

    try {
      setSaving(true);
      const req: CreateAchievementRequest = {
        category,
        title: title.trim(),
        issuer: issuer.trim() || undefined,
        issueDate: issueDate.trim() || undefined,
        description: description.trim() || undefined,
      };

      const created = await addAchievement(req);
      setAchievements([created, ...achievements]);

      setTitle("");
      setIssuer("");
      setIssueDate("");
      setDescription("");
      setShowAddForm(false);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Có lỗi xảy ra khi thêm thành tích.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm("Bạn có chắc chắn muốn xóa thành tích này không?")) return;
    try {
      await deleteAchievement(id);
      setAchievements(achievements.filter((a) => a.id !== id));
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : "Không thể xóa thành tích.");
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-4">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">Nghiên cứu, thực tập & giải thưởng</h2>
          <p className="text-xs text-slate-500">
            Minh chứng năng lực học thuật xuất sắc và hồ sơ nổi bật (Honors & Awards).
          </p>
        </div>
        <button
          onClick={() => setShowAddForm(!showAddForm)}
          className="rounded-lg bg-slate-900 px-3.5 py-1.5 text-xs font-medium text-white hover:bg-slate-800 transition-colors"
        >
          {showAddForm ? "Đóng Form" : "+ Thêm thành tích"}
        </button>
      </div>

      {showAddForm && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-xs p-4"
          onClick={(e) => { if (e.target === e.currentTarget) setShowAddForm(false); }}
        >
          <div className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
            <form onSubmit={handleAdd} className="space-y-4">
              <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                <h3 className="text-base font-semibold text-slate-900">Khai báo thành tích mới</h3>
                <button
                  type="button"
                  onClick={() => setShowAddForm(false)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 transition-colors"
                >
                  <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
                </button>
              </div>

              {error && <div className="rounded-lg bg-rose-50 border border-rose-200 p-2.5 text-xs text-rose-600">{error}</div>}

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">Phân loại *</label>
                  <select
                    value={category}
                    onChange={(e) => setCategory(e.target.value)}
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  >
                    <option value="award">Giải thưởng / Học sinh giỏi</option>
                    <option value="research">Nghiên cứu khoa học / Bài báo</option>
                    <option value="internship">Thực tập / Dự án thực tế</option>
                    <option value="certification">Chứng chỉ chuyên môn</option>
                    <option value="other">Khác</option>
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-700">Tên thành tích / Đề tài *</label>
                  <input
                    type="text"
                    required
                    maxLength={150}
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="VD: Giải Nhất HSG Toán Cấp Tỉnh"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-700">Đơn vị cấp / Nơi thực tập</label>
                  <input
                    type="text"
                    maxLength={150}
                    value={issuer}
                    onChange={(e) => setIssuer(e.target.value)}
                    placeholder="VD: Sở GD&ĐT / Viện Nghiên cứu"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-slate-700">Thời gian đạt được (Năm/Tháng)</label>
                  <input
                    type="text"
                    maxLength={20}
                    value={issueDate}
                    onChange={(e) => setIssueDate(e.target.value)}
                    placeholder="VD: 2024 hoặc 05/2024"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Mô tả tóm tắt nội dung</label>
                <textarea
                  rows={2}
                  maxLength={1000}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="VD: Bài thi đạt 19.5/20 điểm; Đề tài nghiên cứu ứng dụng AI..."
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm bg-white text-slate-900 focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
                />
              </div>

              <div className="flex justify-end gap-2 border-t border-slate-100 pt-3">
                <button
                  type="button"
                  onClick={() => setShowAddForm(false)}
                  className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm text-slate-700 hover:bg-slate-50 transition-colors"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="rounded-lg bg-blue-600 px-5 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
                >
                  {saving ? "Đang lưu..." : "Thêm thành tích"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {loading ? (
        <div className="text-center py-6 text-xs text-slate-400">Đang tải danh sách thành tích...</div>
      ) : achievements.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-200 py-8 text-center">
          <p className="text-sm text-slate-500">Chưa có giải thưởng hoặc nghiên cứu nào được khai báo.</p>
          <p className="text-xs text-slate-400 mt-1">Bấm &quot;+ Thêm thành tích&quot; để làm dày hồ sơ của bạn.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {achievements.map((ach) => {
            const cat = CATEGORY_MAP[ach.category] || CATEGORY_MAP.other;
            return (
              <div
                key={ach.id}
                className="flex items-start justify-between rounded-lg border border-slate-200 bg-slate-50/50 p-3.5 hover:bg-slate-50 transition-colors"
              >
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-sm text-slate-900">{ach.title}</span>
                    <span className={`rounded px-2 py-0.5 text-[11px] font-medium border ${cat.badge}`}>
                      {cat.label}
                    </span>
                  </div>

                  <p className="text-xs text-slate-600">
                    {ach.issuer && <span>{ach.issuer}</span>}
                    {ach.issueDate && <span> • {ach.issueDate}</span>}
                  </p>

                  {ach.description && <p className="text-xs text-slate-500 pt-1 italic">{ach.description}</p>}
                </div>

                <button
                  onClick={() => handleDelete(ach.id)}
                  title="Xóa thành tích"
                  className="text-xs text-rose-500 hover:text-rose-700 p-1"
                >
                  Xóa
                </button>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
