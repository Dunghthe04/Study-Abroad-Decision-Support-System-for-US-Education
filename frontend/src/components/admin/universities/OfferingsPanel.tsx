"use client";

import { useEffect, useState } from "react";
import { DetailPanel } from "../shared/DetailPanel";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import {
  getAdminUniversity,
  getAdminUniversityOfferings,
} from "@/lib/admin-university-api";
import { OfferingSection, type OfferingTable } from "./OfferingSection";
import { UniversityInfoSection } from "./UniversityInfoSection";
import { UniversityMediaSection } from "./UniversityMediaSection";
import { UniversityVisibilitySwitch } from "./UniversityVisibilitySwitch";
import type {
  AdminUniversityDetail,
  AdminUniversityListItem,
  AdminUniversityOffering,
} from "@/types/admin-university";

interface OfferingsPanelProps {
  university: AdminUniversityListItem | null;
  onClose: () => void;
  /** Gọi sau khi sửa thông tin hoặc ẩn/hiện trường, để danh sách cập nhật dòng tương ứng. */
  onUniversityChanged: (university: AdminUniversityDetail) => void;
}

interface LoadResult {
  universityId: string;
  detail: AdminUniversityDetail | null;
  offerings: AdminUniversityOffering[] | null;
  error: string | null;
}

/** Mỗi lúc chỉ sửa một bảng, để không mất dữ liệu đang nhập ở bảng khác. */
type Editing =
  | { kind: "media"; universityId: string }
  | { kind: "university"; universityId: string }
  | {
      kind: "offering";
      universityId: string;
      offeringId: string;
      table: OfferingTable;
    };

export function OfferingsPanel({
  university,
  onClose,
  onUniversityChanged,
}: OfferingsPanelProps) {
  const [result, setResult] = useState<LoadResult | null>(null);
  const [editingState, setEditingState] = useState<Editing | null>(null);
  const universityId = university?.id ?? null;
  const isLoading =
    universityId !== null && result?.universityId !== universityId;
  const current = result?.universityId === universityId ? result : null;
  const detail = current?.detail ?? null;
  const offerings = current?.offerings ?? null;
  const error = current?.error ?? null;
  const editing =
    editingState?.universityId === universityId ? editingState : null;

  useEffect(() => {
    if (!universityId) {
      return;
    }
    let cancelled = false;

    Promise.all([
      getAdminUniversity(universityId),
      getAdminUniversityOfferings(universityId),
    ])
      .then(([loadedDetail, loadedOfferings]) => {
        if (!cancelled) {
          setResult({
            universityId,
            detail: loadedDetail,
            offerings: loadedOfferings,
            error: null,
          });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          const message =
            err instanceof Error
              ? err.message
              : "Không thể tải danh sách bậc học.";
          setResult({
            universityId,
            detail: null,
            offerings: null,
            error: message,
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [universityId]);

  if (!university) {
    return null;
  }

  const stopEditing = () => {
    setEditingState(null);
  };

  const handleUniversityChanged = (saved: AdminUniversityDetail) => {
    if (current) {
      setResult({ ...current, detail: saved });
    }
    onUniversityChanged(saved);
  };

  const handleUniversitySaved = (saved: AdminUniversityDetail) => {
    handleUniversityChanged(saved);
    stopEditing();
  };

  const handleOfferingSaved = (saved: AdminUniversityOffering) => {
    if (current && current.offerings) {
      const nextOfferings = current.offerings.map((offering) =>
        offering.id === saved.id ? saved : offering,
      );
      setResult({ ...current, offerings: nextOfferings });
    }
    stopEditing();
  };

  const editingTableOf = (offeringId: string): OfferingTable | null => {
    if (editing?.kind === "offering" && editing.offeringId === offeringId) {
      return editing.table;
    }
    return null;
  };

  return (
    <DetailPanel title={university.name} description={`Mã ${university.code}`} onClose={onClose}>

        {detail && (
          <UniversityVisibilitySwitch
            university={detail}
            onChanged={handleUniversityChanged}
          />
        )}

        {detail && (
          <UniversityMediaSection
            key={`media-${detail.id}`}
            university={detail}
            isEditing={editing?.kind === "media"}
            canEdit={editing === null}
            onStartEdit={() =>
              setEditingState({ kind: "media", universityId: detail.id })
            }
            onStopEdit={stopEditing}
            onSaved={handleUniversitySaved}
          />
        )}

        {detail && (
          <UniversityInfoSection
            key={detail.id}
            university={detail}
            isEditing={editing?.kind === "university"}
            canEdit={editing === null}
            onStartEdit={() =>
              setEditingState({ kind: "university", universityId: detail.id })
            }
            onStopEdit={stopEditing}
            onSaved={handleUniversitySaved}
          />
        )}

        {isLoading && (
          <div className="flex items-center gap-2 text-muted-foreground">
            <Spinner />
            Đang tải bậc học...
          </div>
        )}

        {error && (
          <Alert variant="destructive">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {offerings && offerings.length === 0 && (
          <p className="text-muted-foreground">
            Trường này chưa có bậc học nào.
          </p>
        )}

        {offerings?.map((offering) => (
          <OfferingSection
            key={offering.id}
            universityId={university.id}
            offering={offering}
            editingTable={editingTableOf(offering.id)}
            canEdit={editing === null}
            onStartEdit={(table) =>
              setEditingState({
                kind: "offering",
                universityId: university.id,
                offeringId: offering.id,
                table,
              })
            }
            onStopEdit={stopEditing}
            onSaved={handleOfferingSaved}
          />
        ))}
    </DetailPanel>
  );
}
