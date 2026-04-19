"use client";

import type { ReactNode } from "react";

type BoundedListProps = {
  children: ReactNode;
  emptyState?: ReactNode;
  hasItems: boolean;
  maxHeightClassName?: string;
  testId?: string;
};

export function BoundedList({
  children,
  emptyState,
  hasItems,
  maxHeightClassName = "max-h-[20rem]",
  testId,
}: BoundedListProps) {
  if (!hasItems) {
    return emptyState ? <>{emptyState}</> : null;
  }

  return (
    <div className={`overflow-y-auto pr-1 ${maxHeightClassName}`} data-testid={testId}>
      <div className="space-y-3">{children}</div>
    </div>
  );
}
