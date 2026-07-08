import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "./select";

// Regression test for a real bug: Base UI's <Select.Value> only resolves the
// selected item's label from the `items` map on Select.Root. Without it, the
// trigger falls back to the raw value once the popup (and its SelectItem) unmounts.
describe("Select", () => {
  it("shows the item's label (not the raw value) in the trigger after selecting", async () => {
    const user = userEvent.setup();
    const onValueChange = vi.fn();

    render(
      <Select
        onValueChange={onValueChange}
        items={{ WerbungskostenArbeitsmittel: "Arbeitsmittel (Werbungskosten)" }}
      >
        <SelectTrigger aria-label="Kategorie">
          <SelectValue placeholder="Kategorie wählen..." />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="WerbungskostenArbeitsmittel">
            Arbeitsmittel (Werbungskosten)
          </SelectItem>
        </SelectContent>
      </Select>
    );

    await user.click(screen.getByRole("combobox", { name: "Kategorie" }));
    await user.click(await screen.findByRole("option", { name: "Arbeitsmittel (Werbungskosten)" }));

    expect(onValueChange).toHaveBeenCalledWith(
      "WerbungskostenArbeitsmittel",
      expect.anything()
    );
    expect(screen.getByRole("combobox", { name: "Kategorie" })).toHaveTextContent(
      "Arbeitsmittel (Werbungskosten)"
    );
    expect(
      screen.queryByText("WerbungskostenArbeitsmittel")
    ).not.toBeInTheDocument();
  });
});
