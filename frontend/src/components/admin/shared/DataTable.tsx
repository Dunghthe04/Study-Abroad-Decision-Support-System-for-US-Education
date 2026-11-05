"use client";

import { useState, type ReactNode } from "react";
import { Columns3Icon } from "lucide-react";
import { cn } from "cn";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

export interface DataTableColumn<T> {
  id: string;
  label: string;
  required?: boolean;
  headerClassName?: string;
  cellClassName?: string;
  render: (row: T) => ReactNode;
}

export interface DataTableToolbarSlots {
  filters?: ReactNode;
  search?: ReactNode;
  actions?: ReactNode;
}

interface DataTableProps<T> {
  columns: DataTableColumn<T>[];
  items: T[];
  getRowId: (row: T) => string;
  selectedRowId?: string;
  onRowSelect?: (row: T) => void;
  isLoading: boolean;
  error?: string | null;
  emptyMessage: string;
  label: string;
  toolbar?: DataTableToolbarSlots;
  footer?: ReactNode;
}

export function DataTable<T>({
  columns,
  items,
  getRowId,
  selectedRowId,
  onRowSelect,
  isLoading,
  error,
  emptyMessage,
  label,
  toolbar,
  footer,
}: DataTableProps<T>) {
  const [hiddenColumnIds, setHiddenColumnIds] = useState<string[]>([]);
  const visibleColumns = columns.filter(
    (column) => column.required || !hiddenColumnIds.includes(column.id),
  );

  const setColumnVisible = (columnId: string, visible: boolean) => {
    setHiddenColumnIds((current) => {
      if (visible) {
        return current.filter((id) => id !== columnId);
      }
      if (current.includes(columnId)) {
        return current;
      }
      return [...current, columnId];
    });
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {toolbar?.filters}
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button
                variant="outline"
                size="sm"
                className="border-border bg-muted/60 text-foreground hover:bg-muted"
              />
            }
          >
            <Columns3Icon />
            Cột hiển thị
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-48">
            {columns.map((column) => (
              <DropdownMenuCheckboxItem
                key={column.id}
                checked={column.required || !hiddenColumnIds.includes(column.id)}
                disabled={column.required}
                closeOnClick={false}
                onCheckedChange={(checked) => setColumnVisible(column.id, checked)}
              >
                {column.label}
                {column.required && <span className="ml-auto text-xs text-muted-foreground">Bắt buộc</span>}
              </DropdownMenuCheckboxItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>
        {toolbar?.search && <div className="min-w-48 flex-1">{toolbar.search}</div>}
        {toolbar?.actions}
      </div>
      <Card className="min-h-0 flex-1 py-0">
        <CardContent className="min-h-0 flex-1 p-0 [&_[data-slot=table-container]]:h-full [&_[data-slot=table-container]]:overflow-auto">
          <Table
            aria-label={label}
            aria-busy={isLoading}
            className="border-separate border-spacing-0 [&_td]:border-r [&_td]:border-b [&_td]:border-border [&_td]:py-1.5 [&_td:last-child]:border-r-0 [&_th]:border-r [&_th]:border-b [&_th]:border-border [&_th]:bg-muted [&_th]:font-semibold [&_th:last-child]:border-r-0 [&_tr:hover_td]:bg-brand-wash/40"
          >
            <TableHeader className="sticky top-0 z-10 bg-card">
              <TableRow>
                {visibleColumns.map((column) => (
                  <TableHead key={column.id} scope="col" className={column.headerClassName}>
                    {column.label}
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 6 }, (_, index) => (
                  <TableRow key={`loading-${index}`}>
                    <TableCell colSpan={visibleColumns.length}>
                      <Skeleton className="h-5 w-full" />
                    </TableCell>
                  </TableRow>
                ))
              ) : error ? (
                <TableRow>
                  <TableCell colSpan={visibleColumns.length}>
                    <Alert variant="destructive">
                      <AlertDescription>{error}</AlertDescription>
                    </Alert>
                  </TableCell>
                </TableRow>
              ) : items.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={visibleColumns.length} className="h-24 text-center text-muted-foreground">
                    {emptyMessage}
                  </TableCell>
                </TableRow>
              ) : (
                items.map((item) => {
                  const rowId = getRowId(item);
                  const isSelected = rowId === selectedRowId;
                  return (
                    <TableRow
                      key={rowId}
                      aria-selected={onRowSelect ? isSelected : undefined}
                      data-state={isSelected ? "selected" : undefined}
                      tabIndex={onRowSelect ? 0 : undefined}
                      className={cn(onRowSelect && "cursor-pointer focus-visible:outline-2 focus-visible:outline-ring focus-visible:-outline-offset-2")}
                      onClick={() => onRowSelect?.(item)}
                      onKeyDown={(event) => {
                        if (event.target !== event.currentTarget || !onRowSelect) {
                          return;
                        }
                        if (event.key === "Enter" || event.key === " ") {
                          event.preventDefault();
                          onRowSelect(item);
                        }
                      }}
                    >
                      {visibleColumns.map((column) => (
                        <TableCell key={column.id} className={column.cellClassName}>
                          {column.render(item)}
                        </TableCell>
                      ))}
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
      {footer}
    </div>
  );
}
