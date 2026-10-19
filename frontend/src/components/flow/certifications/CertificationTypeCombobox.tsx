"use client";

import { useMemo, useState } from "react";
import { PlusIcon } from "lucide-react";
import {
  Combobox,
  ComboboxCollection,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxGroup,
  ComboboxInput,
  ComboboxItem,
  ComboboxLabel,
  ComboboxList,
  ComboboxSeparator,
} from "@/components/ui/combobox";
import { searchCertTypes } from "@/lib/certification-utils";
import {
  OTHER_CERT_TYPE,
  type CertificationCategory,
  type CertificationTypeOption,
} from "@/types/profile-flow";

interface TypeChoice {
  id: string;
  name: string;
  scaleLabel: string | null;
}

// type (không phải interface) để khớp Group<Item> có index signature của Base UI
type ChoiceGroup = {
  value: string;
  label: string | null;
  items: TypeChoice[];
};

const OTHER_CHOICE: TypeChoice = { id: OTHER_CERT_TYPE, name: "Khác, tôi sẽ tự nhập tên", scaleLabel: null };

// Chữ hiện trong ô sau khi chọn
const labelOf = (item: TypeChoice) => (item.id === OTHER_CERT_TYPE ? "Khác (tự nhập tên)" : item.name);

interface CertificationTypeComboboxProps {
  id?: string;
  /** Danh mục từ metadata (app_settings profile.metadata) */
  types: CertificationTypeOption[];
  categories: CertificationCategory[];
  /** id loại đang chọn, "other" hoặc null */
  value: string | null;
  onChange: (typeId: string) => void;
  invalid?: boolean;
}

/** Ô chọn loại chứng chỉ: gõ để lọc (không dấu, kể cả tên gọi khác), 2 nhóm theo metadata, cuối luôn có "Khác" */
export function CertificationTypeCombobox({
  id,
  types,
  categories,
  value,
  onChange,
  invalid,
}: CertificationTypeComboboxProps) {
  const selected = useMemo<TypeChoice | null>(() => {
    if (!value) return null;
    if (value === OTHER_CERT_TYPE) return OTHER_CHOICE;
    const t = types.find((x) => x.id === value);
    return t ? { id: t.id, name: t.name, scaleLabel: t.scaleLabel } : null;
  }, [types, value]);

  const [inputValue, setInputValue] = useState(() => (selected ? labelOf(selected) : ""));

  // Ô đang hiện đúng tên loại đã chọn → coi như chưa gõ gì, hiện cả danh sách
  const query = selected && inputValue === labelOf(selected) ? "" : inputValue;

  const groups = useMemo<ChoiceGroup[]>(() => {
    const matched = searchCertTypes(types, categories, query).map((t) => ({
      choice: { id: t.id, name: t.name, scaleLabel: t.scaleLabel },
      group: t.group,
    }));
    const popular = matched.filter((m) => m.group === "popular").map((m) => m.choice);
    const rest = matched.filter((m) => m.group !== "popular").map((m) => m.choice);
    return [
      { value: "popular", label: "Phổ biến", items: popular },
      { value: "other", label: "Chứng chỉ khác", items: rest },
      { value: "custom", label: null, items: [OTHER_CHOICE] },
    ].filter((g) => g.items.length > 0);
  }, [types, categories, query]);

  const hasCatalog = types.some((t) => categories.includes(t.category));

  return (
    <div className="space-y-1.5">
      <Combobox<TypeChoice>
        items={groups}
        filteredItems={groups}
        value={selected}
        onValueChange={(next) => {
          if (!next) return;
          onChange(next.id);
          setInputValue(labelOf(next));
        }}
        inputValue={inputValue}
        onInputValueChange={setInputValue}
        itemToStringLabel={labelOf}
        isItemEqualToValue={(a, b) => a.id === b.id}
        autoHighlight
      >
        <ComboboxInput
          id={id}
          placeholder="Gõ để tìm, ví dụ: toefl"
          aria-invalid={invalid || undefined}
          className="h-10 w-full"
        />
        <ComboboxContent>
          <ComboboxEmpty>Không tìm thấy</ComboboxEmpty>
          <ComboboxList>
            {(group: ChoiceGroup) => (
              <ComboboxGroup key={group.value} items={group.items}>
                {group.value === "custom" ? (
                  <ComboboxSeparator />
                ) : (
                  <ComboboxLabel className="font-semibold tracking-wide uppercase">{group.label}</ComboboxLabel>
                )}
                <ComboboxCollection>
                  {(item: TypeChoice) => (
                    <ComboboxItem key={item.id} value={item} className="py-2">
                      {item.id === OTHER_CERT_TYPE ? (
                        <span className="flex items-center gap-1.5 font-medium text-brand">
                          <PlusIcon className="size-3.5" />
                          {item.name}
                        </span>
                      ) : (
                        <>
                          <span className="flex-1 truncate">{item.name}</span>
                          {item.scaleLabel && (
                            <span className="shrink-0 text-xs text-muted-foreground">{item.scaleLabel}</span>
                          )}
                        </>
                      )}
                    </ComboboxItem>
                  )}
                </ComboboxCollection>
              </ComboboxGroup>
            )}
          </ComboboxList>
        </ComboboxContent>
      </Combobox>
      <p className="text-xs text-muted-foreground">
        {hasCatalog
          ? "Danh sách lấy từ dữ liệu yêu cầu đầu vào của các trường, có thể bổ sung thêm."
          : "Chưa tải được danh sách chứng chỉ. Bạn vẫn có thể chọn \"Khác\" và tự nhập tên."}
      </p>
    </div>
  );
}
