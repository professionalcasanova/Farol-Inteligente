"use client";

import Link from "next/link";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import {
  createTransaction,
  deleteTransaction,
  getFriendlyApiMessage,
  isUnauthorizedApiError,
  listAccounts,
  listCategories,
  listTransactions,
  transactionTypeOptions,
  updateTransaction,
  type AccountResponse,
  type CategoryResponse,
  type TransactionResponse,
  type TransactionType,
} from "@/lib/api";
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  getCurrentDateInputValue,
} from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

const transactionMessageMap = {
  "Financial account was not found.": "A conta selecionada não foi encontrada.",
  "Category was not found.": "A categoria selecionada não foi encontrada.",
  "Transaction amount must be greater than zero.":
    "Informe um valor maior que zero para a transação.",
  "Transaction description is required.":
    "Informe uma descrição para a transação.",
  "Transaction occurrence date is required.": "Informe a data da transação.",
} as const;

type TransactionFormState = {
  financialAccountId: string;
  categoryId: string;
  type: TransactionType;
  amount: string;
  description: string;
  occurredOn: string;
};

const defaultFormState: TransactionFormState = {
  financialAccountId: "",
  categoryId: "",
  type: 2,
  amount: "",
  description: "",
  occurredOn: getCurrentDateInputValue(),
};

function buildCreateFormState(financialAccountId = ""): TransactionFormState {
  return {
    ...defaultFormState,
    financialAccountId,
    occurredOn: getCurrentDateInputValue(),
  };
}

function buildEditFormState(transaction: TransactionResponse): TransactionFormState {
  return {
    financialAccountId: transaction.financialAccountId,
    categoryId: transaction.categoryId ?? "",
    type: transaction.type,
    amount: String(transaction.amount),
    description: transaction.description,
    occurredOn: transaction.occurredOn,
  };
}

export default function TransactionsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [categories, setCategories] = useState<CategoryResponse[]>([]);
  const [transactions, setTransactions] = useState<TransactionResponse[]>([]);
  const [form, setForm] = useState<TransactionFormState>(defaultFormState);
  const [editingTransactionId, setEditingTransactionId] = useState<string | null>(null);
  const [handledRequestedEditId, setHandledRequestedEditId] = useState<string | null>(null);
  const [requestedEditTransactionId, setRequestedEditTransactionId] = useState<string | null>(
    null,
  );
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  const visibleCategories = useMemo(
    () => categories.filter((category) => category.type === form.type),
    [categories, form.type],
  );
  const editingTransaction =
    transactions.find((transaction) => transaction.id === editingTransactionId) ?? null;
  const isEditing = editingTransactionId !== null;
  const isDashboardCorrectionFlow =
    Boolean(requestedEditTransactionId) &&
    requestedEditTransactionId === editingTransactionId;

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    setRequestedEditTransactionId(
      new URLSearchParams(window.location.search).get("edit"),
    );
  }, []);

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
        const [accountsResponse, categoriesResponse, transactionsResponse] =
          await Promise.all([
            listAccounts(accessToken),
            listCategories(accessToken),
            listTransactions(accessToken),
          ]);

        if (isCancelled) {
          return;
        }

        setAccounts(accountsResponse);
        setCategories(categoriesResponse);
        setTransactions(transactionsResponse);
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel carregar as transacoes agora. Tente novamente em alguns instantes.",
              { messageMap: transactionMessageMap },
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
  }, [logout, reloadKey, session]);

  useEffect(() => {
    setForm((current) => {
      const hasSelectedAccount = accounts.some(
        (account) => account.id === current.financialAccountId,
      );

      if (accounts.length === 0) {
        return current.financialAccountId
          ? { ...current, financialAccountId: "" }
          : current;
      }

      if (hasSelectedAccount) {
        return current;
      }

      if (accounts.length === 1) {
        return {
          ...current,
          financialAccountId: accounts[0].id,
        };
      }

      return current.financialAccountId
        ? { ...current, financialAccountId: "" }
        : current;
    });
  }, [accounts]);

  useEffect(() => {
    if (
      form.categoryId &&
      !visibleCategories.some((category) => category.id === form.categoryId)
    ) {
      setForm((current) => ({
        ...current,
        categoryId: "",
      }));
    }
  }, [form.categoryId, visibleCategories]);

  useEffect(() => {
    if (
      !requestedEditTransactionId ||
      editingTransactionId === requestedEditTransactionId ||
      handledRequestedEditId === requestedEditTransactionId
    ) {
      return;
    }

    const transactionToEdit = transactions.find(
      (transaction) => transaction.id === requestedEditTransactionId,
    );

    if (!transactionToEdit) {
      return;
    }

    setHandledRequestedEditId(requestedEditTransactionId);
    startEditing(transactionToEdit);
  }, [editingTransactionId, handledRequestedEditId, requestedEditTransactionId, transactions]);

  function resetForm(financialAccountId = accounts.length === 1 ? accounts[0].id : "") {
    setEditingTransactionId(null);
    setForm(buildCreateFormState(financialAccountId));
  }

  function startEditing(transaction: TransactionResponse) {
    setEditingTransactionId(transaction.id);
    setFormError("");
    setSuccess("");
    setForm(buildEditFormState(transaction));
  }

  function handleCancelEditing() {
    setFormError("");
    setSuccess("");
    resetForm(form.financialAccountId);
  }

  async function handleDeleteEditing() {
    if (!session || !editingTransactionId || !editingTransaction) {
      return;
    }

    if (
      typeof window !== "undefined" &&
      !window.confirm(
        `Excluir "${editingTransaction.description}"? Esta ação remove a transação manual em definitivo.`,
      )
    ) {
      return;
    }

    const accessToken = session.accessToken;
    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      await deleteTransaction(accessToken, editingTransactionId);
      const transactionsResponse = await listTransactions(accessToken);
      setTransactions(transactionsResponse);
      setSuccess("Transação excluída com sucesso.");
      resetForm(form.financialAccountId);
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          "Não foi possível excluir a transação agora. Tente novamente em instantes.",
          { messageMap: transactionMessageMap },
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    if (!form.financialAccountId) {
      setFormError("Escolha a conta em que essa transação deve ser registrada.");
      return;
    }

    const accessToken = session.accessToken;
    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      const payload = {
        financialAccountId: form.financialAccountId,
        categoryId: form.categoryId || undefined,
        type: form.type,
        amount: Number(form.amount),
        description: form.description,
        occurredOn: form.occurredOn,
      };

      if (editingTransactionId) {
        await updateTransaction(accessToken, editingTransactionId, payload);
      } else {
        await createTransaction(accessToken, payload);
      }

      const transactionsResponse = await listTransactions(accessToken);
      setTransactions(transactionsResponse);
      setSuccess(
        editingTransactionId
          ? "Transação atualizada com sucesso."
          : "Transação criada com sucesso.",
      );
      resetForm(form.financialAccountId);
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          editingTransactionId
            ? "Não foi possível atualizar a transação agora. Revise os dados e tente novamente."
            : "Não foi possível registrar a transação agora. Revise os dados e tente novamente.",
          { messageMap: transactionMessageMap },
        ),
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
      description="Registre entradas e saídas sem sair do MVP. Cada lançamento já atualiza saldo, dinheiro livre e alertas."
      onLogout={logout}
      session={session}
      title="Transações"
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
              Ver impacto no dashboard
            </Link>
            <Link
              className="rounded-full border border-[color:rgba(29,130,93,0.18)] px-4 py-2 text-sm font-medium text-green-700 transition hover:bg-white"
              href="/budget"
            >
              Ajustar orçamento
            </Link>
          </div>
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando contas, categorias e transações..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Não foi possível carregar as transações"
        />
      ) : (
        <div className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.94fr)_minmax(0,1.06fr)]">
          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              {isEditing ? "Editando transação" : "Nova transação"}
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              {isEditing ? "Corrigir lançamento" : "Lançamento rápido"}
            </h2>

            {accounts.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                Você precisa de ao menos uma conta financeira. Crie a primeira no
                <Link className="font-semibold text-[var(--color-accent)]" href="/dashboard#quick-account">
                  {" "}dashboard
                </Link>{" "}
                e volte aqui.
              </div>
            ) : (
              <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
                {isEditing ? (
                  <div className="rounded-[24px] border border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] px-4 py-4 text-sm text-[var(--color-foreground)]">
                    <div className="font-medium">
                      Você está editando{" "}
                      <span className="font-semibold">
                        {editingTransaction?.description ?? "esta transação"}
                      </span>
                      .
                    </div>
                    <div className="mt-1 text-[var(--color-muted)]">
                      Ajuste conta, tipo, valor, data ou categoria e salve quando terminar.
                    </div>
                    {isDashboardCorrectionFlow ? (
                      <div className="mt-2 text-[var(--color-muted)]">
                        Este lançamento veio do dashboard. Corrija aqui ou exclua se foi um engano.
                      </div>
                    ) : null}
                  </div>
                ) : null}

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Conta financeira</span>
                  <select
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        financialAccountId: event.target.value,
                      }))
                    }
                    value={form.financialAccountId}
                  >
                    {accounts.length > 1 ? (
                      <option value="">Escolha a conta para continuar</option>
                    ) : null}
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </label>

                <div className="grid gap-4 md:grid-cols-2">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Tipo</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          type: Number(event.target.value) as TransactionType,
                        }))
                      }
                      value={form.type}
                    >
                      {transactionTypeOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </select>
                  </label>

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
                      placeholder="120.50"
                      step="0.01"
                      type="number"
                      value={form.amount}
                    />
                  </label>
                </div>

                <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                  <span>Descrição</span>
                  <input
                    className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        description: event.target.value,
                      }))
                    }
                    placeholder="Mercado, salário, almoço..."
                    value={form.description}
                  />
                </label>

                <div className="grid gap-4 md:grid-cols-2">
                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Data</span>
                    <input
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          occurredOn: event.target.value,
                        }))
                      }
                      type="date"
                      value={form.occurredOn}
                    />
                  </label>

                  <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                    <span>Categoria</span>
                    <select
                      className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          categoryId: event.target.value,
                        }))
                      }
                      value={form.categoryId}
                    >
                      <option value="">Sem categoria</option>
                      {visibleCategories.map((category) => (
                        <option key={category.id} value={category.id}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    <span className="text-xs leading-5 text-[var(--color-muted)]">
                      Categoria responde ao motivo da movimentação. PIX, boleto e cartão ficam para um campo futuro de meio de pagamento.
                    </span>
                  </label>
                </div>

                <div className="flex flex-col gap-3 xl:flex-row">
                  <button
                    className="w-full rounded-2xl bg-[var(--color-foreground)] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70 xl:flex-1"
                    disabled={isSubmitting}
                    type="submit"
                  >
                    {isSubmitting
                      ? "Salvando..."
                      : isEditing
                        ? "Salvar alteração"
                        : "Criar transação"}
                  </button>

                  {isEditing ? (
                    <button
                      className="w-full rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-sm font-semibold text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)] disabled:cursor-not-allowed disabled:opacity-70 xl:flex-1"
                      disabled={isSubmitting}
                      onClick={handleCancelEditing}
                      type="button"
                    >
                      Cancelar edição
                    </button>
                  ) : null}

                  {isEditing ? (
                    <button
                      className="w-full rounded-2xl border border-[color:rgba(185,28,28,0.14)] bg-white px-4 py-3 text-sm font-semibold text-red-700 transition hover:bg-[color:rgba(254,226,226,0.45)] disabled:cursor-not-allowed disabled:opacity-70 xl:flex-1"
                      disabled={isSubmitting}
                      onClick={handleDeleteEditing}
                      type="button"
                    >
                      Excluir transação
                    </button>
                  ) : null}
                </div>
              </form>
            )}
          </section>

          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex items-end justify-between gap-4">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Histórico
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Transações registradas
                </h2>
              </div>
              <div className="text-sm text-[var(--color-muted)]">
                {transactions.length} itens
              </div>
            </div>

            <div className="mt-6 space-y-3">
              {transactions.length === 0 ? (
                <div className="rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm text-[var(--color-muted)]">
                  Nenhuma transação encontrada. O primeiro lançamento já atualiza
                  o dashboard e o insight de dinheiro livre. Se quiser conferir
                  o resultado depois, volte para{" "}
                  <Link className="font-semibold text-[var(--color-accent)]" href="/dashboard">
                    Dashboard
                  </Link>
                  .
                </div>
              ) : (
                transactions.map((transaction) => {
                  const accountName =
                    accounts.find(
                      (account) => account.id === transaction.financialAccountId,
                    )?.name ?? "Conta";
                  const categoryName =
                    categories.find((category) => category.id === transaction.categoryId)
                      ?.name ?? "Sem categoria";

                  return (
                    <article
                      className="rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4"
                      key={transaction.id}
                    >
                      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                        <div>
                          <div className="text-sm font-semibold text-[var(--color-foreground)]">
                            {transaction.description}
                          </div>
                          <div className="mt-2 flex flex-wrap gap-2 text-xs text-[var(--color-muted)]">
                            <span>{accountName}</span>
                            <span>|</span>
                            <span>{categoryName}</span>
                            <span>|</span>
                            <span>{formatDate(transaction.occurredOn)}</span>
                          </div>
                        </div>
                        <div className="text-right">
                          <div
                            className={`text-base font-semibold ${
                              transaction.type === 1
                                ? "text-[var(--color-success)]"
                                : "text-[var(--color-foreground)]"
                            }`}
                          >
                            {transaction.type === 1 ? "+" : "-"}
                            {formatCurrency(transaction.amount)}
                          </div>
                          <div className="mt-2 text-xs text-[var(--color-muted)]">
                            criado em {formatDateTime(transaction.createdAtUtc)}
                          </div>
                          <button
                            className="mt-3 rounded-full border border-[var(--color-line)] px-3 py-2 text-xs font-semibold text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                            onClick={() => startEditing(transaction)}
                            type="button"
                          >
                            {editingTransactionId === transaction.id ? "Editando" : "Editar"}
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



