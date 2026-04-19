"use client";

import type { ReactNode } from "react";

type SecondarySupportPanelProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  children: ReactNode;
};

export function SecondarySupportPanel({
  eyebrow,
  title,
  description,
  children,
}: SecondarySupportPanelProps) {
  return (
    <article className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
      {eyebrow ? (
        <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
          {eyebrow}
        </div>
      ) : null}
      <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
        {title}
      </h2>
      {description ? (
        <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">{description}</p>
      ) : null}
      <div className="mt-6">{children}</div>
    </article>
  );
}
