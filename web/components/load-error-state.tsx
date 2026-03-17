"use client";

type LoadErrorStateProps = {
  title: string;
  message: string;
  onRetry: () => void;
};

export function LoadErrorState({
  title,
  message,
  onRetry,
}: LoadErrorStateProps) {
  return (
    <div className="rounded-[28px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] p-6">
      <div className="text-xs font-semibold uppercase tracking-[0.22em] text-red-700">
        Falha ao carregar
      </div>
      <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
        {title}
      </h2>
      <p className="mt-3 max-w-2xl text-sm leading-6 text-red-700">
        {message}
      </p>
      <button
        className="mt-5 rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)]"
        onClick={onRetry}
        type="button"
      >
        Tentar novamente
      </button>
    </div>
  );
}
