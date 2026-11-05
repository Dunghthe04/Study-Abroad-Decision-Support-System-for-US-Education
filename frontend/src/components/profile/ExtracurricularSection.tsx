"use client";

// [USAS-364] Component quản lý danh sách hoạt động ngoại khóa & vai trò lãnh đạo

import { useState, useEffect } from "react";
import {
  getExtracurricularActivities,
  addExtracurricularActivity,
  deleteExtracurricularActivity,
} from "@/lib/profile-api";
import type { ExtracurricularActivityDto, CreateExtracurricularRequest } from "@/types/profile";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { FieldError } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";

const IMPACT_LEVEL_LABELS: Record<number, { label: string; variant: "neutral" | "brand" }> = {
  1: { label: "Cấp Trường / CLB", variant: "neutral" },
  2: { label: "Cấp Quận / Huyện", variant: "neutral" },
  3: { label: "Cấp Tỉnh / Thành phố", variant: "brand" },
  4: { label: "Cấp Quốc gia", variant: "brand" },
  5: { label: "Cấp Quốc tế", variant: "brand" },
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
    async function load() {
      try {
        const data = await getExtracurricularActivities();
        setActivities(data);
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
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Lỗi khi thêm hoạt động ngoại khóa.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id: string) {
    if (!confirm("Bạn có chắc chắn muốn xóa hoạt động này không?")) return;
    try {
      await deleteExtracurricularActivity(id);
      setActivities(activities.filter((a) => a.id !== id));
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : "Không thể xóa hoạt động.");
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>2. Hoạt động Ngoại khóa & Lãnh đạo</CardTitle>
        <CardDescription>
          Tiêu chí thứ 3 để AI cộng điểm và đánh giá hồ sơ toàn diện (Holistic Review kiểu Mỹ).
        </CardDescription>
        <CardAction>
          <Button size="sm" onClick={() => setShowAddForm(!showAddForm)}>
            {showAddForm ? "Đóng Form" : "+ Thêm hoạt động"}
          </Button>
        </CardAction>
      </CardHeader>

      <CardContent>
        {showAddForm && (
          <Card size="sm" className="mb-6">
            <CardContent>
              <form onSubmit={handleAdd} className="space-y-3">
                <h3 className="text-h3">Khai báo hoạt động mới</h3>
                {error && <FieldError>{error}</FieldError>}

                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="activity-name">Tên hoạt động / CLB / Dự án *</Label>
                    <Input
                      id="activity-name"
                      type="text"
                      required
                      maxLength={150}
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="VD: CLB Tranh biện FPT Debate Club"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="activity-role">Vai trò của bạn *</Label>
                    <Input
                      id="activity-role"
                      type="text"
                      required
                      maxLength={100}
                      value={role}
                      onChange={(e) => setRole(e.target.value)}
                      placeholder="VD: Chủ tịch CLB / Trưởng ban tổ chức"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="activity-organization">Đơn vị / Tổ chức / Trường</Label>
                    <Input
                      id="activity-organization"
                      type="text"
                      maxLength={150}
                      value={org}
                      onChange={(e) => setOrg(e.target.value)}
                      placeholder="VD: Trường THPT Chuyên Hà Nội - Amsterdam"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="activity-impact">Mức độ ảnh hưởng (Quy mô) *</Label>
                    <NativeSelect
                      id="activity-impact"
                      className="w-full"
                      value={impact}
                      onChange={(e) => setImpact(parseInt(e.target.value, 10))}
                    >
                      <NativeSelectOption value={1}>1 - Cấp Trường / Câu lạc bộ</NativeSelectOption>
                      <NativeSelectOption value={2}>2 - Cấp Quận / Huyện / Liên trường</NativeSelectOption>
                      <NativeSelectOption value={3}>3 - Cấp Tỉnh / Thành phố</NativeSelectOption>
                      <NativeSelectOption value={4}>4 - Cấp Quốc gia</NativeSelectOption>
                      <NativeSelectOption value={5}>5 - Cấp Quốc tế</NativeSelectOption>
                    </NativeSelect>
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="activity-duration">Thời gian tham gia (Số tháng)</Label>
                    <Input
                      id="activity-duration"
                      type="number"
                      min="1"
                      max="120"
                      step="1"
                      value={duration}
                      onChange={(e) => setDuration(e.target.value)}
                      placeholder="12"
                    />
                  </div>

                  <div className="flex items-center gap-2 pt-5">
                    <Checkbox
                      id="isOngoing"
                      checked={isOngoing}
                      onCheckedChange={(checked) => setIsOngoing(checked)}
                    />
                    <Label htmlFor="isOngoing">
                      Đang tiếp tục tham gia
                    </Label>
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="activity-description">Mô tả đóng góp & Kết quả cụ thể</Label>
                  <Textarea
                    id="activity-description"
                    rows={2}
                    maxLength={1000}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="VD: Dẫn dắt 30 thành viên tổ chức giải tranh biện thu hút 200 thí sinh; gây quỹ 15 triệu đồng..."
                  />
                </div>

                <div className="flex justify-end gap-2 pt-1">
                  <Button type="button" variant="outline" size="sm" onClick={() => setShowAddForm(false)}>
                    Hủy
                  </Button>
                  <Button type="submit" size="sm" disabled={saving}>
                    {saving ? "Đang lưu..." : "Thêm hoạt động"}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        )}

        {loading ? (
          <Empty>
            <EmptyDescription>Đang tải danh sách hoạt động...</EmptyDescription>
          </Empty>
        ) : activities.length === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyTitle>
                Chưa có hoạt động ngoại khóa nào được khai báo.
              </EmptyTitle>
              <EmptyDescription>
                Bấm &quot;+ Thêm hoạt động&quot; để tăng cơ hội nhận học bổng.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="space-y-3">
            {activities.map((act) => {
              const badge = IMPACT_LEVEL_LABELS[act.impactLevel] || IMPACT_LEVEL_LABELS[1];
              return (
                <Card key={act.id} size="sm">
                  <CardHeader>
                    <CardTitle>{act.activityName}</CardTitle>
                    <CardDescription>
                      {act.role}
                      {act.organization ? ` • ${act.organization}` : ""}
                      {act.durationMonths ? ` • ${act.durationMonths} tháng` : ""}
                    </CardDescription>
                    <CardAction>
                      <Button
                        variant="destructive"
                        size="xs"
                        onClick={() => handleDelete(act.id)}
                        title="Xóa hoạt động"
                      >
                        Xóa
                      </Button>
                    </CardAction>
                  </CardHeader>

                  <CardContent className="space-y-2">
                    <div className="flex flex-wrap gap-2">
                      <Badge variant={badge.variant}>
                        {badge.label}
                      </Badge>
                      {act.isOngoing && (
                        <Badge variant="ok">
                          Đang tham gia
                        </Badge>
                      )}
                    </div>

                    {act.description && <p className="text-body-s">{act.description}</p>}
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
