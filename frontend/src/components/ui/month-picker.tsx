"use client"

import * as React from "react"
import { CalendarIcon, ChevronLeftIcon, ChevronRightIcon } from "lucide-react"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
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
        data-slot="month-picker"
        id={id}
        disabled={disabled}
        className={cn(
          "flex h-8 w-full cursor-pointer items-center justify-between gap-2 rounded-lg border border-input bg-transparent px-2.5 text-sm transition-colors outline-none select-none",
          "focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50",
          "disabled:cursor-default disabled:bg-muted disabled:text-muted-foreground disabled:opacity-50",
          !value && !disabled && "text-muted-foreground",
          className
        )}
      >
        <span>{label}</span>
        <CalendarIcon className="size-4 shrink-0 text-muted-foreground" />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-64">
        <div className="flex items-center justify-between">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Năm trước"
            onClick={() => setViewYear((y) => y - 1)}
            disabled={min != null && viewYear <= min.year}
          >
            <ChevronLeftIcon />
          </Button>
          <span className="text-sm font-semibold">{viewYear}</span>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Năm sau"
            onClick={() => setViewYear((y) => y + 1)}
            disabled={max != null && viewYear >= max.year}
          >
            <ChevronRightIcon />
          </Button>
        </div>
        <div className="grid grid-cols-3 gap-1.5">
          {MONTH_LABELS.map((text, i) => {
            const m = { year: viewYear, month: i + 1 }
            const selected = value != null && toIndex(value) === toIndex(m)
            return (
              <button
                key={text}
                type="button"
                aria-pressed={selected}
                disabled={isOut(m)}
                onClick={() => {
                  onChange(m)
                  setOpen(false)
                }}
                className={cn(
                  "h-7 cursor-pointer rounded-lg text-xs font-medium outline-none transition-[background-color,color,transform] duration-[var(--cds-dur-fast)] active:not-disabled:scale-[0.975] focus-visible:ring-3 focus-visible:ring-ring/50",
                  selected ? "bg-brand text-white" : "hover:bg-brand-wash hover:text-brand",
                  "disabled:pointer-events-none disabled:cursor-default disabled:opacity-30"
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
