"use client";

import type { ReactNode } from "react";

type CollapsibleHelpProps = {
  title: string;
  summary?: string;
  children: ReactNode;
  defaultOpen?: boolean;
};

export function CollapsibleHelp({
  title,
  summary,
  children,
  defaultOpen = false,
}: CollapsibleHelpProps) {
  return (
    <details
      className="rounded-[24px] border border-[var(--color-line)] bg-white p-5"
      open={defaultOpen ? true : undefined}
    >
      <summary className="cursor-pointer list-none">
        <div className="flex flex-col gap-1 pr-6">
          <span className="text-sm font-semibold text-[var(--color-foreground)]">{title}</span>
          {summary ? (
            <span className="text-xs leading-5 text-[var(--color-muted)]">{summary}</span>
          ) : null}
        </div>
      </summary>
      <div className="mt-4 text-sm leading-6 text-[var(--color-muted)]">{children}</div>
    </details>
  );
}
