"use client";

import { Button } from "@/components/ui/button";
import { PREF_PRESETS } from "@/lib/recommendation-api";
import { findPreset } from "@/lib/recommendation-labels";
import type { Pref } from "@/types/recommendation";

interface PrefButtonsProps {
  current: Pref; // pref của lần gợi ý đang hiện
  disabled: boolean; // đang gọi lại lượt chọn
  onSelect: (pref: Pref) => void;
}

// Bốn nút ưu tiên. Bấm nút chỉ sắp xếp lại trong từng khối, không đổi nhóm của trường.
export function PrefButtons({ current, disabled, onSelect }: PrefButtonsProps) {
  const active = findPreset(current)?.key;

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-foreground">Bạn quan tâm điều gì nhất?</p>
      <div className="flex flex-wrap gap-2">
        {PREF_PRESETS.map((preset) => (
          <Button
            key={preset.key}
            size="sm"
            variant={preset.key === active ? "default" : "outline"}
            disabled={disabled || preset.key === active}
            onClick={() => onSelect(preset.pref)}
          >
            {preset.label}
          </Button>
        ))}
      </div>
    </div>
  );
}
