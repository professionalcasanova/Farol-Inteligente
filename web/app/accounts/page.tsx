"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { AppShell } from "@/components/app-shell";
import { LoadErrorState } from "@/components/load-error-state";
import { LoadingScreen } from "@/components/loading-screen";
import {
  accountTypeOptions,
  createAccount,
  getFriendlyApiMessage,
  isUnauthorizedApiError,
  listAccounts,
  updateAccount,
  type AccountResponse,
  type FinancialAccountType,
} from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import { useProtectedSession } from "@/lib/use-protected-session";

type AccountFormState = {
  name: string;
  type: FinancialAccountType;
  isActive: boolean;
};

const defaultFormState: AccountFormState = {
  name: "",
  type: 2,
  isActive: true,
};

const accountTypeLabels = Object.fromEntries(
  accountTypeOptions.map((option) => [option.value, option.label]),
) as Record<FinancialAccountType, string>;

const accountMessageMap = {
  "Name is required.": "Informe o nome da conta para continuar.",
  "Financial account name is required.": "Informe o nome da conta para continuar.",
  "Financial account name cannot exceed 120 characters.":
    "O nome da conta ficou longo demais. Tente um nome menor.",
  "Financial account type is invalid.": "Selecione um tipo de conta valido.",
  "Financial account was not found.": "A conta selecionada nao foi encontrada.",
} as const;

function buildEditFormState(account: AccountResponse): AccountFormState {
  return {
    name: account.name,
    type: account.type,
    isActive: account.isActive,
  };
}

export default function AccountsPage() {
  const { session, isLoading, logout } = useProtectedSession();
  const [accounts, setAccounts] = useState<AccountResponse[]>([]);
  const [form, setForm] = useState<AccountFormState>(defaultFormState);
  const [editingAccountId, setEditingAccountId] = useState<string | null>(null);
  const [loadError, setLoadError] = useState("");
  const [formError, setFormError] = useState("");
  const [success, setSuccess] = useState("");
  const [isFetching, setIsFetching] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [actionAccountId, setActionAccountId] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  const activeCount = useMemo(
    () => accounts.filter((account) => account.isActive).length,
    [accounts],
  );
  const inactiveCount = accounts.length - activeCount;
  const editingAccount =
    accounts.find((account) => account.id === editingAccountId) ?? null;
  const isEditing = editingAccountId !== null;

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
        const response = await listAccounts(accessToken);

        if (isCancelled) {
          return;
        }

        setAccounts(response);
      } catch (caughtError) {
        if (isUnauthorizedApiError(caughtError)) {
          logout("session-expired");
          return;
        }

        if (!isCancelled) {
          setLoadError(
            getFriendlyApiMessage(
              caughtError,
              "Nao foi possivel carregar as contas agora. Tente novamente em alguns instantes.",
              { messageMap: accountMessageMap },
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

  function resetForm() {
    setEditingAccountId(null);
    setForm(defaultFormState);
  }

  function startEditing(account: AccountResponse) {
    setEditingAccountId(account.id);
    setForm(buildEditFormState(account));
    setFormError("");
    setSuccess("");
  }

  async function refreshAccounts(accessToken: string) {
    const response = await listAccounts(accessToken);
    setAccounts(response);
    return response;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!session) {
      return;
    }

    setIsSubmitting(true);
    setFormError("");
    setSuccess("");

    try {
      if (editingAccountId) {
        await updateAccount(session.accessToken, editingAccountId, {
          name: form.name,
          type: form.type,
          isActive: form.isActive,
        });

        const nextAccounts = await refreshAccounts(session.accessToken);
        const updatedAccount = nextAccounts.find((account) => account.id === editingAccountId);

        if (updatedAccount) {
          setForm(buildEditFormState(updatedAccount));
        }

        setSuccess(
          form.isActive
            ? "Conta atualizada com sucesso."
            : "Conta atualizada com sucesso. Como ela esta inativa, novos lancamentos e importacoes deixam de usar essa conta.",
        );
      } else {
        const createdAccount = await createAccount(session.accessToken, {
          name: form.name,
          type: form.type,
        });

        await refreshAccounts(session.accessToken);
        setSuccess(
          `Conta criada com sucesso. ${createdAccount.name} ja pode receber novos lancamentos e importacoes.`,
        );
        resetForm();
      }
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          isEditing
            ? "Nao foi possivel salvar a conta agora. Revise os dados e tente novamente."
            : "Nao foi possivel criar a conta agora. Revise os dados e tente novamente.",
          { messageMap: accountMessageMap },
        ),
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleToggleAccount(account: AccountResponse) {
    if (!session) {
      return;
    }

    const nextIsActive = !account.isActive;

    setActionAccountId(account.id);
    setFormError("");
    setSuccess("");

    try {
      await updateAccount(session.accessToken, account.id, {
        name: account.name,
        type: account.type,
        isActive: nextIsActive,
      });

      const nextAccounts = await refreshAccounts(session.accessToken);
      const updatedAccount = nextAccounts.find((item) => item.id === account.id);

      if (editingAccountId === account.id && updatedAccount) {
        setForm(buildEditFormState(updatedAccount));
      }

      setSuccess(
        nextIsActive
          ? "Conta reativada. Ela volta a aparecer em novos lancamentos e importacoes."
          : "Conta desativada. Ela sai de novos lancamentos e importacoes, mas continua visivel no historico.",
      );
    } catch (caughtError) {
      if (isUnauthorizedApiError(caughtError)) {
        logout("session-expired");
        return;
      }

      setFormError(
        getFriendlyApiMessage(
          caughtError,
          "Nao foi possivel atualizar o estado da conta agora. Tente novamente em instantes.",
          { messageMap: accountMessageMap },
        ),
      );
    } finally {
      setActionAccountId(null);
    }
  }

  if (isLoading || !session) {
    return <LoadingScreen />;
  }

  return (
    <AppShell
      description="Gerencie nome, tipo e estado ativo das suas contas. Contas inativas saem de novos lancamentos e importacoes, mas continuam visiveis no historico."
      onLogout={logout}
      session={session}
      title="Contas financeiras"
    >
      {formError ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(185,28,28,0.14)] bg-[color:rgba(254,226,226,0.8)] px-5 py-4 text-sm text-red-700">
          {formError}
        </div>
      ) : null}

      {success ? (
        <div className="mb-6 rounded-[24px] border border-[color:rgba(29,130,93,0.16)] bg-[color:rgba(220,252,231,0.8)] px-5 py-4 text-sm text-green-700">
          {success}
        </div>
      ) : null}

      {isFetching ? (
        <LoadingScreen message="Carregando contas..." />
      ) : loadError ? (
        <LoadErrorState
          message={loadError}
          onRetry={() => setReloadKey((current) => current + 1)}
          title="Nao foi possivel carregar as contas"
        />
      ) : (
        <div className="grid items-start gap-8 xl:grid-cols-[minmax(0,0.96fr)_minmax(320px,0.84fr)]">
          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
              <div>
                <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
                  Gestao de contas
                </div>
                <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
                  Suas contas hoje
                </h2>
                <p className="mt-3 max-w-2xl text-sm leading-6 text-[var(--color-muted)]">
                  Aqui voce controla quais contas seguem disponiveis para novos lancamentos e
                  importacoes. O historico continua preservado mesmo quando uma conta fica
                  inativa.
                </p>
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-[20px] border border-[var(--color-line)] bg-white px-4 py-3 text-sm">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    Ativas
                  </div>
                  <div className="mt-2 text-xl font-semibold text-[var(--color-foreground)]">
                    {activeCount}
                  </div>
                </div>
                <div className="rounded-[20px] border border-[var(--color-line)] bg-white px-4 py-3 text-sm">
                  <div className="text-xs uppercase tracking-[0.16em] text-[var(--color-muted)]">
                    Inativas
                  </div>
                  <div className="mt-2 text-xl font-semibold text-[var(--color-foreground)]">
                    {inactiveCount}
                  </div>
                </div>
              </div>
            </div>

            {accounts.length === 0 ? (
              <div className="mt-6 rounded-[24px] border border-dashed border-[var(--color-line)] px-5 py-6 text-sm leading-6 text-[var(--color-muted)]">
                Voce ainda nao tem contas cadastradas. Crie a primeira aqui e depois volte ao{" "}
                <Link className="font-semibold text-[var(--color-accent)]" href="/dashboard">
                  dashboard
                </Link>{" "}
                para registrar movimentacoes.
              </div>
            ) : (
              <div className="mt-6 space-y-4">
                {accounts.map((account) => {
                  const isBusy = actionAccountId === account.id;

                  return (
                    <article
                      className="rounded-[24px] border border-[var(--color-line)] bg-white p-5"
                      key={account.id}
                    >
                      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-3">
                            <div className="text-lg font-semibold text-[var(--color-foreground)]">
                              {account.name}
                            </div>
                            <span
                              className={`rounded-full border px-3 py-1 text-xs font-semibold ${
                                account.isActive
                                  ? "border-[color:rgba(15,118,110,0.14)] bg-[var(--color-accent-soft)] text-[var(--color-foreground)]"
                                  : "border-[color:rgba(217,119,6,0.18)] bg-[color:rgba(255,247,237,0.95)] text-[var(--color-warm)]"
                              }`}
                            >
                              {account.isActive ? "Ativa" : "Inativa"}
                            </span>
                          </div>
                          <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-[var(--color-muted)]">
                            <span>{accountTypeLabels[account.type]}</span>
                            <span>•</span>
                            <span>Criada em {formatDateTime(account.createdAtUtc)}</span>
                          </div>
                        </div>

                        <div className="flex flex-wrap gap-3">
                          <button
                            className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)]"
                            onClick={() => startEditing(account)}
                            type="button"
                          >
                            Editar
                          </button>
                          <button
                            className="rounded-full border border-[var(--color-line)] px-4 py-2 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-[var(--color-panel)] disabled:cursor-not-allowed disabled:opacity-70"
                            disabled={isBusy}
                            onClick={() => handleToggleAccount(account)}
                            type="button"
                          >
                            {isBusy
                              ? "Salvando..."
                              : account.isActive
                                ? "Desativar"
                                : "Reativar"}
                          </button>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>

          <section className="min-w-0 rounded-[28px] border border-[var(--color-line)] bg-[var(--color-panel)] p-6">
            <div className="text-xs font-semibold uppercase tracking-[0.22em] text-[var(--color-accent)]">
              {isEditing ? "Editar conta" : "Nova conta"}
            </div>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.03em] text-[var(--color-foreground)]">
              {isEditing ? "Ajustar conta existente" : "Adicionar conta"}
            </h2>
            <p className="mt-3 text-sm leading-6 text-[var(--color-muted)]">
              {isEditing
                ? "Revise nome, tipo e estado ativo. Contas inativas deixam de aparecer em novos lancamentos e importacoes."
                : "Crie outra conta para separar caixa, conta bancaria, cartao ou outros contextos do mes."}
            </p>

            <form className="mt-6 space-y-4" onSubmit={handleSubmit}>
              <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                <span>Nome da conta</span>
                <input
                  className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      name: event.target.value,
                    }))
                  }
                  placeholder="Conta corrente, cartao, reserva..."
                  value={form.name}
                />
              </label>

              <label className="flex flex-col gap-2 text-sm text-[var(--color-muted)]">
                <span>Tipo da conta</span>
                <select
                  className="rounded-2xl border border-[var(--color-line)] bg-white px-4 py-3 text-[var(--color-foreground)] outline-none transition focus:border-[var(--color-accent)]"
                  onChange={(event) =>
                    setForm((current) => ({
                      ...current,
                      type: Number(event.target.value) as FinancialAccountType,
                    }))
                  }
                  value={form.type}
                >
                  {accountTypeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>

              {isEditing ? (
                <label className="flex items-start gap-3 rounded-[24px] border border-[var(--color-line)] bg-white px-4 py-4 text-sm text-[var(--color-muted)]">
                  <input
                    checked={form.isActive}
                    className="mt-1 h-4 w-4 rounded border-[var(--color-line)] text-[var(--color-accent)] focus:ring-[var(--color-accent)]"
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        isActive: event.target.checked,
                      }))
                    }
                    type="checkbox"
                  />
                  <span>
                    Manter esta conta ativa para novos lancamentos e importacoes.
                  </span>
                </label>
              ) : (
                <div className="rounded-[24px] border border-[var(--color-line)] bg-white px-4 py-4 text-sm leading-6 text-[var(--color-muted)]">
                  Novas contas entram ativas. Se precisar pausar uma conta depois, voce pode
                  desativar sem perder o historico.
                </div>
              )}

              <div className="flex flex-wrap gap-3">
                <button
                  className="rounded-2xl bg-[var(--color-foreground)] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[var(--color-accent)] disabled:cursor-not-allowed disabled:opacity-70"
                  disabled={isSubmitting}
                  type="submit"
                >
                  {isSubmitting
                    ? "Salvando..."
                    : isEditing
                      ? "Salvar conta"
                      : "Criar conta"}
                </button>
                {isEditing ? (
                  <button
                    className="rounded-2xl border border-[var(--color-line)] px-5 py-3 text-sm font-medium text-[var(--color-foreground)] transition hover:bg-white"
                    onClick={resetForm}
                    type="button"
                  >
                    Cancelar edicao
                  </button>
                ) : null}
              </div>
            </form>

            {editingAccount ? (
              <div className="mt-6 rounded-[24px] border border-[var(--color-line)] bg-white px-5 py-4 text-sm leading-6 text-[var(--color-muted)]">
                Voce esta ajustando <strong className="text-[var(--color-foreground)]">{editingAccount.name}</strong>.
                {" "}Se esta conta ficar inativa, novos lancamentos e importacoes deixam de usa-la,
                mas o historico continua preservado.
              </div>
            ) : null}
          </section>
        </div>
      )}
    </AppShell>
  );
}
