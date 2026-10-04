"use client";

// [USAS-364] Component quản lý danh sách hoạt động ngoại khóa & vai trò lãnh đạo

import { useState, useEffect } from "react";
import {
  getExtracurricularActivities,
  addExtracurricularActivity,
  deleteExtracurricularActivity,
} from "@/lib/profile-api";
import type { ExtracurricularActivityDto, CreateExtracurricularRequest } from "@/types/profile";

const IMPACT_LEVEL_LABELS: Record<number, { label: string; color: string }> = {
  1: { label: "Cấp Trường / CLB", color: "bg-slate-100 text-slate-700" },
  2: { label: "Cấp Quận / Huyện", color: "bg-blue-50 text-blue-700 border-blue-200" },
  3: { label: "Cấp Tỉnh / Thành phố", color: "bg-indigo-50 text-indigo-700 border-indigo-200" },
  4: { label: "Cấp Quốc gia", color: "bg-purple-50 text-purple-700 border-purple-200" },
  5: { label: "Cấp Quốc tế", color: "bg-amber-50 text-amber-800 border-amber-200" },
};

export function ExtracurricularSection() {
  const [activities, setActivities] = useState<ExtracurricularActivityDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [showAddForm, setShowAddForm] = useState<boolean>(false);

  // Form states
  const [name, setName] = useState("");
  const [role, setRole] = useState("");
  const [org, setOrg] = useState("");
  const [duration, setDuration] = useState("");
  const [impact, setImpact] = useState(1);
  const [description, setDescription] = useState("");
  const [isOngoing, setIsOngoing] = useState(false);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadActivities();
  }, []);

  async function loadActivities() {
    try {
      setLoading(true);
      const data = await getExtracurricularActivities();
      setActivities(data);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError("Vui lòng nhập tên hoạt động.");
      return;
    }

    if (!role.trim()) {
      setError("Vui lòng nhập vai trò của bạn.");
      return;
    }

    const durationNum = duration ? parseInt(duration, 10) : undefined;

    try {
      setSaving(true);
      const req: CreateExtracurricularRequest = {
        activityName: name.trim(),
        role: role.trim(),
        organization: org.trim() || undefined,
        durationMonths: durationNum,
        impactLevel: impact,
        isOngoing,
        description: description.trim() || undefined,
      };

      const created = await addExtracurricularActivity(req);
      setActivities([created, ...activities]);

      // Reset form
      setName("");
      setRole("");
      setOrg("");
      setDuration("");
      setImpact(1);
      setDescription("");
      setIsOngoing(false);
      setShowAddForm(false);
    } catch (err: any) {
      setError(err?.message || "Lỗi khi thêm hoạt động ngoại khóa.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm("Bạn có chắc chắn muốn xóa hoạt động này không?")) return;
    try {
      await deleteExtracurricularActivity(id);
      setActivities(activities.filter((a) => a.id !== id));
    } catch (err: any) {
      alert(err?.message || "Không thể xóa hoạt động.");
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between border-b border-slate-100 pb-3 mb-4">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">2. Hoạt động Ngoại khóa & Lãnh đạo</h2>
          <p className="text-xs text-slate-500">
            Tiêu chí thứ 3 để AI cộng điểm và đánh giá hồ sơ toàn diện (Holistic Review kiểu Mỹ).
          </p>
        </div>
        <button
          onClick={() => setShowAddForm(!showAddForm)}
          className="rounded-lg bg-slate-900 px-3.5 py-1.5 text-xs font-medium text-white hover:bg-slate-800 transition-colors"
        >
          {showAddForm ? "Đóng Form" : "+ Thêm hoạt động"}
        </button>
      </div>

      {showAddForm && (
        <form onSubmit={handleAdd} className="mb-6 rounded-lg border border-blue-100 bg-blue-50/40 p-4 space-y-3">
          <h3 className="text-sm font-semibold text-blue-900">Khai báo hoạt động mới</h3>
          {error && <div className="text-xs text-rose-600">{error}</div>}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-slate-700">Tên hoạt động / CLB / Dự án *</label>
              <input
                type="text"
                required
                maxLength={150}
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="VD: CLB Tranh biện FPT Debate Club"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">Vai trò của bạn *</label>
              <input
                type="text"
                required
                maxLength={100}
                value={role}
                onChange={(e) => setRole(e.target.value)}
                placeholder="VD: Chủ tịch CLB / Trưởng ban tổ chức"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">Đơn vị / Tổ chức / Trường</label>
              <input
                type="text"
                maxLength={150}
                value={org}
                onChange={(e) => setOrg(e.target.value)}
                placeholder="VD: Trường THPT Chuyên Hà Nội - Amsterdam"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">Mức độ ảnh hưởng (Quy mô) *</label>
              <select
                value={impact}
                onChange={(e) => setImpact(parseInt(e.target.value, 10))}
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
              >
                <option value={1}>1 - Cấp Trường / Câu lạc bộ</option>
                <option value={2}>2 - Cấp Quận / Huyện / Liên trường</option>
                <option value={3}>3 - Cấp Tỉnh / Thành phố</option>
                <option value={4}>4 - Cấp Quốc gia</option>
                <option value={5}>5 - Cấp Quốc tế</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-700">Thời gian tham gia (Số tháng)</label>
              <input
                type="number"
                min="1"
                max="120"
                value={duration}
                onChange={(e) => setDuration(e.target.value)}
                placeholder="12"
                className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
              />
            </div>

            <div className="flex items-center gap-2 pt-5">
              <input
                type="checkbox"
                id="isOngoing"
                checked={isOngoing}
                onChange={(e) => setIsOngoing(e.target.checked)}
                className="h-4 w-4 rounded border-slate-300 text-blue-600"
              />
              <label htmlFor="isOngoing" className="text-xs font-medium text-slate-700">
                Đang tiếp tục tham gia
              </label>
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-slate-700">Mô tả đóng góp & Kết quả cụ thể</label>
            <textarea
              rows={2}
              maxLength={1000}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="VD: Dẫn dắt 30 thành viên tổ chức giải tranh biện thu hút 200 thí sinh; gây quỹ 15 triệu đồng..."
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-1.5 text-sm bg-white text-slate-900"
            />
          </div>

          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={() => setShowAddForm(false)}
              className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs text-slate-700 hover:bg-slate-50"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={saving}
              className="rounded-md bg-blue-600 px-4 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {saving ? "Đang lưu..." : "Thêm hoạt động"}
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <div className="text-center py-6 text-xs text-slate-400">Đang tải danh sách hoạt động...</div>
      ) : activities.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-200 py-8 text-center">
          <p className="text-sm text-slate-500">Chưa có hoạt động ngoại khóa nào được khai báo.</p>
          <p className="text-xs text-slate-400 mt-1">Bấm "+ Thêm hoạt động" để tăng cơ hội nhận học bổng.</p>
        </div>
      ) : (
        <div className="space-y-3">
          {activities.map((act) => {
            const badge = IMPACT_LEVEL_LABELS[act.impactLevel] || IMPACT_LEVEL_LABELS[1];
            return (
              <div
                key={act.id}
                className="flex items-start justify-between rounded-lg border border-slate-200 bg-slate-50/50 p-3.5 hover:bg-slate-50 transition-colors"
              >
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-sm text-slate-900">{act.activityName}</span>
                    <span className={`rounded px-2 py-0.5 text-[11px] font-medium border ${badge.color}`}>
                      {badge.label}
                    </span>
                    {act.isOngoing && (
                      <span className="rounded bg-emerald-50 text-emerald-700 border border-emerald-200 px-1.5 py-0.5 text-[10px]">
                        Đang tham gia
                      </span>
                    )}
                  </div>

                  <p className="text-xs text-slate-600">
                    <span className="font-medium text-slate-700">{act.role}</span>
                    {act.organization ? ` • ${act.organization}` : ""}
                    {act.durationMonths ? ` • ${act.durationMonths} tháng` : ""}
                  </p>

                  {act.description && <p className="text-xs text-slate-500 pt-1 italic">{act.description}</p>}
                </div>

                <button
                  onClick={() => handleDelete(act.id)}
                  title="Xóa hoạt động"
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
