"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import { MonthPicker } from "@/components/month-picker";
import {
  applyBudgetTemplate,
  getBudgetTemplate,
  getFriendlyApiMessage,
  getMonthlyBudget,
  isUnauthorizedApiError,
  listCategories,
  saveBudgetTemplate,
  saveMonthlyBudget,
  type BudgetTemplateResponse,
  type CategoryResponse,
  type MonthlyBudgetResponse,
} from "@/lib/api";
import {
  formatCurrency,
  getCurrentMonthInputValue,
  parseMonthInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

const budgetMessageMap = {
  "One or more categories were not found.":
    "Uma ou mais categorias do orcamento nao foram encontradas.",
  "Budget categories must be expense categories.":
    "Use apenas categorias de despesa no orcamento.",
  "Budget categories cannot be duplicated in the same payload.":
    "Cada categoria pode aparecer apenas uma vez no orcamento.",
  "Planned amount must be greater than zero.":
    "Informe um valor planejado maior que zero.",
  "Month and year are invalid.": "O mes e o ano informados sao invalidos.",
} as const;

type BudgetRow = {
  id: number;
  categoryId: string;
  planned: string;
};

function createBudgetRow(id: number, categoryId = "", planned = ""): BudgetRow {
  return {
    id,
    categoryId,
    planned,
  };
}

function createRowsFromBudget(
  nextId: () => number,
  categories: Array<{ categoryId: string; planned: number }>,
  defaultCategoryId: string,
) {
  if (categories.length === 0) {
    return [createBudgetRow(nextId(), defaultCategoryId, "")];
  }

  return categories.map((item) =>
    createBudgetRow(nextId(), item.categoryId, String(item.planned)),
  );
}

export default function BudgetPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [monthValue, setMonthValue] = useState(getCurrentMonthInputValue());
  const [expenseCategories, setExpenseCategories] = useState<CategoryResponse[]>([]);
  const [budget, setBudget] = useState<MonthlyBudgetResponse | null>(null);
  const [template, setTemplate] = useState<BudgetTemplateResponse | null>(null);
  const [rows, setRows] = useState<BudgetRow[]>([createBudgetRow(1)]);
  const [templateRows, setTemplateRows] = useState<BudgetRow[]>([
    createBudgetRow(1),
  ]);
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [templateError, setTemplateError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSavingTemplate, setIsSavingTemplate] = useState(false);
  const [isApplyingTemplate, setIsApplyingTemplate] = useState(false);
  const rowSeedRef = useRef(2);
  const templateRowSeedRef = useRef(2);
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
        const [categoriesResponse, budgetResponse, templateResponse] =
          await Promise.all([
            listCategories(accessToken),
            getMonthlyBudget(accessToken, monthAndYear.month, monthAndYear.year),
            getBudgetTemplate(accessToken),
          ]);

        if (isCancelled) {
          return;
        }

        const filteredCategories = categoriesResponse.filter(
          (category) => category.type === 2,
        );
        const defaultCategoryId = filteredCategories[0]?.id ?? "";

        setExpenseCategories(filteredCategories);
        setBudget(budgetResponse);
        setTemplate(templateResponse);
        setRows(
          createRowsFromBudget(
            () => rowSeedRef.current++,
            budgetResponse.categories,
            defaultCategoryId,
          ),
        );
        setTemplateRows(
          createRowsFromBudget(
            () => templateRowSeedRef.current++,
            templateResponse.categories,
            defaultCategoryId,
          ),
        );
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel carregar o orcamento agora. Tente novamente em alguns instantes.",
              { messageMap: budgetMessageMap },
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
  }, [logout, monthAndYear.month, monthAndYear.year, reloadKey, session]);

  function createEmptyRow() {
    return createBudgetRow(rowSeedRef.current++, expenseCategories[0]?.id ?? "", "");
  }

  function createEmptyTemplateRow() {
    return createBudgetRow(
      templateRowSeedRef.current++,
      expenseCategories[0]?.id ?? "",
      "",
    );
  }

  function addMonthlyRow() {
    setRows((current) => [...current, createEmptyRow()]);
  }

  function addTemplateRow() {
    setTemplateRows((current) => [...current, createEmptyTemplateRow()]);
  }

  function removeMonthlyRow(id: number) {
    setRows((current) => {
      const nextRows = current.filter((row) => row.id !== id);
      return nextRows.length > 0 ? nextRows : [createEmptyRow()];
    });
  }

  function removeTemplateRow(id: number) {
    setTemplateRows((current) => {
      const nextRows = current.filter((row) => row.id !== id);
      return nextRows.length > 0 ? nextRows : [createEmptyTemplateRow()];
    });
  }

  function buildPayload(rowsToConvert: BudgetRow[]) {
    return rowsToConvert
      .filter((row) => row.categoryId && row.planned)
      .map((row) => ({
        categoryId: row.categoryId,
        planned: Number(row.planned),
      }));
  }

  function hasDuplicateCategories(
    categories: Array<{ categoryId: string; planned: number }>,
  ) {
    return new Set(categories.map((item) => item.categoryId)).size !== categories.length;
  }

  function resetMonthlyRows(nextBudget: MonthlyBudgetResponse) {
    setRows(
      createRowsFromBudget(
        () => rowSeedRef.current++,
        nextBudget.categories,
        expenseCategories[0]?.id ?? "",
      ),
    );
  }

  function resetTemplateRows(nextTemplate: BudgetTemplateResponse) {
    setTemplateRows(
      createRowsFromBudget(
        () => templateRowSeedRef.current++,
        nextTemplate.categories,
        expenseCategories[0]?.id ?? "",
      ),
    );
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const categories = buildPayload(rows);

    if (hasDuplicateCategories(categories)) {
      setFormError("Cada categoria pode aparecer apenas uma vez no orcamento.");
      setSuccess("");
      return;
    }

    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      const response = await saveMonthlyBudget(session.accessToken, {
        month: monthAndYear.month,
        year: monthAndYear.year,
        categories,
      });

      setBudget(response);
      resetMonthlyRows(response);
      setSuccess(
        response.categories.length === 0
          ? "Orcamento do mes limpo com sucesso. Voce pode montar um novo planejamento quando quiser."
          : "Orcamento salvo com sucesso.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel salvar o orcamento agora. Revise os dados e tente novamente.",
          { messageMap: budgetMessageMap },
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSaveTemplate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    const categories = buildPayload(templateRows);

    if (hasDuplicateCategories(categories)) {
      setTemplateError("Cada categoria pode aparecer apenas uma vez no template.");
      setSuccess("");
      return;
    }

    setIsSavingTemplate(true);
    setTemplateError("");
    setSuccess("");

    try {
      const response = await saveBudgetTemplate(session.accessToken, categories);

      setTemplate(response);
      resetTemplateRows(response);
      setSuccess(
        response.categories.length === 0
          ? "Template recorrente limpo com sucesso."
          : "Template recorrente salvo com sucesso.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setTemplateError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel salvar o template recorrente agora. Revise os dados e tente novamente.",
          { messageMap: budgetMessageMap },
        ),
      );
    } finally {
      setIsSavingTemplate(false);
    }
  }

  async function handleApplyTemplate() {
    if (!session) {
      return;
    }

    setIsApplyingTemplate(true);
    setTemplateError("");
    setFormError("");
    setSuccess("");

    try {
      const response = await applyBudgetTemplate(session.accessToken, {
        month: monthAndYear.month,
        year: monthAndYear.year,
      });

      setBudget(response);
      resetMonthlyRows(response);
      setSuccess("Template aplicado ao mes com sucesso.");
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setTemplateError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel aplicar o template ao mes agora. Tente novamente.",
          { messageMap: budgetMessageMap },
        ),
      );
    } finally {
      setIsApplyingTemplate(false);
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
        </div>
      }
      description="Monte o orcamento do mes e mantenha um template recorrente para acelerar os meses seguintes."
      onLogout={logout}
      session={session}
      title="Orcamento mensal"
    >
      {formError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {formError}
        </div>
      ) : null}

      {templateError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(255,247,237,0.95)] px-5 py-4 text-sm text-[color:#9a5700]">
          {templateError}
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
      ) : !budget || !template ? (
        <LoadErrorState
          message="Os dados de planejamento nao retornaram corretamente."
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Planejamento indisponivel"
        />
      ) : (
        <div className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.92fr)_minmax(0,1.08fr)]">
          <div className="space-y-8">
            <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
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
            </section>

            <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
              <div className="flex items-end justify-between gap-4">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    Template recorrente
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Base para os proximos meses
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                    Guarde aqui as categorias que se repetem. Depois aplique esse template ao mes quando quiser.
                  </p>
                </div>
                <button
                  className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isApplyingTemplate}
                  onClick={handleApplyTemplate}
                  type="button"
                >
                  {isApplyingTemplate ? "Aplicando..." : "Aplicar ao mes"}
                </button>
              </div>

              <div className="mt-6 rounded-[24px] border border-[var(--color-line)] bg-white p-5">
                <div className="flex items-center justify-between gap-4">
                  <div className="text-sm text-[var(--color-muted)]">
                    Total recorrente:{" "}
                    <span className="font-semibold text-[var(--color-foreground)]">
                      {formatCurrency(template.totalPlanned)}
                    </span>
                  </div>
                  <button
                    className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-accent-soft)]"
                    onClick={addTemplateRow}
                    type="button"
                  >
                    Adicionar categoria
                  </button>
                </div>

                <form className="mt-5 space-y-4" onSubmit={handleSaveTemplate}>
                  {templateRows.map((row, index) => (
                    <div
                      className="grid gap-3 rounded-[24px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4 md:grid-cols-[1fr_0.8fr_auto]"
                      key={row.id}
                    >
                      <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                        <span>Categoria base {index + 1}</span>
                        <select
                          className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                          onChange={(event) =>
                            setTemplateRows((current) =>
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
                            setTemplateRows((current) =>
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
                          onClick={() => removeTemplateRow(row.id)}
                          type="button"
                        >
                          Remover
                        </button>
                      </div>
                    </div>
                  ))}

                  <button
                    className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                    disabled={isSavingTemplate}
                    type="submit"
                  >
                    {isSavingTemplate ? "Salvando template..." : "Salvar template recorrente"}
                  </button>
                </form>
              </div>
            </section>
          </div>

          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Edicao do mes
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Criar, substituir ou limpar orcamento
                </h2>
                <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                  O template recorrente nao substitui este mes automaticamente. Ajuste o snapshot do mes quando precisar.
                </p>
              </div>
              <button
                className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                onClick={addMonthlyRow}
                type="button"
              >
                Adicionar categoria
              </button>
            </div>

            {expenseCategories.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                Nenhuma categoria de despesa disponivel. Cadastre ou mantenha as categorias do sistema antes de montar o orcamento.
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
                        onClick={() => removeMonthlyRow(row.id)}
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
