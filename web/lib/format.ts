export const currencyFormatter = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
  minimumFractionDigits: 2,
});

const shortDateFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "short",
});

const dateTimeFormatter = new Intl.DateTimeFormat("pt-BR", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

export function formatCurrency(value: number) {
  return currencyFormatter.format(value ?? 0);
}

export function formatDate(value: string) {
  return shortDateFormatter.format(new Date(`${value}T00:00:00`));
}

export function formatDateTime(value: string) {
  return dateTimeFormatter.format(new Date(value));
}

export function getCurrentMonthInputValue(date = new Date()) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  return `${date.getFullYear()}-${month}`;
}

export function getCurrentDateInputValue(date = new Date()) {
  return date.toISOString().slice(0, 10);
}

export function parseMonthInputValue(value: string) {
  const [year, month] = value.split("-").map(Number);

  return {
    month,
    year,
  };
}
