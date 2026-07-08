"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

interface YearFilterProps {
  value?: number;
  onChange: (year?: number) => void;
}

export function YearFilter({ value, onChange }: YearFilterProps) {
  const currentYear = new Date().getFullYear();
  const years = Array.from({ length: 5 }, (_, i) => currentYear - i);

  return (
    <Select
      value={value?.toString() ?? "all"}
      onValueChange={(v) => onChange(!v || v === "all" ? undefined : parseInt(v, 10))}
      items={{ all: "Alle Jahre", ...Object.fromEntries(years.map((y) => [y.toString(), y.toString()])) }}
    >
      <SelectTrigger className="w-[140px]" aria-label="Jahr filtern">
        <SelectValue placeholder="Alle Jahre" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="all">Alle Jahre</SelectItem>
        {years.map((y) => (
          <SelectItem key={y} value={y.toString()}>
            {y}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
