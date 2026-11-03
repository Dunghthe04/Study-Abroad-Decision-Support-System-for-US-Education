"use client";

import { CheckIcon, PencilIcon, XIcon } from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";

export interface TableRow {
  label: string;
  value: React.ReactNode;
  /** id của ô nhập ở chế độ sửa, để bấm vào nhãn thì focus ô đó. */
  inputId?: string;
  error?: string;
}

/** Trạng thái sửa của một bảng; không truyền thì bảng chỉ để xem. */
export interface TableEditState {
  isEditing: boolean;
  canEdit: boolean;
  isSaving: boolean;
  hasChanges: boolean;
  error: string | null;
  onEdit: () => void;
  onCancel: () => void;
  onSave: () => void;
}

const FRAME = "overflow-hidden rounded-md border border-border";
const HEADER =
  "flex min-h-9 items-center justify-between gap-2 bg-muted px-3 py-1 text-xs font-semibold tracking-wide text-foreground uppercase";
const LABEL_CELL =
  "w-1/2 border-t border-r border-border bg-muted px-3 py-1.5 text-left align-middle text-xs font-normal text-muted-foreground";
const VALUE_CELL = "border-t border-border px-3 py-1.5 text-right text-sm font-medium";
const EDIT_VALUE_CELL = "border-t border-border px-2 py-1 text-left text-sm";

function HeaderActions({ edit }: { edit: TableEditState }) {
  if (!edit.isEditing) {
    return (
      <Button
        type="button"
        variant="ghost"
        size="xs"
        className="tracking-normal normal-case"
        onClick={edit.onEdit}
        disabled={!edit.canEdit}
      >
        <PencilIcon />
        Sửa
      </Button>
    );
  }

  return (
    <span className="flex items-center gap-1 tracking-normal normal-case">
      <Button type="button" variant="ghost" size="xs" onClick={edit.onCancel} disabled={edit.isSaving}>
        <XIcon />
        Hủy
      </Button>
      <Button type="submit" size="xs" disabled={edit.isSaving || !edit.hasChanges}>
        {edit.isSaving ? <Spinner /> : <CheckIcon />}
        Lưu
      </Button>
    </span>
  );
}

/**
 * Khung bảng có tiêu đề. Ở chế độ sửa, cả bảng nằm trong một form:
 * Enter trong ô nhập hoặc nút Lưu trên tiêu đề đều lưu.
 */
export function TableFrame({
  title,
  edit,
  children,
}: {
  title: string;
  edit?: TableEditState;
  children: React.ReactNode;
}) {
  const header = (
    <div className={HEADER}>
      <span>{title}</span>
      {edit && <HeaderActions edit={edit} />}
    </div>
  );

  if (!edit || !edit.isEditing) {
    return (
      <div className={FRAME}>
        {header}
        {children}
      </div>
    );
  }

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    edit.onSave();
  };

  return (
    <form onSubmit={handleSubmit} className={`${FRAME} ring-2 ring-ring/40`}>
      {header}
      {edit.error && (
        <Alert variant="destructive" className="rounded-none border-x-0 border-b-0">
          <AlertDescription>{edit.error}</AlertDescription>
        </Alert>
      )}
      {children}
    </form>
  );
}

export function RowsTable({ rows, isEditing = false }: { rows: TableRow[]; isEditing?: boolean }) {
  return (
    <table className="w-full border-separate border-spacing-0">
      <tbody>
        {rows.map((row) => (
          <tr key={row.label}>
            <th scope="row" className={LABEL_CELL}>
              {isEditing && row.inputId ? <label htmlFor={row.inputId}>{row.label}</label> : row.label}
            </th>
            <td className={isEditing ? EDIT_VALUE_CELL : VALUE_CELL}>
              {row.value}
              {row.error && <p className="mt-1 text-xs text-destructive">{row.error}</p>}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function EditableTable({
  title,
  rows,
  edit,
}: {
  title: string;
  rows: TableRow[];
  edit?: TableEditState;
}) {
  return (
    <TableFrame title={title} edit={edit}>
      <RowsTable rows={rows} isEditing={edit?.isEditing ?? false} />
    </TableFrame>
  );
}

export function BlockBody({ children }: { children: React.ReactNode }) {
  return <div className="border-t border-border px-3 py-2 text-sm">{children}</div>;
}
