"use client";

import { useEffect, useState } from "react";
import { PlusIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useAuth } from "@/contexts/AuthContext";
import { getAdminScholarships } from "@/lib/admin-scholarship-api";
import type { AdminScholarship } from "@/types/admin-scholarship";
import { ScholarshipTable } from "../scholarships/ScholarshipTable";

interface ScholarshipsSectionProps {
  universityId: string;
  /** Id học bổng đang sửa, "new" khi đang thêm mới, null khi không sửa bảng nào trong panel. */
  editingId: string | null;
  /** false khi đang sửa một bảng khác trong panel. */
  canEdit: boolean;
  onStartEdit: (scholarshipId: string) => void;
  onStopEdit: () => void;
}

interface LoadResult {
  universityId: string;
  items: AdminScholarship[] | null;
  error: string | null;
}

export function ScholarshipsSection({ universityId, editingId, canEdit, onStartEdit, onStopEdit }: ScholarshipsSectionProps) {
  const { user } = useAuth();
  const [result, setResult] = useState<LoadResult | null>(null);
  const current = result?.universityId === universityId ? result : null;
  const items = current?.items ?? null;

  useEffect(() => {
    let cancelled = false;
    getAdminScholarships({ universityId, page: 1, pageSize: 100 })
      .then((loaded) => {
        if (!cancelled) setResult({ universityId, items: loaded.items, error: null });
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setResult({ universityId, items: null, error: err instanceof Error ? err.message : "Không thể tải học bổng." });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [universityId]);

  const setItems = (next: AdminScholarship[]) => setResult({ universityId, items: next, error: null });
  const replace = (saved: AdminScholarship) => items && setItems(items.map((s) => (s.id === saved.id ? saved : s)));

  const handleSaved = (saved: AdminScholarship) => {
    if (items) setItems(items.some((s) => s.id === saved.id) ? items.map((s) => (s.id === saved.id ? saved : s)) : [...items, saved]);
    onStopEdit();
  };

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">Học bổng của trường</h3>
        {user?.role === "moderator" && (
          <Button type="button" size="sm" variant="outline" disabled={!canEdit} onClick={() => onStartEdit("new")}>
            <PlusIcon />
            Thêm học bổng
          </Button>
        )}
      </div>

      {!current && (
        <div className="flex items-center gap-2 text-muted-foreground">
          <Spinner />
          Đang tải học bổng...
        </div>
      )}
      {current?.error && (
        <Alert variant="destructive">
          <AlertDescription>{current.error}</AlertDescription>
        </Alert>
      )}
      {items && items.length === 0 && editingId !== "new" && (
        <p className="text-sm text-muted-foreground">Trường này chưa có học bổng nào.</p>
      )}

      {editingId === "new" && (
        <ScholarshipTable
          key="new"
          scholarship={null}
          lockedUniversityId={universityId}
          isEditing
          canEdit={false}
          onStartEdit={() => {}}
          onCancel={onStopEdit}
          onSaved={handleSaved}
          onVisibilityChanged={replace}
        />
      )}
      {items?.map((scholarship) => (
        <ScholarshipTable
          key={scholarship.id}
          scholarship={scholarship}
          lockedUniversityId={universityId}
          isEditing={editingId === scholarship.id}
          canEdit={canEdit}
          onStartEdit={() => onStartEdit(scholarship.id)}
          onCancel={onStopEdit}
          onSaved={handleSaved}
          onVisibilityChanged={replace}
        />
      ))}
    </section>
  );
}
