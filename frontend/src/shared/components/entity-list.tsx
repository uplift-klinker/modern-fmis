import { Fragment, type ReactNode } from "react";
import { List } from "@mui/material";
import { EmptyState } from "@/shared/components/empty-state";

export interface EntityListProps<T> {
  items: T[];
  emptyMessage: string;
  onRefresh: () => void;
  getKey: (item: T) => string;
  renderItem: (item: T) => ReactNode;
}

export function EntityList<T>({
  items,
  emptyMessage,
  onRefresh,
  getKey,
  renderItem,
}: EntityListProps<T>) {
  if (items.length === 0) {
    return <EmptyState message={emptyMessage} onRefresh={onRefresh} />;
  }

  return (
    <List>
      {items.map((item) => (
        <Fragment key={getKey(item)}>{renderItem(item)}</Fragment>
      ))}
    </List>
  );
}
