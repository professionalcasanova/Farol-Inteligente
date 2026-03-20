"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  ApiError,
  createBill,
  listBills,
  payBill,
  type BillResponse,
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

type BillFormState = {
  description: string;
  amount: string;
  dueOn: string;
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

function getDefaultDueOn(monthValue: string) {
  const today = getCurrentDateInputValue();
  return today.startsWith(monthValue) ? today : `${monthValue}-01`;
}

export default function BillsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [statusFilter, setStatusFilter] = useState<"" | BillStatus>("");
  const [bills, setBills] = useState<BillResponse[]>([]);
  const [form, setForm] = useState<BillFormState>(() => ({
    description: "",
    amount: "",
    dueOn: getDefaultDueOn(getCurrentMonthInputValue()),
  }));
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
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            caughtError instanceof Error
              ? caughtError.message
              : "Nao foi possivel carregar as bills.",
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
      await createBill(session.accessToken, {
        description: form.description,
        amount: Number(form.amount),
        dueOn: form.dueOn,
      });

      await refreshBills();
      setForm({
        description: "",
        amount: "",
        dueOn: getDefaultDueOn(monthValue),
      });
      setSuccess("Bill criada com sucesso.");
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout("session-expired");
        return;
      }

      setFormError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel criar a bill.",
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
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout("session-expired");
        return;
      }

      setFormError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel atualizar a bill.",
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
            label="Mes das bills"
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
          <Link
            className="rounded-full border border-[var(--color-line)] px-4 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
            href="/dashboard"
          >
            Voltar ao dashboard
          </Link>
        </div>
      }
      description="Acompanhe vencimentos, destaque atrasos e marque pagamentos sem sair do MVP. O dashboard reflete tudo isso no mesmo mes."
      onLogout={logout}
      session={session}
      title="Bills e vencimentos"
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
        <LoadingScreen message="Carregando bills do mes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar as bills"
        />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[0.92fr_1.08fr]">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Nova bill
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Registrar vencimento
            </h2>

            <form className="mt-6 space-y-4" onSubmit={handleCreateBill}>
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

              <button
                className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                disabled={isSubmitting}
                type="submit"
              >
                {isSubmitting ? "Salvando bill..." : "Criar bill"}
              </button>
            </form>
          </section>

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Agenda do mes
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Bills encontradas
                </h2>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                {bills.length} itens
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {bills.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma bill encontrada para os filtros atuais. Use o
                  formulario ao lado para registrar o primeiro vencimento ou
                  ajuste os filtros para rever outro mes.
                </div>
              ) : (
                bills.map((bill) => (
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
                            className={`rounded-full border px-3 py-1 text-xs font-semibold ${statusStyles[bill.status]}`}
                          >
                            {statusLabels[bill.status]}
                          </span>
                        </div>

                        <div className="flex flex-wrap gap-2 text-xs text-[var(--color-muted)]">
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
                ))
              )}
            </div>
          </section>
        </div>
      )}
    </AppShell>
  );
}
