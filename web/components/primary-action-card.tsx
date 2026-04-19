"use client";

import type { ReactNode } from "react";

type PrimaryActionCardProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  children: ReactNode;
  footer?: ReactNode;
};

export function PrimaryActionCard({
  eyebrow,
  title,
  description,
  children,
  footer,
}: PrimaryActionCardProps) {
  return (
    <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
      {eyebrow ? (
        <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
          {eyebrow}
        </div>
      ) : null}
      <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
        {title}
      </h2>
      {description ? (
        <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
          {description}
        </p>
      ) : null}
      <div className="mt-6">{children}</div>
      {footer ? <div className="mt-6">{footer}</div> : null}
    </section>
  );
}
