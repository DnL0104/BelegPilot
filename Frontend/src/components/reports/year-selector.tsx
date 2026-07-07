"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

interface YearSelectorProps {
  value: number;
  onChange: (year: number) => void;
}

export function YearSelector({ value, onChange }: YearSelectorProps) {
  const currentYear = new Date().getFullYear();
  const years = Array.from({ length: 5 }, (_, i) => currentYear - i);

  return (
    <Select
      value={value.toString()}
      onValueChange={(v) => v && onChange(parseInt(v, 10))}
    >
      <SelectTrigger className="w-[140px]" aria-label="Jahr auswählen">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {years.map((y) => (
          <SelectItem key={y} value={y.toString()}>
            {y}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
