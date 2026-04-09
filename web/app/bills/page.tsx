"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  createBill,
  getFriendlyApiMessage,
  isUnauthorizedApiError,
  listBills,
  payBill,
  type BillResponse,
  type BillSeriesKind,
  type BillStatus,
  unpayBill,
} from "@/lib/api";
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getCurrentDateInputValue,
  getCurrentMonthInputValue,
  parseMonthInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

const billMessageMap = {
  "Bill was not found.": "A conta a pagar nao foi encontrada.",
  "Month and year are invalid.": "O mes e o ano informados sao invalidos.",
  "Month and year must be provided together.":
    "Informe mes e ano juntos para filtrar as contas.",
  "Bill status is invalid. Use pending, paid or overdue.":
    "O filtro de status das contas a pagar esta invalido.",
  "Bill amount must be greater than zero.":
    "Informe um valor maior que zero para a conta a pagar.",
  "Bill description is required.":
    "Informe uma descricao para a conta a pagar.",
  "Bill due date is required.":
    "Informe a data de vencimento da conta a pagar.",
  "Recurring bill kind is invalid. Use recurring or installment.":
    "O tipo da conta recorrente ou parcelada esta invalido.",
  "Installment bills must use occurrence_count end mode.":
    "Parcelamentos precisam informar a quantidade total de parcelas.",
  "Installment count must be at least 2.":
    "Informe pelo menos 2 parcelas para criar um parcelamento.",
} as const;

type BillFormKind = "single" | "recurring" | "installment";
type RecurrenceEndMode = "open_ended" | "until_date" | "occurrence_count";

type BillFormState = {
  description: string;
  amount: string;
  dueOn: string;
  kind: BillFormKind;
  recurrenceEndMode: RecurrenceEndMode;
  recurrenceUntilDate: string;
  recurrenceCount: string;
  installmentCount: string;
};

const statusOptions: Array<{ value: "" | BillStatus; label: string }> = [
  { value: "", label: "Todos os status" },
  { value: "pending", label: "Pendentes" },
  { value: "paid", label: "Pagas" },
  { value: "overdue", label: "Atrasadas" },
];

const statusStyles: Record<BillStatus, string> = {
  pending:
    "bg-[color:rgba(217,119,6,0.12)] text-[color:#9a5700] border-[color:rgba(217,119,6,0.14)]",
  paid:
    "bg-[color:rgba(29,130,93,0.12)] text-[var(--color-success)] border-[color:rgba(29,130,93,0.14)]",
  overdue:
    "bg-[color:rgba(185,28,28,0.1)] text-red-700 border-[color:rgba(185,28,28,0.14)]",
};

const statusLabels: Record<BillStatus, string> = {
  pending: "Pendente",
  paid: "Paga",
  overdue: "Atrasada",
};

const billKindStyles = {
  single:
    "bg-[color:rgba(15,23,42,0.06)] text-[var(--color-foreground)] border-[color:rgba(15,23,42,0.08)]",
  recurring:
    "bg-[color:rgba(16,185,129,0.12)] text-[color:#0f766e] border-[color:rgba(15,118,110,0.12)]",
  installment:
    "bg-[color:rgba(59,130,246,0.12)] text-[color:#1d4ed8] border-[color:rgba(29,78,216,0.12)]",
} satisfies Record<BillFormKind | BillSeriesKind, string>;

const billKindLabels = {
  single: "Avulsa",
  recurring: "Recorrente",
  installment: "Parcelada",
} satisfies Record<BillFormKind | BillSeriesKind, string>;

function getDefaultDueOn(monthValue: string) {
  const today = getCurrentDateInputValue();
  return today.startsWith(monthValue) ? today : `${monthValue}-01`;
}

function createEmptyForm(
  monthValue: string,
  currentKind?: BillFormKind,
): BillFormState {
  return {
    description: "",
    amount: "",
    dueOn: getDefaultDueOn(monthValue),
    kind: currentKind ?? "single",
    recurrenceEndMode: "open_ended",
    recurrenceUntilDate: "",
    recurrenceCount: "",
    installmentCount: "12",
  };
}

function getBillKind(bill: BillResponse): BillFormKind | BillSeriesKind {
  return bill.seriesKind ?? "single";
}

function getBillCadenceLabel(bill: BillResponse) {
  const kind = getBillKind(bill);

  if (kind === "installment" && bill.occurrenceNumber && bill.totalOccurrences) {
    return `Parcela ${bill.occurrenceNumber}/${bill.totalOccurrences}`;
  }

  if (kind === "recurring" && bill.occurrenceNumber && bill.totalOccurrences) {
    return `Recorrencia ${bill.occurrenceNumber}/${bill.totalOccurrences}`;
  }

  if (kind === "recurring") {
    return "Recorrencia mensal";
  }

  return "Lancamento unico";
}

export default function BillsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [statusFilter, setStatusFilter] = useState<"" | BillStatus>("");
  const [bills, setBills] = useState<BillResponse[]>([]);
  const [form, setForm] = useState<BillFormState>(() =>
    createEmptyForm(getCurrentMonthInputValue()),
  );
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [actionBillId, setActionBillId] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );

  useEffect(() => {
    if (form.description || form.amount) {
      return;
    }

    setForm((current) => ({
      ...current,
      dueOn: getDefaultDueOn(monthValue),
    }));
  }, [form.amount, form.description, monthValue]);

  useEffect(() => {
    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    let isCancelled = false;

    async function load() {
      setIsFetching(true);
      setLoadError("");

      try {
        const response = await listBills(accessToken, {
          month: monthAndYear.month,
          year: monthAndYear.year,
          status: statusFilter,
        });

        if (isCancelled) {
          return;
        }

        setBills(response);
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel carregar as contas a pagar agora. Tente novamente em alguns instantes.",
              { messageMap: billMessageMap },
            ),
          );
        }
      } finally {
        if (!isCancelled) {
          setIsFetching(false);
        }
      }
    }

    void load();

    return () => {
      isCancelled = true;
    };
  }, [logout, monthAndYear.month, monthAndYear.year, reloadKey, session, statusFilter]);

  async function refreshBills() {
    if (!session) {
      return;
    }

    const response = await listBills(session.accessToken, {
      month: monthAndYear.month,
      year: monthAndYear.year,
      status: statusFilter,
    });

    setBills(response);
  }

  async function handleCreateBill(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      let recurrence:
        | {
            kind: BillSeriesKind;
            frequency: "monthly";
            endMode: RecurrenceEndMode;
            untilDate?: string | null;
            occurrenceCount?: number | null;
          }
        | undefined;

      if (form.kind === "recurring") {
        if (
          form.recurrenceEndMode === "until_date" &&
          !form.recurrenceUntilDate.trim()
        ) {
          setFormError("Informe ate quando essa conta vai se repetir.");
          return;
        }

        if (form.recurrenceEndMode === "occurrence_count") {
          const occurrenceCount = Number(form.recurrenceCount);

          if (!Number.isInteger(occurrenceCount) || occurrenceCount <= 0) {
            setFormError("Informe quantos meses essa recorrencia deve durar.");
            return;
          }

          recurrence = {
            kind: "recurring",
            frequency: "monthly",
            endMode: "occurrence_count",
            occurrenceCount,
          };
        } else {
          recurrence = {
            kind: "recurring",
            frequency: "monthly",
            endMode: form.recurrenceEndMode,
            untilDate:
              form.recurrenceEndMode === "until_date"
                ? form.recurrenceUntilDate
                : null,
          };
        }
      }

      if (form.kind === "installment") {
        const installmentCount = Number(form.installmentCount);

        if (!Number.isInteger(installmentCount) || installmentCount < 2) {
          setFormError("Informe pelo menos 2 parcelas para continuar.");
          return;
        }

        recurrence = {
          kind: "installment",
          frequency: "monthly",
          endMode: "occurrence_count",
          occurrenceCount: installmentCount,
        };
      }

      await createBill(session.accessToken, {
        description: form.description,
        amount: Number(form.amount),
        dueOn: form.dueOn,
        recurrence,
      });

      await refreshBills();
      setForm(createEmptyForm(monthValue, form.kind));
      setSuccess("Conta a pagar criada com sucesso.");
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel registrar a conta a pagar agora. Revise os dados e tente novamente.",
          { messageMap: billMessageMap },
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleTogglePayment(bill: BillResponse) {
    if (!session) {
      return;
    }

    setActionBillId(bill.id);
    setFormError("");
    setSuccess("");

    try {
      if (bill.isPaid) {
        await unpayBill(session.accessToken, bill.id);
        setSuccess("Pagamento removido com sucesso.");
      } else {
        await payBill(session.accessToken, bill.id);
        setSuccess("Bill marcada como paga.");
      }

      await refreshBills();
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel atualizar a conta a pagar agora. Tente novamente.",
          { messageMap: billMessageMap },
        ),
      );
    } finally {
      setActionBillId(null);
    }
  }

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  return (
    <AppShell
      actions={
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <MonthPicker
            label="Mes das contas"
            onChange={setMonthValue}
            value={monthValue}
          />
          <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
            <span>Status</span>
            <select
              className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
              onChange={(event) =>
                setStatusFilter(event.target.value as "" | BillStatus)
              }
              value={statusFilter}
            >
              {statusOptions.map((option) => (
                <option key={option.value || "all"} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        </div>
      }
      description="Acompanhe vencimentos, destaque atrasos e marque pagamentos sem sair do MVP. O dashboard reflete tudo isso no mesmo mes."
      onLogout={logout}
      session={session}
      title="Contas a pagar"
    >
      {formError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {formError}
        </div>
      ) : null}

      {success ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
          <div>{success}</div>
          <div className="mt-3 flex flex-wrap gap-3">
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/dashboard"
            >
              Ver resumo no dashboard
            </Link>
          </div>
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando contas a pagar do mes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar as contas a pagar"
        />
      ) : (
        <div className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.94fr)_minmax(0,1.06fr)]">
          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Nova conta
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Registrar vencimento
            </h2>

            <form className="mt-6 space-y-4" onSubmit={handleCreateBill}>
              <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                <span>Tipo de conta</span>
                <select
                  aria-label="Tipo de conta"
                  className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      kind: event.target.value as BillFormKind,
                    }))
                  }
                  value={form.kind}
                >
                  <option value="single">Conta avulsa</option>
                  <option value="recurring">Recorrente mensal</option>
                  <option value="installment">Parcelada</option>
                </select>
              </label>

              <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                <span>Descricao</span>
                <input
                  className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      description: event.target.value,
                    }))
                  }
                  placeholder="Aluguel, energia, internet..."
                  value={form.description}
                />
              </label>

              <div className="grid gap-4 md:grid-cols-2">
                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Valor</span>
                  <input
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    min="0.01"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        amount: event.target.value,
                      }))
                    }
                    placeholder="250.00"
                    step="0.01"
                    type="number"
                    value={form.amount}
                  />
                </label>

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Vencimento</span>
                  <input
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        dueOn: event.target.value,
                      }))
                    }
                    type="date"
                    value={form.dueOn}
                  />
                </label>
              </div>

              {form.kind === "recurring" ? (
                <div className="space-y-4 rounded-[24px] border border-[var(--color-line)] bg-white/70 p-4">
                  <div className="text-sm text-[var(--color-muted)]">
                    Essa conta se repete todo mes e continua aparecendo na agenda ate voce encerrar.
                  </div>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Duracao</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          recurrenceEndMode: event.target.value as RecurrenceEndMode,
                        }))
                      }
                      value={form.recurrenceEndMode}
                    >
                      <option value="open_ended">Sem prazo definido</option>
                      <option value="until_date">Ate uma data</option>
                      <option value="occurrence_count">Por quantidade de meses</option>
                    </select>
                  </label>

                  {form.recurrenceEndMode === "until_date" ? (
                    <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                      <span>Repetir ate</span>
                      <input
                        className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                        onChange={(event) =>
                          setForm((current) => ({
                            ...current,
                            recurrenceUntilDate: event.target.value,
                          }))
                        }
                        type="date"
                        value={form.recurrenceUntilDate}
                      />
                    </label>
                  ) : null}

                  {form.recurrenceEndMode === "occurrence_count" ? (
                    <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                      <span>Quantidade de meses</span>
                      <input
                        className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                        min="1"
                        onChange={(event) =>
                          setForm((current) => ({
                            ...current,
                            recurrenceCount: event.target.value,
                          }))
                        }
                        step="1"
                        type="number"
                        value={form.recurrenceCount}
                      />
                    </label>
                  ) : null}
                </div>
              ) : null}

              {form.kind === "installment" ? (
                <div className="space-y-4 rounded-[24px] border border-[var(--color-line)] bg-white/70 p-4">
                  <div className="text-sm text-[var(--color-muted)]">
                    Use parcelamento para compromissos com fim conhecido, como 3/12 ou 10/24.
                  </div>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Total de parcelas</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      min="2"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          installmentCount: event.target.value,
                        }))
                      }
                      step="1"
                      type="number"
                      value={form.installmentCount}
                    />
                  </label>
                </div>
              ) : null}

              <button
                className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                disabled={isSubmitting}
                type="submit"
              >
                {isSubmitting ? "Salvando conta..." : "Criar conta a pagar"}
              </button>
            </form>
          </section>

          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Agenda do mes
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Contas encontradas
                </h2>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                {bills.length} itens
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {bills.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma conta a pagar encontrada para os filtros atuais. Use o formulario ao lado para registrar o primeiro vencimento ou ajuste os filtros para rever outro mes.
                </div>
              ) : (
                bills.map((bill) => {
                  const billKind = getBillKind(bill);

                  return (
                    <article
                      className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                      key={bill.id}
                    >
                      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
                        <div className="space-y-3">
                          <div className="flex flex-wrap items-center gap-3">
                            <div className="text-base font-semibold text-[var(--color-foreground)]">
                              {bill.description}
                            </div>
                            <span
                              className={`rounded-full border px-3 py-1 text-xs font-semibold ${billKindStyles[billKind]}`}
                            >
                              {billKindLabels[billKind]}
                            </span>
                            <span
                              className={`rounded-full border px-3 py-1 text-xs font-semibold ${statusStyles[bill.status]}`}
                            >
                              {statusLabels[bill.status]}
                            </span>
                          </div>

                          <div className="flex flex-wrap gap-2 text-xs text-[var(--color-muted)]">
                            <span>{getBillCadenceLabel(bill)}</span>
                            <span>|</span>
                            <span>Vence em {formatDate(bill.dueOn)}</span>
                            <span>|</span>
                            <span>Criada em {formatDateTime(bill.createdAtUtc)}</span>
                            {bill.paidAtUtc ? (
                              <>
                                <span>|</span>
                                <span>Paga em {formatDateTime(bill.paidAtUtc)}</span>
                              </>
                            ) : null}
                          </div>
                        </div>

                        <div className="flex flex-col items-start gap-3 md:items-end">
                          <div className="text-lg font-semibold text-[var(--color-foreground)]">
                            {formatCurrency(bill.amount)}
                          </div>
                          <button
                            className="rounded-2xl border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)] disabled:cursor-not-allowed disabled:opacity-70"
                            disabled={actionBillId === bill.id}
                            onClick={() => handleTogglePayment(bill)}
                            type="button"
                          >
                            {actionBillId === bill.id
                              ? "Atualizando..."
                              : bill.isPaid
                                ? "Desmarcar pagamento"
                                : "Marcar como paga"}
                          </button>
                        </div>
                      </div>
                    </article>
                  );
                })
              )}
            </div>
          </section>
        </div>
      )}
    </AppShell>
  );
}
