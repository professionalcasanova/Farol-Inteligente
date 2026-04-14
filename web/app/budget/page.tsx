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

type SnapshotComparisonStatus =
  | "matching-base"
  | "adjusted-from-base"
  | "month-only"
  | "base-only";

type SnapshotComparisonRow = {
  categoryId: string;
  categoryName: string;
  monthPlanned: number | null;
  templatePlanned: number | null;
  spent: number;
  remaining: number | null;
  status: SnapshotComparisonStatus;
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

const monthReferenceFormatter = new Intl.DateTimeFormat("pt-BR", {
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

function formatMonthReference(month: number, year: number) {
  return monthReferenceFormatter.format(new Date(Date.UTC(year, month - 1, 1)));
}

function buildSnapshotComparison(
  budget: MonthlyBudgetResponse,
  template: BudgetTemplateResponse,
) {
  const templateByCategoryId = new Map(
    template.categories.map((item) => [item.categoryId, item]),
  );
  const snapshotCategoryIds = new Set(budget.categories.map((item) => item.categoryId));

  const snapshotRows: SnapshotComparisonRow[] = budget.categories.map((item) => {
    const templateItem = templateByCategoryId.get(item.categoryId);

    if (!templateItem) {
      return {
        categoryId: item.categoryId,
        categoryName: item.categoryName,
        monthPlanned: item.planned,
        templatePlanned: null,
        spent: item.spent,
        remaining: item.remaining,
        status: "month-only",
      };
    }

    return {
      categoryId: item.categoryId,
      categoryName: item.categoryName,
      monthPlanned: item.planned,
      templatePlanned: templateItem.planned,
      spent: item.spent,
      remaining: item.remaining,
      status:
        templateItem.planned === item.planned ? "matching-base" : "adjusted-from-base",
    };
  });

  const baseOnlyRows: SnapshotComparisonRow[] = template.categories
    .filter((item) => !snapshotCategoryIds.has(item.categoryId))
    .map((item) => ({
      categoryId: item.categoryId,
      categoryName: item.categoryName,
      monthPlanned: null,
      templatePlanned: item.planned,
      spent: 0,
      remaining: null,
      status: "base-only",
    }));

  return {
    snapshotRows,
    baseOnlyRows,
    matchingCount: snapshotRows.filter((item) => item.status === "matching-base").length,
    adjustedCount: snapshotRows.filter((item) => item.status === "adjusted-from-base")
      .length,
    monthOnlyCount: snapshotRows.filter((item) => item.status === "month-only").length,
    baseOnlyCount: baseOnlyRows.length,
  };
}

function getSnapshotStatusCopy(status: SnapshotComparisonStatus) {
  switch (status) {
    case "matching-base":
      return {
        label: "Igual a base",
        className:
          "border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] text-green-700",
      };
    case "adjusted-from-base":
      return {
        label: "Ajustada no mes",
        className:
          "border-[color:rgba(37,99,235,0.16)] bg-[color:rgba(219,234,254,0.9)] text-blue-700",
      };
    case "month-only":
      return {
        label: "Somente neste mes",
        className:
          "border-[color:rgba(124,58,237,0.16)] bg-[color:rgba(243,232,255,0.9)] text-violet-700",
      };
    case "base-only":
      return {
        label: "Ficou fora do snapshot",
        className:
          "border-[color:rgba(217,119,6,0.18)] bg-[color:rgba(255,247,237,0.95)] text-[var(--color-warm)]",
      };
  }
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
  const monthReference = useMemo(
    () => formatMonthReference(monthAndYear.month, monthAndYear.year),
    [monthAndYear.month, monthAndYear.year],
  );
  const snapshotComparison = useMemo(() => {
    if (!budget || !template) {
      return null;
    }

    return buildSnapshotComparison(budget, template);
  }, [budget, template]);

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
          ? "Snapshot do mes limpo com sucesso. Voce pode montar um novo planejamento quando quiser."
          : "Snapshot do mes salvo com sucesso.",
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
      setTemplateError("Cada categoria pode aparecer apenas uma vez no planejamento base.");
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
          ? "Planejamento base limpo com sucesso."
          : "Planejamento base salvo com sucesso.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setTemplateError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel salvar o planejamento base agora. Revise os dados e tente novamente.",
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
      setSuccess("Planejamento base aplicado ao snapshot do mes com sucesso.");
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setTemplateError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel aplicar o planejamento base ao mes agora. Tente novamente.",
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
      description="Monte o orcamento do mes e mantenha uma base recorrente para acelerar os meses seguintes."
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
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/transactions"
            >
              Revisar movimentacoes
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
        <div className="space-y-8">
          <section className="rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              Como usar
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              Base recorrente e snapshot do mes sao coisas diferentes
            </h2>
            <div className="mt-6 grid gap-4 lg:grid-cols-2">
              <article className="rounded-[24px] border border-[var(--color-line)] bg-white p-5">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                  1. Base recorrente
                </div>
                <div className="mt-3 text-lg font-semibold text-[var(--color-foreground)]">
                  Seu ponto de partida para os proximos meses
                </div>
                <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                  Salvar a base nao altera o mes atual. Ela fica guardada para ser aplicada quando voce quiser.
                </p>
              </article>

              <article className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] p-5">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                  2. Snapshot de {monthReference}
                </div>
                <div className="mt-3 text-lg font-semibold text-[var(--color-foreground)]">
                  O que realmente vale para este mes
                </div>
                <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                  Salvar o snapshot substitui apenas {monthReference}. Os proximos meses continuam seguindo a sua base recorrente.
                </p>
              </article>
            </div>
          </section>

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
              <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                    Planejamento base
                  </div>
                  <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                    Base para os proximos meses
                  </h2>
                  <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                    Guarde aqui as categorias que se repetem. Depois aplique essa base ao mes quando quiser.
                  </p>
                </div>
                <button
                  className="self-start rounded-full border border-[var(--color-line)] px-5 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isApplyingTemplate}
                  onClick={handleApplyTemplate}
                  type="button"
                >
                  {isApplyingTemplate ? "Aplicando..." : "Aplicar base ao snapshot"}
                </button>
              </div>

              <div className="mt-6 rounded-[24px] border border-[var(--color-line)] bg-white p-5">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
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
                      className="grid gap-3 rounded-[24px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,0.8fr)]"
                      key={row.id}
                    >
                      <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                        <span>Categoria da base {index + 1}</span>
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

                      <div className="flex items-end lg:col-span-2 lg:justify-end">
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
                    {isSavingTemplate ? "Salvando planejamento..." : "Salvar planejamento base"}
                  </button>
                </form>
              </div>
            </section>
          </div>

          <section
            className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6"
            id="monthly-budget-editor"
          >
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Edicao do mes
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Montar o snapshot do mes
                </h2>
                <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
                  Aqui voce decide o que ficou valendo em {monthReference}, mesmo que seja diferente da base recorrente.
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

            <div className="mt-6 rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-5 py-4 text-sm text-[var(--color-foreground)]">
              <div className="font-medium">
                Voce esta editando o snapshot de {monthReference}.
              </div>
              <div className="mt-1 text-[var(--color-muted)]">
                Salvar aqui substitui apenas este mes. A base recorrente continua intacta. Para corrigir lancamentos individuais, use{" "}
                <Link
                  className="font-semibold text-[var(--color-foreground)] underline-offset-2 hover:underline"
                  href="/transactions"
                >
                  Movimentacoes
                </Link>
                .
              </div>
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
                  {isSubmitting ? "Salvando snapshot..." : "Salvar snapshot mensal"}
                </button>
              </form>
            )}

            <div className="mt-6 rounded-[24px] border border-[var(--color-line)] bg-white p-5">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div>
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-[var(--color-accent)]">
                    Leitura rapida
                  </div>
                  <h3 className="mt-2 text-xl font-semibold text-[var(--color-foreground)]">
                    Como o snapshot de {monthReference} ficou
                  </h3>
                  <p className="mt-2 text-sm leading-6 text-[var(--color-muted)]">
                    Este quadro mostra o que seguiu igual a base, o que foi ajustado e o que ficou so neste mes.
                  </p>
                </div>
                <div className="text-sm text-[var(--color-muted)]">
                  Snapshot salvo:{" "}
                  <span className="font-semibold text-[var(--color-foreground)]">
                    {budget.categories.length} categoria{budget.categories.length === 1 ? "" : "s"}
                  </span>
                </div>
              </div>

              <div className="mt-5 grid gap-3 md:grid-cols-4">
                <article className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    Iguais a base
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-[var(--color-foreground)]">
                    {snapshotComparison?.matchingCount ?? 0}
                  </div>
                </article>
                <article className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    Ajustadas no mes
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-[var(--color-foreground)]">
                    {snapshotComparison?.adjustedCount ?? 0}
                  </div>
                </article>
                <article className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    So neste mes
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-[var(--color-foreground)]">
                    {snapshotComparison?.monthOnlyCount ?? 0}
                  </div>
                </article>
                <article className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    Fora do snapshot
                  </div>
                  <div className="mt-2 text-2xl font-semibold text-[var(--color-foreground)]">
                    {snapshotComparison?.baseOnlyCount ?? 0}
                  </div>
                </article>
              </div>

              {budget.categories.length === 0 ? (
                <div className="mt-5 rounded-[20px] border border-dashed border-[var(--color-line)] px-4 py-5 text-sm leading-6 text-[var(--color-muted)]">
                  Ainda nao existe snapshot salvo para {monthReference}. Se fizer sentido, aplique a base recorrente e ajuste so o que mudou neste mes.
                </div>
              ) : (
                <div className="mt-5 space-y-3">
                  {snapshotComparison?.snapshotRows.map((item) => {
                    const statusCopy = getSnapshotStatusCopy(item.status);

                    return (
                      <article
                        className="rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4"
                        key={item.categoryId}
                      >
                        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                          <div>
                            <div className="text-base font-semibold text-[var(--color-foreground)]">
                              {item.categoryName}
                            </div>
                            <div className="mt-1 text-sm text-[var(--color-muted)]">
                              Snapshot: {formatCurrency(item.monthPlanned ?? 0)}
                              {item.templatePlanned !== null ? (
                                <> • Base: {formatCurrency(item.templatePlanned)}</>
                              ) : (
                                " • Sem referencia na base"
                              )}
                            </div>
                          </div>
                          <span
                            className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${statusCopy.className}`}
                          >
                            {statusCopy.label}
                          </span>
                        </div>

                        <div className="mt-3 grid gap-3 text-sm text-[var(--color-muted)] md:grid-cols-2">
                          <div>
                            Gasto no mes:{" "}
                            <span className="font-semibold text-[var(--color-foreground)]">
                              {formatCurrency(item.spent)}
                            </span>
                          </div>
                          <div>
                            Restante no snapshot:{" "}
                            <span className="font-semibold text-[var(--color-foreground)]">
                              {formatCurrency(item.remaining ?? 0)}
                            </span>
                          </div>
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}

              {snapshotComparison && snapshotComparison.baseOnlyRows.length > 0 ? (
                <div className="mt-5 rounded-[20px] border border-[var(--color-line)] bg-[var(--color-panel)] p-4">
                  <div className="text-sm font-semibold text-[var(--color-foreground)]">
                    Categorias da base que ficaram fora de {monthReference}
                  </div>
                  <div className="mt-3 space-y-3">
                    {snapshotComparison.baseOnlyRows.map((item) => {
                      const statusCopy = getSnapshotStatusCopy(item.status);

                      return (
                        <div
                          className="flex flex-col gap-2 rounded-[18px] border border-[var(--color-line)] bg-white px-4 py-3 md:flex-row md:items-center md:justify-between"
                          key={item.categoryId}
                        >
                          <div>
                            <div className="font-medium text-[var(--color-foreground)]">
                              {item.categoryName}
                            </div>
                            <div className="text-sm text-[var(--color-muted)]">
                              Base recorrente: {formatCurrency(item.templatePlanned ?? 0)}
                            </div>
                          </div>
                          <span
                            className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${statusCopy.className}`}
                          >
                            {statusCopy.label}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ) : null}
            </div>
          </section>
        </div>
        </div>
      )}
    </AppShell>
  );
}
