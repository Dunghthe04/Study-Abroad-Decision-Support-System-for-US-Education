"use client"

import * as React from "react"
import { CalendarIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-react"
import { cn } from "@/lib/utils"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"

export interface MonthValue {
  year: number
  month: number // 1..12
}

interface MonthPickerProps {
  id?: string
  value: MonthValue | null
  onChange: (value: MonthValue) => void
  /** Tháng sớm nhất được chọn (tính cả tháng này). */
  min?: MonthValue | null
  /** Tháng muộn nhất được chọn (tính cả tháng này). */
  max?: MonthValue | null
  placeholder?: string
  disabled?: boolean
  /** Chữ hiện trên nút khi bị khoá, VD "Đến nay". */
  disabledText?: string
  className?: string
}

const MONTH_LABELS = ["Th1", "Th2", "Th3", "Th4", "Th5", "Th6", "Th7", "Th8", "Th9", "Th10", "Th11", "Th12"]

const toIndex = (m: MonthValue) => m.year * 12 + m.month

/** Chọn tháng/năm: bấm mở bảng 12 tháng, đổi năm bằng mũi tên, bấm 1 tháng là xong. */
export function MonthPicker({
  id,
  value,
  onChange,
  min,
  max,
  placeholder = "Chọn tháng",
  disabled,
  disabledText,
  className,
}: MonthPickerProps) {
  const [open, setOpen] = React.useState(false)
  const [viewYear, setViewYear] = React.useState(() => value?.year ?? max?.year ?? new Date().getFullYear())

  const handleOpenChange = (next: boolean) => {
    if (next) setViewYear(value?.year ?? max?.year ?? new Date().getFullYear())
    setOpen(next)
  }

  const isOut = (m: MonthValue) =>
    (min != null && toIndex(m) < toIndex(min)) || (max != null && toIndex(m) > toIndex(max))

  const label = disabled && disabledText
    ? disabledText
    : value
      ? `${String(value.month).padStart(2, "0")}/${value.year}`
      : placeholder

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger
        id={id}
        disabled={disabled}
        className={cn(
          "flex h-9 w-full items-center justify-between gap-2 rounded-md border border-input bg-transparent px-3 text-sm shadow-xs transition-colors outline-none",
          "focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50",
          "disabled:cursor-not-allowed disabled:bg-muted disabled:text-muted-foreground",
          !value && !disabled && "text-muted-foreground",
          className
        )}
      >
        <span>{label}</span>
        <CalendarIcon className="size-4 shrink-0 text-muted-foreground" />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-64">
        <div className="flex items-center justify-between">
          <button
            type="button"
            aria-label="Năm trước"
            onClick={() => setViewYear((y) => y - 1)}
            disabled={min != null && viewYear <= min.year}
            className="rounded-md p-1 hover:bg-muted disabled:opacity-30 disabled:pointer-events-none"
          >
            <ChevronLeftIcon className="size-4" />
          </button>
          <span className="text-sm font-semibold">{viewYear}</span>
          <button
            type="button"
            aria-label="Năm sau"
            onClick={() => setViewYear((y) => y + 1)}
            disabled={max != null && viewYear >= max.year}
            className="rounded-md p-1 hover:bg-muted disabled:opacity-30 disabled:pointer-events-none"
          >
            <ChevronRightIcon className="size-4" />
          </button>
        </div>
        <div className="grid grid-cols-3 gap-1.5">
          {MONTH_LABELS.map((text, i) => {
            const m = { year: viewYear, month: i + 1 }
            const selected = value != null && toIndex(value) === toIndex(m)
            return (
              <button
                key={text}
                type="button"
                disabled={isOut(m)}
                onClick={() => {
                  onChange(m)
                  setOpen(false)
                }}
                className={cn(
                  "rounded-md py-1.5 text-xs font-medium transition-colors",
                  selected ? "bg-brand text-white" : "hover:bg-brand-wash hover:text-brand",
                  "disabled:pointer-events-none disabled:opacity-30"
                )}
              >
                {text}
              </button>
            )
          })}
        </div>
      </PopoverContent>
    </Popover>
  )
}
