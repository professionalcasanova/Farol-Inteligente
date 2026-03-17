type MonthPickerProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
};

export function MonthPicker({ label, value, onChange }: MonthPickerProps) {
  return (
    <label className="flex min-w-[190px] flex-col gap-2 text-sm text-[var(--color-muted)]">
      <span>{label}</span>
      <input
        className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
        onChange={(event) => onChange(event.target.value)}
        type="month"
        value={value}
      />
    </label>
  );
}
