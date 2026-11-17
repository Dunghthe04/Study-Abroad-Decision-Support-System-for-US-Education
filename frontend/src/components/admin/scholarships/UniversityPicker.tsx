"use client";

import { useEffect, useState } from "react";
import { Input } from "@/components/ui/input";
import { getAdminUniversities } from "@/lib/admin-university-api";
import type { LabeledOption } from "@/lib/admin-university-display";
import { OptionSelect } from "../universities/OptionSelect";

interface UniversityPickerProps {
  id: string;
  value: string;
  valueName: string;
  onChange: (id: string, name: string) => void;
  invalid?: boolean;
  disabled?: boolean;
}

/** Tìm trường theo tên rồi chọn; để trống nếu học bổng do đơn vị khác cấp. */
export function UniversityPicker({ id, value, valueName, onChange, invalid, disabled }: UniversityPickerProps) {
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<LabeledOption[]>([]);

  useEffect(() => {
    const term = query.trim();
    if (!term) return;
    let cancelled = false;
    const timer = setTimeout(() => {
      getAdminUniversities({ search: term, state: "", control: "", active: "all", page: 1 })
        .then((result) => {
          if (!cancelled) setFound(result.items.map((u) => ({ value: u.id, label: `${u.name} (${u.code})` })));
        })
        .catch(() => {
          if (!cancelled) setFound([]);
        });
    }, 300);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [query]);

  const options: LabeledOption[] = [{ value: "", label: "Không gắn trường" }];
  if (value && !found.some((o) => o.value === value)) options.push({ value, label: valueName });
  options.push(...(query.trim() ? found : []));

  return (
    <div className="space-y-1.5">
      <Input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Gõ tên hoặc mã trường để tìm"
        aria-label="Tìm trường"
        disabled={disabled}
      />
      <OptionSelect
        id={id}
        value={value}
        options={options}
        onChange={(next) => onChange(next, options.find((o) => o.value === next)?.label ?? "")}
        invalid={invalid}
        disabled={disabled}
      />
    </div>
  );
}
