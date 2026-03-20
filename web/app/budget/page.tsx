"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  ApiError,
  getMonthlyBudget,
  listCategories,
  saveMonthlyBudget,
  type CategoryResponse,
  type MonthlyBudgetResponse,
} from "@/lib/api";
import {
  formatCurrency,
  getCurrentMonthInputValue,
  parseMonthInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

type BudgetRow = {
  id: number;
  categoryId: string;
  planned: string;
};

export default function BudgetPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [expenseCategories, setExpenseCategories] = useState<CategoryResponse[]>([]);
  const [budget, setBudget] = useState<MonthlyBudgetResponse | null>(null);
  const [rows, setRows] = useState<BudgetRow[]>([
    { id: 1, categoryId: "", planned: "" },
  ]);
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const rowSeedRef = useRef(2);
  const [reloadKey, setReloadKey] = useState(0);

  const monthAndYear = useMemo(
    () => parseMonthInputValue(monthValue),
    [monthValue],
  );

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
        const [categoriesResponse, budgetResponse] = await Promise.all([
          listCategories(accessToken),
          getMonthlyBudget(accessToken, monthAndYear.month, monthAndYear.year),
        ]);

        if (isCancelled) {
          return;
        }

        const filteredCategories = categoriesResponse.filter(
          (category) => category.type === 2,
        );

        setExpenseCategories(filteredCategories);
        setBudget(budgetResponse);

        if (budgetResponse.categories.length > 0) {
          setRows(
            budgetResponse.categories.map((item) => ({
              id: rowSeedRef.current++,
              categoryId: item.categoryId,
              planned: String(item.planned),
            })),
          );
        } else {
          setRows([
            {
              id: rowSeedRef.current++,
              categoryId: filteredCategories[0]?.id ?? "",
              planned: "",
            },
          ]);
        }
      } catch (caughtError) {
        if (caughtError instanceof ApiError && caughtError.status === 401) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            caughtError instanceof Error
              ? caughtError.message
              : "Nao foi possivel carregar o orcamento.",
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
  }, [logout, monthAndYear.month, monthAndYear.year, reloadKey, session]);

  function addRow() {
    setRows((current) => [
      ...current,
      {
        id: rowSeedRef.current++,
        categoryId: expenseCategories[0]?.id ?? "",
        planned: "",
      },
    ]);
  }

  function removeRow(id: number) {
    setRows((current) => {
      const nextRows = current.filter((row) => row.id !== id);

      return nextRows.length > 0
        ? nextRows
        : [
            {
              id: rowSeedRef.current++,
              categoryId: expenseCategories[0]?.id ?? "",
              planned: "",
            },
          ];
    });
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const accessToken = session.accessToken;
    const categories = rows
      .filter((row) => row.categoryId && row.planned)
      .map((row) => ({
        categoryId: row.categoryId,
        planned: Number(row.planned),
      }));

    const hasDuplicateCategories =
      new Set(categories.map((item) => item.categoryId)).size !== categories.length;

    if (hasDuplicateCategories) {
      setFormError("Cada categoria pode aparecer apenas uma vez no orcamento.");
      setSuccess("");
      return;
    }

    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      const response = await saveMonthlyBudget(accessToken, {
        month: monthAndYear.month,
        year: monthAndYear.year,
        categories,
      });

      setBudget(response);
      setRows(
        response.categories.length > 0
          ? response.categories.map((item) => ({
              id: rowSeedRef.current++,
              categoryId: item.categoryId,
              planned: String(item.planned),
            }))
          : [
              {
                id: rowSeedRef.current++,
                categoryId: expenseCategories[0]?.id ?? "",
                planned: "",
              },
            ],
      );
      setSuccess("Orcamento salvo com sucesso.");
    } catch (caughtError) {
      if (caughtError instanceof ApiError && caughtError.status === 401) {
        logout("session-expired");
        return;
      }

      setFormError(
        caughtError instanceof Error
          ? caughtError.message
          : "Nao foi possivel salvar o orcamento.",
      );
    } finally {
      setIsSubmitting(false);
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
            label="Mes do orcamento"
            onChange={setMonthValue}
            value={monthValue}
          />
          <Link
            className="rounded-full border border-[var(--color-line)] px-4 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
            href="/dashboard"
          >
            Voltar ao dashboard
          </Link>
        </div>
      }
      description="Monte ou substitua o orcamento mensal por categoria de despesa e acompanhe o restante em tempo real no dashboard."
      onLogout={logout}
      session={session}
      title="Orcamento mensal"
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
              Voltar ao dashboard
            </Link>
          </div>
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando orcamento do mes..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar o orcamento"
        />
      ) : !budget ? (
        <LoadErrorState
          message="O orcamento do mes nao retornou dados."
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Orcamento indisponivel"
        />
      ) : (
        <div className="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Consolidado
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Resumo do orcamento
            </h2>

            <div className="mt-6 grid gap-4 md:grid-cols-3">
              <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                  Planejado
                </div>
                <div className="mt-3 text-xl font-semibold text-[var(--color-foreground)]">
                  {formatCurrency(budget.totalPlanned)}
                </div>
              </article>
              <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                  Gasto
                </div>
                <div className="mt-3 text-xl font-semibold text-[var(--color-foreground)]">
                  {formatCurrency(budget.totalSpent)}
                </div>
              </article>
              <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-4">
                <div className="text-xs uppercase tracking-[0.18em] text-[var(--color-muted)]">
                  Restante
                </div>
                <div className="mt-3 text-xl font-semibold text-[var(--color-foreground)]">
                  {formatCurrency(budget.totalRemaining)}
                </div>
              </article>
            </div>

            <div className="mt-6 space-y-3">
              {budget.categories.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma categoria orcada neste mes ainda. Use o formulario ao
                  lado para montar o primeiro planejamento e depois volte ao
                  dashboard para acompanhar gasto, restante e dinheiro livre.
                </div>
              ) : (
                budget.categories.map((item) => (
                  <div
                    className="grid gap-3 rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 md:grid-cols-[1.1fr_0.9fr_0.9fr_0.9fr]"
                    key={item.categoryId}
                  >
                    <div className="text-sm font-semibold text-[var(--color-foreground)]">
                      {item.categoryName}
                    </div>
                    <div className="text-sm text-[var(--color-muted)]">
                      Planejado: {formatCurrency(item.planned)}
                    </div>
                    <div className="text-sm text-[var(--color-muted)]">
                      Gasto: {formatCurrency(item.spent)}
                    </div>
                    <div className="text-sm text-[var(--color-muted)]">
                      Restante: {formatCurrency(item.remaining)}
                    </div>
                  </div>
                ))
              )}
            </div>
          </section>

          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Edicao
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Criar ou substituir orcamento
                </h2>
              </div>
              <button
                className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                onClick={addRow}
                type="button"
              >
                Adicionar categoria
              </button>
            </div>

            {expenseCategories.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                Nenhuma categoria de despesa disponivel. Cadastre ou mantenha as
                categorias do sistema antes de montar o orcamento.
              </div>
            ) : (
              <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
                {rows.map((row, index) => (
                  <div
                    className="grid gap-3 rounded-[24px] border border-[var(--color-line)] bg-white p-4 md:grid-cols-[1fr_0.8fr_auto]"
                    key={row.id}
                  >
                    <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                      <span>Categoria {index + 1}</span>
                      <select
                        className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                        onChange={(event) =>
                          setRows((current) =>
                            current.map((item) =>
                              item.id === row.id
                                ? { ...item, categoryId: event.target.value }
                                : item,
                            ),
                          )
                        }
                        value={row.categoryId}
                      >
                        <option value="">Selecione uma categoria</option>
                        {expenseCategories.map((category) => (
                          <option key={category.id} value={category.id}>
                            {category.name}
                          </option>
                        ))}
                      </select>
                    </label>

                    <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                      <span>Planejado</span>
                      <input
                        className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                        min="0.01"
                        onChange={(event) =>
                          setRows((current) =>
                            current.map((item) =>
                              item.id === row.id
                                ? { ...item, planned: event.target.value }
                                : item,
                            ),
                          )
                        }
                        placeholder="350.00"
                        step="0.01"
                        type="number"
                        value={row.planned}
                      />
                    </label>

                    <div className="flex items-end">
                      <button
                        className="rounded-2xl border border-[var(--color-line)] px-4 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
                        onClick={() => removeRow(row.id)}
                        type="button"
                      >
                        Remover
                      </button>
                    </div>
                  </div>
                ))}

                <button
                  className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {isSubmitting ? "Salvando orcamento..." : "Salvar orcamento mensal"}
                </button>
              </form>
            )}
          </section>
        </div>
      )}
    </AppShell>
  );
}
