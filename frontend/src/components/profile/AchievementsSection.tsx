"use client";

// [USAS-364] Component quản lý Giải thưởng, Nghiên cứu khoa học và Chứng chỉ

import { useState, useEffect } from "react";
import { Trash2Icon } from "lucide-react";
import { getAchievements, addAchievement, deleteAchievement } from "@/lib/profile-api";
import type { StudentAchievementDto, CreateAchievementRequest } from "@/types/profile";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { AppDialog } from "@/components/ui/dialog";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@/components/ui/empty";
import { FieldError } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";

const ACHIEVEMENT_CATEGORY_OPTIONS = [
  { value: "award", label: "Giải thưởng / Học sinh giỏi" },
  { value: "research", label: "Nghiên cứu khoa học / Bài báo" },
  { value: "internship", label: "Thực tập / Dự án thực tế" },
  { value: "certification", label: "Chứng chỉ chuyên môn" },
  { value: "other", label: "Khác" },
];

const CATEGORY_MAP: Record<string, { label: string; variant: "brand" | "neutral" }> = {
  award: { label: "Giải thưởng / Học sinh giỏi", variant: "brand" },
  research: { label: "Nghiên cứu khoa học", variant: "brand" },
  internship: { label: "Thực tập / Dự án thực tế", variant: "brand" },
  certification: { label: "Chứng chỉ chuyên môn", variant: "brand" },
  other: { label: "Khác", variant: "neutral" },
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
  const [deleteTargetId, setDeleteTargetId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

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

  async function confirmDelete() {
    if (!deleteTargetId) return;
    setDeleteError(null);
    setDeleting(true);
    try {
      await deleteAchievement(deleteTargetId);
      setAchievements((prev) => prev.filter((a) => a.id !== deleteTargetId));
      setDeleteTargetId(null);
    } catch (err: unknown) {
      setDeleteError(err instanceof Error ? err.message : "Không thể xóa thành tích.");
      setDeleteTargetId(null);
    } finally {
      setDeleting(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>3. Nghiên cứu, Thực tập & Giải thưởng</CardTitle>
        <CardDescription>
          Minh chứng năng lực học thuật xuất sắc và hồ sơ nổi bật (Honors & Awards).
        </CardDescription>
        <CardAction>
          <Button size="sm" onClick={() => setShowAddForm(!showAddForm)}>
            {showAddForm ? "Đóng Form" : "+ Thêm thành tích"}
          </Button>
        </CardAction>
      </CardHeader>

      <CardContent>
        {deleteError && <FieldError className="mb-3">{deleteError}</FieldError>}
        {showAddForm && (
          <Card size="sm" className="mb-6">
            <CardContent>
              <form onSubmit={handleAdd} className="space-y-3">
                <h3 className="text-h3">Khai báo thành tích mới</h3>
                {error && <FieldError>{error}</FieldError>}

                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <div className="space-y-1.5">
                    <Label htmlFor="achievement-category">Phân loại *</Label>
                    <Select
                      value={category}
                      onValueChange={(v) => {
                        if (v !== null) setCategory(v);
                      }}
                      items={ACHIEVEMENT_CATEGORY_OPTIONS}
                    >
                      <SelectTrigger id="achievement-category" className="w-full bg-surface">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {ACHIEVEMENT_CATEGORY_OPTIONS.map((opt) => (
                          <SelectItem key={opt.value} value={opt.value}>
                            {opt.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="achievement-title">Tên thành tích / Đề tài *</Label>
                    <Input
                      id="achievement-title"
                      type="text"
                      required
                      maxLength={150}
                      value={title}
                      onChange={(e) => setTitle(e.target.value)}
                      placeholder="VD: Giải Nhất Học sinh Giỏi Toán Cấp Tỉnh"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="achievement-issuer">Đơn vị cấp / Nơi thực tập</Label>
                    <Input
                      id="achievement-issuer"
                      type="text"
                      maxLength={150}
                      value={issuer}
                      onChange={(e) => setIssuer(e.target.value)}
                      placeholder="VD: Sở Giáo dục và Đào tạo / Viện Nghiên cứu"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <Label htmlFor="achievement-issue-date">Thời gian đạt được (Năm/Tháng)</Label>
                    <Input
                      id="achievement-issue-date"
                      type="text"
                      maxLength={20}
                      value={issueDate}
                      onChange={(e) => setIssueDate(e.target.value)}
                      placeholder="VD: 2024 hoặc 05/2024"
                    />
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label htmlFor="achievement-description">Mô tả tóm tắt nội dung</Label>
                  <Textarea
                    id="achievement-description"
                    rows={2}
                    maxLength={1000}
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    placeholder="VD: Bài thi đạt 19.5/20 điểm; Đề tài nghiên cứu ứng dụng AI trong phân loại rác thải..."
                  />
                </div>

                <div className="flex justify-end gap-2 pt-1">
                  <Button type="button" variant="outline" size="sm" onClick={() => setShowAddForm(false)}>
                    Hủy
                  </Button>
                  <Button type="submit" size="sm" disabled={saving}>
                    {saving ? "Đang lưu..." : "Thêm thành tích"}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        )}

        {loading ? (
          <Empty>
            <EmptyDescription>Đang tải danh sách thành tích...</EmptyDescription>
          </Empty>
        ) : achievements.length === 0 ? (
          <Empty>
            <EmptyHeader>
              <EmptyTitle>
                Chưa có giải thưởng hoặc nghiên cứu nào được khai báo.
              </EmptyTitle>
              <EmptyDescription>
                Bấm &quot;+ Thêm thành tích&quot; để làm dày hồ sơ của bạn.
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <div className="space-y-3">
            {achievements.map((ach) => {
              const cat = CATEGORY_MAP[ach.category] || CATEGORY_MAP.other;
              return (
                <Card key={ach.id} size="sm">
                  <CardHeader>
                    <CardTitle>{ach.title}</CardTitle>
                    <CardDescription>
                      {ach.issuer && <span>{ach.issuer}</span>}
                      {ach.issueDate && <span> • {ach.issueDate}</span>}
                    </CardDescription>
                    <CardAction>
                      <Button
                        variant="destructive"
                        size="xs"
                        onClick={() => {
                          setDeleteError(null);
                          setDeleteTargetId(ach.id);
                        }}
                        title="Xóa thành tích"
                      >
                        Xóa
                      </Button>
                    </CardAction>
                  </CardHeader>

                  <CardContent className="space-y-2">
                    <Badge variant={cat.variant}>
                      {cat.label}
                    </Badge>

                    {ach.description && <p className="text-body-s">{ach.description}</p>}
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}
      </CardContent>

      <AppDialog
        open={deleteTargetId !== null}
        onOpenChange={(open) => {
          if (!open && !deleting) setDeleteTargetId(null);
        }}
        variant="action"
        icon={<Trash2Icon aria-hidden />}
        iconTone="destructive"
        title="Xóa thành tích?"
        description="Bạn có chắc chắn muốn xóa thành tích này không?"
        cancelLabel="Hủy"
        confirmLabel="Xóa"
        confirmTone="destructive"
        loading={deleting}
        onConfirm={() => void confirmDelete()}
      />
    </Card>
  );
}
