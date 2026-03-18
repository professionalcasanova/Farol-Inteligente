export function LoadingScreen({
  message = "Preparando seu painel...",
}: {
  message?: string;
}) {
  return (
    <div className="flex min-h-screen items-center justify-center px-6">
      <div className="w-full max-w-md rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-8 shadow-[0_32px_80px_rgba(20,37,51,0.08)]">
        <div className="mb-6 flex items-center gap-3">
          <div className="size-3 animate-pulse rounded-full bg-[var(--color-accent)]" />
          <span className="text-sm uppercase tracking-[0.24em] text-[var(--color-muted)]">
            Farol
          </span>
        </div>
        <p className="text-lg font-semibold text-[var(--color-foreground)]">
          {message}
        </p>
        <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
          Na primeira carga local, a API pode levar alguns segundos para responder.
        </p>
      </div>
    </div>
  );
}
