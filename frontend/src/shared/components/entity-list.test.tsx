import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ListItemButton, ListItemText } from "@mui/material";
import { renderWithProviders } from "@/testing/render-with-providers";
import { EntityList } from "@/shared/components/entity-list";

interface Row {
  id: string;
  label: string;
}

function renderRows(items: Row[], onRefresh: () => void = () => {}) {
  renderWithProviders(
    <EntityList
      items={items}
      emptyMessage="Nothing here yet."
      onRefresh={onRefresh}
      getKey={(row) => row.id}
      renderItem={(row) => (
        <ListItemButton>
          <ListItemText primary={row.label} />
        </ListItemButton>
      )}
    />,
  );
}

describe("EntityList", () => {
  it("renders each item when the collection has entries", () => {
    renderRows([
      { id: "1", label: "Alpha" },
      { id: "2", label: "Beta" },
    ]);

    expect(screen.getByText("Alpha")).toBeInTheDocument();
    expect(screen.getByText("Beta")).toBeInTheDocument();
    expect(screen.queryByText("Nothing here yet.")).not.toBeInTheDocument();
  });

  it("renders an empty state with a working refresh when the collection is empty", async () => {
    const onRefresh = vi.fn();

    renderRows([], onRefresh);

    expect(screen.getByText("Nothing here yet.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /refresh/i }));

    expect(onRefresh).toHaveBeenCalledTimes(1);
  });
});
